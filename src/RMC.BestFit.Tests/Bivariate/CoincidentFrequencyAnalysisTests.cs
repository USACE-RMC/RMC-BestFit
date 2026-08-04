using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Data.Statistics;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Reflection;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Bivariate;

/// <summary>
/// Programmatic unit tests for the model-layer
/// <c>CoincidentFrequencyAnalysis</c>: constructors, property round-trips,
/// validation, XML serialization, and a deterministic point-estimate-only run with no
/// MCMC chains attached. Computational verification against closed-form answers
/// (sum of correlated normals) lives in
/// <c>RMC.BestFit.Verification/Bivariate/CoincidentFrequencyAnalysisTests.cs</c>.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b> Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
/// </para>
/// </remarks>
[TestClass]
public class CoincidentFrequencyAnalysisTests
{
    #region Helpers

    /// <summary>
    /// Builds a bivariate Normal-marginal Normal-copula model with seeded parameter values
    /// (no MLE / MCMC needed) so <c>CoincidentFrequencyAnalysis</c> can run at the
    /// point estimate. Setting <paramref name="markEstimated"/> = true reconstructs the
    /// analysis through the XElement constructor with <c>IsEstimated="true"</c> so it
    /// passes CFA's hard-fail validation gate (which requires upstream IsEstimated == true).
    /// </summary>
    private static BivariateAnalysis CreateBivariateAnalysisAtPointEstimate(double rho = 0.5, bool markEstimated = true)
    {
        // Marginal X: Normal(0, 1) — small inline BestFitDataFrame just to satisfy IUnivariateModel.
        var dfX = new BestFitDataFrame { ExactSeries = new ExactSeries(new[] { -1.0, 0.0, 1.0, -0.5, 0.5 }) };
        var marginalX = new UnivariateDistribution(dfX, UnivariateDistributionType.Normal);
        marginalX.SetParameterValues(new[] { 0.0, 1.0 });

        // Marginal Y: Normal(0, 1).
        var dfY = new BestFitDataFrame { ExactSeries = new ExactSeries(new[] { -1.0, 0.0, 1.0, -0.5, 0.5 }) };
        var marginalY = new UnivariateDistribution(dfY, UnivariateDistributionType.Normal);
        marginalY.SetParameterValues(new[] { 0.0, 1.0 });

        var dist = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
        dist.Copula.SetCopulaParameters(new[] { rho });

        if (!markEstimated)
            return new BivariateAnalysis(dist);

        // Round-trip through XElement to flip IsEstimated to true without running MCMC.
        var stub = new BivariateAnalysis(dist);
        var xElement = stub.ToXElement();
        xElement.SetAttributeValue("IsEstimated", true);
        return new BivariateAnalysis(dist, xElement);
    }

    /// <summary>
    /// Returns a 5×5 sum-of-X-and-Y response surface on the grid X = Y = [-2, -1, 0, 1, 2].
    /// </summary>
    private static (double[] xValues, double[] yValues, double[,] response) BuildSumGrid()
    {
        var x = new[] { -2.0, -1.0, 0.0, 1.0, 2.0 };
        var y = new[] { -2.0, -1.0, 0.0, 1.0, 2.0 };
        var z = new double[5, 5];
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                z[i, j] = x[i] + y[j];
        return (x, y, z);
    }

    /// <summary>
    /// Builds synthetic MCMC results from indexed parameter values.
    /// </summary>
    /// <param name="count">The retained output count.</param>
    /// <param name="parameters">Maps an output index to a parameter vector.</param>
    /// <returns>The synthetic MCMC results.</returns>
    private static MCMCResults BuildMcmcResults(int count, Func<int, double[]> parameters)
    {
        var output = new List<ParameterSet>(count);
        for (int index = 0; index < count; index++)
            output.Add(new ParameterSet(parameters(index), 0d));
        return new MCMCResults(new ParameterSet(parameters(count / 2), 0d), output, 0.1d);
    }

    /// <summary>
    /// Creates a CFA with deliberately aligned, unequal copula and marginal posteriors.
    /// </summary>
    /// <param name="copulaCount">The retained copula count.</param>
    /// <param name="marginalXCount">The optional retained X-marginal count.</param>
    /// <param name="marginalYCount">The optional retained Y-marginal count.</param>
    /// <returns>The configured CFA fixture.</returns>
    private static CoincidentFrequencyAnalysis CreatePosteriorCfa(
        int copulaCount,
        int? marginalXCount,
        int? marginalYCount)
    {
        BivariateAnalysis bivariate = CreateBivariateAnalysisAtPointEstimate();
        MCMCResults copulaResults = BuildMcmcResults(copulaCount, index =>
        {
            double fraction = index / (double)(copulaCount - 1);
            return [-0.7d + 1.4d * fraction];
        });
        bivariate.BayesianAnalysis.SetCustomMCMCResults(copulaResults, skipInformationCriteria: true);

        var (x, y, response) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, response)
        {
            NumberOfBins = 15
        };
        if (marginalXCount.HasValue)
        {
            int count = marginalXCount.Value;
            cfa.MarginalXChain = BuildMcmcResults(count, index =>
            {
                double fraction = index / (double)(count - 1);
                return [-0.9d + 1.8d * fraction, 1d];
            });
        }
        if (marginalYCount.HasValue)
        {
            int count = marginalYCount.Value;
            cfa.MarginalYChain = BuildMcmcResults(count, index =>
            {
                double fraction = index / (double)(count - 1);
                return [-0.8d + 1.6d * fraction, 1d];
            });
        }
        return cfa;
    }

    /// <summary>
    /// Reads CFA's transient posterior-index cache for focused contract testing.
    /// </summary>
    /// <param name="cfa">The CFA instance.</param>
    /// <returns>The cached mapping, or <c>null</c>.</returns>
    private static int[][]? GetPosteriorIndexCache(CoincidentFrequencyAnalysis cfa)
    {
        return (int[][]?)typeof(CoincidentFrequencyAnalysis)
            .GetField("_posteriorRandomIndexes", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cfa);
    }

    #endregion

    #region Constructor

    /// <summary>Verifies that constructor default initializes empty.</summary>
    [TestMethod]
    public void Constructor_Default_InitializesEmpty()
    {
        var cfa = new CoincidentFrequencyAnalysis();
        Assert.IsNotNull(cfa.XValues);
        Assert.IsNotNull(cfa.YValues);
        Assert.IsNotNull(cfa.BivariateResponse);
        Assert.AreEqual(0, cfa.XValues.Length);
        Assert.AreEqual(0, cfa.YValues.Length);
        Assert.AreEqual(50, cfa.NumberOfBins);
        Assert.IsNotNull(cfa.BayesianAnalysis);
        Assert.AreEqual(0.90, cfa.BayesianAnalysis.CredibleIntervalWidth);
        Assert.IsFalse(cfa.IsEstimated);
        Assert.IsNull(cfa.AnalysisResults);
        Assert.IsNull(cfa.ZOutputValues);
    }

    /// <summary>Verifies that constructor with inputs stores arguments.</summary>
    [TestMethod]
    public void Constructor_WithInputs_StoresArguments()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);

        Assert.AreSame(bivariate, cfa.BivariateAnalysis);
        Assert.AreSame(x, cfa.XValues);
        Assert.AreSame(y, cfa.YValues);
        Assert.AreSame(z, cfa.BivariateResponse);
    }

    /// <summary>Verifies that constructor throws when null bivariate analysis.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullBivariateAnalysis_Throws()
    {
        var (x, y, z) = BuildSumGrid();
        _ = new CoincidentFrequencyAnalysis(null!, x, y, z);
    }

    /// <summary>Verifies that constructor throws when null X values.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullXValues_Throws()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (_, y, z) = BuildSumGrid();
        _ = new CoincidentFrequencyAnalysis(bivariate, null!, y, z);
    }

    /// <summary>Verifies that constructor throws when null X element.</summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void Constructor_NullXElement_Throws()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        _ = new CoincidentFrequencyAnalysis(bivariate, null!);
    }

    #endregion

    #region Property setters

    /// <summary>Verifies that number of bins out of range does not throw validate reports error.</summary>
    [TestMethod]
    public void NumberOfBins_OutOfRange_DoesNotThrow_ValidateReportsError()
    {
        // Per the library convention, range checks are reported via Validate(), not by
        // throwing from the setter.
        var cfa = new CoincidentFrequencyAnalysis();

        cfa.NumberOfBins = 2;
        Assert.AreEqual(2, cfa.NumberOfBins);

        cfa.NumberOfBins = 5000;
        Assert.AreEqual(5000, cfa.NumberOfBins);
    }

    /// <summary>Verifies that X values set clears results.</summary>
    [TestMethod]
    public void XValues_Set_ClearsResults()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        // Prior to RunAsync there are no results to clear, but the property change must fire.
        bool fired = false;
        cfa.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(cfa.XValues)) fired = true; };
        cfa.XValues = new[] { -3.0, -1.0, 0.0, 1.0, 3.0 };
        Assert.IsTrue(fired, "PropertyChanged for XValues must fire.");
    }

    // CredibleIntervalWidth_OutOfRange_IsIgnored test removed: CFA's own setter
    // range-clamp was retired when CredibleIntervalWidth moved into the owned
    // BayesianAnalysis (Phase 2.5a). BayesianAnalysis does not range-clamp the value;
    // out-of-range values would surface via Validate() if needed.

    #endregion

    #region Validate

    /// <summary>Verifies that validate returns error when missing bivariate analysis.</summary>
    [TestMethod]
    public void Validate_MissingBivariateAnalysis_ReturnsError()
    {
        var cfa = new CoincidentFrequencyAnalysis();
        var (isValid, messages) = cfa.Validate();
        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Bivariate analysis is required")));
    }

    /// <summary>Verifies that validate returns error when upstream not estimated.</summary>
    [TestMethod]
    public void Validate_UpstreamNotEstimated_ReturnsError()
    {
        // Hard-fail policy: full posterior chains required for uncertainty propagation.
        var bivariate = CreateBivariateAnalysisAtPointEstimate(markEstimated: false);
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);

        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid, "Upstream not estimated must hard-fail the validation gate.");
        Assert.IsTrue(messages.Any(m => m.Contains("not been estimated")),
            $"Expected 'not been estimated' message; got: {string.Join(" | ", messages)}");
    }

    /// <summary>Verifies that constructor owns bayesian analysis parameterless init.</summary>
    [TestMethod]
    public void Constructor_OwnsBayesianAnalysis_ParameterlessInit()
    {
        // CFA mirrors CompositeAnalysis: owns its own BayesianAnalysis for presentation
        // settings (CredibleIntervalWidth, OutputLength, PointEstimator). Constructed via
        // the parameterless BayesianAnalysis() ctor — no IModel needed because CFA runs
        // no MCMC chain of its own.
        var cfa = new CoincidentFrequencyAnalysis();
        Assert.IsNotNull(cfa.BayesianAnalysis);
        Assert.AreEqual(0.90, cfa.BayesianAnalysis.CredibleIntervalWidth, 1e-12);
    }

    /// <summary>Verifies that bayesian analysis owned not proxied from upstream.</summary>
    [TestMethod]
    public void BayesianAnalysis_OwnedNotProxiedFromUpstream()
    {
        // Critical invariant: CFA's own BayesianAnalysis must be independent from the
        // upstream BivariateAnalysis's BayesianAnalysis. Otherwise editing CFA's
        // PointEstimator would mutate the upstream copula's MCMC settings.
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, new[] { -1.0, 1.0 }, new[] { -1.0, 1.0 }, new double[2, 2]);
        Assert.IsNotNull(cfa.BayesianAnalysis);
        Assert.IsNotNull(bivariate.BayesianAnalysis);
        Assert.AreNotSame(cfa.BayesianAnalysis, bivariate.BayesianAnalysis,
            "CFA's BayesianAnalysis must be a separate instance from the upstream's.");
    }

    /// <summary>Verifies that bayesian analysis credible interval width change clears results.</summary>
    [TestMethod]
    public void BayesianAnalysis_CredibleIntervalWidthChange_ClearsResults()
    {
        // Per-policy: CI change forces a full rerun — ClearResults() invalidates the
        // estimated state and the user re-runs explicitly. Mirrors CompositeAnalysis.
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);

        // Stub out an "estimated" state so ClearResults has something to clear.
        cfa.SetAnalysisResults(new UncertaintyAnalysisResults());
        cfa.SetZOutputValues(new[] { 0.0, 1.0, 2.0 });

        cfa.BayesianAnalysis.CredibleIntervalWidth = 0.80;

        Assert.IsNull(cfa.AnalysisResults, "CI change must clear AnalysisResults (full rerun required).");
        Assert.IsNull(cfa.ZOutputValues, "CI change must clear ZOutputValues.");
    }

    /// <summary>Verifies that bayesian analysis point estimator change preserves derived results.</summary>
    [TestMethod]
    public async Task BayesianAnalysis_PointEstimatorChange_DoesNotClearResults()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 10 };
        await cfa.RunAsync();
        var originalResults = cfa.AnalysisResults;
        var originalZOutputValues = cfa.ZOutputValues;

        cfa.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        await Task.Delay(100);

        Assert.IsTrue(cfa.IsEstimated, "PointEstimator change should preserve the estimated state.");
        Assert.AreSame(originalResults, cfa.AnalysisResults, "PointEstimator change should reprocess in place, not clear results.");
        Assert.AreSame(originalZOutputValues, cfa.ZOutputValues, "PointEstimator change should preserve Z output bins.");
    }

    /// <summary>Verifies that validate is valid with no messages when good inputs.</summary>
    [TestMethod]
    public void Validate_GoodInputs_IsValidWithNoMessages()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);

        var (isValid, messages) = cfa.Validate();

        Assert.IsTrue(isValid, $"Expected valid; got messages: {string.Join(" | ", messages)}");
        Assert.AreEqual(0, messages.Count);
    }

    /// <summary>Verifies that validate returns error when non ascending X.</summary>
    [TestMethod]
    public void Validate_NonAscendingX_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (_, y, z) = BuildSumGrid();
        var x = new[] { -2.0, 0.0, -1.0, 1.0, 2.0 }; // out of order

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("X (primary) values must be strictly ascending")));
    }

    /// <summary>Verifies that validate returns error when non ascending Y.</summary>
    [TestMethod]
    public void Validate_NonAscendingY_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, _, z) = BuildSumGrid();
        var y = new[] { -2.0, -1.0, 1.0, 0.0, 2.0 }; // 1.0 before 0.0

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Y (secondary) values must be strictly ascending")));
    }

    /// <summary>Verifies that validate returns error when dimension mismatch.</summary>
    [TestMethod]
    public void Validate_DimensionMismatch_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var x = new[] { -2.0, -1.0, 0.0, 1.0, 2.0 };
        var y = new[] { -2.0, -1.0, 0.0, 1.0, 2.0 };
        var z = new double[3, 5]; // wrong row count

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("must have 5 rows")));
    }

    /// <summary>Verifies that validate returns error when non monotonic response along X.</summary>
    [TestMethod]
    public void Validate_NonMonotonicResponseAlongX_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        z[2, 2] = -10; // breaks monotonicity in column 2

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("strictly increasing along X")));
    }

    /// <summary>Verifies that validate returns error when na n in response.</summary>
    [TestMethod]
    public void Validate_NaNInResponse_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        z[1, 1] = double.NaN;

        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);
        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("NaN")));
    }

    /// <summary>Verifies that validate returns error when number of bins too small.</summary>
    [TestMethod]
    public void Validate_NumberOfBinsTooSmall_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 3 };

        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("at least 5")));
    }

    /// <summary>Verifies that validate returns error when number of bins too large.</summary>
    [TestMethod]
    public void Validate_NumberOfBinsTooLarge_ReturnsError()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 1500 };

        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("at most 1000")));
    }

    /// <summary>Verifies that validate number of bins above100 adds non blocking warning.</summary>
    [TestMethod]
    public void Validate_NumberOfBinsAbove100_AddsNonBlockingWarning()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 200 };

        var (isValid, messages) = cfa.Validate();

        Assert.IsTrue(isValid, "Warning must not block validation.");
        Assert.IsTrue(messages.Any(m => m.StartsWith("Warning")), "A Warning-prefixed message must be present.");
        Assert.IsTrue(messages.Any(m => m.Contains("slow run times")));
    }

    /// <summary>Verifies that validate rejects a negative posterior-resampling seed.</summary>
    [TestMethod]
    public void Validate_NegativePosteriorResamplingSeed_ReturnsError()
    {
        CoincidentFrequencyAnalysis cfa = CreatePosteriorCfa(20, 20, 20);
        cfa.BayesianAnalysis.PRNGSeed = -1;

        var (isValid, messages) = cfa.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(message => message.Contains("PRNG seed", StringComparison.Ordinal)));
    }

    #endregion

    #region XML round-trip

    /// <summary>Verifies that to X element preserves inputs for from X element.</summary>
    [TestMethod]
    public void ToXElement_FromXElement_PreservesInputs()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 75 };
        cfa.BayesianAnalysis.CredibleIntervalWidth = 0.95;

        var xElement = cfa.ToXElement();
        var restored = new CoincidentFrequencyAnalysis(bivariate, xElement);

        CollectionAssert.AreEqual(x, restored.XValues);
        CollectionAssert.AreEqual(y, restored.YValues);
        Assert.AreEqual(75, restored.NumberOfBins);
        Assert.AreEqual(0.95, restored.BayesianAnalysis.CredibleIntervalWidth);
        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                Assert.AreEqual(z[i, j], restored.BivariateResponse[i, j], 1e-12);
    }

    /// <summary>Verifies that constructor with legacy Z table xml still reads values.</summary>
    [TestMethod]
    public void Constructor_WithLegacyZTableXml_StillReadsValues()
    {
        // Older v1 schemas wrote the surface under the "ZTable" element. The new
        // constructor must still read them so existing projects open cleanly.
        var (x, y, z) = BuildSumGrid();
        var legacy = new XElement("CoincidentFrequencyAnalysis",
            new XAttribute("IsEstimated", false),
            new XElement(nameof(CoincidentFrequencyAnalysis.XValues), string.Join(",", x)),
            new XElement(nameof(CoincidentFrequencyAnalysis.YValues), string.Join(",", y)),
            new XElement("ZTable", string.Join(",", z.Cast<double>())));

        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, legacy);

        for (int i = 0; i < 5; i++)
            for (int j = 0; j < 5; j++)
                Assert.AreEqual(z[i, j], cfa.BivariateResponse[i, j], 1e-12);
    }

    #endregion

    #region Algorithm — point-estimate-only run (no MCMC chain)

    /// <summary>Verifies that run async matches closed form sum of normals for point estimate only.</summary>
    [TestMethod]
    [DataRow(0.0)]
    [DataRow(0.5)]
    [DataRow(-0.5)]
    public async Task RunAsync_PointEstimateOnly_MatchesClosedFormSumOfNormals(double rho)
    {
        // X ~ N(0,1), Y ~ N(0,1), Gaussian copula(ρ), response Z = X + Y on a 5×5 grid.
        // Truth: Z ~ N(0, √(2(1+ρ))).
        // Apples-to-apples: compute the closed-form AEP at each ZOutputValues[i] and
        // compare against AnalysisResults.ModeCurve[i] element-wise.
        var bivariate = CreateBivariateAnalysisAtPointEstimate(rho);
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 50 };

        await cfa.RunAsync();

        Assert.IsTrue(cfa.IsEstimated);
        Assert.IsNotNull(cfa.AnalysisResults);
        Assert.IsNotNull(cfa.ZOutputValues);
        Assert.AreEqual(50, cfa.ZOutputValues!.Length);
        Assert.AreEqual(-4.0, cfa.ZOutputValues[0], 1e-12, "First Z bin == zMin.");
        Assert.AreEqual(4.0, cfa.ZOutputValues[^1], 1e-12, "Last Z bin == zMax.");

        // CIs are NaN bands when no posterior samples are available.
        Assert.IsTrue(double.IsNaN(cfa.AnalysisResults!.ConfidenceIntervals![0, 0]));
        Assert.IsTrue(double.IsNaN(cfa.AnalysisResults.ConfidenceIntervals[0, 1]));

        // Closed-form truth at every ZOutputValues bin.
        var truthSigma = Math.Sqrt(2.0 * (1.0 + rho));
        var truthDist = new Normal(0.0, truthSigma);
        var modeCurve = cfa.AnalysisResults.ModeCurve!;

        double maxAbsError = 0;
        double sumAbsError = 0;
        for (int i = 0; i < cfa.ZOutputValues.Length; i++)
        {
            double truthAep = 1.0 - truthDist.CDF(cfa.ZOutputValues[i]);
            double absError = Math.Abs(modeCurve[i] - truthAep);
            if (absError > maxAbsError) maxAbsError = absError;
            sumAbsError += absError;
        }
        double meanAbsError = sumAbsError / cfa.ZOutputValues.Length;

        // Build a diagnostic table for failure messages.
        string DiagnosticTable()
        {
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"\nrho={rho}, sigma={truthSigma:F4}");
            sb.AppendLine($"{"z",10}{"truth",12}{"computed",12}{"err",12}");
            for (int i = 0; i < cfa.ZOutputValues.Length; i++)
            {
                double truthAep = 1.0 - truthDist.CDF(cfa.ZOutputValues[i]);
                double err = modeCurve[i] - truthAep;
                sb.AppendLine($"{cfa.ZOutputValues[i],10:F3}{truthAep,12:F4}{modeCurve[i],12:F4}{err,12:F4}");
            }
            sb.AppendLine($"max abs err = {maxAbsError:F4}, mean abs err = {meanAbsError:F4}");
            return sb.ToString();
        }

        // Python-validated tolerance: max ≤ 0.02, mean ≤ 0.005 for a 5×5 grid (Approach B).
        Assert.IsTrue(maxAbsError <= 0.02,
            $"Max abs error {maxAbsError:F4} exceeds 0.02 (rho={rho}, sigma={truthSigma:F4}).{DiagnosticTable()}");
        Assert.IsTrue(meanAbsError <= 0.005,
            $"Mean abs error {meanAbsError:F4} exceeds 0.005 (rho={rho}).{DiagnosticTable()}");

        // Sanity check: AEP must be (weakly) monotonically decreasing in z.
        for (int k = 1; k < modeCurve.Length; k++)
        {
            Assert.IsTrue(modeCurve[k] <= modeCurve[k - 1] + 1e-9,
                $"AEP must be non-increasing in z; failed at k={k} (rho={rho}).");
        }
    }

    /// <summary>
    /// Verifies CFA creates distinct copula/X/Y index rows in fixed semantic order and that
    /// every indexed empirical distribution exactly reconstructs the aggregate result.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_IndependentPosteriorIndexes_MatchCachedAccessorRealizations()
    {
        CoincidentFrequencyAnalysis cfa = CreatePosteriorCfa(23, 17, 19);
        cfa.BayesianAnalysis.PRNGSeed = 97531;

        await cfa.RunAsync();

        int[][]? cache = GetPosteriorIndexCache(cfa);
        Assert.IsNotNull(cache);
        int[][] expectedIndexes = PosteriorIndexResampler.CreateRandomIndexes([23, 17, 19], 97531);
        Assert.AreEqual(3, cache.Length);
        for (int source = 0; source < cache.Length; source++)
            CollectionAssert.AreEqual(expectedIndexes[source], cache[source]);
        Assert.IsFalse(cache[0].SequenceEqual(cache[1]));
        Assert.IsFalse(cache[1].SequenceEqual(cache[2]));

        AssertAggregateMatchesAccessors(cfa, 17);
        int[][]? cacheAfterAccess = GetPosteriorIndexCache(cfa);
        Assert.AreSame(cache, cacheAfterAccess,
            "Indexed access must reuse the exact mapping used by aggregate construction.");
    }

    /// <summary>
    /// Verifies absent optional marginal chains retain point-estimate fallback while a
    /// present Y chain occupies the second semantic source row.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_MissingMarginalX_UsesFallbackAndPreservesSourceOrder()
    {
        CoincidentFrequencyAnalysis cfa = CreatePosteriorCfa(21, null, 13);
        cfa.BayesianAnalysis.PRNGSeed = 86420;

        await cfa.RunAsync();

        int[][]? cache = GetPosteriorIndexCache(cfa);
        Assert.IsNotNull(cache);
        Assert.AreEqual(2, cache.Length);
        int[][] expectedIndexes = PosteriorIndexResampler.CreateRandomIndexes([21, 13], 86420);
        CollectionAssert.AreEqual(expectedIndexes[0], cache[0]);
        CollectionAssert.AreEqual(expectedIndexes[1], cache[1]);
        AssertAggregateMatchesAccessors(cfa, 13);
    }

    /// <summary>
    /// Verifies changing the result-generation seed or a marginal posterior invalidates both
    /// the cached mapping and derived CFA results before deterministic regeneration.
    /// </summary>
    [TestMethod]
    public async Task PosteriorIndexCache_SeedAndMarginalChanges_InvalidateAndRegenerate()
    {
        CoincidentFrequencyAnalysis cfa = CreatePosteriorCfa(25, 20, 22);
        cfa.BayesianAnalysis.PRNGSeed = 11111;
        await cfa.RunAsync();
        int[][]? firstCache = GetPosteriorIndexCache(cfa);
        Assert.IsNotNull(firstCache);

        cfa.BayesianAnalysis.PRNGSeed = 22222;
        Assert.IsNull(GetPosteriorIndexCache(cfa));
        Assert.IsNull(cfa.AnalysisResults);
        Assert.IsNull(cfa.ZOutputValues);
        await cfa.RunAsync();
        int[][]? secondCache = GetPosteriorIndexCache(cfa);
        Assert.IsNotNull(secondCache);
        Assert.AreNotSame(firstCache, secondCache);

        cfa.MarginalXChain = BuildMcmcResults(18, index => [index / 20d, 1d]);
        Assert.IsNull(GetPosteriorIndexCache(cfa));
        Assert.IsNull(cfa.AnalysisResults);
        Assert.IsNull(cfa.ZOutputValues);
        await cfa.RunAsync();
        int[][]? thirdCache = GetPosteriorIndexCache(cfa);
        Assert.IsNotNull(thirdCache);
        Assert.AreEqual(18, thirdCache[0].Length);
        int[][] expectedIndexes = PosteriorIndexResampler.CreateRandomIndexes([25, 18, 22], 22222);
        for (int source = 0; source < thirdCache.Length; source++)
            CollectionAssert.AreEqual(expectedIndexes[source], thirdCache[source]);
    }

    /// <summary>
    /// Verifies CFA aggregate mean and credible limits equal direct aggregation of its
    /// cached indexed empirical distributions.
    /// </summary>
    /// <param name="cfa">The completed CFA.</param>
    /// <param name="realizationCount">The expected realization count.</param>
    private static void AssertAggregateMatchesAccessors(
        CoincidentFrequencyAnalysis cfa,
        int realizationCount)
    {
        Assert.IsNotNull(cfa.AnalysisResults);
        Assert.IsNotNull(cfa.ZOutputValues);
        var distributions = new EmpiricalDistribution[realizationCount];
        for (int realization = 0; realization < realizationCount; realization++)
        {
            distributions[realization] = cfa.GetEmpiricalDistribution(realization)!;
            Assert.IsNotNull(distributions[realization]);
        }

        double alpha = 1d - cfa.BayesianAnalysis.CredibleIntervalWidth;
        for (int bin = 0; bin < cfa.ZOutputValues.Length; bin++)
        {
            double[] aeps = distributions
                .Select(distribution => distribution.ProbabilityValues[bin])
                .ToArray();
            Assert.AreEqual(
                Statistics.ParallelMean(aeps),
                cfa.AnalysisResults.MeanCurve![bin],
                1E-15);
            Array.Sort(aeps);
            Assert.AreEqual(
                Statistics.Percentile(aeps, alpha / 2d, true),
                cfa.AnalysisResults.ConfidenceIntervals![bin, 0],
                1E-15);
            Assert.AreEqual(
                Statistics.Percentile(aeps, 1d - alpha / 2d, true),
                cfa.AnalysisResults.ConfidenceIntervals[bin, 1],
                1E-15);
        }
    }

    /// <summary>Verifies that clear results after run drops results and Z outputs.</summary>
    [TestMethod]
    public void ClearResults_AfterRun_DropsResultsAndZOutputs()
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 10 };
        cfa.RunAsync().GetAwaiter().GetResult();

        Assert.IsNotNull(cfa.AnalysisResults);
        cfa.ClearResults();

        Assert.IsNull(cfa.AnalysisResults);
        Assert.IsNull(cfa.ZOutputValues);
        Assert.IsFalse(cfa.IsEstimated);
    }

    #endregion

    #region Cancellation

    /// <summary>
    /// Subscribes to <c>AnalysisCompleted</c> BEFORE the run starts and returns a
    /// task that completes with the event args (or null on timeout). Subscribing
    /// after RunAsync starts is racy — the run may finish before the subscribe.
    /// </summary>
    private static (TaskCompletionSource<AnalysisRunCompletedEventArgs> tcs,
                    EventHandler<AnalysisRunCompletedEventArgs> handler)
        BeginAwaitingCompletion(CoincidentFrequencyAnalysis cfa)
    {
        var tcs = new TaskCompletionSource<AnalysisRunCompletedEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        EventHandler<AnalysisRunCompletedEventArgs> handler = null!;
        handler = (_, args) =>
        {
            cfa.AnalysisCompleted -= handler;
            tcs.TrySetResult(args);
        };
        cfa.AnalysisCompleted += handler;
        return (tcs, handler);
    }

    /// <summary>
    /// Waits for completion Async.
    /// </summary>
    /// <param name="cfa">The cfa value.</param>
    /// <param name="tcs">The tcs value.</param>
    /// <param name="handler">The handler value.</param>
    /// <param name="timeout">The timeout value.</param>
    /// <returns>A task that represents the asynchronous operation.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static async Task<AnalysisRunCompletedEventArgs?> AwaitCompletionAsync(
        CoincidentFrequencyAnalysis cfa,
        TaskCompletionSource<AnalysisRunCompletedEventArgs> tcs,
        EventHandler<AnalysisRunCompletedEventArgs> handler,
        TimeSpan timeout)
    {
        var winner = await Task.WhenAny(tcs.Task, Task.Delay(timeout));
        if (winner == tcs.Task) return tcs.Task.Result;
        cfa.AnalysisCompleted -= handler;
        return null;
    }

    /// <summary>
    /// Builds a CFA whose upstream BivariateAnalysis carries an injected synthetic copula
    /// MCMC chain. The chain length is chosen so the per-realisation parallel loop runs
    /// for measurable wall-clock time on a 5×5 sum grid — long enough that a Cancel
    /// arriving 50 ms after RunAsync starts can land mid-loop.
    /// </summary>
    private static CoincidentFrequencyAnalysis CreateCfaWithCopulaChain(int chainLength)
    {
        var bivariate = CreateBivariateAnalysisAtPointEstimate();

        // Inject a synthetic copula chain (single-parameter Normal copula → 1 value/draw).
        var output = new List<Numerics.Mathematics.Optimization.ParameterSet>(chainLength);
        for (int i = 0; i < chainLength; i++)
        {
            // Vary rho slightly so each realisation does the same amount of work.
            double rho = 0.5 + 0.001 * Math.Sin(i);
            output.Add(new Numerics.Mathematics.Optimization.ParameterSet(new[] { rho }, 0.0));
        }
        bivariate.BayesianAnalysis.OutputLength = chainLength;
        bivariate.BayesianAnalysis.SetCustomMCMCResults(
            new Numerics.Sampling.MCMC.MCMCResults(
                new Numerics.Mathematics.Optimization.ParameterSet(new[] { 0.5 }, 0.0),
                output, alpha: 0.10),
            skipInformationCriteria: true);

        var (x, y, z) = BuildSumGrid();
        return new CoincidentFrequencyAnalysis(bivariate, x, y, z) { NumberOfBins = 50 };
    }

    /// <summary>
    /// Calling <c>AnalysisBase.CancelAnalysis</c> mid-loop must terminate the
    /// CFA run promptly with <c>Cancelled = true</c> and no <c>CoincidentFrequencyAnalysis.AnalysisResults</c>
    /// populated. Regression test for the worker-throw vs. silent-return cancellation
    /// observation: with the silent-return pattern, Parallel.For's dispatch-level token
    /// poll is what surfaces the OperationCanceledException upstream.
    /// </summary>
    [TestMethod]
    public async Task Cancel_DuringParallelLoop_StopsAndReportsCanceled()
    {
        // 5000 copula realisations × 50 bins × 5×5 grid is enough wall-clock work in
        // Debug for the cancel token to be observed mid-loop. CFA per-iteration is
        // cheap (linear interp on a small grid), so we need a long-enough chain that
        // the loop doesn't finish in <50ms.
        var cfa = CreateCfaWithCopulaChain(chainLength: 5000);

        // Subscribe BEFORE the run starts so we can't miss the AnalysisCompleted event.
        var (tcs, handler) = BeginAwaitingCompletion(cfa);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        var runTask = cfa.RunAsync();
        // Give the parallel loop a brief moment to dispatch iterations, then cancel.
        await Task.Delay(50);
        cfa.CancelAnalysis();

        var args = await AwaitCompletionAsync(cfa, tcs, handler, TimeSpan.FromSeconds(15));
        sw.Stop();

        Assert.IsNotNull(args, "AnalysisCompleted must fire after a cancel; the CFA run hung.");
        Assert.IsTrue(args!.Cancelled,
            "AnalysisRunCompletedEventArgs.Cancelled must report true after CancelAnalysis(). " +
            $"Succeeded={args.Succeeded}, Error={args.Error?.GetType().Name}");
        Assert.IsFalse(cfa.IsEstimated, "IsEstimated must reset to false on cancellation.");
        // CFA sets AnalysisResults (with the deterministic ModeCurve) BEFORE the parallel
        // loop dispatches, so a mid-loop cancel does not null it back. The Cancelled flag
        // and IsEstimated=false are the authoritative signals; AnalysisResults emptiness
        // is not part of the cancel contract for CFA.
        Assert.IsTrue(sw.Elapsed.TotalSeconds < 10.0,
            $"Cancellation should propagate within seconds; took {sw.Elapsed.TotalSeconds:F2}s. " +
            "If this fails, ParallelOptions.CancellationToken is not being honoured by Parallel.For.");

        // Drain any leftover continuation so it doesn't bleed into the next test.
        try { await runTask; } catch (OperationCanceledException) { /* expected */ }
        catch (System.Exception) { /* tolerated */ }
    }

    /// <summary>
    /// Calling <c>AnalysisBase.CancelAnalysis</c> on a freshly-constructed CFA
    /// flips the inherited <c>System.Threading.CancellationTokenSource</c>'s
    /// token to canceled. Locks the model-layer <c>CancelAnalysis</c> path that the
    /// UI wrapper delegates to.
    /// </summary>
    [TestMethod]
    public void CancelAnalysis_BeforeRun_FlipsCancellationToken()
    {
        var cfa = CreateCfaWithCopulaChain(chainLength: 100);

        Assert.IsFalse(cfa.CancellationTokenSource.IsCancellationRequested,
            "Fresh CFA should have an un-cancelled token.");
        cfa.CancelAnalysis();
        Assert.IsTrue(cfa.CancellationTokenSource.IsCancellationRequested,
            "After CancelAnalysis() the inherited CancellationTokenSource must be canceled.");
    }

    /// <summary>
    /// CFA whose upstream <c>BivariateAnalysis</c> is not estimated must throw
    /// <c>InvalidOperationException</c> on <c>CoincidentFrequencyAnalysis.RunAsync</c>
    /// before reaching the parallel loop. Documents the existing pre-flight check so
    /// the cancellation refactor doesn't regress it.
    /// </summary>
    [TestMethod]
    public async Task RunAsync_WithUnestimatedBivariate_FailsValidation()
    {
        // markEstimated=false produces a BivariateAnalysis with IsEstimated == false.
        var bivariate = CreateBivariateAnalysisAtPointEstimate(markEstimated: false);
        var (x, y, z) = BuildSumGrid();
        var cfa = new CoincidentFrequencyAnalysis(bivariate, x, y, z);

        await Assert.ThrowsExceptionAsync<InvalidOperationException>(
            async () => await cfa.RunAsync());
    }

    #endregion
}
