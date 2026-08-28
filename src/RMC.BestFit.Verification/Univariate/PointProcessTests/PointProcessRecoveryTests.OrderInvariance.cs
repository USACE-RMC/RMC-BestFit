using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Verification.Univariate.PointProcessTests;

/// <summary>
/// Contains exact-series order-invariance checks for the seasonal point-process likelihood.
/// </summary>
public partial class PointProcessRecoveryTests
{
    /// <summary>
    /// Verifies that reordering the exact series leaves the seasonal mixed-observation likelihood on
    /// the independently calculated annual-maximum value, and that a permuted water-year sample
    /// reproduces the date-sorted likelihood.
    /// </summary>
    /// <remarks>
    /// The seasonal likelihood pairs each exact magnitude with its own block day, so the data
    /// log-likelihood is a symmetric function of the (magnitude, day) pairs. The oracle is the
    /// independent annual-maximum construction already established by
    /// <see cref="Test_SeasonalMixedObservationLikelihood_MatchesIndependentAnnualMaximumCalculation"/>;
    /// the acceptance rule adds exact invariance between orderings of the same record.
    /// </remarks>
    [TestMethod]
    public void Test_SeasonalMixedObservationLikelihood_IsOrderInvariantAndMatchesIndependentCalculation()
    {
        const double observationYears = 10.0;
        const double locationTwo = 130.0;
        const double scaleTwo = 30.0;
        var measurementError = new Normal(125.0, 4.0);
        DataFrame sortedFrame = CreateMixedSeasonalFrame(new List<ExactData>
        {
            new ExactData(new DateTime(2000, 1, 20), 105.0),
            new ExactData(new DateTime(2000, 5, 30), 140.0)
        }, new Normal(125.0, 4.0));
        DataFrame reversedFrame = CreateMixedSeasonalFrame(new List<ExactData>
        {
            new ExactData(new DateTime(2000, 5, 30), 140.0),
            new ExactData(new DateTime(2000, 1, 20), 105.0)
        }, new Normal(125.0, 4.0));
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
        PointProcessModel sortedModel = CreateSeasonalModel(sortedFrame, observationYears);
        sortedModel.SetParameterValues(parameters);
        sortedFrame.ProcessThresholdSeries();
        PointProcessModel reversedModel = CreateSeasonalModel(reversedFrame, observationYears);
        reversedModel.SetParameterValues(parameters);
        reversedFrame.ProcessThresholdSeries();

        double expected = IndependentSeasonalAnnualMaximumLogLikelihood(
            observationYears, locationTwo, scaleTwo, measurementError, (ThresholdData)sortedFrame.ThresholdSeries[0]);
        double llSorted = sortedModel.DataLogLikelihood(parameters);
        double llReversed = reversedModel.DataLogLikelihood(parameters);

        Assert.AreEqual(expected, llSorted, 2E-7, "The date-sorted seasonal mixed likelihood disagreed with the independent calculation.");
        Assert.AreEqual(expected, llReversed, 2E-7, "The reordered seasonal mixed likelihood disagreed with the independent calculation.");
        Assert.AreEqual(llSorted, llReversed, 1E-12, "Reordering the exact series changed the seasonal mixed likelihood.");

        DataFrame generated = PointProcessSeasonalFixture.Generate(
            300,
            52001,
            TimeBlockWindow.WaterYear,
            10,
            PointProcessSeasonalFixture.CalendarK1,
            PointProcessSeasonalFixture.CalendarK2,
            PointProcessSeasonalFixture.EventTiming.Uniform);
        var orderedEvents = generated.ExactSeries.Cast<ExactData>().ToList();
        var permutedEvents = orderedEvents.Select(item => new ExactData(item.DateTime, item.Value)).ToList();
        var permutation = new Random(12345);
        for (int i = permutedEvents.Count - 1; i > 0; i--)
        {
            int j = permutation.Next(i + 1);
            (permutedEvents[i], permutedEvents[j]) = (permutedEvents[j], permutedEvents[i]);
        }
        bool sameOrder = true;
        for (int i = 0; i < permutedEvents.Count; i++)
        {
            if (permutedEvents[i].DateTime != orderedEvents[i].DateTime || permutedEvents[i].Value != orderedEvents[i].Value)
            {
                sameOrder = false;
                break;
            }
        }
        Assert.IsFalse(sameOrder, "The permutation fixture failed to reorder the generated series.");
        var permutedFrame = new DataFrame
        {
            ExactSeries = new ExactSeries(permutedEvents),
            PointProcessObservationYears = generated.PointProcessObservationYears
        };
        PointProcessModel orderedModel = CreateAutomaticUniformModel(generated, TimeBlockWindow.WaterYear, 10);
        PointProcessModel permutedModel = CreateAutomaticUniformModel(permutedFrame, TimeBlockWindow.WaterYear, 10);
        double[] parentParameters = PointProcessSeasonalFixture.ParentParameters(
            PointProcessSeasonalFixture.CalendarK1 + 0.5,
            PointProcessSeasonalFixture.CalendarK2 + 0.5);
        double llOrdered = orderedModel.DataLogLikelihood(parentParameters);
        double llPermuted = permutedModel.DataLogLikelihood(parentParameters);

        Assert.IsTrue(double.IsFinite(llOrdered), "The date-sorted water-year likelihood was not finite at the parent parameters.");
        Assert.AreEqual(llOrdered, llPermuted, 1E-10, "Permuting the generated water-year series changed the seasonal likelihood.");
    }

    /// <summary>
    /// Creates the mixed seasonal observation frame with the exact events in the supplied order.
    /// </summary>
    /// <param name="exactEvents">The dated exact exceedances, in the order under test.</param>
    /// <param name="measurementError">The uncertain-observation measurement-error distribution.</param>
    /// <returns>A frame with one uncertain, one interval, and one threshold annual record.</returns>
    private static DataFrame CreateMixedSeasonalFrame(List<ExactData> exactEvents, Normal measurementError)
    {
        return new DataFrame
        {
            ExactSeries = new ExactSeries(exactEvents),
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
    }

    /// <summary>
    /// Independently computes the seasonal mixed-observation log-likelihood for the fixed frame of
    /// <see cref="CreateMixedSeasonalFrame"/> from the annual maximum of the two exposure-adjusted
    /// seasonal processes.
    /// </summary>
    /// <param name="observationYears">The known source exposure.</param>
    /// <param name="locationTwo">The season-two location.</param>
    /// <param name="scaleTwo">The season-two scale.</param>
    /// <param name="measurementError">The uncertain-observation measurement-error distribution.</param>
    /// <param name="thresholdRecord">The processed annual threshold record.</param>
    /// <returns>The independent log-likelihood value.</returns>
    /// <remarks>
    /// Mirrors the construction in
    /// <see cref="Test_SeasonalMixedObservationLikelihood_MatchesIndependentAnnualMaximumCalculation"/>:
    /// the 105.0 event on block day 20 belongs to the wrapped first season and the 140.0 event on
    /// block day 151 to the second season for changepoints at K1 + 0.5 and K2 + 0.5.
    /// </remarks>
    private static double IndependentSeasonalAnnualMaximumLogLikelihood(
        double observationYears,
        double locationTwo,
        double scaleTwo,
        Normal measurementError,
        ThresholdData thresholdRecord)
    {
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
        expected += thresholdRecord.NumberBelow * Math.Log(annualCdf(thresholdRecord.Value));
        expected += thresholdRecord.NumberAbove * Math.Log(1.0 - annualCdf(thresholdRecord.Value));
        return expected;
    }
}
