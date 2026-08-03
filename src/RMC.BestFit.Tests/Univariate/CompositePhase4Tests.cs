using System.Globalization;
using System.Reflection;
using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Fast programmatic regression tests for Phase 4 composite-analysis corrections.
/// </summary>
/// <remarks>
/// Covers criterion filtering, exact-zero RMSE weights, and correlation-matrix
/// validation, serialization, ownership, and result construction. Posterior coupling
/// and chain-order behavior are deliberately outside this class because TR-014 is deferred.
/// </remarks>
[TestClass]
public class CompositePhase4Tests
{
    /// <summary>
    /// Creates an estimated Normal child with deterministic posterior and comparison results.
    /// </summary>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="standardDeviation">The Normal standard deviation.</param>
    /// <param name="criterion">The criterion value assigned to every supported field.</param>
    /// <returns>An estimated child analysis.</returns>
    private static UnivariateAnalysis CreateEstimatedChild(
        double mean,
        double standardDeviation,
        double criterion)
    {
        var dataFrame = new BestFitDataFrame();
        for (int index = 0; index < 12; index++)
            dataFrame.ExactSeries.Add(new ExactData(2000 + index, mean + standardDeviation * (index - 5.5d) / 5d));

        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.01d);
        analysis.ProbabilityOrdinates.Add(0.5d);
        analysis.ProbabilityOrdinates.Add(0.99d);
        double[] parameterValues = { mean, standardDeviation };
        var output = new List<ParameterSet>();
        for (int index = 0; index < 100; index++)
            output.Add(new ParameterSet((double[])parameterValues.Clone(), 0d));

        analysis.BayesianAnalysis.OutputLength = output.Count;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet((double[])parameterValues.Clone(), 0d), output, 0.1d),
            skipInformationCriteria: true);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.DIC), criterion);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.WAIC), criterion);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.LOOIC), criterion);

        typeof(AnalysisBase)
            .GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(analysis, true);
        typeof(UnivariateAnalysis)
            .GetProperty(nameof(UnivariateAnalysis.AnalysisResults), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(analysis, new UncertaintyAnalysisResults
            {
                AIC = criterion,
                BIC = criterion,
                RMSE = criterion
            });

        return analysis;
    }

    /// <summary>
    /// Assigns a private-set Bayesian comparison criterion for an inline fixture.
    /// </summary>
    /// <param name="analysis">The Bayesian analysis.</param>
    /// <param name="propertyName">The criterion property name.</param>
    /// <param name="value">The criterion value.</param>
    private static void SetPrivateCriterion(BayesianAnalysis analysis, string propertyName, double value)
    {
        analysis.GetType()
            .GetProperty(propertyName, BindingFlags.Instance | BindingFlags.Public)!
            .GetSetMethod(true)!
            .Invoke(analysis, new object[] { value });
    }

    /// <summary>
    /// Creates a configured composite over the supplied estimated children.
    /// </summary>
    /// <param name="children">The estimated children.</param>
    /// <returns>A composite with valid probability ordinates.</returns>
    private static CompositeAnalysis CreateComposite(params UnivariateAnalysis[] children)
    {
        var composite = new CompositeAnalysis();
        composite.ProbabilityOrdinates.Clear();
        composite.ProbabilityOrdinates.Add(0.01d);
        composite.ProbabilityOrdinates.Add(0.5d);
        composite.ProbabilityOrdinates.Add(0.99d);
        foreach (UnivariateAnalysis child in children)
            composite.Analyses.Add(new WeightedUnivariateAnalysis(child, 1d / children.Length));
        return composite;
    }

    /// <summary>
    /// Verifies invalid criteria receive zero weight and a named warning while valid
    /// children retain a normalized average.
    /// </summary>
    [TestMethod]
    public void EstimateModelWeights_MixedInvalidCriteria_WarnsAndExcludesInvalidChildren()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, 100d),
            CreateEstimatedChild(110d, 12d, double.NaN),
            CreateEstimatedChild(120d, 14d, 110d));
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = AverageMethod.AIC;

        composite.EstimateModelWeights();
        var validation = composite.Validate();

        Assert.IsTrue(validation.IsValid, string.Join("; ", validation.ValidationMessages));
        Assert.AreEqual(0d, composite.Analyses[1].Weight, 0d);
        Assert.AreEqual(1d, composite.Analyses.Sum(entry => entry.Weight), 1E-12);
        Assert.IsTrue(validation.ValidationMessages.Any(message =>
            message.StartsWith("Warning: Sub-analysis 2", StringComparison.Ordinal) &&
            message.Contains("assigned zero weight", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies a model average with no valid criterion is rejected with named errors and
    /// leaves every child at exactly zero weight.
    /// </summary>
    [TestMethod]
    public void EstimateModelWeights_AllInvalidCriteria_ReturnsNamedErrorsAndZeroWeights()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, double.NaN),
            CreateEstimatedChild(110d, 12d, double.PositiveInfinity));
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = AverageMethod.AIC;

        composite.EstimateModelWeights();
        var validation = composite.Validate();

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(composite.Analyses.All(entry => entry.Weight == 0d));
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.StartsWith("Error: Sub-analysis 1", StringComparison.Ordinal)));
        Assert.IsTrue(validation.ValidationMessages.Any(message => message.StartsWith("Error: Sub-analysis 2", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies exact-zero RMSE children split all weight without division by zero or NaN.
    /// </summary>
    [TestMethod]
    public void EstimateModelWeights_ExactZeroRmse_SplitsWeightAmongZeroChildren()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, 0d),
            CreateEstimatedChild(110d, 12d, 0d),
            CreateEstimatedChild(120d, 14d, 2d),
            CreateEstimatedChild(130d, 16d, double.NaN),
            CreateEstimatedChild(140d, 18d, -1d));
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = AverageMethod.RMSE;

        composite.EstimateModelWeights();

        Assert.AreEqual(0.5d, composite.Analyses[0].Weight, 0d);
        Assert.AreEqual(0.5d, composite.Analyses[1].Weight, 0d);
        Assert.AreEqual(0d, composite.Analyses[2].Weight, 0d);
        Assert.AreEqual(0d, composite.Analyses[3].Weight, 0d);
        Assert.AreEqual(0d, composite.Analyses[4].Weight, 0d);
        Assert.IsTrue(composite.Analyses.All(entry => double.IsFinite(entry.Weight)));
    }

    /// <summary>
    /// Verifies ordinary finite AIC values retain the established Numerics weighting result.
    /// </summary>
    [TestMethod]
    public void EstimateModelWeights_FiniteAic_MatchesEstablishedWeights()
    {
        double[] criteria = { 100d, 104d, 110d };
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, criteria[0]),
            CreateEstimatedChild(110d, 12d, criteria[1]),
            CreateEstimatedChild(120d, 14d, criteria[2]));
        composite.CompositeDistributionType = CompositeType.ModelAverage;
        composite.ModelAverageMethod = AverageMethod.AIC;

        composite.EstimateModelWeights();
        double[] expected = GoodnessOfFit.AICWeights(criteria);

        for (int index = 0; index < expected.Length; index++)
            Assert.AreEqual(expected[index], composite.Analyses[index].Weight, 1E-15);
    }

    /// <summary>
    /// Verifies correlation-matrix assignment and retrieval use defensive copies.
    /// </summary>
    [TestMethod]
    public void CorrelationMatrix_AssignmentAndGetter_OwnDefensiveCopies()
    {
        var composite = new CompositeAnalysis();
        var source = new[,] { { 1d, 0.4d }, { 0.4d, 1d } };

        composite.CorrelationMatrix = source;
        source[0, 1] = 0.9d;
        double[,] exposed = composite.CorrelationMatrix!;
        exposed[1, 0] = 0.8d;

        Assert.AreEqual(0.4d, composite.CorrelationMatrix![0, 1], 0d);
        Assert.AreEqual(0.4d, composite.CorrelationMatrix![1, 0], 0d);
    }

    /// <summary>
    /// Verifies malformed, asymmetric, and non-positive-definite matrices are rejected.
    /// </summary>
    [TestMethod]
    public void CorrelationMatrix_InvalidStructures_AreRejected()
    {
        var composite = new CompositeAnalysis();

        Assert.ThrowsException<ArgumentException>(() => composite.CorrelationMatrix = new double[2, 3]);
        Assert.ThrowsException<ArgumentException>(() => composite.CorrelationMatrix = new[,] { { 0.9d, 0d }, { 0d, 1d } });
        Assert.ThrowsException<ArgumentException>(() => composite.CorrelationMatrix = new[,] { { 1d, 0.2d }, { 0.3d, 1d } });
        Assert.ThrowsException<ArgumentException>(() => composite.CorrelationMatrix = new[,] { { 1d, 1d }, { 1d, 1d } });
        Assert.ThrowsException<ArgumentException>(() => composite.CorrelationMatrix = new[,] { { 1d, double.NaN }, { double.NaN, 1d } });
    }

    /// <summary>
    /// Verifies XML round-trip uses invariant matrix rows and legacy XML remains matrix-free.
    /// </summary>
    [TestMethod]
    public void CorrelationMatrix_XmlRoundTrip_PreservesValuesAndLegacyNull()
    {
        var composite = new CompositeAnalysis
        {
            CorrelationMatrix = new[,] { { 1d, 0.375d }, { 0.375d, 1d } }
        };

        string xml = composite.ToXElement().ToString();
        var restored = new CompositeAnalysis(composite.ToXElement());
        var legacy = new CompositeAnalysis(new System.Xml.Linq.XElement("CompositeAnalysis"));

        StringAssert.Contains(xml, "0.375");
        Assert.AreEqual(0.375d, restored.CorrelationMatrix![0, 1], 0d);
        Assert.AreEqual("0.375", restored.CorrelationMatrix[0, 1].ToString("G17", CultureInfo.InvariantCulture));
        Assert.IsNull(legacy.CorrelationMatrix);
    }

    /// <summary>
    /// Verifies active correlation dependency requires a dimensionally compatible matrix.
    /// </summary>
    [TestMethod]
    public void Validate_CorrelationMatrixDependency_RequiresMatchingDimensions()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, 100d),
            CreateEstimatedChild(110d, 12d, 110d));
        composite.Dependency = Probability.DependencyType.CorrelationMatrix;

        var missing = composite.Validate();
        composite.CorrelationMatrix = new[,] { { 1d, 0.2d, 0.1d }, { 0.2d, 1d, 0.3d }, { 0.1d, 0.3d, 1d } };
        var mismatched = composite.Validate();

        Assert.IsFalse(missing.IsValid);
        Assert.IsTrue(missing.ValidationMessages.Any(message => message.Contains("requires a correlation matrix", StringComparison.Ordinal)));
        Assert.IsFalse(mismatched.IsValid);
        Assert.IsTrue(mismatched.ValidationMessages.Any(message => message.Contains("number of components", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Verifies point-estimate and frequency-result construction carry correlation dependency
    /// through to dependency-aware simulation.
    /// </summary>
    [TestMethod]
    public async Task CorrelationMatrix_ResultConstruction_PropagatesDependencyAndMatrix()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, 100d),
            CreateEstimatedChild(110d, 12d, 110d));
        composite.Dependency = Probability.DependencyType.CorrelationMatrix;
        composite.CorrelationMatrix = new[,] { { 1d, 0.6d }, { 0.6d, 1d } };

        var pointEstimate = (CompetingRisks)composite.GetPointEstimateDistribution()!;
        double[] first = pointEstimate.GenerateRandomValues(64, 12345);
        double[] second = pointEstimate.GenerateRandomValues(64, 12345);
        await composite.CreateFrequencyAnalysisResultsAsync();

        Assert.AreEqual(Probability.DependencyType.CorrelationMatrix, pointEstimate.Dependency);
        Assert.AreEqual(0.6d, pointEstimate.CorrelationMatrix[0, 1], 0d);
        CollectionAssert.AreEqual(first, second);
        UncertaintyAnalysisResults? results = composite.AnalysisResults;
        Assert.IsNotNull(results);
        Assert.IsNotNull(results.ModeCurve);
        Assert.IsTrue(results.ModeCurve.All(double.IsFinite));
    }
}
