using System.Text.Json;
using RMC.BestFit.Models;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;
using NumericsLogNormal = Numerics.Distributions.LogNormal;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Verifies that the rating-curve data log likelihood is the discharge-space density implied by its
/// base-10 lognormal observation model (TR-043), against the committed SciPy oracle and an
/// independent in-process evaluation with the Numerics base-10 lognormal density.
/// </summary>
/// <remarks>
/// <para>
/// For aligned pair <c>i</c> with predicted discharge <c>q_i</c> and log10-space scale
/// <c>sigma</c>, the observation model <c>log10 Q_i ~ Normal(log10 q_i, sigma^2)</c> implies the
/// discharge-space density
/// <c>log f(Q_i) = log phi((log10 Q_i - log10 q_i) / sigma) - log sigma - log(Q_i ln 10)</c>.
/// The scalar, pointwise, and component likelihoods must all carry the base-10 change-of-variables
/// term <c>-log(Q_i ln 10)</c>.
/// </para>
/// <para>
/// The oracle <c>rating-curve-likelihood-oracle.json</c> evaluates the log-space Gaussian terms,
/// the change-of-variables terms, and SciPy <c>lognorm(s = sigma ln 10, scale = q_i)</c> densities at
/// the generating parameters and at the independent optimum of every example case. The Numerics
/// base-10 <c>LogNormal</c> density supplies a second, in-process implementation of the same density.
/// </para>
/// </remarks>
[TestClass]
public class RatingCurveLikelihoodOracleTests
{
    /// <summary>Absolute tolerance for summed log likelihoods.</summary>
    private const double SumTolerance = 1e-8;

    /// <summary>Absolute tolerance for per-observation terms.</summary>
    private const double TermTolerance = 1e-10;

    /// <summary>One-segment example: the data log likelihood is the discharge-space density.</summary>
    [TestMethod]
    public void OneSegment_DataLogLikelihood_IsDischargeSpaceDensity() =>
        AssertDischargeSpaceLikelihood("one_segment");

    /// <summary>Two-segment example: the data log likelihood is the discharge-space density.</summary>
    [TestMethod]
    public void TwoSegment_DataLogLikelihood_IsDischargeSpaceDensity() =>
        AssertDischargeSpaceLikelihood("two_segment");

    /// <summary>Three-segment example: the data log likelihood is the discharge-space density.</summary>
    [TestMethod]
    public void ThreeSegment_DataLogLikelihood_IsDischargeSpaceDensity() =>
        AssertDischargeSpaceLikelihood("three_segment");

    /// <summary>
    /// Asserts the discharge-space likelihood contract for one example case.
    /// </summary>
    /// <param name="key">The example case key.</param>
    private static void AssertDischargeSpaceLikelihood(string key)
    {
        var example = RatingCurveExampleFixtures.LoadCase(key);
        BestFitRatingCurve model = RatingCurveExampleFixtures.CreateModel(example);
        using JsonDocument document = RatingCurveExampleFixtures.LoadDocument(RatingCurveExampleFixtures.LikelihoodOracleFileName);
        JsonElement oracle = document.RootElement.GetProperty("cases").GetProperty(key);
        JsonElement atTruth = oracle.GetProperty("at_truth");
        double[] truth = RatingCurveExampleFixtures.ReadDoubles(atTruth.GetProperty("parameters"));
        double[] dischargeTerms = RatingCurveExampleFixtures.ReadDoubles(atTruth.GetProperty("discharge_space_terms"));
        double[] logSpaceTerms = RatingCurveExampleFixtures.ReadDoubles(atTruth.GetProperty("log_space_terms"));
        double[] jacobianTerms = RatingCurveExampleFixtures.ReadDoubles(atTruth.GetProperty("jacobian_terms"));
        double dischargeSum = atTruth.GetProperty("discharge_space_log_likelihood").GetDouble();
        double logSpaceSum = atTruth.GetProperty("log_space_log_likelihood").GetDouble();
        double jacobianSum = atTruth.GetProperty("jacobian_sum").GetDouble();
        int observations = RatingCurveExampleFixtures.Observations;

        CollectionAssert.AreEqual(example.TrueParameters, truth, $"{key}: oracle parameters equal the generating parameters.");
        Assert.AreEqual(observations, dischargeTerms.Length, $"{key}: oracle term count.");

        // Independent in-process oracle: the Numerics base-10 lognormal density at every aligned pair.
        double sigma = truth[^1];
        double numericsSum = 0.0;
        for (int index = 0; index < observations; index++)
        {
            double predicted = model.Predict(truth, example.Stage[index]);
            var density = new NumericsLogNormal(Math.Log10(predicted), sigma) { Base = 10.0 };
            double term = density.LogPDF(example.Discharge[index]);
            Assert.AreEqual(dischargeTerms[index], term, TermTolerance, $"{key}: Numerics base-10 LogNormal term {index} versus the SciPy oracle.");
            Assert.AreEqual(logSpaceTerms[index] - jacobianTerms[index], dischargeTerms[index], TermTolerance, $"{key}: oracle identity at observation {index}.");
            numericsSum += term;
        }
        Assert.AreEqual(dischargeSum, numericsSum, SumTolerance, $"{key}: Numerics base-10 LogNormal sum versus the SciPy oracle.");

        // BestFit contract: scalar, pointwise, and component likelihoods are the discharge-space density.
        double scalar = model.DataLogLikelihood(truth);
        double[] pointwise = model.PointwiseDataLogLikelihood(truth);
        List<DataComponent> components = model.PointwiseDataLogLikelihoodComponents(truth);
        Assert.AreEqual(observations, pointwise.Length, $"{key}: pointwise term count.");
        Assert.AreEqual(observations, components.Count, $"{key}: component count.");
        Assert.AreEqual(
            dischargeSum,
            scalar,
            SumTolerance,
            $"{key}: DataLogLikelihood {scalar:G17} versus the discharge-space density {dischargeSum:G17}; "
            + $"the difference is {scalar - dischargeSum:G17}. The log-space value without the change-of-variables term "
            + $"is {logSpaceSum:G17} (Jacobian sum {jacobianSum:G17}).");
        for (int index = 0; index < observations; index++)
        {
            Assert.AreEqual(dischargeTerms[index], pointwise[index], TermTolerance, $"{key}: pointwise term {index}.");
            Assert.AreEqual(dischargeTerms[index], components[index].LogLikelihood, TermTolerance, $"{key}: component term {index}.");
        }
        Assert.AreEqual(scalar, pointwise.Sum(), SumTolerance, $"{key}: pointwise sum identity.");
        Assert.AreEqual(scalar, components.Sum(component => component.LogLikelihood), SumTolerance, $"{key}: component sum identity.");

        // The same contract at the independent optimum.
        JsonElement atOptimum = oracle.GetProperty("at_independent_mle");
        double[] optimum = RatingCurveExampleFixtures.ReadDoubles(atOptimum.GetProperty("parameters"));
        Assert.AreEqual(
            atOptimum.GetProperty("discharge_space_log_likelihood").GetDouble(),
            model.DataLogLikelihood(optimum),
            SumTolerance,
            $"{key}: DataLogLikelihood at the independent optimum.");
    }
}
