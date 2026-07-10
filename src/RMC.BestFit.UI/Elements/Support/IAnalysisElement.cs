using Numerics.Distributions;
using Numerics.Utilities;
using FrameworkInterfaces;
using System.Threading.Tasks;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using ModelAnalyses = RMC.BestFit.Analyses;

namespace RMC.BestFit.UI
{
    /// <summary>
    /// Interface for RMC-BestFit analysis elements that participate in the WPF project tree.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This interface bridges the UI element layer (<see cref="IElement"/>) with the model-layer
    /// analysis contract (<see cref="ModelAnalyses.IAnalysis"/>). UI wrapper classes implement
    /// this interface to expose analysis capabilities while remaining part of the project tree.
    /// </para>
    /// </remarks>
    public interface IAnalysisElement : IElement
    {
        /// <summary>
        /// Gets the underlying model-layer analysis used by <see cref="ModelAnalyses.BatchAnalysisRunner"/>
        /// and other non-UI consumers. Returns <c>null</c> for analysis elements that have not yet been
        /// migrated to the delegation pattern (e.g. B17CAnalysis).
        /// </summary>
        ModelAnalyses.IAnalysis InnerAnalysis { get; }

        /// <summary>
        /// Gets the Bayesian analysis object containing prior distributions and Bayesian inference settings.
        /// </summary>
        BayesianAnalysis BayesianAnalysis { get; }

        /// <summary>
        /// Gets the uncertainty analysis results containing parameter estimates, confidence intervals, and goodness-of-fit statistics.
        /// </summary>
        UncertaintyAnalysisResults AnalysisResults { get; }

        /// <summary>
        /// Runs the analysis asynchronously and reports progress.
        /// </summary>
        /// <param name="progressReporter">The progress reporter for tracking and reporting analysis progress.</param>
        /// <returns>A task representing the asynchronous analysis operation.</returns>
        Task RunAsync(SafeProgressReporter progressReporter);

        /// <summary>
        /// Cancels the currently running analysis operation.
        /// </summary>
        void CancelAnalysis();

        /// <summary>
        /// Gets a value indicating whether the analysis has been successfully estimated and has valid results.
        /// </summary>
        bool IsEstimated { get; }
    }
}
