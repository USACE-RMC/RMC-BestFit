using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets.UnivariateData;
using System.Text.Json;

namespace RMC.BestFit.Verification.Univariate.Bulletin17CTests;

/// <summary>Verifies Bulletin 17C complete-data GMM covariance against an independent sandwich derivation.</summary>
/// <remarks>
/// The oracle uses central moments through order six and the finite-sample centered-moment Jacobian,
/// including the B17C second- and third-moment Bessel factors. A Python-standard-library artifact
/// freezes reference calculations at declared coordinates; current fits are compared with a separate
/// C# derivation that calls no production covariance, moment-conversion, matrix, or differentiation API.
/// The exact analytical sandwich is frozen cross-language. Current fitted samples are compared with
/// the same independent analytical construction at `1e-5` scaled plus `1e-10` absolute tolerance.
/// A production covariance that is materially changed by matrix conditioning remains a reported gap;
/// the oracle does not reproduce the production regularization policy.
/// </remarks>
[TestClass]
[DoNotParallelize]
public class B17CCovarianceTests
{
    private const string ArtifactFileName = "b17c-gmm-covariance-oracle.json";
    private const double ScaledTolerance = 1E-5;
    private const double AbsoluteTolerance = 1E-10;

    /// <summary>Verifies N=25 Exponential covariance in `(Xi, Alpha)` coordinates.</summary>
    /// <remarks>Uses parent `(10,50)` and the independently derived complete-data sandwich.</remarks>
    [TestMethod]
    public void Exponential_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateExponentialData(xi: 10d, alpha: 50d, n: 25);
        VerifyCovariance("Exponential_N25", B17CCovarianceFamily.Exponential, frame, UnivariateDistributionType.Exponential);
    }

    /// <summary>Verifies N=100 Exponential covariance in `(Xi, Alpha)` coordinates.</summary>
    /// <remarks>Uses parent `(10,50)` and the independently derived complete-data sandwich.</remarks>
    [TestMethod]
    public void Exponential_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateExponentialData(xi: 10d, alpha: 50d, n: 100);
        VerifyCovariance("Exponential_N100", B17CCovarianceFamily.Exponential, frame, UnivariateDistributionType.Exponential);
    }

    /// <summary>Verifies N=25 Gamma covariance in `(Theta scale, Kappa shape)` coordinates.</summary>
    /// <remarks>Uses parent `(5,2)` without converting scale to rate.</remarks>
    [TestMethod]
    public void Gamma_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateGammaData(n: 25);
        VerifyCovariance("Gamma_N25", B17CCovarianceFamily.Gamma, frame, UnivariateDistributionType.GammaDistribution);
    }

    /// <summary>Verifies N=100 Gamma covariance in `(Theta scale, Kappa shape)` coordinates.</summary>
    /// <remarks>Uses parent `(5,2)` without converting scale to rate.</remarks>
    [TestMethod]
    public void Gamma_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateGammaData(n: 100);
        VerifyCovariance("Gamma_N100", B17CCovarianceFamily.Gamma, frame, UnivariateDistributionType.GammaDistribution);
    }

    /// <summary>Verifies N=25 Normal covariance in `(Mu, Sigma)` coordinates.</summary>
    /// <remarks>Uses parent `(100,15)` and Normal central moments through order six.</remarks>
    [TestMethod]
    public void Normal_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateNormalData(n: 25);
        VerifyCovariance("Normal_N25", B17CCovarianceFamily.Normal, frame, UnivariateDistributionType.Normal);
    }

    /// <summary>Verifies N=100 Normal covariance in `(Mu, Sigma)` coordinates.</summary>
    /// <remarks>Uses parent `(100,15)` and Normal central moments through order six.</remarks>
    [TestMethod]
    public void Normal_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateNormalData(n: 100);
        VerifyCovariance("Normal_N100", B17CCovarianceFamily.Normal, frame, UnivariateDistributionType.Normal);
    }

    /// <summary>Verifies N=25 Pearson III covariance in `(Mu, Sigma, Gamma)` coordinates.</summary>
    /// <remarks>The finite-sample Jacobian retains `D[2,0] = -3 c3 mean((X-Mu)^2)`.</remarks>
    [TestMethod]
    public void PearsonTypeIII_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(n: 25);
        VerifyCovariance("PearsonTypeIII_N25", B17CCovarianceFamily.PearsonTypeIII, frame, UnivariateDistributionType.PearsonTypeIII);
    }

    /// <summary>Verifies N=100 Pearson III covariance in `(Mu, Sigma, Gamma)` coordinates.</summary>
    /// <remarks>The finite-sample Jacobian retains `D[2,0] = -3 c3 mean((X-Mu)^2)`.</remarks>
    [TestMethod]
    public void PearsonTypeIII_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GeneratePearsonTypeIIIData(n: 100);
        VerifyCovariance("PearsonTypeIII_N100", B17CCovarianceFamily.PearsonTypeIII, frame, UnivariateDistributionType.PearsonTypeIII);
    }

    /// <summary>Verifies N=25 Log-Normal covariance in base-10 `(Mu, Sigma)` coordinates.</summary>
    /// <remarks>The independent calculation transforms each observation with `log10` before forming moments.</remarks>
    [TestMethod]
    public void LogNormal_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateLogNormalData(n: 25);
        VerifyCovariance("LogNormal_N25", B17CCovarianceFamily.LogNormal, frame, UnivariateDistributionType.LogNormal);
    }

    /// <summary>Verifies N=100 Log-Normal covariance in base-10 `(Mu, Sigma)` coordinates.</summary>
    /// <remarks>The independent calculation transforms each observation with `log10` before forming moments.</remarks>
    [TestMethod]
    public void LogNormal_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateLogNormalData(n: 100);
        VerifyCovariance("LogNormal_N100", B17CCovarianceFamily.LogNormal, frame, UnivariateDistributionType.LogNormal);
    }

    /// <summary>Verifies N=25 Log-Pearson III covariance in base-10 `(Mu, Sigma, Gamma)` coordinates.</summary>
    /// <remarks>Skew sign is unchanged; the finite-sample third-moment Jacobian is evaluated in log10 space.</remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_N25()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(n: 25);
        VerifyCovariance("LogPearsonTypeIII_N25", B17CCovarianceFamily.LogPearsonTypeIII, frame, UnivariateDistributionType.LogPearsonTypeIII);
    }

    /// <summary>Verifies N=100 Log-Pearson III covariance in base-10 `(Mu, Sigma, Gamma)` coordinates.</summary>
    /// <remarks>Skew sign is unchanged; the finite-sample third-moment Jacobian is evaluated in log10 space.</remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_N100()
    {
        var (frame, _) = SyntheticUnivariateData.GenerateLogPearsonTypeIIIData(n: 100);
        VerifyCovariance("LogPearsonTypeIII_N100", B17CCovarianceFamily.LogPearsonTypeIII, frame, UnivariateDistributionType.LogPearsonTypeIII);
    }

    /// <summary>Verifies the Bulletin 17C Example 1 LP3 covariance in base-10 moment coordinates.</summary>
    /// <remarks>
    /// The frozen reference uses the published Example 1 parameter vector `(3.328623159,
    /// 0.140287994,0.396626124)` at N=68. The current fitted covariance is evaluated at the current
    /// fitted vector and actual complete log10 sample through the same independent derivation.
    /// </remarks>
    [TestMethod]
    public void LogPearsonTypeIII_Covariance_Example1()
    {
        var (frame, _) = Bulletin17CData.GetExample1();
        VerifyCovariance(
            "LogPearsonTypeIII_Bulletin17CExample1_N68",
            B17CCovarianceFamily.LogPearsonTypeIII,
            frame,
            UnivariateDistributionType.LogPearsonTypeIII);
    }

    /// <summary>Fits one family and compares its current GMM covariance with the independent derivation.</summary>
    /// <param name="artifactCase">Frozen reference case identifier.</param>
    /// <param name="family">Independent family and coordinate system.</param>
    /// <param name="frame">Complete exact-data fixture.</param>
    /// <param name="distributionType">Matching Bulletin 17C family.</param>
    /// <remarks>
    /// The artifact first checks the independent evaluator at fixed coordinates. The scientific
    /// comparison then evaluates both covariances at the current fitted vector; estimator success is
    /// only a prerequisite. Entry tolerances scale by the larger expected entry or diagonal geometric
    /// mean so near-zero off-diagonal terms remain meaningful.
    /// </remarks>
    private static void VerifyCovariance(
        string artifactCase,
        B17CCovarianceFamily family,
        DataFrame frame,
        UnivariateDistributionType distributionType)
    {
        AssertFrozenArtifactCase(artifactCase, family);
        var model = new Bulletin17CDistribution(frame, distributionType);
        var gmm = new GeneralizedMethodOfMoments(model);
        gmm.Estimate();
        Assert.IsTrue(gmm.IsEstimated, $"GMM estimation failed for {family}.");

        double[] observations = frame.ExactSeries.Select(observation => observation.Value).ToArray();
        double[,] expected = B17CIndependentCovarianceOracle.Evaluate(
            family,
            gmm.BestParameterSet.Values,
            observations);
        var actual = gmm.GetCovarianceMatrix();
        for (int i = 0; i < expected.GetLength(0); i++)
        {
            for (int j = 0; j < expected.GetLength(1); j++)
            {
                double diagonalScale = Math.Sqrt(Math.Abs(expected[i, i] * expected[j, j]));
                double tolerance = ScaledTolerance * Math.Max(Math.Abs(expected[i, j]), diagonalScale) + AbsoluteTolerance;
                Assert.AreEqual(expected[i, j], actual[i, j], tolerance,
                    $"Independent covariance[{i},{j}] mismatch for {family}. " +
                    $"Status={gmm.CovarianceStatus}; Diagnostic={gmm.CovarianceDiagnostic}");
            }
        }
    }

    /// <summary>Checks one frozen Python case against the separate C# independent evaluator.</summary>
    /// <param name="caseId">Frozen case identifier.</param>
    /// <param name="family">Expected family and coordinate system.</param>
    /// <remarks>
    /// Frozen values use Python 3.12.13 standard-library matrix arithmetic and no production code.
    /// Cross-language agreement is deterministic at `1e-12` scaled absolute tolerance.
    /// </remarks>
    private static void AssertFrozenArtifactCase(string caseId, B17CCovarianceFamily family)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        JsonElement selected = document.RootElement.GetProperty("cases").EnumerateArray()
            .Single(element => element.GetProperty("id").GetString() == caseId);
        Assert.AreEqual(family.ToString(), selected.GetProperty("family").GetString());
        int sampleSize = selected.GetProperty("sampleSize").GetInt32();
        double[] parameters = selected.GetProperty("parameters").EnumerateArray()
            .Select(element => element.GetDouble()).ToArray();
        double[,] expected = B17CIndependentCovarianceOracle.EvaluateAtMomentSolution(family, parameters, sampleSize);
        JsonElement.ArrayEnumerator rows = selected.GetProperty("covariance").EnumerateArray();
        int rowIndex = 0;
        foreach (JsonElement row in rows)
        {
            int columnIndex = 0;
            foreach (JsonElement value in row.EnumerateArray())
            {
                double frozen = value.GetDouble();
                double tolerance = 1E-12 * Math.Max(1d, Math.Abs(frozen));
                Assert.AreEqual(frozen, expected[rowIndex, columnIndex], tolerance,
                    $"Frozen independent covariance[{rowIndex},{columnIndex}] drifted for {caseId}.");
                columnIndex++;
            }
            rowIndex++;
        }
    }
}
