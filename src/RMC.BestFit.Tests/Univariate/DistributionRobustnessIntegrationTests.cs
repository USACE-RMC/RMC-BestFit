using System.Reflection;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Mathematics.Integration;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Verifies stable distribution arithmetic at BestFit prior and EM observation boundaries without fitting.
/// </summary>
[TestClass]
public class DistributionRobustnessIntegrationTests
{
    /// <summary>
    /// Verifies scalar and pointwise priors retain finite logarithms when the raw determinant is unrepresentable.
    /// </summary>
    /// <param name="location">The natural-log location producing overflow or underflow.</param>
    [TestMethod]
    [DataRow(400.0)]
    [DataRow(-400.0)]
    public void QuantilePriors_LogNormal_ConsumeFiniteLogJacobian(double location)
    {
        var distribution = new LogNormal(location, 1.0) { Base = Math.E };
        var model = new UnivariateDistribution(CreateDataFrame(), distribution);
        double[] probabilities = { 0.25, 0.75 };
        ConfigureProcessedPriors(model, distribution, probabilities);

        // det = Q(.25) Q(.75) [z(.75)-z(.25)]; R stats qnorm supplies the constant.
        double expectedJacobian = 2.0 * location + 0.29934838095905414;
        AssertPriorContributions(model, distribution.GetParameters, expectedJacobian);
    }

    /// <summary>
    /// Verifies the point-process scalar and pointwise paths consume a three-parameter GEV Jacobian.
    /// </summary>
    /// <param name="scale">The GEV scale, including a representable scale with an overflowing determinant.</param>
    /// <param name="shape">The GEV shape, including its zero limit.</param>
    /// <param name="expectedLogDeterminant">The independent defining-formula log determinant.</param>
    [TestMethod]
    [DataRow(1E308, 0.0, 710.20562535203)]
    [DataRow(3.0, 0.2, 2.1145580687246843)]
    public void QuantilePriors_PointProcess_ConsumeThreeParameterLogJacobian(
        double scale, double shape, double expectedLogDeterminant)
    {
        var component = new GeneralizedExtremeValue(0.0, scale, shape);
        var model = new PointProcessModel(
            CreateDataFrame(),
            new CompetingRisks(new UnivariateDistributionBase[] { new GeneralizedExtremeValue(0.0, 1.0, 0.2) })
            {
                MinimumOfRandomVariables = false
            });
        double[] probabilities =
        {
            Math.Exp(-Math.Exp(1.4)), Math.Exp(-1.0), Math.Exp(-Math.Exp(-1.4))
        };
        ConfigureProcessedPriors(model, component, probabilities);

        // At shape zero and y = {-1.4,0,1.4}, |det| = scale * 1.4^3.
        AssertPriorContributions(model, component.GetParameters, expectedLogDeterminant);
    }

    /// <summary>
    /// Verifies an insufficient block sample retains editable priors and that a later valid sample clears the diagnostic.
    /// </summary>
    /// <param name="isSeasonal">Whether the model includes its two structural seasonal changepoints.</param>
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void PointProcessInitialization_InvalidRefresh_PreservesParametersAndRecovers(bool isSeasonal)
    {
        var validFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new(new DateTime(2000, 1, 15), 1.0), new(new DateTime(2001, 1, 15), 2.0),
                new(new DateTime(2002, 1, 15), 3.0), new(new DateTime(2003, 1, 15), 4.0)
            })
        };
        var model = new PointProcessModel
        {
            IsSeasonal = isSeasonal,
            TimeBlock = TimeBlockWindow.CalendarYear,
            StartMonth = 1,
            DataFrame = validFrame
        };
        ModelParameter[] originalParameters = model.Parameters.ToArray();
        string[] originalParameterStates = originalParameters.Select(parameter => parameter.ToXElement().ToString()).ToArray();
        Assert.AreEqual(isSeasonal ? 8 : 3, originalParameters.Length);

        model.DataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData> { new(new DateTime(2004, 1, 15), 2.0) })
        };

        var invalid = model.Validate();
        Assert.IsFalse(invalid.IsValid);
        Assert.IsTrue(invalid.ValidationMessages.Any(message => message.Contains("Insufficient observations", StringComparison.Ordinal)));
        Assert.AreEqual(originalParameters.Length, model.Parameters.Count);
        for (int i = 0; i < originalParameters.Length; i++)
        {
            Assert.AreSame(originalParameters[i], model.Parameters[i]);
            Assert.AreEqual(originalParameterStates[i], model.Parameters[i].ToXElement().ToString());
        }
        Assert.AreEqual(1, model.AMSDataFrame.ExactSeries.Count);
        Assert.AreEqual(isSeasonal ? 1 : 0, model.POTDays.Count);
        Assert.AreEqual(TimeBlockWindow.CalendarYear, model.TimeBlock);

        model.DataFrame = validFrame;

        var recovered = model.Validate();
        Assert.IsTrue(recovered.IsValid, string.Join("; ", recovered.ValidationMessages));
        Assert.AreEqual(isSeasonal ? 8 : 3, model.Parameters.Count);
        Assert.AreEqual(4, model.AMSDataFrame.ExactSeries.Count);
    }

    /// <summary>
    /// Verifies exact densities remain logarithmic through component responsibility normalization.
    /// </summary>
    [TestMethod]
    public void EmObservation_ExactExtreme_NormalizesFiniteLogDensities()
    {
        AssertObservation(
            CreateTailMixture(), new ExactData(2000, 40.0), 0.0,
            -797.42256721210072, new[] { 0.012122863632044123, 0.98787713636796981 });
    }

    /// <summary>
    /// Verifies a zero physical component weight retains exactly zero responsibility in an extreme tail.
    /// </summary>
    [TestMethod]
    public void EmObservation_ZeroComponentWeight_RetainsZeroResponsibility()
    {
        var mixture = new Mixture(new[] { 0.0, 1.0 }, new UnivariateDistributionBase[]
        {
            new Normal(0.0, 1.0), new Normal(0.1, 1.0)
        });
        double expected = -0.5 * 39.9 * 39.9 - 0.5 * Math.Log(2.0 * Math.PI);
        AssertObservation(mixture, new ExactData(2000, 40.0), 0.0, expected, new[] { 0.0, 1.0 });
    }

    /// <summary>
    /// Verifies an inactive component's endpoint singularity does not invalidate an ordinary mixture observation.
    /// </summary>
    [TestMethod]
    public void EmObservation_ZeroComponentWeight_IgnoresInactiveEndpointSingularity()
    {
        var mixture = new Mixture(new[] { 0.0, 1.0 }, new UnivariateDistributionBase[]
        {
            new GammaDistribution(1.0, 0.5), new Normal(0.0, 1.0)
        });
        AssertObservation(
            mixture, new ExactData(2000, 0.0), 0.0,
            -0.9189385332046727, new[] { 0.0, 1.0 });
    }

    /// <summary>
    /// Verifies identical components preserve their physical weights when their common log density dwarfs log weights.
    /// </summary>
    [TestMethod]
    public void EmObservation_DeepCommonLogDensity_PreservesRelativeWeights()
    {
        var mixture = new Mixture(new[] { 0.4, 0.6 }, new UnivariateDistributionBase[]
        {
            new Normal(0.0, 1.0), new Normal(0.0, 1.0)
        });
        var responsibilities = new double[1, 2];
        double actual = EvaluateObservation(mixture, new ExactData(2000, 1E100), 0.0, responsibilities);
        Assert.IsTrue(double.IsFinite(actual));
        Assert.AreEqual(-5E199, actual, 1E184);
        Assert.AreEqual(0.4, responsibilities[0, 0], 1E-15);
        Assert.AreEqual(0.6, responsibilities[0, 1], 1E-15);
        Assert.AreEqual(1.0, responsibilities[0, 0] + responsibilities[0, 1], 1E-15);
    }

    /// <summary>
    /// Verifies both censoring directions and low-outlier censoring use finite logarithmic tails.
    /// </summary>
    [TestMethod]
    public void EmObservation_ExtremeCensoring_NormalizesFiniteLogTails()
    {
        double[] expected = { 0.012092960332159716, 0.9879070396678733 };
        AssertObservation(
            CreateTailMixture(), new ThresholdData(2000, 2000, 40.0) { NumberAbove = 1 }, 0.0,
            -801.109600959212, expected);
        AssertObservation(
            CreateTailMixture(reflected: true),
            new ThresholdData(2000, 2000, -40.0) { NumberBelow = 1 }, 0.0,
            -801.109600959212, expected);
        AssertObservation(
            CreateTailMixture(reflected: true), new ExactData(2000, -41.0, isLowOutlier: true), -40.0,
            -801.109600959212, expected);
    }

    /// <summary>
    /// Verifies interval probabilities remain finite when both endpoint CDFs round to one.
    /// </summary>
    [TestMethod]
    public void EmObservation_ExtremeInterval_UsesLogTailDifference()
    {
        AssertObservation(
            CreateTailMixture(), new IntervalData(2000, 40.0, 40.005, 40.01), 0.0,
            -802.22063348929146, new[] { 0.012117274644390144, 0.98788272535558863 });
    }

    /// <summary>
    /// Verifies a positive-conditioned component is valid even when its unconditioned positive mass underflows.
    /// </summary>
    [TestMethod]
    public void EmObservation_HurdleExtreme_NormalizesPositiveMassInLogSpace()
    {
        Mixture mixture = CreateHurdleMixture(new Normal(-40.0, 1.0));
        AssertObservation(mixture, new ExactData(2000, 0.02), 0.0, 2.6661599292347509, new[] { 1.0 });
        AssertObservation(
            mixture, new ThresholdData(2000, 2000, 0.02) { NumberAbove = 1 }, 0.0,
            -1.0238428037661871, new[] { 1.0 });
        AssertObservation(
            mixture, new IntervalData(2000, 0.01, 0.015, 0.02), 0.0,
            -1.7322641576343623, new[] { 1.0 });
        AssertObservation(
            mixture, new ThresholdData(2000, 2000, 0.02) { NumberBelow = 1 }, 0.0,
            -0.44505645963310253, new[] { 0.68788433939523796 });
        AssertObservation(
            mixture, new IntervalData(2000, -1.0, 0.0, 0.02), 0.0,
            -0.44505645963310253, new[] { 0.68788433939523796 });
    }

    /// <summary>
    /// Verifies model validation accepts remote normal components with finite log positive mass.
    /// </summary>
    /// <param name="location">The remote unconditioned normal mean.</param>
    [TestMethod]
    [DataRow(-40.0)]
    [DataRow(-1000.0)]
    public void HurdleValidation_RemoteNormal_AcceptsFiniteLogPositiveMass(double location)
    {
        var model = new MixtureModel(
            CreateDataFrame(), new List<UnivariateDistributionType> { UnivariateDistributionType.Normal },
            isZeroInflated: true);
        model.Mixture!.SetParameters(new[] { 1.0 }, new[] { location, 1.0 });
        var validation = model.Validate();
        Assert.IsTrue(validation.IsValid, string.Join("; ", validation.ValidationMessages));
    }

    /// <summary>
    /// Verifies a hurdle component with zero physical weight need not have positive conditional support.
    /// </summary>
    [TestMethod]
    public void HurdleValidation_ZeroComponentWeight_IgnoresInactivePositiveSupport()
    {
        var frame = CreateDataFrame();
        frame.ExactSeries = new ExactSeries(new List<ExactData>
        {
            new(1999, 0.0), new(2000, 1.0), new(2001, 2.0), new(2002, 3.0), new(2003, 4.0)
        });
        var mixture = new Mixture(new[] { 0.0, 0.8 }, new UnivariateDistributionBase[]
        {
            new GeneralizedExtremeValue(-3.0, 1.0, 1.0), new Normal(0.0, 1.0)
        });
        MixtureModel model = CreateManualPriorModel(mixture, frame, isZeroInflated: true);

        var validation = model.Validate();
        Assert.IsTrue(validation.IsValid, string.Join("; ", validation.ValidationMessages));
        Assert.AreEqual(0.2, model.Mixture!.ZeroWeight, 0.0);
    }

    /// <summary>
    /// Verifies an inactive log component does not exclude observations supported by the active normal component.
    /// </summary>
    [TestMethod]
    public void MixtureValidation_ZeroComponentWeight_IgnoresInactiveLogSupport()
    {
        var frame = CreateDataFrame();
        frame.ExactSeries = new ExactSeries(new List<ExactData>
        {
            new(2000, -2.0), new(2001, -1.0), new(2002, 1.0), new(2003, 2.0)
        });
        var mixture = new Mixture(new[] { 1.0, 0.0 }, new UnivariateDistributionBase[]
        {
            new Normal(0.0, 1.0), new LogNormal(0.0, 1.0)
        });
        MixtureModel model = CreateManualPriorModel(mixture, frame);

        var validation = model.Validate();
        Assert.IsTrue(validation.IsValid, string.Join("; ", validation.ValidationMessages));
    }

    /// <summary>
    /// Verifies the fixed zero atom and open-lower interval convention survive the EM log calculation.
    /// </summary>
    [TestMethod]
    public void EmObservation_HurdleAtom_PreservesEndpointConventions()
    {
        Mixture mixture = CreateHurdleMixture(new Normal(0.0, 1.0));
        AssertObservation(mixture, new ExactData(2000, 0.0), 0.0, Math.Log(0.2), new[] { 0.0 });
        AssertObservation(
            mixture, new IntervalData(2000, -1.0, -0.5, 0.0), 0.0, Math.Log(0.2), new[] { 0.0 });
        AssertObservation(
            mixture, new ThresholdData(2000, 2000, 0.0) { NumberBelow = 1 }, 0.0,
            Math.Log(0.2), new[] { 0.0 });
        AssertObservation(
            mixture, new ThresholdData(2000, 2000, 0.0) { NumberAbove = 1 }, 0.0,
            Math.Log(0.8), new[] { 1.0 });
        AssertObservation(
            mixture, new ThresholdData(2000, 2000, -1.0) { NumberAbove = 1 }, 0.0,
            0.0, new[] { 0.8 });
    }

    /// <summary>
    /// Verifies uncertain observations retain the established GL20 domain and retained-mass normalization.
    /// </summary>
    [TestMethod]
    public void EmObservation_Uncertain_PreservesExistingQuadratureContract()
    {
        var measurement = new Normal(0.0, 0.5);
        Mixture mixture = CreateHurdleMixture(new Normal(0.0, 1.0));
        const double lowerProbability = 1E-8;
        const double upperProbability = 1.0 - 1E-8;
        double retainedMass = upperProbability - lowerProbability;
        double lower = Math.Max(0.0, measurement.InverseCDF(lowerProbability));
        double upper = measurement.InverseCDF(upperProbability);
        double componentProbability = 0.8 * Integration.GaussLegendre20(
            value => measurement.PDF(value) * mixture.Distributions[0].PDF(value) /
                mixture.Distributions[0].CCDF(0.0), lower, upper) / retainedMass;
        double atomProbability = 0.2 * measurement.PDF(0.0) / retainedMass;
        double total = atomProbability + componentProbability;
        AssertObservation(
            mixture, new UncertainData(2000, measurement), 0.0,
            Math.Log(total), new[] { componentProbability / total });
    }

    /// <summary>
    /// Verifies a truly impossible observation still reports its row instead of inventing probability mass.
    /// </summary>
    [TestMethod]
    public void EmObservation_ImpossibleRow_PreservesDiagnosticContext()
    {
        var mixture = new Mixture(new[] { 1.0 }, new UnivariateDistributionBase[] { new Uniform(0.0, 1.0) });
        var exception = Assert.ThrowsException<TargetInvocationException>(
            () => EvaluateObservation(mixture, new ExactData(2000, 2.0), 0.0, new double[1, 1]));
        Assert.IsInstanceOfType<InvalidOperationException>(exception.InnerException);
        StringAssert.Contains(exception.InnerException.Message, "row 0");
    }

    /// <summary>
    /// Creates a small valid frame for model initialization without estimation.
    /// </summary>
    /// <returns>A frame containing four varied exact observations.</returns>
    private static BestFitDataFrame CreateDataFrame()
    {
        return new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new(2000, 1.0), new(2001, 2.0), new(2002, 3.0), new(2003, 4.0)
            })
        };
    }

    /// <summary>
    /// Configures fixed two-component validation fixtures without invoking data-based parameter estimation.
    /// </summary>
    /// <param name="mixture">The physical component parameters and weights.</param>
    /// <param name="frame">The observations whose support will be validated.</param>
    /// <param name="isZeroInflated">Whether exact zero observations determine the fixed zero atom.</param>
    /// <returns>A model with explicit valid priors in its public parameter coordinates.</returns>
    private static MixtureModel CreateManualPriorModel(
        Mixture mixture, BestFitDataFrame frame, bool isZeroInflated = false)
    {
        var model = new MixtureModel
        {
            UseDefaultFlatPriors = false,
            Mixture = mixture,
            IsZeroInflated = isZeroInflated,
            DataFrame = frame
        };
        model.Parameters.Clear();
        foreach (double value in mixture.GetParameters)
        {
            model.Parameters.Add(new ModelParameter
            {
                Name = $"Parameter {model.Parameters.Count + 1}",
                Value = value,
                PriorDistribution = new Normal(value, Math.Max(1.0, Math.Abs(value)))
            });
        }
        return model;
    }

    /// <summary>
    /// Isolates consumption of processed priors from the separate moment-based prior-construction algorithm.
    /// </summary>
    /// <param name="model">The BestFit model under test.</param>
    /// <param name="distribution">The proposed physical distribution.</param>
    /// <param name="probabilities">The quantile probabilities in ascending order.</param>
    private static void ConfigureProcessedPriors(
        UnivariateDistributionModelBase model, UnivariateDistributionBase distribution, double[] probabilities)
    {
        model.UseJeffreysRuleForScale = false;
        double[] parameters = distribution.GetParameters;
        for (int i = 0; i < parameters.Length; i++)
            model.Parameters[i].PriorDistribution = new Normal(parameters[i], Math.Max(1.0, Math.Abs(parameters[i])));
        var priors = new List<QuantilePrior>();
        double previous = 0.0;
        foreach (double probability in probabilities)
        {
            double current = distribution.InverseCDF(probability);
            double value = current - previous;
            priors.Add(new QuantilePrior(1.0 - probability, new Normal(value, Math.Max(1.0, Math.Abs(value)))));
            previous = current;
        }
        Type baseType = typeof(UnivariateDistributionModelBase);
        baseType.GetField("_enableQuantilePriors", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, true);
        baseType.GetField("_useSingleQuantile", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, false);
        baseType.GetField("_quantilePriorsTrue", BindingFlags.Instance | BindingFlags.NonPublic)!.SetValue(model, priors);
    }

    /// <summary>
    /// Checks both prior entry points against the independent determinant and each other.
    /// </summary>
    /// <param name="model">The model with processed priors.</param>
    /// <param name="parameters">The proposed public parameter vector.</param>
    /// <param name="expectedJacobian">The independent log determinant.</param>
    private static void AssertPriorContributions(
        UnivariateDistributionModelBase model, double[] parameters, double expectedJacobian)
    {
        List<PriorComponent> components = model.PointwisePriorLogLikelihood(parameters);
        double actualJacobian = components.Single(component => component.Type == PriorComponentType.Jacobian).LogLikelihood;
        Assert.IsTrue(double.IsFinite(actualJacobian));
        Assert.AreEqual(expectedJacobian, actualJacobian, 2E-10);
        double actualTotal = model.PriorLogLikelihood(parameters);
        Assert.IsTrue(double.IsFinite(actualTotal));
        double expectedTotal = components.Where(component => component.Type != PriorComponentType.Jacobian)
            .Sum(component => component.LogLikelihood) + expectedJacobian;
        Assert.AreEqual(expectedTotal, actualTotal, 2E-10);
        Assert.AreEqual(components.Sum(component => component.LogLikelihood), actualTotal, 2E-10);
    }

    /// <summary>
    /// Creates the fixed ordinary mixture used by the independent R tail fixtures.
    /// </summary>
    /// <param name="reflected">Whether to reflect the second mean for lower-tail symmetry.</param>
    /// <returns>A mixture with unchanged physical weights of 0.4 and 0.6.</returns>
    private static Mixture CreateTailMixture(bool reflected = false)
    {
        return new Mixture(new[] { 0.4, 0.6 }, new UnivariateDistributionBase[]
        {
            new Normal(0.0, 1.0), new Normal(reflected ? -0.1 : 0.1, 1.0)
        });
    }

    /// <summary>
    /// Creates a fixed hurdle law without deriving weights from data or fitting.
    /// </summary>
    /// <param name="component">The unconditioned continuous law.</param>
    /// <returns>A hurdle mixture with a 0.2 zero atom and 0.8 positive component weight.</returns>
    private static Mixture CreateHurdleMixture(UnivariateDistributionBase component)
    {
        return new Mixture(new[] { 0.8 }, new[] { component }) { IsZeroInflated = true, ZeroWeight = 0.2 };
    }

    /// <summary>
    /// Checks one deterministic EM observation likelihood and its component responsibilities.
    /// </summary>
    /// <param name="mixture">The current mixture, with physical weights.</param>
    /// <param name="observation">The observed datum.</param>
    /// <param name="lowOutlierThreshold">The threshold for flagged exact values.</param>
    /// <param name="expectedLogLikelihood">The independent log probability or density.</param>
    /// <param name="expectedResponsibilities">The expected continuous-component responsibilities.</param>
    private static void AssertObservation(
        Mixture mixture, Data observation, double lowOutlierThreshold,
        double expectedLogLikelihood, double[] expectedResponsibilities)
    {
        var responsibilities = new double[1, mixture.Distributions.Length];
        double actual = EvaluateObservation(mixture, observation, lowOutlierThreshold, responsibilities);
        Assert.IsTrue(double.IsFinite(actual));
        Assert.AreEqual(expectedLogLikelihood, actual, 2E-10);
        for (int i = 0; i < expectedResponsibilities.Length; i++)
            Assert.AreEqual(expectedResponsibilities[i], responsibilities[0, i], 2E-11);
    }

    /// <summary>
    /// Calls only the extracted observation step; missing production support fails at runtime during the red gate.
    /// </summary>
    /// <param name="mixture">The current mixture.</param>
    /// <param name="observation">The observed datum.</param>
    /// <param name="lowOutlierThreshold">The threshold for flagged exact values.</param>
    /// <param name="responsibilities">The single-row output matrix.</param>
    /// <returns>The row log likelihood.</returns>
    private static double EvaluateObservation(
        Mixture mixture, Data observation, double lowOutlierThreshold, double[,] responsibilities)
    {
        MethodInfo? method = typeof(MixtureModel).GetMethod(
            "ExpectationStepObservationLogLikelihood", BindingFlags.NonPublic | BindingFlags.Static);
        Assert.IsNotNull(method, "The EM observation step must expose a private deterministic log-likelihood boundary.");
        return (double)method.Invoke(null, new object[] { mixture, observation, lowOutlierThreshold, 0, responsibilities })!;
    }
}
