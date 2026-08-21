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
/// Covers criterion filtering, exact-zero RMSE weights, correlation-matrix validation,
/// serialization, ownership, and independent posterior result construction.
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
    /// Creates an estimated generalized extreme-value child with a deterministic posterior.
    /// </summary>
    /// <param name="location">The GEV location.</param>
    /// <param name="scale">The GEV scale.</param>
    /// <param name="kappa">The Hosking GEV shape (negative for a heavy upper tail).</param>
    /// <returns>An estimated child analysis.</returns>
    private static UnivariateAnalysis CreateEstimatedGevChild(double location, double scale, double kappa)
    {
        var dataFrame = new BestFitDataFrame();
        for (int index = 0; index < 12; index++)
            dataFrame.ExactSeries.Add(new ExactData(2000 + index, location + scale * (index - 5.5d) / 5d));

        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.GeneralizedExtremeValue);
        var analysis = new UnivariateAnalysis(model);
        analysis.ProbabilityOrdinates.Clear();
        analysis.ProbabilityOrdinates.Add(0.01d);
        analysis.ProbabilityOrdinates.Add(0.5d);
        analysis.ProbabilityOrdinates.Add(0.99d);
        double[] parameterValues = { location, scale, kappa };
        var output = new List<ParameterSet>();
        for (int index = 0; index < 100; index++)
            output.Add(new ParameterSet((double[])parameterValues.Clone(), 0d));

        analysis.BayesianAnalysis.OutputLength = output.Count;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet((double[])parameterValues.Clone(), 0d), output, 0.1d),
            skipInformationCriteria: true);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.DIC), 100d);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.WAIC), 100d);
        SetPrivateCriterion(analysis.BayesianAnalysis, nameof(BayesianAnalysis.LOOIC), 100d);

        typeof(AnalysisBase)
            .GetField("_isEstimated", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(analysis, true);
        typeof(UnivariateAnalysis)
            .GetProperty(nameof(UnivariateAnalysis.AnalysisResults), BindingFlags.Instance | BindingFlags.Public)!
            .SetValue(analysis, new UncertaintyAnalysisResults { AIC = 100d, BIC = 100d, RMSE = 100d });

        return analysis;
    }

    /// <summary>
    /// Creates an estimated Normal child with a deliberately varying retained posterior.
    /// </summary>
    /// <param name="pointMean">The point-estimate Normal mean.</param>
    /// <param name="standardDeviation">The fixed posterior standard deviation.</param>
    /// <param name="outputCount">The retained posterior count.</param>
    /// <param name="posteriorMean">Maps a raw output index to its Normal mean.</param>
    /// <returns>An estimated child analysis.</returns>
    private static UnivariateAnalysis CreateVariablePosteriorChild(
        double pointMean,
        double standardDeviation,
        int outputCount,
        Func<int, double> posteriorMean)
    {
        UnivariateAnalysis analysis = CreateEstimatedChild(pointMean, standardDeviation, 100d);
        var output = new List<ParameterSet>(outputCount);
        for (int index = 0; index < outputCount; index++)
            output.Add(new ParameterSet([posteriorMean(index), standardDeviation], 0d));

        analysis.BayesianAnalysis.OutputLength = outputCount;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(new ParameterSet([pointMean, standardDeviation], 0d), output, 0.1d),
            skipInformationCriteria: true);
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

    /// <summary>
    /// Verifies both Composite construction branches consume independently randomized
    /// source indices based on actual retained counts without mutating child posteriors.
    /// </summary>
    /// <param name="compositeType">The Composite construction branch under test.</param>
    [TestMethod]
    [DataRow(CompositeType.Mixture)]
    [DataRow(CompositeType.CompetingRisks)]
    public async Task CreateFrequencyAnalysisResults_IndependentIndexes_MatchExactFiniteOracle(
        CompositeType compositeType)
    {
        UnivariateAnalysis first = CreateVariablePosteriorChild(100d, 6d, 120, index => 80d + 0.4d * index);
        UnivariateAnalysis second = CreateVariablePosteriorChild(180d, 9d, 190, index => 230d - 0.5d * index);
        CompositeAnalysis composite = CreateComposite(first, second);
        composite.CompositeDistributionType = compositeType;
        composite.BayesianAnalysis.PRNGSeed = 314159;
        double[][] firstSnapshot = first.BayesianAnalysis.Results!.Output.Select(draw => (double[])draw.Values.Clone()).ToArray();
        double[][] secondSnapshot = second.BayesianAnalysis.Results!.Output.Select(draw => (double[])draw.Values.Clone()).ToArray();
        double[] pointEstimateBefore = composite.GetPointEstimateDistribution()!.GetParameters;

        await composite.CreateFrequencyAnalysisResultsAsync();

        UncertaintyAnalysisResults expected = BuildExpectedCompositeResults(composite);
        AssertUncertaintyResultsEqual(expected, composite.AnalysisResults!, 1E-10);
        AssertPosteriorUnchanged(firstSnapshot, first.BayesianAnalysis.Results.Output);
        AssertPosteriorUnchanged(secondSnapshot, second.BayesianAnalysis.Results.Output);
        CollectionAssert.AreEqual(pointEstimateBefore, composite.GetPointEstimateDistribution()!.GetParameters);
    }

    /// <summary>
    /// Verifies a fixed seed is exactly repeatable and changing only that seed changes the
    /// finite posterior summary while preserving the point-estimate curve.
    /// </summary>
    [TestMethod]
    public async Task CreateFrequencyAnalysisResults_SeedContract_IsRepeatableAndResultDefining()
    {
        UnivariateAnalysis first = CreateVariablePosteriorChild(100d, 5d, 120, index => 70d + 0.3d * index);
        UnivariateAnalysis second = CreateVariablePosteriorChild(160d, 8d, 131, index => 225d - 0.4d * index);
        CompositeAnalysis composite = CreateComposite(first, second);
        composite.CompositeDistributionType = CompositeType.Mixture;
        composite.BayesianAnalysis.PRNGSeed = 271828;

        await composite.CreateFrequencyAnalysisResultsAsync();
        double[] firstMean = (double[])composite.AnalysisResults!.MeanCurve!.Clone();
        double[] firstMode = (double[])composite.AnalysisResults.ModeCurve!.Clone();
        await composite.CreateFrequencyAnalysisResultsAsync();

        CollectionAssert.AreEqual(firstMean, composite.AnalysisResults!.MeanCurve!);
        CollectionAssert.AreEqual(firstMode, composite.AnalysisResults.ModeCurve!);

        composite.BayesianAnalysis.PRNGSeed = 271829;
        Assert.IsNull(composite.AnalysisResults, "Changing the seed must invalidate derived results.");
        await composite.CreateFrequencyAnalysisResultsAsync();

        Assert.IsTrue(firstMean.Where((value, index) =>
            Math.Abs(value - composite.AnalysisResults!.MeanCurve![index]) > 1E-8).Any());
        CollectionAssert.AreEqual(firstMode, composite.AnalysisResults!.ModeCurve!);
    }

    /// <summary>
    /// Verifies mixture weights that sum to one within floating-point roundoff do not create a
    /// zero-inflated mixture, so negative quantiles of the children are preserved.
    /// </summary>
    /// <returns>A task representing the asynchronous result construction.</returns>
    [TestMethod]
    public async Task MixtureWeights_SummingToOneWithinRoundoff_AreNotZeroInflated()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(0d, 10d, 100d),
            CreateEstimatedChild(5d, 12d, 100d),
            CreateEstimatedChild(-5d, 8d, 100d));
        composite.CompositeDistributionType = CompositeType.Mixture;
        composite.Analyses[0].Weight = 0.7d;
        composite.Analyses[1].Weight = 0.2d;
        composite.Analyses[2].Weight = 0.1d;

        double sum = 0d;
        foreach (WeightedUnivariateAnalysis entry in composite.Analyses)
            sum += entry.Weight;
        Assert.IsTrue(sum < 1d && 1d - sum < 1E-12, $"The fixture must sum to one within roundoff but below one; sum = {sum:R}.");

        var pointEstimate = (Mixture)composite.GetPointEstimateDistribution()!;
        Assert.IsFalse(pointEstimate.IsZeroInflated, "Roundoff below one must not create a zero atom.");
        Assert.IsTrue(pointEstimate.InverseCDF(0.05d) < 0d, "Negative quantiles of the children must be preserved.");

        await composite.CreateFrequencyAnalysisResultsAsync();

        UncertaintyAnalysisResults? results = composite.AnalysisResults;
        Assert.IsNotNull(results);
        Assert.IsTrue(results.ModeCurve![2] < 0d, "The 0.99 exceedance ordinate must remain negative.");
    }

    /// <summary>
    /// Verifies a competing-risks composite of heavy-tailed children resolves its upper quantiles
    /// to within half a percent of a root-solved inversion of the composite distribution.
    /// </summary>
    [TestMethod]
    public void CompetingRisksPointEstimate_HeavyTailedChildren_MatchesRootSolvedQuantiles()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedGevChild(100d, 20d, -0.2d),
            CreateEstimatedGevChild(130d, 25d, -0.15d));
        composite.CompositeDistributionType = CompositeType.CompetingRisks;

        var pointEstimate = (CompetingRisks)composite.GetPointEstimateDistribution()!;
        var reference = new CompetingRisks(composite.Analyses
            .Select(entry => entry.UnivariateAnalysis!.GetDistribution(0)!)
            .ToArray())
        {
            MinimumOfRandomVariables = pointEstimate.MinimumOfRandomVariables,
            Dependency = pointEstimate.Dependency,
        };

        foreach (double probability in new[] { 0.5d, 0.9d, 0.99d, 0.999d })
        {
            double expected = reference.InverseCDF(probability);
            double actual = pointEstimate.InverseCDF(probability);
            Assert.AreEqual(expected, actual, Math.Abs(expected) * 0.005d, $"quantile at p = {probability}");
        }
    }

    /// <summary>
    /// Verifies a negative Composite posterior-resampling seed fails validation.
    /// </summary>
    [TestMethod]
    public void Validate_NegativePosteriorResamplingSeed_ReturnsError()
    {
        CompositeAnalysis composite = CreateComposite(
            CreateEstimatedChild(100d, 10d, 100d),
            CreateEstimatedChild(120d, 12d, 100d));
        composite.BayesianAnalysis.PRNGSeed = -1;

        var validation = composite.Validate();

        Assert.IsFalse(validation.IsValid);
        Assert.IsTrue(validation.ValidationMessages.Any(message =>
            message.Contains("PRNG seed", StringComparison.Ordinal)));
    }

    /// <summary>
    /// Reconstructs the exact finite Composite result from the helper's deterministic index matrix.
    /// </summary>
    /// <param name="composite">The configured Composite fixture.</param>
    /// <returns>The independently constructed finite-sample uncertainty result.</returns>
    private static UncertaintyAnalysisResults BuildExpectedCompositeResults(CompositeAnalysis composite)
    {
        int[] counts = composite.Analyses
            .Select(entry => entry.UnivariateAnalysis!.BayesianAnalysis!.Results!.Output.Count)
            .ToArray();
        int[][] indexes = PosteriorIndexResampler.CreateRandomIndexes(counts, composite.BayesianAnalysis.PRNGSeed);
        var distributions = new UnivariateDistributionBase[indexes[0].Length];

        for (int realization = 0; realization < distributions.Length; realization++)
        {
            var children = composite.Analyses
                .Select((entry, source) => entry.UnivariateAnalysis!.GetDistribution(indexes[source][realization])!)
                .ToArray();
            if (composite.CompositeDistributionType == CompositeType.CompetingRisks)
            {
                var competingRisks = new CompetingRisks(children)
                {
                    MinimumOfRandomVariables = !composite.IsMaximum,
                    Dependency = composite.Dependency,
                    CorrelationMatrix = composite.CorrelationMatrix!,
                    XTransform = Numerics.Data.Transform.None,
                    ProbabilityTransform = Numerics.Data.Transform.NormalZ
                };
                competingRisks.CreateEmpiricalCDF();
                distributions[realization] = competingRisks;
            }
            else
            {
                double[] weights = composite.Analyses.Select(entry => entry.Weight).ToArray();
                var mixture = new Mixture(weights, children)
                {
                    XTransform = Numerics.Data.Transform.None,
                    ProbabilityTransform = Numerics.Data.Transform.NormalZ
                };
                mixture.CreateEmpiricalCDF();
                distributions[realization] = mixture;
            }
        }

        double[] probabilities = composite.ProbabilityOrdinates.Select(probability => 1d - probability).ToArray();
        var bootstrap = new BootstrapAnalysis(
            composite.GetPointEstimateDistribution()!,
            ParameterEstimationMethod.MaximumLikelihood,
            100,
            distributions.Length);
        return bootstrap.Estimate(
            probabilities,
            1d - composite.BayesianAnalysis.CredibleIntervalWidth,
            distributions,
            false);
    }

    /// <summary>
    /// Verifies two uncertainty results agree at every reported ordinate.
    /// </summary>
    /// <param name="expected">The independently reconstructed result.</param>
    /// <param name="actual">The Composite result.</param>
    /// <param name="tolerance">The maximum absolute difference.</param>
    private static void AssertUncertaintyResultsEqual(
        UncertaintyAnalysisResults expected,
        UncertaintyAnalysisResults actual,
        double tolerance)
    {
        for (int index = 0; index < expected.ModeCurve!.Length; index++)
        {
            Assert.AreEqual(expected.ModeCurve[index], actual.ModeCurve![index], tolerance);
            Assert.AreEqual(expected.MeanCurve![index], actual.MeanCurve![index], tolerance);
            Assert.AreEqual(expected.ConfidenceIntervals![index, 0], actual.ConfidenceIntervals![index, 0], tolerance);
            Assert.AreEqual(expected.ConfidenceIntervals[index, 1], actual.ConfidenceIntervals[index, 1], tolerance);
        }
    }

    /// <summary>
    /// Verifies retained parameter values were not modified during Composite result construction.
    /// </summary>
    /// <param name="expected">The retained parameter snapshot.</param>
    /// <param name="actual">The retained posterior output after result construction.</param>
    private static void AssertPosteriorUnchanged(
        IReadOnlyList<double[]> expected,
        IReadOnlyList<ParameterSet> actual)
    {
        Assert.AreEqual(expected.Count, actual.Count);
        for (int index = 0; index < expected.Count; index++)
            CollectionAssert.AreEqual(expected[index], actual[index].Values);
    }
}
