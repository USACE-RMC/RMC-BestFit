using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.MixtureTests;

/// <summary>
/// Cross-engine recovery verification for finite Normal mixtures.
/// </summary>
[TestClass]
public class MixtureRecoveryTests
{
    /// <summary>
    /// Verifies recovery and cross-engine parity for a two-component Normal mixture.
    /// </summary>
    [TestMethod]
    public void NormalMixture2D_Recovery_Parity()
    {
        VerifyRecovery(
            generatingWeights: new[] { 0.3, 0.7 },
            generatingDistributions: new[]
            {
                new Normal(0.0, 1.0),
                new Normal(3.0, 0.1)
            },
            isZeroInflated: false,
            zeroWeight: 0.0);
    }

    /// <summary>
    /// Verifies recovery and cross-engine parity for a zero-inflated two-component Normal hurdle mixture.
    /// </summary>
    [TestMethod]
    public void ZeroInflatedNormalMixture2D_Recovery_Parity()
    {
        VerifyRecovery(
            generatingWeights: new[] { 0.3, 0.6 },
            generatingDistributions: new[]
            {
                new Normal(3.0, 0.1),
                new Normal(5.0, 2.0)
            },
            isZeroInflated: true,
            zeroWeight: 0.1);
    }

    /// <summary>
    /// Verifies recovery and cross-engine parity for a three-component Normal mixture.
    /// </summary>
    [TestMethod]
    public void NormalMixture3D_Recovery_Parity()
    {
        VerifyRecovery(
            generatingWeights: new[] { 0.2, 0.3, 0.5 },
            generatingDistributions: new[]
            {
                new Normal(0.0, 1.0),
                new Normal(3.0, 0.1),
                new Normal(5.0, 2.0)
            },
            isZeroInflated: false,
            zeroWeight: 0.0);
    }

    /// <summary>
    /// Generates one shared sample, compares pre-fit likelihoods, fits both engines, and checks parity and recovery.
    /// </summary>
    /// <param name="generatingWeights">The physical component weights.</param>
    /// <param name="generatingDistributions">The generating Normal components.</param>
    /// <param name="isZeroInflated">Whether to generate and fit a positive-hurdle mixture.</param>
    /// <param name="zeroWeight">The generating atom probability.</param>
    private static void VerifyRecovery(
        double[] generatingWeights,
        Normal[] generatingDistributions,
        bool isZeroInflated,
        double zeroWeight)
    {
        var generator = new Mixture(
            generatingWeights.ToArray(),
            generatingDistributions.Select(distribution => distribution.Clone()).ToArray())
        {
            IsZeroInflated = isZeroInflated,
            ZeroWeight = zeroWeight
        };
        double[] sample = generator.GenerateRandomValues(1000, 12345);
        BestFitDataFrame dataFrame = CreateDataFrame(sample);
        var distributionTypes = Enumerable
            .Repeat(UnivariateDistributionType.Normal, generatingDistributions.Length)
            .ToList();
        var bestFit = new MixtureModel(dataFrame, distributionTypes, isZeroInflated);

        double fittedZeroWeight = isZeroInflated ? bestFit.Mixture!.ZeroWeight : 0.0;
        double componentMass = 1.0 - fittedZeroWeight;
        double[] prefitWeights = generatingWeights.ToArray();
        if (isZeroInflated)
        {
            prefitWeights[^1] = componentMass - prefitWeights.Take(prefitWeights.Length - 1).Sum();
        }
        var numericsPrefit = new Mixture(
            prefitWeights,
            generatingDistributions.Select(distribution => distribution.Clone()).ToArray())
        {
            IsZeroInflated = isZeroInflated,
            ZeroWeight = fittedZeroWeight
        };
        double[] bestFitPrefitParameters = CreateBestFitParameters(
            prefitWeights,
            generatingDistributions);

        Assert.AreEqual(
            numericsPrefit.LogLikelihood(sample),
            bestFit.DataLogLikelihood(bestFitPrefitParameters),
            1E-10,
            "Pre-fit Numerics and BestFit data log likelihoods differ.");

        double initialWeight = componentMass / generatingWeights.Length;
        var numericsFit = new Mixture(
            Enumerable.Repeat(initialWeight, generatingWeights.Length).ToArray(),
            generatingDistributions
                .Select(_ => (UnivariateDistributionBase)new Normal())
                .ToArray())
        {
            IsZeroInflated = isZeroInflated,
            ZeroWeight = fittedZeroWeight
        };
        double[] numericsParameters = numericsFit.MLE(sample);
        numericsFit.SetParameters(numericsParameters);

        bestFit.ExpectationMaximization(
            out double[] bestFitParameters,
            out _,
            out _);
        Mixture bestFitFit = ReconstructBestFitMixture(bestFit, bestFitParameters);

        var numericsSorted = SortByComponentMean(numericsFit);
        var bestFitSorted = SortByComponentMean(bestFitFit);
        for (int componentIndex = 0; componentIndex < generatingWeights.Length; componentIndex++)
        {
            Assert.AreEqual(
                numericsSorted[componentIndex].Weight,
                bestFitSorted[componentIndex].Weight,
                1E-8,
                $"Cross-engine weight mismatch for component {componentIndex + 1}.");
            for (int parameterIndex = 0;
                 parameterIndex < numericsSorted[componentIndex].Parameters.Length;
                 parameterIndex++)
            {
                Assert.AreEqual(
                    numericsSorted[componentIndex].Parameters[parameterIndex],
                    bestFitSorted[componentIndex].Parameters[parameterIndex],
                    1E-8,
                    $"Cross-engine parameter mismatch for component {componentIndex + 1}, parameter {parameterIndex + 1}.");
            }
        }

        var generatingSorted = generatingWeights
            .Select((weight, index) => (
                Weight: weight,
                Parameters: generatingDistributions[index].GetParameters))
            .OrderBy(component => component.Parameters[0])
            .ToArray();
        for (int componentIndex = 0; componentIndex < generatingSorted.Length; componentIndex++)
        {
            Assert.AreEqual(
                generatingSorted[componentIndex].Weight,
                bestFitSorted[componentIndex].Weight,
                0.1,
                $"Weight recovery failed for component {componentIndex + 1}.");
            for (int parameterIndex = 0;
                 parameterIndex < generatingSorted[componentIndex].Parameters.Length;
                 parameterIndex++)
            {
                Assert.AreEqual(
                    generatingSorted[componentIndex].Parameters[parameterIndex],
                    bestFitSorted[componentIndex].Parameters[parameterIndex],
                    0.1,
                    $"Parameter recovery failed for component {componentIndex + 1}, parameter {parameterIndex + 1}.");
            }
        }
    }

    /// <summary>
    /// Creates an exact annual data frame from the shared generated sample.
    /// </summary>
    /// <param name="sample">The generated sample.</param>
    /// <returns>A BestFit data frame containing the sample as exact annual records.</returns>
    private static BestFitDataFrame CreateDataFrame(double[] sample)
    {
        var exactData = sample
            .Select((value, index) => new ExactData { Index = index, Value = value })
            .ToList();
        return new BestFitDataFrame { ExactSeries = new ExactSeries(exactData) };
    }

    /// <summary>
    /// Creates a BestFit K-1 parameter vector from physical weights and component parameters.
    /// </summary>
    /// <param name="weights">The physical component weights.</param>
    /// <param name="distributions">The component distributions.</param>
    /// <returns>The BestFit parameter vector.</returns>
    private static double[] CreateBestFitParameters(
        double[] weights,
        IReadOnlyList<Normal> distributions)
    {
        var parameters = new List<double>();
        parameters.AddRange(weights.Take(Math.Max(0, weights.Length - 1)));
        foreach (Normal distribution in distributions)
        {
            parameters.AddRange(distribution.GetParameters);
        }
        return parameters.ToArray();
    }

    /// <summary>
    /// Reconstructs all physical weights and component parameters from a BestFit K-1 vector.
    /// </summary>
    /// <param name="model">The fitted BestFit model.</param>
    /// <param name="parameters">The BestFit EM parameter vector.</param>
    /// <returns>A Numerics mixture carrying the fitted physical parameters.</returns>
    private static Mixture ReconstructBestFitMixture(
        MixtureModel model,
        double[] parameters)
    {
        int componentCount = model.Mixture!.Distributions.Length;
        int freeWeightCount = Math.Max(0, componentCount - 1);
        double componentMass = model.IsZeroInflated ? 1.0 - model.Mixture.ZeroWeight : 1.0;
        var weights = new double[componentCount];
        for (int i = 0; i < freeWeightCount; i++) weights[i] = parameters[i];
        weights[^1] = componentMass - weights.Take(freeWeightCount).Sum();

        var result = (Mixture)model.Mixture.Clone();
        result.SetParameters(
            weights,
            parameters.Skip(freeWeightCount).ToArray());
        return result;
    }

    /// <summary>
    /// Sorts a fitted mixture by component mean to resolve label switching.
    /// </summary>
    /// <param name="mixture">The fitted mixture.</param>
    /// <returns>The physical weights and parameter arrays ordered by component mean.</returns>
    private static (double Weight, double[] Parameters)[] SortByComponentMean(
        Mixture mixture)
    {
        return mixture.Weights
            .Select((weight, index) => (
                Weight: weight,
                Parameters: mixture.Distributions[index].GetParameters))
            .OrderBy(component => component.Parameters[0])
            .ToArray();
    }
}
