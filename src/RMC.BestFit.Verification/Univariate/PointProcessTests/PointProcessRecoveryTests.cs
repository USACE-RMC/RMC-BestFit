using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Verifies point-process recovery and simulation against independently implemented analytical
/// Poisson and generalized-Pareto constructions.
/// </summary>
/// <remarks>
/// Recovery methods run Bayesian MCMC and exercise the production fixed-size Poisson-GPA
/// generators. References: Stedinger et al. (1993), Madsen et al. (1997), Coles (2001),
/// chapters 5 and 7, and Coles and Pericchi (2003).
/// </remarks>
[TestClass]
public partial class PointProcessRecoveryTests
{
    private const double LocationOne = 100.0;
    private const double ScaleOne = 20.0;
    private const double ColesShapeOne = 0.10;

    private const double ColesShapeTwo = -0.05;
    private const double Threshold = 80.0;
    private const double SeasonalGpaScaleOne = 12.0;
    private const double SeasonalGpaScaleTwo = 40.0;
    private const double SeasonalLambda = 8.0;
    private const int TrueK1 = 80;
    private const int TrueK2 = 260;

    /// <summary>
    /// Verifies recovery of a nonseasonal parent from the production Poisson-GPA generator.
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    [TestMethod]
    public async Task Test_NonSeasonalProductionGenerator_RecoversParent()
    {
        const int sampleSize = 1000;
        double lambda = ThresholdIntensity(LocationOne, ScaleOne, ColesShapeOne, Threshold);
        PointProcessModel parent = CreateNonSeasonalParentModel(lambda);
        double[] generatedSample = parent.GenerateRandomValues(sampleSize, 41001);
        double observationYears = sampleSize / lambda;
        DataFrame frame = CreateNonSeasonalRecoveryFrame(generatedSample, observationYears);
        PointProcessModel model = CreateNonSeasonalModel(frame, observationYears);
        PointProcessAnalysis analysis = ConfigureAnalysis(model, 41002);

        await analysis.RunAsync();

        Assert.AreEqual(sampleSize, generatedSample.Length, "The production generator did not return the requested POT sample size.");
        Assert.IsTrue(analysis.IsEstimated, "Nonseasonal point-process recovery did not complete.");
        double[] posteriorMean = analysis.BayesianAnalysis.Results!.PosteriorMean.Values;
        Assert.AreEqual(LocationOne, posteriorMean[0], 8.0, "Location was not recovered.");
        Assert.AreEqual(ScaleOne, posteriorMean[1], 5.0, "Scale was not recovered.");
        Assert.AreEqual(-ColesShapeOne, posteriorMean[2], 0.08, "Numerics Kappa was not recovered.");
    }

    /// <summary>
    /// Verifies recovery of both seasonal Poisson-GPA processes and their floored changepoints.
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    [TestMethod]
    public async Task Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints()
    {
        const int sampleSize = 4000;
        PointProcessModel parent = CreateSeasonalParentModel();
        TimeSeries generatedSample = parent.GeneratePOTTimeSeries(sampleSize, 42001);
        double observationYears = sampleSize / SeasonalLambda;
        DataFrame frame = CreateSeasonalRecoveryFrame(generatedSample, observationYears);
        PointProcessModel model = CreateSeasonalModel(frame, observationYears);
        PointProcessAnalysis analysis = ConfigureAnalysis(model, 42002);
        Exception? analysisError = null;
        analysis.AnalysisCompleted += (_, args) => analysisError = args.Error;

        var validation = analysis.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));

        double[] trueParameters = parent.Parameters.Select(parameter => parameter.Value).ToArray();
        double trueLogLikelihood = model.DataLogLikelihood(trueParameters);
        double[] collapsedParameters = (double[])trueParameters.Clone();
        collapsedParameters[0] = 1.5;
        double collapsedLogLikelihood = model.DataLogLikelihood(collapsedParameters);
        Assert.IsTrue(
            trueLogLikelihood > collapsedLogLikelihood,
            $"The generated sample favored collapsed K1 before estimation: true={trueLogLikelihood:G17}, collapsed={collapsedLogLikelihood:G17}.");

        await analysis.RunAsync();

        Assert.AreEqual(sampleSize, generatedSample.Count, "The production generator did not return the requested seasonal POT sample size.");
        Assert.IsTrue(
            analysis.IsEstimated,
            $"Seasonal point-process recovery did not complete. {analysisError ?? analysis.BayesianAnalysis.LastError}");
        var results = analysis.BayesianAnalysis.Results!;
        double[] posteriorMean = results.PosteriorMean.Values;
        var output = results.Output;
        AssertFlooredChangePointRecovery(output.Select(sample => sample.Values[0]), TrueK1, "K1");
        AssertFlooredChangePointRecovery(output.Select(sample => sample.Values[1]), TrueK2, "K2");

        double[] parentParameters = parent.Parameters.Select(parameter => parameter.Value).ToArray();
        Assert.AreEqual(parentParameters[2], posteriorMean[2], 12.0, "Season-one location was not recovered.");
        Assert.AreEqual(parentParameters[3], posteriorMean[3], 8.0, "Season-one scale was not recovered.");
        Assert.AreEqual(parentParameters[4], posteriorMean[4], 0.12, "Season-one Kappa was not recovered.");
        Assert.AreEqual(parentParameters[5], posteriorMean[5], 12.0, "Season-two location was not recovered.");
        Assert.AreEqual(parentParameters[6], posteriorMean[6], 8.0, "Season-two scale was not recovered.");
        Assert.AreEqual(parentParameters[7], posteriorMean[7], 0.12, "Season-two Kappa was not recovered.");
    }

    /// <summary>
    /// Verifies the nonseasonal production simulator's Poisson rate and analytical conditional tail.
    /// </summary>
    [TestMethod]
    public void Test_NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail()
    {
        const int replicates = 500;
        const double durationYears = 20.0;
        const double tailPoint = 120.0;
        double lambda = ThresholdIntensity(LocationOne, ScaleOne, ColesShapeOne, Threshold);
        PointProcessModel model = CreateNonSeasonalParentModel(lambda);

        int totalCount = 0;
        int tailCount = 0;
        for (int replicate = 0; replicate < replicates; replicate++)
        {
            TimeSeries sample = model.GeneratePOTTimeSeries(new DateTime(2000, 1, 1), durationYears, 43100 + replicate);
            totalCount += sample.Count;
            tailCount += sample.Count(eventValue => eventValue.Value > tailPoint);
        }

        double expectedCount = replicates * durationYears * model.Lambda;
        double countStandardError = Math.Sqrt(expectedCount);
        Assert.AreEqual(expectedCount, totalCount, 5.0 * countStandardError, "Simulated Poisson count missed its analytical Monte Carlo bound.");

        double scaleAtThreshold = ScaleOne + ColesShapeOne * (Threshold - LocationOne);
        double expectedTailProbability = GpaConditionalSurvival(scaleAtThreshold, ColesShapeOne, tailPoint - Threshold);
        double observedTailProbability = tailCount / (double)totalCount;
        double tailStandardError = Math.Sqrt(expectedTailProbability * (1.0 - expectedTailProbability) / totalCount);
        Assert.AreEqual(expectedTailProbability, observedTailProbability, 5.0 * tailStandardError, "Conditional exceedance tail missed its analytical binomial bound.");
    }

    /// <summary>
    /// Verifies seasonal counts, date assignments, and component-specific conditional tails.
    /// </summary>
    [TestMethod]
    public void Test_SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails()
    {
        const int replicates = 500;
        const double durationYears = 20.0;
        const double tailPointOne = 120.0;
        const double tailPointTwo = 165.0;
        PointProcessModel model = CreateSeasonalParentModel();

        int countOne = 0;
        int countTwo = 0;
        int tailOne = 0;
        int tailTwo = 0;
        for (int replicate = 0; replicate < replicates; replicate++)
        {
            TimeSeries sample = model.GeneratePOTTimeSeries(new DateTime(2000, 1, 1), durationYears, 44100 + replicate);
            foreach (SeriesOrdinate<DateTime, double> point in sample)
            {
                int day = point.Index.DayOfYear;
                if (IsSeasonOne(day))
                {
                    countOne++;
                    if (point.Value > tailPointOne) tailOne++;
                }
                else
                {
                    countTwo++;
                    if (point.Value > tailPointTwo) tailTwo++;
                }
            }
        }

        double weightOne = (TrueK1 + 366.0 - TrueK2) / 366.0;
        double weightTwo = (TrueK2 - TrueK1) / 366.0;
        AssertPoissonMonteCarloCount(countOne, replicates * durationYears * weightOne * SeasonalLambda, "season one");
        AssertPoissonMonteCarloCount(countTwo, replicates * durationYears * weightTwo * SeasonalLambda, "season two");
        AssertBinomialTail(tailOne, countOne, GpaConditionalSurvival(SeasonalGpaScaleOne, ColesShapeOne, tailPointOne - Threshold), "season one");
        AssertBinomialTail(tailTwo, countTwo, GpaConditionalSurvival(SeasonalGpaScaleTwo, ColesShapeTwo, tailPointTwo - Threshold), "season two");
    }

    /// <summary>
    /// Verifies the hybrid exact/uncertain/interval/threshold likelihood against a separately coded calculation.
    /// </summary>
    [TestMethod]
    public void Test_MixedObservationLikelihood_MatchesIndependentCalculation()
    {
        var frame = new DataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new ExactData(1, 105.0),
                new ExactData(2, 125.0)
            }),
            UncertainSeries = new UncertainSeries(new List<UncertainData>
            {
                new UncertainData(3, new Normal(115.0, 4.0))
            }),
            IntervalSeries = new IntervalSeries(new List<IntervalData>
            {
                new IntervalData(4, 110.0, 118.0, 126.0)
            }),
            ThresholdSeries = new ThresholdSeries(new List<ThresholdData>
            {
                new ThresholdData(5, 9, 112.0) { NumberAbove = 2 }
            })
        };
        var model = CreateNonSeasonalModel(frame, 10.0);
        double[] parameters = { LocationOne, ScaleOne, -ColesShapeOne };
        model.SetParameterValues(parameters);
        frame.ProcessThresholdSeries();

        double expected = IndependentMixedLogLikelihood(frame, 10.0);
        double actual = model.DataLogLikelihood(parameters);

        Assert.AreEqual(expected, actual, 2E-7, "Mixed point-process likelihood disagreed with the independent calculation.");
        Assert.AreEqual(2, model.EmpiricalEventCount, "Non-exact rows must not become Poisson events.");
    }

    /// <summary>
    /// Verifies that seasonal non-exact records use the annual maximum of the two
    /// exposure-adjusted seasonal processes.
    /// </summary>
    [TestMethod]
    public void Test_SeasonalMixedObservationLikelihood_MatchesIndependentAnnualMaximumCalculation()
    {
        const double observationYears = 10.0;
        const double locationTwo = 130.0;
        const double scaleTwo = 30.0;
        var measurementError = new Normal(125.0, 4.0);
        var frame = new DataFrame
        {
            ExactSeries = new ExactSeries(new List<ExactData>
            {
                new ExactData(new DateTime(2000, 1, 20), 105.0),
                new ExactData(new DateTime(2000, 5, 30), 140.0)
            }),
            UncertainSeries = new UncertainSeries(new List<UncertainData>
            {
                new UncertainData(2001, measurementError)
            }),
            IntervalSeries = new IntervalSeries(new List<IntervalData>
            {
                new IntervalData(2002, 110.0, 130.0, 150.0)
            }),
            ThresholdSeries = new ThresholdSeries(new List<ThresholdData>
            {
                new ThresholdData(2003, 2004, 120.0) { NumberAbove = 2 }
            })
        };
        PointProcessModel model = CreateSeasonalModel(frame, observationYears);
        double[] parameters =
        {
            TrueK1 + 0.5,
            TrueK2 + 0.5,
            LocationOne,
            ScaleOne,
            -ColesShapeOne,
            locationTwo,
            scaleTwo,
            -ColesShapeTwo
        };
        model.SetParameterValues(parameters);
        frame.ProcessThresholdSeries();

        double weightOne = (TrueK1 + 366.0 - TrueK2) / 366.0;
        double weightTwo = (TrueK2 - TrueK1) / 366.0;
        Func<double, double, double, double, double> tailMeasure = (value, location, scale, shape) =>
            Math.Pow(1.0 + shape * (value - location) / scale, -1.0 / shape);
        Func<double, double, double, double, double> intensityDensity = (value, location, scale, shape) =>
            Math.Pow(1.0 + shape * (value - location) / scale, -1.0 / shape - 1.0) / scale;
        Func<double, double> annualCdf = value => Math.Exp(
            -weightOne * tailMeasure(value, LocationOne, ScaleOne, ColesShapeOne) -
            weightTwo * tailMeasure(value, locationTwo, scaleTwo, ColesShapeTwo));
        Func<double, double> annualDensity = value => annualCdf(value) *
            (weightOne * intensityDensity(value, LocationOne, ScaleOne, ColesShapeOne) +
             weightTwo * intensityDensity(value, locationTwo, scaleTwo, ColesShapeTwo));

        double expected =
            -observationYears * weightOne * tailMeasure(Threshold, LocationOne, ScaleOne, ColesShapeOne) -
            observationYears * weightTwo * tailMeasure(Threshold, locationTwo, scaleTwo, ColesShapeTwo) +
            Math.Log(intensityDensity(105.0, LocationOne, ScaleOne, ColesShapeOne)) +
            Math.Log(intensityDensity(140.0, locationTwo, scaleTwo, ColesShapeTwo));
        double lower = measurementError.InverseCDF(1E-8);
        double upper = measurementError.InverseCDF(1.0 - 1E-8);
        expected += Math.Log(SimpsonIntegrate(
            value => NormalDensity(value, measurementError.Mu, measurementError.Sigma) * annualDensity(value),
            lower,
            upper,
            20000) / (1.0 - 2E-8));
        expected += Math.Log(annualCdf(150.0) - annualCdf(110.0));
        expected += 2.0 * Math.Log(1.0 - annualCdf(120.0));

        double actual = model.DataLogLikelihood(parameters);

        Assert.AreEqual(expected, actual, 2E-7, "Seasonal mixed likelihood disagreed with the independently calculated annual maximum distribution.");
        Assert.AreEqual(2, model.EmpiricalEventCount, "Annualized non-exact rows must not become seasonal Poisson events.");
    }

    /// <summary>Configures the shared Bayesian recovery analysis.</summary>
    /// <param name="model">The point-process model.</param>
    /// <param name="seed">The sampler seed.</param>
    /// <returns>A configured analysis using DEMCzs.</returns>
    private static PointProcessAnalysis ConfigureAnalysis(PointProcessModel model, int seed)
    {
        var analysis = new PointProcessAnalysis(model);
        analysis.BayesianAnalysis.Type = BayesianAnalysis.SamplerType.DEMCzs;
        analysis.BayesianAnalysis.NumberOfChains = 4;
        analysis.BayesianAnalysis.WarmupIterations = 4000;
        analysis.BayesianAnalysis.Iterations = 8000;
        analysis.BayesianAnalysis.ThinningInterval = 10;
        analysis.BayesianAnalysis.PRNGSeed = seed;
        analysis.BayesianAnalysis.UseSimulationDefaults = false;
        analysis.BayesianAnalysis.PointEstimator = BayesianAnalysis.PointEstimateType.PosteriorMean;
        return analysis;
    }

    /// <summary>Creates a configured nonseasonal model from the supplied POT data and exposure.</summary>
    /// <param name="frame">The independently generated data.</param>
    /// <param name="observationYears">The known source exposure.</param>
    /// <returns>The configured model.</returns>
    private static PointProcessModel CreateNonSeasonalModel(DataFrame frame, double observationYears)
    {
        var model = new PointProcessModel { UseDefaults = false, DataFrame = frame };
        model.Threshold = Threshold;
        model.TotalYears = observationYears;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>Creates a nonseasonal parent whose empirical arrival rate equals the supplied rate.</summary>
    /// <param name="lambda">The annual Poisson arrival rate.</param>
    /// <returns>A configured parent point-process model.</returns>
    private static PointProcessModel CreateNonSeasonalParentModel(double lambda)
    {
        const int eventCount = 100;
        double observationYears = eventCount / lambda;
        DateTime start = new DateTime(1900, 1, 1);
        double spanDays = observationYears * 365.25;
        var events = new List<ExactData>(eventCount);
        for (int i = 0; i < eventCount; i++)
        {
            DateTime date = start.AddDays(i * spanDays / eventCount);
            double value = Threshold + 1.0 + i % 25;
            events.Add(new ExactData(date, value));
        }

        var frame = new DataFrame
        {
            ExactSeries = new ExactSeries(events),
            PointProcessObservationYears = observationYears
        };
        PointProcessModel parent = CreateNonSeasonalModel(frame, observationYears);
        parent.SetParameterValues(new[] { LocationOne, ScaleOne, -ColesShapeOne });
        Assert.AreEqual(lambda, parent.Lambda, 1E-12, "The parent empirical arrival rate was not configured correctly.");
        return parent;
    }

    /// <summary>Creates dated recovery data from a fixed-size generated POT magnitude sample.</summary>
    /// <param name="sample">The generated POT magnitudes.</param>
    /// <param name="observationYears">The exposure that preserves the parent arrival rate.</param>
    /// <returns>A point-process data frame containing the generated sample.</returns>
    private static DataFrame CreateNonSeasonalRecoveryFrame(double[] sample, double observationYears)
    {
        DateTime start = new DateTime(1800, 1, 1);
        double spanDays = observationYears * 365.25;
        var events = new List<ExactData>(sample.Length);
        for (int i = 0; i < sample.Length; i++)
        {
            DateTime date = start.AddDays(i * spanDays / sample.Length);
            events.Add(new ExactData(date, sample[i]));
        }

        return new DataFrame
        {
            ExactSeries = new ExactSeries(events),
            PointProcessObservationYears = observationYears
        };
    }

    /// <summary>Creates a seasonal parent from two GPAs and the common annual Poisson rate.</summary>
    /// <returns>A configured parent seasonal point-process model.</returns>
    private static PointProcessModel CreateSeasonalParentModel()
    {
        const int eventCount = 80;
        double observationYears = eventCount / SeasonalLambda;
        DateTime start = new DateTime(2000, 1, 1);
        var events = new List<ExactData>(eventCount);
        for (int i = 0; i < eventCount; i++)
        {
            events.Add(new ExactData(start.AddDays(i % 366), Threshold + 1.0 + i % 20));
        }

        var frame = new DataFrame
        {
            ExactSeries = new ExactSeries(events),
            PointProcessObservationYears = observationYears
        };
        PointProcessModel parent = CreateSeasonalModel(frame, observationYears);
        double kappaOne = -ColesShapeOne;
        double kappaTwo = -ColesShapeTwo;
        double locationOne = Threshold + SeasonalGpaScaleOne / kappaOne *
            (1.0 - Math.Pow(SeasonalLambda, -kappaOne));
        double scaleOne = SeasonalGpaScaleOne * Math.Pow(SeasonalLambda, -kappaOne);
        double locationTwo = Threshold + SeasonalGpaScaleTwo / kappaTwo *
            (1.0 - Math.Pow(SeasonalLambda, -kappaTwo));
        double scaleTwo = SeasonalGpaScaleTwo * Math.Pow(SeasonalLambda, -kappaTwo);
        parent.SetParameterValues(new[]
        {
            TrueK1 + 0.5,
            TrueK2 + 0.5,
            locationOne,
            scaleOne,
            kappaOne,
            locationTwo,
            scaleTwo,
            kappaTwo
        });
        Assert.AreEqual(SeasonalLambda, parent.Lambda, 1E-12, "The seasonal parent arrival rate was not configured correctly.");
        return parent;
    }

    /// <summary>Creates a seasonal recovery frame from production-generated dated POT events.</summary>
    /// <param name="sample">The production-generated POT sample.</param>
    /// <param name="observationYears">The exposure that preserves the parent arrival rate.</param>
    /// <returns>A point-process data frame containing the dated seasonal exceedances.</returns>
    private static DataFrame CreateSeasonalRecoveryFrame(TimeSeries sample, double observationYears)
    {
        var events = sample
            .Select(point => new ExactData(point.Index, point.Value))
            .ToList();
        return new DataFrame
        {
            ExactSeries = new ExactSeries(events),
            PointProcessObservationYears = observationYears
        };
    }

    /// <summary>Creates a configured seasonal model with the approved changepoint supports.</summary>
    /// <param name="frame">The generated POT data.</param>
    /// <param name="observationYears">The known source exposure.</param>
    /// <returns>The configured seasonal model.</returns>
    private static PointProcessModel CreateSeasonalModel(DataFrame frame, double observationYears)
    {
        var model = new PointProcessModel
        {
            UseDefaults = false,
            IsSeasonal = true,
            TimeBlock = TimeBlockWindow.CalendarYear,
            StartMonth = 1,
            DataFrame = frame
        };
        model.Threshold = Threshold;
        model.TotalYears = observationYears;
        model.SetDefaultParameters();
        SetChangePointPrior(model.Parameters[0], 10.0, 100.0, TrueK1 + 0.5);
        SetChangePointPrior(model.Parameters[1], 200.0, 360.0, TrueK2 + 0.5);
        return model;
    }

    /// <summary>Applies a diagnostic bounded support through a continuous flat changepoint prior.</summary>
    /// <param name="parameter">The changepoint parameter to configure.</param>
    /// <param name="lower">The inclusive lower bound.</param>
    /// <param name="upper">The inclusive upper bound.</param>
    /// <param name="initial">The valid point value retained by the configured model.</param>
    private static void SetChangePointPrior(ModelParameter parameter, double lower, double upper, double initial)
    {
        parameter.LowerBound = lower;
        parameter.UpperBound = upper;
        parameter.Value = initial;
        parameter.PriorDistribution = new Uniform(lower, upper);
    }

    /// <summary>Evaluates the analytical GEV-compatible threshold intensity.</summary>
    /// <param name="location">The location.</param>
    /// <param name="scale">The scale.</param>
    /// <param name="shape">The Coles shape.</param>
    /// <param name="threshold">The threshold.</param>
    /// <returns>The annual threshold intensity.</returns>
    private static double ThresholdIntensity(double location, double scale, double shape, double threshold)
    {
        return Math.Abs(shape) < 1E-12
            ? Math.Exp(-(threshold - location) / scale)
            : Math.Pow(1.0 + shape * (threshold - location) / scale, -1.0 / shape);
    }

    /// <summary>Evaluates an analytical generalized-Pareto survival probability.</summary>
    /// <param name="scale">The GPA scale at the threshold.</param>
    /// <param name="shape">The Coles shape.</param>
    /// <param name="excess">The excess above the threshold.</param>
    /// <returns>The conditional survival probability.</returns>
    private static double GpaConditionalSurvival(double scale, double shape, double excess)
    {
        return Math.Abs(shape) < 1E-12
            ? Math.Exp(-excess / scale)
            : Math.Pow(1.0 + shape * excess / scale, -1.0 / shape);
    }
    /// <summary>Identifies the wrapped calendar-day season.</summary>
    /// <param name="day">The calendar day.</param>
    /// <returns><c>true</c> for season one.</returns>
    private static bool IsSeasonOne(int day)
    {
        return day < TrueK1 || day >= TrueK2;
    }

    /// <summary>Asserts a production count against an analytical Poisson Monte Carlo bound.</summary>
    /// <param name="actual">The simulated count.</param>
    /// <param name="expected">The analytical expected count.</param>
    /// <param name="label">The component label.</param>
    private static void AssertPoissonMonteCarloCount(int actual, double expected, string label)
    {
        Assert.AreEqual(expected, actual, 5.0 * Math.Sqrt(expected), $"The {label} count missed its analytical Poisson bound.");
    }

    /// <summary>Asserts a component tail frequency against an analytical binomial bound.</summary>
    /// <param name="tailCount">The tail count.</param>
    /// <param name="totalCount">The component count.</param>
    /// <param name="expectedProbability">The analytical conditional probability.</param>
    /// <param name="label">The component label.</param>
    private static void AssertBinomialTail(int tailCount, int totalCount, double expectedProbability, string label)
    {
        double actualProbability = tailCount / (double)totalCount;
        double standardError = Math.Sqrt(expectedProbability * (1.0 - expectedProbability) / totalCount);
        Assert.AreEqual(expectedProbability, actualProbability, 5.0 * standardError, $"The {label} conditional tail missed its analytical binomial bound.");
    }

    /// <summary>Checks floored posterior support and modal-day recovery without relying on a posterior mean.</summary>
    /// <param name="samples">Continuous latent changepoint samples.</param>
    /// <param name="truth">The true integer day.</param>
    /// <param name="label">The changepoint label.</param>
    private static void AssertFlooredChangePointRecovery(IEnumerable<double> samples, int truth, string label)
    {
        int[] days = samples.Select(sample => (int)Math.Floor(sample)).OrderBy(day => day).ToArray();
        int lower = days[(int)Math.Floor(0.025 * (days.Length - 1))];
        int upper = days[(int)Math.Ceiling(0.975 * (days.Length - 1))];
        int mode = days.GroupBy(day => day).OrderByDescending(group => group.Count()).ThenBy(group => group.Key).First().Key;
        Assert.IsTrue(truth >= lower && truth <= upper, $"True {label}={truth} was outside the floored 95% credible set [{lower}, {upper}].");
        Assert.AreEqual(truth, mode, 20, $"Floored posterior mode for {label} was not recovered.");
    }

    /// <summary>Computes the hybrid mixed likelihood independently from production code.</summary>
    /// <param name="frame">The mixed data frame after threshold-count processing.</param>
    /// <param name="observationYears">The point-process exposure.</param>
    /// <returns>The independently calculated log-likelihood.</returns>
    private static double IndependentMixedLogLikelihood(DataFrame frame, double observationYears)
    {
        double logLikelihood = -observationYears * ThresholdIntensity(LocationOne, ScaleOne, ColesShapeOne, Threshold);
        foreach (ExactData point in frame.ExactSeries)
        {
            double support = 1.0 + ColesShapeOne * (point.Value - LocationOne) / ScaleOne;
            logLikelihood += -Math.Log(ScaleOne) - (1.0 + 1.0 / ColesShapeOne) * Math.Log(support);
        }

        foreach (UncertainData point in frame.UncertainSeries)
        {
            var normal = (Normal)point.Distribution;
            double lower = normal.InverseCDF(1E-8);
            double upper = normal.InverseCDF(1.0 - 1E-8);
            double integral = SimpsonIntegrate(
                value => NormalDensity(value, normal.Mu, normal.Sigma) * GevDensity(value),
                lower,
                upper,
                20000);
            logLikelihood += Math.Log(integral / (1.0 - 2E-8));
        }

        foreach (IntervalData point in frame.IntervalSeries)
            logLikelihood += Math.Log(GevCdf(point.UpperValue) - GevCdf(point.LowerValue));

        foreach (ThresholdData point in frame.ThresholdSeries)
        {
            if (point.NumberBelow > 0) logLikelihood += point.NumberBelow * Math.Log(GevCdf(point.Value));
            if (point.NumberAbove > 0) logLikelihood += point.NumberAbove * Math.Log(1.0 - GevCdf(point.Value));
        }

        return logLikelihood;
    }

    /// <summary>Evaluates the ordinary GEV CDF for the mixed-magnitude calculation.</summary>
    /// <param name="value">The evaluation point.</param>
    /// <returns>The GEV cumulative probability.</returns>
    private static double GevCdf(double value)
    {
        double support = 1.0 + ColesShapeOne * (value - LocationOne) / ScaleOne;
        return Math.Exp(-Math.Pow(support, -1.0 / ColesShapeOne));
    }

    /// <summary>Evaluates the ordinary GEV density for the mixed-magnitude calculation.</summary>
    /// <param name="value">The evaluation point.</param>
    /// <returns>The GEV density.</returns>
    private static double GevDensity(double value)
    {
        double support = 1.0 + ColesShapeOne * (value - LocationOne) / ScaleOne;
        return Math.Exp(-Math.Pow(support, -1.0 / ColesShapeOne)) / ScaleOne *
               Math.Pow(support, -1.0 / ColesShapeOne - 1.0);
    }

    /// <summary>Evaluates a Normal density independently.</summary>
    /// <param name="value">The evaluation point.</param>
    /// <param name="mean">The Normal mean.</param>
    /// <param name="standardDeviation">The Normal standard deviation.</param>
    /// <returns>The Normal density.</returns>
    private static double NormalDensity(double value, double mean, double standardDeviation)
    {
        double z = (value - mean) / standardDeviation;
        return Math.Exp(-0.5 * z * z) / (standardDeviation * Math.Sqrt(2.0 * Math.PI));
    }

    /// <summary>Integrates a smooth scalar function with composite Simpson's rule.</summary>
    /// <param name="function">The integrand.</param>
    /// <param name="lower">The lower bound.</param>
    /// <param name="upper">The upper bound.</param>
    /// <param name="subintervals">An even number of subintervals.</param>
    /// <returns>The numerical integral.</returns>
    private static double SimpsonIntegrate(Func<double, double> function, double lower, double upper, int subintervals)
    {
        double width = (upper - lower) / subintervals;
        double sum = function(lower) + function(upper);
        for (int i = 1; i < subintervals; i++)
            sum += (i % 2 == 0 ? 2.0 : 4.0) * function(lower + i * width);
        return sum * width / 3.0;
    }
}
