using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;
using BestFitDataFrame = RMC.BestFit.Models.DataFrame;

namespace RMC.BestFit.Tests.Univariate;

/// <summary>
/// Tests for the seasonal point-process exposure annualization, the clone arrival rate and the
/// seasonal quantile-prior evaluation.
/// </summary>
[TestClass]
public class PointProcessSeasonalAnnualizationTests
{
    private const double Tolerance = 1E-10;
    private const int ChangePointOne = 60;
    private const int ChangePointTwo = 210;
    private const double Threshold = 1000.0;

    /// <summary>
    /// Verifies the Gumbel-limit annualization shifts each seasonal location by
    /// <c>alpha * log(p)</c>, the zero-shape limit of <c>xi + (alpha / kappa) * (1 - p^-kappa)</c>,
    /// for both the published distribution and the parameter-value path.
    /// </summary>
    [TestMethod]
    public void GumbelLimitAnnualization_ShiftsLocationByAlphaLogExposure()
    {
        PointProcessModel model = CreateSeasonalModel();
        double[] parameters = { ChangePointOne + 0.5, ChangePointTwo + 0.5, 1000.0, 200.0, 0.0, 1500.0, 300.0, 0.0 };
        double exposureOne = (ChangePointOne + 366.0 - ChangePointTwo) / 366.0;
        double exposureTwo = (ChangePointTwo - ChangePointOne) / 366.0;

        CompetingRisks annual = model.GetDistribution(parameters);
        double[] seasonOne = annual.Distributions[0].GetParameters;
        double[] seasonTwo = annual.Distributions[1].GetParameters;

        Assert.AreEqual(1000.0 + 200.0 * Math.Log(exposureOne), seasonOne[0], Tolerance, "season one location");
        Assert.AreEqual(200.0, seasonOne[1], Tolerance, "season one scale");
        Assert.AreEqual(0.0, seasonOne[2], Tolerance, "season one shape");
        Assert.AreEqual(1500.0 + 300.0 * Math.Log(exposureTwo), seasonTwo[0], Tolerance, "season two location");
        Assert.AreEqual(300.0, seasonTwo[1], Tolerance, "season two scale");
        Assert.AreEqual(0.0, seasonTwo[2], Tolerance, "season two shape");

        // Annualized locations lie below the seasonal locations because each season is exposed
        // for less than a full year.
        Assert.IsTrue(seasonOne[0] < 1000.0, "season one annualized location must decrease");
        Assert.IsTrue(seasonTwo[0] < 1500.0, "season two annualized location must decrease");

        model.SetParameterValues(parameters);
        CollectionAssert.AreEqual(
            seasonOne.Select(value => Math.Round(value, 10)).ToArray(),
            model.Distribution!.Distributions[0].GetParameters.Select(value => Math.Round(value, 10)).ToArray(),
            "SetParameterValues season one");
        CollectionAssert.AreEqual(
            seasonTwo.Select(value => Math.Round(value, 10)).ToArray(),
            model.Distribution!.Distributions[1].GetParameters.Select(value => Math.Round(value, 10)).ToArray(),
            "SetParameterValues season two");
    }

    /// <summary>
    /// Verifies the general annualization formula is continuous with the Gumbel limit across the
    /// shape cutoff used by the likelihood's zero-shape branch.
    /// </summary>
    [TestMethod]
    public void Annualization_IsContinuousAcrossZeroShapeCutoff()
    {
        PointProcessModel model = CreateSeasonalModel();
        double exposureOne = (ChangePointOne + 366.0 - ChangePointTwo) / 366.0;
        const double alpha = 200.0;
        double limitLocation = model.GetDistribution(new[] { ChangePointOne + 0.5, ChangePointTwo + 0.5, 1000.0, alpha, 0.0, 1500.0, 300.0, 0.1 })
            .Distributions[0].GetParameters[0];

        foreach (double kappa in new[] { -2E-4, 2E-4 })
        {
            double[] generalParameters = { ChangePointOne + 0.5, ChangePointTwo + 0.5, 1000.0, alpha, kappa, 1500.0, 300.0, 0.1 };
            double generalLocation = model.GetDistribution(generalParameters).Distributions[0].GetParameters[0];
            double expectedGeneral = 1000.0 + alpha / kappa * (1.0 - Math.Pow(exposureOne, -kappa));

            Assert.AreEqual(expectedGeneral, generalLocation, Tolerance, $"general annualization at kappa {kappa}");
            // The general branch differs from the Gumbel limit by alpha*kappa*log(p)^2/2 to first order.
            Assert.IsTrue(
                Math.Abs(generalLocation - limitLocation) < alpha * Math.Abs(kappa) * Math.Pow(Math.Log(exposureOne), 2.0),
                $"continuity at kappa {kappa}: general {generalLocation:G17} versus limit {limitLocation:G17}");
        }

        double insideCutoff = model.GetDistribution(new[] { ChangePointOne + 0.5, ChangePointTwo + 0.5, 1000.0, alpha, 5E-5, 1500.0, 300.0, 0.1 })
            .Distributions[0].GetParameters[0];
        Assert.AreEqual(limitLocation, insideCutoff, 0.0, "shapes inside the cutoff use the Gumbel limit");
    }

    /// <summary>
    /// Verifies a clone with an explicit observation span reproduces the source arrival rate.
    /// </summary>
    [TestMethod]
    public void Clone_WithExplicitTotalYears_PreservesLambda()
    {
        var model = new PointProcessModel
        {
            DataFrame = CreateAnnualMaximumFrame(),
        };
        model.TotalYears = 37.5;

        var clone = (PointProcessModel)model.Clone();

        Assert.AreEqual(20.0 / 37.5, model.Lambda, Tolerance, "source arrival rate");
        Assert.AreEqual(37.5, clone.TotalYears, Tolerance, "clone observation span");
        Assert.AreEqual(model.Lambda, clone.Lambda, Tolerance, "clone arrival rate");
    }

    /// <summary>
    /// Verifies a seasonal single quantile prior is evaluated on the exposure-annualized
    /// distribution used by the likelihood and the fitted curve.
    /// </summary>
    [TestMethod]
    public void SeasonalQuantilePrior_UsesAnnualizedDistribution()
    {
        PointProcessModel model = CreateSeasonalModel();
        model.UseJeffreysRuleForScale = false;
        model.EnableQuantilePriors = true;
        model.UseSingleQuantile = true;
        var prior = new Normal(3000.0, 400.0);
        model.QuantilePriors.Clear();
        model.QuantilePriors.Add(new QuantilePrior(0.01, prior));
        model.ProcessQuantilePriors();

        // Start from the in-support default values and move the two shapes away from zero so the
        // general annualization branch is exercised.
        double[] parameters = model.Parameters.Select(parameter => parameter.Value).ToArray();
        parameters[4] = Math.Clamp(-0.1, model.Parameters[4].LowerBound, model.Parameters[4].UpperBound);
        parameters[7] = Math.Clamp(0.15, model.Parameters[7].LowerBound, model.Parameters[7].UpperBound);
        double expected = 0.0;
        for (int i = 0; i < model.Parameters.Count; i++)
        {
            double logPrior = model.Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            Assert.IsTrue(double.IsFinite(logPrior), $"parameter {i} ({model.Parameters[i].Name}) value {parameters[i]} must lie inside its prior support");
            expected += logPrior;
        }
        double annualQuantile = model.GetDistribution(parameters).InverseCDF(0.99);
        expected += prior.LogPDF(annualQuantile);

        Assert.IsTrue(double.IsFinite(expected), "expected prior");
        Assert.AreEqual(expected, model.PriorLogLikelihood(parameters), Tolerance, "scalar prior");
        Assert.AreEqual(
            expected,
            model.PointwisePriorLogLikelihood(parameters).Sum(component => component.LogLikelihood),
            Tolerance,
            "pointwise prior sum");
    }

    /// <summary>
    /// Verifies the seasonal simulators follow the fitted per-season threshold intensities: the
    /// annual rate is the exposure-weighted sum of the two intensities, events are assigned to a
    /// season in proportion to its exposure-weighted intensity, and each season's marks follow the
    /// generalized Pareto distribution implied by that season's own intensity.
    /// </summary>
    [TestMethod]
    public void SeasonalSimulation_FollowsFittedSeasonalIntensities()
    {
        const double intensityOne = 6.0;
        const double intensityTwo = 2.0;
        const double gpaScaleOne = 200.0;
        const double gpaKappaOne = 0.1;
        const double gpaScaleTwo = 300.0;
        const double gpaKappaTwo = -0.2;
        const double durationYears = 1500.0;
        PointProcessModel model = CreateSeasonalModel();
        model.SetParameterValues(new[]
        {
            ChangePointOne + 0.5,
            ChangePointTwo + 0.5,
            GevLocation(gpaScaleOne, gpaKappaOne, intensityOne),
            GevScale(gpaScaleOne, gpaKappaOne, intensityOne),
            gpaKappaOne,
            GevLocation(gpaScaleTwo, gpaKappaTwo, intensityTwo),
            GevScale(gpaScaleTwo, gpaKappaTwo, intensityTwo),
            gpaKappaTwo,
        });
        double exposureOne = (ChangePointOne + 366.0 - ChangePointTwo) / 366.0;
        double exposureTwo = (ChangePointTwo - ChangePointOne) / 366.0;
        double annualRate = exposureOne * intensityOne + exposureTwo * intensityTwo;

        Assert.AreEqual(annualRate, model.FittedThresholdIntensity, 1E-9, "fitted annual rate");
        Assert.IsTrue(Math.Abs(model.Lambda - annualRate) > 1.0, "the fixture separates the empirical rate from the fitted rate");

        var sample = model.GeneratePOTTimeSeries(new DateTime(2000, 1, 1), durationYears, 90210);
        var seasonOneExcess = new List<double>();
        var seasonTwoExcess = new List<double>();
        foreach (var point in sample)
        {
            int day = point.Index.DayOfYear;
            if (day < ChangePointOne || day >= ChangePointTwo)
                seasonOneExcess.Add(point.Value - Threshold);
            else
                seasonTwoExcess.Add(point.Value - Threshold);
        }

        AssertPoissonCount(seasonOneExcess.Count, durationYears * exposureOne * intensityOne, "season one count");
        AssertPoissonCount(seasonTwoExcess.Count, durationYears * exposureTwo * intensityTwo, "season two count");
        AssertMeanExcess(seasonOneExcess, gpaScaleOne, gpaKappaOne, "season one marks");
        AssertMeanExcess(seasonTwoExcess, gpaScaleTwo, gpaKappaTwo, "season two marks");
    }

    /// <summary>
    /// Asserts a simulated Poisson count lies within five standard errors of its expectation.
    /// </summary>
    /// <param name="count">The simulated count.</param>
    /// <param name="expected">The expected count.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertPoissonCount(int count, double expected, string context)
    {
        Assert.AreEqual(expected, count, 5.0 * Math.Sqrt(expected), $"{context}: expected {expected:F1}, simulated {count}");
    }

    /// <summary>
    /// Asserts simulated excesses have the generalized Pareto mean of their season within five
    /// standard errors.
    /// </summary>
    /// <param name="excesses">The simulated excesses over the threshold.</param>
    /// <param name="scale">The Hosking generalized Pareto scale.</param>
    /// <param name="kappa">The Hosking generalized Pareto shape.</param>
    /// <param name="context">The assertion context.</param>
    private static void AssertMeanExcess(List<double> excesses, double scale, double kappa, string context)
    {
        double mean = scale / (1.0 + kappa);
        double standardDeviation = scale / ((1.0 + kappa) * Math.Sqrt(1.0 + 2.0 * kappa));
        double standardError = standardDeviation / Math.Sqrt(excesses.Count);
        Assert.AreEqual(mean, excesses.Average(), 5.0 * standardError, $"{context}: mean excess");
    }

    /// <summary>
    /// Computes the Hosking GEV location whose threshold intensity and conditional excess law
    /// equal a generalized Pareto process with the given scale, shape and annual intensity.
    /// </summary>
    /// <param name="gpaScale">The generalized Pareto scale at the threshold.</param>
    /// <param name="kappa">The Hosking shape.</param>
    /// <param name="intensity">The annual threshold intensity.</param>
    /// <returns>The GEV location.</returns>
    private static double GevLocation(double gpaScale, double kappa, double intensity) =>
        Threshold + gpaScale / kappa * (1.0 - Math.Pow(intensity, -kappa));

    /// <summary>
    /// Computes the Hosking GEV scale paired with <see cref="GevLocation"/>.
    /// </summary>
    /// <param name="gpaScale">The generalized Pareto scale at the threshold.</param>
    /// <param name="kappa">The Hosking shape.</param>
    /// <param name="intensity">The annual threshold intensity.</param>
    /// <returns>The GEV scale.</returns>
    private static double GevScale(double gpaScale, double kappa, double intensity) =>
        gpaScale * Math.Pow(intensity, -kappa);

    /// <summary>
    /// Creates a seasonal point-process model with explicit threshold and observation span.
    /// </summary>
    /// <returns>The configured model with eight default parameters.</returns>
    private static PointProcessModel CreateSeasonalModel()
    {
        var model = new PointProcessModel
        {
            UseDefaults = false,
            IsSeasonal = true,
            TimeBlock = TimeBlockWindow.CalendarYear,
            StartMonth = 1,
            DataFrame = CreateSeasonalFrame(),
        };
        model.Threshold = Threshold;
        model.TotalYears = 10.0;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>
    /// Creates a seasonal peaks-over-threshold frame with one winter and one summer event per year.
    /// </summary>
    /// <returns>The data frame.</returns>
    private static BestFitDataFrame CreateSeasonalFrame()
    {
        var data = new List<ExactData>();
        for (int year = 1990; year < 2000; year++)
        {
            data.Add(new ExactData(new DateTime(year, 2, 15), 1500 + (year - 1990) * 100));
            data.Add(new ExactData(new DateTime(year, 7, 15), 2000 + (year - 1990) * 150));
        }

        return new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
    }

    /// <summary>
    /// Creates a twenty-value annual maximum frame.
    /// </summary>
    /// <returns>The data frame.</returns>
    private static BestFitDataFrame CreateAnnualMaximumFrame()
    {
        var data = new List<ExactData>();
        for (int i = 0; i < 20; i++)
            data.Add(new ExactData(1980 + i, 2000 + i * 100));

        return new BestFitDataFrame { ExactSeries = new ExactSeries(data) };
    }
}
