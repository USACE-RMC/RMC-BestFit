using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;
using System.Reflection;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <c>Bulletin17CAnalysis</c> class.
/// </summary>
/// <remarks>
/// Covers construction, property round-trip, validation, XElement serialization, and the
/// supporting <c>UncertaintyMethod</c> enum + <c>CohnConfidenceIntervalResult</c>
/// DTO. GMM estimation, bootstrap, and Cohn-style CI computations are computationally
/// expensive and live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class Bulletin17CAnalysisTests
{
    #region Inline test fixtures

    private const int FixtureSize = 50;

    // Deterministic flood-like fixture. Inline (fixed RNG seed) so this file does not
    // depend on the Verification project's TestData.
    private static readonly double[] InlineFloodData = new LogNormal(8.0, 0.4)
        .GenerateRandomValues(FixtureSize, 12345);

    /// <summary>
    /// Creates flood Data Frame.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static BestFitDataFrame CreateFloodDataFrame()
    {
        var df = new BestFitDataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
        {
            df.ExactSeries.Add(new ExactData(1970 + i, InlineFloodData[i]));
        }
        return df;
    }

    /// <summary>
    /// Creates lP3 Model.
    /// </summary>
    /// <returns>The created test object.</returns>
    /// <remarks>
    /// This helper keeps fixture setup local to the tests that use it.
    /// </remarks>
    private static Bulletin17CDistribution CreateLP3Model()
    {
        var df = CreateFloodDataFrame();
        return new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
    }

    #endregion

    #region Constructor

    /// <summary>
    /// Default analysis constructor wires a Bulletin17CDistribution model, owns its own
    /// BayesianAnalysis (point estimator defaulting to PosteriorMode), and starts un-estimated.
    /// </summary>
    [TestMethod]
    public void Constructor_WithModel_InitializesAllRequiredState()
    {
        var model = CreateLP3Model();

        var analysis = new Bulletin17CAnalysis(model);

        Assert.AreSame(model, analysis.Bulletin17CDistribution);
        Assert.IsNotNull(analysis.BayesianAnalysis,
            "BayesianAnalysis is used as the GMM result container per the wrapper-class invariants.");
        Assert.IsNotNull(analysis.ProbabilityOrdinates);
        Assert.IsFalse(analysis.IsEstimated, "Newly constructed analysis should not be estimated.");
        Assert.IsNull(analysis.AnalysisResults, "Pre-run AnalysisResults must be null.");
        Assert.IsNull(analysis.GMM, "GMM is created lazily during RunAsync — null until then.");
    }

    /// <summary>
    /// Default constructor sets the point estimator to <c>PosteriorMode</c> because GMM
    /// produces a point estimate rather than a chain — using the chain mean would average
    /// over warm-up jitter that doesn't apply to GMM output.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultPointEstimator_IsPosteriorMode()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());

        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode,
            analysis.BayesianAnalysis.PointEstimator);
    }

    /// <summary>
    /// Default uncertainty method is <c>LinkedMultivariateNormal</c> — the production
    /// calibrated default. Tests should pin this so an accidental flip in production code
    /// surfaces here rather than silently changing computed CIs.
    /// </summary>
    [TestMethod]
    public void Constructor_DefaultUncertaintyMethod_IsLinkedMultivariateNormal()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());

        Assert.AreEqual(UncertaintyMethod.LinkedMultivariateNormal, analysis.UncertaintyMethod);
    }

    /// <summary>
    /// Null-arg guard: a B17C analysis without a B17C model is non-sensical.
    /// </summary>
    [TestMethod]
    public void Constructor_NullModel_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CAnalysis(null!));
    }

    /// <summary>
    /// XElement constructor null-arg guards — both arguments are required.
    /// </summary>
    [TestMethod]
    public void Constructor_NullXmlOrModel_Throws()
    {
        var model = CreateLP3Model();

        // Null XElement
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CAnalysis(model, null!));

        // Null model
        var any = new Bulletin17CAnalysis(model);
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CAnalysis(null!, any.ToXElement()));
    }

    #endregion

    #region UncertaintyMethod property

    /// <summary>
    /// Setting <c>Bulletin17CAnalysis.UncertaintyMethod</c> to a new value clears
    /// existing results: the prior CIs no longer reflect the chosen method.
    /// </summary>
    [TestMethod]
    public void UncertaintyMethod_SettingDifferentValue_RaisesPropertyChange()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());
        bool propertyChangedFired = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Bulletin17CAnalysis.UncertaintyMethod))
                propertyChangedFired = true;
        };

        analysis.UncertaintyMethod = UncertaintyMethod.Bootstrap;

        Assert.AreEqual(UncertaintyMethod.Bootstrap, analysis.UncertaintyMethod);
        Assert.IsTrue(propertyChangedFired);
    }

    /// <summary>
    /// Setting <c>Bulletin17CAnalysis.UncertaintyMethod</c> to the same value is a no-op:
    /// no PropertyChanged event, no result clearing — protects against unnecessary recomputes.
    /// </summary>
    [TestMethod]
    public void UncertaintyMethod_SettingSameValue_DoesNotRaisePropertyChange()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());
        var original = analysis.UncertaintyMethod;
        bool propertyChangedFired = false;
        analysis.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(Bulletin17CAnalysis.UncertaintyMethod))
                propertyChangedFired = true;
        };

        analysis.UncertaintyMethod = original;

        Assert.IsFalse(propertyChangedFired);
    }

    #endregion

    #region ClearResults

    /// <summary>
    /// ClearResults transitions the analysis back to the un-estimated state and frees
    /// downstream results. The model itself is preserved.
    /// </summary>
    [TestMethod]
    public void ClearResults_NullsResultsAndUnsetsIsEstimated()
    {
        var model = CreateLP3Model();
        var analysis = new Bulletin17CAnalysis(model);

        analysis.ClearResults();

        Assert.IsFalse(analysis.IsEstimated);
        Assert.IsNull(analysis.AnalysisResults);
        Assert.AreSame(model, analysis.Bulletin17CDistribution,
            "ClearResults must not detach the model — only its computed results.");
    }

    #endregion

    #region Validation

    /// <summary>
    /// A B17C analysis with a valid LP3 model and the default probability ordinates
    /// should validate cleanly.
    /// </summary>
    [TestMethod]
    public void Validate_GoodFixture_IsValid()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());

        var (isValid, messages) = analysis.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join("; ", messages)}");
    }

    /// <summary>
    /// Validate must propagate failures from the underlying distribution: if the model is
    /// invalid (e.g., null BestFitDataFrame) the analysis is invalid too.
    /// </summary>
    [TestMethod]
    public void Validate_InvalidModel_PropagatesFailure()
    {
        // Default constructor has no BestFitDataFrame, which the distribution's Validate flags.
        var modelWithoutData = new Bulletin17CDistribution();
        var analysis = new Bulletin17CAnalysis(modelWithoutData);

        var (isValid, messages) = analysis.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Count > 0);
    }

    #endregion

    #region Serialization

    /// <summary>
    /// ToXElement / FromXElement must preserve the configured uncertainty method,
    /// probability ordinates, and core BayesianAnalysis settings (iterations, warmup).
    /// </summary>
    [TestMethod]
    public void XmlSerialization_RoundTrip_PreservesConfiguration()
    {
        var model = CreateLP3Model();
        var analysis = new Bulletin17CAnalysis(model);
        analysis.UncertaintyMethod = UncertaintyMethod.BiasCorrectedBootstrap;
        analysis.BayesianAnalysis.Iterations = 4000;
        analysis.BayesianAnalysis.WarmupIterations = 800;

        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.5);
        analysis.ProbabilityOrdinates.Add(0.1);
        analysis.ProbabilityOrdinates.Add(0.01);

        var xml = analysis.ToXElement();

        var restored = new Bulletin17CAnalysis(model, xml);

        Assert.AreEqual(UncertaintyMethod.BiasCorrectedBootstrap, restored.UncertaintyMethod,
            "UncertaintyMethod must round-trip.");
        Assert.AreEqual(3, restored.ProbabilityOrdinates.Count,
            "ProbabilityOrdinates must round-trip.");
        Assert.AreEqual(4000, restored.BayesianAnalysis.Iterations);
        Assert.AreEqual(800, restored.BayesianAnalysis.WarmupIterations);
        Assert.IsFalse(restored.IsEstimated, "Pre-run analysis must restore as non-estimated.");
    }

    /// <summary>
    /// Serialized XElement uses the analysis class name as its root tag.
    /// </summary>
    [TestMethod]
    public void ToXElement_HasExpectedRootName()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());

        var xml = analysis.ToXElement();

        Assert.AreEqual(nameof(Bulletin17CAnalysis), xml.Name.LocalName);
    }

    #endregion

    #region Linked Multivariate Normal link helpers

    /// <summary>
    /// Location uses signed WEDS because censoring direction has a direct location interpretation.
    /// </summary>
    [TestMethod]
    public void LocationLink_UsesSignedWedsForCensoringDirection()
    {
        var method = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateLocationLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Location WEDS handling should remain explicit and defensible.");

        var leftCensoredLink = (ASinHLink)method!.Invoke(null, [100.0, 5.0, -0.7])!;
        var rightCensoredLink = (ASinHLink)method!.Invoke(null, [100.0, 5.0, 0.7])!;

        Assert.AreEqual(-0.7, leftCensoredLink.ParentIndicator, 1e-12,
            "Signed WEDS must preserve left-censoring direction for the location marginal.");
        Assert.AreEqual(0.7, rightCensoredLink.ParentIndicator, 1e-12,
            "Signed WEDS must preserve right-censoring direction for the location marginal.");
        Assert.AreEqual(0.50, leftCensoredLink.EpsilonMax, 1e-12);
        Assert.AreEqual(0.50, rightCensoredLink.EpsilonMax, 1e-12);
        Assert.AreEqual(1.0, leftCensoredLink.Delta, 1e-12,
            "Location WEDS changes asymmetry, not tail thickness.");
    }

    /// <summary>
    /// LP3/P3 location retains the theory-calibrated fitted-gamma blend and conservative cap.
    /// </summary>
    [TestMethod]
    public void PearsonLocationLink_RetainsGammaBlendAndConservativeCap()
    {
        var method = typeof(Bulletin17CAnalysis).GetMethod(
            "CreatePearsonLocationLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "Pearson location behavior should remain separate from the generic location link.");

        var link = (ASinHLink)method!.Invoke(null, [100.0, 5.0, 0.4, -0.1])!;

        Assert.AreEqual(0.10, link.ParentIndicator, 1e-12,
            "LP3/P3 location should preserve the prior 0.5*gammaHat + WEDS blend.");
        Assert.AreEqual(0.50, link.EpsilonMax, 1e-12,
            "LP3/P3 location should keep its fixed asymmetry cap.");
        Assert.AreEqual(1.0, link.EpsilonSlope, 1e-12);
        Assert.AreEqual(1.0, link.Delta, 1e-12,
            "LP3/P3 location should not be the tail-thickening knob.");
    }

    /// <summary>
    /// Raw WEDS for the gamma moment is magnitude evidence, not direction evidence. A
    /// positive fitted skew such as gamma = 0.5 can produce negative raw WEDS, but the
    /// ASinH link must still receive a positive direction score because gammaHat supplies
    /// the theory-based sign.
    /// </summary>
    [TestMethod]
    public void GammaWeds_PositiveGamma_UsesWedsMagnitudeWithPositiveDirection()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod, "Gamma WEDS orientation helper should remain explicit.");
        Assert.IsNotNull(linkMethod, "Gamma ASinH link helper should remain explicit.");

        double orientedScore = (double)orientMethod!.Invoke(null, [0.5, -0.8])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [0.5, 0.2, orientedScore])!;

        Assert.IsTrue(orientedScore > 0.65,
            "Positive gamma should orient a large raw WEDS magnitude into strong positive gamma-link evidence.");
        Assert.AreEqual(orientedScore, link.ParentIndicator, 1e-12,
            "The ASinH parent indicator should use the oriented gamma direction score.");
        Assert.IsTrue(link.EpsilonMax > 0.75,
            "A strong positive gamma direction should permit strong positive ASinH asymmetry.");
    }

    /// <summary>
    /// Negative fitted gamma uses the same WEDS magnitude but flips the ASinH direction negative.
    /// </summary>
    [TestMethod]
    public void GammaWeds_NegativeGamma_UsesWedsMagnitudeWithNegativeDirection()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [-0.5, 0.8])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [-0.5, 0.2, orientedScore])!;

        Assert.IsTrue(orientedScore < -0.65,
            "Negative gamma should orient a large raw WEDS magnitude into negative gamma-link evidence.");
        Assert.AreEqual(orientedScore, link.ParentIndicator, 1e-12);
        Assert.AreEqual(0.45, link.EpsilonMax, 1e-12,
            "Negative gamma asymmetry remains lower than the positive side to avoid over-negative LP3/P3 uncertainty tails.");
    }

    /// <summary>
    /// A fitted zero gamma remains symmetric; high-quantile support comes from heavier tails.
    /// </summary>
    [TestMethod]
    public void GammaWeds_ZeroGamma_UsesSymmetricHeavyTails()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [0.0, 0.0])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [0.0, 0.2, orientedScore])!;

        Assert.AreEqual(0.0, orientedScore, 1e-12,
            "Exactly zero fitted gamma should remain symmetric rather than receiving positive asymmetry.");
        Assert.AreEqual(0.0, link.ParentIndicator, 1e-12);
        Assert.AreEqual(0.0, link.EpsilonMax, 1e-12);
        Assert.IsTrue(link.Delta < 1.0 && link.Delta > 0.90,
            "Near-zero gamma should use symmetric tail thickening through Delta instead of epsilon.");
    }

    /// <summary>
    /// Slight positive gamma should preserve positive skew direction; near-zero tail spread is handled by Delta.
    /// </summary>
    [TestMethod]
    public void GammaWeds_SlightPositiveGamma_UsesPositiveDirectionAndHeavyTails()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [0.03, 1.0])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [0.03, 0.2, orientedScore])!;

        Assert.AreEqual(1.0, orientedScore, 1e-12,
            "Positive fitted gamma should use positive WEDS magnitude immediately rather than being gated by gammaSE.");
        Assert.AreEqual(1.0, link.ParentIndicator, 1e-12,
            "Positive fitted gamma should always receive positive ASinH skew direction.");
        Assert.AreEqual(1.0, link.EpsilonMax, 1e-12);
        Assert.IsTrue(link.Delta < 1.0 && link.Delta > 0.90,
            "Near-zero positive gamma can still receive symmetric tail thickness through Delta.");
    }

    /// <summary>
    /// Heavy censoring can make raw WEDS large when gamma is only slightly negative. The
    /// negative-side gate should suppress ASinH asymmetry in that case.
    /// </summary>
    [TestMethod]
    public void GammaWeds_SlightNegativeGamma_DampensLargeWedsMagnitude()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [-0.03, 1.0])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [-0.03, 0.2, orientedScore])!;

        Assert.IsTrue(Math.Abs(orientedScore) < 0.02,
            "Slight negative gamma should strongly damp even maximal raw WEDS magnitude.");
        Assert.AreEqual(0.0, link.ParentIndicator, 1e-12);
        Assert.AreEqual(0.0, link.EpsilonMax, 1e-12);
        Assert.IsTrue(link.Delta < 1.0 && link.Delta > 0.90,
            "Slight negative gamma should remain symmetric but gain near-zero tail thickness.");
    }

    /// <summary>
    /// Moderate negative gamma should re-enter the negative ASinH direction smoothly.
    /// </summary>
    [TestMethod]
    public void GammaWeds_ModerateNegativeGamma_SmoothlyRestoresNegativeDirection()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [-0.35, 1.0])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [-0.35, 0.2, orientedScore])!;

        Assert.AreEqual(-0.784, orientedScore, 1e-12,
            "Negative gamma asymmetry should enter smoothly as |gammaHat| moves away from zero.");
        Assert.AreEqual(-0.784, link.ParentIndicator, 1e-12);
        Assert.AreEqual(0.446, link.EpsilonMax, 1e-12,
            "Moderate negative gamma should remain below the positive-side epsilon floor.");
        Assert.IsTrue(link.Delta > 0.95,
            "Gamma tail thickening should fade as fitted gamma moves away from zero.");
    }

    /// <summary>
    /// Positive fitted gamma still receives a small positive direction when WEDS is balanced.
    /// </summary>
    [TestMethod]
    public void GammaWeds_PositiveGammaWithBalancedWeds_UsesPositiveFloor()
    {
        var orientMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "OrientGammaWedsForLink",
            BindingFlags.NonPublic | BindingFlags.Static);
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(orientMethod);
        Assert.IsNotNull(linkMethod);

        double orientedScore = (double)orientMethod!.Invoke(null, [0.03, 0.0])!;
        var link = (ASinHLink)linkMethod!.Invoke(null, [0.03, 0.2, orientedScore])!;

        Assert.AreEqual(0.10, orientedScore, 1e-12,
            "Positive fitted gamma should retain a small positive ASinH direction even when WEDS magnitude is balanced.");
        Assert.AreEqual(0.10, link.ParentIndicator, 1e-12);
        Assert.AreEqual(0.55, link.EpsilonMax, 1e-12);
        Assert.IsTrue(link.Delta < 1.0,
            "GammaSE remains available for symmetric tail thickening.");
    }

    /// <summary>
    /// Larger gamma standard error should produce heavier symmetric gamma tails near zero.
    /// </summary>
    [TestMethod]
    public void GammaTailDelta_NearZeroGamma_DecreasesAsGammaUncertaintyIncreases()
    {
        var linkMethod = typeof(Bulletin17CAnalysis).GetMethod(
            "CreateGammaShapeLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(linkMethod);

        var wellIdentified = (ASinHLink)linkMethod!.Invoke(null, [0.0, 0.1, 0.0])!;
        var weaklyIdentified = (ASinHLink)linkMethod!.Invoke(null, [0.0, 0.8, 0.0])!;

        Assert.IsTrue(weaklyIdentified.Delta < wellIdentified.Delta,
            "GammaSE is used as an effective-information surrogate; larger gammaSE should thicken tails.");
        Assert.IsTrue(weaklyIdentified.Delta >= 0.80,
            "Adaptive gamma tail thickening should respect the configured minimum delta.");
        Assert.AreEqual(0.0, weaklyIdentified.EpsilonMax, 1e-12,
            "Tail thickening near zero should not introduce positive or negative skewness by itself.");
    }

    /// <summary>
    /// Positive-parameter links use relative standard error so shape is invariant to flow units.
    /// </summary>
    [TestMethod]
    public void PositiveParameterLink_UsesRelativeStandardErrorDrivenLogASinH()
    {
        var method = typeof(Bulletin17CAnalysis).GetMethod(
            "CreatePositiveParameterLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method);

        var baselineUnits = (LogASinHLink)method!.Invoke(null, [10.0, 1.0])!;
        var scaledUnits = (LogASinHLink)method!.Invoke(null, [100.0, 10.0])!;
        var weaklyIdentified = (LogASinHLink)method!.Invoke(null, [10.0, 5.0])!;

        Assert.AreEqual(Math.Sqrt(Math.Log(1.01)), baselineUnits.LogScale, 1e-12,
            "Positive-parameter log-standardization should be derived from SE / estimate.");
        Assert.AreEqual(baselineUnits.LogScale, scaledUnits.LogScale, 1e-12,
            "Changing measurement units should not change positive-parameter link shape.");
        Assert.AreEqual(baselineUnits.ParentIndicator, scaledUnits.ParentIndicator, 1e-12,
            "Relative uncertainty, not raw SE, should drive adaptive asymmetry.");
        Assert.IsTrue(weaklyIdentified.ParentIndicator > baselineUnits.ParentIndicator,
            "Larger relative SE should strengthen positive-parameter right-tail asymmetry.");
        Assert.IsTrue(weaklyIdentified.Delta < baselineUnits.Delta,
            "Larger relative SE should produce fatter symmetric log-space tails.");
    }

    /// <summary>
    /// LP3/P3 scale uses a positive-support log-ASinH link driven by relative standard error.
    /// </summary>
    [TestMethod]
    public void PearsonScaleLink_UsesRelativeStandardErrorDrivenLogASinH()
    {
        var method = typeof(Bulletin17CAnalysis).GetMethod(
            "CreatePearsonScaleLink",
            BindingFlags.NonPublic | BindingFlags.Static);

        Assert.IsNotNull(method, "P3/LP3 scale handling should remain isolated from generic positive-parameter links.");

        var link = (LogASinHLink)method!.Invoke(null, [10.0, 1.0])!;

        Assert.AreEqual(10.0, link.Sigma0, 1e-12);
        Assert.AreEqual(Math.Sqrt(Math.Log(1.01)), link.LogScale, 1e-12,
            "Scale log-standardization should be derived from scaleSE / scaleHat.");
        Assert.AreEqual(1.0 / 3.5, link.ParentIndicator, 1e-12,
            "Relative scale uncertainty should drive adaptive positive asymmetry.");
        Assert.IsTrue(link.UseAdaptiveEpsilon,
            "LP3/P3 scale should adapt asymmetry to relative standard error.");
        Assert.AreEqual(0.75, link.EpsilonMax, 1e-12);
        Assert.AreEqual(1.25, link.EpsilonSlope, 1e-12);
        Assert.IsTrue(link.Delta < 1.0 && link.Delta > 0.90,
            "Scale tail thickness should increase modestly with relative standard error.");
    }

    #endregion

    #region UncertaintyMethod enum surface

    /// <summary>
    /// Pin the production set of uncertainty methods. Adding a new entry should be a
    /// deliberate change paired with downstream UI / report changes — failing here flags
    /// the contract change.
    /// </summary>
    [TestMethod]
    public void UncertaintyMethod_EnumHasFourMembers()
    {
        var members = Enum.GetValues<UncertaintyMethod>();

        // MultivariateNormal, LinkedMultivariateNormal, Bootstrap, BiasCorrectedBootstrap
        // (LinkedMultivariateStudentT was removed — see source comment.)
        Assert.AreEqual(4, members.Length,
            "UncertaintyMethod is expected to have exactly four members; flag a deliberate API change here.");
    }

    #endregion

    #region CohnConfidenceIntervalResult DTO

    /// <summary>
    /// CohnConfidenceIntervalResult is a plain DTO. Default-constructed instance should
    /// have empty arrays (not null) so consumers can iterate without null checks.
    /// </summary>
    [TestMethod]
    public void CohnConfidenceIntervalResult_DefaultArraysAreEmpty()
    {
        var dto = new CohnConfidenceIntervalResult();

        Assert.IsNotNull(dto.ExceedanceProbabilities);
        Assert.IsNotNull(dto.PointEstimates);
        Assert.IsNotNull(dto.LowerCI);
        Assert.IsNotNull(dto.UpperCI);
        Assert.IsNotNull(dto.Beta1);
        Assert.IsNotNull(dto.Nu);
        Assert.IsNotNull(dto.QuantileVariance);

        Assert.AreEqual(0, dto.ExceedanceProbabilities.Length);
        Assert.AreEqual(0, dto.PointEstimates.Length);
        Assert.AreEqual(0, dto.LowerCI.Length);
        Assert.AreEqual(0, dto.UpperCI.Length);
        Assert.AreEqual(0, dto.Beta1.Length);
        Assert.AreEqual(0, dto.Nu.Length);
        Assert.AreEqual(0, dto.QuantileVariance.Length);

        Assert.AreEqual(0.0, dto.ConfidenceLevel);
    }

    /// <summary>
    /// CohnConfidenceIntervalResult round-trips its set values directly — it has no
    /// custom equality / immutability semantics, so a property setter test is enough.
    /// </summary>
    [TestMethod]
    public void CohnConfidenceIntervalResult_PropertyRoundTrip()
    {
        double[] aeps = [0.5, 0.1, 0.01];
        double[] points = [100, 200, 400];
        double[] lower = [80, 160, 320];
        double[] upper = [120, 240, 480];

        var dto = new CohnConfidenceIntervalResult
        {
            ExceedanceProbabilities = aeps,
            PointEstimates = points,
            LowerCI = lower,
            UpperCI = upper,
            ConfidenceLevel = 0.90,
            Beta1 = [0.1, 0.2, 0.3],
            Nu = [10, 8, 6],
            QuantileVariance = [1.0, 4.0, 16.0],
        };

        CollectionAssert.AreEqual(aeps, dto.ExceedanceProbabilities);
        CollectionAssert.AreEqual(points, dto.PointEstimates);
        CollectionAssert.AreEqual(lower, dto.LowerCI);
        CollectionAssert.AreEqual(upper, dto.UpperCI);
        Assert.AreEqual(0.90, dto.ConfidenceLevel);
        Assert.AreEqual(3, dto.Beta1.Length);
        Assert.AreEqual(3, dto.Nu.Length);
        Assert.AreEqual(3, dto.QuantileVariance.Length);
    }

    #endregion

    #region Uncertainty diagnostic message

    /// <summary>
    /// The uncertainty diagnostic message defaults to empty and is reset by ClearResults,
    /// so a stale abort reason can never survive into the next run's report.
    /// </summary>
    [TestMethod]
    public void UncertaintyDiagnosticMessage_DefaultsEmpty_AndClearedByClearResults()
    {
        var analysis = new Bulletin17CAnalysis(CreateLP3Model());
        Assert.AreEqual(string.Empty, analysis.UncertaintyDiagnosticMessage);

        analysis.ClearResults();

        Assert.AreEqual(string.Empty, analysis.UncertaintyDiagnosticMessage);
    }

    #endregion
}
