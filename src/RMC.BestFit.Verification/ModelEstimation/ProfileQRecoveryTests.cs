using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;
using RMC.BestFit.Verification.Datasets.UnivariateData;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Verifies true Profile-Q support and identified Q(0.99) recovery for generated LP3 data.
/// </summary>
/// <remarks>
/// The recovery design is 1,000 seeded scalar observations in log10-space, generated with parent
/// (mu=3, sigma=0.5, gamma=-0.5). The profile-Q interval itself is the predeclared uncertainty
/// source, while Q(0.99) is the predeclared identified recovery response; a conditional secondary
/// 5% point rule is not used for these correlated coordinates.
/// </remarks>
[TestClass]
public class ProfileQRecoveryTests
{
#region Test Setup

    /// <summary>
    /// True LP3 parameters for all tests: μ=3.0, σ=0.5, γ=-0.5 in log10-space.
    /// </summary>
    private const double TrueMu = 3.0;
    private const double TrueSigma = 0.5;
    private const double TrueGamma = -0.5;
    private const int SampleSize = RecoveryDesign.SampleSize;
    private const int Seed = 12345;
    private const double IdentifiedResponseProbability = 0.99d;

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data and runs GMM estimation.
    /// Returns the estimated GMM object ready for profile Q analysis.
    /// </summary>
    /// <param name="n">Sample size. Default = the mandated recovery size of 1000.</param>
    /// <param name="seed">PRNG seed for reproducibility. Default = 12345.</param>
    /// <returns>A tuple of the estimated GMM and the Bulletin17CDistribution model.</returns>
    /// <remarks>
    /// The fit uses 1,000 seeded scalar log10 observations from parent (μ=3, σ=0.5, γ=-0.5).
    /// Estimation success is asserted before a true Profile-Q interval is requested so a failed
    /// initial fit cannot be mistaken for a profile result. The identified response is Q(0.99);
    /// its 95 percent delta-method band uses Numerics' Log-Pearson Type III method-of-moments
    /// quantile variance at recovered parameters and is checked by the caller. The conditional
    /// secondary 5 percent point rule does not apply to this response cell.
    /// </remarks>
    private static (GeneralizedMethodOfMoments GMM, Bulletin17CDistribution Model) CreateEstimatedLP3GMM(
        int n = SampleSize, int seed = Seed)
    {
        var (df, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: n, prngSeed: seed);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        var gmm = new GeneralizedMethodOfMoments(model);
        Assert.IsTrue(gmm.Estimate(), "The initial LP3 GMM estimate required for true Profile-Q failed.");

        return (gmm, model);
    }

    /// <summary>
    /// Creates a Bulletin17CDistribution with LP3 synthetic data but does NOT estimate.
    /// Used for testing pre-estimation error handling.
    /// </summary>
    private static GeneralizedMethodOfMoments CreateUnestimatedLP3GMM()
    {
        var (df, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(
            mu: TrueMu, sigma: TrueSigma, gamma: TrueGamma, n: SampleSize, prngSeed: Seed);

        var model = new Bulletin17CDistribution(df, UnivariateDistributionType.LogPearsonTypeIII);
        return new GeneralizedMethodOfMoments(model);
    }

    #endregion

    /// <summary>
    /// Verifies finite true Profile-Q intervals and the identified LP3 response recovery band.
    /// </summary>
    /// <remarks>
    /// Sample unit: scalar log10 observation; N=1000; seed=12345; parent=(mu=3, sigma=0.5,
    /// gamma=-0.5); fitted coordinates=(mu, sigma, gamma). True Profile-Q supplies 95 percent
    /// coordinate intervals, which must all be finite and ordered. The correlated-coordinate
    /// recovery claim is instead made for the predeclared Q(0.99) response: its 95 percent
    /// delta-method band uses <see cref="LogPearsonTypeIII.QuantileVariance(double, int, ParameterEstimationMethod)"/>
    /// at recovered parameters with the method-of-moments variance convention.
    /// The conditional secondary 5 percent point rule does not apply to the response cell.
    /// </remarks>
    [TestMethod]
    public void TrueProfile_LP3Q99_RecoversGeneratingResponse()
    {
        var (gmm, model) = CreateEstimatedLP3GMM();

        var cis = gmm.ProfileConfidenceIntervals(alpha: 0.05, trueProfile: true);

        string[] paramNames = { "μ", "σ", "γ" };
        for (int i = 0; i < 3; i++)
        {
            Assert.IsTrue(double.IsFinite(cis[i, 0]) && double.IsFinite(cis[i, 1]) && cis[i, 0] <= cis[i, 1],
                $"True Profile-Q must produce a finite ordered 95% interval for {paramNames[i]}.");
        }

        model.SetParameterValues(gmm.BestParameterSet.Values);
        var fittedDistribution = (LogPearsonTypeIII)model.Distribution;
        double responseEstimate = fittedDistribution.InverseCDF(IdentifiedResponseProbability);
        double responseVariance = fittedDistribution.QuantileVariance(
            IdentifiedResponseProbability,
            SampleSize,
            ParameterEstimationMethod.MethodOfMoments);
        Assert.IsTrue(double.IsFinite(responseVariance) && responseVariance > 0d,
            "The predeclared LP3 Q(0.99) delta-method variance must be finite and positive.");

        var parentDistribution = new LogPearsonTypeIII(TrueMu, TrueSigma, TrueGamma);
        double parentResponse = parentDistribution.InverseCDF(IdentifiedResponseProbability);
        double responseHalfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * Math.Sqrt(responseVariance);
        RecoveryAcceptance.AssertIdentifiedResponseGrid(
            "LP3 Q(0.99)", parentResponse, responseEstimate - responseHalfWidth, responseEstimate + responseHalfWidth);
    }
}
