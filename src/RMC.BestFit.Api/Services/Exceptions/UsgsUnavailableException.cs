namespace RMC.BestFit.Api.Services.Exceptions
{
    /// <summary>
    /// Thrown when the USGS water services cannot be reached or return an upstream error.
    /// Mapped to HTTP 502 (upstream error) or 503 (no connectivity) by the controller layer.
    /// </summary>
    public class UsgsUnavailableException : Exception
    {
        /// <summary>
        /// Constructs the exception with a client-facing message and the suggested HTTP status code.
        /// </summary>
        /// <param name="message">A message describing the upstream failure.</param>
        /// <param name="statusCode">The HTTP status code the API should return: 502 for an upstream USGS error, 503 for no connectivity.</param>
        /// <param name="innerException">The underlying exception from the download layer, if any.</param>
        public UsgsUnavailableException(string message, int statusCode = 502, Exception? innerException = null)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }

        /// <summary>
        /// The HTTP status code the API should return for this failure: 502 when the USGS service
        /// responded with an error, 503 when there is no internet connectivity.
        /// </summary>
        public int StatusCode { get; }
    }
}
