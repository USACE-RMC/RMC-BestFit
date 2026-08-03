using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Fast programmatic tests for competing-risk dependency validation and simulation.
/// </summary>
[TestClass]
public class CompetingRisksDependencyTests
{
    /// <summary>
    /// Creates a small valid data frame for simulation-model validation.
    /// </summary>
    /// <returns>A data frame containing exact positive observations.</returns>
    private static BestFitDataFrame CreateDataFrame()
    {
        var dataFrame = new BestFitDataFrame();
        for (int index = 0; index < 10; index++)
            dataFrame.ExactSeries.Add(new ExactData(2000 + index, 100d + 5d * index));
        return dataFrame;
    }

    /// <summary>
    /// Creates a two-Normal competing-risk model with the specified dependency.
    /// </summary>
    /// <param name="dependency">The dependency mode.</param>
    /// <returns>A configured competing-risk model.</returns>
    private static CompetingRisksModel CreateModel(Probability.DependencyType dependency)
    {
        var distribution = new CompetingRisks(new UnivariateDistributionBase[]
        {
            new Normal(100d, 10d),
            new Normal(120d, 15d)
        })
        {
            Dependency = dependency,
            MinimumOfRandomVariables = false,
            CorrelationMatrix = new[,] { { 1d, 0.6d }, { 0.6d, 1d } }
        };
        return new CompetingRisksModel(CreateDataFrame(), distribution);
    }

    /// <summary>
    /// Verifies the BestFit simulation path is deterministic and agrees with the explicit
    /// dependency-aware Numerics entry point for every supported dependency mode.
    /// </summary>
    /// <param name="dependency">The dependency mode to exercise.</param>
    [TestMethod]
    [DataRow(Probability.DependencyType.Independent)]
    [DataRow(Probability.DependencyType.PerfectlyPositive)]
    [DataRow(Probability.DependencyType.PerfectlyNegative)]
    [DataRow(Probability.DependencyType.CorrelationMatrix)]
    public void GenerateRandomValues_AllDependencyModes_AreDeterministicAndDependencyAware(
        Probability.DependencyType dependency)
    {
        CompetingRisksModel model = CreateModel(dependency);

        double[] expected = model.CompetingRisks!.GenerateRandomValuesWithDependency(128, 24680);
        double[] first = model.GenerateRandomValues(128, 24680);
        double[] second = model.GenerateRandomValues(128, 24680);

        CollectionAssert.AreEqual(expected, first);
        CollectionAssert.AreEqual(first, second);
    }

    /// <summary>
    /// Verifies model validation reports a missing or dimensionally invalid custom matrix.
    /// </summary>
    [TestMethod]
    public void Validate_CorrelationMatrixDependency_ReportsInvalidConfiguration()
    {
        CompetingRisksModel model = CreateModel(Probability.DependencyType.CorrelationMatrix);
        model.CompetingRisks!.CorrelationMatrix = null!;
        var missing = model.Validate();
        model.CompetingRisks.CorrelationMatrix = new[,] { { 1d } };
        var mismatched = model.Validate();

        Assert.IsFalse(missing.IsValid);
        Assert.IsTrue(missing.ValidationMessages.Any(message => message.Contains("requires a correlation matrix", StringComparison.Ordinal)));
        Assert.IsFalse(mismatched.IsValid);
        Assert.IsTrue(mismatched.ValidationMessages.Any(message => message.Contains("number of components", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies simulation rejects an invalid matrix through the BestFit preflight path.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_InvalidCorrelationMatrix_ThrowsBeforeSimulation()
    {
        CompetingRisksModel model = CreateModel(Probability.DependencyType.CorrelationMatrix);
        model.CompetingRisks!.CorrelationMatrix = new[,] { { 1d, 1d }, { 1d, 1d } };

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.GenerateRandomValues(10, 12345));

        StringAssert.Contains(exception.Message, "positive definite");
    }
}
