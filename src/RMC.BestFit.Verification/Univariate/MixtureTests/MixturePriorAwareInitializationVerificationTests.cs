using Numerics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.MixtureTests;

/// <summary>
/// Verifies the EM-seeded, prior-aware MAP approximation used for mixture MCMC initialization.
/// </summary>
[TestClass]
public sealed class MixturePriorAwareInitializationVerificationTests
{
    /// <summary>
    /// Verifies EM supplies the local MAP basin while an informative prior shifts and improves
    /// the full posterior used to construct the deterministic sampler population.
    /// </summary>
    [TestMethod]
    public void InformativePrior_EmSeededMapInitialization_UsesFullPosterior()
    {
        MixtureModel generator = CreateGeneratingModel();
        double[] sample = generator.GenerateRandomValues(1000, 12345);
        var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(sample) };
        var model = new MixtureModel(
            dataFrame,
            [UnivariateDistributionType.Normal, UnivariateDistributionType.Normal]);

        model.ExpectationMaximization(
            out double[] emParameters,
            out _,
            out _);
        const int firstComponentMeanIndex = 1;
        model.Parameters[firstComponentMeanIndex].PriorDistribution = new Normal(
            emParameters[firstComponentMeanIndex] + 1d,
            0.1d);

        double emLogPosterior = model.LogLikelihood(emParameters);
        var map = new MaximumAPosteriori(
            model,
            OptimizationMethod.NelderMead,
            emParameters);

        Assert.IsTrue(map.Estimate(), $"EM-seeded MAP refinement failed with status {map.Status}.");
        CollectionAssert.AreEqual(
            emParameters,
            map.InitialValues,
            "The local posterior refinement did not start from the EM solution.");

        double[] mapParameters = map.BestParameterSet.Values;
        double mapLogPosterior = model.LogLikelihood(mapParameters);
        Assert.IsTrue(
            double.IsFinite(mapLogPosterior) && mapLogPosterior >= emLogPosterior - 1E-8,
            $"MAP log posterior {mapLogPosterior:R} did not preserve the EM-start posterior {emLogPosterior:R}.");
        Assert.IsTrue(
            Math.Abs(mapParameters[firstComponentMeanIndex] - emParameters[firstComponentMeanIndex]) > 0.05d,
            "The informative prior did not materially shift the likelihood-only EM estimate.");

        Assert.IsTrue(
            map.TryGetInitializationCovarianceMatrix(out var covariance, out string? diagnostic),
            diagnostic ?? "The EM-seeded MAP covariance was unavailable.");

        var analysis = new MixtureAnalysis(model);
        analysis.BayesianAnalysis.SetUpSampler();
        MCMCSampler sampler = analysis.BayesianAnalysis.Sampler!;
        MixtureAnalysis.PopulateSamplerFromPosteriorApproximation(
            model,
            sampler,
            mapParameters,
            covariance.ToArray(),
            CancellationToken.None);

        Assert.AreEqual(MCMCSampler.InitializationType.UserDefined, sampler.Initialize);
        Assert.AreEqual(sampler.InitialIterations, sampler.PopulationMatrix.Count);
        foreach (ParameterSet parameterSet in sampler.PopulationMatrix)
        {
            Assert.AreEqual(
                model.LogLikelihood(parameterSet.Values),
                parameterSet.Fitness,
                1E-10,
                "The mixture population member was not scored by the full posterior.");
        }
    }

    /// <summary>
    /// Creates the separated two-Normal mixture used to generate the deterministic sample.
    /// </summary>
    /// <returns>A configured BestFit mixture generator.</returns>
    private static MixtureModel CreateGeneratingModel()
    {
        var distributions = new UnivariateDistributionBase[]
        {
            new Normal(0d, 1d),
            new Normal(5d, 1d)
        };
        var model = new MixtureModel(
            new BestFitDataFrame(),
            distributions.Select(distribution => distribution.Clone()).ToList());
        model.Mixture!.SetParameters(
            [0.4d, 0.6d],
            distributions.Select(distribution => distribution.Clone()).ToArray());
        return model;
    }
}
