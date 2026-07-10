using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RMC.BestFit.Api.DTOs;
using RMC.BestFit.Api.Helpers;
using RMC.BestFit.Api.Services.Exceptions;

namespace RMC.BestFit.Api.Controllers
{
    /// <summary>
    /// Base controller providing the shared execute-and-map pipeline: every endpoint's work runs
    /// inside <see cref="ExecuteAsync{TResponse}(Func{Task{TResponse}}, ILogger, string, int)"/>,
    /// which stamps timing/timestamp, audits the response for non-finite values, and translates
    /// the API's typed exceptions into the documented HTTP status codes.
    /// </summary>
    /// <remarks>
    /// Status mapping: 400 validation/argument errors, 404 unknown resource or no USGS data,
    /// 409 conflicts (store full, analysis already running), 499 client cancellation,
    /// 502/503 USGS upstream failures, 500 anything else.
    /// </remarks>
    [ApiController]
    [Produces("application/json")]
    public abstract class ApiControllerBase : ControllerBase
    {
        /// <summary>
        /// Runs an asynchronous operation and maps its outcome (or exception) to the shared
        /// response contract.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="operation">The operation producing the response body.</param>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="operationName">Short operation name used in log messages.</param>
        /// <param name="successStatusCode">The status code for the success path (200 or 201).</param>
        /// <returns>The action result carrying the response body.</returns>
        protected async Task<ActionResult<TResponse>> ExecuteAsync<TResponse>(
            Func<Task<TResponse>> operation,
            ILogger logger,
            string operationName,
            int successStatusCode = StatusCodes.Status200OK)
            where TResponse : ResponseBase, new()
        {
            ArgumentNullException.ThrowIfNull(operation);
            ArgumentNullException.ThrowIfNull(logger);
            var stopwatch = Stopwatch.StartNew();
            try
            {
                // Success defaults to true on ResponseBase and is NOT forced here: workflow
                // endpoints report step failures in-body (success=false with partial resource ids)
                // while still returning through this success path.
                var response = await operation();
                response.ComputationTimeMs = stopwatch.ElapsedMilliseconds;
                response.Timestamp = DateTime.UtcNow.ToString("O");

                // Final seatbelt: fail loudly on ±Infinity rather than letting the JSON serializer
                // emit a value most clients cannot parse. NaN is legitimate (missing data) and passes.
                var findings = ResponseFiniteAuditor.Audit(response);
                if (findings.Count > 0)
                {
                    logger.LogError("[API] {Operation}: non-finite values detected in response at: {Paths}",
                        operationName, string.Join("; ", findings));
                    return Fail<TResponse>(StatusCodes.Status500InternalServerError,
                        "The operation produced non-finite values. See nonFiniteFindings.",
                        stopwatch, nonFiniteFindings: findings);
                }

                return StatusCode(successStatusCode, response);
            }
            catch (RequestValidationException ex)
            {
                logger.LogWarning("[API] {Operation}: validation failed: {Errors}", operationName, string.Join("; ", ex.Errors));
                return Fail<TResponse>(StatusCodes.Status400BadRequest, ex.Message, stopwatch, validationErrors: ex.Errors.ToList());
            }
            catch (ResourceNotFoundException ex)
            {
                logger.LogWarning("[API] {Operation}: {Message}", operationName, ex.Message);
                return Fail<TResponse>(StatusCodes.Status404NotFound, ex.Message, stopwatch);
            }
            catch (UsgsDataNotFoundException ex)
            {
                logger.LogWarning("[API] {Operation}: {Message}", operationName, ex.Message);
                return Fail<TResponse>(StatusCodes.Status404NotFound, ex.Message, stopwatch);
            }
            catch (ResourceConflictException ex)
            {
                logger.LogWarning("[API] {Operation}: {Message}", operationName, ex.Message);
                return Fail<TResponse>(StatusCodes.Status409Conflict, ex.Message, stopwatch);
            }
            catch (UsgsUnavailableException ex)
            {
                logger.LogError(ex, "[API] {Operation}: USGS unavailable", operationName);
                return Fail<TResponse>(ex.StatusCode, ex.Message, stopwatch);
            }
            catch (OperationCanceledException)
            {
                logger.LogInformation("[API] {Operation}: cancelled by client", operationName);
                return Fail<TResponse>(499, "Operation cancelled by client.", stopwatch);
            }
            catch (ArgumentException ex)
            {
                logger.LogWarning(ex, "[API] {Operation}: bad request", operationName);
                return Fail<TResponse>(StatusCodes.Status400BadRequest, ex.Message, stopwatch);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "[API] {Operation}: unhandled error", operationName);
                return Fail<TResponse>(StatusCodes.Status500InternalServerError, $"Internal error: {ex.Message}", stopwatch);
            }
        }

        /// <summary>
        /// Runs a synchronous operation through the same pipeline as
        /// <see cref="ExecuteAsync{TResponse}(Func{Task{TResponse}}, ILogger, string, int)"/>.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="operation">The operation producing the response body.</param>
        /// <param name="logger">The controller's logger.</param>
        /// <param name="operationName">Short operation name used in log messages.</param>
        /// <param name="successStatusCode">The status code for the success path (200 or 201).</param>
        /// <returns>The action result carrying the response body.</returns>
        protected Task<ActionResult<TResponse>> ExecuteAsync<TResponse>(
            Func<TResponse> operation,
            ILogger logger,
            string operationName,
            int successStatusCode = StatusCodes.Status200OK)
            where TResponse : ResponseBase, new()
        {
            ArgumentNullException.ThrowIfNull(operation);
            return ExecuteAsync(() => Task.FromResult(operation()), logger, operationName, successStatusCode);
        }

        /// <summary>
        /// Builds a failure response body with the shared contract fields populated.
        /// </summary>
        /// <typeparam name="TResponse">The response DTO type.</typeparam>
        /// <param name="statusCode">The HTTP status code to return.</param>
        /// <param name="errorMessage">The client-facing error message.</param>
        /// <param name="stopwatch">The request stopwatch, used to stamp the elapsed time.</param>
        /// <param name="validationErrors">Optional individual validation error messages.</param>
        /// <param name="nonFiniteFindings">Optional non-finite audit findings.</param>
        /// <returns>The failure action result.</returns>
        private ObjectResult Fail<TResponse>(
            int statusCode,
            string errorMessage,
            Stopwatch stopwatch,
            List<string>? validationErrors = null,
            List<string>? nonFiniteFindings = null)
            where TResponse : ResponseBase, new()
        {
            var response = new TResponse
            {
                Success = false,
                ErrorMessage = errorMessage,
                ValidationErrors = validationErrors,
                NonFiniteFindings = nonFiniteFindings,
                ComputationTimeMs = stopwatch.ElapsedMilliseconds,
                Timestamp = DateTime.UtcNow.ToString("O")
            };
            return StatusCode(statusCode, response);
        }
    }
}
