using System.Reflection;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Regression tests for threshold likelihood records whose below or above count is zero.
/// </summary>
/// <remarks>
/// These are programmatic likelihood-contract tests. They avoid estimation and verify that
/// threshold records with a zero count on one censored side remain finite and preserve the
/// pointwise sum invariant.
/// </remarks>
[TestClass]
public class ThresholdLikelihoodGuardTests
{
    /// <summary>
    /// Identifies the univariate model type used by a threshold likelihood test case.
    /// </summary>
    public enum ModelKind
    {
        /// <summary>Standard univariate distribution wrapper.</summary>
        Univariate,

        /// <summary>Finite mixture model.</summary>
        Mixture,

        /// <summary>Competing risks model.</summary>
        CompetingRisks,

        /// <summary>Point-process model.</summary>
        PointProcess
    }

    /// <summary>
    /// Verifies that threshold likelihoods skip whichever censored side has count zero.
    /// </summary>
    /// <param name="modelKind">The model type to exercise.</param>
    /// <param name="numberBelow">The count below the threshold.</param>
    /// <param name="numberAbove">The count above the threshold.</param>
    /// <remarks>
    /// A threshold window can have one censored side with count zero after deserialization,
    /// resampling, or nonstationary full-time-series expansion. The zero side must contribute
    /// exactly zero log likelihood rather than calling a censored likelihood with count zero.
    /// </remarks>
    [DataTestMethod]
    [DataRow(ModelKind.Univariate, 0, 2)]
    [DataRow(ModelKind.Univariate, 2, 0)]
    [DataRow(ModelKind.Mixture, 0, 2)]
    [DataRow(ModelKind.Mixture, 2, 0)]
    [DataRow(ModelKind.CompetingRisks, 0, 2)]
    [DataRow(ModelKind.CompetingRisks, 2, 0)]
    [DataRow(ModelKind.PointProcess, 0, 2)]
    [DataRow(ModelKind.PointProcess, 2, 0)]
    public void ThresholdLikelihood_OneZeroCountSide_RemainsFiniteAndPointwiseConsistent(
        ModelKind modelKind,
        int numberBelow,
        int numberAbove)
    {
        var (dataFrame, threshold) = CreateThresholdDataFrame();
        var (model, parameters) = CreateModel(modelKind, dataFrame);
        SetThresholdCounts(threshold, numberBelow, numberAbove);

        double total = model.DataLogLikelihood(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        AssertFinite(total, "Total threshold log likelihood should remain finite.");
        Assert.AreEqual(1, pointwise.Length);
        Assert.AreEqual(1, components.Count);
        AssertFinite(pointwise[0], "Pointwise threshold log likelihood should remain finite.");
        Assert.AreEqual(total, pointwise.Sum(), 1e-10);
        Assert.AreEqual(pointwise[0], components[0].LogLikelihood, 1e-10);
        Assert.AreEqual(numberBelow + numberAbove, components[0].Count);
    }

    /// <summary>
    /// Verifies that a threshold record with no below or above counts contributes zero in stationary models.
    /// </summary>
    /// <param name="modelKind">The stationary model type to exercise.</param>
    /// <remarks>
    /// Point-process likelihoods include a separate Poisson rate term, so this zero-only
    /// contribution assertion is scoped to the stationary distribution, mixture, and
    /// competing-risks models whose threshold-only data likelihood is otherwise empty.
    /// </remarks>
    [DataTestMethod]
    [DataRow(ModelKind.Univariate)]
    [DataRow(ModelKind.Mixture)]
    [DataRow(ModelKind.CompetingRisks)]
    public void ThresholdLikelihood_BothCountsZero_ContributesZero(ModelKind modelKind)
    {
        var (dataFrame, threshold) = CreateThresholdDataFrame();
        var (model, parameters) = CreateModel(modelKind, dataFrame);
        SetThresholdCounts(threshold, 0, 0);

        double total = model.DataLogLikelihood(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        Assert.AreEqual(0.0, total, 1e-12);
        Assert.AreEqual(1, pointwise.Length);
        Assert.AreEqual(0.0, pointwise[0], 1e-12);
        Assert.AreEqual(1, components.Count);
        Assert.AreEqual(0.0, components[0].LogLikelihood, 1e-12);
        Assert.AreEqual(0, components[0].Count);
    }

    /// <summary>
    /// Verifies that point-process threshold records with no censored counts keep only the rate term.
    /// </summary>
    /// <remarks>
    /// The point-process likelihood has a global Poisson rate contribution. With no exact,
    /// uncertain, or interval data, the pointwise implementation assigns that term to the
    /// first threshold component even when the threshold record itself has no censored counts.
    /// </remarks>
    [TestMethod]
    public void PointProcessThresholdLikelihood_BothCountsZero_RemainsPointwiseConsistent()
    {
        var (dataFrame, threshold) = CreateThresholdDataFrame();
        var (model, parameters) = CreateModel(ModelKind.PointProcess, dataFrame);
        SetThresholdCounts(threshold, 0, 0);

        double total = model.DataLogLikelihood(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        AssertFinite(total, "Point-process rate term should remain finite.");
        Assert.AreEqual(1, pointwise.Length);
        Assert.AreEqual(total, pointwise.Sum(), 1e-10);
        Assert.AreEqual(1, components.Count);
        Assert.AreEqual(0, components[0].Count);
        Assert.AreEqual(pointwise[0], components[0].LogLikelihood, 1e-10);
    }

    /// <summary>
    /// Verifies nonstationary threshold expansion handles one-sided zero counts.
    /// </summary>
    /// <remarks>
    /// <see cref="DataFrame.CreateFullTimeSeries"/> expands grouped threshold records into
    /// single-count threshold observations where exactly one side has count one and the other
    /// side has count zero. This exercises the nonstationary aggregate, pointwise, and component
    /// likelihood paths.
    /// </remarks>
    [TestMethod]
    public void NonstationaryUnivariateThresholdLikelihood_SplitThresholdsRemainFinite()
    {
        var dataFrame = new DataFrame();
        dataFrame.ThresholdSeries.Add(new ThresholdData(2000, 2002, 100.0) { NumberAbove = 1 });

        var model = new UnivariateDistribution { UseDefaultFlatPriors = false };
        model.Distribution = new Normal(100.0, 10.0);
        model.DataFrame = dataFrame;
        model.IsNonstationary = true;

        double[] parameters = [100.0, 10.0];
        double total = model.DataLogLikelihood(parameters);
        double[] pointwise = model.PointwiseDataLogLikelihood(parameters);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(parameters);

        AssertFinite(total, "Nonstationary threshold log likelihood should remain finite.");
        Assert.AreEqual(3, pointwise.Length);
        Assert.AreEqual(total, pointwise.Sum(), 1e-10);
        Assert.AreEqual(3, components.Count);
        Assert.IsTrue(components.All(component => component.Count == 1));
        Assert.IsTrue(pointwise.All(value => !double.IsNaN(value) && !double.IsInfinity(value)));
    }

    /// <summary>
    /// Creates a data frame with one grouped threshold record.
    /// </summary>
    /// <returns>The data frame and its threshold record.</returns>
    /// <remarks>
    /// Tests adjust the threshold counts after model construction so the model's data-frame
    /// subscription cannot reprocess and overwrite the edge-case counts.
    /// </remarks>
    private static (DataFrame DataFrame, ThresholdData Threshold) CreateThresholdDataFrame()
    {
        var dataFrame = new DataFrame();
        var threshold = new ThresholdData(2000, 2002, 100.0);
        dataFrame.ThresholdSeries.Add(threshold);
        return (dataFrame, threshold);
    }

    /// <summary>
    /// Creates the requested model with fixed distribution parameters.
    /// </summary>
    /// <param name="modelKind">The model type to create.</param>
    /// <param name="dataFrame">The data frame to attach to the model.</param>
    /// <returns>The model and the parameter vector used for likelihood evaluation.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when <paramref name="modelKind"/> is not a known test model kind.
    /// </exception>
    /// <remarks>
    /// Default flat priors are disabled where possible because these tests evaluate fixed
    /// likelihood values rather than estimating parameters from the threshold fixture.
    /// </remarks>
    private static (IModel Model, double[] Parameters) CreateModel(ModelKind modelKind, DataFrame dataFrame)
    {
        return modelKind switch
        {
            ModelKind.Univariate => CreateUnivariateModel(dataFrame),
            ModelKind.Mixture => CreateMixtureModel(dataFrame),
            ModelKind.CompetingRisks => CreateCompetingRisksModel(dataFrame),
            ModelKind.PointProcess => CreatePointProcessModel(dataFrame),
            _ => throw new ArgumentOutOfRangeException(nameof(modelKind), modelKind, "Unknown model kind.")
        };
    }

    /// <summary>
    /// Creates a stationary Normal univariate model.
    /// </summary>
    /// <param name="dataFrame">The data frame to attach.</param>
    /// <returns>The model and Normal parameter vector.</returns>
    private static (IModel Model, double[] Parameters) CreateUnivariateModel(DataFrame dataFrame)
    {
        var model = new UnivariateDistribution { UseDefaultFlatPriors = false };
        model.Distribution = new Normal(100.0, 10.0);
        model.DataFrame = dataFrame;
        return (model, [100.0, 10.0]);
    }

    /// <summary>
    /// Creates a one-component Normal mixture model.
    /// </summary>
    /// <param name="dataFrame">The data frame to attach.</param>
    /// <returns>The model and component parameter vector.</returns>
    private static (IModel Model, double[] Parameters) CreateMixtureModel(DataFrame dataFrame)
    {
        var model = new MixtureModel { UseDefaultFlatPriors = false };
        model.Mixture = new Mixture([1.0], [new Normal(100.0, 10.0)]);
        model.DataFrame = dataFrame;
        return (model, [100.0, 10.0]);
    }

    /// <summary>
    /// Creates a one-component Normal competing-risks model.
    /// </summary>
    /// <param name="dataFrame">The data frame to attach.</param>
    /// <returns>The model and component parameter vector.</returns>
    private static (IModel Model, double[] Parameters) CreateCompetingRisksModel(DataFrame dataFrame)
    {
        var model = new CompetingRisksModel { UseDefaultFlatPriors = false };
        model.CompetingRisks = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(100.0, 10.0)
        });
        model.DataFrame = dataFrame;
        return (model, [100.0, 10.0]);
    }

    /// <summary>
    /// Creates a one-component GEV point-process model.
    /// </summary>
    /// <param name="dataFrame">The data frame to attach.</param>
    /// <returns>The model and GEV parameter vector.</returns>
    private static (IModel Model, double[] Parameters) CreatePointProcessModel(DataFrame dataFrame)
    {
        var model = new PointProcessModel
        {
            UseDefaults = false,
            UseDefaultFlatPriors = false
        };
        model.Distribution = new CompetingRisks(new IUnivariateDistribution[]
        {
            new GeneralizedExtremeValue(100.0, 20.0, 0.0)
        });
        model.DataFrame = dataFrame;
        model.Threshold = 80.0;
        model.TotalYears = 3.0;
        return (model, [100.0, 20.0, 0.0]);
    }

    /// <summary>
    /// Sets threshold counts after model construction.
    /// </summary>
    /// <param name="threshold">The threshold record to mutate.</param>
    /// <param name="numberBelow">The below-threshold count.</param>
    /// <param name="numberAbove">The above-threshold count.</param>
    /// <exception cref="InvalidOperationException">
    /// Thrown if the expected backing fields are unavailable.
    /// </exception>
    /// <remarks>
    /// <see cref="ThresholdData.NumberBelow"/> has an internal setter and model data-frame
    /// subscriptions reprocess threshold counts when the data frame changes. Reflection keeps
    /// these tests focused on the likelihood edge case without changing production visibility.
    /// </remarks>
    private static void SetThresholdCounts(ThresholdData threshold, int numberBelow, int numberAbove)
    {
        const BindingFlags flags = BindingFlags.Instance | BindingFlags.NonPublic;
        var belowField = typeof(ThresholdData).GetField("_numberBelow", flags);
        var aboveField = typeof(ThresholdData).GetField("_numberAbove", flags);

        if (belowField is null || aboveField is null)
            throw new InvalidOperationException("Threshold count backing fields were not found.");

        belowField.SetValue(threshold, numberBelow);
        aboveField.SetValue(threshold, numberAbove);
    }

    /// <summary>
    /// Asserts that a value is finite.
    /// </summary>
    /// <param name="value">The value to inspect.</param>
    /// <param name="message">The assertion message.</param>
    /// <exception cref="AssertFailedException">Thrown when the value is NaN or infinite.</exception>
    private static void AssertFinite(double value, string message)
    {
        Assert.IsFalse(double.IsNaN(value), message);
        Assert.IsFalse(double.IsInfinity(value), message);
    }
}
