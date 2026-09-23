using Numerics.Distributions;
using Numerics.Distributions.Copulas;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;

namespace RMC.BestFit.Tests.Documentation.Examples
{
    /// <summary>
    /// Compile-checked examples used by the bivariate, coincident-frequency,
    /// and spatial-extremes chapters.
    /// </summary>
    internal static class BivariateAndSpatialExamples
    {
        #region doc:bivariate-workflow
        private static BivariateAnalysis ConfigureBivariateAnalysis()
        {
            double[] peakFlow =
            {
                1_240, 1_510, 1_370, 1_860, 2_110, 1_740,
                2_430, 2_080, 2_760, 2_350, 3_020, 2_690
            };
            double[] floodVolume =
            {
                18.2, 22.5, 20.1, 27.8, 31.4, 25.7,
                35.9, 30.6, 40.8, 34.2, 44.7, 39.1
            };

            var peakData = new RMC.BestFit.Models.DataFrame
            {
                ExactSeries = new ExactSeries(peakFlow)
            };
            var volumeData = new RMC.BestFit.Models.DataFrame
            {
                ExactSeries = new ExactSeries(floodVolume)
            };

            var peakModel = new UnivariateDistribution(
                peakData,
                UnivariateDistributionType.GeneralizedExtremeValue);
            var volumeModel = new UnivariateDistribution(
                volumeData,
                UnivariateDistributionType.LogNormal);

            var jointModel = new BivariateDistribution(
                peakModel,
                volumeModel,
                CopulaType.Gumbel)
            {
                CopulaEstimationMethod =
                    CopulaEstimationMethod.InferenceFromMargins
            };
            jointModel.SetSampleData();

            return new BivariateAnalysis(jointModel);
        }
        #endregion

        #region doc:coincident-frequency-workflow
        private static CoincidentFrequencyAnalysis ConfigureCoincidentFrequency(
            BivariateAnalysis fittedJointModel)
        {
            double[] peakFlow = { 1_000, 2_000, 3_000, 4_000 };
            double[] floodVolume = { 10, 20, 30 };
            double[,] maximumPoolElevation =
            {
                { 1_205.1, 1_206.3, 1_207.4 },
                { 1_208.0, 1_209.5, 1_211.0 },
                { 1_211.2, 1_213.0, 1_214.8 },
                { 1_214.0, 1_216.1, 1_218.3 }
            };

            return new CoincidentFrequencyAnalysis(
                fittedJointModel,
                peakFlow,
                floodVolume,
                maximumPoolElevation)
            {
                NumberOfBins = 100
            };
        }
        #endregion

        #region doc:spatial-gev-workflow
        private static SpatialGEVAnalysis ConfigureSpatialGevAnalysis()
        {
            double[,] annualMaximumFlow =
            {
                { 1_240, 1_820, 2_810, 3_640, 4_930, 6_110 },
                { 1_510, 2_090, 3_260, 4_020, 5_410, 6_750 },
                { 1_370, 1_960, 3_040, 3_810, 5_120, 6_430 },
                { 1_860, 2_480, 3_790, 4_690, 6_080, 7_520 },
                { 2_110, 2_730, 4_120, 5_060, 6_540, 8_010 },
                { 1_740, 2_310, 3_510, 4_390, 5_770, 7_160 },
                { 2_430, 3_090, 4_480, 5_510, 7_030, 8_640 },
                { 2_080, 2_690, 4_060, 4_970, 6_420, 7_910 },
                { 2_760, 3_410, 4_910, 6_020, 7_590, 9_180 },
                { 2_350, 2_980, 4_370, 5_360, 6_880, 8_430 }
            };
            double[,] projectedCoordinatesKm =
            {
                { 12.0, 18.0 },
                { 29.0, 24.0 },
                { 46.0, 31.0 },
                { 61.0, 43.0 },
                { 79.0, 57.0 },
                { 96.0, 66.0 }
            };
            double[,] standardizedSiteCovariates =
            {
                { -1.31, -1.18 },
                { -0.78, -0.61 },
                { -0.24, -0.16 },
                {  0.29,  0.22 },
                {  0.82,  0.71 },
                {  1.22,  1.02 }
            };

            var location = new GeneralLinearFunction(
                "GEV location",
                standardizedSiteCovariates);
            var scale = new GeneralLinearFunction(
                "GEV scale",
                standardizedSiteCovariates);
            var shape = new GeneralLinearFunction("GEV shape");

            var model = new SpatialGEV(
                annualMaximumFlow,
                projectedCoordinatesKm,
                location,
                scale,
                shape);
            model.ConfigureForProperCoverage(
                CorrelationFunctionType.PoweredExponential,
                includeScaleErrors: false,
                includeShapeErrors: false,
                useWeightedLikelihood: false);

            return new SpatialGEVAnalysis(model);
        }
        #endregion
    }
}
