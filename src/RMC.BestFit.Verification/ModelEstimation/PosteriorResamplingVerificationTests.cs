using Numerics.Data.Statistics;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Xml.Linq;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Verification.ModelEstimation;

/// <summary>
/// Independently oracled verification of cross-analysis posterior resampling.
/// </summary>
/// <remarks>
/// The fixtures intentionally align retained posterior values by raw index. Independent
/// product-posterior targets are calculated without calling the implementation's index
/// helper, while the former raw-paired policy is retained as a negative control.
/// </remarks>
[TestClass]
public class PosteriorResamplingVerificationTests
{
    private const int RetainedDrawCount = 5000;
    private const int SupportCount = 100;
    private const double CredibleIntervalWidth = 0.50d;
    private const double MeanTolerance = 0.02d;
    private const double CredibleLimitTolerance = 0.05d;
    private const double RawPairingMinimumMiss = 0.10d;

    /// <summary>
    /// Verifies Composite posterior summaries against the empirical Cartesian product of
    /// two deliberately raw-aligned retained chains.
    /// </summary>
    /// <returns>A task that completes after all order variants have been verified.</returns>
    [TestMethod]
    public async Task CompositePosteriorResampling_MatchesIndependentCartesianOracle()
    {
        double[] firstSupport = EvenlySpaced(10d, 14d, SupportCount);
        double[] secondSupport = EvenlySpaced(11d, 15d, SupportCount);
        const double sigma = 0.20d;
        const double nonexceedanceProbability = 0.50d;
        PosteriorSummary oracle = BuildCompositeCartesianOracle(
            firstSupport,
            secondSupport,
            sigma,
            nonexceedanceProbability,
            CredibleIntervalWidth);
        PosteriorSummary rawPaired = BuildCompositeRawPairedSummary(
            firstSupport,
            secondSupport,
            sigma,
            nonexceedanceProbability,
            CredibleIntervalWidth);

        PosteriorSummary original = await RunCompositeAsync(firstSupport, secondSupport, false, false);
        PosteriorSummary reversed = await RunCompositeAsync(firstSupport, secondSupport, true, false);
        PosteriorSummary swapped = await RunCompositeAsync(firstSupport, secondSupport, false, true);

        AssertPosteriorSummaryMatches(original, oracle, "original Composite order");
        AssertPosteriorSummaryMatches(reversed, oracle, "reversed second chain");
        AssertPosteriorSummaryMatches(swapped, oracle, "swapped Composite children");
        double rawMiss = MaximumSummaryDifference(rawPaired, oracle);
        Assert.IsTrue(rawMiss >= RawPairingMinimumMiss,
            $"Raw-index Composite pairing missed its independent target by only {rawMiss:F6}; " +
            $"the verification requires at least {RawPairingMinimumMiss:F2}.");
        Assert.IsTrue(MaximumSummaryDifference(original, reversed) > 1E-12,
            "Reversing a retained chain should change the finite seeded sample, not its target distribution.");
        Assert.IsTrue(MaximumSummaryDifference(original, swapped) > 1E-12,
            "Swapping commutative children should change the finite seeded sample, not its target distribution.");
    }

    /// <summary>
    /// Verifies CFA posterior AEP summaries against the closed-form independent posterior
    /// of a sum of two Normal marginals.
    /// </summary>
    /// <returns>A task that completes after both chain-order variants have been verified.</returns>
    [TestMethod]
    public async Task CoincidentFrequencyPosteriorResampling_MatchesIndependentClosedFormOracle()
    {
        double[] firstSupport = EvenlySpaced(0d, 5d, SupportCount);
        double[] secondSupport = EvenlySpaced(1d, 6d, SupportCount);
        const double marginalSigma = 0.70d;

        CoincidentFrequencyAnalysis original = await RunCoincidentFrequencyAsync(
            firstSupport,
            secondSupport,
            marginalSigma,
            reverseSecondChain: false);
        CoincidentFrequencyAnalysis reversed = await RunCoincidentFrequencyAsync(
            firstSupport,
            secondSupport,
            marginalSigma,
            reverseSecondChain: true);

        double maxOriginalMeanError = 0d;
        double maxReversedMeanError = 0d;
        double maxOriginalLimitError = 0d;
        double maxReversedLimitError = 0d;
        double maxRawMeanMiss = 0d;
        for (int bin = 0; bin < original.ZOutputValues!.Length; bin++)
        {
            double z = original.ZOutputValues[bin];
            PosteriorSummary oracle = BuildCfaClosedFormOracle(
                firstSupport,
                secondSupport,
                marginalSigma,
                z,
                CredibleIntervalWidth);
            double rawMean = BuildCfaRawPairedMean(firstSupport, secondSupport, marginalSigma, z);

            maxOriginalMeanError = Math.Max(
                maxOriginalMeanError,
                Math.Abs(original.AnalysisResults!.MeanCurve![bin] - oracle.Mean));
            maxReversedMeanError = Math.Max(
                maxReversedMeanError,
                Math.Abs(reversed.AnalysisResults!.MeanCurve![bin] - oracle.Mean));
            maxOriginalLimitError = Math.Max(
                maxOriginalLimitError,
                Math.Max(
                    Math.Abs(original.AnalysisResults!.ConfidenceIntervals![bin, 0] - oracle.Lower),
                    Math.Abs(original.AnalysisResults.ConfidenceIntervals[bin, 1] - oracle.Upper)));
            maxReversedLimitError = Math.Max(
                maxReversedLimitError,
                Math.Max(
                    Math.Abs(reversed.AnalysisResults!.ConfidenceIntervals![bin, 0] - oracle.Lower),
                    Math.Abs(reversed.AnalysisResults.ConfidenceIntervals[bin, 1] - oracle.Upper)));
            maxRawMeanMiss = Math.Max(maxRawMeanMiss, Math.Abs(rawMean - oracle.Mean));
        }

        Assert.IsTrue(maxOriginalMeanError <= MeanTolerance,
            $"Original-order CFA maximum posterior-mean error {maxOriginalMeanError:F6} exceeded {MeanTolerance:F2}.");
        Assert.IsTrue(maxReversedMeanError <= MeanTolerance,
            $"Reversed-order CFA maximum posterior-mean error {maxReversedMeanError:F6} exceeded {MeanTolerance:F2}.");
        Assert.IsTrue(maxOriginalLimitError <= CredibleLimitTolerance,
            $"Original-order CFA maximum credible-limit error {maxOriginalLimitError:F6} exceeded {CredibleLimitTolerance:F2}.");
        Assert.IsTrue(maxReversedLimitError <= CredibleLimitTolerance,
            $"Reversed-order CFA maximum credible-limit error {maxReversedLimitError:F6} exceeded {CredibleLimitTolerance:F2}.");
        Assert.IsTrue(maxRawMeanMiss >= RawPairingMinimumMiss,
            $"Raw-index CFA pairing missed its closed-form independent target by only {maxRawMeanMiss:F6}; " +
            $"the verification requires at least {RawPairingMinimumMiss:F2}.");
        Assert.IsTrue(original.AnalysisResults!.MeanCurve!
            .Where((value, index) => Math.Abs(value - reversed.AnalysisResults!.MeanCurve![index]) > 1E-12)
            .Any(),
            "Reversing a retained chain should change the finite seeded CFA sample, not its target distribution.");
    }

    /// <summary>
    /// Runs one Composite order variant over repeated empirical support points.
    /// </summary>
    /// <param name="firstSupport">The first marginal posterior support.</param>
    /// <param name="secondSupport">The second marginal posterior support.</param>
    /// <param name="reverseSecondChain">Whether to reverse the second retained chain.</param>
    /// <param name="swapChildren">Whether to swap the commutative Composite children.</param>
    /// <returns>The Composite posterior summary at AEP 0.5.</returns>
    private static async Task<PosteriorSummary> RunCompositeAsync(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        bool reverseSecondChain,
        bool swapChildren)
    {
        double[] firstDraws = RepeatSupport(firstSupport, RetainedDrawCount, false);
        double[] secondDraws = RepeatSupport(secondSupport, RetainedDrawCount, reverseSecondChain);
        UnivariateAnalysis first = CreateNormalChild(firstDraws, firstSupport.Average(), 0.20d);
        UnivariateAnalysis second = CreateNormalChild(secondDraws, secondSupport.Average(), 0.20d);
        var composite = new CompositeAnalysis
        {
            CompositeDistributionType = CompositeType.CompetingRisks,
            IsMaximum = true
        };
        composite.ProbabilityOrdinates.Clear();
        composite.ProbabilityOrdinates.Add(0.50d);
        composite.BayesianAnalysis.CredibleIntervalWidth = CredibleIntervalWidth;
        composite.BayesianAnalysis.PRNGSeed = 20260803;
        if (swapChildren)
        {
            composite.Analyses.Add(new WeightedUnivariateAnalysis(second, 0.5d));
            composite.Analyses.Add(new WeightedUnivariateAnalysis(first, 0.5d));
        }
        else
        {
            composite.Analyses.Add(new WeightedUnivariateAnalysis(first, 0.5d));
            composite.Analyses.Add(new WeightedUnivariateAnalysis(second, 0.5d));
        }

        await composite.CreateFrequencyAnalysisResultsAsync();
        Assert.IsNotNull(composite.AnalysisResults);
        return new PosteriorSummary(
            composite.AnalysisResults.MeanCurve![0],
            composite.AnalysisResults.ConfidenceIntervals![0, 0],
            composite.AnalysisResults.ConfidenceIntervals[0, 1]);
    }

    /// <summary>
    /// Creates an estimated Normal child from an externally supplied retained mean chain.
    /// </summary>
    /// <param name="posteriorMeans">The retained Normal means.</param>
    /// <param name="pointMean">The point-estimate mean.</param>
    /// <param name="sigma">The fixed Normal standard deviation.</param>
    /// <returns>An estimated univariate child analysis.</returns>
    private static UnivariateAnalysis CreateNormalChild(
        IReadOnlyList<double> posteriorMeans,
        double pointMean,
        double sigma)
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([pointMean - sigma, pointMean, pointMean + sigma])
        };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        model.SetParameterValues([pointMean, sigma]);
        var stub = new UnivariateAnalysis(model);
        XElement state = stub.ToXElement();
        state.SetAttributeValue(nameof(AnalysisBase.IsEstimated), true);
        var output = posteriorMeans
            .Select(mean => new ParameterSet([mean, sigma], 0d))
            .ToList();
        var mcmcResults = new MCMCResults(
            new ParameterSet([pointMean, sigma], 0d),
            output,
            1d - CredibleIntervalWidth);
        return new UnivariateAnalysis(model, state, mcmcResults, new UncertaintyAnalysisResults());
    }

    /// <summary>
    /// Calculates the independent Cartesian Composite target.
    /// </summary>
    /// <param name="firstSupport">The first posterior support.</param>
    /// <param name="secondSupport">The second posterior support.</param>
    /// <param name="sigma">The common Normal standard deviation.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <param name="credibleWidth">The central credible interval width.</param>
    /// <returns>The Cartesian posterior mean-curve ordinate and quantile limits.</returns>
    private static PosteriorSummary BuildCompositeCartesianOracle(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        double sigma,
        double probability,
        double credibleWidth)
    {
        double meanCurve = SolveMonotoneProbability(
            z => MeanNormalCdf(firstSupport, sigma, z) * MeanNormalCdf(secondSupport, sigma, z),
            probability,
            firstSupport.Min() - 8d * sigma,
            secondSupport.Max() + 8d * sigma);
        var quantiles = new double[firstSupport.Count * secondSupport.Count];
        int outputIndex = 0;
        foreach (double firstMean in firstSupport)
        {
            foreach (double secondMean in secondSupport)
            {
                quantiles[outputIndex++] = MaximumNormalQuantile(
                    firstMean,
                    secondMean,
                    sigma,
                    probability);
            }
        }

        Array.Sort(quantiles);
        double alpha = 1d - credibleWidth;
        return new PosteriorSummary(
            meanCurve,
            Statistics.Percentile(quantiles, alpha / 2d, true),
            Statistics.Percentile(quantiles, 1d - alpha / 2d, true));
    }

    /// <summary>
    /// Calculates the former raw-index Composite result as a negative control.
    /// </summary>
    /// <param name="firstSupport">The first aligned posterior support.</param>
    /// <param name="secondSupport">The second aligned posterior support.</param>
    /// <param name="sigma">The common Normal standard deviation.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <param name="credibleWidth">The central credible interval width.</param>
    /// <returns>The raw-paired posterior summary.</returns>
    private static PosteriorSummary BuildCompositeRawPairedSummary(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        double sigma,
        double probability,
        double credibleWidth)
    {
        double meanCurve = SolveMonotoneProbability(
            z => Enumerable.Range(0, firstSupport.Count)
                .Average(index => NormalCdf(z, firstSupport[index], sigma) *
                    NormalCdf(z, secondSupport[index], sigma)),
            probability,
            firstSupport.Min() - 8d * sigma,
            secondSupport.Max() + 8d * sigma);
        double[] quantiles = Enumerable.Range(0, firstSupport.Count)
            .Select(index => MaximumNormalQuantile(
                firstSupport[index],
                secondSupport[index],
                sigma,
                probability))
            .ToArray();
        Array.Sort(quantiles);
        double alpha = 1d - credibleWidth;
        return new PosteriorSummary(
            meanCurve,
            Statistics.Percentile(quantiles, alpha / 2d, true),
            Statistics.Percentile(quantiles, 1d - alpha / 2d, true));
    }

    /// <summary>
    /// Runs CFA with one retained-chain ordering and a sum response surface.
    /// </summary>
    /// <param name="firstSupport">The X-marginal posterior support.</param>
    /// <param name="secondSupport">The Y-marginal posterior support.</param>
    /// <param name="sigma">The common marginal standard deviation.</param>
    /// <param name="reverseSecondChain">Whether to reverse the Y retained chain.</param>
    /// <returns>The completed CFA.</returns>
    private static async Task<CoincidentFrequencyAnalysis> RunCoincidentFrequencyAsync(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        double sigma,
        bool reverseSecondChain)
    {
        double[] firstDraws = RepeatSupport(firstSupport, RetainedDrawCount, false);
        double[] secondDraws = RepeatSupport(secondSupport, RetainedDrawCount, reverseSecondChain);
        MCMCResults copulaResults = CreateMcmcResults(
            Enumerable.Repeat(new[] { 0d }, RetainedDrawCount).ToArray(),
            [0d]);
        MCMCResults marginalXResults = CreateMcmcResults(
            firstDraws.Select(mean => new[] { mean, sigma }).ToArray(),
            [firstSupport.Average(), sigma]);
        MCMCResults marginalYResults = CreateMcmcResults(
            secondDraws.Select(mean => new[] { mean, sigma }).ToArray(),
            [secondSupport.Average(), sigma]);

        var marginalX = CreateNormalModel(firstSupport.Average(), sigma);
        var marginalY = CreateNormalModel(secondSupport.Average(), sigma);
        var model = new BivariateDistribution(marginalX, marginalY, CopulaType.Normal);
        model.Copula.SetCopulaParameters([0d]);
        var stub = new BivariateAnalysis(model);
        XElement state = stub.ToXElement();
        state.SetAttributeValue(nameof(AnalysisBase.IsEstimated), true);
        var bivariate = new BivariateAnalysis(
            model,
            state,
            copulaResults,
            new UncertaintyAnalysisResults());

        double[] grid = EvenlySpaced(-5d, 12d, 35);
        var response = new double[grid.Length, grid.Length];
        for (int x = 0; x < grid.Length; x++)
        {
            for (int y = 0; y < grid.Length; y++)
                response[x, y] = grid[x] + grid[y];
        }

        var cfa = new CoincidentFrequencyAnalysis(bivariate, grid, grid, response)
        {
            NumberOfBins = 33,
            MarginalXChain = marginalXResults,
            MarginalYChain = marginalYResults
        };
        cfa.BayesianAnalysis.CredibleIntervalWidth = CredibleIntervalWidth;
        cfa.BayesianAnalysis.PRNGSeed = 20260803;
        await cfa.RunAsync();
        Assert.IsTrue(cfa.IsEstimated);
        Assert.IsNotNull(cfa.AnalysisResults);
        Assert.IsNotNull(cfa.ZOutputValues);
        return cfa;
    }

    /// <summary>
    /// Creates a Normal univariate model at a fixed point estimate.
    /// </summary>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="sigma">The Normal standard deviation.</param>
    /// <returns>The configured univariate model.</returns>
    private static UnivariateDistribution CreateNormalModel(double mean, double sigma)
    {
        var dataFrame = new BestFitDataFrame
        {
            ExactSeries = new ExactSeries([mean - sigma, mean, mean + sigma])
        };
        var model = new UnivariateDistribution(dataFrame, UnivariateDistributionType.Normal);
        model.SetParameterValues([mean, sigma]);
        return model;
    }

    /// <summary>
    /// Creates synthetic MCMC results from explicit parameter vectors.
    /// </summary>
    /// <param name="values">The retained parameter vectors.</param>
    /// <param name="map">The MAP parameter vector.</param>
    /// <returns>The synthetic results.</returns>
    private static MCMCResults CreateMcmcResults(
        IReadOnlyList<double[]> values,
        double[] map)
    {
        var output = values.Select(parameters => new ParameterSet(parameters, 0d)).ToList();
        return new MCMCResults(new ParameterSet(map, 0d), output, 1d - CredibleIntervalWidth);
    }

    /// <summary>
    /// Calculates the closed-form product-posterior CFA summary at one response ordinate.
    /// </summary>
    /// <param name="firstSupport">The X posterior-mean support.</param>
    /// <param name="secondSupport">The Y posterior-mean support.</param>
    /// <param name="sigma">The common marginal standard deviation.</param>
    /// <param name="z">The response ordinate.</param>
    /// <param name="credibleWidth">The central credible interval width.</param>
    /// <returns>The mean AEP and posterior AEP limits.</returns>
    private static PosteriorSummary BuildCfaClosedFormOracle(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        double sigma,
        double z,
        double credibleWidth)
    {
        double sumSigma = Math.Sqrt(2d) * sigma;
        var aeps = new double[firstSupport.Count * secondSupport.Count];
        int outputIndex = 0;
        foreach (double firstMean in firstSupport)
        {
            foreach (double secondMean in secondSupport)
                aeps[outputIndex++] = 1d - NormalCdf(z, firstMean + secondMean, sumSigma);
        }

        double mean = aeps.Average();
        Array.Sort(aeps);
        double alpha = 1d - credibleWidth;
        return new PosteriorSummary(
            mean,
            Statistics.Percentile(aeps, alpha / 2d, true),
            Statistics.Percentile(aeps, 1d - alpha / 2d, true));
    }

    /// <summary>
    /// Calculates the former raw-index CFA posterior mean AEP as a negative control.
    /// </summary>
    /// <param name="firstSupport">The aligned X posterior support.</param>
    /// <param name="secondSupport">The aligned Y posterior support.</param>
    /// <param name="sigma">The common marginal standard deviation.</param>
    /// <param name="z">The response ordinate.</param>
    /// <returns>The raw-paired posterior mean AEP.</returns>
    private static double BuildCfaRawPairedMean(
        IReadOnlyList<double> firstSupport,
        IReadOnlyList<double> secondSupport,
        double sigma,
        double z)
    {
        double sumSigma = Math.Sqrt(2d) * sigma;
        return Enumerable.Range(0, firstSupport.Count)
            .Average(index => 1d - NormalCdf(
                z,
                firstSupport[index] + secondSupport[index],
                sumSigma));
    }

    /// <summary>
    /// Computes the quantile of the maximum of two independent Normal variables.
    /// </summary>
    /// <param name="firstMean">The first Normal mean.</param>
    /// <param name="secondMean">The second Normal mean.</param>
    /// <param name="sigma">The common standard deviation.</param>
    /// <param name="probability">The requested nonexceedance probability.</param>
    /// <returns>The maximum-distribution quantile.</returns>
    private static double MaximumNormalQuantile(
        double firstMean,
        double secondMean,
        double sigma,
        double probability)
    {
        return SolveMonotoneProbability(
            z => NormalCdf(z, firstMean, sigma) * NormalCdf(z, secondMean, sigma),
            probability,
            Math.Min(firstMean, secondMean) - 8d * sigma,
            Math.Max(firstMean, secondMean) + 8d * sigma);
    }

    /// <summary>
    /// Solves a continuous monotone probability function by bisection.
    /// </summary>
    /// <param name="cdf">The monotone probability function.</param>
    /// <param name="probability">The target probability.</param>
    /// <param name="lower">A lower bracketing ordinate.</param>
    /// <param name="upper">An upper bracketing ordinate.</param>
    /// <returns>The solved ordinate.</returns>
    private static double SolveMonotoneProbability(
        Func<double, double> cdf,
        double probability,
        double lower,
        double upper)
    {
        for (int iteration = 0; iteration < 100; iteration++)
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
    /// Computes the average Normal CDF over a discrete posterior support.
    /// </summary>
    /// <param name="support">The posterior mean support.</param>
    /// <param name="sigma">The fixed standard deviation.</param>
    /// <param name="z">The evaluation ordinate.</param>
    /// <returns>The mean conditional CDF.</returns>
    private static double MeanNormalCdf(IReadOnlyList<double> support, double sigma, double z)
    {
        return support.Average(mean => NormalCdf(z, mean, sigma));
    }

    /// <summary>
    /// Evaluates a Normal CDF.
    /// </summary>
    /// <param name="z">The evaluation ordinate.</param>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="sigma">The Normal standard deviation.</param>
    /// <returns>The nonexceedance probability.</returns>
    private static double NormalCdf(double z, double mean, double sigma)
    {
        return new Normal(mean, sigma).CDF(z);
    }

    /// <summary>
    /// Creates an evenly spaced inclusive support.
    /// </summary>
    /// <param name="minimum">The minimum support value.</param>
    /// <param name="maximum">The maximum support value.</param>
    /// <param name="count">The number of support values.</param>
    /// <returns>The support values.</returns>
    private static double[] EvenlySpaced(double minimum, double maximum, int count)
    {
        var values = new double[count];
        for (int index = 0; index < count; index++)
            values[index] = minimum + (maximum - minimum) * index / (count - 1d);
        return values;
    }

    /// <summary>
    /// Repeats a discrete support to the requested retained-output count.
    /// </summary>
    /// <param name="support">The support to repeat.</param>
    /// <param name="count">The requested output count.</param>
    /// <param name="reverse">Whether to reverse the resulting retained chain.</param>
    /// <returns>The retained posterior values.</returns>
    private static double[] RepeatSupport(
        IReadOnlyList<double> support,
        int count,
        bool reverse)
    {
        var values = new double[count];
        for (int index = 0; index < count; index++)
            values[index] = support[index % support.Count];
        if (reverse) Array.Reverse(values);
        return values;
    }

    /// <summary>
    /// Verifies a computed summary against its independent oracle tolerances.
    /// </summary>
    /// <param name="actual">The computed summary.</param>
    /// <param name="expected">The independent oracle.</param>
    /// <param name="label">The order-variant label.</param>
    private static void AssertPosteriorSummaryMatches(
        PosteriorSummary actual,
        PosteriorSummary expected,
        string label)
    {
        Assert.AreEqual(expected.Mean, actual.Mean, MeanTolerance,
            $"{label} posterior mean did not match the Cartesian oracle.");
        Assert.AreEqual(expected.Lower, actual.Lower, CredibleLimitTolerance,
            $"{label} lower credible limit did not match the Cartesian oracle.");
        Assert.AreEqual(expected.Upper, actual.Upper, CredibleLimitTolerance,
            $"{label} upper credible limit did not match the Cartesian oracle.");
    }

    /// <summary>
    /// Returns the maximum absolute component difference between two summaries.
    /// </summary>
    /// <param name="first">The first summary.</param>
    /// <param name="second">The second summary.</param>
    /// <returns>The maximum component difference.</returns>
    private static double MaximumSummaryDifference(PosteriorSummary first, PosteriorSummary second)
    {
        return Math.Max(
            Math.Abs(first.Mean - second.Mean),
            Math.Max(
                Math.Abs(first.Lower - second.Lower),
                Math.Abs(first.Upper - second.Upper)));
    }

    /// <summary>
    /// Stores one posterior mean ordinate and its central credible limits.
    /// </summary>
    /// <param name="Mean">The posterior mean ordinate.</param>
    /// <param name="Lower">The lower credible limit.</param>
    /// <param name="Upper">The upper credible limit.</param>
    private sealed record PosteriorSummary(double Mean, double Lower, double Upper);
}
