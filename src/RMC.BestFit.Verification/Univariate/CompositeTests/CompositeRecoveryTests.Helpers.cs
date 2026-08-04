using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.Univariate.CompositeTests;

/// <summary>
/// Supplies report constants, controlled estimated children, and independent Cartesian
/// probability oracles for <see cref="CompositeRecoveryTests"/>.
/// </summary>
public partial class CompositeRecoveryTests
{
    /// <summary>The report Table 44 child means.</summary>
    private static readonly double[] ReportMeans = [10d, 20d, 30d];

    /// <summary>The report Table 44 child standard deviations.</summary>
    private static readonly double[] ReportStandardDeviations = [2d, 1d, 5d];

    /// <summary>The report Table 44 mixture weights.</summary>
    private static readonly double[] ReportWeights = [0.3d, 0.2d, 0.5d];

    /// <summary>The report Table 45 annual exceedance probabilities.</summary>
    private static readonly double[] ReportAeps =
    [
        1E-6, 2E-6, 5E-6, 1E-5, 2E-5, 5E-5, 1E-4, 2E-4, 5E-4,
        1E-3, 2E-3, 5E-3, 1E-2, 2E-2, 5E-2, 1E-1, 2E-1, 3E-1,
        5E-1, 7E-1, 8E-1, 9E-1, 9.5E-1, 9.8E-1, 9.9E-1
    ];

    /// <summary>The published R <c>mistr</c> Table 45 quantiles.</summary>
    private static readonly double[] MistrTable45 =
    [
        53.10d, 52.34d, 51.33d, 50.54d, 49.72d, 48.60d, 47.70d, 46.76d, 45.45d,
        44.39d, 43.26d, 41.63d, 40.27d, 38.75d, 36.40d, 34.21d, 31.26d, 28.72d,
        21.38d, 15.58d, 10.88d, 9.15d, 8.07d, 7.00d, 6.34d
    ];

    /// <summary>The retained posterior count used by each controlled child.</summary>
    private const int RetainedDrawCount = 5000;

    /// <summary>The compact retained count used when only a fixed point estimate is required.</summary>
    private const int FixedDrawCount = 100;

    /// <summary>The number of deterministic posterior-mean support points per child.</summary>
    private const int PosteriorSupportCount = 20;

    /// <summary>The fixed composite posterior-resampling seed.</summary>
    private const int CompositeSeed = 20260803;

    /// <summary>The central credible interval used by posterior fixtures.</summary>
    private const double CredibleIntervalWidth = 0.90d;

    /// <summary>The maximum posterior mean-curve ordinate error.</summary>
    private const double PosteriorMeanTolerance = 0.02d;

    /// <summary>The maximum posterior credible-limit ordinate error.</summary>
    private const double PosteriorLimitTolerance = 0.05d;

    /// <summary>The five central and tail nonexceedance probabilities.</summary>
    private static readonly double[] PosteriorProbabilities = [0.05d, 0.25d, 0.50d, 0.75d, 0.95d];

    /// <summary>
    /// Creates the report's three fixed estimated Normal children and requested composite rule.
    /// </summary>
    /// <param name="type">The mixture or competing-risk composite type.</param>
    /// <param name="isMaximum">Whether a competing-risk composite selects the maximum.</param>
    /// <param name="dependency">The competing-risk dependency.</param>
    /// <returns>The configured composite analysis.</returns>
    private static CompositeAnalysis CreateReportComposite(
        CompositeType type,
        bool isMaximum = true,
        Probability.DependencyType dependency = Probability.DependencyType.Independent)
    {
        var children = new UnivariateAnalysis[ReportMeans.Length];
        for (int index = 0; index < children.Length; index++)
        {
            children[index] = CreateEstimatedNormalChild(
                [ReportMeans[index]],
                ReportMeans[index],
                ReportStandardDeviations[index]);
        }
        return CreateComposite(children, ReportWeights, type, isMaximum, dependency);
    }

    /// <summary>
    /// Creates a configured composite over already estimated child analyses.
    /// </summary>
    /// <param name="children">The estimated child analyses.</param>
    /// <param name="weights">The child mixture weights.</param>
    /// <param name="type">The composite type.</param>
    /// <param name="isMaximum">Whether a competing-risk composite selects the maximum.</param>
    /// <param name="dependency">The competing-risk dependency.</param>
    /// <param name="correlationMatrix">The optional fixed correlation matrix.</param>
    /// <returns>The configured composite analysis.</returns>
    private static CompositeAnalysis CreateComposite(
        IReadOnlyList<UnivariateAnalysis> children,
        IReadOnlyList<double> weights,
        CompositeType type,
        bool isMaximum,
        Probability.DependencyType dependency,
        double[,]? correlationMatrix = null)
    {
        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = type,
            IsMaximum = isMaximum,
            Dependency = dependency
        };
        if (correlationMatrix != null)
            composite.CorrelationMatrix = (double[,])correlationMatrix.Clone();
        for (int index = 0; index < children.Count; index++)
            composite.Analyses.Add(new WeightedUnivariateAnalysis(children[index], weights[index]));
        return composite;
    }

    /// <summary>
    /// Gets the fixed point-estimate distribution and fails with a focused message when absent.
    /// </summary>
    /// <param name="composite">The configured composite.</param>
    /// <returns>The composite point-estimate distribution.</returns>
    private static UnivariateDistributionBase GetPointEstimate(CompositeAnalysis composite)
    {
        UnivariateDistributionBase? distribution = composite.GetPointEstimateDistribution();
        Assert.IsNotNull(distribution, "Composite point-estimate distribution is null.");
        return distribution!;
    }

    /// <summary>
    /// Creates an estimated Normal child from a deterministic retained mean support.
    /// </summary>
    /// <param name="meanSupport">The deterministic retained mean support.</param>
    /// <param name="pointMean">The fixed parent and point-estimate mean.</param>
    /// <param name="standardDeviation">The fixed Normal standard deviation.</param>
    /// <returns>An estimated univariate child analysis with explicit MCMC results.</returns>
    private static UnivariateAnalysis CreateEstimatedNormalChild(
        IReadOnlyList<double> meanSupport,
        double pointMean,
        double standardDeviation)
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries(
                [pointMean - standardDeviation, pointMean, pointMean + standardDeviation])
        };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        model.SetParameterValues([pointMean, standardDeviation]);
        var stub = new UnivariateAnalysis(model);
        XElement state = stub.ToXElement();
        state.SetAttributeValue(nameof(AnalysisBase.IsEstimated), true);
        int outputCount = meanSupport.Count == PosteriorSupportCount
            ? RetainedDrawCount
            : FixedDrawCount;
        double[] retainedMeans = RepeatSupport(meanSupport, outputCount);
        var output = retainedMeans
            .Select(mean => new ParameterSet([mean, standardDeviation], 0d))
            .ToList();
        var results = new MCMCResults(
            new ParameterSet([pointMean, standardDeviation], 0d),
            output,
            1d - CredibleIntervalWidth);
        return new UnivariateAnalysis(model, state, results, new UncertaintyAnalysisResults());
    }

    /// <summary>
    /// Verifies one posterior rule against its complete 20-by-20-by-20 Cartesian oracle.
    /// </summary>
    /// <param name="rule">The mixture, maximum, or minimum rule.</param>
    /// <returns>A task that completes after result construction and comparison.</returns>
    private static async Task VerifyPosteriorCartesianOracleAsync(PosteriorRule rule)
    {
        double[][] supports =
        [
            EvenlySpaced(9.8d, 10.2d, PosteriorSupportCount),
            EvenlySpaced(19.8d, 20.2d, PosteriorSupportCount),
            EvenlySpaced(29.8d, 30.2d, PosteriorSupportCount)
        ];
        var children = new UnivariateAnalysis[supports.Length];
        for (int index = 0; index < children.Length; index++)
        {
            children[index] = CreateEstimatedNormalChild(
                supports[index],
                ReportMeans[index],
                ReportStandardDeviations[index]);
        }

        CompositeType type = rule == PosteriorRule.Mixture
            ? CompositeType.Mixture
            : CompositeType.CompetingRisks;
        bool isMaximum = rule != PosteriorRule.Minimum;
        CompositeAnalysis composite = CreateComposite(
            children,
            ReportWeights,
            type,
            isMaximum,
            Probability.DependencyType.Independent);
        composite.ProbabilityOrdinates.Clear();
        foreach (double probability in PosteriorProbabilities)
            composite.ProbabilityOrdinates.Add(1d - probability);
        composite.BayesianAnalysis.CredibleIntervalWidth = CredibleIntervalWidth;
        composite.BayesianAnalysis.PRNGSeed = CompositeSeed;

        await composite.CreateFrequencyAnalysisResultsAsync();

        Assert.IsNotNull(composite.AnalysisResults, $"{rule} posterior results are null.");
        Assert.IsNotNull(composite.AnalysisResults!.MeanCurve, $"{rule} posterior mean curve is null.");
        Assert.IsNotNull(composite.AnalysisResults.ConfidenceIntervals,
            $"{rule} posterior confidence intervals are null.");

        for (int probabilityIndex = 0; probabilityIndex < PosteriorProbabilities.Length; probabilityIndex++)
        {
            double probability = PosteriorProbabilities[probabilityIndex];
            PosteriorSummary expected = BuildCartesianOracle(rule, supports, probability);
            double actualMean = composite.AnalysisResults.MeanCurve![probabilityIndex];
            double actualLower = composite.AnalysisResults.ConfidenceIntervals![probabilityIndex, 0];
            double actualUpper = composite.AnalysisResults.ConfidenceIntervals[probabilityIndex, 1];
            Assert.AreEqual(expected.Mean, actualMean, PosteriorMeanTolerance,
                $"{rule} posterior mean mismatch at nonexceedance {probability:G3}.");
            Assert.AreEqual(expected.Lower, actualLower, PosteriorLimitTolerance,
                $"{rule} lower 90% limit mismatch at nonexceedance {probability:G3}.");
            Assert.AreEqual(expected.Upper, actualUpper, PosteriorLimitTolerance,
                $"{rule} upper 90% limit mismatch at nonexceedance {probability:G3}.");

            double fixedParentQuantile = SolveCompositeQuantile(
                rule,
                ReportMeans,
                ReportStandardDeviations,
                ReportWeights,
                probability);
            Assert.IsTrue(fixedParentQuantile >= actualLower - 1E-12,
                $"{rule} fixed parent fell below its 90% band at nonexceedance {probability:G3}.");
            Assert.IsTrue(fixedParentQuantile <= actualUpper + 1E-12,
                $"{rule} fixed parent rose above its 90% band at nonexceedance {probability:G3}.");
        }
    }

    /// <summary>
    /// Builds the posterior mean ordinate and 90% limits from all 8,000 Cartesian
    /// combinations without calling a production composite constructor or resampler.
    /// </summary>
    /// <param name="rule">The mixture, maximum, or minimum rule.</param>
    /// <param name="supports">The three deterministic mean supports.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <returns>The independent Cartesian posterior summary.</returns>
    private static PosteriorSummary BuildCartesianOracle(
        PosteriorRule rule,
        IReadOnlyList<double[]> supports,
        double probability)
    {
        double meanQuantile = SolveExpectedCompositeQuantile(rule, supports, probability);
        var quantiles = new double[supports[0].Length * supports[1].Length * supports[2].Length];
        int outputIndex = 0;
        foreach (double firstMean in supports[0])
        {
            foreach (double secondMean in supports[1])
            {
                foreach (double thirdMean in supports[2])
                {
                    quantiles[outputIndex++] = SolveCompositeQuantile(
                        rule,
                        [firstMean, secondMean, thirdMean],
                        ReportStandardDeviations,
                        ReportWeights,
                        probability);
                }
            }
        }

        Array.Sort(quantiles);
        double alpha = 1d - CredibleIntervalWidth;
        return new PosteriorSummary(
            meanQuantile,
            Statistics.Percentile(quantiles, alpha / 2d, true),
            Statistics.Percentile(quantiles, 1d - alpha / 2d, true));
    }

    /// <summary>
    /// Solves the inverse of the expected composite CDF over independent discrete
    /// posterior supports.
    /// </summary>
    /// <param name="rule">The mixture, maximum, or minimum rule.</param>
    /// <param name="supports">The three deterministic mean supports.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <returns>The posterior mean-curve ordinate.</returns>
    private static double SolveExpectedCompositeQuantile(
        PosteriorRule rule,
        IReadOnlyList<double[]> supports,
        double probability)
    {
        return SolveMonotoneProbability(
            x =>
            {
                double[] meanCdfs = new double[supports.Count];
                for (int child = 0; child < supports.Count; child++)
                {
                    int childIndex = child;
                    meanCdfs[child] = supports[child]
                        .Average(mean => NormalCdf(x, mean, ReportStandardDeviations[childIndex]));
                }
                return CombineCdfs(rule, meanCdfs, ReportWeights);
            },
            probability,
            -40d,
            90d);
    }

    /// <summary>
    /// Solves one conditional composite quantile using direct Normal CDF formulas.
    /// </summary>
    /// <param name="rule">The mixture, maximum, or minimum rule.</param>
    /// <param name="means">The conditional child means.</param>
    /// <param name="standardDeviations">The fixed child standard deviations.</param>
    /// <param name="weights">The mixture weights.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <returns>The conditional composite quantile.</returns>
    private static double SolveCompositeQuantile(
        PosteriorRule rule,
        IReadOnlyList<double> means,
        IReadOnlyList<double> standardDeviations,
        IReadOnlyList<double> weights,
        double probability)
    {
        return SolveMonotoneProbability(
            x =>
            {
                var cdfs = new double[means.Count];
                for (int index = 0; index < means.Count; index++)
                    cdfs[index] = NormalCdf(x, means[index], standardDeviations[index]);
                return CombineCdfs(rule, cdfs, weights);
            },
            probability,
            -40d,
            90d);
    }

    /// <summary>
    /// Combines child CDF values under one independent composite rule.
    /// </summary>
    /// <param name="rule">The mixture, maximum, or minimum rule.</param>
    /// <param name="cdfs">The child CDF values.</param>
    /// <param name="weights">The mixture weights.</param>
    /// <returns>The composite CDF value.</returns>
    private static double CombineCdfs(
        PosteriorRule rule,
        IReadOnlyList<double> cdfs,
        IReadOnlyList<double> weights)
    {
        if (rule == PosteriorRule.Mixture)
        {
            double sum = 0d;
            for (int index = 0; index < cdfs.Count; index++)
                sum += weights[index] * cdfs[index];
            return sum;
        }

        if (rule == PosteriorRule.Maximum)
            return cdfs.Aggregate(1d, (product, cdf) => product * cdf);

        return 1d - cdfs.Aggregate(1d, (product, cdf) => product * (1d - cdf));
    }

    /// <summary>
    /// Solves a continuous monotone probability function by deterministic bisection.
    /// </summary>
    /// <param name="cdf">The independent CDF oracle.</param>
    /// <param name="probability">The target nonexceedance probability.</param>
    /// <param name="lower">The lower bracketing ordinate.</param>
    /// <param name="upper">The upper bracketing ordinate.</param>
    /// <returns>The solved ordinate.</returns>
    private static double SolveMonotoneProbability(
        Func<double, double> cdf,
        double probability,
        double lower,
        double upper)
    {
        for (int iteration = 0; iteration < 120; iteration++)
        {
            double midpoint = 0.5d * (lower + upper);
            if (cdf(midpoint) < probability)
                lower = midpoint;
            else
                upper = midpoint;
        }
        return 0.5d * (lower + upper);
    }

    /// <summary>
    /// Evaluates the report mixture CDF from direct Normal formulas.
    /// </summary>
    /// <param name="x">The evaluation ordinate.</param>
    /// <returns>The exact weighted-Normal mixture CDF.</returns>
    private static double ReportMixtureCdf(double x)
    {
        double[] cdfs = ReportChildCdfs(x);
        return CombineCdfs(PosteriorRule.Mixture, cdfs, ReportWeights);
    }

    /// <summary>
    /// Evaluates the three report child CDFs from direct Normal formulas.
    /// </summary>
    /// <param name="x">The evaluation ordinate.</param>
    /// <returns>The three child CDF values.</returns>
    private static double[] ReportChildCdfs(double x)
    {
        var cdfs = new double[ReportMeans.Length];
        for (int index = 0; index < cdfs.Length; index++)
            cdfs[index] = NormalCdf(x, ReportMeans[index], ReportStandardDeviations[index]);
        return cdfs;
    }

    /// <summary>
    /// Evaluates a Normal CDF without a production composite object.
    /// </summary>
    /// <param name="x">The evaluation ordinate.</param>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="standardDeviation">The Normal standard deviation.</param>
    /// <returns>The Normal nonexceedance probability.</returns>
    private static double NormalCdf(double x, double mean, double standardDeviation)
    {
        return new Normal(mean, standardDeviation).CDF(x);
    }

    /// <summary>
    /// Creates evenly spaced inclusive support values.
    /// </summary>
    /// <param name="minimum">The minimum support value.</param>
    /// <param name="maximum">The maximum support value.</param>
    /// <param name="count">The support count.</param>
    /// <returns>The evenly spaced support.</returns>
    private static double[] EvenlySpaced(double minimum, double maximum, int count)
    {
        var support = new double[count];
        for (int index = 0; index < count; index++)
            support[index] = minimum + (maximum - minimum) * index / (count - 1d);
        return support;
    }

    /// <summary>
    /// Repeats a deterministic support to the requested retained-output length.
    /// </summary>
    /// <param name="support">The support values.</param>
    /// <param name="count">The requested output length.</param>
    /// <returns>The repeated retained values.</returns>
    private static double[] RepeatSupport(IReadOnlyList<double> support, int count)
    {
        var values = new double[count];
        for (int index = 0; index < count; index++)
            values[index] = support[index % support.Count];
        return values;
    }

    /// <summary>
    /// Identifies the three posterior combination rules exercised by Cartesian oracles.
    /// </summary>
    private enum PosteriorRule
    {
        /// <summary>Weighted aleatory mixture.</summary>
        Mixture,

        /// <summary>Independent maximum competing risk.</summary>
        Maximum,

        /// <summary>Independent minimum or weakest-link competing risk.</summary>
        Minimum
    }

    /// <summary>
    /// Stores one posterior mean ordinate and its central credible limits.
    /// </summary>
    /// <param name="Mean">The posterior mean-curve ordinate.</param>
    /// <param name="Lower">The lower central credible limit.</param>
    /// <param name="Upper">The upper central credible limit.</param>
    private sealed record PosteriorSummary(double Mean, double Lower, double Upper);
}
