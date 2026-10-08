using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.SpatialExtremes;

/// <summary>
/// Verifies maximum-likelihood recovery of identifiable homogeneous spatial GEV and Gaussian-copula
/// coordinates from ten-site networks containing 100 complete row/year vectors (1,000 scalar values).
/// </summary>
/// <remarks>
/// Both fixtures use Cartesian projected coordinates and unchanged default Differential Evolution
/// settings (seed 12345). Parent recovery is judged with unregularized observed-information standard
/// errors; a singular or regularized covariance is explicit failure evidence.
/// </remarks>
[TestClass]
public class SpatialGEVMLERecoveryTests
{
    private const int ObservationCountPerSite = 100;
    private const int SiteCount = 10;
    private const int TotalScalarObservationCount = 1000;

    /// <summary>
    /// Recovers the log-location, log-scale, and identity-shape intercepts of an independent
    /// ten-site homogeneous GEV network generated with seed 54321.
    /// </summary>
    /// <remarks>
    /// The 10 by 2 coordinate matrix is the partial 25 km Cartesian grid
    /// (0,0), (25,0), (50,0), (75,0), (0,25), (25,25), (50,25), (75,25),
    /// (0,50), and (25,50). Each of the 100 complete rows contributes ten marginal GEV
    /// densities, for 1,000 scalar observations. The physical parent is location 10000,
    /// scale 3000, shape -0.1; the fitted
    /// order is [log(location), log(scale), shape].
    /// </remarks>
    [TestMethod]
    public void MLE_BasicHomogeneous_RecoversParameters()
    {
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetBasicIdentifiableData(
            nObs: ObservationCountPerSite, nSites: SiteCount,
            location: 10000, scale: 3000, shape: -0.1, seed: 54321);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        AssertObservedInformationRecovery(
            model,
            mle,
            new[] { trueParams.LocationIntercept, trueParams.ScaleIntercept, trueParams.ShapeIntercept },
            "independent homogeneous network");
    }

    /// <summary>
    /// Recovers the exponential-copula range and all homogeneous marginal coordinates from a
    /// ten-site dependent GEV network generated with seed 66666.
    /// </summary>
    /// <remarks>
    /// The 10 by 2 coordinate matrix is the partial 33.333 km Cartesian grid. The Gaussian-copula
    /// correlation is exp(-h/40). Every one of the 100 complete row/year vectors contributes ten
    /// marginal densities and one ten-dimensional copula density, for 1,000 scalar observations.
    /// The fitted order is
    /// [range, log(location), log(scale), shape].
    /// </remarks>
    [TestMethod]
    public void MLE_WithCopula_RecoversParameters()
    {
        var (data, coords, trueParams) = SyntheticSpatialGEVData.GetCopulaDataBasicExponential(
            nObs: ObservationCountPerSite, nSites: SiteCount, range: 40.0, seed: 66666);
        AssertCompleteNetwork(data);

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        model.SpatialDependence = new GaussianCopula(coords, CorrelationFunctionType.Exponential);
        model.UseCopulaDependence = true;
        model.SetDefaultParameters();
        var mle = new MaximumLikelihood(model, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();

        AssertObservedInformationRecovery(
            model,
            mle,
            new[]
            {
                trueParams.CopulaRange,
                trueParams.LocationIntercept,
                trueParams.ScaleIntercept,
                trueParams.ShapeIntercept
            },
            "dependent homogeneous network");
    }

    /// <summary>
    /// Confirms that a generated spatial fixture contains ten sites with 100 observations per site,
    /// for total scalar N equal to 1,000.
    /// </summary>
    /// <param name="data">Generated row-by-site matrix.</param>
    private static void AssertCompleteNetwork(double[,] data)
    {
        Assert.AreEqual(ObservationCountPerSite, data.GetLength(0), "Each site must contain 100 observations.");
        Assert.AreEqual(SiteCount, data.GetLength(1), "Every row must contain the ten-site network.");
        Assert.AreEqual(TotalScalarObservationCount, data.Length, "Total scalar recovery N must equal 1,000.");
        Assert.IsTrue(data.Cast<double>().All(double.IsFinite), "Every retained row/year vector must be complete and finite.");
    }

    /// <summary>
    /// Applies the predeclared frequentist recovery rule to every estimator-owned coordinate.
    /// </summary>
    /// <param name="model">Fitted spatial model whose parameter order labels the coordinates.</param>
    /// <param name="mle">Completed maximum-likelihood estimator.</param>
    /// <param name="parents">Generating parents in the model's flat parameter order.</param>
    /// <param name="fixture">Fixture label used in assertion diagnostics.</param>
    private static void AssertObservedInformationRecovery(
        SpatialGEV model,
        MaximumLikelihood mle,
        IReadOnlyList<double> parents,
        string fixture)
    {
        Assert.IsTrue(mle.IsEstimated, $"{fixture}: Differential Evolution did not produce an estimate.");
        Assert.AreEqual(model.Parameters.Count, parents.Count, $"{fixture}: generating-parent order does not cover every fitted coordinate.");
        Assert.AreEqual(parents.Count, mle.BestParameterSet.Values.Length, $"{fixture}: estimated coordinate count differs from the declared parent vector.");

        bool covarianceAvailable = mle.TryGetCovarianceMatrix(out var covariance);
        Assert.IsTrue(covarianceAvailable, $"{fixture}: {mle.CovarianceDiagnostic}");
        Assert.AreEqual(
            CovarianceComputationStatus.Available,
            mle.CovarianceStatus,
            $"{fixture}: recovery requires the unregularized observed-information covariance; {mle.CovarianceDiagnostic}");

        for (int index = 0; index < parents.Count; index++)
        {
            ModelParameter parameter = model.Parameters[index];
            double parent = parents[index];
            Assert.IsTrue(parent >= parameter.LowerBound && parent <= parameter.UpperBound,
                $"{fixture}.{parameter.Name}: generating parent {parent:G17} is outside [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
            double parentPriorLogDensity = parameter.PriorDistribution.LogPDF(parent);
            Assert.IsTrue(double.IsFinite(parentPriorLogDensity),
                $"{fixture}.{parameter.Name}: generating parent {parent:G17} is outside the unchanged prior support.");

            double standardError = Math.Sqrt(covariance[index, index]);
            double estimate = mle.BestParameterSet.Values[index];
            string coordinate = $"{fixture}.{parameter.Name}";
            RecoveryAcceptance.AssertFrequentistStandardizedError(coordinate, estimate, parent, standardError);
            RecoveryAcceptance.AssertSecondaryPointCriterionWhenResolved(
                coordinate,
                estimate,
                parent,
                estimate - RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError,
                estimate + RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * standardError);
        }
    }
}
