using Numerics.Data.Statistics;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.CompositeTests;

/// <summary>
/// Verifies end-to-end composite predictive recovery from independently fitted child analyses.
/// </summary>
/// <remarks>
/// Two independent N=1000 Normal child samples use generator seeds 51001 and 51002; child and
/// composite samplers retain production seed 12345 and all other defaults. Each child must recover
/// its generating coordinates in central 95% intervals with R-hat below 1.10 and ESS of at least
/// 100. The scientific composite oracle is the analytical parent response at predeclared
/// nonexceedance probabilities 0.10, 0.25, 0.50, 0.75, and 0.90, not completion or finiteness.
/// </remarks>
[TestClass]
public class CompositePredictiveRecoveryTests
{
    /// <summary>The predeclared parent child means.</summary>
    private static readonly double[] ParentMeans = [10d, 22d];

    /// <summary>The predeclared parent child standard deviations.</summary>
    private static readonly double[] ParentStandardDeviations = [2d, 3d];

    /// <summary>The predeclared child generation seeds.</summary>
    private static readonly int[] ChildSeeds = [51001, 51002];

    /// <summary>The predeclared unequal mixture weights.</summary>
    private static readonly double[] MixtureWeights = [0.35d, 0.65d];

    /// <summary>The predeclared identified nonexceedance probabilities.</summary>
    private static readonly double[] ParentProbabilities = [0.90d, 0.75d, 0.50d, 0.25d, 0.10d];

    /// <summary>
    /// Verifies predictive recovery for an unequal-weight mixture composite.
    /// </summary>
    /// <returns>A task representing the child fits and composite propagation.</returns>
    [TestMethod]
    public Task MixtureComposite_EndToEndPredictiveRecovery() =>
        VerifyPredictiveRecoveryAsync(PredictiveRule.Mixture);

    /// <summary>
    /// Verifies predictive recovery for an independent maximum competing-risk composite.
    /// </summary>
    /// <returns>A task representing the child fits and composite propagation.</returns>
    [TestMethod]
    public Task MaximumComposite_EndToEndPredictiveRecovery() =>
        VerifyPredictiveRecoveryAsync(PredictiveRule.Maximum);

    /// <summary>
    /// Verifies predictive recovery for an independent minimum competing-risk composite.
    /// </summary>
    /// <returns>A task representing the child fits and composite propagation.</returns>
    [TestMethod]
    public Task MinimumComposite_EndToEndPredictiveRecovery() =>
        VerifyPredictiveRecoveryAsync(PredictiveRule.Minimum);

    /// <summary>
    /// Verifies predictive recovery for equal-weight model averaging.
    /// </summary>
    /// <returns>A task representing the child fits and composite propagation.</returns>
    [TestMethod]
    public Task EqualWeightModelAverage_EndToEndPredictiveRecovery() =>
        VerifyPredictiveRecoveryAsync(PredictiveRule.EqualModelAverage);

    /// <summary>
    /// Fits two N=1000 Normal children through unchanged defaults, constructs the requested
    /// composite, and requires the analytical parent quantiles inside central 95% bands.
    /// </summary>
    /// <param name="rule">The selected composite interaction.</param>
    /// <returns>A task representing the complete recovery operation.</returns>
    private static async Task VerifyPredictiveRecoveryAsync(PredictiveRule rule)
    {
        var children = new UnivariateAnalysis[ParentMeans.Length];
        for (int childIndex = 0; childIndex < children.Length; childIndex++)
        {
            children[childIndex] = await FitChildAsync(
                childIndex,
                ParentMeans[childIndex],
                ParentStandardDeviations[childIndex],
                ChildSeeds[childIndex]);
        }

        double[] weights = rule == PredictiveRule.Mixture
            ? MixtureWeights.ToArray()
            : [0.5d, 0.5d];
        CompositeAnalysis composite = CreateComposite(rule, children, weights);
        composite.ProbabilityOrdinates.Clear();
        foreach (double probability in ParentProbabilities)
            composite.ProbabilityOrdinates.Add(1d - probability);
        composite.BayesianAnalysis.CredibleIntervalWidth = 0.95d;

        var validation = composite.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));
        await composite.RunAsync();

        Assert.IsTrue(composite.IsEstimated, $"{rule} composite recovery did not complete.");
        Assert.IsNotNull(composite.AnalysisResults, $"{rule} composite results are null.");
        Assert.IsNotNull(composite.AnalysisResults!.ConfidenceIntervals,
            $"{rule} composite confidence intervals are null.");
        Assert.AreEqual(ParentProbabilities.Length, composite.AnalysisResults.ConfidenceIntervals!.GetLength(0),
            $"{rule} composite response-grid length changed.");

        for (int probabilityIndex = 0; probabilityIndex < ParentProbabilities.Length; probabilityIndex++)
        {
            double probability = ParentProbabilities[probabilityIndex];
            double parentQuantile = SolveParentQuantile(rule, weights, probability);
            RecoveryAcceptance.AssertIdentifiedResponseGrid(
                $"{rule} parent quantile at nonexceedance {probability:G3}",
                parentQuantile,
                composite.AnalysisResults.ConfidenceIntervals[probabilityIndex, 0],
                composite.AnalysisResults.ConfidenceIntervals[probabilityIndex, 1]);
        }
    }

    /// <summary>
    /// Generates and fits one Normal child using unchanged Bayesian production defaults.
    /// </summary>
    /// <param name="childIndex">The zero-based child identifier.</param>
    /// <param name="parentMean">The generating Normal mean.</param>
    /// <param name="parentStandardDeviation">The generating Normal standard deviation.</param>
    /// <param name="seed">The generation seed.</param>
    /// <returns>The successfully fitted child analysis.</returns>
    private static async Task<UnivariateAnalysis> FitChildAsync(
        int childIndex,
        double parentMean,
        double parentStandardDeviation,
        int seed)
    {
        var parent = new Normal(parentMean, parentStandardDeviation);
        double[] sample = parent.GenerateRandomValues(RecoveryDesign.SampleSize, seed);
        Assert.AreEqual(RecoveryDesign.SampleSize, sample.Length,
            $"Child {childIndex + 1} generation did not return exactly N=1000 observations.");
        var dataFrame = new BestFitDataFrame { ExactSeries = new ExactSeries(sample) };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        double[] truth = [parentMean, parentStandardDeviation];
        Assert.IsTrue(double.IsFinite(model.PriorLogLikelihood(truth)),
            $"Child {childIndex + 1} generating truth lies outside a configured prior.");
        double parentLogLikelihood = model.DataLogLikelihood(truth);
        double[] misspecified = [parentMean + 3d * parentStandardDeviation, parentStandardDeviation];
        double misspecifiedLogLikelihood = model.DataLogLikelihood(misspecified);
        Assert.IsTrue(
            double.IsFinite(parentLogLikelihood) && parentLogLikelihood > misspecifiedLogLikelihood,
            $"Child {childIndex + 1} parent likelihood {parentLogLikelihood:G17} must beat the declared shifted alternative {misspecifiedLogLikelihood:G17}.");

        var analysis = new UnivariateAnalysis(model);
        Assert.AreEqual(12345, analysis.BayesianAnalysis.PRNGSeed,
            "The child Bayesian production seed default changed.");
        Exception? analysisError = null;
        analysis.AnalysisCompleted += (_, args) => analysisError = args.Error;
        var validation = analysis.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));
        await analysis.RunAsync();

        Assert.IsTrue(
            analysis.IsEstimated,
            $"Child {childIndex + 1} Bayesian fit did not complete. {analysisError ?? analysis.BayesianAnalysis.LastError}");
        Assert.IsNotNull(analysis.BayesianAnalysis.Results,
            $"Child {childIndex + 1} Bayesian results are null.");
        Assert.IsNotNull(analysis.AnalysisResults,
            $"Child {childIndex + 1} frequency results are null.");
        AssertChildRecovery(childIndex, truth, model, analysis);
        return analysis;
    }

    /// <summary>
    /// Applies central-95% parent inclusion, R-hat, and ESS acceptance to both Normal coordinates.
    /// </summary>
    /// <param name="childIndex">The zero-based child identifier.</param>
    /// <param name="truth">The generating mean and standard deviation.</param>
    /// <param name="model">The fitted child model.</param>
    /// <param name="analysis">The completed child analysis.</param>
    private static void AssertChildRecovery(
        int childIndex,
        IReadOnlyList<double> truth,
        UnivariateDistribution model,
        UnivariateAnalysis analysis)
    {
        var results = analysis.BayesianAnalysis.Results!;
        Assert.AreEqual(truth.Count, results.ParameterResults.Length,
            $"Child {childIndex + 1} diagnostic dimension changed.");
        for (int parameterIndex = 0; parameterIndex < truth.Count; parameterIndex++)
        {
            double[] draws = results.Output
                .Select(output => output.Values[parameterIndex])
                .ToArray();
            Assert.IsTrue(draws.All(double.IsFinite),
                $"Child {childIndex + 1} posterior coordinate {parameterIndex + 1} must contain only finite draws.");
            Array.Sort(draws);
            double lower = Statistics.Percentile(draws, 0.025d, true);
            double upper = Statistics.Percentile(draws, 0.975d, true);
            var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
            RecoveryAcceptance.AssertBayesianRecovery(
                $"child {childIndex + 1} {model.Parameters[parameterIndex].Name}",
                truth[parameterIndex],
                lower,
                upper,
                summary.Rhat,
                summary.ESS);
        }
    }

    /// <summary>
    /// Constructs the requested composite over fitted child analyses.
    /// </summary>
    /// <param name="rule">The composite interaction.</param>
    /// <param name="children">The fitted child analyses.</param>
    /// <param name="weights">The declared physical weights.</param>
    /// <returns>The configured composite analysis.</returns>
    private static CompositeAnalysis CreateComposite(
        PredictiveRule rule,
        IReadOnlyList<UnivariateAnalysis> children,
        IReadOnlyList<double> weights)
    {
        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = rule is PredictiveRule.Maximum or PredictiveRule.Minimum
                ? CompositeType.CompetingRisks
                : rule == PredictiveRule.EqualModelAverage
                    ? CompositeType.ModelAverage
                    : CompositeType.Mixture,
            IsMaximum = rule != PredictiveRule.Minimum,
            Dependency = Probability.DependencyType.Independent,
            ModelAverageMethod = rule == PredictiveRule.EqualModelAverage
                ? AverageMethod.Equal
                : AverageMethod.DIC
        };
        for (int childIndex = 0; childIndex < children.Count; childIndex++)
            composite.Analyses.Add(new WeightedUnivariateAnalysis(children[childIndex], weights[childIndex]));

        if (rule == PredictiveRule.EqualModelAverage)
        {
            composite.EstimateModelWeights();
            Assert.IsTrue(composite.Analyses.All(child => Math.Abs(child.Weight - 0.5d) <= 1E-12),
                "Equal model averaging did not assign exactly 0.5/0.5 weights.");
        }
        return composite;
    }

    /// <summary>
    /// Solves the analytically defined parent composite quantile by bisection.
    /// </summary>
    /// <param name="rule">The composite interaction.</param>
    /// <param name="weights">The physical mixture or model-average weights.</param>
    /// <param name="probability">The target nonexceedance probability.</param>
    /// <returns>The analytical parent quantile.</returns>
    private static double SolveParentQuantile(
        PredictiveRule rule,
        IReadOnlyList<double> weights,
        double probability)
    {
        double lower = -20d;
        double upper = 60d;
        for (int iteration = 0; iteration < 120; iteration++)
        {
            double midpoint = 0.5d * (lower + upper);
            if (ParentCompositeCdf(rule, weights, midpoint) < probability)
                lower = midpoint;
            else
                upper = midpoint;
        }
        return 0.5d * (lower + upper);
    }

    /// <summary>
    /// Evaluates the parent composite CDF without calling a production composite constructor.
    /// </summary>
    /// <param name="rule">The composite interaction.</param>
    /// <param name="weights">The physical mixture or model-average weights.</param>
    /// <param name="x">The evaluation ordinate.</param>
    /// <returns>The analytical composite CDF.</returns>
    private static double ParentCompositeCdf(
        PredictiveRule rule,
        IReadOnlyList<double> weights,
        double x)
    {
        double first = StandardNormalCdf((x - ParentMeans[0]) / ParentStandardDeviations[0]);
        double second = StandardNormalCdf((x - ParentMeans[1]) / ParentStandardDeviations[1]);
        return rule switch
        {
            PredictiveRule.Maximum => first * second,
            PredictiveRule.Minimum => 1d - (1d - first) * (1d - second),
            _ => weights[0] * first + weights[1] * second
        };
    }

    /// <summary>
    /// Evaluates the standard-Normal CDF from the error function.
    /// </summary>
    /// <param name="z">The standardized ordinate.</param>
    /// <returns>The standard-Normal nonexceedance probability.</returns>
    private static double StandardNormalCdf(double z)
    {
        double absoluteZ = Math.Abs(z);
        double t = 1d / (1d + 0.2316419d * absoluteZ);
        double density = Math.Exp(-0.5d * absoluteZ * absoluteZ) / Math.Sqrt(2d * Math.PI);
        double upperTail = density * t *
            (0.319381530d + t *
                (-0.356563782d + t *
                    (1.781477937d + t *
                        (-1.821255978d + t * 1.330274429d))));
        return z >= 0d ? 1d - upperTail : upperTail;
    }

    /// <summary>
    /// Identifies the four distinct end-to-end predictive interactions.
    /// </summary>
    private enum PredictiveRule
    {
        /// <summary>Unequal-weight aleatory mixture.</summary>
        Mixture,

        /// <summary>Independent maximum competing risk.</summary>
        Maximum,

        /// <summary>Independent minimum competing risk.</summary>
        Minimum,

        /// <summary>Equal-weight model averaging.</summary>
        EqualModelAverage
    }
}
