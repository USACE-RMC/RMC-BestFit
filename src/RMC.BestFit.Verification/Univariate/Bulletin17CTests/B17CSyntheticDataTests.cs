using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using RMC.BestFit.Verification.Recovery;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>
/// Verifies generated-parent recovery for the six families supported by the Bulletin 17C GMM path.
/// </summary>
/// <remarks>
/// Every method uses exactly 1,000 complete scalar observations and seed 12345. Parameter coordinates
/// use Numerics method-of-moments covariance at the fitted values. Pearson III and Log-Pearson III use
/// the predeclared Q(0.99) response variance for their weak skew direction. The complete crosswalk is
/// recorded in <c>docs/verification/bulletin-17c.md</c>.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CSyntheticDataTests
{
    private const int GeneratorSeed = 12345;
    private const double IdentifiedResponseProbability = 0.99d;

    /// <summary>Verifies Exponential GMM recovery in `(Xi, Alpha)` coordinates.</summary>
    /// <remarks>
    /// Parent `(0,50)` is generated in natural response space. Both coordinates must have absolute
    /// standardized error no greater than 1.96 using fitted method-of-moments parameter covariance.
    /// </remarks>
    [TestMethod]
    public void Exponential_GmmRecoversGeneratingParent()
    {
        var (frame, parents) = SyntheticUnivariateData.GenerateExponentialData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        VerifyParameterRecovery(
            frame,
            new Exponential(parents[0], parents[1]),
            UnivariateDistributionType.Exponential,
            ["Xi", "Alpha"]);
    }

    /// <summary>Verifies Gamma GMM recovery in `(Theta scale, Kappa shape)` coordinates.</summary>
    /// <remarks>
    /// Parent `(5,2)` uses the Numerics scale/shape order; no shape/rate conversion is applied.
    /// Both coordinates use the fitted method-of-moments covariance and the 1.96 rule.
    /// </remarks>
    [TestMethod]
    public void Gamma_GmmRecoversGeneratingParent()
    {
        var (frame, parents) = SyntheticUnivariateData.GenerateGammaData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        VerifyParameterRecovery(
            frame,
            new GammaDistribution(parents[0], parents[1]),
            UnivariateDistributionType.GammaDistribution,
            ["Theta", "Kappa"]);
    }

    /// <summary>Verifies Normal GMM recovery in `(Mu, Sigma)` coordinates.</summary>
    /// <remarks>
    /// Parent `(100,15)` is generated in natural response space. Both coordinates use the fitted
    /// method-of-moments covariance and the common absolute standardized-error rule.
    /// </remarks>
    [TestMethod]
    public void Normal_GmmRecoversGeneratingParent()
    {
        var (frame, parents) = SyntheticUnivariateData.GenerateNormalData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        VerifyParameterRecovery(
            frame,
            new Normal(parents[0], parents[1]),
            UnivariateDistributionType.Normal,
            ["Mu", "Sigma"]);
    }

    /// <summary>Verifies Pearson Type III GMM recovery with a Q(0.99) identified skew response.</summary>
    /// <remarks>
    /// Parent `(Mu,Sigma,Gamma)=(100,20,0.5)` is in natural response space and positive Gamma means
    /// positive/right skew. Mu and Sigma use parameter covariance; the weak skew direction uses the
    /// generating Q(0.99) inside the fitted Numerics method-of-moments quantile-variance band.
    /// </remarks>
    [TestMethod]
    public void PearsonTypeIII_GmmRecoversGeneratingParentAndQ99()
    {
        var (frame, parents) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        var parent = new PearsonTypeIII(parents[0], parents[1], parents[2]);
        UnivariateDistributionBase fitted = VerifyParameterRecovery(
            frame,
            parent,
            UnivariateDistributionType.PearsonTypeIII,
            ["Mu", "Sigma"]);
        AssertIdentifiedQ99(parent, fitted, "Pearson Type III Q(0.99)");
    }

    /// <summary>Verifies base-10 Log-Normal GMM recovery in `(Mu, Sigma)` log coordinates.</summary>
    /// <remarks>
    /// Parent `(3,0.5)` describes `log10(X)`, not natural-log or real-space moments. Both fitted
    /// base-10 coordinates use Numerics method-of-moments parameter covariance and the 1.96 rule.
    /// </remarks>
    [TestMethod]
    public void LogNormal_GmmRecoversGeneratingLog10Parent()
    {
        var (frame, parents) = SyntheticUnivariateData.GenerateLogNormalData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        VerifyParameterRecovery(
            frame,
            new LogNormal(parents[0], parents[1]),
            UnivariateDistributionType.LogNormal,
            ["log10 Mu", "log10 Sigma"]);
    }

    /// <summary>Verifies base-10 Log-Pearson III GMM recovery with a real-space Q(0.99) check.</summary>
    /// <remarks>
    /// Parent `(Mu,Sigma,Gamma)=(3,0.5,0.2)` describes `log10(X)` and preserves the positive/right
    /// skew sign. Log-space Mu and Sigma use parameter covariance; the weak skew direction uses the
    /// real-space generating Q(0.99) inside the fitted method-of-moments quantile-variance band.
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_GmmRecoversGeneratingLog10ParentAndQ99()
    {
        var (frame, parents) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(
            n: RecoveryDesign.SampleSize,
            prngSeed: GeneratorSeed);
        var parent = new LogPearsonTypeIII(parents[0], parents[1], parents[2]);
        UnivariateDistributionBase fitted = VerifyParameterRecovery(
            frame,
            parent,
            UnivariateDistributionType.LogPearsonTypeIII,
            ["log10 Mu", "log10 Sigma"]);
        AssertIdentifiedQ99(parent, fitted, "Log-Pearson Type III Q(0.99)");
    }

    /// <summary>Fits one Bulletin 17C family and applies the common parameter-coordinate rule.</summary>
    /// <param name="frame">Exactly 1,000 generated complete scalar observations.</param>
    /// <param name="parentDistribution">The declared generating distribution in fitted coordinates.</param>
    /// <param name="distributionType">The matching Bulletin 17C family.</param>
    /// <param name="coordinateNames">Names of the leading coordinates checked by parameter covariance.</param>
    /// <returns>A clone configured with the recovered parameter vector.</returns>
    /// <remarks>
    /// Estimator success is a prerequisite, not the scientific oracle. The assertion uses only the
    /// predeclared fitted method-of-moments covariance at N=1000 and the shared 1.96 cutoff.
    /// </remarks>
    private static UnivariateDistributionBase VerifyParameterRecovery(
        DataFrame frame,
        UnivariateDistributionBase parentDistribution,
        UnivariateDistributionType distributionType,
        IReadOnlyList<string> coordinateNames)
    {
        var design = RecoveryDesign.ScalarObservations("complete Bulletin 17C family observation");
        Assert.AreEqual(RecoverySampleUnit.ScalarObservation, design.Unit);
        Assert.AreEqual(RecoveryDesign.SampleSize, frame.ExactSeries.Count, design.Description);

        var model = new Bulletin17CDistribution(frame, distributionType);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();

        Assert.IsTrue(gmm.IsEstimated, $"Bulletin 17C GMM estimation failed for {parentDistribution.DisplayName}.");
        double[] estimates = gmm.BestParameterSet.Values;
        double[] parents = parentDistribution.GetParameters;
        Assert.IsTrue(coordinateNames.Count <= estimates.Length, "The declared recovery coordinates exceed the fitted vector.");

        UnivariateDistributionBase fittedDistribution = parentDistribution.Clone();
        fittedDistribution.SetParameters(estimates);
        Assert.IsInstanceOfType<IStandardError>(fittedDistribution, "The fitted family must expose method-of-moments uncertainty.");
        double[,] covariance = ((IStandardError)fittedDistribution).ParameterCovariance(
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MethodOfMoments);
        for (int index = 0; index < coordinateNames.Count; index++)
        {
            RecoveryAcceptance.AssertFrequentistStandardizedError(
                coordinateNames[index],
                estimates[index],
                parents[index],
                Math.Sqrt(covariance[index, index]));
        }

        return fittedDistribution;
    }

    /// <summary>Applies the predeclared method-of-moments Q(0.99) identified-response rule.</summary>
    /// <param name="parent">The declared generating distribution.</param>
    /// <param name="fitted">The distribution configured with recovered parameters.</param>
    /// <param name="label">The scientific response label.</param>
    /// <remarks>
    /// The response band is a central Normal approximation from the fitted Numerics quantile
    /// variance at N=1000. It replaces a raw skew-coordinate assertion and has no secondary 5%
    /// criterion because the skew direction is predeclared as weakly identified.
    /// </remarks>
    private static void AssertIdentifiedQ99(
        UnivariateDistributionBase parent,
        UnivariateDistributionBase fitted,
        string label)
    {
        var standardErrorSource = (IStandardError)fitted;
        double variance = standardErrorSource.QuantileVariance(
            IdentifiedResponseProbability,
            RecoveryDesign.SampleSize,
            ParameterEstimationMethod.MethodOfMoments);
        Assert.IsTrue(double.IsFinite(variance) && variance > 0d, $"{label} variance must be finite and positive.");
        double estimate = fitted.InverseCDF(IdentifiedResponseProbability);
        double parentResponse = parent.InverseCDF(IdentifiedResponseProbability);
        double halfWidth = RecoveryAcceptance.NinetyFivePercentStandardNormalCutoff * Math.Sqrt(variance);
        RecoveryAcceptance.AssertIdentifiedResponseGrid(label, parentResponse, estimate - halfWidth, estimate + halfWidth);
    }
}
