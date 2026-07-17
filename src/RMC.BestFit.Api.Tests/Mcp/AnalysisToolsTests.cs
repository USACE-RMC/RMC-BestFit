using System.Text.Json;
using Microsoft.Extensions.Options;
using Numerics.Distributions;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Mcp;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Services.Exceptions;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;
using RMC.BestFit.Models;

namespace RMC.BestFit.Api.Tests.Mcp
{
    /// <summary>
    /// Unit tests for <see cref="AnalysisTools"/>: creation and validation tools with contract
    /// parity. Run tools are not invoked (they execute estimators).
    /// </summary>
    [TestClass]
    public class AnalysisToolsTests
    {
        /// <summary>
        /// The store shared by the tool stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The tools under test.
        /// </summary>
        private AnalysisTools _tools = null!;

        /// <summary>
        /// Creates a fresh tool stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _tools = new AnalysisTools(new AnalysisService(_store, Options.Create(new ApiOptions())));
        }

        /// <summary>
        /// Verifies the univariate creation tool parses flat MCMC parameters into the analysis.
        /// </summary>
        [TestMethod]
        public void CreateUnivariateAnalysis_ParsesFlatOptions()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());

            string json = _tools.CreateUnivariateAnalysis(
                input.Id,
                distribution: "generalizedExtremeValue",
                probabilityOrdinates: new[] { 0.5, 0.01 },
                prngSeed: 42,
                sampler: "demCz",
                pointEstimator: "posteriorMean");

            using var document = JsonDocument.Parse(json);
            var summary = document.RootElement.GetProperty("analysis");
            Assert.AreEqual("univariate", summary.GetProperty("kind").GetString());
            Assert.AreEqual("generalizedExtremeValue", summary.GetProperty("distribution").GetString());
            Assert.IsTrue(summary.GetProperty("isValid").GetBoolean());

            var resource = _store.GetAnalysis(summary.GetProperty("id").GetGuid())!;
            Assert.AreEqual(42, resource.Univariate!.BayesianAnalysis.PRNGSeed);
            Assert.AreEqual(2, resource.Univariate.ProbabilityOrdinates.Count);
        }

        /// <summary>
        /// Verifies the Bulletin 17C creation tool honors the nullable uncertainty method.
        /// </summary>
        [TestMethod]
        public void CreateBulletin17CAnalysis_ParsesMethod()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            string json = _tools.CreateBulletin17CAnalysis(input.Id, uncertaintyMethod: "bootstrap");
            using var document = JsonDocument.Parse(json);
            Assert.AreEqual("bootstrap", document.RootElement.GetProperty("analysis").GetProperty("uncertaintyMethod").GetString());
        }

        /// <summary>
        /// Verifies the rating curve creation tool links both series and validation reports through
        /// the validate tool.
        /// </summary>
        [TestMethod]
        public void CreateRatingCurveAnalysis_ThenValidate()
        {
            var (stage, discharge) = TestAnalyses.CreateStageDischargePair();
            var stageResource = _store.AddTimeSeries(new TimeSeriesResource(stage) { Name = "stage", Source = TimeSeriesSource.Manual });
            var dischargeResource = _store.AddTimeSeries(new TimeSeriesResource(discharge) { Name = "discharge", Source = TimeSeriesSource.Manual });

            string json = _tools.CreateRatingCurveAnalysis(stageResource.Id, dischargeResource.Id, prngSeed: 7);
            using var document = JsonDocument.Parse(json);
            var id = document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid();

            string validationJson = _tools.ValidateAnalysis(id);
            using var validationDocument = JsonDocument.Parse(validationJson);
            Assert.IsTrue(validationDocument.RootElement.GetProperty("isValid").GetBoolean());
        }

        /// <summary>
        /// Verifies the results tool enforces run-first semantics.
        /// </summary>
        [TestMethod]
        public void GetAnalysisResults_BeforeRun_Throws()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            string json = _tools.CreateUnivariateAnalysis(input.Id);
            using var document = JsonDocument.Parse(json);
            var id = document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid();

            var ex = Assert.ThrowsException<ResourceNotFoundException>(() => _tools.GetAnalysisResults(id));
            StringAssert.Contains(ex.Message, "Run it first");
        }

        /// <summary>
        /// Verifies the univariate creation tool applies typed parameter-prior and quantile-prior
        /// arguments onto the model.
        /// </summary>
        [TestMethod]
        public void CreateUnivariateAnalysis_AppliesPriors()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var probe = new UnivariateDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
            string skewName = probe.Parameters[2].DisplayName;

            string json = _tools.CreateUnivariateAnalysis(
                input.Id,
                parameterPriors: new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = skewName,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.Normal,
                            Parameters = new List<double> { -0.2, 0.3 }
                        }
                    }
                },
                quantilePriors: new List<QuantilePriorDto>
                {
                    new()
                    {
                        Alpha = 0.01,
                        Distribution = new DistributionSpecDto
                        {
                            Type = UnivariateDistributionType.LogNormal,
                            Parameters = new List<double> { 4.7, 0.1 }
                        }
                    }
                },
                useSingleQuantile: true);

            using var document = JsonDocument.Parse(json);
            var resource = _store.GetAnalysis(document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid())!;
            var model = resource.Univariate!.UnivariateDistribution;
            Assert.IsFalse(model.UseDefaultFlatPriors);
            Assert.AreEqual(UnivariateDistributionType.Normal, model.Parameters[2].PriorDistribution.Type);
            Assert.IsTrue(model.EnableQuantilePriors);
            Assert.IsTrue(model.UseSingleQuantile);
        }

        /// <summary>
        /// Verifies the Bulletin 17C creation tool applies typed penalty arguments onto the
        /// distribution (the regional-skew workflow).
        /// </summary>
        [TestMethod]
        public void CreateBulletin17CAnalysis_AppliesPenalties()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var probe = new Bulletin17CDistribution(TestAnalyses.CreateDataFrame(), UnivariateDistributionType.LogPearsonTypeIII);
            string skewName = probe.ParameterPenalties[2].Name;

            string json = _tools.CreateBulletin17CAnalysis(
                input.Id,
                parameterPenalties: new List<ParameterPenaltyDto>
                {
                    new() { ParameterName = skewName, Mean = -0.05, Mse = 0.12 }
                },
                quantilePenalties: new List<QuantilePenaltyDto>
                {
                    new() { Aep = 0.002, Mean = 4.85, Mse = 0.02 }
                });

            using var document = JsonDocument.Parse(json);
            var resource = _store.GetAnalysis(document.RootElement.GetProperty("analysis").GetProperty("id").GetGuid())!;
            var distribution = resource.Bulletin17C!.Bulletin17CDistribution;
            Assert.IsTrue(distribution.ParameterPenalties[2].Enabled);
            Assert.AreEqual(1, distribution.QuantilePenalties.Count);
            Assert.IsTrue(distribution.QuantilePenalties[0].Enabled);
        }
    }
}
