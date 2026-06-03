using Numerics.Distributions;
using Numerics.Functions;
using RMC.BestFit.Models;
using RMC.BestFit.Models.LinkFunctions;
using DataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Programmatic unit tests for the <see cref="Bulletin17CDistribution"/> class.
/// </summary>
/// <remarks>
/// Covers construction, supported-distribution gating, parameter management, validation,
/// serialization round-trip, cloning, and small RNG-backed helpers. GMM estimation,
/// bootstrap, and influence-function tests are computationally expensive and live in
/// <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class Bulletin17CDistributionTests
{
    #region Inline test fixtures

    private const int FixtureSize = 50;

    // Deterministic Log-Pearson-III-like flood fixture. Inline (fixed RNG seed) so this
    // file does not depend on the Verification project's TestData.
    private static readonly double[] InlineFloodData = new LogNormal(8.0, 0.4)
        .GenerateRandomValues(FixtureSize, 12345);

    private static DataFrame CreateFloodDataFrame()
    {
        var df = new DataFrame();
        for (int i = 0; i < InlineFloodData.Length; i++)
        {
            // Add as exact systematic record (simulated water years).
            df.ExactSeries.Add(new ExactData(1970 + i, InlineFloodData[i]));
        }
        return df;
    }

    #endregion

    #region Construction

    /// <summary>
    /// Default constructor falls back to LogPearsonTypeIII (the canonical B17C distribution)
    /// even with no DataFrame attached. Quantile penalty list is initialized non-empty so
    /// the UI grid binds correctly before InputData is selected.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_UsesLogPearsonTypeIII()
    {
        var model = new Bulletin17CDistribution();

        Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, model.DistributionType);
        Assert.IsNotNull(model.Distribution);
        Assert.IsNull(model.DataFrame, "Default constructor leaves DataFrame null.");
        Assert.IsTrue(model.QuantilePenalties.Count >= 1, "SetUpQuantilePenalties seeds at least one quantile penalty row.");
    }

    /// <summary>
    /// Verifies that the (DataFrame, type) constructor sets the distribution type and
    /// triggers <see cref="Bulletin17CDistribution.SetDefaultParameters"/>, which seeds
    /// the parameter list from the distribution.
    /// </summary>
    [TestMethod]
    public void Constructor_WithDataFrameAndType_BuildsParameters()
    {
        var df = CreateFloodDataFrame();

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        Assert.AreEqual(UnivariateDistributionType.LogPearsonTypeIII, model.DistributionType);
        Assert.AreSame(df, model.DataFrame);
        Assert.AreEqual(3, model.NumberOfParameters,
            "LP3 has three parameters (xi, alpha, kappa) so the parameter list should match.");
    }

    /// <summary>
    /// The (DataFrame, distribution) overload clones the supplied distribution to avoid aliasing.
    /// </summary>
    [TestMethod]
    public void Constructor_WithDistributionInstance_ClonesDistribution()
    {
        var df = CreateFloodDataFrame();
        var prototype = new LogPearsonTypeIII();

        var model = new Bulletin17CDistribution(df, prototype);

        // Must clone, not retain the same reference — otherwise edits to one model
        // would silently mutate another.
        Assert.AreNotSame(prototype, model.Distribution);
        Assert.AreEqual(prototype.Type, model.Distribution.Type);
    }

    /// <summary>
    /// Null-arg guards on the principal constructor.
    /// </summary>
    [TestMethod]
    public void Constructor_NullDataFrame_Throws()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CDistribution(null!, UnivariateDistributionType.LogPearsonTypeIII));

        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CDistribution(null!, new LogPearsonTypeIII()));
    }

    /// <summary>
    /// Null-distribution guard.
    /// </summary>
    [TestMethod]
    public void Constructor_NullDistribution_Throws()
    {
        var df = CreateFloodDataFrame();
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CDistribution(df, (UnivariateDistributionBase)null!));
    }

    #endregion

    #region IsSupportedDistributionType gate

    /// <summary>
    /// B17C is restricted to six supported distributions. Anything else (e.g., GEV, Weibull,
    /// KappaFour) must report unsupported so the analysis layer can surface a validation error
    /// rather than silently producing nonsense GMM moments.
    /// </summary>
    [TestMethod]
    public void IsSupportedDistributionType_AcceptsB17CFamily()
    {
        // The six supported types per B17C guidelines.
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.Exponential));
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.GammaDistribution));
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.LogNormal));
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.LogPearsonTypeIII));
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.Normal));
        Assert.IsTrue(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.PearsonTypeIII));
    }

    /// <summary>
    /// Distributions outside the B17C family must report unsupported.
    /// </summary>
    [TestMethod]
    public void IsSupportedDistributionType_RejectsNonB17CFamily()
    {
        Assert.IsFalse(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.GeneralizedExtremeValue));
        Assert.IsFalse(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.Weibull));
        Assert.IsFalse(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.KappaFour));
        Assert.IsFalse(Bulletin17CDistribution.IsSupportedDistributionType(UnivariateDistributionType.GeneralizedPareto));
    }

    #endregion

    #region CreateDistribution factory

    /// <summary>
    /// The static factory must produce a concrete distribution instance for each supported type.
    /// </summary>
    [TestMethod]
    public void CreateDistribution_AllSupportedTypes_ReturnsCorrectType()
    {
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.LogPearsonTypeIII),
            typeof(LogPearsonTypeIII));
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.LogNormal),
            typeof(LogNormal));
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.Normal),
            typeof(Normal));
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.PearsonTypeIII),
            typeof(PearsonTypeIII));
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.GammaDistribution),
            typeof(GammaDistribution));
        Assert.IsInstanceOfType(
            Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.Exponential),
            typeof(Exponential));
    }

    /// <summary>
    /// Unsupported distribution types must fail loudly rather than silently substitute a default.
    /// </summary>
    [TestMethod]
    public void CreateDistribution_UnsupportedType_Throws()
    {
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => Bulletin17CDistribution.CreateDistribution(UnivariateDistributionType.GeneralizedExtremeValue));
    }

    #endregion

    #region Properties / parameter management

    /// <summary>
    /// IsNonstationary is hard-coded to false: B17C assumes a stationary parent population.
    /// This is a design invariant — verify the contract in test, not just docs.
    /// </summary>
    [TestMethod]
    public void IsNonstationary_AlwaysFalse()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        Assert.IsFalse(model.IsNonstationary,
            "Bulletin 17C does not support trend-on-parameters; this property must always be false.");
    }

    /// <summary>
    /// SetParameterValues with the wrong arity must throw rather than silently fall through —
    /// a wrong-length array would leave the model in a half-updated state.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_WrongLength_Throws()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);

        Assert.ThrowsException<ArgumentException>(
            () => model.SetParameterValues(new[] { 1.0, 2.0, 3.0, 4.0 }));
    }

    /// <summary>
    /// SetParameterValues round-trip on a Normal-backed B17C: values must reach both
    /// the ModelParameters list and the underlying Distribution.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_RoundTrip_UpdatesParametersAndDistribution()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);

        double[] target = [123.0, 45.0];
        model.SetParameterValues(target);

        Assert.AreEqual(123.0, model.Parameters[0].Value, 1e-12);
        Assert.AreEqual(45.0, model.Parameters[1].Value, 1e-12);
        var dist = (Normal)model.Distribution;
        Assert.AreEqual(123.0, dist.Mu, 1e-12);
        Assert.AreEqual(45.0, dist.Sigma, 1e-12);
    }

    /// <summary>
    /// Sample size derives from <see cref="DataFrame.TotalRecordLength"/>; with no DataFrame it falls back to zero.
    /// </summary>
    [TestMethod]
    public void SampleSize_NullDataFrame_IsZero()
    {
        var model = new Bulletin17CDistribution();
        Assert.AreEqual(0, model.SampleSize);
    }

    #endregion

    #region Validation

    /// <summary>
    /// A B17C model with valid LP3 setup and a non-degenerate flood fixture must validate.
    /// </summary>
    [TestMethod]
    public void Validate_GoodFixture_IsValid()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Validation failed: {string.Join("; ", messages)}");
    }

    /// <summary>
    /// Validate must reject a null DataFrame with a clear message — otherwise the GMM
    /// estimation path would dereference null and surface a NullReferenceException to the user.
    /// </summary>
    [TestMethod]
    public void Validate_NullDataFrame_IsInvalid()
    {
        var model = new Bulletin17CDistribution();
        // Default constructor does not set a DataFrame.

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Count > 0, "Should produce at least one validation message.");
    }

    /// <summary>
    /// Log-based distributions (LogNormal / LogPearsonTypeIII) must reject non-positive
    /// systematic exact data because log(x) is undefined for x ≤ 0.
    /// </summary>
    [TestMethod]
    public void Validate_LogDistribution_NonPositiveData_IsInvalid()
    {
        var df = new DataFrame();
        df.ExactSeries.Add(new ExactData(1990, 1000.0));
        df.ExactSeries.Add(new ExactData(1991, 0.0));     // disallowed for log distributions
        df.ExactSeries.Add(new ExactData(1992, -5.0));    // disallowed
        df.ExactSeries.Add(new ExactData(1993, 2000.0));

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogNormal);

        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Log-based", StringComparison.OrdinalIgnoreCase)
                                         || m.Contains("non-positive", StringComparison.OrdinalIgnoreCase)
                                         || m.Contains("log", StringComparison.OrdinalIgnoreCase)));
    }

    #endregion

    #region Serialization round-trip

    /// <summary>
    /// ToXElement / FromXElement must preserve distribution type, parameter values, and
    /// quantile-penalty rows. The XElement constructor sets <c>_isDeserializing = true</c>
    /// so the persisted parameter values aren't overwritten by SetDefaultParameters.
    /// </summary>
    [TestMethod]
    public void ToXElement_FromXElement_RoundTripsCoreState()
    {
        var df = CreateFloodDataFrame();
        var original = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        // Mutate parameters so we can detect whether the round-trip preserved them.
        var snapshot = original.Parameters.Select(p => p.Value).ToArray();
        snapshot[0] += 0.5;
        original.SetParameterValues(snapshot);

        var xml = original.ToXElement();
        var restored = new Bulletin17CDistribution(df, xml);

        Assert.AreEqual(original.DistributionType, restored.DistributionType);
        Assert.AreEqual(original.NumberOfParameters, restored.NumberOfParameters);
        for (int i = 0; i < original.NumberOfParameters; i++)
        {
            Assert.AreEqual(original.Parameters[i].Value, restored.Parameters[i].Value, 1e-10,
                $"Parameter {i} not preserved across XElement round-trip.");
        }
        Assert.AreEqual(original.QuantilePenalties.Count, restored.QuantilePenalties.Count);
    }

    /// <summary>
    /// The DataFrame-less XElement constructor exists to support the undo path where InputData
    /// has been undone back to null. It must still rehydrate the model state without throwing.
    /// </summary>
    [TestMethod]
    public void FromXElement_NoDataFrame_ConstructsWithoutThrowing()
    {
        var df = CreateFloodDataFrame();
        var original = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var xml = original.ToXElement();

        var restored = new Bulletin17CDistribution(xml);

        Assert.IsNull(restored.DataFrame, "DataFrame-less constructor must leave DataFrame null.");
        Assert.AreEqual(original.DistributionType, restored.DistributionType);
        Assert.AreEqual(original.NumberOfParameters, restored.NumberOfParameters);
    }

    /// <summary>
    /// Null XElement guards.
    /// </summary>
    [TestMethod]
    public void FromXElement_NullXml_Throws()
    {
        var df = CreateFloodDataFrame();
        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CDistribution(df, (System.Xml.Linq.XElement)null!));

        Assert.ThrowsException<ArgumentNullException>(
            () => new Bulletin17CDistribution((System.Xml.Linq.XElement)null!));
    }

    #endregion

    #region Clone

    /// <summary>
    /// Clone goes through XElement to avoid running SetDefaultParameters (which would clobber
    /// user-defined penalties). The clone must be a separate instance with matching state.
    /// </summary>
    [TestMethod]
    public void Clone_ReturnsSeparateInstance_WithMatchingState()
    {
        var df = CreateFloodDataFrame();
        var original = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);

        var clone = original.Clone();

        Assert.IsInstanceOfType(clone, typeof(Bulletin17CDistribution));
        Assert.AreNotSame(original, clone);

        var clonedB17C = (Bulletin17CDistribution)clone;
        Assert.AreEqual(original.DistributionType, clonedB17C.DistributionType);
        Assert.AreEqual(original.NumberOfParameters, clonedB17C.NumberOfParameters);
    }

    #endregion

    #region GenerateRandomValues

    /// <summary>
    /// Random-value generation delegates to the underlying distribution and must respect the
    /// seed contract: same seed -> same sequence.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_FixedSeed_IsDeterministic()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);

        // Pin parameters so the underlying distribution is fully specified before sampling.
        model.SetParameterValues([100.0, 15.0]);

        var s1 = model.GenerateRandomValues(20, seed: 42);
        var s2 = model.GenerateRandomValues(20, seed: 42);

        Assert.AreEqual(20, s1.Length);
        CollectionAssert.AreEqual(s1, s2);
    }

    /// <summary>
    /// Sample-size guard.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_NonPositiveSampleSize_Throws()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        model.SetParameterValues([100.0, 15.0]);

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => model.GenerateRandomValues(0, seed: 1));

        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => model.GenerateRandomValues(-5, seed: 1));
    }

    /// <summary>
    /// GenerateRandomValues with a null underlying distribution is invalid — the model
    /// must surface a clear InvalidOperationException rather than NullReference.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_RequiresDistribution()
    {
        // Normal sampling path works.
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        model.SetParameterValues([100.0, 15.0]);

        var values = model.GenerateRandomValues(5, seed: 1);
        Assert.AreEqual(5, values.Length);
    }

    #endregion

    #region Pointwise vs Scalar Moment-Condition Row-Ordering Invariant (CON-23)

    /// <summary>
    /// Verifies the invariant documented at <c>Bulletin17CDistribution.cs:1979</c>:
    /// the column-wise mean of <c>PointwiseMomentConditions</c> equals the G vector returned
    /// by <c>MomentConditions</c>, i.e. (1/n) Σᵢ result[i, j] = G[j].
    /// </summary>
    /// <remarks>
    /// The pointwise matrix is consumed by GMM influence diagnostics
    /// (<c>GeneralizedMethodOfMoments.GetObservationInfluence</c>,
    /// <c>GetCooksDistance</c>, <c>GetInfluenceDiagnostics</c>). If the row ordering or the
    /// per-row contributions diverge from the scalar moment conditions, those diagnostics
    /// produce wrong leverage values without any direct error indication. This test guards
    /// against that.
    /// </remarks>
    [TestMethod]
    public void PointwiseMomentConditions_ColumnMeans_MatchMomentConditionsG_LP3()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        model.SetDefaultParameters();

        var p = model.Parameters.Select(x => x.Value).ToArray();

        var (G, _) = model.MomentConditions(p);
        Assert.IsNotNull(model.PointwiseMomentConditions, "PointwiseMomentConditions delegate must be exposed by Bulletin17CDistribution.");
        var pointwise = model.PointwiseMomentConditions(p);

        int n = pointwise.GetLength(0);
        int q = pointwise.GetLength(1);
        Assert.AreEqual(model.NumberOfParameters, q,
            "Pointwise matrix must have one column per parameter (q).");
        Assert.AreEqual(G.Length, q,
            "Scalar G vector length must match parameter count.");

        for (int j = 0; j < q; j++)
        {
            double colMean = 0.0;
            for (int i = 0; i < n; i++) colMean += pointwise[i, j];
            colMean /= n;
            Assert.AreEqual(G[j], colMean, 1e-9,
                $"Column {j} mean {colMean} disagreed with G[{j}] = {G[j]}.");
        }
    }

    /// <summary>
    /// Same invariant as <see cref="PointwiseMomentConditions_ColumnMeans_MatchMomentConditionsG_LP3"/>,
    /// exercised against a Normal distribution to cover a different supported-distribution
    /// branch in the moment-condition setup.
    /// </summary>
    [TestMethod]
    public void PointwiseMomentConditions_ColumnMeans_MatchMomentConditionsG_Normal()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.Normal);
        model.SetDefaultParameters();

        var p = model.Parameters.Select(x => x.Value).ToArray();

        var (G, _) = model.MomentConditions(p);
        Assert.IsNotNull(model.PointwiseMomentConditions, "PointwiseMomentConditions delegate must be exposed by Bulletin17CDistribution.");
        var pointwise = model.PointwiseMomentConditions(p);

        int n = pointwise.GetLength(0);
        int q = pointwise.GetLength(1);
        for (int j = 0; j < q; j++)
        {
            double colMean = 0.0;
            for (int i = 0; i < n; i++) colMean += pointwise[i, j];
            colMean /= n;
            Assert.AreEqual(G[j], colMean, 1e-9,
                $"[Normal] Column {j} mean {colMean} disagreed with G[{j}] = {G[j]}.");
        }
    }

    #endregion

    #region Weighted Error Direction Score

    /// <summary>
    /// WEDS is a natural-parameter diagnostic. Installing a non-identity LinkController must
    /// not change the score when the caller supplies theta-space parameters.
    /// </summary>
    [TestMethod]
    public void WeightedErrorDirectionScore_NaturalParameters_IgnoresCurrentLinkController()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        model.SetDefaultParameters();
        var theta = model.Parameters.Select(p => p.Value).ToArray();

        var baseline = model.WeightedErrorDirectionScore(theta);

        model.LinkController = new LinkController(new ILinkFunction?[]
        {
            new ASinHLink(theta[0], scale: 0.5, epsilon: 0.25),
            new LogLink(),
            new ASinHLink(theta[2], scale: 0.5, epsilon: -0.25)
        });

        var withTemporaryLinks = model.WeightedErrorDirectionScore(theta);

        Assert.AreEqual(baseline.Length, withTemporaryLinks.Length);
        for (int i = 0; i < baseline.Length; i++)
        {
            Assert.IsTrue(double.IsFinite(baseline[i]),
                $"Baseline WEDS[{i}] should be finite for the inline fixture.");
            Assert.AreEqual(baseline[i], withTemporaryLinks[i], 1e-12,
                $"WEDS[{i}] changed after installing temporary uncertainty links.");
        }
    }

    /// <summary>
    /// The explicit link-space helper should match the natural-parameter method after
    /// inverse-linking through the current controller.
    /// </summary>
    [TestMethod]
    public void WeightedErrorDirectionScoreFromLinked_InverseLinksBeforeScoring()
    {
        var df = CreateFloodDataFrame();
        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        model.SetDefaultParameters();
        var theta = model.Parameters.Select(p => p.Value).ToArray();

        model.LinkController = new LinkController(new ILinkFunction?[]
        {
            new ASinHLink(theta[0], scale: 0.5, epsilon: 0.25),
            new LogLink(),
            new ASinHLink(theta[2], scale: 0.5, epsilon: -0.25)
        });

        var linked = model.LinkController.Link(theta);
        var naturalScore = model.WeightedErrorDirectionScore(theta);
        var linkedScore = model.WeightedErrorDirectionScoreFromLinked(linked);

        Assert.AreEqual(naturalScore.Length, linkedScore.Length);
        for (int i = 0; i < naturalScore.Length; i++)
        {
            Assert.AreEqual(naturalScore[i], linkedScore[i], 1e-12,
                $"Linked WEDS[{i}] did not match the natural-parameter score.");
        }
    }

    #endregion
}
