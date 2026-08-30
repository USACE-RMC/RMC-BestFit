using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;
using System.Xml.Linq;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies Bayesian posterior summaries against an independently calculated Normal-Normal conjugate posterior.
/// </summary>
/// <remarks>
/// This is analytical oracle evidence, not generated-parent recovery. The fixed N=1000, seed=12345
/// Normal sample is used only to define the likelihood input. The independent posterior calculation
/// protects the known-scale Normal likelihood, Normal prior, posterior moments, and central 95%
/// interval calculation without weakening it into a recovery tolerance.
/// </remarks>
[TestClass]
public sealed class BayesianConjugateOracleVerificationTests
{
    private const double GeneratingMean = 100d;
    private const double GeneratingStandardDeviation = 15d;

    /// <summary>
    /// Verifies the sampled posterior summary against the exact conjugate Normal-Normal posterior.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar Normal observation; N=1000; seed=12345; known observation sigma=15;
    /// prior=(mu=120, sigma=5); fitted coordinate=mu. The central interval width is 95%; the
    /// conditional secondary 5% criterion does not apply to this analytical coordinate oracle.
    /// </remarks>
    [TestMethod]
    public async Task InformativePrior_NormalNormalConjugatePosterior_MatchesClosedForm()
    {
        const double priorMean = 120d;
        const double priorStandardDeviation = 5d;
        double[] observations = CreateObservations();
        var model = new KnownScaleNormalMeanModel(observations, GeneratingStandardDeviation, priorMean, priorStandardDeviation);
        var bayesian = new BayesianAnalysis(model) { CredibleIntervalWidth = 0.95d };

        await bayesian.RunAsync();

        Assert.IsNotNull(bayesian.Results);
        var actual = bayesian.Results.ParameterResults[0].SummaryStatistics;
        double likelihoodVariance = GeneratingStandardDeviation * GeneratingStandardDeviation / observations.Length;
        double priorVariance = priorStandardDeviation * priorStandardDeviation;
        double posteriorVariance = 1d / ((1d / likelihoodVariance) + (1d / priorVariance));
        double posteriorMean = posteriorVariance * ((observations.Average() / likelihoodVariance) + (priorMean / priorVariance));
        double posteriorStandardDeviation = Math.Sqrt(posteriorVariance);
        var posterior = new Normal(posteriorMean, posteriorStandardDeviation);
        const double tailProbability = 0.025d;

        Assert.AreEqual(posteriorMean, actual.Mean, 0.05d, "Posterior mean differs from the conjugate oracle.");
        Assert.AreEqual(posteriorStandardDeviation, actual.StandardDeviation, 0.03d, "Posterior scale differs from the conjugate oracle.");
        Assert.AreEqual(posterior.InverseCDF(tailProbability), actual.LowerCI, 0.10d, "Lower posterior limit differs from the conjugate oracle.");
        Assert.AreEqual(posterior.InverseCDF(1d - tailProbability), actual.UpperCI, 0.10d, "Upper posterior limit differs from the conjugate oracle.");
    }

    /// <summary>Creates the declared seeded Normal observations for the analytical likelihood input.</summary>
    /// <returns>The 1,000 scalar observations.</returns>
    private static double[] CreateObservations()
    {
        _ = RecoveryDesign.ScalarObservations("Independent Normal scalar observations supplied to a conjugate likelihood.");
        var random = new Random(12345);
        var normal = new Normal(GeneratingMean, GeneratingStandardDeviation);
        var observations = new double[RecoveryDesign.SampleSize];
        for (int index = 0; index < observations.Length; index++)
            observations[index] = normal.InverseCDF(random.NextDouble());
        return observations;
    }

    /// <summary>Models a Normal mean with known observation scale and a Gaussian prior.</summary>
    private sealed class KnownScaleNormalMeanModel : ModelBase
    {
        private readonly double[] _observations;
        private readonly double _knownStandardDeviation;
        private readonly double _priorMean;
        private readonly double _priorStandardDeviation;

        /// <summary>Initializes the conjugate fixture.</summary>
        /// <param name="observations">Independent Normal observations.</param>
        /// <param name="knownStandardDeviation">Known positive observation standard deviation.</param>
        /// <param name="priorMean">Prior mean.</param>
        /// <param name="priorStandardDeviation">Prior positive standard deviation.</param>
        public KnownScaleNormalMeanModel(IReadOnlyList<double> observations, double knownStandardDeviation, double priorMean, double priorStandardDeviation)
        {
            _observations = observations.ToArray();
            _knownStandardDeviation = knownStandardDeviation;
            _priorMean = priorMean;
            _priorStandardDeviation = priorStandardDeviation;
            _useDefaultFlatPriors = false;
            SetDefaultParameters();
        }

        /// <inheritdoc/>
        public override void SetDefaultParameters() => Parameters =
        [
            new ModelParameter("Known-scale Normal", "mu", _priorMean, double.MinValue, double.MaxValue, new Normal(_priorMean, _priorStandardDeviation))
        ];

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters) => parameters.Length == 1 ? PointwiseDataLogLikelihood(parameters).Sum() : double.NegativeInfinity;

        /// <inheritdoc/>
        public override double[] PointwiseDataLogLikelihood(double[] parameters)
        {
            if (parameters.Length != 1)
                return Enumerable.Repeat(double.NegativeInfinity, _observations.Length).ToArray();
            double variance = _knownStandardDeviation * _knownStandardDeviation;
            double normalization = -Math.Log(_knownStandardDeviation * Math.Sqrt(2d * Math.PI));
            return _observations.Select(value => normalization - (0.5d * (value - parameters[0]) * (value - parameters[0]) / variance)).ToArray();
        }

        /// <inheritdoc/>
        public override List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters) => PointwiseDataLogLikelihood(parameters).Select((value, index) => new DataComponent(index, value, 0d, name: "Normal observation")).ToList();

        /// <inheritdoc/>
        public override IModel Clone()
        {
            var clone = new KnownScaleNormalMeanModel(_observations, _knownStandardDeviation, _priorMean, _priorStandardDeviation);
            clone.SetParameterValues(Parameters.Select(parameter => parameter.Value).ToArray());
            return clone;
        }

        /// <inheritdoc/>
        public override XElement ToXElement() => new(nameof(KnownScaleNormalMeanModel));

        /// <inheritdoc/>
        public override (bool IsValid, List<string> ValidationMessages) Validate() =>
            _observations.Length > 0 && double.IsFinite(_knownStandardDeviation) && _knownStandardDeviation > 0d && double.IsFinite(_priorMean) && double.IsFinite(_priorStandardDeviation) && _priorStandardDeviation > 0d
                ? (true, [])
                : (false, ["The conjugate Normal fixture requires observations and finite positive scales."]);
    }
}
