using System.Text.Json;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.MixtureTests;

/// <summary>
/// Verifies ordinary Normal-mixture likelihood, response, and fit overlap against scikit-learn.
/// </summary>
[TestClass]
public class MixtureExternalPackageOracleTests
{
    /// <summary>Gets the committed external-package artifact file name.</summary>
    private const string ArtifactFileName = "normal-mixture-sklearn-oracle.json";

    /// <summary>
    /// Verifies full-K weights, Normal standard deviations, likelihoods, CDF ordinates, and the EM fit
    /// against the frozen scikit-learn 1.9.0 diagonal-covariance artifact.
    /// </summary>
    [TestMethod]
    public void OrdinaryNormalMixture2D_MatchesScikitLearnArtifact()
    {
        using JsonDocument document = LoadArtifact();
        JsonElement root = document.RootElement;
        Assert.AreEqual(1, root.GetProperty("schemaVersion").GetInt32());
        Assert.AreEqual("1.9.0", root.GetProperty("tool").GetProperty("scikitLearn").GetString());

        double[] sample = ReadArray(root.GetProperty("sample"));
        Assert.AreEqual(1000, sample.Length, "The external-package fixture must contain N=1000 observations.");
        JsonElement parent = root.GetProperty("parent");
        JsonElement frozenFit = root.GetProperty("scikitLearnFit");
        Assert.IsTrue(frozenFit.GetProperty("converged").GetBoolean(), "The frozen scikit-learn fit did not converge.");

        AssertFrozenLikelihoodAndCdf(sample, parent, parent, "parent");
        AssertFrozenLikelihoodAndCdf(sample, parent, frozenFit, "scikit-learn fit");

        var distributionTypes = new List<UnivariateDistributionType>
        {
            UnivariateDistributionType.Normal,
            UnivariateDistributionType.Normal
        };
        var model = new MixtureModel(CreateDataFrame(sample), distributionTypes, false);
        double[] frozenWeights = ReadArray(frozenFit.GetProperty("weights"));
        double[] frozenMeans = ReadArray(frozenFit.GetProperty("means"));
        double[] frozenStandardDeviations = ReadArray(frozenFit.GetProperty("standardDeviations"));
        double[] frozenParameters = CreateFullParameters(
            frozenWeights,
            frozenMeans,
            frozenStandardDeviations);
        Assert.AreEqual(
            frozenFit.GetProperty("logLikelihood").GetDouble(),
            model.DataLogLikelihood(frozenParameters),
            1E-9,
            "BestFit likelihood at the frozen scikit-learn fit differs from the independent artifact.");

        model.ExpectationMaximization(out double[] fittedParameters, out _, out _);
        var fitted = ReconstructAndSort(model, fittedParameters);
        for (int componentIndex = 0; componentIndex < fitted.Length; componentIndex++)
        {
            AssertScaledEqual(
                frozenWeights[componentIndex],
                fitted[componentIndex].Weight,
                $"component {componentIndex + 1} weight");
            AssertScaledEqual(
                frozenMeans[componentIndex],
                fitted[componentIndex].Mean,
                $"component {componentIndex + 1} mean");
            AssertScaledEqual(
                frozenStandardDeviations[componentIndex],
                fitted[componentIndex].StandardDeviation,
                $"component {componentIndex + 1} standard deviation");
        }
    }

    /// <summary>
    /// Verifies one frozen parameter set against independent likelihood and CDF values.
    /// </summary>
    /// <param name="sample">The frozen N=1000 sample.</param>
    /// <param name="parent">The artifact element containing the shared CDF locations.</param>
    /// <param name="parameterSet">The artifact parameter set to evaluate.</param>
    /// <param name="label">A diagnostic label.</param>
    private static void AssertFrozenLikelihoodAndCdf(
        double[] sample,
        JsonElement parent,
        JsonElement parameterSet,
        string label)
    {
        double[] weights = ReadArray(parameterSet.GetProperty("weights"));
        double[] means = ReadArray(parameterSet.GetProperty("means"));
        double[] standardDeviations = ReadArray(parameterSet.GetProperty("standardDeviations"));
        var mixture = new Mixture(
            weights,
            means.Select((mean, index) =>
                    (UnivariateDistributionBase)new Normal(mean, standardDeviations[index]))
                .ToArray());
        Assert.AreEqual(
            parameterSet.GetProperty("logLikelihood").GetDouble(),
            mixture.LogLikelihood(sample),
            1E-9,
            $"Numerics {label} log likelihood differs from the independent artifact.");

        double[] x = ReadArray(parent.GetProperty("cdfX"));
        double[] expectedCdf = ReadArray(parameterSet.GetProperty("cdf"));
        Assert.AreEqual(x.Length, expectedCdf.Length, $"{label} CDF grid dimensions differ.");
        for (int i = 0; i < x.Length; i++)
        {
            Assert.AreEqual(
                expectedCdf[i],
                mixture.CDF(x[i]),
                1E-12,
                $"{label} CDF differs at x={x[i]:G17}.");
        }
    }

    /// <summary>
    /// Reconstructs the BestFit EM output and orders it by component mean.
    /// </summary>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="parameters">The public full-K EM parameter vector.</param>
    /// <returns>The fitted components ordered by mean.</returns>
    private static (double Weight, double Mean, double StandardDeviation)[] ReconstructAndSort(
        MixtureModel model,
        double[] parameters)
    {
        var mixture = (Mixture)model.Mixture!.Clone();
        mixture.SetParameters(
            parameters.Take(2).ToArray(),
            parameters.Skip(2).ToArray());
        return mixture.Weights
            .Select((weight, index) => (
                Weight: weight,
                Mean: mixture.Distributions[index].Mean,
                StandardDeviation: mixture.Distributions[index].StandardDeviation))
            .OrderBy(component => component.Mean)
            .ToArray();
    }

    /// <summary>
    /// Creates the BestFit full-K public parameter vector.
    /// </summary>
    /// <param name="weights">Full-K physical weights.</param>
    /// <param name="means">Normal means.</param>
    /// <param name="standardDeviations">Normal standard deviations.</param>
    /// <returns>The public mixture parameter vector.</returns>
    private static double[] CreateFullParameters(
        IReadOnlyList<double> weights,
        IReadOnlyList<double> means,
        IReadOnlyList<double> standardDeviations)
    {
        var parameters = new List<double>(weights);
        for (int i = 0; i < means.Count; i++)
        {
            parameters.Add(means[i]);
            parameters.Add(standardDeviations[i]);
        }
        return parameters.ToArray();
    }

    /// <summary>
    /// Applies the predeclared 1E-4 scaled external-fit overlap tolerance.
    /// </summary>
    /// <param name="expected">The frozen scikit-learn coordinate.</param>
    /// <param name="actual">The BestFit EM coordinate.</param>
    /// <param name="label">A diagnostic label.</param>
    private static void AssertScaledEqual(double expected, double actual, string label)
    {
        double tolerance = 1E-4 * Math.Max(1.0, Math.Abs(expected));
        Assert.AreEqual(expected, actual, tolerance, $"External-package fit mismatch for {label}.");
    }

    /// <summary>
    /// Creates an exact annual BestFit data frame from the frozen sample.
    /// </summary>
    /// <param name="sample">The frozen scalar observations.</param>
    /// <returns>The populated data frame.</returns>
    private static BestFitDataFrame CreateDataFrame(IEnumerable<double> sample)
    {
        var exactData = sample
            .Select((value, index) => new ExactData { Index = index, Value = value })
            .ToList();
        return new BestFitDataFrame { ExactSeries = new ExactSeries(exactData) };
    }

    /// <summary>
    /// Reads a JSON number array.
    /// </summary>
    /// <param name="element">The JSON array element.</param>
    /// <returns>The parsed values.</returns>
    private static double[] ReadArray(JsonElement element) =>
        element.EnumerateArray().Select(value => value.GetDouble()).ToArray();

    /// <summary>
    /// Loads the committed artifact copied into the Verification output directory.
    /// </summary>
    /// <returns>The parsed artifact document.</returns>
    private static JsonDocument LoadArtifact()
    {
        string path = Path.Combine(AppContext.BaseDirectory, "VerificationData", ArtifactFileName);
        Assert.IsTrue(File.Exists(path), $"Missing mixture oracle artifact: {path}");
        return JsonDocument.Parse(File.ReadAllText(path));
    }
}
