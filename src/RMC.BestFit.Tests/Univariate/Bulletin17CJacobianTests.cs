using Numerics.Distributions;
using Numerics.Functions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>Independent algebraic and finite-difference checks of systematic B17C moment derivatives.</summary>
[TestClass]
public class Bulletin17CJacobianTests
{
    /// <summary>Checks all nine natural derivatives against hand-calculated moments and Bessel factors.</summary>
    /// <param name="type">The Pearson family in raw or logarithmic data space.</param>
    [TestMethod]
    [DataRow(UnivariateDistributionType.PearsonTypeIII)]
    [DataRow(UnivariateDistributionType.LogPearsonTypeIII)]
    public void SystematicJacobian_MatchesAnalyticalValues(UnivariateDistributionType type)
    {
        var model = CreateModel(type);
        Assert.IsNotNull(model.JacobianFunction);
        // Data in moment space: 1, 2, 3, 4, 5. At mu=2: E[d]=1, E[d^2]=3.
        var actual = model.JacobianFunction(new[] { 2d, 0.5, 0.2 });
        double[,] expected = { { -1, 0, 0 }, { -2.5, -1, 0 }, { -18.75, -0.15, -0.125 } };
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                Assert.AreEqual(expected[i, j], actual[i, j], 1e-12, $"Derivative [{i},{j}].");
    }

    /// <summary>Independently differentiates the actual moment function with a five-point stencil in link space.</summary>
    /// <param name="type">The Pearson family.</param>
    [TestMethod]
    [DataRow(UnivariateDistributionType.PearsonTypeIII)]
    [DataRow(UnivariateDistributionType.LogPearsonTypeIII)]
    public void LinkedJacobian_MatchesIndependentFiniteDifferences(UnivariateDistributionType type)
    {
        var model = CreateModel(type);
        model.LinkController = new LinkController(new ASinHLink(1, 2, 0.25), new LogLink(), new ASinHLink(0, 0.5, -0.25));
        double[] eta = model.LinkController.Link(new[] { 2d, 0.5, 0.2 });
        Assert.IsNotNull(model.JacobianFunction);
        var actual = model.JacobianFunction(eta);
        const double h = 1e-4;
        for (int j = 0; j < 3; j++)
        {
            double[][] values = new double[4][];
            int[] offsets = [-2, -1, 1, 2];
            for (int k = 0; k < 4; k++)
            {
                var trial = (double[])eta.Clone();
                trial[j] += offsets[k] * h;
                values[k] = model.MomentConditions(trial).G.Array;
            }
            for (int i = 0; i < 3; i++)
            {
                double expected = (values[0][i] - 8 * values[1][i] + 8 * values[2][i] - values[3][i]) / (12 * h);
                Assert.AreEqual(expected, actual[i, j], 2e-8, $"Linked derivative [{i},{j}].");
            }
        }
    }

    /// <summary>Small samples retain the existing Bessel-factor conventions.</summary>
    /// <param name="n">The number of exact observations.</param>
    /// <param name="second">The expected mean derivative of the second moment condition.</param>
    /// <param name="third">The expected mean derivative of the third moment condition.</param>
    [TestMethod]
    [DataRow(1, 2d, -3d)]
    [DataRow(2, 2d, -1.5)]
    public void SmallSampleJacobian_PreservesBesselCorrections(int n, double second, double third)
    {
        var model = CreateModel(UnivariateDistributionType.PearsonTypeIII);
        while (model.DataFrame.ExactSeries.Count > n) model.DataFrame.ExactSeries.RemoveAt(model.DataFrame.ExactSeries.Count - 1);
        Assert.IsNotNull(model.JacobianFunction);
        var actual = model.JacobianFunction(new[] { 2d, 0.5, 0.2 });
        Assert.AreEqual(second, actual[1, 0], 1e-12);
        Assert.AreEqual(third, actual[2, 0], 1e-12);
    }

    /// <summary>Other distribution families continue to request numerical differentiation.</summary>
    /// <param name="type">The distribution using the existing numerical path.</param>
    [TestMethod]
    [DataRow(UnivariateDistributionType.Normal)]
    [DataRow(UnivariateDistributionType.LogNormal)]
    [DataRow(UnivariateDistributionType.GammaDistribution)]
    [DataRow(UnivariateDistributionType.Exponential)]
    public void OtherFamilies_KeepNumericalFallback(UnivariateDistributionType type)
    {
        Assert.IsNull(CreateModel(type).JacobianFunction);
    }

    /// <summary>Censoring removes the systematic-only derivative even after a delegate was previously available.</summary>
    [TestMethod]
    public void LowOutliers_KeepNumericalFallback()
    {
        var model = CreateModel(UnivariateDistributionType.PearsonTypeIII);
        Assert.IsNotNull(model.JacobianFunction);
        for (int i = 6; i <= 10; i++) model.DataFrame.ExactSeries.Add(new ExactData(2000 + i, i));
        model.DataFrame.LowOutlierThreshold = 1.5;
        model.DataFrame.SetLowOutliersFromThreshold();
        Assert.IsNull(model.JacobianFunction);
    }

    /// <summary>Any non-systematic data category preserves the existing numerical derivative path.</summary>
    /// <param name="category">The category added to the systematic fixture.</param>
    [TestMethod]
    [DataRow("interval")]
    [DataRow("threshold")]
    [DataRow("uncertain")]
    public void MixedData_KeepNumericalFallback(string category)
    {
        var model = CreateModel(UnivariateDistributionType.PearsonTypeIII);
        if (category == "interval") model.DataFrame.IntervalSeries.Add(new IntervalData(2010, 1, 2, 3));
        if (category == "threshold") model.DataFrame.ThresholdSeries.Add(new ThresholdData(1980, 1985, 1));
        if (category == "uncertain") model.DataFrame.UncertainSeries.Add(new UncertainData(2010, new Normal(2, 0.1)));
        Assert.IsNull(model.JacobianFunction);
    }

    /// <summary>Creates five inline systematic observations without executing an estimator.</summary>
    /// <param name="type">The model distribution.</param>
    /// <returns>The configured model.</returns>
    private static Bulletin17CDistribution CreateModel(UnivariateDistributionType type)
    {
        var frame = new RMC.BestFit.Models.DataFrame();
        for (int i = 1; i <= 5; i++)
            frame.ExactSeries.Add(new ExactData(2000 + i, type == UnivariateDistributionType.LogPearsonTypeIII ? Math.Pow(10, i) : i));
        return new Bulletin17CDistribution(frame, type);
    }
}
