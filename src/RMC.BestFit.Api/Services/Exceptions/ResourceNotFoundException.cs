namespace RMC.BestFit.Api.Services.Exceptions
{
    /// <summary>
    /// Thrown when a client references a resource id that does not exist in the resource store.
    /// Mapped to HTTP 404 by the controller layer.
    /// </summary>
    public class ResourceNotFoundException : Exception
    {
        /// <summary>
        /// Constructs the exception with a client-facing message.
        /// </summary>
        /// <param name="message">A message identifying the missing resource, including its id and expected type.</param>
        public ResourceNotFoundException(string message) : base(message) { }

        /// <summary>
        /// Constructs the exception for a specific resource type and id.
        /// </summary>
        /// <param name="resourceType">The user-facing resource type name (e.g., "time series").</param>
        /// <param name="id">The id that was not found.</param>
        public ResourceNotFoundException(string resourceType, Guid id)
            : base($"No {resourceType} resource exists with id '{id}'. It may have been deleted, or the id may belong to a different resource type.")
        {
        }
    }
}
