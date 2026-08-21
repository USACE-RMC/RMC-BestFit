using System.ComponentModel;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.ModelEstimation;

/// <summary>
/// Programmatic property-round-trip and configuration unit tests for the
/// <c>BayesianAnalysis</c> class. These tests do NOT run any MCMC sampler —
/// they only exercise the property setters/getters, default-options helpers,
/// validation, and the SamplerType / PointEstimateType enums.
/// </summary>
/// <remarks>
/// Computational MCMC tests live in <c>RMC.BestFit.Verification</c>.
/// </remarks>
[TestClass]
public class BayesianAnalysisPropertyTests
{
    /// <summary>
    /// Creates a small Normal model for use across tests.
    /// </summary>
    private static UnivariateDistribution MakeNormalModel()
    {
        var df = new BestFitDataFrame();
        var values = new double[] { 12500, 15300, 8900, 22100, 18700, 14200, 9800, 28500, 17400, 11600 };
        for (int i = 0; i < values.Length; i++)
            df.ExactSeries.Add(new ExactData(1990 + i, values[i]));
        return new UnivariateDistribution(df, UnivariateDistributionType.Normal);
    }

    /// <summary>
    /// Creates deterministic two-parameter MCMC results without running a sampler.
    /// </summary>
    /// <param name="offset">A value added to every retained parameter value.</param>
    /// <returns>Processed MCMC results containing four retained parameter sets.</returns>
    private static MCMCResults MakeResults(double offset = 0.0)
    {
        var output = new List<ParameterSet>
        {
            new(new[] { 1.0 + offset, 2.0 + offset }, -4.0),
            new(new[] { 1.1 + offset, 2.2 + offset }, -3.0),
            new(new[] { 0.9 + offset, 1.8 + offset }, -2.0),
            new(new[] { 1.2 + offset, 2.1 + offset }, -1.0)
        };
        return new MCMCResults(output[^1], output, alpha: 0.10);
    }

    #region Results notification tests

    /// <summary>
    /// Installing results notifies observers before the analysis becomes estimated.
    /// </summary>
    [TestMethod]
    public void SetCustomMCMCResults_RaisesResultsBeforeIsEstimated()
    {
        var bayesian = new BayesianAnalysis(MakeNormalModel());
        var notifications = new List<string?>();
        bayesian.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results) ||
                e.PropertyName == nameof(BayesianAnalysis.IsEstimated))
            {
                notifications.Add(e.PropertyName);
            }
        };

        bayesian.SetCustomMCMCResults(MakeResults(), skipInformationCriteria: true);

        CollectionAssert.AreEqual(
            new[] { nameof(BayesianAnalysis.Results), nameof(BayesianAnalysis.IsEstimated) },
            notifications);
    }

    /// <summary>
    /// Replacing results on an already-estimated analysis still notifies observers exactly once.
    /// </summary>
    [TestMethod]
    public void SetCustomMCMCResults_ReplacesResults_RaisesResultsExactlyOnce()
    {
        var bayesian = new BayesianAnalysis(MakeNormalModel());
        bayesian.SetCustomMCMCResults(MakeResults(), skipInformationCriteria: true);
        int resultsNotifications = 0;
        int estimatedNotifications = 0;
        bayesian.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) resultsNotifications++;
            if (e.PropertyName == nameof(BayesianAnalysis.IsEstimated)) estimatedNotifications++;
        };

        bayesian.SetCustomMCMCResults(MakeResults(10.0), skipInformationCriteria: true);

        Assert.AreEqual(1, resultsNotifications);
        Assert.AreEqual(0, estimatedNotifications);
    }

    /// <summary>
    /// Clearing results notifies once for a real reference change and not for a repeated null assignment.
    /// </summary>
    [TestMethod]
    public void ClearResults_RaisesResultsOnlyWhenReferenceChanges()
    {
        var bayesian = new BayesianAnalysis(MakeNormalModel());
        bayesian.SetCustomMCMCResults(MakeResults(), skipInformationCriteria: true);
        int resultsNotifications = 0;
        bayesian.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.Results)) resultsNotifications++;
        };

        bayesian.ClearResults();
        bayesian.ClearResults();

        Assert.AreEqual(1, resultsNotifications);
    }

    #endregion

    #region Type / SamplerType Tests

    /// <summary>
    /// Default sampler type is DEMCzs (the workhorse per the project Bayesian-first philosophy).
    /// </summary>
    [TestMethod]
    public void Type_Default_IsDEMCzs()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.AreEqual(BayesianAnalysis.SamplerType.DEMCzs, bayesian.Type);
    }

    /// <summary>
    /// Type setter accepts every SamplerType value and round-trips.
    /// </summary>
    [TestMethod]
    public void Type_SetterRoundTrips_ForEverySamplerValue()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        foreach (BayesianAnalysis.SamplerType type in Enum.GetValues<BayesianAnalysis.SamplerType>())
        {
            // Act
            bayesian.Type = type;

            // Assert
            Assert.AreEqual(type, bayesian.Type);
        }
    }

    /// <summary>
    /// Type setter raises PropertyChanged with the Type name when the value actually changes.
    /// </summary>
    /// <remarks>
    /// Setting Type triggers ClearResults, which fires additional PropertyChanged events
    /// (e.g., for ThinningInterval if defaults are reapplied), so the assertion checks
    /// for inclusion rather than equality of the most-recent event name.
    /// </remarks>
    [TestMethod]
    public void Type_SetToNewValue_RaisesPropertyChanged()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel())
        {
            Type = BayesianAnalysis.SamplerType.DEMCzs
        };
        var changedNames = new List<string?>();
        bayesian.PropertyChanged += (_, e) => changedNames.Add(e.PropertyName);

        // Act
        bayesian.Type = BayesianAnalysis.SamplerType.NUTS;

        // Assert
        CollectionAssert.Contains(changedNames, nameof(BayesianAnalysis.Type));
    }

    /// <summary>
    /// Type setter does NOT raise PropertyChanged when the value is unchanged.
    /// </summary>
    [TestMethod]
    public void Type_SetToSameValue_DoesNotRaisePropertyChanged()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());
        bool eventFired = false;
        bayesian.PropertyChanged += (_, _) => eventFired = true;

        // Act
        bayesian.Type = bayesian.Type;

        // Assert
        Assert.IsFalse(eventFired, "PropertyChanged should not fire when the new value equals the old.");
    }

    #endregion

    #region MCMC iteration / thinning configuration

    /// <summary>
    /// Iterations setter round-trips.
    /// </summary>
    [TestMethod]
    public void Iterations_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.Iterations = 7777;

        // Assert
        Assert.AreEqual(7777, bayesian.Iterations);
    }

    /// <summary>
    /// WarmupIterations setter round-trips.
    /// </summary>
    [TestMethod]
    public void WarmupIterations_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.WarmupIterations = 555;

        // Assert
        Assert.AreEqual(555, bayesian.WarmupIterations);
    }

    /// <summary>
    /// ThinningInterval setter round-trips.
    /// </summary>
    [TestMethod]
    public void ThinningInterval_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.ThinningInterval = 25;

        // Assert
        Assert.AreEqual(25, bayesian.ThinningInterval);
    }

    /// <summary>
    /// NumberOfChains setter round-trips.
    /// </summary>
    [TestMethod]
    public void NumberOfChains_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.NumberOfChains = 16;

        // Assert
        Assert.AreEqual(16, bayesian.NumberOfChains);
    }

    /// <summary>
    /// PRNGSeed setter round-trips.
    /// </summary>
    [TestMethod]
    public void PRNGSeed_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.PRNGSeed = 99999;

        // Assert
        Assert.AreEqual(99999, bayesian.PRNGSeed);
    }

    /// <summary>
    /// InitialIterations setter round-trips.
    /// </summary>
    [TestMethod]
    public void InitialIterations_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.InitialIterations = 2500;

        // Assert
        Assert.AreEqual(2500, bayesian.InitialIterations);
    }

    /// <summary>
    /// Iterations setter raises a PropertyChanged event with the correct property name.
    /// </summary>
    [TestMethod]
    public void Iterations_RaisesPropertyChangedWithCorrectName()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());
        var changes = new List<string?>();
        bayesian.PropertyChanged += (_, e) => changes.Add(e.PropertyName);

        // Act
        bayesian.Iterations = 12345;

        // Assert
        CollectionAssert.Contains(changes, nameof(BayesianAnalysis.Iterations));
    }

    #endregion

    #region Advanced options round-trip

    /// <summary>
    /// Jump setter round-trips.
    /// </summary>
    [TestMethod]
    public void Jump_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.Jump = 0.85;

        // Assert
        Assert.AreEqual(0.85, bayesian.Jump, 1e-12);
    }

    /// <summary>
    /// JumpThreshold setter round-trips.
    /// </summary>
    [TestMethod]
    public void JumpThreshold_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.JumpThreshold = 0.20;

        // Assert
        Assert.AreEqual(0.20, bayesian.JumpThreshold, 1e-12);
    }

    /// <summary>
    /// SnookerThreshold setter round-trips.
    /// </summary>
    [TestMethod]
    public void SnookerThreshold_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.SnookerThreshold = 0.15;

        // Assert
        Assert.AreEqual(0.15, bayesian.SnookerThreshold, 1e-12);
    }

    /// <summary>
    /// Noise setter round-trips.
    /// </summary>
    [TestMethod]
    public void Noise_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.Noise = 1e-7;

        // Assert
        Assert.AreEqual(1e-7, bayesian.Noise, 1e-15);
    }

    /// <summary>
    /// Scale setter round-trips.
    /// </summary>
    [TestMethod]
    public void Scale_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.Scale = 1.75;

        // Assert
        Assert.AreEqual(1.75, bayesian.Scale, 1e-12);
    }

    /// <summary>
    /// Beta setter round-trips.
    /// </summary>
    [TestMethod]
    public void Beta_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.Beta = 0.07;

        // Assert
        Assert.AreEqual(0.07, bayesian.Beta, 1e-12);
    }

    /// <summary>
    /// MaxTreeDepth setter round-trips.
    /// </summary>
    [TestMethod]
    public void MaxTreeDepth_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.MaxTreeDepth = 12;

        // Assert
        Assert.AreEqual(12, bayesian.MaxTreeDepth);
    }

    #endregion

    #region Output options

    /// <summary>
    /// CredibleIntervalWidth setter round-trips.
    /// </summary>
    [TestMethod]
    public void CredibleIntervalWidth_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.CredibleIntervalWidth = 0.95;

        // Assert
        Assert.AreEqual(0.95, bayesian.CredibleIntervalWidth, 1e-12);
    }

    /// <summary>
    /// OutputLength setter round-trips.
    /// </summary>
    [TestMethod]
    public void OutputLength_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.OutputLength = 5000;

        // Assert
        Assert.AreEqual(5000, bayesian.OutputLength);
    }

    /// <summary>
    /// PointEstimator default value is PosteriorMean.
    /// </summary>
    [TestMethod]
    public void PointEstimator_DefaultIsPosteriorMean()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, bayesian.PointEstimator);
    }

    /// <summary>
    /// PointEstimator setter round-trips for both PosteriorMean and PosteriorMode.
    /// </summary>
    [TestMethod]
    public void PointEstimator_SetterRoundTripsForBothValues()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act + Assert
        bayesian.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMode, bayesian.PointEstimator);

        bayesian.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        Assert.AreEqual(BayesianAnalysis.PointEstimateType.PosteriorMean, bayesian.PointEstimator);
    }

    /// <summary>
    /// PointEstimator change raises PropertyChanged.
    /// </summary>
    [TestMethod]
    public void PointEstimator_ChangeRaisesPropertyChanged()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel())
        {
            PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean
        };
        bool fired = false;
        bayesian.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(BayesianAnalysis.PointEstimator)) fired = true;
        };

        // Act
        bayesian.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMode;

        // Assert
        Assert.IsTrue(fired);
    }

    #endregion

    #region Default options behaviour

    /// <summary>
    /// SetDefaultSimulationOptions is callable and yields positive iteration counts.
    /// </summary>
    [TestMethod]
    public void SetDefaultSimulationOptions_PopulatesPositiveCounts()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.SetDefaultSimulationOptions();

        // Assert
        Assert.IsTrue(bayesian.NumberOfChains > 0);
        Assert.IsTrue(bayesian.Iterations > 0);
        Assert.IsTrue(bayesian.WarmupIterations >= 0);
        Assert.IsTrue(bayesian.ThinningInterval >= 1);
    }

    /// <summary>
    /// SetDefaultAdvancedSimulationOptions is callable and produces sensible values.
    /// </summary>
    [TestMethod]
    public void SetDefaultAdvancedSimulationOptions_PopulatesSaneValues()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        bayesian.SetDefaultAdvancedSimulationOptions();

        // Assert: jump positive, snooker threshold in [0,1], noise non-negative.
        Assert.IsTrue(bayesian.Jump > 0);
        Assert.IsTrue(bayesian.SnookerThreshold >= 0 && bayesian.SnookerThreshold <= 1);
        Assert.IsTrue(bayesian.Noise >= 0);
    }

    /// <summary>
    /// UseSimulationDefaults set to true triggers SetDefaultSimulationOptions, which
    /// resets the chain/thinning configuration to sampler-appropriate values.
    /// </summary>
    [TestMethod]
    public void UseSimulationDefaults_SetTrue_AppliesDefaults()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel())
        {
            // Set non-default values to confirm they get overridden by defaults.
            NumberOfChains = 1,
            Iterations = 100
        };

        // Act
        bayesian.UseSimulationDefaults = true;

        // Assert: defaults must produce values >= 1 chain and a sensible iteration count.
        Assert.IsTrue(bayesian.UseSimulationDefaults);
        Assert.IsTrue(bayesian.NumberOfChains >= 1);
        Assert.IsTrue(bayesian.Iterations >= 100);
    }

    /// <summary>
    /// UseAdvancedSimulationDefaults transitioning from false to true triggers
    /// SetDefaultAdvancedSimulationOptions, which restores positive defaults
    /// (e.g., Jump = 2.38/√(2d) for DEMC samplers).
    /// </summary>
    /// <remarks>
    /// The setter only fires the default-options helper when the value actually
    /// changes, so the test starts from <c>false</c> and asserts the upward edge.
    /// </remarks>
    [TestMethod]
    public void UseAdvancedSimulationDefaults_FalseToTrue_AppliesDefaults()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel())
        {
            UseAdvancedSimulationDefaults = false
        };
        bayesian.Jump = 0.0;  // unrealistic; defaults helper should overwrite this

        // Act
        bayesian.UseAdvancedSimulationDefaults = true;

        // Assert
        Assert.IsTrue(bayesian.UseAdvancedSimulationDefaults);
        Assert.IsTrue(bayesian.Jump > 0, $"Jump should be set to positive default but was {bayesian.Jump}.");
    }

    #endregion

    #region Validation

    /// <summary>
    /// Validate returns IsValid=true on a fresh, well-configured BayesianAnalysis.
    /// </summary>
    /// <remarks>
    /// On a fresh analysis IsEstimated is false, so Validate adds a "model has not
    /// been estimated" warning — the message list is non-empty, but IsValid is still
    /// true (warnings are advisory, errors flip the bool).
    /// </remarks>
    [TestMethod]
    public void Validate_FreshAnalysis_IsValid()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Act
        var (isValid, messages) = bayesian.Validate();

        // Assert: warnings are allowed; only errors flip IsValid to false.
        Assert.IsTrue(isValid, $"Validation should be valid (warnings only). Messages: {string.Join("; ", messages)}");
        // No "Error:" lines should appear.
        Assert.IsFalse(messages.Any(m => m.StartsWith("Error:")),
            $"Fresh analysis should produce no error-level validation messages. Got: {string.Join("; ", messages)}");
    }

    #endregion

    #region IsEstimated / Results state

    /// <summary>
    /// IsEstimated is false on a fresh analysis (no MCMC has been run yet).
    /// </summary>
    [TestMethod]
    public void IsEstimated_OnFreshAnalysis_IsFalse()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsFalse(bayesian.IsEstimated);
    }

    /// <summary>
    /// Results is null on a fresh analysis.
    /// </summary>
    [TestMethod]
    public void Results_OnFreshAnalysis_IsNull()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsNull(bayesian.Results);
    }

    /// <summary>
    /// Sampler is set up automatically when the constructor receives a valid model
    /// (via ClearResults → SetUpSampler).
    /// </summary>
    [TestMethod]
    public void Sampler_AfterModelConstruction_IsNotNull()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert: with a valid model, the sampler is set up immediately.
        Assert.IsNotNull(bayesian.Sampler);
    }

    /// <summary>
    /// Sampler is null on an analysis constructed with no model.
    /// </summary>
    [TestMethod]
    public void Sampler_NoModel_IsNull()
    {
        // Arrange
        var bayesian = new BayesianAnalysis();

        // Assert
        Assert.IsNull(bayesian.Sampler);
    }

    /// <summary>
    /// LastError is null on a fresh analysis.
    /// </summary>
    [TestMethod]
    public void LastError_OnFreshAnalysis_IsNull()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsNull(bayesian.LastError);
    }

    /// <summary>
    /// ElapsedTime is null on a fresh analysis.
    /// </summary>
    [TestMethod]
    public void ElapsedTime_OnFreshAnalysis_IsNull()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsNull(bayesian.ElapsedTime);
    }

    /// <summary>
    /// NumberOfEstimatedParameters reflects the model's parameter count.
    /// </summary>
    [TestMethod]
    public void NumberOfEstimatedParameters_MatchesModelParameters()
    {
        // Arrange
        var model = MakeNormalModel();
        var bayesian = new BayesianAnalysis(model);

        // Assert
        Assert.AreEqual(model.Parameters.Count, bayesian.NumberOfEstimatedParameters);
    }

    /// <summary>
    /// ParameterNames matches the model's parameter names.
    /// </summary>
    [TestMethod]
    public void ParameterNames_MatchesModelParameterNames()
    {
        // Arrange
        var model = MakeNormalModel();
        var bayesian = new BayesianAnalysis(model);

        // Act
        var names = bayesian.ParameterNames;

        // Assert
        Assert.IsNotNull(names);
        Assert.AreEqual(model.Parameters.Count, names!.Count);
    }

    /// <summary>
    /// Model property setter assigns and round-trips.
    /// </summary>
    [TestMethod]
    public void Model_SetterRoundTrips()
    {
        // Arrange
        var bayesian = new BayesianAnalysis();
        var model = MakeNormalModel();

        // Act
        bayesian.Model = model;

        // Assert
        Assert.AreSame(model, bayesian.Model);
    }

    /// <summary>
    /// ModelPropertiesToIgnore default is empty list.
    /// </summary>
    [TestMethod]
    public void ModelPropertiesToIgnore_Default_IsEmpty()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsNotNull(bayesian.ModelPropertiesToIgnore);
        Assert.AreEqual(0, bayesian.ModelPropertiesToIgnore.Count);
    }

    /// <summary>
    /// Default DIC / WAIC / LOOIC are all NaN before a fit.
    /// </summary>
    [TestMethod]
    public void InformationCriteria_BeforeFit_AreAllNaN()
    {
        // Arrange
        var bayesian = new BayesianAnalysis(MakeNormalModel());

        // Assert
        Assert.IsTrue(double.IsNaN(bayesian.DIC));
        Assert.IsTrue(double.IsNaN(bayesian.WAIC));
        Assert.IsTrue(double.IsNaN(bayesian.LOOIC));
    }

    #endregion

    #region Empty constructor

    /// <summary>
    /// Empty constructor produces an analysis with null Model.
    /// </summary>
    [TestMethod]
    public void EmptyConstructor_LeavesModelNull()
    {
        // Act
        var bayesian = new BayesianAnalysis();

        // Assert
        Assert.IsNull(bayesian.Model);
    }

    /// <summary>
    /// Constructor with explicit SamplerType honors the supplied type.
    /// </summary>
    [TestMethod]
    public void Constructor_WithSamplerType_UsesSuppliedType()
    {
        // Arrange
        var model = MakeNormalModel();

        // Act
        var bayesian = new BayesianAnalysis(model, BayesianAnalysis.SamplerType.NUTS);

        // Assert
        Assert.AreEqual(BayesianAnalysis.SamplerType.NUTS, bayesian.Type);
    }

    #endregion
}
