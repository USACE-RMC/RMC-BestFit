namespace RMC.BestFit.Api.Services.Exceptions
{
    /// <summary>
    /// Thrown when a USGS download succeeds at the HTTP level but returns no data for the
    /// requested site and series type. Mapped to HTTP 404 by the controller layer.
    /// </summary>
    public class UsgsDataNotFoundException : Exception
    {
        /// <summary>
        /// Constructs the exception with a client-facing message.
        /// </summary>
        /// <param name="message">A message identifying the site number and series type that returned no data.</param>
        public UsgsDataNotFoundException(string message) : base(message) { }
    }
}
