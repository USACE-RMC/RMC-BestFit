using System.ComponentModel;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;
using RMC.BestFit.Models.TrendFunctions;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Tests.Models;

/// <summary>
/// Locks in the <c>SetDefaultParameters</c> / <c>SetDefaultQuantilePriors</c> property-change
/// contract: every compliant model raises its method name as the <c>INotifyPropertyChanged</c>
/// signal. App XAML controls (<c>ParameterPriorsControl</c>, <c>QuantilePriorsControl</c>,
/// <c>B17CAnalysisPropertiesControl</c>) subscribe to those method-name events and rely on the
/// raise firing AFTER the parameter / quantile-prior list is fully populated.
/// </summary>
/// <remarks>
/// The canonical template is <c>UnivariateDistribution.SetDefaultParameters</c> /
/// <c>SetDefaultQuantilePriors</c>. Models covered by the tests below deviated prior to this
/// audit by either raising <c>nameof(Parameters)</c> (premature, during empty-list state via
/// the setter) or by not raising at all on early-return paths.
/// </remarks>
[TestClass]
public class SetDefaultRaiseTests
{
    #region Helpers

    /// <summary>
    /// Captures every <c>PropertyChangedEventArgs.PropertyName</c> the subject raises
    /// and returns the capture list. Caller invokes the operation to exercise after subscribing.
    /// </summary>
    private static List<string?> CapturePropertyNames(INotifyPropertyChanged subject, Action invoke)
    {
        var captured = new List<string?>();
        void Handler(object? sender, PropertyChangedEventArgs e) => captured.Add(e.PropertyName);
        subject.PropertyChanged += Handler;
        try
        {
            invoke();
        }
        finally
        {
            subject.PropertyChanged -= Handler;
        }
        return captured;
    }

    /// <summary>
    /// Small univariate sample used as the exact-series fixture in univariate marginals.
    /// </summary>
    private static readonly double[] s_sampleX =
    {
        98.1, 102.7, 115.3, 88.4, 104.9, 92.0, 110.5, 99.2, 107.6, 101.3,
        95.8, 108.1, 103.4, 97.6, 112.9, 89.5, 106.2, 100.0, 93.7, 109.4
    };

    private static readonly double[] s_sampleY =
    {
        75.2, 82.1, 93.6, 68.7, 84.3, 72.5, 90.4, 78.9, 88.5, 81.0,
        74.1, 87.6, 82.8, 76.3, 91.5, 69.2, 86.0, 80.4, 73.5, 89.1
    };

    /// <summary>
    /// Builds exact Data Frame.
    /// </summary>
    /// <param name="values">The input values.</param>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame BuildExactDataFrame(double[] values)
    {
        var df = new BestFitDataFrame();
        var data = new List<ExactData>();
        for (int i = 0; i < values.Length; i++)
            data.Add(new ExactData { Index = 1980 + i, Value = values[i] });
        df.ExactSeries = new ExactSeries(data);
        return df;
    }

    #endregion

    #region MixtureModel

    /// <summary>
    /// <c>MixtureModel.SetDefaultParameters</c> raises the method name as its sole terminal signal
    /// so App controls refresh AFTER the full parameter list is assembled (not during the empty
    /// setter-assignment phase it did prior to the fix).
    /// </summary>
    [TestMethod]
    public void MixtureModel_SetDefaultParameters_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var mixture = new Mixture(
            new[] { 0.5, 0.5 },
            new UnivariateDistributionBase[] { new Normal(95, 10), new Normal(110, 10) });
        var model = new MixtureModel(df, mixture);

        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(MixtureModel.SetDefaultParameters)),
            "Expected SetDefaultParameters raise so App controls can refresh on full list.");
    }

    /// <summary>
    /// The invalid-data early-return path still raises the method name. Without this raise,
    /// App controls cached against the prior valid state would stale when the data frame is
    /// cleared.
    /// </summary>
    [TestMethod]
    public void MixtureModel_SetDefaultParameters_InvalidData_StillRaises_MethodName()
    {
        var model = new MixtureModel(); // default: no data, triggers early return
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(MixtureModel.SetDefaultParameters)),
            "Early-return path must raise the method name for UI consistency.");
    }

    /// <summary>
    /// The enabled path of <c>SetDefaultQuantilePriors</c> raises the method name and populates
    /// <c>QuantilePriors</c> before the raise fires.
    /// </summary>
    [TestMethod]
    public void MixtureModel_SetDefaultQuantilePriors_Enabled_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var mixture = new Mixture(
            new[] { 0.5, 0.5 },
            new UnivariateDistributionBase[] { new Normal(95, 10), new Normal(110, 10) });
        var model = new MixtureModel(df, mixture) { EnableQuantilePriors = true };

        var captured = CapturePropertyNames(model, () => model.SetDefaultQuantilePriors());

        Assert.IsTrue(captured.Contains(nameof(MixtureModel.SetDefaultQuantilePriors)));
        Assert.IsTrue(model.QuantilePriors.Count > 0, "Enabled path should populate priors.");
    }

    /// <summary>
    /// <c>EnableQuantilePriors = false</c> still raises the method name and clears the list —
    /// the previous implementation raised nothing on the disabled branch, leaving controls stale.
    /// </summary>
    [TestMethod]
    public void MixtureModel_SetDefaultQuantilePriors_Disabled_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var mixture = new Mixture(
            new[] { 0.5, 0.5 },
            new UnivariateDistributionBase[] { new Normal(95, 10), new Normal(110, 10) });
        var model = new MixtureModel(df, mixture) { EnableQuantilePriors = false };

        var captured = CapturePropertyNames(model, () => model.SetDefaultQuantilePriors());

        Assert.IsTrue(captured.Contains(nameof(MixtureModel.SetDefaultQuantilePriors)));
        Assert.AreEqual(0, model.QuantilePriors.Count);
    }

    #endregion

    #region CompetingRisksModel

    /// <summary>Verifies that competing risks model set default parameters raises method name.</summary>
    [TestMethod]
    public void CompetingRisksModel_SetDefaultParameters_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var cr = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(95, 10), new Normal(110, 10)
        });
        var model = new CompetingRisksModel(df, cr);

        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(CompetingRisksModel.SetDefaultParameters)));
    }

    /// <summary>Verifies that competing risks model set default parameters invalid data still raises method name.</summary>
    [TestMethod]
    public void CompetingRisksModel_SetDefaultParameters_InvalidData_StillRaises_MethodName()
    {
        var model = new CompetingRisksModel();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(CompetingRisksModel.SetDefaultParameters)));
    }

    /// <summary>Verifies that competing risks model set default quantile priors enabled raises method name.</summary>
    [TestMethod]
    public void CompetingRisksModel_SetDefaultQuantilePriors_Enabled_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var cr = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(95, 10), new Normal(110, 10)
        });
        var model = new CompetingRisksModel(df, cr) { EnableQuantilePriors = true };

        var captured = CapturePropertyNames(model, () => model.SetDefaultQuantilePriors());

        Assert.IsTrue(captured.Contains(nameof(CompetingRisksModel.SetDefaultQuantilePriors)));
    }

    /// <summary>Verifies that competing risks model set default quantile priors disabled raises method name.</summary>
    [TestMethod]
    public void CompetingRisksModel_SetDefaultQuantilePriors_Disabled_Raises_MethodName()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var cr = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(95, 10), new Normal(110, 10)
        });
        var model = new CompetingRisksModel(df, cr) { EnableQuantilePriors = false };

        var captured = CapturePropertyNames(model, () => model.SetDefaultQuantilePriors());

        Assert.IsTrue(captured.Contains(nameof(CompetingRisksModel.SetDefaultQuantilePriors)));
    }

    #endregion

    #region PointProcessModel

    /// <summary>Verifies that point process model set default parameters raises method name.</summary>
    [TestMethod]
    public void PointProcessModel_SetDefaultParameters_Raises_MethodName()
    {
        var model = new PointProcessModel();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(PointProcessModel.SetDefaultParameters)));
    }

    /// <summary>
    /// The guard previously used <c>&amp;&amp;</c> which allowed a null-<c>Distribution</c> path
    /// through to a dereference. The fix is <c>||</c> — verify the method early-returns cleanly.
    /// </summary>
    [TestMethod]
    public void PointProcessModel_SetDefaultQuantilePriors_NullDistribution_DoesNotThrow()
    {
        var model = new PointProcessModel();
        model.Distribution = null!;

        model.SetDefaultQuantilePriors(); // no throw
    }

    /// <summary>Verifies that point process model set default quantile priors disabled raises method name.</summary>
    [TestMethod]
    public void PointProcessModel_SetDefaultQuantilePriors_Disabled_Raises_MethodName()
    {
        var model = new PointProcessModel { EnableQuantilePriors = false };
        var captured = CapturePropertyNames(model, () => model.SetDefaultQuantilePriors());

        Assert.IsTrue(captured.Contains(nameof(PointProcessModel.SetDefaultQuantilePriors)));
    }

    #endregion

    #region BivariateDistribution

    /// <summary>Verifies that bivariate distribution set default parameters raises method name.</summary>
    [TestMethod]
    public void BivariateDistribution_SetDefaultParameters_Raises_MethodName()
    {
        var dfX = BuildExactDataFrame(s_sampleX);
        var dfY = BuildExactDataFrame(s_sampleY);
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);
        marginalX.SetParameterValues(new[] { 101.8, 7.5 });
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Normal);
        marginalY.SetParameterValues(new[] { 80.8, 7.5 });
        for (int i = 0; i < s_sampleX.Length; i++)
        {
            ((ExactData)dfX.ExactSeries[i]).Index = i;
            ((ExactData)dfY.ExactSeries[i]).Index = i;
        }

        var bivariate = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);

        var captured = CapturePropertyNames(bivariate, () => bivariate.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(BivariateDistribution.SetDefaultParameters)));
    }

    #endregion

    #region BestFitRatingCurve

    /// <summary>Verifies that rating curve set default parameters raises method name.</summary>
    [TestMethod]
    public void RatingCurve_SetDefaultParameters_Raises_MethodName()
    {
        var stage = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1),
            new[] { 5.0, 6.5, 7.5, 9.0, 10.5, 12.0, 14.0 });
        var discharge = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1),
            new[] { 110.0, 300.0, 600.0, 1200.0, 2000.0, 3500.0, 5100.0 });
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1);

        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(RMC.BestFit.Models.RatingCurve.SetDefaultParameters)));
    }

    #endregion

    #region Time series

    /// <summary>Verifies that auto regressive set default parameters raises method name.</summary>
    [TestMethod]
    public void AutoRegressive_SetDefaultParameters_Raises_MethodName()
    {
        var model = new AutoRegressive();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(AutoRegressive.SetDefaultParameters)));
    }

    /// <summary>Verifies that moving average set default parameters raises method name.</summary>
    [TestMethod]
    public void MovingAverage_SetDefaultParameters_Raises_MethodName()
    {
        var model = new MovingAverage();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(MovingAverage.SetDefaultParameters)));
    }

    /// <summary>Verifies that ARIMA set default parameters raises method name.</summary>
    [TestMethod]
    public void ARIMA_SetDefaultParameters_Raises_MethodName()
    {
        var model = new ARIMA();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(ARIMA.SetDefaultParameters)));
    }

    /// <summary>Verifies that ARIMAX set default parameters raises method name.</summary>
    [TestMethod]
    public void ARIMAX_SetDefaultParameters_Raises_MethodName()
    {
        var model = new ARIMAX();
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(ARIMAX.SetDefaultParameters)));
    }

    #endregion

    #region SpatialGEV

    /// <summary>Verifies that spatial GEV set default parameters raises method name.</summary>
    [TestMethod]
    public void SpatialGEV_SetDefaultParameters_Raises_MethodName()
    {
        // Minimal at-site data: 10 observations × 2 sites, drawn from a homogeneous GEV.
        var data = new double[10, 2];
        var rng = new Random(42);
        var gev = new GeneralizedExtremeValue(6000, 1500, 0.0);
        for (int i = 0; i < 10; i++)
        {
            data[i, 0] = gev.InverseCDF(rng.NextDouble());
            data[i, 1] = gev.InverseCDF(rng.NextDouble());
        }
        var coords = new double[,] { { 0.0, 0.0 }, { 10.0, 0.0 } };

        var location = new GeneralLinearFunction("Location");
        var scale = new GeneralLinearFunction("Scale");
        var shape = new GeneralLinearFunction("Shape");
        var model = new SpatialGEV(data, coords, location, scale, shape);

        // Constructor already called SetDefaultParameters once; capture a fresh invocation.
        var captured = CapturePropertyNames(model, () => model.SetDefaultParameters());

        Assert.IsTrue(captured.Contains(nameof(SpatialGEV.SetDefaultParameters)));
    }

    #endregion

    #region Analysis-side regressions — Model.SetDefaultParameters must propagate to BayesianAnalysis defaults

    /// <summary>
    /// Regression for the reported bug: changing <c>NumberOfSegments</c> on the rating-curve model
    /// rebuilds the parameter list via <c>SetDefaultParameters</c>, and the wrapping
    /// <c>RatingCurveAnalysis</c> must re-run <c>SetDefaultSimulationOptions</c> so the DEMCzs
    /// <c>NumberOfChains</c> reflects the new parameter count. Prior to widening the listener to also
    /// match <c>nameof(SetDefaultParameters)</c>, the model-layer raise was silently dropped.
    /// </summary>
    [TestMethod]
    public void RatingCurveAnalysis_NumberOfSegmentsChange_UpdatesBayesianDefaults()
    {
        var stage = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1),
            new[] { 5.0, 6.5, 7.5, 9.0, 10.5, 12.0, 14.0 });
        var discharge = new Numerics.Data.TimeSeries(TimeInterval.OneDay, new DateTime(2000, 1, 1),
            new[] { 110.0, 300.0, 600.0, 1200.0, 2000.0, 3500.0, 5100.0 });
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1);
        var analysis = new RMC.BestFit.Analyses.RatingCurveAnalysis(model);

        // DEMCzs formula from BayesianAnalysis.SetDefaultSimulationOptions:
        //   NumberOfChains = Math.Max(3, Math.Min(20, 2 * d))
        //   1 segment → 4 params → 2*4 = 8 chains
        Assert.AreEqual(8, analysis.BayesianAnalysis.NumberOfChains,
            "Baseline: 1-segment model should produce 8 chains for DEMCzs.");

        model.NumberOfSegments = 3;

        // 3 segments → 10 params → Math.Min(20, 2*10) = 20 chains
        Assert.AreEqual(20, analysis.BayesianAnalysis.NumberOfChains,
            "After NumberOfSegments = 3, defaults must recompute via the SetDefaultParameters raise.");
    }

    /// <summary>
    /// Regression coverage for the canonical path: changing <c>Distribution</c> on a
    /// <c>UnivariateDistribution</c> rebuilds parameters via <c>SetDefaultParameters</c>,
    /// and <c>UnivariateAnalysis</c> must re-run <c>SetDefaultSimulationOptions</c>.
    /// </summary>
    [TestMethod]
    public void UnivariateAnalysis_DistributionTypeChange_UpdatesBayesianDefaults()
    {
        var df = BuildExactDataFrame(s_sampleX);
        var dist = new UnivariateDistribution(df, UnivariateDistributionType.Normal);
        var analysis = new RMC.BestFit.Analyses.UnivariateAnalysis(dist);

        // Normal → 2 params → DEMCzs: Math.Max(3, Math.Min(20, 2*2)) = 4
        Assert.AreEqual(4, analysis.BayesianAnalysis.NumberOfChains,
            "Baseline: Normal distribution should produce 4 chains for DEMCzs.");

        dist.DistributionType = UnivariateDistributionType.GeneralizedExtremeValue;

        // GEV → 3 params → Math.Max(3, Math.Min(20, 2*3)) = 6
        Assert.AreEqual(6, analysis.BayesianAnalysis.NumberOfChains,
            "After switching to GEV, defaults must recompute via the SetDefaultParameters raise.");
    }

    #endregion
}
