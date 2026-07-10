using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RMC.BestFit.Api.Configuration;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services;
using RMC.BestFit.Api.Store;
using RMC.BestFit.Api.Tests.Support;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="CoincidentFrequencyAnalysisController"/> status codes and
    /// payloads, using the real service over an in-memory store. Analyses are created and
    /// validated but never run.
    /// </summary>
    [TestClass]
    public class CoincidentFrequencyAnalysisControllerTests
    {
        /// <summary>
        /// The store backing the controller stack.
        /// </summary>
        private InMemoryResourceStore _store = null!;

        /// <summary>
        /// The service shared by the controller and upstream creation.
        /// </summary>
        private AnalysisService _service = null!;

        /// <summary>
        /// The controller under test.
        /// </summary>
        private CoincidentFrequencyAnalysisController _controller = null!;

        /// <summary>
        /// Creates a fresh controller stack before each test.
        /// </summary>
        [TestInitialize]
        public void Initialize()
        {
            _store = new InMemoryResourceStore(Options.Create(new ApiOptions()));
            _service = new AnalysisService(_store, Options.Create(new ApiOptions()));
            _controller = new CoincidentFrequencyAnalysisController(NullLogger<CoincidentFrequencyAnalysisController>.Instance, _service);
        }

        /// <summary>
        /// Extracts status code and body from an action result.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="actionResult">The action result.</param>
        /// <returns>The status code and body.</returns>
        private static (int StatusCode, TResponse Body) Unwrap<TResponse>(ActionResult<TResponse> actionResult)
            where TResponse : ResponseBase
        {
            var objectResult = (ObjectResult)actionResult.Result!;
            return (objectResult.StatusCode!.Value, (TResponse)objectResult.Value!);
        }

        /// <summary>
        /// Creates a stored bivariate analysis over two unrun univariate marginals.
        /// </summary>
        /// <returns>The bivariate resource.</returns>
        private AnalysisResource CreateStoredBivariate()
        {
            var input = _store.AddInputData(TestAnalyses.CreateInputDataResource());
            var marginalX = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            var marginalY = _service.CreateUnivariate(new CreateUnivariateAnalysisRequest { InputDataId = input.Id });
            return _service.CreateBivariate(new CreateBivariateAnalysisRequest
            {
                MarginalXAnalysisId = marginalX.Id,
                MarginalYAnalysisId = marginalY.Id,
                XyOrdinates = new List<XyOrdinateDto> { new() { X = 300d, Y = 320d } }
            });
        }

        /// <summary>
        /// Builds a valid creation request over a stored bivariate analysis.
        /// </summary>
        /// <param name="bivariateId">The upstream bivariate id.</param>
        /// <returns>The request.</returns>
        private static CreateCoincidentFrequencyAnalysisRequest CreateValidRequest(Guid bivariateId)
        {
            return new CreateCoincidentFrequencyAnalysisRequest
            {
                BivariateAnalysisId = bivariateId,
                XValues = new List<double> { 100d, 200d, 300d },
                YValues = new List<double> { 50d, 100d, 150d },
                BivariateResponse = new List<List<double>>
                {
                    new() { 10d, 11d, 12d },
                    new() { 13d, 14d, 15d },
                    new() { 16d, 17d, 18d }
                }
            };
        }

        /// <summary>
        /// Verifies create returns 201 with provenance and bins, 404 for an unknown bivariate id,
        /// and 400 for a ragged surface.
        /// </summary>
        [TestMethod]
        public async Task Create_Returns201_404_400()
        {
            var bivariate = CreateStoredBivariate();

            var (status, body) = Unwrap(await _controller.Create(CreateValidRequest(bivariate.Id)));
            Assert.AreEqual(201, status);
            Assert.AreEqual("coincidentFrequency", body.Analysis!.Kind);
            Assert.AreEqual(bivariate.Id, body.Analysis.BivariateAnalysisId);
            Assert.AreEqual(50, body.Analysis.NumberOfBins);
            Assert.IsFalse(body.Analysis.IsValid, "An unestimated bivariate must make the analysis invalid.");

            var (missingStatus, _) = Unwrap(await _controller.Create(CreateValidRequest(Guid.NewGuid())));
            Assert.AreEqual(404, missingStatus);

            var ragged = CreateValidRequest(bivariate.Id);
            ragged.BivariateResponse[1] = new List<double> { 13d };
            var (raggedStatus, _) = Unwrap(await _controller.Create(ragged));
            Assert.AreEqual(400, raggedStatus);
        }

        /// <summary>
        /// Verifies validate reports the unestimated bivariate run-free, plus get/list/
        /// results-before-run/delete round-trip with kind guarding and the run conflict while
        /// the upstream bivariate is locked.
        /// </summary>
        [TestMethod]
        public async Task Get_List_Validate_Delete_Lifecycle()
        {
            var bivariate = CreateStoredBivariate();
            var (_, created) = Unwrap(await _controller.Create(CreateValidRequest(bivariate.Id)));
            var id = created.Analysis!.Id;

            var validateResult = _controller.Validate(id);
            var validateBody = (ValidationResponse)((OkObjectResult)validateResult.Result!).Value!;
            Assert.IsFalse(validateBody.IsValid);
            Assert.IsTrue(validateBody.Errors.Any(e => e.Contains("not been estimated")),
                "Validation must explain that the bivariate requires estimation.");

            var (getStatus, got) = Unwrap(await _controller.Get(id));
            Assert.AreEqual(200, getStatus);
            Assert.AreEqual(id, got.Analysis!.Id);

            var (listStatus, list) = Unwrap(await _controller.List());
            Assert.AreEqual(200, listStatus);
            Assert.AreEqual(1, list.Count);

            var (resultsStatus, _) = Unwrap(await _controller.GetResults(id));
            Assert.AreEqual(404, resultsStatus, "Results before any run must be 404.");

            var resource = _store.GetAnalysis(id)!;
            Assert.IsTrue(bivariate.RunLock.Wait(0));
            try
            {
                var (conflictStatus, _) = Unwrap(await _controller.Run(resource.Id, CancellationToken.None));
                Assert.AreEqual(409, conflictStatus, "A busy upstream bivariate must block the run.");
            }
            finally
            {
                bivariate.RunLock.Release();
            }

            var other = _store.AddAnalysis(TestAnalyses.CreateUnivariateResource());
            var (guardStatus, _) = Unwrap(await _controller.Get(other.Id));
            Assert.AreEqual(404, guardStatus, "A univariate id must 404 on the coincident frequency routes.");

            var (deleteStatus, deleted) = Unwrap(await _controller.Delete(id));
            Assert.AreEqual(200, deleteStatus);
            Assert.AreEqual(id, deleted.DeletedId);
        }
    }
}
