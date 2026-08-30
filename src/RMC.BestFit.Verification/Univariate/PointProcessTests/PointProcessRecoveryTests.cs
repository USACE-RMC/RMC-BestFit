using Numerics.Data;
using Numerics.Distributions;
using Numerics.Sampling.MCMC;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Recovery;

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
        double[] parentParameters = { LocationOne, ScaleOne, -ColesShapeOne };
        AssertFixtureAndPriorAgreement(model, frame, parentParameters, sampleSize, observationYears, "nonseasonal production");
        Assert.AreEqual(-ColesShapeOne, parentParameters[2], 0.0, "Numerics Kappa must be the negative of the Coles shape.");
        Assert.AreEqual(lambda, ThresholdIntensity(parentParameters[0], parentParameters[1], -parentParameters[2], Threshold), 1E-12,
            "The generator and fitted model did not share the annual threshold intensity.");
        AssertTruthBeatsCollapsedAlternative(model, parentParameters, false, "nonseasonal production");
        PointProcessAnalysis analysis = ConfigureAnalysis(model);

        await analysis.RunAsync();

        Assert.AreEqual(sampleSize, generatedSample.Length, "The production generator did not return the requested POT sample size.");
        MCMCResults? results = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(results, $"Nonseasonal point-process recovery returned no posterior results. {analysis.BayesianAnalysis.LastError}");
        AssertContinuousCoordinateRecovery(results, parentParameters, 0, "nonseasonal Mu");
        AssertContinuousCoordinateRecovery(results, parentParameters, 1, "nonseasonal Sigma");
        AssertContinuousCoordinateRecovery(results, parentParameters, 2, "nonseasonal Kappa");
        AssertNonseasonalResponseRecovery(results, parentParameters, "nonseasonal production");
    }

    /// <summary>
    /// Verifies recovery of both seasonal Poisson-GPA processes and their floored changepoints.
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    [TestMethod]
    public async Task Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints()
    {
        const int sampleSize = 1000;
        PointProcessModel parent = CreateSeasonalParentModel();
        TimeSeries generatedSample = parent.GeneratePOTTimeSeries(sampleSize, 42001);
        double observationYears = sampleSize / SeasonalLambda;
        DataFrame frame = CreateSeasonalRecoveryFrame(generatedSample, observationYears);
        PointProcessModel model = CreateSeasonalModel(frame, observationYears);
        double[] parentParameters = parent.Parameters.Select(parameter => parameter.Value).ToArray();
        AssertFixtureAndPriorAgreement(model, frame, parentParameters, sampleSize, observationYears, "seasonal equal-intensity production");
        AssertSeasonalCrosswalkAndAnnualization(parentParameters, SeasonalLambda, SeasonalLambda, sampleSize / observationYears, "seasonal equal-intensity production");
        AssertTruthBeatsCollapsedAlternative(model, parentParameters, true, "seasonal equal-intensity production");
        PointProcessAnalysis analysis = ConfigureAnalysis(model);
        Exception? analysisError = null;
        analysis.AnalysisCompleted += (_, args) => analysisError = args.Error;

        var validation = analysis.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));

        await analysis.RunAsync();

        Assert.AreEqual(sampleSize, generatedSample.Count, "The production generator did not return the requested seasonal POT sample size.");
        MCMCResults? results = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(results, $"Seasonal point-process recovery returned no posterior results. {analysisError ?? analysis.BayesianAnalysis.LastError}");
        AssertSeasonalPosteriorRecovery(results, parentParameters, sampleSize, TrueK1, TrueK2, "seasonal equal-intensity production");
    }

    /// <summary>
    /// Verifies recovery of both seasonal processes and changepoints when the seasons have
    /// unequal threshold intensities (season one three times season two).
    /// </summary>
    /// <returns>A task representing the asynchronous Bayesian analysis.</returns>
    [TestMethod]
    public async Task Test_SeasonalProductionGenerator_WithUnequalIntensities_RecoversParentAndBothChangePoints()
    {
        const int sampleSize = 1000;
        const double intensityOne = 12.0;
        const double intensityTwo = 4.0;
        PointProcessModel parent = CreateSeasonalParentModel(intensityOne, intensityTwo);
        TimeSeries generatedSample = parent.GeneratePOTTimeSeries(sampleSize, 42101);
        double observationYears = sampleSize / parent.FittedThresholdIntensity;
        DataFrame frame = CreateSeasonalRecoveryFrame(generatedSample, observationYears);
        PointProcessModel model = CreateSeasonalModel(frame, observationYears);
        double[] parentParameters = parent.Parameters.Select(parameter => parameter.Value).ToArray();
        AssertFixtureAndPriorAgreement(model, frame, parentParameters, sampleSize, observationYears, "seasonal unequal-intensity production");
        AssertSeasonalCrosswalkAndAnnualization(parentParameters, intensityOne, intensityTwo, sampleSize / observationYears, "seasonal unequal-intensity production");
        AssertTruthBeatsCollapsedAlternative(model, parentParameters, true, "seasonal unequal-intensity production");
        PointProcessAnalysis analysis = ConfigureAnalysis(model);
        Exception? analysisError = null;
        analysis.AnalysisCompleted += (_, args) => analysisError = args.Error;

        var validation = analysis.Validate();
        Assert.IsTrue(validation.IsValid, string.Join(Environment.NewLine, validation.ValidationMessages));

        await analysis.RunAsync();

        Assert.AreEqual(sampleSize, generatedSample.Count, "The production generator did not return the requested seasonal POT sample size.");
        MCMCResults? results = analysis.BayesianAnalysis.Results;
        Assert.IsNotNull(results, $"Unequal-intensity seasonal recovery returned no posterior results. {analysisError ?? analysis.BayesianAnalysis.LastError}");
        AssertSeasonalPosteriorRecovery(results, parentParameters, sampleSize, TrueK1, TrueK2, "seasonal unequal-intensity production");
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
    /// Verifies seasonal counts follow each season's own exposure-weighted threshold intensity and
    /// seasonal marks follow each season's own conditional tail when the intensities differ.
    /// </summary>
    [TestMethod]
    public void Test_SeasonalSimulation_WithUnequalIntensities_MatchesSeasonRatesAssignmentsAndConditionalTails()
    {
        const int replicates = 500;
        const double durationYears = 20.0;
        const double tailPointOne = 120.0;
        const double tailPointTwo = 165.0;
        const double intensityOne = 12.0;
        const double intensityTwo = 4.0;
        PointProcessModel model = CreateSeasonalParentModel(intensityOne, intensityTwo);

        int countOne = 0;
        int countTwo = 0;
        int tailOne = 0;
        int tailTwo = 0;
        for (int replicate = 0; replicate < replicates; replicate++)
        {
            TimeSeries sample = model.GeneratePOTTimeSeries(new DateTime(2000, 1, 1), durationYears, 45100 + replicate);
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
        AssertPoissonMonteCarloCount(countOne, replicates * durationYears * weightOne * intensityOne, "season one");
        AssertPoissonMonteCarloCount(countTwo, replicates * durationYears * weightTwo * intensityTwo, "season two");
        AssertBinomialTail(tailOne, countOne, GpaConditionalSurvival(SeasonalGpaScaleOne, ColesShapeOne, tailPointOne - Threshold), "season one");
        AssertBinomialTail(tailTwo, countTwo, GpaConditionalSurvival(SeasonalGpaScaleTwo, ColesShapeTwo, tailPointTwo - Threshold), "season two");
    }

    /// <summary>Configures the shared Bayesian recovery analysis.</summary>
    /// <param name="model">The point-process model.</param>
    /// <returns>An analysis using the default DEMCzs configuration.</returns>
    private static PointProcessAnalysis ConfigureAnalysis(PointProcessModel model)
    {
        return new PointProcessAnalysis(model);
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
    private static PointProcessModel CreateSeasonalParentModel() =>
        CreateSeasonalParentModel(SeasonalLambda, SeasonalLambda);

    /// <summary>
    /// Creates a seasonal parent from two GPAs and per-season annual threshold intensities.
    /// </summary>
    /// <param name="intensityOne">The annual threshold intensity of the wrapped first season.</param>
    /// <param name="intensityTwo">The annual threshold intensity of the second season.</param>
    /// <returns>A configured parent seasonal point-process model whose empirical rate equals its fitted annual rate.</returns>
    private static PointProcessModel CreateSeasonalParentModel(double intensityOne, double intensityTwo)
    {
        const int eventCount = 80;
        double weightOne = (TrueK1 + 366.0 - TrueK2) / 366.0;
        double weightTwo = (TrueK2 - TrueK1) / 366.0;
        double annualRate = weightOne * intensityOne + weightTwo * intensityTwo;
        double observationYears = eventCount / annualRate;
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
            (1.0 - Math.Pow(intensityOne, -kappaOne));
        double scaleOne = SeasonalGpaScaleOne * Math.Pow(intensityOne, -kappaOne);
        double locationTwo = Threshold + SeasonalGpaScaleTwo / kappaTwo *
            (1.0 - Math.Pow(intensityTwo, -kappaTwo));
        double scaleTwo = SeasonalGpaScaleTwo * Math.Pow(intensityTwo, -kappaTwo);
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
        Assert.AreEqual(annualRate, parent.Lambda, 1E-12, "The seasonal parent arrival rate was not configured correctly.");
        Assert.AreEqual(annualRate, parent.FittedThresholdIntensity, 1E-9, "The seasonal parent fitted annual rate was not configured correctly.");
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

    /// <summary>Creates a configured seasonal model with the default changepoint supports.</summary>
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

    /// <summary>Checks floored central-95% posterior support and convergence diagnostics.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="parameterIndex">Changepoint parameter index.</param>
    /// <param name="truth">The true integer day.</param>
    /// <param name="label">The changepoint label.</param>
    private static void AssertFlooredChangePointRecovery(MCMCResults results, int parameterIndex, int truth, string label)
    {
        AssertPosteriorDiagnostics(results, parameterIndex, label);
        int[] days = results.Output
            .Select(sample => (int)Math.Floor(sample.Values[parameterIndex]))
            .OrderBy(day => day)
            .ToArray();
        int lower = days[(int)Math.Floor(0.025 * (days.Length - 1))];
        int upper = days[(int)Math.Ceiling(0.975 * (days.Length - 1))];
        Assert.IsTrue(truth >= lower && truth <= upper, $"True {label}={truth} was outside the floored 95% credible set [{lower}, {upper}].");
    }

    /// <summary>Verifies the generated sample, fitted exposure, and every configured prior before MCMC.</summary>
    /// <param name="model">Configured fitted model.</param>
    /// <param name="frame">Generated exact POT data.</param>
    /// <param name="truth">Generating parameter vector in fitted coordinates.</param>
    /// <param name="sampleSize">Predeclared event count.</param>
    /// <param name="observationYears">Predeclared exposure in years.</param>
    /// <param name="label">Fixture label.</param>
    private static void AssertFixtureAndPriorAgreement(
        PointProcessModel model,
        DataFrame frame,
        double[] truth,
        int sampleSize,
        double observationYears,
        string label)
    {
        Assert.AreEqual(sampleSize, frame.ExactSeries.Count, $"{label}: generated event count changed.");
        Assert.AreEqual(observationYears, frame.PointProcessObservationYears, 0.0, $"{label}: source exposure changed.");
        Assert.AreEqual(observationYears, model.TotalYears, 0.0, $"{label}: fitted exposure disagreed with the generator.");
        Assert.AreEqual(Threshold, model.Threshold, 0.0, $"{label}: fitted threshold disagreed with the generator.");
        Assert.AreEqual(truth.Length, model.Parameters.Count, $"{label}: fitted coordinate count changed.");
        for (int index = 0; index < truth.Length; index++)
        {
            ModelParameter parameter = model.Parameters[index];
            Assert.IsTrue(truth[index] >= parameter.LowerBound && truth[index] <= parameter.UpperBound,
                $"{label}: parent {parameter.Name}={truth[index]:G17} is outside configured bounds [{parameter.LowerBound:G17}, {parameter.UpperBound:G17}].");
            Assert.IsNotNull(parameter.PriorDistribution, $"{label}: {parameter.Name} has no configured prior.");
            Assert.IsTrue(parameter.PriorDistribution.PDF(truth[index]) > 0.0,
                $"{label}: parent {parameter.Name}={truth[index]:G17} is outside its configured prior.");
        }

        Assert.IsTrue(double.IsFinite(model.PriorLogLikelihood(truth)), $"{label}: the complete parent prior density is not finite.");
    }

    /// <summary>Requires the parent likelihood to beat one predeclared collapsed alternative.</summary>
    /// <param name="model">Configured fitted model.</param>
    /// <param name="truth">Generating vector.</param>
    /// <param name="seasonal">Whether the vector contains changepoints.</param>
    /// <param name="label">Fixture label.</param>
    private static void AssertTruthBeatsCollapsedAlternative(PointProcessModel model, double[] truth, bool seasonal, string label)
    {
        double[] collapsed = (double[])truth.Clone();
        if (seasonal)
            collapsed[0] = model.Parameters[0].LowerBound + 0.5;
        else
            collapsed[1] = Math.Max(model.Parameters[1].LowerBound, 0.1 * truth[1]);
        double truthLogLikelihood = model.DataLogLikelihood(truth);
        double collapsedLogLikelihood = model.DataLogLikelihood(collapsed);
        Assert.IsTrue(double.IsFinite(truthLogLikelihood), $"{label}: parent data likelihood is not finite.");
        Assert.IsTrue(truthLogLikelihood > collapsedLogLikelihood,
            $"{label}: parent likelihood {truthLogLikelihood:G17} did not beat collapsed alternative {collapsedLogLikelihood:G17}.");
    }

    /// <summary>Checks the shape sign and seasonal intensity annualization before MCMC.</summary>
    /// <param name="truth">Generating seasonal vector.</param>
    /// <param name="intensityOne">Generating annual intensity for season one.</param>
    /// <param name="intensityTwo">Generating annual intensity for season two.</param>
    /// <param name="annualRate">Generating exposure-weighted annual rate.</param>
    /// <param name="label">Fixture label.</param>
    private static void AssertSeasonalCrosswalkAndAnnualization(
        double[] truth,
        double intensityOne,
        double intensityTwo,
        double annualRate,
        string label)
    {
        Assert.AreEqual(-ColesShapeOne, truth[4], 0.0, $"{label}: season-one Numerics Kappa is not -Coles xi.");
        Assert.AreEqual(-ColesShapeTwo, truth[7], 0.0, $"{label}: season-two Numerics Kappa is not -Coles xi.");
        double recoveredIntensityOne = ThresholdIntensity(truth[2], truth[3], -truth[4], Threshold);
        double recoveredIntensityTwo = ThresholdIntensity(truth[5], truth[6], -truth[7], Threshold);
        Assert.AreEqual(intensityOne, recoveredIntensityOne, 1E-10, $"{label}: season-one threshold intensity crosswalk changed.");
        Assert.AreEqual(intensityTwo, recoveredIntensityTwo, 1E-10, $"{label}: season-two threshold intensity crosswalk changed.");
        int k1 = (int)Math.Floor(truth[0]);
        int k2 = (int)Math.Floor(truth[1]);
        double weightOne = (k1 + 366.0 - k2) / 366.0;
        double weightTwo = (k2 - k1) / 366.0;
        Assert.AreEqual(annualRate, weightOne * recoveredIntensityOne + weightTwo * recoveredIntensityTwo, 1E-10,
            $"{label}: seasonal intensities were not annualized with the configured exposure fractions.");
    }

    /// <summary>Applies central-95% parent inclusion plus R-hat and ESS to one continuous coordinate.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="truth">Generating vector.</param>
    /// <param name="parameterIndex">Continuous coordinate index.</param>
    /// <param name="label">Coordinate label.</param>
    private static void AssertContinuousCoordinateRecovery(MCMCResults results, double[] truth, int parameterIndex, string label)
    {
        (double lower, double upper) = CentralNinetyFivePercentInterval(
            results.Output.Select(sample => sample.Values[parameterIndex]),
            label);
        var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
        string diagnosticLabel = $"{label} (R-hat {summary.Rhat:G17}, ESS {summary.ESS:G17})";
        RecoveryAcceptance.AssertBayesianRecovery(diagnosticLabel, truth[parameterIndex], lower, upper, summary.Rhat, summary.ESS);
    }

    /// <summary>Applies R-hat and ESS to one monitored effective-changepoint coordinate.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="parameterIndex">Coordinate index.</param>
    /// <param name="label">Coordinate label.</param>
    private static void AssertPosteriorDiagnostics(MCMCResults results, int parameterIndex, string label)
    {
        var summary = results.ParameterResults[parameterIndex].SummaryStatistics;
        Assert.IsTrue(double.IsFinite(summary.Rhat) && summary.Rhat < RecoveryAcceptance.MaximumRhat,
            $"{label} R-hat {summary.Rhat:G17} must be below {RecoveryAcceptance.MaximumRhat:G17}.");
        Assert.IsTrue(double.IsFinite(summary.ESS) && summary.ESS >= RecoveryAcceptance.MinimumEffectiveSampleSize,
            $"{label} ESS {summary.ESS:G17} must be at least {RecoveryAcceptance.MinimumEffectiveSampleSize:G17}.");
    }

    /// <summary>Applies all seasonal coordinate and identified-response recovery rules.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="truth">Generating seasonal vector.</param>
    /// <param name="sampleSize">Total generated event count across both seasonal components.</param>
    /// <param name="trueK1">Generating effective first changepoint day.</param>
    /// <param name="trueK2">Generating effective second changepoint day.</param>
    /// <param name="label">Cell label.</param>
    /// <remarks>
    /// Continuous component recovery is evaluated in the likelihood-native Poisson-GPA
    /// coordinates: threshold intensity, GPA scale, and Hosking Kappa. The Poisson standard error
    /// and Numerics GPA maximum-likelihood covariance both use <c>N_s = N p_s</c>, where
    /// <c>p_s = w_s Lambda_s / sum(w_j Lambda_j)</c> is the fixed-size generator's seasonal
    /// mixture weight. All fitted GEV
    /// coordinates retain R-hat and ESS checks; changepoints and identified response coordinates
    /// retain their empirical central-95% posterior acceptance.
    /// </remarks>
    private static void AssertSeasonalPosteriorRecovery(
        MCMCResults results,
        double[] truth,
        int sampleSize,
        int trueK1,
        int trueK2,
        string label)
    {
        var failures = new List<string>();
        CaptureAssertion(failures, () => AssertFlooredChangePointRecovery(results, 0, trueK1, $"{label} K1"));
        CaptureAssertion(failures, () => AssertFlooredChangePointRecovery(results, 1, trueK2, $"{label} K2"));
        CaptureAssertion(failures, () => AssertSeasonalResponseRecovery(results, truth, label));
        for (int component = 0; component < 2; component++)
        {
            int capturedComponent = component;
            CaptureAssertion(failures, () => AssertSeasonalComponentRecovery(
                results,
                truth,
                sampleSize,
                capturedComponent,
                $"{label} season {capturedComponent + 1}"));
        }

        Assert.AreEqual(0, failures.Count, string.Join(Environment.NewLine, failures));
    }

    /// <summary>Applies effective-season Poisson-GPA recovery plus posterior diagnostics.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="truth">Generating seasonal vector.</param>
    /// <param name="sampleSize">Total generated event count across both seasonal components.</param>
    /// <param name="component">Zero-based seasonal component index.</param>
    /// <param name="label">Component label.</param>
    /// <remarks>
    /// The fixed-size seasonal generator has total N=1000. A component receives the fraction
    /// <c>p_s = w_s Lambda_s / sum(w_j Lambda_j)</c> of those events, combining its
    /// changepoint-defined exposure fraction with its threshold intensity. Thus
    /// <c>N_s = N p_s</c>. Numerics GPA scale and
    /// Kappa covariance entries are exactly proportional to <c>1 / N</c>, so evaluating them at
    /// total N and dividing by <c>p_s</c> is algebraically the covariance at fractional
    /// <c>N_s</c>. The intensity standard error follows the corresponding Poisson rate result.
    /// </remarks>
    private static void AssertSeasonalComponentRecovery(
        MCMCResults results,
        double[] truth,
        int sampleSize,
        int component,
        string label)
    {
        Assert.AreEqual(RecoveryDesign.SampleSize, sampleSize,
            $"{label} must retain the predeclared N={RecoveryDesign.SampleSize} recovery design.");
        int componentOffset = 2 + 3 * component;
        string[] coordinateNames = { "Mu", "Sigma", "Kappa" };
        for (int coordinate = 0; coordinate < 3; coordinate++)
        {
            AssertPosteriorDiagnostics(
                results,
                componentOffset + coordinate,
                $"{label} {coordinateNames[coordinate]}");
        }

        int k1 = (int)Math.Floor(truth[0]);
        int k2 = (int)Math.Floor(truth[1]);
        double exposureWeightOne = (k1 + 366.0 - k2) / 366.0;
        double exposureWeightTwo = (k2 - k1) / 366.0;
        double parentIntensityOne = ThresholdIntensity(truth[2], truth[3], -truth[4], Threshold);
        double parentIntensityTwo = ThresholdIntensity(truth[5], truth[6], -truth[7], Threshold);
        double totalAnnualIntensity = exposureWeightOne * parentIntensityOne
            + exposureWeightTwo * parentIntensityTwo;
        double parentIntensity = ThresholdIntensity(
            truth[componentOffset],
            truth[componentOffset + 1],
            -truth[componentOffset + 2],
            Threshold);
        double exposureWeight = component == 0 ? exposureWeightOne : exposureWeightTwo;
        double mixtureWeight = exposureWeight * parentIntensity / totalAnnualIntensity;
        double effectiveSampleSize = sampleSize * mixtureWeight;
        Assert.IsTrue(mixtureWeight > 0.0 && mixtureWeight < 1.0,
            $"{label} seasonal mixture weight {mixtureWeight:G17} must be strictly between zero and one.");
        Assert.IsTrue(effectiveSampleSize > 0.0,
            $"{label} effective seasonal sample size {effectiveSampleSize:G17} must be positive.");

        double parentGpaScale = truth[componentOffset + 1]
            * Math.Pow(parentIntensity, truth[componentOffset + 2]);
        double parentKappa = truth[componentOffset + 2];
        double posteriorIntensityMean = results.Output.Average(sample => ThresholdIntensity(
            sample.Values[componentOffset],
            sample.Values[componentOffset + 1],
            -sample.Values[componentOffset + 2],
            Threshold));
        double posteriorGpaScaleMean = results.Output.Average(sample =>
        {
            double intensity = ThresholdIntensity(
                sample.Values[componentOffset],
                sample.Values[componentOffset + 1],
                -sample.Values[componentOffset + 2],
                Threshold);
            return sample.Values[componentOffset + 1]
                * Math.Pow(intensity, sample.Values[componentOffset + 2]);
        });
        double posteriorKappaMean = results.Output.Average(sample => sample.Values[componentOffset + 2]);

        var parentGpa = new GeneralizedPareto(Threshold, parentGpaScale, parentKappa);
        double[,] totalSampleCovariance = parentGpa.ParameterCovariance(
            sampleSize,
            ParameterEstimationMethod.MaximumLikelihood);
        double intensityStandardError = parentIntensity / Math.Sqrt(effectiveSampleSize);
        double scaleStandardError = Math.Sqrt(totalSampleCovariance[1, 1] / mixtureWeight);
        double kappaStandardError = Math.Sqrt(totalSampleCovariance[2, 2] / mixtureWeight);
        string effectiveDesign = $"N_s={effectiveSampleSize:G17}, mixture weight={mixtureWeight:G17}";

        RecoveryAcceptance.AssertFrequentistStandardizedError(
            $"{label} threshold intensity ({effectiveDesign})",
            posteriorIntensityMean,
            parentIntensity,
            intensityStandardError);
        RecoveryAcceptance.AssertFrequentistStandardizedError(
            $"{label} GPA scale ({effectiveDesign})",
            posteriorGpaScaleMean,
            parentGpaScale,
            scaleStandardError);
        RecoveryAcceptance.AssertFrequentistStandardizedError(
            $"{label} GPA Kappa ({effectiveDesign})",
            posteriorKappaMean,
            parentKappa,
            kappaStandardError);
    }

    /// <summary>Captures one recovery assertion so every predeclared coordinate is evaluated.</summary>
    /// <param name="failures">Collected assertion messages.</param>
    /// <param name="assertion">One shared recovery or response assertion.</param>
    private static void CaptureAssertion(ICollection<string> failures, Action assertion)
    {
        try
        {
            assertion();
        }
        catch (AssertFailedException exception)
        {
            failures.Add(exception.Message);
        }
    }

    /// <summary>Checks nonseasonal threshold intensity and conditional-tail posterior bands.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="truth">Generating vector.</param>
    /// <param name="label">Cell label.</param>
    private static void AssertNonseasonalResponseRecovery(MCMCResults results, double[] truth, string label)
    {
        const double tailValue = 120.0;
        double parentIntensity = ThresholdIntensity(truth[0], truth[1], -truth[2], Threshold);
        double parentScaleAtThreshold = truth[1] - truth[2] * (Threshold - truth[0]);
        double parentTail = GpaConditionalSurvival(parentScaleAtThreshold, -truth[2], tailValue - Threshold);
        AssertIdentifiedResponseRecovery(results.Output.Select(sample =>
            ThresholdIntensity(sample.Values[0], sample.Values[1], -sample.Values[2], Threshold)), parentIntensity, $"{label} annual threshold intensity");
        AssertIdentifiedResponseRecovery(results.Output.Select(sample =>
        {
            double scaleAtThreshold = sample.Values[1] - sample.Values[2] * (Threshold - sample.Values[0]);
            return GpaConditionalSurvival(scaleAtThreshold, -sample.Values[2], tailValue - Threshold);
        }), parentTail, $"{label} conditional tail at {tailValue:G17}");
    }

    /// <summary>Checks seasonal intensity and component-tail posterior bands.</summary>
    /// <param name="results">Posterior results.</param>
    /// <param name="truth">Generating vector.</param>
    /// <param name="label">Cell label.</param>
    private static void AssertSeasonalResponseRecovery(MCMCResults results, double[] truth, string label)
    {
        const double tailOne = 120.0;
        const double tailTwo = 165.0;
        for (int component = 0; component < 2; component++)
        {
            int offset = 2 + 3 * component;
            double tailValue = component == 0 ? tailOne : tailTwo;
            double parentIntensity = ThresholdIntensity(truth[offset], truth[offset + 1], -truth[offset + 2], Threshold);
            double parentScaleAtThreshold = truth[offset + 1] - truth[offset + 2] * (Threshold - truth[offset]);
            double parentTail = GpaConditionalSurvival(parentScaleAtThreshold, -truth[offset + 2], tailValue - Threshold);
            int capturedOffset = offset;
            AssertIdentifiedResponseRecovery(results.Output.Select(sample =>
                ThresholdIntensity(sample.Values[capturedOffset], sample.Values[capturedOffset + 1], -sample.Values[capturedOffset + 2], Threshold)),
                parentIntensity,
                $"{label} season {component + 1} annual threshold intensity");
            AssertIdentifiedResponseRecovery(results.Output.Select(sample =>
            {
                double scaleAtThreshold = sample.Values[capturedOffset + 1] - sample.Values[capturedOffset + 2] * (Threshold - sample.Values[capturedOffset]);
                return GpaConditionalSurvival(scaleAtThreshold, -sample.Values[capturedOffset + 2], tailValue - Threshold);
            }), parentTail, $"{label} season {component + 1} conditional tail at {tailValue:G17}");
        }
    }

    /// <summary>Requires one identified response truth to lie in its empirical central-95% posterior band.</summary>
    /// <param name="samples">Derived posterior response draws.</param>
    /// <param name="parent">Generating response.</param>
    /// <param name="label">Response label.</param>
    private static void AssertIdentifiedResponseRecovery(IEnumerable<double> samples, double parent, string label)
    {
        (double lower, double upper) = CentralNinetyFivePercentInterval(samples, label);
        RecoveryAcceptance.AssertIdentifiedResponseGrid(label, parent, lower, upper);
    }

    /// <summary>Computes a conservative empirical central-95% interval from finite retained draws.</summary>
    /// <param name="samples">Posterior draws.</param>
    /// <param name="label">Coordinate or response label.</param>
    /// <returns>The lower and upper empirical limits.</returns>
    private static (double Lower, double Upper) CentralNinetyFivePercentInterval(IEnumerable<double> samples, string label)
    {
        double[] ordered = samples.OrderBy(value => value).ToArray();
        Assert.IsTrue(ordered.Length > 0, $"{label} has no retained posterior draws.");
        Assert.IsTrue(ordered.All(double.IsFinite), $"{label} contains non-finite retained posterior draws.");
        int lowerIndex = (int)Math.Floor(0.025 * (ordered.Length - 1));
        int upperIndex = (int)Math.Ceiling(0.975 * (ordered.Length - 1));
        return (ordered[lowerIndex], ordered[upperIndex]);
    }

}
