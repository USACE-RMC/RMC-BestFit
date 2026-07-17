namespace RMC.BestFit.Api.Services.Exceptions
{
    /// <summary>
    /// Thrown when a request fails model-layer validation (e.g., <c>DataFrame.Validate()</c> or an
    /// analysis <c>Validate()</c> reports errors). Carries the individual validation messages so
    /// the controller layer can populate the response's <c>validationErrors</c> list alongside
    /// HTTP 400.
    /// </summary>
    public class RequestValidationException : Exception
    {
        /// <summary>
        /// Constructs the exception from a list of validation error messages.
        /// </summary>
        /// <param name="errors">The individual validation error messages reported by the model layer.</param>
        /// <param name="message">An optional summary message; defaults to a generic validation-failed summary.</param>
        public RequestValidationException(IEnumerable<string> errors, string? message = null)
            : base(message ?? "Validation failed. See validationErrors for details.")
        {
            Errors = errors.ToList();
        }

        /// <summary>
        /// The individual validation error messages reported by the model layer.
        /// </summary>
        public IReadOnlyList<string> Errors { get; }
    }
}
