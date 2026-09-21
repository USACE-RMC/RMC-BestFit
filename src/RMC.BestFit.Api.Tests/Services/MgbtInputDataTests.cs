using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Numerics.Data;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mappers;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Services
{
    /// <summary>
    /// Verifies opt-in screening and transport forwarding without running an estimator.
    /// The existing data-frame method is the delegation contract, not a new numerical oracle.
    /// </summary>
    [TestClass]
    public class MgbtInputDataTests
    {
        /// <summary>The isolated resource store.</summary>
        private InMemoryResourceStore _store = null!;
        /// <summary>The fake annual-peak download source.</summary>
        private FakeUsgsTimeSeriesService _usgs = null!;
        /// <summary>The input service under test.</summary>
        private InputDataService _inputs = null!;

        /// <summary>Initializes an independent store and a zero/tied-flow fixture for each test.</summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _usgs = new FakeUsgsTimeSeriesService { Result = Peaks() };
            _inputs = new InputDataService(_store, _usgs);
        }

        /// <summary>Checks that old JSON requests retain false and true survives round-trip.</summary>
        [TestMethod]
        public void RequestDtos_DefaultOff_AndRoundtripOptIn()
        {
            foreach (var type in new[] { typeof(CreateManualInputDataRequest), typeof(CreateUsgsPeaksInputDataRequest), typeof(UsgsBulletin17CWorkflowRequest) })
            {
                foreach (bool enabled in new[] { false, true })
                {
                    string json = enabled ? "{\"useMultipleGrubbsBeckTest\":true}" : "{}";
                    object value = JsonSerializer.Deserialize(json, type, TestJson.Options)!;
                    using var result = JsonDocument.Parse(JsonSerializer.Serialize(value, type, TestJson.Options));
                    Assert.IsTrue(result.RootElement.TryGetProperty("useMultipleGrubbsBeckTest", out var option), type.Name);
                    Assert.AreEqual(enabled, option.GetBoolean(), type.Name);
                }
            }
        }

        /// <summary>Checks screening, refreshed display positions, retained history, and analysis cloning.</summary>
        [TestMethod]
        public void Manual_OptIn_ScreensBeforeClone_AndPreservesHistory()
        {
            var request = ManualRequest(true);
            request.IntervalData = new() { new() { Index = 1950, LowerBound = 450, UpperBound = 650 } };
            request.ThresholdData = new() { new() { StartIndex = 1900, EndIndex = 1949, Value = 400, NumberAbove = 1 } };
            request.Lambda = 1.25;
            var expected = _inputs.CreateManual(ManualRequest(false)).DataFrame;
            expected.SetLowOutliersFromMGBT();
            Assert.IsTrue(expected.NumberOfLowOutliers > 0, "The fixture must exercise actual screening.");

            var resource = _inputs.CreateManual(request);
            var frame = resource.DataFrame;
            Assert.AreEqual(expected.LowOutlierThreshold, frame.LowOutlierThreshold);
            CollectionAssert.AreEqual(expected.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray(),
                frame.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray());
            Assert.AreEqual(1.25, frame.Lambda);
            Assert.AreEqual(450d, ((IntervalData)frame.IntervalSeries[0]).LowerValue);
            Assert.AreEqual(650d, ((IntervalData)frame.IntervalSeries[0]).UpperValue);
            Assert.AreEqual(1900, ((ThresholdData)frame.ThresholdSeries[0]).StartIndex);
            Assert.AreEqual(1, ((ThresholdData)frame.ThresholdSeries[0]).NumberAbove);

            var refreshed = frame.Clone();
            refreshed.SetLowOutliersFromMGBT();
            CollectionAssert.AreEqual(refreshed.ExactSeries.Select(x => x.PlottingPosition).ToArray(),
                frame.ExactSeries.Select(x => x.PlottingPosition).ToArray());
            var summary = InputDataMapper.ToResourceResponse(resource, true);
            Assert.AreEqual(frame.NumberOfLowOutliers, summary.InputData!.LowOutlierCount);
            Assert.AreEqual(frame.LowOutlierThreshold, summary.InputData.LowOutlierThreshold);

            var analyses = new AnalysisService(_store, Options.Create(new ApiOptions()));
            var analysis = analyses.CreateBulletin17C(new CreateBulletin17CAnalysisRequest { InputDataId = resource.Id });
            Assert.IsFalse(analysis.Bulletin17C!.IsEstimated);
            CollectionAssert.AreEqual(frame.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray(),
                analysis.Bulletin17C.Bulletin17CDistribution.DataFrame.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray());
        }

        /// <summary>Checks that omitted/false screening preserves existing manual flags and thresholds.</summary>
        /// <param name="omit">Whether the new field is omitted entirely.</param>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Manual_NotEnabled_PreservesManualScreening(bool omit)
        {
            var request = ManualRequest(omit ? null : false);
            request.LowOutlierThreshold = 50;
            request.ExactData[0].IsLowOutlier = true;
            var frame = _inputs.CreateManual(request).DataFrame;
            Assert.AreEqual(50d, frame.LowOutlierThreshold);
            Assert.IsTrue(((ExactData)frame.ExactSeries[0]).IsLowOutlier);
            Assert.IsFalse(((ExactData)frame.ExactSeries[1]).IsLowOutlier);
        }

        /// <summary>Rejects competing screening policies before adding any resource.</summary>
        /// <param name="threshold">True to conflict with a threshold; false to conflict with a flag.</param>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void Manual_ConflictingScreening_FailsWithoutResource(bool threshold)
        {
            var request = ManualRequest(true);
            if (threshold) request.LowOutlierThreshold = 0;
            else request.ExactData[0].IsLowOutlier = true;
            Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(request));
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>Uses the existing insufficient-data error and leaves no partial resource.</summary>
        [TestMethod]
        public async Task InsufficientData_FailsWithoutResource_InBothInputPaths()
        {
            Assert.ThrowsException<ArgumentException>(() => _inputs.CreateManual(ManualRequest(true, 9)));
            _usgs.Result = Peaks(9);
            await Assert.ThrowsExceptionAsync<ArgumentException>(() => _inputs.CreateFromUsgsPeaksAsync(
                JsonSerializer.Deserialize<CreateUsgsPeaksInputDataRequest>("{\"siteNumber\":\"01646500\",\"useMultipleGrubbsBeckTest\":true}", TestJson.Options)!));
            Assert.AreEqual(0, _store.TotalCount);
        }

        /// <summary>Verifies the USGS path forwards screening, including zeros and repeated values.</summary>
        /// <param name="enabled">Whether screening is requested.</param>
        [DataTestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public async Task Usgs_RespectsOptIn(bool enabled)
        {
            var request = JsonSerializer.Deserialize<CreateUsgsPeaksInputDataRequest>(
                JsonSerializer.Serialize(new { siteNumber = "01646500", useMultipleGrubbsBeckTest = enabled }), TestJson.Options)!;
            var frame = (await _inputs.CreateFromUsgsPeaksAsync(request)).DataFrame;
            var expected = _inputs.CreateManual(ManualRequest(false)).DataFrame;
            if (enabled) expected.SetLowOutliersFromMGBT();
            CollectionAssert.AreEqual(expected.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray(),
                frame.ExactSeries.Cast<ExactData>().Select(x => x.IsLowOutlier).ToArray());
            CollectionAssert.AreEqual(expected.ExactSeries.Select(x => x.PlottingPosition).ToArray(),
                frame.ExactSeries.Select(x => x.PlottingPosition).ToArray());
        }

        /// <summary>Proves workflow forwarding while deliberately stopping before estimation.</summary>
        [TestMethod]
        public async Task Workflow_ScreeningPrecedesAnalysisCreation()
        {
            var request = JsonSerializer.Deserialize<UsgsBulletin17CWorkflowRequest>(
                "{\"siteNumber\":\"01646500\",\"useMultipleGrubbsBeckTest\":true,\"distribution\":\"generalizedExtremeValue\"}", TestJson.Options)!;
            var response = await Workflow().RunUsgsBulletin17CAsync(request);
            Assert.AreEqual("createAnalysis", response.FailedStep);
            Assert.IsNotNull(response.InputDataId);
            Assert.IsTrue(_inputs.Get(response.InputDataId.Value).DataFrame.NumberOfLowOutliers > 0);
            Assert.IsNull(response.AnalysisId);

            _usgs.Result = Peaks(9);
            int before = _store.TotalCount;
            response = await Workflow().RunUsgsBulletin17CAsync(request);
            Assert.AreEqual("createInputData", response.FailedStep);
            Assert.IsNull(response.InputDataId);
            Assert.AreEqual(before, _store.TotalCount);
        }

        /// <summary>Checks MCP discovery defaults and actual manual/USGS forwarding.</summary>
        [TestMethod]
        public async Task McpTools_ExposeFalseDefault_AndForwardOptIn()
        {
            var tools = new InputDataTools(_inputs);
            var manual = InvokeWithMgbt(tools, nameof(InputDataTools.CreateInputDataManual), new() { ["exactData"] = ManualRequest(false).ExactData });
            var usgs = await (Task<string>)InvokeWithMgbt(tools, nameof(InputDataTools.CreateInputDataUsgsPeaks), new() { ["siteNumber"] = "01646500" });
            foreach (string json in new[] { (string)manual, usgs })
            {
                using var document = JsonDocument.Parse(json);
                Assert.IsTrue(document.RootElement.GetProperty("inputData").GetProperty("lowOutlierCount").GetInt32() > 0);
            }
            _usgs.Result = Peaks(9);
            string workflow = await (Task<string>)InvokeWithMgbt(new WorkflowTools(Workflow()), nameof(WorkflowTools.RunUsgsBulletin17CWorkflow),
                new() { ["siteNumber"] = "01646500", ["probabilityOrdinates"] = new[] { 0d } });
            using var result = JsonDocument.Parse(workflow);
            Assert.AreEqual("createInputData", result.RootElement.GetProperty("failedStep").GetString());
        }

        /// <summary>Builds the real workflow around the fake download seam.</summary>
        /// <returns>The composed workflow.</returns>
        private WorkflowService Workflow() => new(new TimeSeriesService(_store, _usgs), _inputs,
            new AnalysisService(_store, Options.Create(new ApiOptions())));

        /// <summary>Invokes a discovered MCP method and verifies its new optional parameter.</summary>
        /// <param name="target">The MCP tool instance.</param>
        /// <param name="name">The CLR method name.</param>
        /// <param name="arguments">Required and overridden argument values.</param>
        /// <returns>The method result, possibly a task.</returns>
        private static object InvokeWithMgbt(object target, string name, Dictionary<string, object> arguments)
        {
            MethodInfo method = target.GetType().GetMethod(name)!;
            var parameters = method.GetParameters();
            var option = parameters.SingleOrDefault(x => x.Name == "useMultipleGrubbsBeckTest");
            Assert.IsNotNull(option, name);
            Assert.AreEqual(false, option.DefaultValue);
            arguments[option.Name!] = true;
            return method.Invoke(target, parameters.Select(x => arguments.TryGetValue(x.Name!, out var value) ? value : x.DefaultValue).ToArray())!;
        }

        /// <summary>Builds a request through the wire format, allowing a pre-feature behavioral baseline.</summary>
        /// <param name="enabled">The option, or null to omit it.</param>
        /// <param name="count">The number of observations.</param>
        /// <returns>The deserialized request.</returns>
        private static CreateManualInputDataRequest ManualRequest(bool? enabled, int count = 15)
        {
            var values = new[] { 0d, 0d, 100d, 100d, 120d, 140d, 160d, 180d, 200d, 220d, 240d, 260d, 280d, 300d, 320d };
            var payload = new Dictionary<string, object>
            {
                ["exactData"] = values.Take(count).Select((value, i) => new { index = 2000 + i, value }).ToArray()
            };
            if (enabled.HasValue) payload["useMultipleGrubbsBeckTest"] = enabled.Value;
            return JsonSerializer.Deserialize<CreateManualInputDataRequest>(JsonSerializer.Serialize(payload), TestJson.Options)!;
        }

        /// <summary>Builds a fake USGS series from the same small inline fixture.</summary>
        /// <param name="count">The requested record length.</param>
        /// <returns>The peak series.</returns>
        private static TimeSeries Peaks(int count = 15)
        {
            var series = new TimeSeries(TimeInterval.Irregular);
            foreach (var point in ManualRequest(false, count).ExactData)
                series.Add(new SeriesOrdinate<DateTime, double>(new DateTime(point.Index!.Value, 5, 1), point.Value));
            return series;
        }
    }
}
