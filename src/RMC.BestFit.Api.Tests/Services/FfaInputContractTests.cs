using System.Text.Json;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>Exercises pre-fit FFA provenance and configuration contracts without estimation.</summary>
    [TestClass]
    public class FfaInputContractTests
    {
        /// <summary>Creates a fresh in-memory store.</summary>
        /// <returns>The empty resource store.</returns>
        private static InMemoryResourceStore Store() => new(Options.Create(new ApiOptions()));

        /// <summary>Requires a named additive MCP tool and reads its real serialized response.</summary>
        /// <param name="tools">The real input tools.</param>
        /// <param name="name">The CLR tool method name.</param>
        /// <param name="id">The stored resource identifier.</param>
        /// <returns>The JSON response.</returns>
        private static JsonElement Invoke(InputDataTools tools, string name, Guid id)
        {
            var method = typeof(InputDataTools).GetMethod(name);
            Assert.IsNotNull(method, $"Missing {name} contract");
            return JsonDocument.Parse((string)method.Invoke(tools, [id])!).RootElement.Clone();
        }

        /// <summary>Preserves explicit floods and inclusive threshold counts without fabricated dates.</summary>
        [TestMethod]
        public void Chronology_ReportsDatedFloodAndCensoredWindowBeforeFit()
        {
            var service = new InputDataService(Store(), new FakeUsgsTimeSeriesService());
            var request = new CreateManualInputDataRequest
            {
                ExactData = [new() { Index = 2000, Value = 100 }, new() { Index = 2001, Value = 200 }],
                IntervalData = [new() { Index = 1882, LowerBound = 115000, UpperBound = 150000 }],
                ThresholdData = [new() { StartIndex = 1870, EndIndex = 1922, Value = 110000, NumberAbove = 0 }]
            };
            var input = service.CreateManual(request);
            var result = Invoke(new InputDataTools(service), "GetInputDataChronology", input.Id);
            Assert.AreEqual(1, result.GetProperty("schemaVersion").GetInt32());
            Assert.AreEqual(input.Id, result.GetProperty("inputData").GetProperty("id").GetGuid());
            Assert.AreEqual(1, result.GetProperty("intervalData").GetArrayLength());
            Assert.AreEqual(52, result.GetProperty("thresholdData")[0].GetProperty("numberBelow").GetInt32());
            Assert.AreEqual(0, result.GetProperty("thresholdData")[0].GetProperty("numberAbove").GetInt32());
            request.ThresholdData[0].Value = 1;
            var source = Invoke(new InputDataTools(service), "GetInputDataSource", input.Id);
            Assert.AreEqual(110000d, source.GetProperty("request").GetProperty("thresholdData")[0].GetProperty("value").GetDouble());
        }

        /// <summary>Keeps raw USGS dates and qualifier text even when the numerical series omits them.</summary>
        [TestMethod]
        public async Task Source_PreservesRawUsgsTextAndChecksum()
        {
            const string raw = "# source\nsite_no\tpeak_dt\tpeak_cd\n01234567\t1980-10-04\t7\n";
            var service = new InputDataService(Store(), new FakeUsgsTimeSeriesService { Result = TestSeries.IrregularPeaks(), RawText = raw });
            var input = await service.CreateFromUsgsPeaksAsync(new() { SiteNumber = "01234567" });
            var result = Invoke(new InputDataTools(service), "GetInputDataSource", input.Id);
            Assert.AreEqual(raw, result.GetProperty("rawText").GetString());
            Assert.AreEqual(64, result.GetProperty("sha256").GetString()!.Length);
            Assert.AreEqual("01234567", result.GetProperty("request").GetProperty("siteNumber").GetString());
        }

        /// <summary>Exposes an existing model switch and effective configuration without changing defaults.</summary>
        [TestMethod]
        public void Univariate_JeffreysOverrideAndEffectivePriorsAreReported()
        {
            var store = Store();
            var input = store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(store, Options.Create(new ApiOptions()));
            var baseline = service.CreateUnivariate(new() { InputDataId = input.Id });
            var expectedDefault = baseline.Univariate!.UnivariateDistribution.UseJeffreysRuleForScale;
            var request = JsonSerializer.Deserialize<CreateUnivariateAnalysisRequest>(
                "{\"useJeffreysRuleForScale\":" + (!expectedDefault).ToString().ToLowerInvariant() + "}", TestJson.Options)!;
            request.InputDataId = input.Id;
            request.UseSingleQuantile = true;
            request.QuantilePriors = [new() { Alpha = 0.002, Distribution = new() { Type = Numerics.Distributions.UnivariateDistributionType.Normal, Parameters = [480, 80] } }];
            var resource = service.CreateUnivariate(request);
            Assert.AreEqual(!expectedDefault, resource.Univariate!.UnivariateDistribution.UseJeffreysRuleForScale);
            var response = JsonDocument.Parse(TestJson.Serialize(AnalysisMapper.ToResourceResponse(resource))).RootElement;
            var config = response.GetProperty("configuration");
            Assert.AreEqual(!expectedDefault, config.GetProperty("useJeffreysRuleForScale").GetBoolean());
            Assert.AreEqual(0.002, config.GetProperty("quantilePriors")[0].GetProperty("alpha").GetDouble());
            Assert.IsTrue(config.GetProperty("bayesianOptions").GetProperty("iterations").GetInt32() > 0);
        }

        /// <summary>Rejects a two-quantile LP3 request that the model cannot apply.</summary>
        [TestMethod]
        public void Univariate_RejectsUnsupportedQuantileCount()
        {
            var store = Store();
            var input = store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(store, Options.Create(new ApiOptions()));
            Assert.ThrowsException<ArgumentException>(() => service.CreateUnivariate(new()
            {
                InputDataId = input.Id,
                QuantilePriors = [
                    new() { Alpha = 0.01, Distribution = new() { Type = Numerics.Distributions.UnivariateDistributionType.Normal, Parameters = [480, 80] } },
                    new() { Alpha = 0.001, Distribution = new() { Type = Numerics.Distributions.UnivariateDistributionType.Normal, Parameters = [800, 100] } }]
            }));
        }

        /// <summary>REST and MCP preserve zeros, ancient indexes, uncertainty bounds and valid below-threshold events.</summary>
        [TestMethod]
        public async Task Chronology_RestAndMcpAgreeForMixedObservations()
        {
            var service = new InputDataService(Store(), new FakeUsgsTimeSeriesService());
            var input = service.CreateManual(new()
            {
                ExactData = [new() { Index = 2000, Value = 0, IsLowOutlier = true }, new() { Index = 2002, Value = 200 }],
                LowOutlierThreshold = 10,
                IntervalData = [new() { Index = -100, LowerBound = 40, Value = 50, UpperBound = 60 }],
                UncertainData = [new() { Index = 1900, Distribution = new() { Parameters = [100, 10] } }],
                ThresholdData = [new() { StartIndex = -101, EndIndex = -99, Value = 100 }]
            });
            var controller = new InputDataController(NullLogger<InputDataController>.Instance, service);
            var action = await controller.GetChronology(input.Id);
            var body = (InputDataChronologyResponse)((ObjectResult)action.Result!).Value!;
            // REST adds transport timing; compare the shared scientific payload.
            body.Timestamp = null;
            body.ComputationTimeMs = null;
            Assert.AreEqual(TestJson.Serialize(body), new InputDataTools(service).GetInputDataChronology(input.Id));
            Assert.AreEqual(0d, body.ExactData![0].Value);
            Assert.IsTrue(body.ExactData[0].IsLowOutlier);
            Assert.AreEqual(-100, body.IntervalData![0].Index);
            Assert.IsTrue(body.UncertainData![0].LowerBound < body.UncertainData[0].Value);
            Assert.IsTrue(body.UncertainData[0].UpperBound > body.UncertainData[0].Value);
            Assert.AreEqual(2, body.ThresholdData![0].NumberBelow);
            var missing = await controller.GetChronology(Guid.NewGuid());
            Assert.AreEqual(404, ((ObjectResult)missing.Result!).StatusCode);
            var source = (InputDataSourceResponse)((ObjectResult)(await controller.GetSource(input.Id)).Result!).Value!;
            source.Timestamp = null;
            source.ComputationTimeMs = null;
            Assert.AreEqual(TestJson.Serialize(source), new InputDataTools(service).GetInputDataSource(input.Id));
        }

        /// <summary>Enabled B17C penalties keep their original mean/MSE units in detached configuration responses.</summary>
        [TestMethod]
        public void Bulletin17C_ConfigurationPreservesPenaltySpaces()
        {
            var store = Store();
            var input = store.AddInputData(TestAnalyses.CreateInputDataResource());
            var service = new AnalysisService(store, Options.Create(new ApiOptions()));
            var resource = service.CreateBulletin17C(new()
            {
                InputDataId = input.Id,
                ParameterPenalties = [new() { ParameterName = "Skew (of log)", Mean = -0.17, Mse = 0.12 }],
                QuantilePenalties = [new() { Aep = 0.0001, Mean = 5.4689, Mse = 0.016, UseLog10 = true }]
            });
            var config = TestJson.Roundtrip(AnalysisMapper.ToResourceResponse(resource)).Configuration!;
            Assert.AreEqual(-0.17, config.ParameterPenalties![0].Mean);
            Assert.AreEqual(0.12, config.ParameterPenalties[0].Mse);
            Assert.AreEqual(5.4689, config.QuantilePenalties![0].Mean);
            Assert.AreEqual(0.016, config.QuantilePenalties[0].Mse);
            Assert.IsTrue(config.QuantilePenalties[0].UseLog10);
            Assert.IsNull(config.BayesianOptions);
            config.QuantilePenalties[0].Mean = 1;
            Assert.AreEqual(5.4689, FrequencyConfigurationMapper.Map(resource)!.QuantilePenalties![0].Mean);
            Assert.IsNull(FrequencyConfigurationMapper.Map(TestAnalyses.CreateRatingCurveResource()));
        }
    }
}
