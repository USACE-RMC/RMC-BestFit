using System.Text.Json;
using Microsoft.Extensions.Options;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="AdvancedAnalysisTools"/>: creation tools with contract parity.
    /// Run tools are not invoked (they execute estimators).
    /// </summary>
    [TestClass]
    public class AdvancedAnalysisToolsTests
    {
        /// <summary>
        /// The store shared by the tool stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private AdvancedAnalysisTools _tools = null!;

        /// <summary>
        /// Creates a fresh tool stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _tools = new AdvancedAnalysisTools(new AnalysisService(_store, Options.Create(new ApiOptions())));
        }

        /// <summary>
        /// Verifies the mixture creation tool parses component names and priors into the model.
        /// </summary>
        [TestMethod]
        public void CreateMixtureAnalysis_ParsesComponents()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            string json = _tools.CreateMixtureAnalysis(
                input.Id,
                distributions: new[] { "gumbel", "logNormal" },
                isZeroInflated: true,
                prngSeed: 11);

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("mixture", summary.GetProperty("kind").GetString());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            var model = resource.Mixture!.MixtureDistribution;
            Assert.IsTrue(model.IsZeroInflated);
            Assert.AreEqual(2, model.Mixture!.Distributions.Length);
            Assert.AreEqual(11, resource.Mixture.BayesianAnalysis.PRNGSeed);

            Assert.ThrowsException<ArgumentException>(() => _tools.CreateMixtureAnalysis(
                input.Id, distributions: new[] { "notADistribution" }));
        }

        /// <summary>
        /// Verifies the point process creation tool parses seasonal options and overrides.
        /// </summary>
        [TestMethod]
        public void CreatePointProcessAnalysis_ParsesOptions()
        {
            var input = _store.AddInputData(new InputDataResource
            {
                Name = "pot",
                DataFrame = TestAnalyses.CreatePotDataFrame(),
                Method = InputDataMethod.PeaksOverThreshold,
                Threshold = 400d
            });

            string json = _tools.CreatePointProcessAnalysis(
                input.Id,
                isSeasonal: true,
                timeBlock: "waterYear",
                startMonth: 10,
                totalYears: 20d);

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("pointProcess", summary.GetProperty("kind").GetString());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            var model = resource.PointProcess!.PointProcess;
            Assert.IsTrue(model.IsSeasonal);
            Assert.AreEqual(10, model.StartMonth);
            Assert.AreEqual(20d, model.TotalYears);
        }

        /// <summary>
        /// Verifies the composite creation tool aligns weights with component ids and rejects
        /// misaligned arrays.
        /// </summary>
        [TestMethod]
        public void CreateCompositeAnalysis_AlignsWeights()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            var componentA = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var componentB = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            string json = _tools.CreateCompositeAnalysis(
                new[] { componentA.Id, componentB.Id },
                compositeType: "mixture",
                weights: new[] { 0.6, 0.3 });

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("composite", summary.GetProperty("kind").GetString());
            Assert.AreEqual("mixture", summary.GetProperty("compositeType").GetString());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            Assert.AreEqual(0.6, resource.Composite!.Analyses[0].Weight);
            Assert.AreEqual(0.3, resource.Composite.Analyses[1].Weight);

            Assert.ThrowsException<ArgumentException>(() => _tools.CreateCompositeAnalysis(
                new[] { componentA.Id }, weights: new[] { 0.5, 0.5 }));
        }

        /// <summary>
        /// Verifies the distribution-fitting creation tool honors a candidate subset.
        /// </summary>
        [TestMethod]
        public void CreateDistributionFittingAnalysis_ParsesSubset()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            string json = _tools.CreateDistributionFittingAnalysis(
                input.Id,
                distributions: new[] { "logPearsonTypeIII", "generalizedExtremeValue" });

            using var document = JsonDocument.Parse(json);
            var resource = _store.GetAnalysis(document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid())!;
            Assert.AreEqual(2, resource.DistributionFitting!.DistributionList.Count);
        }

        /// <summary>
        /// Verifies the bivariate creation tool zips the parallel ordinate arrays and rejects
        /// misaligned ones.
        /// </summary>
        [TestMethod]
        public void CreateBivariateAnalysis_ZipsOrdinates()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            var marginalX = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var marginalY = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });

            string json = _tools.CreateBivariateAnalysis(
                marginalX.Id, marginalY.Id,
                xOrdinates: new[] { 400d, 200d },
                yOrdinates: new[] { 430d, 220d },
                copulaType: "gumbel");

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("bivariate", summary.GetProperty("kind").GetString());
            Assert.AreEqual("gumbel", summary.GetProperty("copulaType").GetString());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            Assert.AreEqual(2, resource.Bivariate!.XYOrdinates.Count);
            Assert.AreEqual(200d, resource.Bivariate.XYOrdinates[0].X, "The grid must be sorted by x.");

            Assert.ThrowsException<ArgumentException>(() => _tools.CreateBivariateAnalysis(
                marginalX.Id, marginalY.Id, xOrdinates: new[] { 1d }, yOrdinates: new[] { 1d, 2d }));
        }

        /// <summary>
        /// Verifies the coincident frequency creation tool parses the surface JSON and rejects
        /// malformed payloads.
        /// </summary>
        [TestMethod]
        public void CreateCoincidentFrequencyAnalysis_ParsesSurfaceJson()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            var marginalX = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var marginalY = service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var bivariate = service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 1d, Y = 1d } }
            });

            string json = _tools.CreateCoincidentFrequencyAnalysis(
                bivariate.Id,
                xValues: new[] { 1d, 2d },
                yValues: new[] { 10d, 20d },
                bivariateResponseJson: "[[100,110],[120,130]]",
                numberOfBins: 25);

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("coincidentFrequency", summary.GetProperty("kind").GetString());
            Assert.AreEqual(25, summary.GetProperty("numberOfBins").GetInt32());

            var parseError = Assert.ThrowsException<ArgumentException>(() => _tools.CreateCoincidentFrequencyAnalysis(
                bivariate.Id, new[] { 1d }, new[] { 1d }, bivariateResponseJson: "not json"));
            StringAssert.Contains(parseError.Message, "bivariateResponseJson");
        }

        /// <summary>
        /// Verifies the time-series creation tool parses the model type and family options.
        /// </summary>
        [TestMethod]
        public void CreateTimeSeriesAnalysis_ParsesModelType()
        {
            var source = _store.AddTimeSeries(new TimeSeriesResource(TestSeries.DailyThreeWaterYears())
            {
                Name = "daily",
                Source = TimeSeriesSource.Manual
            });

            string json = _tools.CreateTimeSeriesAnalysis(
                source.Id,
                modelType: "arima",
                pOrder: 2,
                dOrder: 1,
                transformType: "logarithmic",
                forecastingTimeSteps: 12);

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("timeSeries", summary.GetProperty("kind").GetString());
            Assert.AreEqual("arima", summary.GetProperty("timeSeriesModelType").GetString());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            Assert.AreEqual(2, resource.Arima!.ARIMA.POrder);
            Assert.AreEqual(RMC.BestFit.Models.Transform.Logarithmic, resource.Arima.ARIMA.TransformType);
            Assert.AreEqual(12, resource.Arima.ForecastingTimeSteps);

            var badField = Assert.ThrowsException<ArgumentException>(() => _tools.CreateTimeSeriesAnalysis(
                source.Id, modelType: "ar", pOrder: 2));
            StringAssert.Contains(badField.Message, "pOrder");

            var blankType = Assert.ThrowsException<ArgumentException>(() => _tools.CreateTimeSeriesAnalysis(
                source.Id, modelType: " "));
            StringAssert.Contains(blankType.Message, "modelType");
        }

        /// <summary>
        /// Verifies the competing risks creation tool parses component names and priors.
        /// </summary>
        [TestMethod]
        public void CreateCompetingRisksAnalysis_ParsesComponents()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var probe = new RMC.BestFit.Models.CompetingRisksModel(TestAnalyses.CreateDataFrame(),
                new List<UnivariateDistributionType> { UnivariateDistributionType.Gumbel, UnivariateDistributionType.GeneralizedExtremeValue });
            string parameterName = probe.Parameters[0].DisplayName;

            string json = _tools.CreateCompetingRisksAnalysis(
                input.Id,
                distributions: new[] { "gumbel", "generalizedExtremeValue" },
                parameterPriors: new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = parameterName,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Normal,
                            Parameters = new List<double> { 0d, 10d }
                        }
                    }
                });

            using var document = JsonDocument.Parse(json);
            var resource = _store.GetAnalysis(document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid())!;
            var model = resource.CompetingRisks!.CompetingRisksDistribution;
            Assert.AreEqual(2, model.CompetingRisks!.Distributions.Count);
            Assert.IsFalse(model.UseDefaultFlatPriors);
            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[0].PriorDistribution.Type);
        }
    }
}
