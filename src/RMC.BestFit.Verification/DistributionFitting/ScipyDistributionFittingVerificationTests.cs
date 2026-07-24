using System.Text.Json;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.DistributionFitting;

/// <summary>
/// Verifies distribution functions and maximum-likelihood fits against committed SciPy 1.17.1 oracles.
/// </summary>
/// <remarks>
/// Each public test is intentionally family-specific so it can be executed alone through the guarded
/// verification runner. Python is used only to generate the committed JSON artifact.
/// </remarks>
[TestClass]
public class ScipyDistributionFittingVerificationTests
{
    /// <summary>
    /// Verifies the Normal family against SciPy.
    /// </summary>
    [TestMethod]
    public void Normal_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Normal", UnivariateDistributionType.Normal, OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the base-10 Log-Normal family against SciPy's natural-log parameterization.
    /// </summary>
    [TestMethod]
    public void LogNormal_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("LogNormal", UnivariateDistributionType.LogNormal, OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the natural-space Ln-Normal family against SciPy's log-space parameterization.
    /// </summary>
    [TestMethod]
    public void LnNormal_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("LnNormal", UnivariateDistributionType.LnNormal, OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the shifted Exponential family against SciPy.
    /// </summary>
    [TestMethod]
    public void Exponential_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Exponential", UnivariateDistributionType.Exponential, OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies the Gamma family and its scale-before-shape crosswalk against SciPy.
    /// </summary>
    [TestMethod]
    public void Gamma_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Gamma", UnivariateDistributionType.GammaDistribution, OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies the generalized extreme-value shape convention against SciPy.
    /// </summary>
    [TestMethod]
    public void GeneralizedExtremeValue_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily(
            "GeneralizedExtremeValue",
            UnivariateDistributionType.GeneralizedExtremeValue,
            OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies the generalized-Pareto shape-sign crosswalk against SciPy.
    /// </summary>
    [TestMethod]
    public void GeneralizedPareto_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily(
            "GeneralizedPareto",
            UnivariateDistributionType.GeneralizedPareto,
            OptimizationMethod.DifferentialEvolution);
    }

    /// <summary>
    /// Verifies the right-skewed Gumbel family against SciPy.
    /// </summary>
    [TestMethod]
    public void Gumbel_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Gumbel", UnivariateDistributionType.Gumbel, OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the Logistic family against SciPy.
    /// </summary>
    [TestMethod]
    public void Logistic_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Logistic", UnivariateDistributionType.Logistic, OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the Weibull scale-before-shape crosswalk against SciPy.
    /// </summary>
    [TestMethod]
    public void Weibull_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("Weibull", UnivariateDistributionType.Weibull, OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies the Pearson Type III moment parameterization against SciPy.
    /// </summary>
    [TestMethod]
    public void PearsonTypeIII_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("PearsonTypeIII", UnivariateDistributionType.PearsonTypeIII, OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies the base-10 Log-Pearson Type III parameterization and Jacobian against SciPy.
    /// </summary>
    [TestMethod]
    public void LogPearsonTypeIII_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily(
            "LogPearsonTypeIII",
            UnivariateDistributionType.LogPearsonTypeIII,
            OptimizationMethod.BFGS);
    }

    /// <summary>
    /// Verifies the Kappa Four shape ordering and distribution functions against SciPy.
    /// </summary>
    [TestMethod]
    public void KappaFour_MleAndDistributionFunctionsMatchScipy()
    {
        VerifyFamily("KappaFour", UnivariateDistributionType.KappaFour, OptimizationMethod.NelderMead);
    }

    /// <summary>
    /// Verifies one family against its committed SciPy fit and distribution-function values.
    /// </summary>
    /// <param name="familyName">Artifact key for the family.</param>
    /// <param name="distributionType">BestFit distribution type.</param>
    /// <param name="optimizationMethod">Deterministic local optimizer used for the C# fit.</param>
    private static void VerifyFamily(
        string familyName,
        UnivariateDistributionType distributionType,
        OptimizationMethod optimizationMethod)
    {
        JsonElement family = LoadFamily(familyName);
        double[] data = ReadArray(family.GetProperty("data"));
        double[] expectedParameters = ReadArray(family.GetProperty("numerics_parameters"));
        double expectedMaximumLogLikelihood = family.GetProperty("maximum_log_likelihood").GetDouble();
        JsonElement evaluation = family.GetProperty("evaluation");
        double evaluationX = evaluation.GetProperty("x").GetDouble();
        double expectedPdf = evaluation.GetProperty("pdf").GetDouble();
        double expectedCdf = evaluation.GetProperty("cdf").GetDouble();
        double evaluationProbability = evaluation.GetProperty("probability").GetDouble();
        double expectedQuantile = evaluation.GetProperty("quantile").GetDouble();

        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(data)
        };
        var model = new UnivariateDistribution(dataFrame, distributionType);
        UnivariateDistributionBase crosswalkDistribution = model.Distribution.Clone();
        crosswalkDistribution.SetParameters(expectedParameters);

        AssertCrossLanguageEqual(expectedPdf, crosswalkDistribution.PDF(evaluationX), "PDF");
        AssertCrossLanguageEqual(expectedCdf, crosswalkDistribution.CDF(evaluationX), "CDF");
        AssertCrossLanguageEqual(
            expectedQuantile,
            crosswalkDistribution.InverseCDF(evaluationProbability),
            "quantile");
        AssertCrossLanguageEqual(
            expectedMaximumLogLikelihood,
            model.DataLogLikelihood(expectedParameters),
            "data log likelihood at the SciPy optimum");

        var mle = new MaximumLikelihood(model, optimizationMethod)
        {
            ComputeHessian = false,
            ReportFailure = true
        };
        mle.Optimizer.AbsoluteTolerance = 1E-12d;
        mle.Optimizer.RelativeTolerance = 1E-12d;

        bool estimated = mle.Estimate();

        Assert.IsTrue(estimated, $"{familyName} MLE did not converge.");
        Assert.AreEqual(expectedParameters.Length, mle.BestParameterSet.Values.Length);
        AssertCrossLanguageEqual(
            expectedMaximumLogLikelihood,
            mle.MaximumLogLikelihood,
            "maximized data log likelihood");
        for (int i = 0; i < expectedParameters.Length; i++)
        {
            double parameterTolerance = 1E-5d * Math.Max(1d, Math.Abs(expectedParameters[i]));
            Assert.AreEqual(
                expectedParameters[i],
                mle.BestParameterSet.Values[i],
                parameterTolerance,
                $"{familyName} parameter {i} differs from the SciPy MLE.");
        }
    }

    /// <summary>
    /// Loads and clones one family element from the committed SciPy artifact.
    /// </summary>
    /// <param name="familyName">Artifact family key.</param>
    /// <returns>A detached JSON element for the requested family.</returns>
    private static JsonElement LoadFamily(string familyName)
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "VerificationData",
            "scipy-family-oracles.json");
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
        return document.RootElement.GetProperty("families").GetProperty(familyName).Clone();
    }

    /// <summary>
    /// Reads a JSON numeric array into managed doubles.
    /// </summary>
    /// <param name="element">JSON array element.</param>
    /// <returns>Array of double values.</returns>
    private static double[] ReadArray(JsonElement element)
    {
        return element.EnumerateArray().Select(value => value.GetDouble()).ToArray();
    }

    /// <summary>
    /// Applies the declared cross-language absolute and relative tolerance.
    /// </summary>
    /// <param name="expected">External oracle value.</param>
    /// <param name="actual">C# value.</param>
    /// <param name="quantity">Quantity name for the assertion message.</param>
    private static void AssertCrossLanguageEqual(double expected, double actual, string quantity)
    {
        double tolerance = 1E-8d + 1E-7d * Math.Abs(expected);
        Assert.AreEqual(expected, actual, tolerance, $"Cross-language {quantity} mismatch.");
    }
}
