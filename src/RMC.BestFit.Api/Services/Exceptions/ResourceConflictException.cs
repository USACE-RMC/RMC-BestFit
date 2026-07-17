namespace RMC.BestFit.Api.Services.Exceptions
{
    /// <summary>
    /// Thrown when a request conflicts with the current state of the server — the resource store
    /// is at capacity, or the target analysis is already running. Mapped to HTTP 409 by the
    /// controller layer.
    /// </summary>
    public class ResourceConflictException : Exception
    {
        /// <summary>
        /// Constructs the exception with a client-facing message describing the conflict and how to resolve it.
        /// </summary>
        /// <param name="message">A message describing the conflict (e.g., store at capacity, analysis already running).</param>
        public ResourceConflictException(string message) : base(message) { }
    }
}
