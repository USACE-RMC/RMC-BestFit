using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Compile-checked examples used by the time-series and rating-curve chapters.
    /// </summary>
    internal static class TimeSeriesAndRatingCurveExamples
    {
        #region doc:autoregressive-workflow
        private static ARAnalysis ConfigureAutoregressiveAnalysis()
        {
            double[] annualFlow =
            {
                112, 128, 121, 139, 151, 147, 160, 158, 171, 166,
                179, 185, 181, 194, 202, 198, 211, 219, 214, 228
            };
            var series = new NumericsTimeSeries(
                TimeInterval.OneYear,
                new DateTime(2000, 1, 1),
                annualFlow);

            var model = new AutoRegressive(
                series,
                order: 1,
                includeIntercept: true)
            {
                UseDefaultTrainingSteps = false,
                UseJeffreysRuleForScale = true,
                TransformType = RMC.BestFit.Models.Transform.None
            };
            model.TrainingTimeSteps = 16;
            model.SetDefaultParameters();

            return new ARAnalysis(model)
            {
                ForecastingTimeSteps = 4
            };
        }
        #endregion

        #region doc:moving-average-workflow
        private static MAAnalysis ConfigureMovingAverageAnalysis()
        {
            double[] monthlyAnomaly =
            {
                0.3, -0.2, 0.1, 0.5, -0.4, 0.2,
                0.0, 0.4, -0.1, -0.3, 0.2, 0.1,
                -0.2, 0.3, 0.0, -0.1, 0.4, -0.2
            };
            var series = new NumericsTimeSeries(
                TimeInterval.OneMonth,
                new DateTime(2020, 1, 1),
                monthlyAnomaly);

            var model = new MovingAverage(
                series,
                order: 1,
                includeIntercept: true)
            {
                UseDefaultTrainingSteps = false,
                UseJeffreysRuleForScale = true
            };
            model.TrainingTimeSteps = 15;
            model.SetDefaultParameters();

            return new MAAnalysis(model)
            {
                ForecastingTimeSteps = 3
            };
        }
        #endregion

        #region doc:arima-workflow
        private static ARIMAAnalysis ConfigureArimaAnalysis()
        {
            double[] annualStorage =
            {
                410, 422, 431, 447, 452, 468, 475, 489, 501, 496,
                514, 526, 531, 548, 559, 571, 580, 594, 607, 615
            };
            var series = new NumericsTimeSeries(
                TimeInterval.OneYear,
                new DateTime(2000, 1, 1),
                annualStorage);

            var model = new ARIMA(
                series,
                pOrder: 1,
                dOrder: 1,
                qOrder: 1,
                includeIntercept: true)
            {
                UseDefaultTrainingSteps = false,
                UseJeffreysRuleForScale = true,
                TransformType = RMC.BestFit.Models.Transform.None
            };
            model.TrainingTimeSteps = 16;
            model.SetDefaultParameters();

            return new ARIMAAnalysis(model)
            {
                ForecastingTimeSteps = 4
            };
        }
        #endregion

        #region doc:arimax-workflow
        private static ARIMAXAnalysis ConfigureArimaxAnalysis()
        {
            double[] monthlyFlow =
            {
                92, 105, 121, 138, 151, 144, 132, 119, 108, 101, 96, 90,
                95, 109, 125, 141, 155, 148, 136, 122, 111, 103, 98, 93
            };
            double[] monthlyPrecipitation =
            {
                28, 35, 48, 61, 72, 65, 54, 43, 36, 31, 29, 26,
                30, 38, 51, 64, 75, 68, 57, 46, 39, 34, 31, 28
            };
            var start = new DateTime(2022, 1, 1);
            var response = new NumericsTimeSeries(
                TimeInterval.OneMonth,
                start,
                monthlyFlow);
            var precipitation = new NumericsTimeSeries(
                TimeInterval.OneMonth,
                start,
                monthlyPrecipitation);

            var model = new ARIMAX(response)
            {
                AROrderP = 1,
                DiffOrderD = 0,
                MAOrderQ = 1,
                XOrderB = 1,
                IncludeIntercept = true,
                IncludeSeasonality = true,
                TrendType = ARIMAX.Trend.None,
                CovariateExtension =
                    ARIMAX.CovariateExtensionMethod.None,
                UseDefaultTrainingSteps = false
            };
            model.TrainingTimeSteps = 20;
            model.SetCovariates(
                new List<NumericsTimeSeries> { precipitation });
            model.SetDefaultParameters();

            return new ARIMAXAnalysis(model)
            {
                ForecastingTimeSteps = 4
            };
        }
        #endregion

        #region doc:rating-curve-workflow
        private static (
            RatingCurveAnalysis Analysis,
            MaximumLikelihood InitialFit) ConfigureRatingCurveAnalysis()
        {
            double[] stage =
            {
                1.2, 1.4, 1.7, 2.0, 2.3, 2.7,
                3.1, 3.6, 4.1, 4.7, 5.3, 6.0
            };
            double[] discharge =
            {
                18, 29, 51, 79, 113, 169,
                238, 345, 476, 651, 861, 1_140
            };
            var start = new DateTime(2024, 1, 1);
            var stageSeries = new NumericsTimeSeries(
                TimeInterval.OneDay,
                start,
                stage);
            var dischargeSeries = new NumericsTimeSeries(
                TimeInterval.OneDay,
                start,
                discharge);

            var model = new RMC.BestFit.Models.RatingCurve(
                stageSeries,
                dischargeSeries,
                numberOfSegments: 1);
            var analysis = new RatingCurveAnalysis(model);
            var initialFit = new MaximumLikelihood(
                model,
                OptimizationMethod.DifferentialEvolution);

            return (analysis, initialFit);
        }
        #endregion
    }
}
