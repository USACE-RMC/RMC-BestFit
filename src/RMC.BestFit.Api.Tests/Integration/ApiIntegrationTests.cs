using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Integration
{
    /// <summary>
    /// In-process integration tests exercising the full HTTP pipeline (routing, model binding,
    /// JSON options, status-code mapping) with the USGS seam faked at the DI level. No network
    /// access and no estimator runs — the suite completes in well under a second of compute.
    /// </summary>
    [TestClass]
    public class ApiIntegrationTests
    {
        /// <summary>
        /// The shared application factory with the faked USGS service.
        /// </summary>
        private static WebApplicationFactory<Program> _factory = null!;

        /// <summary>
        /// The faked USGS seam injected into the host.
        /// </summary>
        private static FakeUsgsTimeSeriesService _usgs = null!;

        /// <summary>
        /// JSON options matching the API contract for response deserialization.
        /// </summary>
        private static readonly JsonSerializerOptions JsonOptions = TestJson.Options;

        /// <summary>
        /// Boots the in-process host once for the test class.
        /// </summary>
        /// <param name="context">The MSTest context (unused).</param>
        [ClassInitialize]
        public static void ClassInitialize(TestContext context)
        {
            _usgs = new FakeUsgsTimeSeriesService { Result = TestSeries.DailyThreeWaterYears() };
            _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.ConfigureServices(services =>
                {
                    services.AddSingleton<IUsgsTimeSeriesService>(_usgs);
                });
            });
        }

        /// <summary>
        /// Disposes the in-process host.
        /// </summary>
        [ClassCleanup]
        public static void ClassCleanup()
        {
            _factory.Dispose();
        }

        /// <summary>
        /// Verifies the health and info endpoints respond.
        /// </summary>
        [TestMethod]
        public async Task Health_And_Info_Respond()
        {
            using var client = _factory.CreateClient();

            var health = await client.GetAsync("/health");
            Assert.AreEqual(HttpStatusCode.OK, health.StatusCode);

            var info = await client.GetFromJsonAsync<ApiInfoDto>("/api/info", JsonOptions);
            Assert.IsNotNull(info);
            Assert.AreEqual("RMC-BestFit API", info.Name);
        }

        /// <summary>
        /// Verifies the metadata endpoints respond with populated listings.
        /// </summary>
        [TestMethod]
        public async Task Metadata_Endpoints_Respond()
        {
            using var client = _factory.CreateClient();

            var enums = await client.GetFromJsonAsync<EnumOptionsResponse>("/api/metadata/enums", JsonOptions);
            Assert.IsNotNull(enums);
            Assert.IsTrue(enums.Samplers.Count > 0);

            var distributions = await client.GetFromJsonAsync<DistributionsResponse>("/api/metadata/distributions", JsonOptions);
            Assert.IsNotNull(distributions);
            Assert.IsTrue(distributions.Distributions.Any(d => d.Name == "logPearsonTypeIII"));

            var defaults = await client.GetFromJsonAsync<DefaultsResponse>("/api/metadata/defaults", JsonOptions);
            Assert.IsNotNull(defaults);
            Assert.IsTrue(defaults.ProbabilityOrdinates.Count > 0);
        }

        /// <summary>
        /// Verifies the multi-step resource workflow over HTTP: USGS time series → block-max
        /// input data → detail with observations.
        /// </summary>
        [TestMethod]
        public async Task Workflow_UsgsSeries_To_BlockMaxInputData()
        {
            using var client = _factory.CreateClient();

            // Step 1: create a time series from the (faked) USGS download.
            var seriesResponse = await client.PostAsJsonAsync("/api/timeseries/usgs",
                new CreateUsgsTimeSeriesRequest { SiteNumber = "01646500" }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, seriesResponse.StatusCode);
            var series = await seriesResponse.Content.ReadFromJsonAsync<TimeSeriesResourceResponse>(JsonOptions);
            Assert.IsNotNull(series?.TimeSeries);

            // Step 2: extract water-year block maxima from the stored series.
            var inputDataResponse = await client.PostAsJsonAsync("/api/inputdata/block-max",
                new CreateBlockMaxInputDataRequest { TimeSeriesId = series.TimeSeries.Id }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, inputDataResponse.StatusCode);
            var inputData = await inputDataResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Assert.IsNotNull(inputData?.InputData);
            Assert.AreEqual(3, inputData.InputData.ExactCount);
            Assert.AreEqual(series.TimeSeries.Id, inputData.InputData.SourceTimeSeriesId);

            // Step 3: fetch the detail with observations and verify the planted maxima survived
            // the HTTP round-trip.
            var detail = await client.GetFromJsonAsync<InputDataResourceResponse>(
                $"/api/inputdata/{inputData.InputData.Id}?includeData=true", JsonOptions);
            Assert.IsNotNull(detail?.ExactData);
            var values = detail.ExactData.Select(o => o.Value).OrderBy(v => v).ToList();
            CollectionAssert.AreEqual(new List<double> { 500d, 650d, 800d }, values);
        }

        /// <summary>
        /// Verifies the error contract over HTTP: unknown ids return 404 bodies with
        /// success=false, and invalid requests return 400 with validation errors.
        /// </summary>
        [TestMethod]
        public async Task ErrorContract_404_And_400()
        {
            using var client = _factory.CreateClient();

            var notFound = await client.GetAsync($"/api/timeseries/{Guid.NewGuid()}");
            Assert.AreEqual(HttpStatusCode.NotFound, notFound.StatusCode);
            var notFoundBody = await notFound.Content.ReadFromJsonAsync<TimeSeriesResourceResponse>(JsonOptions);
            Assert.IsNotNull(notFoundBody);
            Assert.IsFalse(notFoundBody.Success);
            Assert.IsNotNull(notFoundBody.ErrorMessage);

            var badRequest = await client.PostAsJsonAsync("/api/inputdata/manual", new CreateManualInputDataRequest
            {
                ExactData = new List<ExactObservationDto> { new() { Index = 2000, Value = 100d } },
                ThresholdData = new List<ThresholdObservationDto> { new() { StartIndex = 1950, EndIndex = 1900, Value = 1d } }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.BadRequest, badRequest.StatusCode);
            var badRequestBody = await badRequest.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Assert.IsNotNull(badRequestBody);
            Assert.IsFalse(badRequestBody.Success);
            Assert.IsNotNull(badRequestBody.ValidationErrors);
        }

        /// <summary>
        /// Verifies the analysis workflow over HTTP without running an estimator: create manual
        /// input data → create a univariate analysis → validate → results are 404 before any run
        /// → the Bulletin 17C route does not see the univariate id (kind guard).
        /// </summary>
        [TestMethod]
        public async Task Workflow_CreateAnalysis_Validate_ResultsBeforeRun()
        {
            using var client = _factory.CreateClient();

            var inputDataResponse = await client.PostAsJsonAsync("/api/inputdata/manual", new CreateManualInputDataRequest
            {
                ExactData = Enumerable.Range(0, 20)
                    .Select(i => new ExactObservationDto { Index = 2000 + i, Value = 100d + 20d * i })
                    .ToList()
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, inputDataResponse.StatusCode);
            var inputData = await inputDataResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);

            var createResponse = await client.PostAsJsonAsync("/api/analyses/univariate", new CreateUnivariateAnalysisRequest
            {
                InputDataId = inputData!.InputData!.Id,
                ProbabilityOrdinates = new List<double> { 0.5, 0.1, 0.01 }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode);
            var created = await createResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.IsNotNull(created?.Analysis);
            Assert.IsTrue(created.Analysis.IsValid);
            var analysisId = created.Analysis.Id;

            var validation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/univariate/{analysisId}/validate", JsonOptions);
            Assert.IsNotNull(validation);
            Assert.IsTrue(validation.IsValid);

            var resultsBeforeRun = await client.GetAsync($"/api/analyses/univariate/{analysisId}/results");
            Assert.AreEqual(HttpStatusCode.NotFound, resultsBeforeRun.StatusCode);

            var wrongKind = await client.GetAsync($"/api/analyses/bulletin17c/{analysisId}");
            Assert.AreEqual(HttpStatusCode.NotFound, wrongKind.StatusCode);
        }

        /// <summary>
        /// Smoke-tests the MCP endpoint: in stateless mode a bare JSON-RPC tools/list request must
        /// return the registered tool set without a prior initialize handshake.
        /// </summary>
        [TestMethod]
        public async Task Mcp_ToolsList_ReturnsRegisteredTools()
        {
            using var client = _factory.CreateClient();
            using var request = new HttpRequestMessage(HttpMethod.Post, "/mcp")
            {
                Content = new StringContent(
                    "{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"tools/list\"}",
                    System.Text.Encoding.UTF8, "application/json")
            };
            request.Headers.Accept.ParseAdd("application/json");
            request.Headers.Accept.ParseAdd("text/event-stream");

            var response = await client.SendAsync(request);
            string body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual(HttpStatusCode.OK, response.StatusCode, $"MCP endpoint failed: {body}");
            StringAssert.Contains(body, "usgs_download_timeseries");
            StringAssert.Contains(body, "run_usgs_bulletin17c_workflow");
            StringAssert.Contains(body, "run_analysis");
        }

        /// <summary>
        /// Verifies enum strings bind case-insensitively in camelCase over HTTP and the resources
        /// overview reflects created resources.
        /// </summary>
        [TestMethod]
        public async Task EnumBinding_And_ResourcesOverview()
        {
            using var client = _factory.CreateClient();

            var response = await client.PostAsync("/api/timeseries/usgs",
                new StringContent("{\"siteNumber\":\"01646500\",\"seriesType\":\"peakDischarge\"}",
                    System.Text.Encoding.UTF8, "application/json"));
            Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
            var created = await response.Content.ReadFromJsonAsync<TimeSeriesResourceResponse>(JsonOptions);
            Assert.AreEqual("peakDischarge", created!.TimeSeries!.SeriesType);

            var overview = await client.GetFromJsonAsync<ResourcesOverviewResponse>("/api/resources", JsonOptions);
            Assert.IsNotNull(overview);
            Assert.IsTrue(overview.TimeSeriesCount >= 1);
        }

        /// <summary>
        /// End-to-end Bayesian-inputs flow over HTTP: manual input data with an uncertain
        /// observation, a prior-informed univariate analysis, and a Bulletin 17C analysis that
        /// warns about the ignored uncertain data. No estimator runs.
        /// </summary>
        [TestMethod]
        public async Task BayesianInputs_UncertainData_Priors_Penalties_Flow()
        {
            using var client = _factory.CreateClient();

            // Manual input data with an uncertain (paleoflood-style) observation.
            var inputRequest = new CreateManualInputDataRequest
            {
                ExactData = Enumerable.Range(0, 20)
                    .Select(i => new ExactObservationDto { Index = 2000 + i, Value = 100d + 20d * i })
                    .ToList(),
                UncertainData = new List<UncertainObservationDto>
                {
                    new()
                    {
                        Index = 1875,
                        Distribution = new DistributionSpecDto
                        {
                            Type = Numerics.Distributions.UnivariateDistributionType.Triangular,
                            Parameters = new List<double> { 400d, 550d, 900d }
                        }
                    }
                }
            };
            var inputResponse = await client.PostAsJsonAsync("/api/inputdata/manual", inputRequest, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, inputResponse.StatusCode);
            var input = await inputResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Assert.AreEqual(1, input!.InputData!.UncertainCount);

            // The detail response echoes the uncertain observation with its distribution spec.
            var detail = await client.GetFromJsonAsync<InputDataResourceResponse>(
                $"/api/inputdata/{input.InputData.Id}?includeData=true", JsonOptions);
            Assert.IsNotNull(detail!.UncertainData);
            Assert.AreEqual(1875, detail.UncertainData![0].Index);
            Assert.IsNotNull(detail.UncertainData[0].Value);

            // A univariate analysis with an informative skew prior and a quantile prior.
            var univariateRequest = new CreateUnivariateAnalysisRequest
            {
                InputDataId = input.InputData.Id,
                ParameterPriors = new List<ParameterPriorDto>
                {
                    new()
                    {
                        ParameterName = "Skew (of log)",
                        Distribution = new DistributionSpecDto
                        {
                            Type = Numerics.Distributions.UnivariateDistributionType.Normal,
                            Parameters = new List<double> { -0.2, 0.3 }
                        }
                    }
                },
                QuantilePriors = new List<QuantilePriorDto>
                {
                    new()
                    {
                        Alpha = 0.01,
                        Distribution = new DistributionSpecDto
                        {
                            Type = Numerics.Distributions.UnivariateDistributionType.LogNormal,
                            Parameters = new List<double> { 6.5, 0.2 }
                        }
                    }
                },
                UseSingleQuantile = true
            };
            var univariateResponse = await client.PostAsJsonAsync("/api/analyses/univariate", univariateRequest, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, univariateResponse.StatusCode);
            var univariate = await univariateResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.IsTrue(univariate!.Analysis!.IsValid);

            // An unknown prior parameter name is a 400 with the valid names listed.
            univariateRequest.ParameterPriors![0].ParameterName = "No Such Parameter";
            var badPrior = await client.PostAsJsonAsync("/api/analyses/univariate", univariateRequest, JsonOptions);
            Assert.AreEqual(HttpStatusCode.BadRequest, badPrior.StatusCode);

            // A Bulletin 17C analysis with a regional-skew penalty warns about the ignored
            // uncertain observation on both the create and validate responses.
            var b17cRequest = new CreateBulletin17CAnalysisRequest
            {
                InputDataId = input.InputData.Id,
                ParameterPenalties = new List<ParameterPenaltyDto>
                {
                    new() { ParameterName = "Skew (of log)", Mean = -0.05, Mse = 0.12 }
                }
            };
            var b17cResponse = await client.PostAsJsonAsync("/api/analyses/bulletin17c", b17cRequest, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, b17cResponse.StatusCode);
            var b17c = await b17cResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.IsNotNull(b17c!.Analysis!.Warnings);
            StringAssert.Contains(b17c.Analysis.Warnings![0], "uncertain observation");

            var validation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/bulletin17c/{b17c.Analysis.Id}/validate", JsonOptions);
            Assert.AreEqual(1, validation!.Warnings.Count);
        }

        /// <summary>
        /// End-to-end create→validate→kind-guard flow over HTTP for the mixture, point process,
        /// and competing risks endpoints. No estimator runs.
        /// </summary>
        [TestMethod]
        public async Task Phase4FrequencyKinds_Create_Validate_KindGuard()
        {
            using var client = _factory.CreateClient();

            var inputRequest = new CreateManualInputDataRequest
            {
                ExactData = Enumerable.Range(0, 20)
                    .Select(i => new ExactObservationDto { Index = 2000 + i, Value = 100d + 20d * i })
                    .ToList()
            };
            var inputResponse = await client.PostAsJsonAsync("/api/inputdata/manual", inputRequest, JsonOptions);
            var input = await inputResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Guid inputId = input!.InputData!.Id;

            // Mixture.
            var mixtureResponse = await client.PostAsJsonAsync("/api/analyses/mixture", new CreateMixtureAnalysisRequest
            {
                InputDataId = inputId,
                Distributions = new List<Numerics.Distributions.UnivariateDistributionType>
                {
                    Numerics.Distributions.UnivariateDistributionType.Gumbel,
                    Numerics.Distributions.UnivariateDistributionType.LogNormal
                }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, mixtureResponse.StatusCode);
            var mixture = await mixtureResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("mixture", mixture!.Analysis!.Kind);

            var mixtureValidation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/mixture/{mixture.Analysis.Id}/validate", JsonOptions);
            Assert.IsTrue(mixtureValidation!.IsValid);

            // Competing risks.
            var competingResponse = await client.PostAsJsonAsync("/api/analyses/competingrisks", new CreateCompetingRisksAnalysisRequest
            {
                InputDataId = inputId,
                Distributions = new List<Numerics.Distributions.UnivariateDistributionType>
                {
                    Numerics.Distributions.UnivariateDistributionType.Gumbel,
                    Numerics.Distributions.UnivariateDistributionType.GeneralizedExtremeValue
                }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, competingResponse.StatusCode);
            var competing = await competingResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("competingRisks", competing!.Analysis!.Kind);

            // Point process over a POT extraction from a stored time series.
            var seriesResponse = await client.PostAsync("/api/timeseries/usgs",
                new StringContent("{\"siteNumber\":\"01646500\",\"seriesType\":\"dailyDischarge\"}",
                    System.Text.Encoding.UTF8, "application/json"));
            var series = await seriesResponse.Content.ReadFromJsonAsync<TimeSeriesResourceResponse>(JsonOptions);
            var potResponse = await client.PostAsJsonAsync("/api/inputdata/peaks-over-threshold", new CreatePotInputDataRequest
            {
                TimeSeriesId = series!.TimeSeries!.Id,
                Threshold = 400d,
                MinStepsBetweenPeaks = 7
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, potResponse.StatusCode);
            var pot = await potResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);

            var pointProcessResponse = await client.PostAsJsonAsync("/api/analyses/pointprocess", new CreatePointProcessAnalysisRequest
            {
                InputDataId = pot!.InputData!.Id
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, pointProcessResponse.StatusCode);
            var pointProcess = await pointProcessResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("pointProcess", pointProcess!.Analysis!.Kind);
            Assert.IsNotNull(pointProcess.Analysis.Threshold, "The POT threshold must seed the analysis.");

            // Kind guards: each new id 404s on a different kind's route.
            var guard1 = await client.GetAsync($"/api/analyses/univariate/{mixture.Analysis.Id}");
            Assert.AreEqual(HttpStatusCode.NotFound, guard1.StatusCode);
            var guard2 = await client.GetAsync($"/api/analyses/mixture/{competing.Analysis.Id}");
            Assert.AreEqual(HttpStatusCode.NotFound, guard2.StatusCode);
            var guard3 = await client.GetAsync($"/api/analyses/pointprocess/{mixture.Analysis.Id}");
            Assert.AreEqual(HttpStatusCode.NotFound, guard3.StatusCode);

            // Results before any run are 404s.
            var results = await client.GetAsync($"/api/analyses/mixture/{mixture.Analysis.Id}/results");
            Assert.AreEqual(HttpStatusCode.NotFound, results.StatusCode);
        }

        /// <summary>
        /// End-to-end composite and distribution-fitting flow over HTTP: create a composite over
        /// two unrun univariate components (validate reports they require estimation), and create
        /// a fitting screen with a candidate subset. No estimator runs.
        /// </summary>
        [TestMethod]
        public async Task CompositeAndFitting_Create_Validate_Flow()
        {
            using var client = _factory.CreateClient();

            var inputRequest = new CreateManualInputDataRequest
            {
                ExactData = Enumerable.Range(0, 20)
                    .Select(i => new ExactObservationDto { Index = 2000 + i, Value = 100d + 20d * i })
                    .ToList()
            };
            var inputResponse = await client.PostAsJsonAsync("/api/inputdata/manual", inputRequest, JsonOptions);
            var input = await inputResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Guid inputId = input!.InputData!.Id;

            var componentA = await (await client.PostAsJsonAsync("/api/analyses/univariate",
                new CreateUnivariateAnalysisRequest { InputDataId = inputId }, JsonOptions))
                .Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            var componentB = await (await client.PostAsJsonAsync("/api/analyses/univariate",
                new CreateUnivariateAnalysisRequest { InputDataId = inputId }, JsonOptions))
                .Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);

            var compositeResponse = await client.PostAsJsonAsync("/api/analyses/composite", new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto>
                {
                    new() { AnalysisId = componentA!.Analysis!.Id },
                    new() { AnalysisId = componentB!.Analysis!.Id }
                }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, compositeResponse.StatusCode);
            var composite = await compositeResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("composite", composite!.Analysis!.Kind);

            var compositeValidation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/composite/{composite.Analysis.Id}/validate", JsonOptions);
            Assert.IsFalse(compositeValidation!.IsValid, "Unrun components must fail composite validation.");

            // A rating curve id is rejected as a composite component with a 400.
            var badComponent = await client.PostAsJsonAsync("/api/analyses/composite", new CreateCompositeAnalysisRequest
            {
                Components = new List<CompositeComponentDto> { new() { AnalysisId = composite.Analysis.Id } }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.BadRequest, badComponent.StatusCode);

            var fittingResponse = await client.PostAsJsonAsync("/api/analyses/distributionfitting",
                new CreateDistributionFittingAnalysisRequest
                {
                    InputDataId = inputId,
                    Distributions = new List<Numerics.Distributions.UnivariateDistributionType>
                    {
                        Numerics.Distributions.UnivariateDistributionType.LogPearsonTypeIII,
                        Numerics.Distributions.UnivariateDistributionType.Gumbel
                    }
                }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, fittingResponse.StatusCode);
            var fitting = await fittingResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("distributionFitting", fitting!.Analysis!.Kind);
            Assert.AreEqual(2, fitting.Analysis.ComponentDistributions!.Count);

            var fittingResults = await client.GetAsync($"/api/analyses/distributionfitting/{fitting.Analysis.Id}/results");
            Assert.AreEqual(HttpStatusCode.NotFound, fittingResults.StatusCode);
        }

        /// <summary>
        /// End-to-end bivariate and coincident frequency flow over HTTP: create a bivariate over
        /// two unrun marginals (validate names them), then a coincident frequency analysis over
        /// it (validate reports the unestimated bivariate). No estimator runs.
        /// </summary>
        [TestMethod]
        public async Task BivariateAndCoincidentFrequency_Create_Validate_Flow()
        {
            using var client = _factory.CreateClient();

            var inputRequest = new CreateManualInputDataRequest
            {
                ExactData = Enumerable.Range(0, 20)
                    .Select(i => new ExactObservationDto { Index = 2000 + i, Value = 100d + 20d * i })
                    .ToList()
            };
            var inputResponse = await client.PostAsJsonAsync("/api/inputdata/manual", inputRequest, JsonOptions);
            var input = await inputResponse.Content.ReadFromJsonAsync<InputDataResourceResponse>(JsonOptions);
            Guid inputId = input!.InputData!.Id;

            var marginalX = await (await client.PostAsJsonAsync("/api/analyses/univariate",
                new CreateUnivariateAnalysisRequest { InputDataId = inputId }, JsonOptions))
                .Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            var marginalY = await (await client.PostAsJsonAsync("/api/analyses/univariate",
                new CreateUnivariateAnalysisRequest { InputDataId = inputId }, JsonOptions))
                .Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);

            var bivariateResponse = await client.PostAsJsonAsync("/api/analyses/bivariate", new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalX!.Analysis!.Id,
                MarginalYAnalysisId = marginalY!.Analysis!.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 300d, Y = 320d } }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, bivariateResponse.StatusCode);
            var bivariate = await bivariateResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("bivariate", bivariate!.Analysis!.Kind);

            var bivariateValidation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/bivariate/{bivariate.Analysis.Id}/validate", JsonOptions);
            Assert.IsFalse(bivariateValidation!.IsValid);
            Assert.IsTrue(bivariateValidation.Errors.Any(e => e.Contains("marginal")));

            var cfaResponse = await client.PostAsJsonAsync("/api/analyses/coincidentfrequency", new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariate.Analysis.Id,
                XValues = new List<double> { 100d, 200d },
                YValues = new List<double> { 50d, 100d },
                BivariateResponse = new List<List<double>>
                {
                    new() { 10d, 11d },
                    new() { 12d, 13d }
                }
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.Created, cfaResponse.StatusCode);
            var cfa = await cfaResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
            Assert.AreEqual("coincidentFrequency", cfa!.Analysis!.Kind);
            Assert.AreEqual(bivariate.Analysis.Id, cfa.Analysis.BivariateAnalysisId);

            var cfaValidation = await client.GetFromJsonAsync<ValidationResponse>(
                $"/api/analyses/coincidentfrequency/{cfa.Analysis.Id}/validate", JsonOptions);
            Assert.IsFalse(cfaValidation!.IsValid);

            // Kind guards across the new routes.
            var guard = await client.GetAsync($"/api/analyses/bivariate/{cfa.Analysis.Id}");
            Assert.AreEqual(HttpStatusCode.NotFound, guard.StatusCode);
        }

        /// <summary>
        /// End-to-end time-series flow over HTTP: create each model family against a stored
        /// series, verify validation and the irrelevant-field rejection. No estimator runs.
        /// </summary>
        [TestMethod]
        public async Task TimeSeries_Create_Validate_Flow()
        {
            using var client = _factory.CreateClient();

            var seriesResponse = await client.PostAsync("/api/timeseries/usgs",
                new StringContent("{\"siteNumber\":\"01646500\",\"seriesType\":\"dailyDischarge\"}",
                    System.Text.Encoding.UTF8, "application/json"));
            var series = await seriesResponse.Content.ReadFromJsonAsync<TimeSeriesResourceResponse>(JsonOptions);
            Guid seriesId = series!.TimeSeries!.Id;

            foreach (string modelType in new[] { "ar", "ma", "arima", "arimax" })
            {
                var createResponse = await client.PostAsync("/api/analyses/timeseries",
                    new StringContent($"{{\"timeSeriesId\":\"{seriesId}\",\"modelType\":\"{modelType}\"}}",
                        System.Text.Encoding.UTF8, "application/json"));
                Assert.AreEqual(HttpStatusCode.Created, createResponse.StatusCode, $"Create must succeed for {modelType}.");
                var created = await createResponse.Content.ReadFromJsonAsync<AnalysisResourceResponse>(JsonOptions);
                Assert.AreEqual("timeSeries", created!.Analysis!.Kind);
                Assert.AreEqual(modelType, created.Analysis.TimeSeriesModelType);

                var validation = await client.GetFromJsonAsync<ValidationResponse>(
                    $"/api/analyses/timeseries/{created.Analysis.Id}/validate", JsonOptions);
                Assert.IsTrue(validation!.IsValid, $"A default {modelType} configuration must validate.");
            }

            // An irrelevant field is rejected with the offending field named.
            var badResponse = await client.PostAsJsonAsync("/api/analyses/timeseries", new CreateTimeSeriesAnalysisRequest
            {
                TimeSeriesId = seriesId,
                ModelType = Api.Store.TimeSeriesModelType.Ar,
                IncludeSeasonality = true
            }, JsonOptions);
            Assert.AreEqual(HttpStatusCode.BadRequest, badResponse.StatusCode);

            // A time-series id 404s on the univariate route and vice versa.
            var listResponse = await client.GetFromJsonAsync<AnalysisListResponse>("/api/analyses/timeseries", JsonOptions);
            var anyTimeSeries = listResponse!.Analyses.First();
            var crossGuard = await client.GetAsync($"/api/analyses/univariate/{anyTimeSeries.Id}");
            Assert.AreEqual(HttpStatusCode.NotFound, crossGuard.StatusCode);
        }
    }
}
