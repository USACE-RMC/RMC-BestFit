using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests for the parameterized
/// <c>IUnivariateAnalysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType)</c>
/// overload added to support <c>CompositeAnalysis</c>'s need to extract
/// posterior-mean / MAP distributions from children without mutating the child's own
/// <c>BayesianAnalysis.PointEstimator</c>.
/// </summary>
/// <remarks>
/// Critical regression coverage for the fix described as "Issue 1" in the
/// bug-fixes-and-enhancements branch: the previous private static
/// <c>GetChildPointEstimateDistribution</c> in CompositeAnalysis directly cloned
/// <c>UnivariateDistribution.Distribution</c> and called <c>SetParameters</c>, which
/// silently corrupted nonstationary children whose parameter array contains trend
/// coefficients in addition to base distribution parameters.
/// </remarks>
[TestClass]
public class GetPointEstimateDistributionOverloadTests
{
    #region Inline test fixtures

    private static readonly double[] InlineExactData =
    {
        12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600,
        19200, 13800, 25600, 10500, 16900, 21300, 14700, 8200, 23800, 15900
    };

    /// <summary>
    /// Creates data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateDataFrame()
    {
        var df = new BestFitDataFrame();
        for (int i = 0; i < InlineExactData.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, InlineExactData[i]));
        df.CalculatePlottingPositions();
        return df;
    }

    /// <summary>Marks the analysis as estimated without running an MCMC chain.</summary>
    private static void ForceIsEstimated(AnalysisBase analysis)
    {
        var field = typeof(AnalysisBase).GetField("_isEstimated",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.IsNotNull(field, "AnalysisBase._isEstimated field not found.");
        field!.SetValue(analysis, true);
    }

    /// <summary>Builds an <c>MCMCResults</c> with MAP + N output draws averaged to a known mean.</summary>
    private static MCMCResults BuildResults(double[] mapValues, double[] meanValues, int sampleSize = 100)
    {
        // Use the meanValues array verbatim as every draw so PosteriorMean equals it.
        var output = new List<ParameterSet>(sampleSize);
        for (int i = 0; i < sampleSize; i++)
            output.Add(new ParameterSet((double[])meanValues.Clone(), 0.0));
        return new MCMCResults(new ParameterSet((double[])mapValues.Clone(), 0.0), output, alpha: 0.10);
    }

    #endregion

    #region Stationary UnivariateAnalysis

    /// <summary>
    /// Returns null when the analysis has not been estimated, regardless of which
    /// estimator is requested. Mirrors the parameterless overload's contract.
    /// </summary>
    [TestMethod]
    public void Stationary_UnEstimated_ReturnsNull()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);

        Assert.IsNull(analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean));
        Assert.IsNull(analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMode));
    }

    /// <summary>
    /// PosteriorMean estimator returns a distribution whose parameters match the injected
    /// PosteriorMean parameter array (not the MAP array).
    /// </summary>
    [TestMethod]
    public void Stationary_PosteriorMeanEstimator_UsesPosteriorMeanParameters()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);
        // Distinct values for MAP vs Mean so we can tell which array was used.
        double[] mapValues  = { 17000.0, 5000.0 };
        double[] meanValues = { 16000.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);

        var result = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean);

        Assert.IsNotNull(result);
        var parameters = result.GetParameters;
        Assert.AreEqual(meanValues[0], parameters[0], 1e-9, "Mu should match injected PosteriorMean.");
        Assert.AreEqual(meanValues[1], parameters[1], 1e-9, "Sigma should match injected PosteriorMean.");
    }

    /// <summary>
    /// PosteriorMode estimator returns the MAP parameter array.
    /// </summary>
    [TestMethod]
    public void Stationary_PosteriorModeEstimator_UsesMAPParameters()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);
        double[] mapValues  = { 17000.0, 5000.0 };
        double[] meanValues = { 16000.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);

        var result = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMode);

        Assert.IsNotNull(result);
        var parameters = result.GetParameters;
        Assert.AreEqual(mapValues[0], parameters[0], 1e-9, "Mu should match injected MAP.");
        Assert.AreEqual(mapValues[1], parameters[1], 1e-9, "Sigma should match injected MAP.");
    }

    /// <summary>
    /// Calling the parameterized overload with an explicit estimator must not mutate the
    /// analysis's own <c>BayesianAnalysis.PointEstimator</c> property.
    /// </summary>
    /// <remarks>
    /// This is the design contract that justifies adding the parameterized overload at all
    /// — a parent <c>CompositeAnalysis</c> needs to evaluate a child under an
    /// estimator different from the child's current setting without triggering the child's
    /// reprocess cascade (which fires on PointEstimator change).
    /// </remarks>
    [TestMethod]
    public void Stationary_DoesNotMutateAnalysisPointEstimatorProperty()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);
        double[] mapValues  = { 17000.0, 5000.0 };
        double[] meanValues = { 16000.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean);

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator,
            "Parameterized overload must not flip the analysis's PointEstimator property.");
    }

    /// <summary>
    /// The parameterless overload still delegates to the parameterized overload using the
    /// analysis's currently configured estimator. This proves the refactor preserves the
    /// public parameterless contract.
    /// </summary>
    [TestMethod]
    public void Stationary_ParameterlessOverload_UsesAnalysisOwnPointEstimator()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(dist);
        double[] mapValues  = { 17000.0, 5000.0 };
        double[] meanValues = { 16000.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;

        var result = analysis.GetPointEstimateDistribution();

        Assert.IsNotNull(result);
        Assert.AreEqual(meanValues[0], result.GetParameters[0], 1e-9);
    }

    #endregion

    #region Nonstationary UnivariateAnalysis (Issue 1 — core fix)

    /// <summary>
    /// Calling the parameterized overload on a nonstationary <c>UnivariateAnalysis</c>
    /// returns a stationary distribution evaluated at the last time step. This is the
    /// regression test for the bug where <c>CompositeAnalysis</c> previously cloned
    /// the base distribution and called <c>SetParameters</c> on a parameter array that
    /// included trend coefficients — silently corrupting the result.
    /// </summary>
    /// <remarks>
    /// The fix routes through <c>UnivariateDistribution.SetParameterValues(IList{double})</c>,
    /// which correctly distributes the array across base + trend models, then unwraps the
    /// frozen distribution at the configured <c>ParameterTimeIndex</c>.
    /// </remarks>
    [TestMethod]
    public void Nonstationary_ReturnsValidDistributionAtLastTimeStep()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        dist.IsNonstationary = true;
        // Linear trend on parameter 0 (mu): mu(t) = b0 + b1 * t
        dist.SetTrendModel(0, TrendModelType.Linear);
        // Constant trend on parameter 1 (sigma): sigma(t) = b0
        // (default after IsNonstationary = true; SetTrendModel call would replace it, leave alone).

        var analysis = new UnivariateAnalysis(dist);
        // Parameter layout for [LinearTrend(mu), ConstantTrend(sigma)]:
        // [mu_b0, mu_b1, sigma_b0]
        double[] mapValues  = { 16000.0, 50.0, 5000.0 };
        double[] meanValues = { 17000.0, 80.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);

        var result = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean);

        // The Issue 1 fix returns a stationary distribution at the last time step. The
        // test passes if the call:
        //   (a) does not throw on the NS parameter-count mismatch,
        //   (b) returns a non-null UnivariateDistributionBase,
        //   (c) the returned distribution is stationary (i.e. has the base distribution's
        //       parameter count, not the NS expanded count).
        Assert.IsNotNull(result, "NS GetPointEstimateDistribution(PosteriorMean) must not return null.");
        Assert.AreEqual(2, result.GetParameters.Length,
            "Returned distribution should be the frozen stationary distribution (2 params for Normal), " +
            "not the NS expanded array (3 params).");
        // Parameters are finite (no silent corruption from feeding a 3-array to a 2-param SetParameters).
        Assert.IsTrue(double.IsFinite(result.GetParameters[0]), "Mu must be finite.");
        Assert.IsTrue(double.IsFinite(result.GetParameters[1]), "Sigma must be finite.");
        Assert.IsTrue(result.GetParameters[1] > 0, "Sigma must be positive.");
    }

    /// <summary>
    /// Nonstationary path also works for <c>BayesianAnalysis.PointEstimateType.PosteriorMode</c>.
    /// </summary>
    [TestMethod]
    public void Nonstationary_PosteriorMode_ReturnsValidDistribution()
    {
        var dist = new UnivariateDistribution(CreateDataFrame(), UnivariateDistributionType.Normal);
        dist.IsNonstationary = true;
        dist.SetTrendModel(0, TrendModelType.Linear);
        var analysis = new UnivariateAnalysis(dist);
        double[] mapValues  = { 16000.0, 50.0, 5000.0 };
        double[] meanValues = { 17000.0, 80.0, 6000.0 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);

        var result = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMode);

        Assert.IsNotNull(result);
        Assert.AreEqual(2, result.GetParameters.Length);
        Assert.IsTrue(result.GetParameters[1] > 0);
    }

    #endregion

    #region Bulletin17CAnalysis

    /// <summary>
    /// <c>Bulletin17CAnalysis</c> implements the same parameterized overload and
    /// returns null when not estimated. (B17C has no nonstationary path — it is always a
    /// stationary LP3 fit — so a single round-trip test covers the contract.)
    /// </summary>
    [TestMethod]
    public void B17C_UnEstimated_ReturnsNull()
    {
        var df = CreateDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);

        Assert.IsNull(analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean));
        Assert.IsNull(analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMode));
    }

    /// <summary>
    /// PosteriorMean and PosteriorMode select different parameter arrays from injected
    /// MCMC results. Both return non-null distributions with the LP3 parameter count (3).
    /// </summary>
    [TestMethod]
    public void B17C_DifferentEstimators_SelectDifferentParameterArrays()
    {
        var df = CreateDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);
        // LP3: location, scale, shape.
        double[] mapValues  = { 4.10, 0.30, 0.10 };
        double[] meanValues = { 4.05, 0.32, 0.05 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);

        var meanResult = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean);
        var modeResult = analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMode);

        Assert.IsNotNull(meanResult);
        Assert.IsNotNull(modeResult);
        Assert.AreEqual(3, meanResult.GetParameters.Length, "LP3 has 3 parameters.");
        Assert.AreEqual(meanValues[0], meanResult.GetParameters[0], 1e-9);
        Assert.AreEqual(mapValues[0], modeResult.GetParameters[0], 1e-9);
        Assert.AreNotEqual(meanResult.GetParameters[0], modeResult.GetParameters[0],
            "Different estimators on different injected arrays must yield different distributions.");
    }

    /// <summary>
    /// B17C parameterized overload also does not mutate the analysis's
    /// <c>BayesianAnalysis.PointEstimator</c>.
    /// </summary>
    [TestMethod]
    public void B17C_DoesNotMutateAnalysisPointEstimatorProperty()
    {
        var df = CreateDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var analysis = new Bulletin17CAnalysis(model);
        double[] mapValues  = { 4.10, 0.30, 0.10 };
        double[] meanValues = { 4.05, 0.32, 0.05 };
        analysis.BayesianAnalysis.OutputLength = 100;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            BuildResults(mapValues, meanValues), skipInformationCriteria: true);
        ForceIsEstimated(analysis);
        // B17C defaults to PosteriorMode; pin it so the cross-check is meaningful.
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        analysis.GetPointEstimateDistribution(BayesianAnalysis.PointEstimateType.PosteriorMean);

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator);
    }

    #endregion
}
