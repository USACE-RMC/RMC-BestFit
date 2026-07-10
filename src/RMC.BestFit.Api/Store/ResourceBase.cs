namespace RMC.BestFit.Api.Store
{
    /// <summary>
    /// Base class for all server-side resources held in the in-memory resource store.
    /// </summary>
    /// <remarks>
    /// Resources are immutable after creation (aside from analysis run state): changing a time
    /// series or input data set means creating a new resource. This keeps downstream analyses,
    /// which clone their inputs at creation, permanently consistent with the data they were fit to.
    /// </remarks>
    public abstract class ResourceBase
    {
        /// <summary>
        /// The unique identifier clients use to reference this resource in subsequent requests.
        /// </summary>
        public Guid Id { get; } = Guid.NewGuid();

        /// <summary>
        /// The client-supplied (or generated) display name of the resource.
        /// </summary>
        public required string Name { get; init; }

        /// <summary>
        /// An optional client-supplied description of the resource.
        /// </summary>
        public string? Description { get; init; }

        /// <summary>
        /// The UTC timestamp at which the resource was created.
        /// </summary>
        public DateTime CreatedUtc { get; } = DateTime.UtcNow;
    }
}
