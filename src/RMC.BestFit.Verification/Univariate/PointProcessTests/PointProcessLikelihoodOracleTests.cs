using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Verifies mixed-observation point-process likelihoods against independently coded formulas.
/// </summary>
/// <remarks>
/// These are direct numerical likelihood oracles. They invoke neither an optimizer nor a sampler.
/// The seasonal oracle treats annual non-exact observations as draws from the annual maximum of
/// the two exposure-adjusted seasonal Poisson processes.
/// </remarks>
[TestClass]
public class PointProcessLikelihoodOracleTests
{
    private const double LocationOne = 100.0;
    private const double ScaleOne = 20.0;
    private const double ColesShapeOne = 0.10;
    private const double ColesShapeTwo = -0.05;
    private const double Threshold = 80.0;
    private const int ChangePointOne = 80;
    private const int ChangePointTwo = 260;

    /// <summary>
    /// Verifies the nonseasonal exact, uncertain, interval, and threshold likelihood terms against
    /// a separately coded calculation.
    /// </summary>
    /// <remarks>
    /// The independent oracle combines the Poisson exposure term, intensity-density exact terms,
    /// numerical convolution for measurement error, and GEV probability masses for censored rows.
    /// </remarks>
    [TestMethod]
    public void NonseasonalMixedObservations_MatchIndependentLikelihood()
    {
        const double observationYears = 10.0;
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
        PointProcessModel model = CreateNonseasonalModel(frame, observationYears);
        double[] parameters = { LocationOne, ScaleOne, -ColesShapeOne };
        model.SetParameterValues(parameters);
        frame.ProcessThresholdSeries();

        double expected = IndependentNonseasonalMixedLogLikelihood(frame, observationYears);
        double actual = model.DataLogLikelihood(parameters);

        Assert.AreEqual(expected, actual, 2E-7, "Mixed point-process likelihood disagreed with the independent calculation.");
        Assert.AreEqual(2, model.EmpiricalEventCount, "Non-exact rows must not become Poisson events.");
    }

    /// <summary>
    /// Verifies that seasonal non-exact records use the annual maximum of the two
    /// exposure-adjusted seasonal processes.
    /// </summary>
    /// <remarks>
    /// Exact dated events retain their season-specific intensity density. Uncertain, interval, and
    /// threshold rows are annual observations and therefore use the independent competing-process
    /// annual-maximum distribution.
    /// </remarks>
    [TestMethod]
    public void SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood()
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
                new ThresholdData(2003, 2005, 120.0) { NumberAbove = 2 }
            })
        };
        PointProcessModel model = CreateSeasonalModel(frame, observationYears);
        double[] parameters =
        {
            ChangePointOne + 0.5,
            ChangePointTwo + 0.5,
            LocationOne,
            ScaleOne,
            -ColesShapeOne,
            locationTwo,
            scaleTwo,
            -ColesShapeTwo
        };
        model.SetParameterValues(parameters);
        frame.ProcessThresholdSeries();
        var thresholdRecord = (ThresholdData)frame.ThresholdSeries[0];

        double weightOne = (ChangePointOne + 366.0 - ChangePointTwo) / 366.0;
        double weightTwo = (ChangePointTwo - ChangePointOne) / 366.0;
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
        expected += thresholdRecord.NumberBelow * Math.Log(annualCdf(thresholdRecord.Value));
        expected += thresholdRecord.NumberAbove * Math.Log(1.0 - annualCdf(thresholdRecord.Value));

        double actual = model.DataLogLikelihood(parameters);

        Assert.AreEqual(expected, actual, 2E-7, "Seasonal mixed likelihood disagreed with the independently calculated annual maximum distribution.");
        Assert.AreEqual(1, thresholdRecord.NumberBelow, "The fixture must retain one left-censored annual threshold observation.");
        Assert.AreEqual(2, thresholdRecord.NumberAbove, "The fixture must retain two right-censored annual threshold observations.");
        Assert.AreEqual(2, model.EmpiricalEventCount, "Annualized non-exact rows must not become seasonal Poisson events.");
    }

    /// <summary>Creates a configured nonseasonal point-process model.</summary>
    /// <param name="frame">The mixed observation data.</param>
    /// <param name="observationYears">The point-process exposure.</param>
    /// <returns>The configured model.</returns>
    private static PointProcessModel CreateNonseasonalModel(DataFrame frame, double observationYears)
    {
        var model = new PointProcessModel { UseDefaults = false, DataFrame = frame };
        model.Threshold = Threshold;
        model.TotalYears = observationYears;
        model.SetDefaultParameters();
        return model;
    }

    /// <summary>Creates a configured seasonal point-process model.</summary>
    /// <param name="frame">The mixed observation data.</param>
    /// <param name="observationYears">The point-process exposure.</param>
    /// <returns>The configured model.</returns>
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
        SetChangePointPrior(model.Parameters[0], 10.0, 100.0, ChangePointOne + 0.5);
        SetChangePointPrior(model.Parameters[1], 200.0, 360.0, ChangePointTwo + 0.5);
        return model;
    }

    /// <summary>Applies a bounded continuous flat prior to a seasonal changepoint.</summary>
    /// <param name="parameter">The changepoint parameter.</param>
    /// <param name="lower">The inclusive lower bound.</param>
    /// <param name="upper">The inclusive upper bound.</param>
    /// <param name="initial">The retained initial value.</param>
    private static void SetChangePointPrior(ModelParameter parameter, double lower, double upper, double initial)
    {
        parameter.LowerBound = lower;
        parameter.UpperBound = upper;
        parameter.Value = initial;
        parameter.PriorDistribution = new Uniform(lower, upper);
    }

    /// <summary>Computes the nonseasonal mixed likelihood independently from production code.</summary>
    /// <param name="frame">The mixed data frame after threshold-count processing.</param>
    /// <param name="observationYears">The point-process exposure.</param>
    /// <returns>The independently calculated log-likelihood.</returns>
    private static double IndependentNonseasonalMixedLogLikelihood(DataFrame frame, double observationYears)
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

    /// <summary>Evaluates the analytical threshold intensity.</summary>
    /// <param name="location">The GEV location.</param>
    /// <param name="scale">The GEV scale.</param>
    /// <param name="shape">The Coles shape.</param>
    /// <param name="threshold">The point-process threshold.</param>
    /// <returns>The annual threshold intensity.</returns>
    private static double ThresholdIntensity(double location, double scale, double shape, double threshold)
    {
        return Math.Pow(1.0 + shape * (threshold - location) / scale, -1.0 / shape);
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
