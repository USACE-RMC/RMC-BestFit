using RMC.BestFit.Analyses;
using RMC.BestFit.Api.Store;

namespace RMC.BestFit.Api.Helpers
{
    /// <summary>
    /// Orchestrates a single synchronous analysis run against the model layer, normalizing the
    /// three analyses' divergent failure semantics into thrown exceptions the controller layer
    /// maps to status codes.
    /// </summary>
    /// <remarks>
    /// Verified model behavior this helper encodes: <c>UnivariateAnalysis.RunAsync</c> and
    /// <c>RatingCurveAnalysis.RunAsync</c> swallow exceptions and report the outcome only through
    /// the <c>AnalysisCompleted</c> event; <c>Bulletin17CAnalysis.RunAsync</c> fires no events,
    /// rethrows exceptions, catches cancellation itself, and returns silently with
    /// <c>IsEstimated == false</c> when the GMM solver fails. Cancellation for every kind goes
    /// through <c>AnalysisBase.CancelAnalysis()</c>.
    /// </remarks>
    public static class AnalysisRunHelper
    {
        /// <summary>
        /// Runs model-layer validation for the resource's analysis, combined with API-level rules
        /// the model cannot see: a bivariate analysis's marginals live in OTHER resources, so the
        /// model's own Validate cannot know whether they have been estimated.
        /// </summary>
        /// <param name="resource">The analysis resource.</param>
        /// <returns>The validity flag and validation messages.</returns>
        public static (bool IsValid, List<string> Messages) Validate(AnalysisResource resource)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var (isValid, messages) = resource.Analysis.Validate();

            if (resource.Kind == AnalysisKind.Bivariate)
            {
                if (resource.MarginalXResource is { } marginalX && !marginalX.Analysis.IsEstimated)
                {
                    isValid = false;
                    messages.Add($"Error: The marginal X analysis '{marginalX.Name}' requires estimation. Run it first (POST .../{marginalX.Id}/run).");
                }
                if (resource.MarginalYResource is { } marginalY && !marginalY.Analysis.IsEstimated)
                {
                    isValid = false;
                    messages.Add($"Error: The marginal Y analysis '{marginalY.Name}' requires estimation. Run it first (POST .../{marginalY.Id}/run).");
                }
            }

            return (isValid, messages);
        }

        /// <summary>
        /// Executes the analysis run and throws when it did not produce an estimated fit.
        /// </summary>
        /// <param name="resource">The analysis resource to run.</param>
        /// <param name="cancellationToken">Client cancellation; wired to <c>CancelAnalysis()</c> for the duration of the run.</param>
        /// <returns>A task completing when the run has succeeded.</returns>
        /// <exception cref="OperationCanceledException">Thrown when the run was cancelled (mapped to HTTP 499).</exception>
        /// <exception cref="InvalidOperationException">Thrown when the run failed (mapped to HTTP 500 with the model's error message).</exception>
        public static async Task ExecuteAsync(AnalysisResource resource, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(resource);
            var analysis = resource.Analysis;

            // CancelAnalysis() is the model's real cancellation path: it cancels the analysis-level
            // token source and (for MCMC kinds) the sampler simulation.
            using var registration = cancellationToken.Register(static state => ((AnalysisBase)state!).CancelAnalysis(), analysis);

            if (resource.Kind == AnalysisKind.Bulletin17C)
            {
                // B17C rethrows run errors and swallows cancellation internally; detect the latter
                // from the request token, and the silent GMM failure from IsEstimated.
                await analysis.RunAsync(null);
                cancellationToken.ThrowIfCancellationRequested();
                if (!analysis.IsEstimated)
                {
                    throw new InvalidOperationException(
                        "The GMM solver failed to find a solution for the Bulletin 17C distribution fit. " +
                        "Check the input data (record length, low outliers, zero flows) and try a different distribution or uncertainty method.");
                }
                return;
            }

            // Univariate and rating curve analyses swallow exceptions and report only through the
            // AnalysisCompleted event; missing that subscription would turn failures into silent
            // 200-with-no-results responses.
            AnalysisRunCompletedEventArgs? completion = null;
            void OnCompleted(object? sender, AnalysisRunCompletedEventArgs e) => completion = e;
            analysis.AnalysisCompleted += OnCompleted;
            try
            {
                await analysis.RunAsync(null);
            }
            finally
            {
                analysis.AnalysisCompleted -= OnCompleted;
            }

            ThrowIfRunFailed(completion, analysis.IsEstimated, cancellationToken);
        }

        /// <summary>
        /// Converts a captured run-completion outcome into the exception contract: cancellation,
        /// the model's error, or a generic no-fit failure. No-op when the run succeeded.
        /// </summary>
        /// <param name="completion">The captured <c>AnalysisCompleted</c> event args, or null when the event never fired.</param>
        /// <param name="isEstimated">The analysis's estimated flag after the run.</param>
        /// <param name="cancellationToken">The client cancellation token observed during the run.</param>
        /// <exception cref="OperationCanceledException">Thrown when the run was cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the run failed.</exception>
        public static void ThrowIfRunFailed(AnalysisRunCompletedEventArgs? completion, bool isEstimated, CancellationToken cancellationToken)
        {
            if (completion?.Cancelled == true || cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException("The analysis run was cancelled.");
            }
            if (completion?.Error != null)
            {
                throw new InvalidOperationException($"The analysis run failed: {completion.Error.Message}", completion.Error);
            }
            if (completion == null || !completion.Succeeded || !isEstimated)
            {
                throw new InvalidOperationException(
                    "The analysis run did not produce an estimated fit. Check the input data and estimation settings, then rerun.");
            }
        }
    }
}
