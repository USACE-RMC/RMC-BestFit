using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using RMC.BestFit.Api.Controllers;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Tests.Controllers
{
    /// <summary>
    /// Unit tests for <see cref="ApiControllerBase"/>: the exception-to-status-code mapping, the
    /// response contract stamping, and the non-finite response audit.
    /// </summary>
    [TestClass]
    public class ApiControllerBaseTests
    {
        /// <summary>
        /// Minimal concrete controller exposing the protected execute pipeline for testing.
        /// </summary>
        private sealed class TestController : ApiControllerBase
        {
            /// <summary>
            /// Runs an operation through the shared pipeline.
            /// </summary>
            /// <typeparam name="TResponse">The response DTO type.</typeparam>
            /// <param name="operation">The operation under test.</param>
            /// <param name="successStatusCode">The success status code.</param>
            /// <returns>The action result.</returns>
            public Task<ActionResult<TResponse>> Run<TResponse>(Func<TResponse> operation, int successStatusCode = 200)
                where TResponse : ResponseBase, new()
            {
                return ExecuteAsync(operation, NullLogger.Instance, "test.operation", successStatusCode);
            }
        }

        /// <summary>
        /// Extracts the object result (status + body) from an action result.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="actionResult">The action result returned by the pipeline.</param>
        /// <returns>The status code and typed response body.</returns>
        private static (int StatusCode, TResponse Body) Unwrap<TResponse>(ActionResult<TResponse> actionResult)
            where TResponse : ResponseBase
        {
            var objectResult = actionResult.Result as ObjectResult;
            Assert.IsNotNull(objectResult, "Expected an ObjectResult.");
            Assert.IsNotNull(objectResult.StatusCode, "Expected an explicit status code.");
            var body = objectResult.Value as TResponse;
            Assert.IsNotNull(body, "Expected a typed response body.");
            return (objectResult.StatusCode.Value, body);
        }

        /// <summary>
        /// Verifies the success path stamps success, timing, and timestamp, and honors the
        /// requested status code.
        /// </summary>
        [TestMethod]
        public async Task Execute_Success_StampsContract()
        {
            var controller = new TestController();
            var (status, body) = Unwrap(await controller.Run(
                () => new DeleteResourceResponse { DeletedId = Guid.NewGuid() }, successStatusCode: 201));

            Assert.AreEqual(201, status);
            Assert.IsTrue(body.Success);
            Assert.IsNotNull(body.ComputationTimeMs);
            Assert.IsNotNull(body.Timestamp);
            Assert.IsNull(body.ErrorMessage);
        }

        /// <summary>
        /// Verifies each typed exception maps to its documented status code.
        /// </summary>
        [TestMethod]
        public async Task Execute_ExceptionMapping_MatchesContract()
        {
            var controller = new TestController();

            var cases = new (Exception Exception, int ExpectedStatus)[]
            {
                (new RequestValidationException(new[] { "bad data" }), 400),
                (new ArgumentException("bad argument"), 400),
                (new ResourceNotFoundException("missing"), 404),
                (new UsgsDataNotFoundException("no data"), 404),
                (new ResourceConflictException("busy"), 409),
                (new OperationCanceledException(), 499),
                (new UsgsUnavailableException("upstream", statusCode: 502), 502),
                (new UsgsUnavailableException("offline", statusCode: 503), 503),
                (new InvalidOperationException("boom"), 500)
            };

            foreach (var (exception, expectedStatus) in cases)
            {
                var (status, body) = Unwrap(await controller.Run<DeleteResourceResponse>(() => throw exception));
                Assert.AreEqual(expectedStatus, status, $"Exception {exception.GetType().Name} mapped to {status}.");
                Assert.IsFalse(body.Success);
                Assert.IsNotNull(body.ErrorMessage);
            }
        }

        /// <summary>
        /// Verifies validation exceptions carry their individual messages in validationErrors.
        /// </summary>
        [TestMethod]
        public async Task Execute_ValidationFailure_CarriesErrorList()
        {
            var controller = new TestController();
            var (status, body) = Unwrap(await controller.Run<DeleteResourceResponse>(
                () => throw new RequestValidationException(new[] { "error one", "error two" })));

            Assert.AreEqual(400, status);
            Assert.IsNotNull(body.ValidationErrors);
            Assert.AreEqual(2, body.ValidationErrors.Count);
        }

        /// <summary>
        /// Verifies a response containing ±Infinity is rejected with 500 and the audit findings.
        /// </summary>
        [TestMethod]
        public async Task Execute_InfinityInResponse_Returns500WithFindings()
        {
            var controller = new TestController();
            var (status, body) = Unwrap(await controller.Run(() => new SummaryStatisticsResponse
            {
                InputDataId = Guid.NewGuid(),
                Statistics = new Dictionary<string, double> { ["bad"] = double.PositiveInfinity }
            }));

            Assert.AreEqual(500, status);
            Assert.IsFalse(body.Success);
            Assert.IsNotNull(body.NonFiniteFindings);
            Assert.AreEqual(1, body.NonFiniteFindings.Count);
        }

        /// <summary>
        /// Verifies NaN in a response is tolerated (missing-data marker, not a defect).
        /// </summary>
        [TestMethod]
        public async Task Execute_NaNInResponse_Succeeds()
        {
            var controller = new TestController();
            var (status, body) = Unwrap(await controller.Run(() => new SummaryStatisticsResponse
            {
                InputDataId = Guid.NewGuid(),
                Statistics = new Dictionary<string, double> { ["skew"] = double.NaN }
            }));

            Assert.AreEqual(200, status);
            Assert.IsTrue(body.Success);
        }
    }
}
