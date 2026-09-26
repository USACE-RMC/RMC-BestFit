using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;

namespace RMC.BestFit.Tests.Analyses;

/// <summary>
/// Fast programmatic tests for the shared RMSE-or-NaN guard used by every analysis that
/// reports RMSE alongside AIC/BIC.
/// </summary>
/// <remarks>
/// <see cref="GoodnessOfFit"/>'s distribution-based RMSE overload throws when the observation
/// count does not exceed the distribution's parameter count. These tests pin the guard's
/// boundary behavior (NaN at the boundary, finite just past it) and confirm it never disagrees
/// with a direct <see cref="GoodnessOfFit"/> call once the residual degrees of freedom are
/// positive.
/// </remarks>
[TestClass]
public class GoodnessOfFitGuardsTests
{
    /// <summary>
    /// Verifies the guard returns <see cref="double.NaN"/> when the observation count equals
    /// the distribution's parameter count (zero residual degrees of freedom) instead of
    /// throwing, which is what a direct <see cref="GoodnessOfFit"/> RMSE call would do for the
    /// same inputs.
    /// </summary>
    [TestMethod]
    public void RmseOrNaN_ObservationCountEqualsParameterCount_ReturnsNaN()
    {
        // Normal has 2 parameters (mean, standard deviation), so n = k = 2.
        var distribution = new Normal(0d, 1d);
        Assert.AreEqual(2, distribution.NumberOfParameters);

        double[] values = [10d, 20d];
        double[] plottingPositions = [0.25d, 0.75d];

        var result = GoodnessOfFitGuards.RmseOrNaN(values, plottingPositions, distribution);

        Assert.IsTrue(double.IsNaN(result));
    }

    /// <summary>
    /// Verifies the guard returns a finite RMSE as soon as the observation count exceeds the
    /// parameter count by one (n = k + 1, one residual degree of freedom).
    /// </summary>
    [TestMethod]
    public void RmseOrNaN_ObservationCountOneMoreThanParameterCount_ReturnsFiniteValue()
    {
        var distribution = new Normal(0d, 1d);

        double[] values = [-1d, 0d, 1d];
        double[] plottingPositions = [0.25d, 0.5d, 0.75d];

        var result = GoodnessOfFitGuards.RmseOrNaN(values, plottingPositions, distribution);

        Assert.IsFalse(double.IsNaN(result));
        Assert.IsTrue(double.IsFinite(result));
    }

    /// <summary>
    /// Verifies that once the residual degrees of freedom are positive, the guard's result
    /// matches a direct <see cref="GoodnessOfFit"/> RMSE call with the same inputs exactly --
    /// the guard only intercepts the otherwise-throwing boundary case, it does not change the
    /// computed value.
    /// </summary>
    [TestMethod]
    public void RmseOrNaN_ObservationCountExceedsParameterCount_MatchesDirectGoodnessOfFitRmse()
    {
        var distribution = new Normal(0d, 1d);

        double[] values = [-2d, -1d, 0d, 1d, 2d];
        double[] plottingPositions = [0.1d, 0.3d, 0.5d, 0.7d, 0.9d];

        var expected = GoodnessOfFit.RMSE(values, plottingPositions, distribution);
        var actual = GoodnessOfFitGuards.RmseOrNaN(values, plottingPositions, distribution);

        Assert.AreEqual(expected, actual, 1e-12);
    }
}
