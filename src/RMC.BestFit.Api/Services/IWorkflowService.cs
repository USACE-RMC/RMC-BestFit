using RMC.BestFit.Api.DTOs;

namespace RMC.BestFit.Api.Services
{
    /// <summary>
    /// Composes the resource services into one-shot USGS workflows (download → process → analyze
    /// → results). Step failures are reported in-body (success=false, failedStep, partial ids)
    /// rather than thrown, so clients keep the ids of everything already created.
    /// </summary>
    public interface IWorkflowService
    {
        /// <summary>
        /// Downloads USGS annual peaks, builds input data, and runs a univariate frequency analysis.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Client cancellation.</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        Task<UsgsFrequencyWorkflowResponse> RunUsgsPeakFrequencyAsync(UsgsPeakFrequencyWorkflowRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads USGS daily flow, extracts block maxima, and runs a univariate frequency analysis.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Client cancellation.</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        Task<UsgsFrequencyWorkflowResponse> RunUsgsBlockMaxFrequencyAsync(UsgsBlockMaxFrequencyWorkflowRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads USGS annual peaks, builds input data, and runs a Bulletin 17C analysis.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Client cancellation.</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        Task<UsgsFrequencyWorkflowResponse> RunUsgsBulletin17CAsync(UsgsBulletin17CWorkflowRequest request, CancellationToken cancellationToken = default);

        /// <summary>
        /// Downloads USGS measured stage and discharge, and runs a rating curve analysis over the
        /// date-aligned pairs.
        /// </summary>
        /// <param name="request">The workflow request.</param>
        /// <param name="cancellationToken">Client cancellation.</param>
        /// <returns>The workflow response with resource ids and results.</returns>
        Task<UsgsRatingCurveWorkflowResponse> RunUsgsRatingCurveAsync(UsgsRatingCurveWorkflowRequest request, CancellationToken cancellationToken = default);
    }
}
