using System.Reflection;
using Numerics.Data;
using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using Numerics.Mathematics.Optimization;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using NumericTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.TimeSeriesAnalysis;

/// <summary>
/// Deterministic routing regressions for analysis AIC/BIC evaluation at the stored MAP.
/// </summary>
[TestClass]
public class AnalysisInformationCriteriaRoutingTests
{
    /// <summary>
    /// Verifies a time-series analysis calls the model's data likelihood exactly once at MAP,
    /// excludes the posterior/prior paths, and routes that value to both criteria.
    /// </summary>
    [TestMethod]
    public async Task TimeSeriesCriteria_UseOneDataLikelihoodCallAtMap()
    {
        const double dataLogLikelihood = -12.5;
        double[] mapValues = { 10.0, 1.5 };
        var model = new CountingAutoRegressive(CreateSeries(), dataLogLikelihood)
        {
            Order = 0,
            IncludeIntercept = true,
        };
        model.SetDefaultParameters();
        var analysis = new ARAnalysis(model);
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(
                new ParameterSet(mapValues, dataLogLikelihood + 7.0),
                new List<ParameterSet>
                {
                    new(new[] { 9.0, 1.2 }, -20.0),
                    new(new[] { 11.0, 1.8 }, -19.0),
                },
                alpha: 0.1),
            skipInformationCriteria: true);
        SetAnalysisResults(analysis, new UncertaintyAnalysisResults());

        await analysis.UpdatePointEstimateResultsAsync();

        Assert.AreEqual(1, model.DataLikelihoodCallCount);
        Assert.AreEqual(0, model.PriorLikelihoodCallCount);
        Assert.AreEqual(0, model.PosteriorLikelihoodCallCount);
        CollectionAssert.AreEqual(mapValues, model.LastDataLikelihoodParameters);
        Assert.IsNotNull(analysis.AnalysisResults);
        Assert.AreEqual(
            -2.0 * dataLogLikelihood + 2.0 * model.NumberOfParameters,
            analysis.AnalysisResults.AIC,
            1E-12);
        Assert.AreEqual(
            -2.0 * dataLogLikelihood + model.NumberOfParameters * Math.Log(model.TimeSeries.Count),
            analysis.AnalysisResults.BIC,
            1E-12);
    }

    /// <summary>
    /// Verifies a bivariate analysis calls the copula data likelihood exactly once at MAP,
    /// excludes the posterior/prior paths, and routes that value to both criteria (TR-047).
    /// </summary>
    [TestMethod]
    public async Task BivariateCriteria_UseOneDataLikelihoodCallAtMap()
    {
        const double dataLogLikelihood = -8.25;
        double[] mapValues = { 0.6 };
        CountingBivariateDistribution model = CreateCountingBivariate(dataLogLikelihood);
        var analysis = new BivariateAnalysis(model);
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        analysis.BayesianAnalysis.SetCustomMCMCResults(
            new MCMCResults(
                new ParameterSet(mapValues, dataLogLikelihood + 7.0),
                // Six retained draws keep the kernel-density posterior mean finite (a two-draw
                // output yields a NaN mean), so the default posterior-mean route is exercised.
                new List<ParameterSet>
                {
                    new(new[] { 0.50 }, -16.0),
                    new(new[] { 0.55 }, -15.0),
                    new(new[] { 0.58 }, -14.5),
                    new(new[] { 0.62 }, -14.5),
                    new(new[] { 0.65 }, -14.0),
                    new(new[] { 0.70 }, -16.0),
                },
                alpha: 0.1),
            skipInformationCriteria: true);
        SetAnalysisResults(analysis, new UncertaintyAnalysisResults());

        await analysis.UpdatePointEstimateResultsAsync();

        Assert.AreEqual(1, model.DataLikelihoodCallCount);
        Assert.AreEqual(0, model.PriorLikelihoodCallCount);
        Assert.AreEqual(0, model.PosteriorLikelihoodCallCount);
        CollectionAssert.AreEqual(mapValues, model.LastDataLikelihoodParameters);
        Assert.IsNotNull(analysis.AnalysisResults);
        int matchedPairs = model.MarginalX.DataFrame.ExactSeries.Count;
        int copulaParameters = model.Copula.NumberOfCopulaParameters;
        Assert.AreEqual(
            -2.0 * dataLogLikelihood + 2.0 * copulaParameters,
            analysis.AnalysisResults.AIC,
            1E-12);
        Assert.AreEqual(
            -2.0 * dataLogLikelihood + copulaParameters * Math.Log(matchedPairs),
            analysis.AnalysisResults.BIC,
            1E-12);
    }

    /// <summary>
    /// Assigns a deterministic result container through the analysis's private setter.
    /// </summary>
    /// <param name="analysis">The analysis under test.</param>
    /// <param name="results">The empty result container.</param>
    private static void SetAnalysisResults(ARAnalysis analysis, UncertaintyAnalysisResults results)
    {
        PropertyInfo property = typeof(ARAnalysis).GetProperty(nameof(ARAnalysis.AnalysisResults))!;
        property.GetSetMethod(nonPublic: true)!.Invoke(analysis, new object[] { results });
    }

    /// <summary>
    /// Assigns a deterministic result container through the bivariate analysis's private setter.
    /// </summary>
    /// <param name="analysis">The analysis under test.</param>
    /// <param name="results">The empty result container.</param>
    private static void SetAnalysisResults(BivariateAnalysis analysis, UncertaintyAnalysisResults results)
    {
        PropertyInfo property = typeof(BivariateAnalysis).GetProperty(nameof(BivariateAnalysis.AnalysisResults))!;
        property.GetSetMethod(nonPublic: true)!.Invoke(analysis, new object[] { results });
    }

    /// <summary>
    /// Creates a Gaussian-copula bivariate probe with six matched Normal-marginal observations and
    /// an informative copula prior.
    /// </summary>
    /// <param name="dataLogLikelihood">The fixed data-only log likelihood the probe returns.</param>
    /// <returns>The counting bivariate probe.</returns>
    private static CountingBivariateDistribution CreateCountingBivariate(double dataLogLikelihood)
    {
        var dataFrameX = new RMC.BestFit.Models.DataFrame { ExactSeries = new ExactSeries(new[] { 9.0, 11, 10, 12, 8, 10 }) };
        var dataFrameY = new RMC.BestFit.Models.DataFrame { ExactSeries = new ExactSeries(new[] { 20.0, 24, 21, 26, 18, 22 }) };
        dataFrameX.CalculatePlottingPositions();
        dataFrameY.CalculatePlottingPositions();
        var marginalX = new UnivariateDistribution(dataFrameX, UnivariateDistributionType.Normal);
        var marginalY = new UnivariateDistribution(dataFrameY, UnivariateDistributionType.Normal);
        marginalX.SetParameterValues(new[] { 10.0, 1.5 });
        marginalY.SetParameterValues(new[] { 21.8, 2.8 });
        var model = new CountingBivariateDistribution(marginalX, marginalY, dataLogLikelihood);
        model.Parameters[0].PriorDistribution = new Normal(0.5, 0.1);
        return model;
    }

    /// <summary>
    /// Creates a short exact daily response fixture.
    /// </summary>
    /// <returns>The response series.</returns>
    private static NumericTimeSeries CreateSeries()
    {
        return new NumericTimeSeries(
            TimeInterval.OneDay,
            new DateTime(2002, 3, 4),
            new[] { 9.0, 11, 10, 12, 8, 10 });
    }

    /// <summary>
    /// AutoRegressive probe that independently counts criterion-routing calls.
    /// </summary>
    private sealed class CountingAutoRegressive : AutoRegressive
    {
        private readonly double _dataLogLikelihood;

        /// <summary>
        /// Initializes the probe with fixed response dates and data likelihood.
        /// </summary>
        /// <param name="series">The response series.</param>
        /// <param name="dataLogLikelihood">The fixed data-only log likelihood.</param>
        public CountingAutoRegressive(NumericTimeSeries series, double dataLogLikelihood)
            : base(series, order: 0, includeIntercept: true)
        {
            _dataLogLikelihood = dataLogLikelihood;
        }

        /// <summary>Gets the data-likelihood call count.</summary>
        public int DataLikelihoodCallCount { get; private set; }

        /// <summary>Gets the prior-likelihood call count.</summary>
        public int PriorLikelihoodCallCount { get; private set; }

        /// <summary>Gets the posterior-likelihood call count.</summary>
        public int PosteriorLikelihoodCallCount { get; private set; }

        /// <summary>Gets the parameters supplied to the latest data-likelihood call.</summary>
        public double[]? LastDataLikelihoodParameters { get; private set; }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            DataLikelihoodCallCount++;
            LastDataLikelihoodParameters = (double[])parameters.Clone();
            return _dataLogLikelihood;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            PriorLikelihoodCallCount++;
            return 7.0;
        }

        /// <inheritdoc/>
        public override double LogLikelihood(double[] parameters)
        {
            PosteriorLikelihoodCallCount++;
            return _dataLogLikelihood + 7.0;
        }
    }

    /// <summary>
    /// Gaussian-copula bivariate probe that independently counts criterion-routing calls.
    /// </summary>
    private sealed class CountingBivariateDistribution : BivariateDistribution
    {
        private readonly double _dataLogLikelihood;

        /// <summary>
        /// Initializes the probe with fixed marginals and data likelihood.
        /// </summary>
        /// <param name="marginalX">The X marginal distribution.</param>
        /// <param name="marginalY">The Y marginal distribution.</param>
        /// <param name="dataLogLikelihood">The fixed data-only log likelihood.</param>
        public CountingBivariateDistribution(UnivariateDistribution marginalX, UnivariateDistribution marginalY, double dataLogLikelihood)
            : base(marginalX, marginalY, CopulaType.Normal)
        {
            _dataLogLikelihood = dataLogLikelihood;
        }

        /// <summary>Gets the data-likelihood call count.</summary>
        public int DataLikelihoodCallCount { get; private set; }

        /// <summary>Gets the prior-likelihood call count.</summary>
        public int PriorLikelihoodCallCount { get; private set; }

        /// <summary>Gets the posterior-likelihood call count.</summary>
        public int PosteriorLikelihoodCallCount { get; private set; }

        /// <summary>Gets the parameters supplied to the latest data-likelihood call.</summary>
        public double[]? LastDataLikelihoodParameters { get; private set; }

        /// <inheritdoc/>
        public override double DataLogLikelihood(double[] parameters)
        {
            DataLikelihoodCallCount++;
            LastDataLikelihoodParameters = (double[])parameters.Clone();
            return _dataLogLikelihood;
        }

        /// <inheritdoc/>
        public override double PriorLogLikelihood(double[] parameters)
        {
            PriorLikelihoodCallCount++;
            return 7.0;
        }

        /// <inheritdoc/>
        public override double LogLikelihood(double[] parameters)
        {
            PosteriorLikelihoodCallCount++;
            return _dataLogLikelihood + 7.0;
        }
    }
}
