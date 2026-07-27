using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies true nuisance-parameter profiling for MLE and MAP.
/// </summary>
/// <remarks>
/// The correlated quadratic fixture has an analytic nuisance optimum, so the R result
/// also has an independent closed-form check. MAP is tested with both flat and informative priors.
/// </remarks>
[TestClass]
public sealed class ProfileLikelihoodFindingTests
{
    /// <summary>
    /// Confirms that MLE reoptimizes nuisance parameters and matches the R true profile.
    /// </summary>
    [TestMethod]
    public void MLE_ProfileLikelihood_MatchesRTrueProfile()
    {
        ProfileLikelihoodOracle oracle = LoadOracle();
        var model = CreateModel(oracle);
        var estimator = new MaximumLikelihood(model, OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };

        Assert.IsTrue(estimator.Estimate(), "The deterministic quadratic MLE should converge.");
        Assert.AreEqual(
            oracle.BbmleFit.Theta1,
            estimator.BestParameterSet.Values[0],
            oracle.Metadata.Tolerances.FittedParameterAbsolute);
        Assert.AreEqual(
            oracle.BbmleFit.Theta2,
            estimator.BestParameterSet.Values[1],
            oracle.Metadata.Tolerances.FittedParameterAbsolute);
        Assert.AreEqual(
            oracle.BbmleFit.MaximumLogLikelihood,
            estimator.MaximumLogLikelihood,
            oracle.Metadata.Tolerances.LogLikelihoodAbsolute);

        double[,] actualProfile = estimator.ProfileLikelihood(oracle.Fixture.BinCount)[0];
        AssertTrueProfile(actualProfile, oracle);

        double[,] actualIntervals = estimator.ParameterConfidenceIntervals(oracle.Fixture.Alpha);
        AssertTrueProfileIntervals(actualIntervals, oracle);
    }

    /// <summary>
    /// Confirms that MAP with flat priors matches the R true profile up to the prior constant.
    /// </summary>
    [TestMethod]
    public void MAP_ProfileLikelihood_WithFlatPriors_MatchesRTrueProfile()
    {
        ProfileLikelihoodOracle oracle = LoadOracle();
        var model = CreateModel(oracle);
        var estimator = new MaximumAPosteriori(model, OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };

        Assert.IsTrue(estimator.Estimate(), "The deterministic quadratic MAP should converge.");
        Assert.AreEqual(
            oracle.BbmleFit.Theta1,
            estimator.BestParameterSet.Values[0],
            oracle.Metadata.Tolerances.FittedParameterAbsolute);
        Assert.AreEqual(
            oracle.BbmleFit.Theta2,
            estimator.BestParameterSet.Values[1],
            oracle.Metadata.Tolerances.FittedParameterAbsolute);

        double priorConstant = model.PriorLogLikelihood(estimator.BestParameterSet.Values);
        double[,] actualProfile = estimator.ProfileLikelihood(oracle.Fixture.BinCount)[0];
        AssertTrueProfile(actualProfile, oracle, priorConstant);

        double[,] actualIntervals = estimator.ParameterConfidenceIntervals(oracle.Fixture.Alpha);
        AssertTrueProfileIntervals(actualIntervals, oracle);
    }

    /// <summary>
    /// Confirms that MAP nuisance profiling maximizes the full posterior kernel, including an informative prior.
    /// </summary>
    [TestMethod]
    public void MAP_ProfileLikelihood_WithInformativePrior_ProfilesFullPosteriorKernel()
    {
        ProfileLikelihoodOracle oracle = LoadOracle();
        const double nuisancePriorMean = 1.25d;
        const double nuisancePriorStandardDeviation = 0.45d;
        var model = new CorrelatedQuadraticModel(
            oracle.Fixture.Rho,
            oracle.Fixture.LowerBound,
            oracle.Fixture.UpperBound,
            oracle.Fixture.Start,
            nuisancePriorMean,
            nuisancePriorStandardDeviation);
        var estimator = new MaximumAPosteriori(model, OptimizationMethod.BFGS)
        {
            ComputeHessian = false,
            ReportFailure = true
        };

        Assert.IsTrue(estimator.Estimate(), "The informative-prior quadratic MAP should converge.");
        double[,] actualProfile = estimator.ProfileLikelihood(oracle.Fixture.BinCount)[0];
        double dataPrecision = 1d / (1d - (oracle.Fixture.Rho * oracle.Fixture.Rho));
        double priorPrecision = 1d / (nuisancePriorStandardDeviation * nuisancePriorStandardDeviation);
        int separatedFromCoordinateSlice = 0;

        for (int row = 0; row < actualProfile.GetLength(0); row++)
        {
            double theta1 = actualProfile[row, 0];
            double profiledTheta2 =
                ((oracle.Fixture.Rho * theta1 * dataPrecision) + (nuisancePriorMean * priorPrecision)) /
                (dataPrecision + priorPrecision);
            double expected = model.LogLikelihood([theta1, profiledTheta2]);
            Assert.AreEqual(
                expected,
                actualProfile[row, 1],
                oracle.Metadata.Tolerances.LogLikelihoodAbsolute,
                $"Full-posterior profile mismatch at row {row}.");

            double coordinateSlice = model.LogLikelihood(
                [theta1, estimator.BestParameterSet.Values[1]]);
            if (Math.Abs(expected - coordinateSlice) > 0.1d)
                separatedFromCoordinateSlice++;
        }

        Assert.IsTrue(
            separatedFromCoordinateSlice >= actualProfile.GetLength(0) / 2,
            "The fixture must distinguish nuisance reoptimization from a coordinate slice.");
    }

    /// <summary>
    /// Creates the correlated quadratic model represented by the oracle fixture.
    /// </summary>
    /// <param name="oracle">The deserialized R oracle.</param>
    /// <returns>A deterministic two-parameter model.</returns>
    private static CorrelatedQuadraticModel CreateModel(ProfileLikelihoodOracle oracle)
    {
        return new CorrelatedQuadraticModel(
            oracle.Fixture.Rho,
            oracle.Fixture.LowerBound,
            oracle.Fixture.UpperBound,
            oracle.Fixture.Start);
    }

    /// <summary>
    /// Verifies MLE nuisance reoptimization against every R true-profile grid value.
    /// </summary>
    /// <param name="actualProfile">The profile returned by the MLE estimator.</param>
    /// <param name="oracle">The R oracle.</param>
    /// <param name="logKernelOffset">Constant added to the data likelihood, such as bounded flat-prior density.</param>
    private static void AssertTrueProfile(
        double[,] actualProfile,
        ProfileLikelihoodOracle oracle,
        double logKernelOffset = 0d)
    {
        Assert.AreEqual(oracle.ProfileGrid.Count, actualProfile.GetLength(0));

        for (int row = 0; row < oracle.ProfileGrid.Count; row++)
        {
            ProfileGridPoint expected = oracle.ProfileGrid[row];
            Assert.AreEqual(
                expected.Theta1,
                actualProfile[row, 0],
                oracle.Metadata.Tolerances.LogLikelihoodAbsolute,
                $"Grid value mismatch at row {row}.");
            Assert.AreEqual(
                expected.TrueProfileLogLikelihood + logKernelOffset,
                actualProfile[row, 1],
                oracle.Metadata.Tolerances.LogLikelihoodAbsolute,
                $"True profile mismatch at row {row}.");
        }
    }

    /// <summary>
    /// Verifies the MLE likelihood-ratio interval against the R true-profile interval.
    /// </summary>
    /// <param name="actualIntervals">The interval matrix returned by MLE.</param>
    /// <param name="oracle">The R oracle.</param>
    private static void AssertTrueProfileIntervals(double[,] actualIntervals, ProfileLikelihoodOracle oracle)
    {
        Assert.AreEqual(
            oracle.Intervals.TrueProfile[0],
            actualIntervals[0, 0],
            oracle.Metadata.Tolerances.IntervalAbsolute);
        Assert.AreEqual(
            oracle.Intervals.TrueProfile[1],
            actualIntervals[0, 1],
            oracle.Metadata.Tolerances.IntervalAbsolute);
    }

    /// <summary>
    /// Loads the committed oracle copied to the verification output directory.
    /// </summary>
    /// <returns>The deserialized profile-likelihood oracle.</returns>
    private static ProfileLikelihoodOracle LoadOracle()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "profile-likelihood-oracle.json");
        string json = File.ReadAllText(path);
        return JsonSerializer.Deserialize<ProfileLikelihoodOracle>(
            json,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
            }) ?? throw new InvalidOperationException("Unable to deserialize the TR-023 oracle.");
    }

    /// <summary>
    /// Two-parameter correlated quadratic log-likelihood with bounded flat priors.
    /// </summary>
    private sealed class CorrelatedQuadraticModel : ModelBase
    {
        private readonly double _rho;
        private readonly double _lowerBound;
        private readonly double _upperBound;
        private readonly double[] _start;
        private readonly double? _nuisancePriorMean;
        private readonly double _nuisancePriorStandardDeviation;

        /// <summary>
        /// Initializes the deterministic correlated quadratic fixture.
        /// </summary>
        /// <param name="rho">Correlation controlling nuisance-parameter coupling.</param>
        /// <param name="lowerBound">Common lower parameter bound.</param>
        /// <param name="upperBound">Common upper parameter bound.</param>
        /// <param name="start">Initial values for both parameters.</param>
        /// <param name="nuisancePriorMean">Optional additional Gaussian prior mean for theta2.</param>
        /// <param name="nuisancePriorStandardDeviation">Standard deviation of the optional theta2 prior.</param>
        public CorrelatedQuadraticModel(
            double rho,
            double lowerBound,
            double upperBound,
            IReadOnlyList<double> start,
            double? nuisancePriorMean = null,
            double nuisancePriorStandardDeviation = 1d)
        {
            _rho = rho;
            _lowerBound = lowerBound;
            _upperBound = upperBound;
            _start = start.ToArray();
            _nuisancePriorMean = nuisancePriorMean;
            _nuisancePriorStandardDeviation = nuisancePriorStandardDeviation;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters()
        {
            Parameters = new List<ModelParameter>
            {
                new("Correlated quadratic", "theta1", _start[0], _lowerBound, _upperBound, new Uniform(_lowerBound, _upperBound)),
                new("Correlated quadratic", "theta2", _start[1], _lowerBound, _upperBound, new Uniform(_lowerBound, _upperBound))
            };
        }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            double theta1 = parameters[0];
            double theta2 = parameters[1];
            double numerator =
                (theta1 * theta1) -
                (2.0 * _rho * theta1 * theta2) +
                (theta2 * theta2);
            return -numerator / (2.0 * (1.0 - (_rho * _rho)));
        }

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            return new[] { DataLogLikelihood(parameters) };
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            double logPrior = base.PriorLogLikelihood(parameters);
            if (!_nuisancePriorMean.HasValue)
                return logPrior;

            double z = (parameters[1] - _nuisancePriorMean.Value) /
                _nuisancePriorStandardDeviation;
            return logPrior -
                (0.5d * z * z) -
                Math.Log(_nuisancePriorStandardDeviation * Math.Sqrt(2d * Math.PI));
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters)
        {
            return new List<DataComponent>
            {
                new(0, DataLogLikelihood(parameters), 0.0, name: "quadratic fixture")
            };
        }

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new CorrelatedQuadraticModel(
                _rho,
                _lowerBound,
                _upperBound,
                _start,
                _nuisancePriorMean,
                _nuisancePriorStandardDeviation);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement()
        {
            return new XElement(nameof(CorrelatedQuadraticModel));
        }

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate()
        {
            return (true, new List<string>());
        }
    }

    /// <summary>
    /// Root DTO for the committed R profile-likelihood oracle.
    /// </summary>
    private sealed class ProfileLikelihoodOracle
    {
        /// <summary>Gets or initializes artifact metadata.</summary>
        public required OracleMetadata Metadata { get; init; }

        /// <summary>Gets or initializes fixture inputs.</summary>
        public required OracleFixture Fixture { get; init; }

        /// <summary>Gets or initializes the bbmle joint fit.</summary>
        public required BbmleFit BbmleFit { get; init; }

        /// <summary>Gets or initializes profiled grid values.</summary>
        public required List<ProfileGridPoint> ProfileGrid { get; init; }

        /// <summary>Gets or initializes likelihood-ratio intervals.</summary>
        public required OracleIntervals Intervals { get; init; }
    }

    /// <summary>
    /// Metadata and numeric tolerances for the R oracle.
    /// </summary>
    private sealed class OracleMetadata
    {
        /// <summary>Gets or initializes numeric tolerances.</summary>
        public required OracleTolerances Tolerances { get; init; }
    }

    /// <summary>
    /// Numeric comparison tolerances written by the R generator.
    /// </summary>
    private sealed class OracleTolerances
    {
        /// <summary>Gets or initializes the parameter tolerance.</summary>
        public double FittedParameterAbsolute { get; init; }

        /// <summary>Gets or initializes the log-likelihood tolerance.</summary>
        public double LogLikelihoodAbsolute { get; init; }

        /// <summary>Gets or initializes the interval tolerance.</summary>
        public double IntervalAbsolute { get; init; }
    }

    /// <summary>
    /// Inputs for the correlated quadratic fixture.
    /// </summary>
    private sealed class OracleFixture
    {
        /// <summary>Gets or initializes the correlation.</summary>
        public double Rho { get; init; }

        /// <summary>Gets or initializes the common lower bound.</summary>
        public double LowerBound { get; init; }

        /// <summary>Gets or initializes the common upper bound.</summary>
        public double UpperBound { get; init; }

        /// <summary>Gets or initializes the grid size.</summary>
        public int BinCount { get; init; }

        /// <summary>Gets or initializes the interval alpha.</summary>
        public double Alpha { get; init; }

        /// <summary>Gets or initializes the two starting values.</summary>
        public required double[] Start { get; init; }
    }

    /// <summary>
    /// Joint optimum from R bbmle.
    /// </summary>
    private sealed class BbmleFit
    {
        /// <summary>Gets or initializes theta1.</summary>
        public double Theta1 { get; init; }

        /// <summary>Gets or initializes theta2.</summary>
        public double Theta2 { get; init; }

        /// <summary>Gets or initializes the maximum data log-likelihood.</summary>
        public double MaximumLogLikelihood { get; init; }
    }

    /// <summary>
    /// One fixed-parameter grid result from the R oracle.
    /// </summary>
    private sealed class ProfileGridPoint
    {
        /// <summary>Gets or initializes the fixed theta1 value.</summary>
        public double Theta1 { get; init; }

        /// <summary>Gets or initializes the coordinate-slice log-likelihood.</summary>
        public double ConditionalLogLikelihood { get; init; }

        /// <summary>Gets or initializes the true-profile log-likelihood.</summary>
        public double TrueProfileLogLikelihood { get; init; }
    }

    /// <summary>
    /// Likelihood-ratio intervals for true profiling and coordinate slicing.
    /// </summary>
    private sealed class OracleIntervals
    {
        /// <summary>Gets or initializes the true-profile interval.</summary>
        public required double[] TrueProfile { get; init; }

        /// <summary>Gets or initializes the coordinate-slice interval.</summary>
        public required double[] ConditionalSlice { get; init; }
    }
}
