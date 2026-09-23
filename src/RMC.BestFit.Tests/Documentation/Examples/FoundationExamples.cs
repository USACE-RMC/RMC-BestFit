using Numerics.Distributions;
using Numerics.Functions;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Contains compile-checked examples used by the foundations chapters.
    /// </summary>
    internal static class FoundationExamples
    {
        #region doc:foundations-likelihood-decomposition
        private static (double Data, double Prior, double Posterior) EvaluateLikelihood()
        {
            var dataFrame = new global::RMC.BestFit.Models.DataFrame
            {
                ExactSeries = new ExactSeries(new[] { 1040.0, 1250.0, 1510.0, 1870.0 })
            };

            var model = new UnivariateDistribution(
                dataFrame,
                UnivariateDistributionType.Normal);

            double[] parameters = { 1400.0, 350.0 };
            double data = model.DataLogLikelihood(parameters);
            double prior = model.PriorLogLikelihood(parameters);
            double posterior = model.LogLikelihood(parameters);

            return (data, prior, posterior);
        }
        #endregion

        #region doc:data-frame-mixed-observations
        private static global::RMC.BestFit.Models.DataFrame CreateMixedDataFrame()
        {
            var threshold = new ThresholdData(1900, 1949, 1000.0)
            {
                NumberAbove = 3
            };

            return new global::RMC.BestFit.Models.DataFrame
            {
                ExactSeries = new ExactSeries(new[]
                {
                    new ExactData(1950, 1210.0),
                    new ExactData(1951, 1675.0)
                }),
                UncertainSeries = new UncertainSeries(new[]
                {
                    new UncertainData(1890, new Normal(1550.0, 180.0))
                }),
                IntervalSeries = new IntervalSeries(new[]
                {
                    new IntervalData(1880, 1100.0, 1300.0, 1500.0)
                }),
                ThresholdSeries = new ThresholdSeries(new[] { threshold })
            };
        }
        #endregion

        #region doc:trend-linear-prediction
        private static double PredictLocationIn2050()
        {
            var trend = new LinearTrend
            {
                StartIndex = 2000
            };
            trend.Parameters[0].Value = 1200.0;
            trend.Parameters[1].Value = 4.0;

            return trend.Predict(2050);
        }
        #endregion

        #region doc:links-centered-log
        private static (double LinkValue, double RoundTrip) TransformPositiveScale()
        {
            ILinkFunction positiveLink = new LogLink();
            var centered = new CenteredLink(positiveLink, mu0: 0.0, scale: 100.0);

            const double naturalScale = 250.0;
            double linkValue = centered.Link(naturalScale);
            double roundTrip = centered.InverseLink(linkValue);

            return (linkValue, roundTrip);
        }
        #endregion
    }
}
