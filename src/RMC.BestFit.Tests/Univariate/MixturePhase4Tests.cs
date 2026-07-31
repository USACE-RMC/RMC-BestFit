using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using Numerics.Mathematics.SpecialFunctions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Fast regression tests for the corrected K-1 mixture parameterization and positive-hurdle likelihood.
/// </summary>
[TestClass]
public class MixturePhase4Tests
{
    /// <summary>
    /// Creates an exact-data frame from supplied values.
    /// </summary>
    /// <param name="values">The exact annual values.</param>
    /// <returns>A data frame containing one exact record per value.</returns>
    private static BestFitDataFrame CreateExactDataFrame(params double[] values)
    {
        var data = values
            .Select((value, index) => new ExactData { Index = 1980 + index, Value = value })
            .ToList();
        return new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
    }

    /// <summary>
    /// Creates a one-component positive-hurdle Normal model with a stable positive exact sample.
    /// </summary>
    /// <param name="configure">An optional action that adds a mixed observation before model construction.</param>
    /// <returns>The configured mixture model.</returns>
    private static MixtureModel CreateMixedHurdleModel(Action<BestFitDataFrame>? configure = null)
    {
        BestFitDataFrame dataFrame = CreateExactDataFrame(1.0, 2.0, 3.0, 4.0);
        configure?.Invoke(dataFrame);
        return new MixtureModel(
            dataFrame,
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
    }

    /// <summary>
    /// Verifies a K-component model exposes exactly K-1 named weight parameters.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_UsesKMinusOnePhysicalWeights()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(1.0, 2.0, 3.0, 4.0, 5.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });

        Assert.AreEqual(8, model.NumberOfParameters);
        CollectionAssert.AreEqual(
            new[] { "Weight (w₁)", "Weight (w₂)" },
            model.Parameters.Take(2).Select(parameter => parameter.Name).ToArray());
        Assert.IsFalse(model.Parameters.Skip(2).Any(parameter => parameter.Name.Contains("Weight", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies the final physical weight is derived and public proposal arrays are never modified.
    /// </summary>
    [TestMethod]
    public void LikelihoodAndSetParameterValues_DeriveFinalWeightWithoutMutatingProposal()
    {
        BestFitDataFrame dataFrame = CreateExactDataFrame(0.5, 1.0, 2.5, 3.0);
        var physical = new Mixture(
            new[] { 0.3, 0.7 },
            new UnivariateDistributionBase[] { new Normal(0.0, 1.0), new Normal(3.0, 1.0) });
        var model = new MixtureModel(dataFrame, physical)
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        double[] proposal = { 0.3, 0.0, 1.0, 3.0, 1.0 };
        double[] snapshot = proposal.ToArray();

        double actual = model.DataLogLikelihood(proposal);
        double expected = dataFrame.ExactSeries.Sum(data => physical.LogPDF(data.Value));
        Assert.AreEqual(expected, actual, 1E-12);
        CollectionAssert.AreEqual(snapshot, proposal);

        _ = model.PriorLogLikelihood(proposal);
        _ = model.PointwiseDataLogLikelihood(proposal);
        _ = model.PointwiseDataLogLikelihoodComponents(proposal);
        CollectionAssert.AreEqual(snapshot, proposal);

        model.SetParameterValues(proposal);
        CollectionAssert.AreEqual(snapshot, proposal);
        Assert.AreEqual(0.3, model.Mixture!.Weights[0], 1E-15);
        Assert.AreEqual(0.7, model.Mixture.Weights[1], 1E-15);
    }

    /// <summary>
    /// Verifies the flat simplex prior includes its normalization constant.
    /// </summary>
    [TestMethod]
    public void PriorLogLikelihood_IncludesNormalizedFlatSimplexDensity()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 1.0, 2.0, 3.0, 4.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            },
            isZeroInflated: true)
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        double expected = model.Parameters
            .Select((parameter, index) => parameter.PriorDistribution.LogPDF(parameters[index]))
            .Sum() + Gamma.LogGamma(3.0);

        double actual = model.PriorLogLikelihood(parameters);
        Assert.AreEqual(expected, actual, 1E-12);
        Assert.AreEqual(
            actual,
            model.PointwisePriorLogLikelihood(parameters).Sum(component => component.LogLikelihood),
            1E-12);
    }

    /// <summary>
    /// Verifies EM returns the K-1 vector and covariance dimensions.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ReturnsKMinusOneVectorAndCovariance()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 0.2, 0.5, 2.5, 2.8, 3.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out _);

        Assert.AreEqual(5, parameters.Length);
        Assert.AreEqual(5, covariance.GetLength(0));
        Assert.AreEqual(5, covariance.GetLength(1));
        Assert.IsTrue(parameters[0] >= 0.0 && 1.0 - parameters[0] >= 0.0);
    }

    /// <summary>
    /// Verifies uncertain, interval, and threshold records do not determine the fixed zero atom.
    /// </summary>
    [TestMethod]
    public void ZeroWeight_UsesExactAnnualZerosOnly()
    {
        BestFitDataFrame dataFrame = CreateExactDataFrame(0.0, 1.0, 2.0, 3.0);
        dataFrame.UncertainSeries.Add(new UncertainData(2000, new Normal(0.0, 1.0)));
        dataFrame.IntervalSeries.Add(new IntervalData(2001, -1.0, 0.0, 1.0));
        dataFrame.ThresholdSeries.Add(new ThresholdData(1900, 1901, 0.0) { NumberBelow = 4 });

        var model = new MixtureModel(
            dataFrame,
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);

        Assert.AreEqual(0.25, model.Mixture!.ZeroWeight, 0.0);
        Assert.AreEqual(0.75, model.Mixture.Weights[0], 1E-15);
    }

    /// <summary>
    /// Verifies mixed observation likelihoods include the atom and positive-conditioned continuous law.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_MixedObservations_UsesFixedAtomAndPositiveConditioning()
    {
        BestFitDataFrame dataFrame = CreateExactDataFrame(0.0, 2.0);
        var measurementError = new Normal(0.0, 0.5);
        dataFrame.UncertainSeries.Add(new UncertainData(2000, measurementError));
        dataFrame.IntervalSeries.Add(new IntervalData(2001, -1.0, 0.0, 1.0));
        dataFrame.ThresholdSeries.Add(new ThresholdData(1900, 1901, 0.0) { NumberBelow = 1 });
        var model = new MixtureModel(
            dataFrame,
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
        double[] parameters = { 0.0, 1.0 };
        var physical = new Mixture(
            new[] { 0.5 },
            new UnivariateDistributionBase[] { new Normal(0.0, 1.0) })
        {
            IsZeroInflated = true,
            ZeroWeight = 0.5
        };

        const double lowerProbability = 1E-8;
        const double upperProbability = 1.0 - 1E-8;
        double lower = measurementError.InverseCDF(lowerProbability);
        double upper = measurementError.InverseCDF(upperProbability);
        double uncertainProbability =
            (0.5 * measurementError.PDF(0.0) +
             Integration.GaussLegendre20(
                 value => measurementError.PDF(value) * physical.PDF(value),
                 Math.Max(0.0, lower),
                 upper)) /
            (upperProbability - lowerProbability);
        double expected =
            physical.LogPDF(0.0) +
            physical.LogPDF(2.0) +
            Math.Log(uncertainProbability) +
            physical.LogLikelihood_Intervals(-1.0, 1.0) +
            physical.LogLikelihood_LeftCensored(
                0.0,
                ((ThresholdData)dataFrame.ThresholdSeries[0]).NumberBelow);

        double actual = model.DataLogLikelihood(parameters);
        Assert.AreEqual(expected, actual, 1E-12);
        Assert.AreEqual(actual, model.PointwiseDataLogLikelihood(parameters).Sum(), 1E-12);
    }

    /// <summary>
    /// Verifies negative exact data are rejected in a zero-inflated model.
    /// </summary>
    [TestMethod]
    public void ZeroInflatedModel_RejectsNegativeExactObservation()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(-1.0, 0.0, 1.0, 2.0),
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
        double[] parameters = { 1.5, 0.5 };

        var validation = model.Validate();
        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("negative exact", StringComparison.OrdinalIgnoreCase)));
        Assert.AreEqual(double.NegativeInfinity, model.DataLogLikelihood(parameters));
        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.ExpectationMaximization(out _, out _, out _));
        StringAssert.Contains(exception.Message, "negative exact value");
    }

    /// <summary>
    /// Verifies a hurdle component with no numerically positive mass is rejected.
    /// </summary>
    [TestMethod]
    public void ZeroInflatedModel_RejectsComponentWithoutPositiveMass()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 1.0, 2.0),
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
        model.Mixture!.SetParameters(
            new[] { 2.0 / 3.0 },
            new[] { -1000.0, 1.0 });

        var validation = model.Validate();
        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.Contains("positive probability above zero", StringComparison.OrdinalIgnoreCase)));
    }

    /// <summary>
    /// Verifies an impossible exact row throws with row context.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ImpossibleExactRow_ThrowsWithContext()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 1.0, 2.0),
            new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
        model.Mixture!.ZeroWeight = 0.0;

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.ExpectationMaximization(out _, out _, out _));
        StringAssert.Contains(exception.Message, "row 0");
        StringAssert.Contains(exception.Message, "zero or nonfinite");
    }

    /// <summary>
    /// Verifies an impossible uncertain row throws with row context.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ImpossibleUncertainRow_ThrowsWithContext()
    {
        MixtureModel model = CreateMixedHurdleModel(
            dataFrame => dataFrame.UncertainSeries.Add(
                new UncertainData(2000, new Uniform(-2.0, -1.0))));

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.ExpectationMaximization(out _, out _, out _));
        StringAssert.Contains(exception.Message, "zero or nonfinite");
    }

    /// <summary>
    /// Verifies an impossible interval row throws with row context.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ImpossibleIntervalRow_ThrowsWithContext()
    {
        MixtureModel model = CreateMixedHurdleModel(
            dataFrame => dataFrame.IntervalSeries.Add(
                new IntervalData(2000, -2.0, -1.5, -1.0)));

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.ExpectationMaximization(out _, out _, out _));
        StringAssert.Contains(exception.Message, "zero or nonfinite");
    }

    /// <summary>
    /// Verifies an impossible threshold row throws with row context.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ImpossibleThresholdRow_ThrowsWithContext()
    {
        MixtureModel model = CreateMixedHurdleModel(
            dataFrame => dataFrame.ThresholdSeries.Add(
                new ThresholdData(1900, 1901, -1.0) { NumberBelow = 1 }));

        InvalidOperationException exception = Assert.ThrowsException<InvalidOperationException>(
            () => model.ExpectationMaximization(out _, out _, out _));
        StringAssert.Contains(exception.Message, "zero or nonfinite");
    }
}
