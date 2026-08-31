using System.Text.Json;
using Numerics.Mathematics.Optimization;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Bivariate;

/// <summary>
/// Verifies BestFit copula estimation by maximum pseudo-likelihood and by inference from margins for
/// the Ali-Mikhail-Haq, Clayton, Frank, Gumbel, Joe, Gaussian, and Student-t families against the independent
/// SciPy optimum committed in <c>copula-estimation-oracle.json</c>.
/// </summary>
/// <remarks>
/// <para>
/// The artifact transcribes the twelve fixtures of <see cref="BivariateDistributionMLETests"/>, fits
/// every family with independently implemented closed-form copula densities (each self-checked against
/// the numerical mixed partial of its distribution function), and records the independent optimum, the
/// maximized log likelihood, the closed-form Normal marginal estimates used for inference from margins,
/// and the historical R <c>copula</c> target embedded in the test source. Each cell rebuilds the fixture,
/// fits with the production Differential Evolution path at default optimizer tolerances, and requires
/// same-point likelihood parity at the independent optimum, parameterization agreement with the fitted
/// dependence coordinate, and production optimality at the untouched Differential Evolution objective
/// tolerance; the historical R value is retained as a provenance check at its original tolerance.
/// </para>
/// <para>
/// Maximum pseudo-likelihood uses the default Weibull plotting-position complements; inference from
/// margins sets both Normal marginals to the closed-form maximum-likelihood mean and standard deviation
/// so that the copula likelihood is evaluated on exactly the pseudo-observations of the oracle.
/// </para>
/// </remarks>
[TestClass]
public class CopulaEstimationOracleTests
{
    /// <summary>The committed artifact copied to the test output.</summary>
    private const string ArtifactFileName = "copula-estimation-oracle.json";

    /// <summary>Absolute parameterization-crosswalk tolerance for independent and historical targets.</summary>
    private const double ThetaCrosswalkTolerance = 1e-3;

    /// <summary>Absolute tolerance for log likelihoods.</summary>
    private const double LikelihoodTolerance = 1e-8;

    /// <summary>Ali-Mikhail-Haq copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void AliMikhailHaq_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_AMH_MPL");

    /// <summary>Ali-Mikhail-Haq copula by inference from margins.</summary>
    [TestMethod]
    public void AliMikhailHaq_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_AMH_IFM");

    /// <summary>Clayton copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void Clayton_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_Clayton_MPL");

    /// <summary>Clayton copula by inference from margins.</summary>
    [TestMethod]
    public void Clayton_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_Clayton_IFM");

    /// <summary>Frank copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void Frank_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_Frank_MPL");

    /// <summary>Frank copula by inference from margins.</summary>
    [TestMethod]
    public void Frank_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_Frank_IFM");

    /// <summary>Gumbel copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void Gumbel_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_Gumbel_MPL");

    /// <summary>Gumbel copula by inference from margins.</summary>
    [TestMethod]
    public void Gumbel_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_Gumbel_IFM");

    /// <summary>Joe copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void Joe_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_Joe_MPL");

    /// <summary>Joe copula by inference from margins.</summary>
    [TestMethod]
    public void Joe_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_Joe_IFM");

    /// <summary>Gaussian copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void Normal_PseudoLikelihood_MatchesIndependentOptimum() => AssertFixture("Test_Normal_MPL");

    /// <summary>Gaussian copula by inference from margins.</summary>
    [TestMethod]
    public void Normal_InferenceFromMargins_MatchesIndependentOptimum() => AssertFixture("Test_Normal_IFM");

    /// <summary>Student-t copula by maximum pseudo-likelihood.</summary>
    [TestMethod]
    public void StudentT_PseudoLikelihood_MatchesIndependentOptimum() => AssertStudentTFixture("StudentT_MPL");

    /// <summary>Student-t copula by inference from margins.</summary>
    [TestMethod]
    public void StudentT_InferenceFromMargins_MatchesIndependentOptimum() => AssertStudentTFixture("StudentT_IFM");

    /// <summary>
    /// Rebuilds one fixture, fits it with production Differential Evolution at default optimizer
    /// tolerances, and compares with the oracle.
    /// </summary>
    /// <param name="testMethod">The fixture key (the historical test method name).</param>
    private static void AssertFixture(string testMethod)
    {
        JsonElement fixture = LoadFixture(testMethod);
        string family = fixture.GetProperty("family").GetString()!;
        string method = fixture.GetProperty("method").GetString()!;
        double[] dataX = ReadDoubles(fixture.GetProperty("data_x"));
        double[] dataY = ReadDoubles(fixture.GetProperty("data_y"));
        double independentTheta = fixture.GetProperty("independent_theta").GetDouble();
        double independentLogLikelihood = fixture.GetProperty("independent_maximum_log_likelihood").GetDouble();
        double historicalTarget = fixture.GetProperty("r_copula_target").GetDouble();
        var copulaType = Enum.Parse<CopulaType>(family);
        CopulaEstimationMethod estimationMethod = method == "MPL"
            ? CopulaEstimationMethod.PseudoLikelihood
            : CopulaEstimationMethod.InferenceFromMargins;

        var dfX = new DataFrame { ExactSeries = new ExactSeries(dataX) };
        var dfY = new DataFrame { ExactSeries = new ExactSeries(dataY) };
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        if (estimationMethod == CopulaEstimationMethod.InferenceFromMargins)
        {
            JsonElement marginals = fixture.GetProperty("marginal_mle");
            distX.SetParameterValues(new[] { marginals.GetProperty("mu_x").GetDouble(), marginals.GetProperty("sigma_x").GetDouble() });
            distY.SetParameterValues(new[] { marginals.GetProperty("mu_y").GetDouble(), marginals.GetProperty("sigma_y").GetDouble() });
        }
        else
        {
            distX.SetParameterValues(ClosedFormNormal(dataX));
            distY.SetParameterValues(ClosedFormNormal(dataY));
        }

        var bivariate = new BivariateDistribution(distX, distY, copulaType)
        {
            CopulaEstimationMethod = estimationMethod,
        };
        var mle = new MaximumLikelihood(bivariate, OptimizationMethod.DifferentialEvolution);
        mle.Estimate();
        Assert.IsTrue(mle.IsEstimated, $"{testMethod}: copula estimation did not complete.");
        double theta = mle.BestParameterSet.Values[0];

        Assert.AreEqual(
            independentLogLikelihood,
            bivariate.DataLogLikelihood([independentTheta]),
            LikelihoodTolerance,
            $"{testMethod}: {family} {method} log likelihood at the independent optimum.");
        Assert.AreEqual(
            independentTheta,
            theta,
            ThetaCrosswalkTolerance,
            $"{testMethod}: {family} {method} dependence-parameter crosswalk versus the independent optimum.");
        double optimizerObjectiveTolerance =
            mle.Optimizer.AbsoluteTolerance +
            mle.Optimizer.RelativeTolerance * Math.Abs(independentLogLikelihood);
        Assert.IsTrue(
            mle.MaximumLogLikelihood >= independentLogLikelihood - optimizerObjectiveTolerance,
            $"{testMethod}: production maximum {mle.MaximumLogLikelihood:G17} must reach the independent optimum " +
            $"{independentLogLikelihood:G17} within the untouched optimizer objective tolerance " +
            $"{optimizerObjectiveTolerance:G17}.");
        Assert.AreEqual(
            historicalTarget,
            theta,
            ThetaCrosswalkTolerance,
            $"{testMethod}: historical R copula provenance check.");
    }

    /// <summary>
    /// Rebuilds one independently generated Student-t fixture, fits both continuous copula
    /// coordinates with the default production MLE, and compares with the SciPy optimum.
    /// </summary>
    /// <param name="testMethod">The Student-t artifact fixture key.</param>
    private static void AssertStudentTFixture(string testMethod)
    {
        JsonElement fixture = LoadFixture(testMethod);
        string method = fixture.GetProperty("method").GetString()!;
        double[] dataX = ReadDoubles(fixture.GetProperty("data_x"));
        double[] dataY = ReadDoubles(fixture.GetProperty("data_y"));
        double[] independentParameters = ReadDoubles(fixture.GetProperty("independent_parameters"));
        double[] parameterTolerances = ReadDoubles(fixture.GetProperty("cross_solver_parameter_tolerances"));
        double independentLogLikelihood = fixture.GetProperty("independent_maximum_log_likelihood").GetDouble();
        double likelihoodTolerance = fixture.GetProperty("cross_solver_log_likelihood_tolerance").GetDouble();
        CopulaEstimationMethod estimationMethod = method == "MPL"
            ? CopulaEstimationMethod.PseudoLikelihood
            : CopulaEstimationMethod.InferenceFromMargins;

        var dfX = new DataFrame { ExactSeries = new ExactSeries(dataX) };
        var dfY = new DataFrame { ExactSeries = new ExactSeries(dataY) };
        dfX.CalculatePlottingPositions();
        dfY.CalculatePlottingPositions();
        var distX = new UnivariateDistribution(dfX, Numerics.Distributions.UnivariateDistributionType.Normal);
        var distY = new UnivariateDistribution(dfY, Numerics.Distributions.UnivariateDistributionType.Normal);
        if (estimationMethod == CopulaEstimationMethod.InferenceFromMargins)
        {
            JsonElement marginals = fixture.GetProperty("marginal_mle");
            distX.SetParameterValues([marginals.GetProperty("mu_x").GetDouble(), marginals.GetProperty("sigma_x").GetDouble()]);
            distY.SetParameterValues([marginals.GetProperty("mu_y").GetDouble(), marginals.GetProperty("sigma_y").GetDouble()]);
        }
        else
        {
            distX.SetParameterValues(ClosedFormNormal(dataX));
            distY.SetParameterValues(ClosedFormNormal(dataY));
        }

        var bivariate = new BivariateDistribution(distX, distY, CopulaType.StudentT)
        {
            CopulaEstimationMethod = estimationMethod,
        };
        Assert.AreEqual(
            independentLogLikelihood,
            bivariate.DataLogLikelihood(independentParameters),
            LikelihoodTolerance,
            $"{testMethod}: production likelihood at the independent [rho, nu] optimum.");

        var mle = new MaximumLikelihood(bivariate, OptimizationMethod.DifferentialEvolution);
        Assert.IsTrue(mle.Estimate(), $"{testMethod}: Student-t MLE failed with status {mle.Status}.");
        Assert.IsTrue(mle.IsEstimated, $"{testMethod}: Student-t MLE did not publish an estimate.");
        Assert.AreEqual(independentParameters.Length, mle.BestParameterSet.Values.Length,
            $"{testMethod}: fitted coordinate order must be [rho, nu].");
        for (int index = 0; index < independentParameters.Length; index++)
        {
            Assert.AreEqual(
                independentParameters[index],
                mle.BestParameterSet.Values[index],
                parameterTolerances[index],
                $"{testMethod}: coordinate {index} ([rho, nu]) versus independent optimum.");
        }
        Assert.IsTrue(
            mle.MaximumLogLikelihood >= independentLogLikelihood - likelihoodTolerance,
            $"{testMethod}: production maximum {mle.MaximumLogLikelihood:G17} must reach the independent optimum " +
            $"{independentLogLikelihood:G17} within {likelihoodTolerance:G17}.");
    }

    /// <summary>
    /// Closed-form Normal maximum-likelihood mean and (1/n) standard deviation.
    /// </summary>
    /// <param name="values">The sample.</param>
    /// <returns>The parameter vector [mean, standard deviation].</returns>
    private static double[] ClosedFormNormal(double[] values)
    {
        double mean = values.Average();
        double sigma = Math.Sqrt(values.Sum(value => (value - mean) * (value - mean)) / values.Length);
        return [mean, sigma];
    }

    /// <summary>
    /// Loads one fixture by its historical test method name.
    /// </summary>
    /// <param name="testMethod">The fixture key.</param>
    /// <returns>A detached fixture element.</returns>
    private static JsonElement LoadFixture(string testMethod)
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        foreach (JsonElement fixture in document.RootElement.GetProperty("fixtures").EnumerateArray())
        {
            if (fixture.GetProperty("test_method").GetString() == testMethod)
            {
                return fixture.Clone();
            }
        }
        throw new AssertFailedException($"Fixture {testMethod} is not present in {ArtifactFileName}.");
    }

    /// <summary>
    /// Reads a JSON array of numbers.
    /// </summary>
    /// <param name="element">The array element.</param>
    /// <returns>The values.</returns>
    private static double[] ReadDoubles(JsonElement element) =>
        element.EnumerateArray().Select(item => item.GetDouble()).ToArray();
}
