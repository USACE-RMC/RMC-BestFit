using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Fast regression tests for the full-K mixture contract and positive-hurdle likelihood.
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
    /// Verifies a K-component model retains all K named weight parameters publicly.
    /// </summary>
    [TestMethod]
    public void SetDefaultParameters_RetainsAllPhysicalWeights()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(1.0, 2.0, 3.0, 4.0, 5.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });

        Assert.AreEqual(9, model.NumberOfParameters);
        CollectionAssert.AreEqual(
            new[] { "Weight (w₁)", "Weight (w₂)", "Weight (w₃)" },
            model.Parameters.Take(3).Select(parameter => parameter.Name).ToArray());
        Assert.IsFalse(model.Parameters.Skip(3).Any(parameter => parameter.Name.Contains("Weight", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies all physical weights are accepted and public proposal arrays are never modified.
    /// </summary>
    [TestMethod]
    public void LikelihoodAndSetParameterValues_UseFullWeightsWithoutMutatingProposal()
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
        double[] proposal = { 0.15, 0.35, 0.0, 1.0, 3.0, 1.0 };
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
    /// Verifies every full-K parameter prior contributes to the scalar and pointwise totals.
    /// </summary>
    [TestMethod]
    public void PriorLogLikelihood_IncludesEveryFullKParameterPrior()
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
            .Sum();

        double actual = model.PriorLogLikelihood(parameters);
        Assert.AreEqual(expected, actual, 1E-12);
        Assert.AreEqual(
            actual,
            model.PointwisePriorLogLikelihood(parameters).Sum(component => component.LogLikelihood),
            1E-12);
    }

    /// <summary>
    /// Verifies the K-1 sampler target derives the final weight and applies its configured prior factor.
    /// </summary>
    [TestMethod]
    public void SamplingLogLikelihood_DerivesFinalWeightAndIncludesItsPrior()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(-0.5, 0.0, 0.5, 2.5, 3.0, 3.5),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            })
        {
            UseJeffreysRuleForScale = false,
            EnableQuantilePriors = false
        };
        double[] fullParameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        fullParameters[0] = 0.35;
        fullParameters[1] = 0.65;
        double[] sampledParameters = fullParameters.Where((_, index) => index != 1).ToArray();

        double baseline = model.SamplingLogLikelihood(sampledParameters);
        Assert.AreEqual(model.LogLikelihood(fullParameters), baseline, 1E-12);

        double oldDerivedPrior = model.Parameters[1].PriorDistribution.LogPDF(0.65);
        model.Parameters[1].PriorDistribution = new Normal(0.8, 0.2);
        double newDerivedPrior = model.Parameters[1].PriorDistribution.LogPDF(0.65);
        double changed = model.SamplingLogLikelihood(sampledParameters);

        Assert.AreEqual(newDerivedPrior - oldDerivedPrior, changed - baseline, 1E-10);
        Assert.AreNotEqual(baseline, changed);
    }

    /// <summary>
    /// Verifies proposals beyond the physical simplex are rejected rather than clamped or normalized.
    /// </summary>
    [TestMethod]
    public void SamplingLogLikelihood_WithNegativeResidualWeight_ReturnsNegativeInfinity()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 1.0, 2.0, 3.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });
        double[] sampledParameters = model.Parameters.Select(parameter => parameter.Value)
            .Where((_, index) => index != 1)
            .ToArray();
        sampledParameters[0] = 1.01;

        Assert.AreEqual(double.NegativeInfinity, model.SamplingLogLikelihood(sampledParameters));
    }

    /// <summary>
    /// Verifies zero-inflated K-1 results derive the final continuous weight from one minus the fixed atom.
    /// </summary>
    [TestMethod]
    public void GetPhysicalParameters_ZeroInflatedThreeComponentResult_UsesContinuousMass()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 1.0, 2.0, 3.0, 4.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            },
            isZeroInflated: true);
        double[] sampledParameters = model.Parameters.Select(parameter => parameter.Value)
            .Where((_, index) => index != 2)
            .ToArray();
        sampledParameters[0] = 0.2;
        sampledParameters[1] = 0.3;

        double[] physical = model.GetPhysicalParameters(sampledParameters);

        Assert.AreEqual(0.2, physical[0], 0.0);
        Assert.AreEqual(0.3, physical[1], 0.0);
        Assert.AreEqual(0.3, physical[2], 1E-15);
        Assert.AreEqual(1.0 - model.Mixture!.ZeroWeight, physical.Take(3).Sum(), 1E-15);
        CollectionAssert.AreEqual(sampledParameters.Skip(2).ToArray(), physical.Skip(3).ToArray());
    }

    /// <summary>
    /// Verifies EM returns the original full-K public vector and covariance dimensions.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ReturnsFullKVectorAndCovariance()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(0.0, 0.2, 0.5, 2.5, 2.8, 3.0),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out _);

        Assert.AreEqual(6, parameters.Length);
        Assert.AreEqual(6, covariance.GetLength(0));
        Assert.AreEqual(6, covariance.GetLength(1));
        Assert.AreEqual(1.0, parameters[0] + parameters[1], 1E-12);
        Assert.IsTrue(parameters[0] >= 0.0 && parameters[1] >= 0.0);

        double expectedVariance = parameters[0] * parameters[1] / model.DataFrame.TotalRecordLength();
        Assert.AreEqual(expectedVariance, covariance[0, 0], 1E-10);
        Assert.AreEqual(expectedVariance, covariance[1, 1], 1E-10);
        Assert.AreEqual(-expectedVariance, covariance[0, 1], 1E-10);
        Assert.AreEqual(-expectedVariance, covariance[1, 0], 1E-10);
        Assert.AreEqual(0.0, covariance[0, 0] + covariance[0, 1], 1E-12);
        Assert.AreEqual(0.0, covariance[1, 0] + covariance[1, 1], 1E-12);
    }

    /// <summary>
    /// Verifies the full-K EM covariance is the singular multinomial block induced by the identified coordinates.
    /// </summary>
    [TestMethod]
    public void ExpectationMaximization_ThreeWeightsHaveNegativeCovarianceAndZeroRowSums()
    {
        var model = new MixtureModel(
            CreateExactDataFrame(-5.2, -5.0, -4.8, -0.2, 0.0, 0.2, 4.8, 5.0, 5.2),
            new List<UnivariateDistributionType>
            {
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal,
                UnivariateDistributionType.Normal
            });

        model.ExpectationMaximization(out double[] parameters, out double[,] covariance, out _);

        for (int row = 0; row < 3; row++)
        {
            Assert.IsTrue(covariance[row, row] > 0.0);
            double rowSum = 0.0;
            for (int column = 0; column < 3; column++)
            {
                Assert.AreEqual(covariance[row, column], covariance[column, row], 1E-12);
                if (row != column) Assert.IsTrue(covariance[row, column] < 0.0);
                rowSum += covariance[row, column];
            }
            Assert.AreEqual(0.0, rowSum, 1E-12);
        }

        double leadingDeterminant = covariance[0, 0] * covariance[1, 1] -
            covariance[0, 1] * covariance[1, 0];
        Assert.IsTrue(leadingDeterminant > 0.0, "The identified K-1 principal block must be positive definite.");
        Assert.AreEqual(1.0, parameters.Take(3).Sum(), 1E-12);
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
