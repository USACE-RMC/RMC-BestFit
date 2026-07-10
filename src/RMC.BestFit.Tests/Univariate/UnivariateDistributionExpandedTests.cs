using Numerics.Distributions;
using RMC.BestFit.Models;
using RMC.BestFit.Models.TrendFunctions;
using RMC.BestFit.Models.TrendFunctions.Support;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Expanded programmatic unit tests for <c>UnivariateDistribution</c> targeting
/// nonstationary configuration, the <c>GenerateRandomValues</c> simulation surface,
/// validation edge cases, Clone deep-copy contract, and the static type-support
/// helpers — none of which require running an estimator.
/// </summary>
/// <remarks>
/// Estimation-driven tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class UnivariateDistributionExpandedTests
{
    /// <summary>
    /// Builds a small, well-defined Normal model used as a base across tests.
    /// Deterministic — no RNG dependence.
    /// </summary>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
                new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 })
        };
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    #region IsSupportedDistributionType (static helper)

    /// <summary>
    /// IsSupportedDistributionType returns true for every value in the
    /// 15-distribution canonical list documented in CLAUDE.md.
    /// </summary>
    [TestMethod]
    public void IsSupportedDistributionType_AllSupportedValues_ReturnTrue()
    {
        var supported = new[]
        {
            UnivariateDistributionType.Exponential,
            UnivariateDistributionType.GammaDistribution,
            UnivariateDistributionType.GeneralizedExtremeValue,
            UnivariateDistributionType.GeneralizedLogistic,
            UnivariateDistributionType.GeneralizedNormal,
            UnivariateDistributionType.GeneralizedPareto,
            UnivariateDistributionType.Gumbel,
            UnivariateDistributionType.KappaFour,
            UnivariateDistributionType.LnNormal,
            UnivariateDistributionType.Logistic,
            UnivariateDistributionType.LogNormal,
            UnivariateDistributionType.LogPearsonTypeIII,
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.PearsonTypeIII,
            UnivariateDistributionType.Weibull,
        };

        foreach (var t in supported)
        {
            Assert.IsTrue(UnivariateDistribution.IsSupportedDistributionType(t),
                $"{t} should be a supported distribution type.");
        }
    }

    /// <summary>
    /// CreateDistribution with Normal returns a Normal instance.
    /// </summary>
    [TestMethod]
    public void CreateDistribution_Normal_ReturnsNormalInstance()
    {
        var dist = UnivariateDistribution.CreateDistribution(UnivariateDistributionType.Normal);

        Assert.IsInstanceOfType(dist, typeof(Normal));
    }

    /// <summary>
    /// CreateDistribution with Gumbel returns a Gumbel instance.
    /// </summary>
    [TestMethod]
    public void CreateDistribution_Gumbel_ReturnsGumbelInstance()
    {
        var dist = UnivariateDistribution.CreateDistribution(UnivariateDistributionType.Gumbel);

        Assert.IsInstanceOfType(dist, typeof(Gumbel));
    }

    /// <summary>
    /// CreateDistribution with GeneralizedExtremeValue returns a GEV instance.
    /// </summary>
    [TestMethod]
    public void CreateDistribution_GEV_ReturnsGEVInstance()
    {
        var dist = UnivariateDistribution.CreateDistribution(UnivariateDistributionType.GeneralizedExtremeValue);

        Assert.IsInstanceOfType(dist, typeof(GeneralizedExtremeValue));
    }

    #endregion

    #region IsNonstationary toggle

    /// <summary>
    /// Setting IsNonstationary to true creates trend models for every distribution parameter.
    /// </summary>
    [TestMethod]
    public void IsNonstationary_SetTrue_PopulatesTrendModels()
    {
        // Arrange
        var model = MakeNormalModel();
        Assert.IsFalse(model.IsNonstationary, "Default should be stationary.");

        // Act
        model.IsNonstationary = true;

        // Assert
        Assert.IsTrue(model.IsNonstationary);
        Assert.IsNotNull(model.TrendModels);
        Assert.IsTrue(model.TrendModels.Count >= model.Distribution.ParameterNames.Length);
    }

    /// <summary>
    /// Toggling IsNonstationary back to false leaves a constant trend per parameter.
    /// </summary>
    [TestMethod]
    public void IsNonstationary_TrueThenFalse_TrendModelsAreConstant()
    {
        // Arrange
        var model = MakeNormalModel();
        model.IsNonstationary = true;

        // Act
        model.IsNonstationary = false;

        // Assert
        Assert.IsFalse(model.IsNonstationary);
        foreach (var t in model.TrendModels)
        {
            Assert.AreEqual(TrendModelType.Constant, t.Type);
        }
    }

    /// <summary>
    /// Setting IsNonstationary to its current value is a no-op (no PropertyChanged event).
    /// </summary>
    [TestMethod]
    public void IsNonstationary_SetToCurrentValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var model = MakeNormalModel();
        bool fired = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateDistribution.IsNonstationary))
                fired = true;
        };

        // Act
        model.IsNonstationary = false;  // already false

        // Assert
        Assert.IsFalse(fired);
    }

    /// <summary>
    /// Alpha setter round-trips the value and raises PropertyChanged on change.
    /// </summary>
    [TestMethod]
    public void Alpha_SetterRoundTrips()
    {
        // Arrange
        var model = MakeNormalModel();
        bool fired = false;
        model.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(UnivariateDistribution.Alpha)) fired = true;
        };

        // Act
        model.Alpha = 0.05;

        // Assert
        Assert.AreEqual(0.05, model.Alpha, 1e-12);
        Assert.IsTrue(fired);
    }

    /// <summary>
    /// ParameterTimeIndex setter round-trips.
    /// </summary>
    [TestMethod]
    public void ParameterTimeIndex_SetterRoundTrips()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        model.ParameterTimeIndex = 1995;

        // Assert
        Assert.AreEqual(1995, model.ParameterTimeIndex);
    }

    #endregion

    #region GenerateRandomValues

    /// <summary>
    /// GenerateRandomValues returns an array of the requested length.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_RequestedSize_ReturnsArrayOfThatLength()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([100.0, 15.0]);

        // Act
        var samples = model.GenerateRandomValues(50, seed: 42);

        // Assert
        Assert.AreEqual(50, samples.Length);
    }

    /// <summary>
    /// GenerateRandomValues with a fixed seed is reproducible across calls.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_SameSeed_IsReproducible()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([100.0, 15.0]);

        // Act
        var a = model.GenerateRandomValues(20, seed: 7);
        var b = model.GenerateRandomValues(20, seed: 7);

        // Assert
        for (int i = 0; i < 20; i++)
            Assert.AreEqual(a[i], b[i], 1e-12);
    }

    /// <summary>
    /// GenerateRandomValues with different seeds produces different samples.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_DifferentSeeds_ProducesDifferentSamples()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([100.0, 15.0]);

        // Act
        var a = model.GenerateRandomValues(20, seed: 1);
        var b = model.GenerateRandomValues(20, seed: 2);

        // Assert
        bool anyDiff = false;
        for (int i = 0; i < 20; i++)
            if (Math.Abs(a[i] - b[i]) > 1e-9) { anyDiff = true; break; }
        Assert.IsTrue(anyDiff, "Different seeds should yield at least one different sample.");
    }

    /// <summary>
    /// GenerateRandomValues throws ArgumentOutOfRangeException when sample size is zero.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_SampleSizeZero_Throws()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => model.GenerateRandomValues(0, seed: 1));
    }

    /// <summary>
    /// GenerateRandomValues throws ArgumentOutOfRangeException when sample size is negative.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_NegativeSampleSize_Throws()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act & Assert
        Assert.ThrowsException<ArgumentOutOfRangeException>(
            () => model.GenerateRandomValues(-5, seed: 1));
    }

    #endregion

    #region Clone

    /// <summary>
    /// Clone produces a copy that is not the same reference.
    /// </summary>
    [TestMethod]
    public void Clone_ProducesDistinctInstance()
    {
        // Arrange
        var original = MakeNormalModel();

        // Act
        var clone = original.Clone();

        // Assert
        Assert.AreNotSame(original, clone);
    }

    /// <summary>
    /// Clone preserves DistributionType.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesDistributionType()
    {
        // Arrange
        var original = MakeNormalModel();

        // Act
        var clone = (UnivariateDistribution)original.Clone();

        // Assert
        Assert.AreEqual(original.DistributionType, clone.DistributionType);
    }

    /// <summary>
    /// Clone preserves parameter values.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesParameterValues()
    {
        // Arrange
        var original = MakeNormalModel();
        original.SetParameterValues([105.0, 17.5]);

        // Act
        var clone = (UnivariateDistribution)original.Clone();

        // Assert
        for (int i = 0; i < original.Parameters.Count; i++)
            Assert.AreEqual(original.Parameters[i].Value, clone.Parameters[i].Value, 1e-12);
    }

    /// <summary>
    /// Clone preserves IsNonstationary.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesIsNonstationary()
    {
        // Arrange
        var original = MakeNormalModel();
        original.IsNonstationary = true;

        // Act
        var clone = (UnivariateDistribution)original.Clone();

        // Assert
        Assert.AreEqual(original.IsNonstationary, clone.IsNonstationary);
    }

    /// <summary>
    /// Clone preserves Alpha.
    /// </summary>
    [TestMethod]
    public void Clone_PreservesAlpha()
    {
        // Arrange
        var original = MakeNormalModel();
        original.Alpha = 0.25;

        // Act
        var clone = (UnivariateDistribution)original.Clone();

        // Assert
        Assert.AreEqual(0.25, clone.Alpha, 1e-12);
    }

    /// <summary>
    /// Clone produces parameters that are independent (changing the clone does not
    /// affect the original).
    /// </summary>
    [TestMethod]
    public void Clone_ParametersAreIndependent()
    {
        // Arrange
        var original = MakeNormalModel();
        original.SetParameterValues([100.0, 15.0]);

        // Act
        var clone = (UnivariateDistribution)original.Clone();
        clone.Parameters[0].Value = 999.0;

        // Assert
        Assert.AreEqual(100.0, original.Parameters[0].Value, 1e-12,
            "Modifying clone.Parameters should not affect original.Parameters.");
    }

    #endregion

    #region Validate edge cases

    /// <summary>
    /// Validate returns valid=true on a fresh well-configured model.
    /// </summary>
    [TestMethod]
    public void Validate_WellFormedModel_IsValid()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        var (isValid, _) = model.Validate();

        // Assert
        Assert.IsTrue(isValid);
    }

    /// <summary>
    /// Validate returns invalid when the data frame is null.
    /// </summary>
    [TestMethod]
    public void Validate_NullDataFrame_IsInvalid()
    {
        // Arrange
        var model = MakeNormalModel();
        model.DataFrame = null!;

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("DataFrame")), $"Expected DataFrame error. Got: {string.Join("; ", messages)}");
    }

    /// <summary>
    /// Validate returns invalid for log-based distributions with non-positive data.
    /// </summary>
    [TestMethod]
    public void Validate_LogDistributionWithNegativeData_IsInvalid()
    {
        // Arrange
        var df = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new double[] { -1.0, 5.0, 10.0, 20.0, 100.0 })
        };
        var model = new UnivariateDistribution(df, UnivariateDistributionType.LogNormal);

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Log") || m.Contains("non positive")),
            $"Expected log-based / non-positive error. Got: {string.Join("; ", messages)}");
    }

    /// <summary>
    /// Validate returns invalid when nonstationary with Alpha = 0.
    /// </summary>
    [TestMethod]
    public void Validate_NonstationaryWithZeroAlpha_IsInvalid()
    {
        // Arrange
        var model = MakeNormalModel();
        model.IsNonstationary = true;
        model.Alpha = 0.0;

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Alpha")),
            $"Expected Alpha-related validation error. Got: {string.Join("; ", messages)}");
    }

    /// <summary>
    /// Validate returns invalid when nonstationary with Alpha = 1.
    /// </summary>
    [TestMethod]
    public void Validate_NonstationaryWithAlphaOne_IsInvalid()
    {
        // Arrange
        var model = MakeNormalModel();
        model.IsNonstationary = true;
        model.Alpha = 1.0;

        // Act
        var (isValid, messages) = model.Validate();

        // Assert
        Assert.IsFalse(isValid);
        Assert.IsTrue(messages.Any(m => m.Contains("Alpha")));
    }

    #endregion

    #region GetParameterValues

    /// <summary>
    /// GetParameterValues returns an array with the model's parameter count.
    /// </summary>
    [TestMethod]
    public void GetParameterValues_ReturnsParameterCount()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([100.0, 15.0]);

        // Act
        var values = model.GetParameterValues(0);

        // Assert: stationary Normal returns 2 parameters
        Assert.AreEqual(2, values.Length);
    }

    /// <summary>
    /// GetParameterValues returns the current set parameter values.
    /// </summary>
    [TestMethod]
    public void GetParameterValues_ReturnsCurrentValues()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([42.0, 7.5]);

        // Act
        var values = model.GetParameterValues(0);

        // Assert
        Assert.AreEqual(42.0, values[0], 1e-12);
        Assert.AreEqual(7.5, values[1], 1e-12);
    }

    #endregion

    #region Pointwise log-likelihood invariants

    /// <summary>
    /// PointwiseDataLogLikelihood length equals the exact series count.
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_LengthEqualsSeriesCount()
    {
        // Arrange
        var model = MakeNormalModel();
        model.SetParameterValues([16500.0, 6000.0]);

        // Act
        var pw = model.PointwiseDataLogLikelihood(new[] { 16500.0, 6000.0 });

        // Assert
        Assert.AreEqual(model.DataFrame.ExactSeries.Count, pw.Length);
    }

    /// <summary>
    /// PointwiseDataLogLikelihood sum equals DataLogLikelihood (within numerical tolerance).
    /// </summary>
    [TestMethod]
    public void PointwiseDataLogLikelihood_SumEqualsDataLogLikelihood()
    {
        // Arrange
        var model = MakeNormalModel();
        var p = new[] { 16500.0, 6000.0 };

        // Act
        var pw = model.PointwiseDataLogLikelihood(p);
        var total = model.DataLogLikelihood(p);

        // Assert
        Assert.AreEqual(total, pw.Sum(), 1e-6);
    }

    /// <summary>
    /// LogLikelihood = DataLogLikelihood + PriorLogLikelihood (sign-convention invariant).
    /// </summary>
    [TestMethod]
    public void LogLikelihood_EqualsDataPlusPrior()
    {
        // Arrange
        var model = MakeNormalModel();
        var p = new[] { 16500.0, 6000.0 };

        // Act
        double total = model.LogLikelihood(p);
        double dataLL = model.DataLogLikelihood(p);
        double priorLL = model.PriorLogLikelihood(p);

        // Assert
        Assert.AreEqual(dataLL + priorLL, total, 1e-6);
    }

    #endregion

    #region Distribution swap

    /// <summary>
    /// Setting Distribution to a new type updates DistributionType.
    /// </summary>
    [TestMethod]
    public void Distribution_SetToNewType_UpdatesDistributionType()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        model.Distribution = UnivariateDistribution.CreateDistribution(UnivariateDistributionType.Gumbel);

        // Assert
        Assert.AreEqual(UnivariateDistributionType.Gumbel, model.DistributionType);
    }

    #endregion
}
