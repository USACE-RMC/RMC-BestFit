using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Analyses;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Support
{
    /// <summary>
    /// Builders for small, valid analysis resources used by service/mapper/controller tests.
    /// Analyses are only constructed and configured — never run (no MCMC/GMM in unit tests).
    /// </summary>
    public static class TestAnalyses
    {
        /// <summary>
        /// Builds a data frame with a deterministic 20-year systematic record (values 100..480).
        /// </summary>
        /// <param name="count">The number of annual observations. Default 20.</param>
        /// <returns>The data frame.</returns>
        public static DataFrame CreateDataFrame(int count = 20)
        {
            var dataFrame = new DataFrame();
            dataFrame.ExactSeries.SuppressCollectionChanged = true;
            for (int i = 0; i < count; i++)
            {
                dataFrame.ExactSeries.Add(new ExactData(2000 + i, 100d + 20d * i));
            }
            dataFrame.ExactSeries.SuppressCollectionChanged = false;
            dataFrame.ExactSeries.RaiseCollectionChangedReset();
            return dataFrame;
        }

        /// <summary>
        /// Builds a data frame with the standard 20-year systematic record plus two uncertain
        /// observations (historical floods described by measurement-error distributions).
        /// </summary>
        /// <returns>The data frame.</returns>
        public static DataFrame CreateDataFrameWithUncertain()
        {
            var dataFrame = CreateDataFrame();
            dataFrame.UncertainSeries.SuppressCollectionChanged = true;
            dataFrame.UncertainSeries.Add(new UncertainData(1902, new Normal(600d, 60d)));
            dataFrame.UncertainSeries.Add(new UncertainData(1875, new Triangular(500d, 650d, 900d)));
            dataFrame.UncertainSeries.SuppressCollectionChanged = false;
            dataFrame.UncertainSeries.RaiseCollectionChangedReset();
            return dataFrame;
        }

        /// <summary>
        /// Builds an input-data resource over <see cref="CreateDataFrame"/>.
        /// </summary>
        /// <param name="name">The resource name.</param>
        /// <returns>The input-data resource.</returns>
        public static InputDataResource CreateInputDataResource(string name = "input data")
        {
            return new InputDataResource
            {
                Name = name,
                DataFrame = CreateDataFrame(),
                Method = InputDataMethod.Manual
            };
        }

        /// <summary>
        /// Builds an unrun univariate analysis resource over a fresh data frame.
        /// </summary>
        /// <param name="type">The distribution type. Default Log-Pearson Type III.</param>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateUnivariateResource(UnivariateDistributionType type = UnivariateDistributionType.LogPearsonTypeIII)
        {
            var analysis = new UnivariateAnalysis(new UnivariateDistribution(CreateDataFrame(), type));
            return new AnalysisResource { Name = "univariate", Kind = AnalysisKind.Univariate, Univariate = analysis };
        }

        /// <summary>
        /// Builds an unrun Bulletin 17C analysis resource over a fresh data frame.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateBulletin17CResource()
        {
            var analysis = new Bulletin17CAnalysis(new Bulletin17CDistribution(CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII));
            return new AnalysisResource { Name = "b17c", Kind = AnalysisKind.Bulletin17C, Bulletin17C = analysis };
        }

        /// <summary>
        /// Builds a peaks-over-threshold data frame extracted from a 15-water-year synthetic
        /// daily series (one planted peak per year) — the same extraction path the input-data
        /// service uses, with enough exceedances for point process parameter constraints.
        /// </summary>
        /// <param name="threshold">The extraction threshold. Default 400 (below the planted peaks).</param>
        /// <returns>The data frame.</returns>
        public static DataFrame CreatePotDataFrame(double threshold = 400d)
        {
            var dataFrame = new DataFrame();
            dataFrame.CreatePeaksOverThresholdSeries(TestSeries.DailyWithAnnualPeaks(), threshold, minStepsBetweenPeaks: 7);
            return dataFrame;
        }

        /// <summary>
        /// Builds an unrun mixture analysis resource with two components over a fresh data frame.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateMixtureResource()
        {
            var model = new MixtureModel(CreateDataFrame(),
                new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel, UnivariateDistributionType.LogNormal });
            return new AnalysisResource { Name = "mixture", Kind = AnalysisKind.Mixture, Mixture = new MixtureAnalysis(model) };
        }

        /// <summary>
        /// Builds an unrun point process analysis resource over a fresh POT data frame.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreatePointProcessResource()
        {
            var model = new PointProcessModel();
            model.DataFrame = CreatePotDataFrame();
            return new AnalysisResource { Name = "pointprocess", Kind = AnalysisKind.PointProcess, PointProcess = new PointProcessAnalysis(model) };
        }

        /// <summary>
        /// Builds an unrun competing risks analysis resource with two components over a fresh data frame.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateCompetingRisksResource()
        {
            var model = new CompetingRisksModel(CreateDataFrame(),
                new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel, UnivariateDistributionType.GeneralizedExtremeValue });
            return new AnalysisResource { Name = "competingrisks", Kind = AnalysisKind.CompetingRisks, CompetingRisks = new CompetingRiskAnalysis(model) };
        }

        /// <summary>
        /// Builds an unrun composite analysis resource (competing risks type) over two fresh,
        /// unrun univariate component resources, with live component references wired the way the
        /// service wires them.
        /// </summary>
        /// <returns>The composite resource; its components are reachable via ComponentResources.</returns>
        public static AnalysisResource CreateCompositeResource()
        {
            var componentA = CreateUnivariateResource(UnivariateDistributionType.Gumbel);
            var componentB = CreateUnivariateResource(UnivariateDistributionType.LogNormal);
            var analysis = new CompositeAnalysis(new List<WeightedUnivariateAnalysis>
            {
                new(componentA.Univariate!, 0d),
                new(componentB.Univariate!, 0d)
            });
            return new AnalysisResource
            {
                Name = "composite",
                Kind = AnalysisKind.Composite,
                Composite = analysis,
                ComponentResources = new List<AnalysisResource> { componentA, componentB },
                ComponentAnalysisIds = new List<Guid> { componentA.Id, componentB.Id }
            };
        }

        /// <summary>
        /// Builds an unrun bivariate copula analysis resource over two fresh, unrun univariate
        /// marginal resources whose frames share all 20 time indexes, with live references wired
        /// the way the service wires them.
        /// </summary>
        /// <returns>The bivariate resource; its marginals are reachable via MarginalX/YResource.</returns>
        public static AnalysisResource CreateBivariateResource()
        {
            var marginalX = CreateUnivariateResource(UnivariateDistributionType.Gumbel);
            var marginalY = CreateUnivariateResource(UnivariateDistributionType.LogNormal);
            var distribution = new BivariateDistribution(
                marginalX.Univariate!.UnivariateDistribution,
                marginalY.Univariate!.UnivariateDistribution,
                Numerics.Distributions.Copulas.CopulaType.Normal);
            var analysis = new BivariateAnalysis(distribution)
            {
                XYOrdinates = new UncertainOrderedPairedData(
                    new List<UncertainOrdinate>
                    {
                        new(200d, new Deterministic(220d)),
                        new(400d, new Deterministic(430d))
                    },
                    false, SortOrder.Ascending, false, SortOrder.Ascending,
                    UnivariateDistributionType.Deterministic)
            };
            return new AnalysisResource
            {
                Name = "bivariate",
                Kind = AnalysisKind.Bivariate,
                Bivariate = analysis,
                MarginalXResource = marginalX,
                MarginalYResource = marginalY,
                ComponentResources = new List<AnalysisResource> { marginalX, marginalY },
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id
            };
        }

        /// <summary>
        /// Builds an unrun coincident frequency analysis resource over a fresh bivariate resource
        /// with a small strictly-monotone 3x3 response surface.
        /// </summary>
        /// <returns>The coincident frequency resource; the upstream is reachable via BivariateResource.</returns>
        public static AnalysisResource CreateCoincidentFrequencyResource()
        {
            var bivariate = CreateBivariateResource();
            var analysis = new CoincidentFrequencyAnalysis(
                bivariate.Bivariate!,
                new[] { 100d, 200d, 300d },
                new[] { 50d, 100d, 150d },
                new double[,]
                {
                    { 10d, 11d, 12d },
                    { 13d, 14d, 15d },
                    { 16d, 17d, 18d }
                });
            var components = new List<AnalysisResource> { bivariate };
            components.AddRange(bivariate.ComponentResources!);
            return new AnalysisResource
            {
                Name = "cfa",
                Kind = AnalysisKind.CoincidentFrequency,
                CoincidentFrequency = analysis,
                BivariateResource = bivariate,
                ComponentResources = components,
                BivariateAnalysisId = bivariate.Id
            };
        }

        /// <summary>
        /// Builds an unrun distribution-fitting analysis resource over a fresh data frame.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateDistributionFittingResource()
        {
            return new AnalysisResource
            {
                Name = "fitting",
                Kind = AnalysisKind.DistributionFitting,
                DistributionFitting = new FittingAnalysis(CreateDataFrame())
            };
        }

        /// <summary>
        /// Builds an unrun time-series analysis resource of the requested model family over the
        /// synthetic three-water-year daily series.
        /// </summary>
        /// <param name="modelType">The model family. Default Ar.</param>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateTimeSeriesAnalysisResource(TimeSeriesModelType modelType = TimeSeriesModelType.Ar)
        {
            var series = TestSeries.DailyThreeWaterYears();
            return modelType switch
            {
                TimeSeriesModelType.Ar => new AnalysisResource
                {
                    Name = "ar",
                    Kind = AnalysisKind.TimeSeries,
                    TimeSeriesModel = TimeSeriesModelType.Ar,
                    Ar = new ARAnalysis(new AutoRegressive(series))
                },
                TimeSeriesModelType.Ma => new AnalysisResource
                {
                    Name = "ma",
                    Kind = AnalysisKind.TimeSeries,
                    TimeSeriesModel = TimeSeriesModelType.Ma,
                    Ma = new MAAnalysis(new MovingAverage(series))
                },
                TimeSeriesModelType.Arima => new AnalysisResource
                {
                    Name = "arima",
                    Kind = AnalysisKind.TimeSeries,
                    TimeSeriesModel = TimeSeriesModelType.Arima,
                    Arima = new ARIMAAnalysis(new ARIMA(series))
                },
                _ => new AnalysisResource
                {
                    Name = "arimax",
                    Kind = AnalysisKind.TimeSeries,
                    TimeSeriesModel = TimeSeriesModelType.Arimax,
                    Arimax = new ARIMAXAnalysis(new ARIMAX(series))
                }
            };
        }

        /// <summary>
        /// Builds a pair of date-aligned stage/discharge measurement series (15 shared dates).
        /// </summary>
        /// <returns>The stage and discharge series.</returns>
        public static (TimeSeries Stage, TimeSeries Discharge) CreateStageDischargePair()
        {
            var stage = new TimeSeries(TimeInterval.Irregular);
            var discharge = new TimeSeries(TimeInterval.Irregular);
            for (int i = 0; i < 15; i++)
            {
                var date = new DateTime(2010, 1, 1).AddMonths(i);
                double h = 2d + 0.5d * i;
                stage.Add(new SeriesOrdinate<DateTime, double>(date, h));
                discharge.Add(new SeriesOrdinate<DateTime, double>(date, 25d * Math.Pow(h - 1d, 1.6)));
            }
            return (stage, discharge);
        }

        /// <summary>
        /// Builds an unrun rating curve analysis resource over a fresh aligned measurement pair.
        /// </summary>
        /// <returns>The analysis resource.</returns>
        public static AnalysisResource CreateRatingCurveResource()
        {
            var (stage, discharge) = CreateStageDischargePair();
            var analysis = new RatingCurveAnalysis(new RatingCurve(stage, discharge, numberOfSegments: 1));
            return new AnalysisResource { Name = "rating", Kind = AnalysisKind.RatingCurve, RatingCurve = analysis };
        }
    }
}
