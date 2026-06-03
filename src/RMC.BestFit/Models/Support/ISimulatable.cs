namespace RMC.BestFit.Models
{
    /// <summary>
    /// Interface for models that can generate simulated data.
    /// </summary>
    /// <typeparam name="TData">
    /// The type of data the model generates. Common types include:
    /// <list type="bullet">
    /// <item><description><c>double[]</c> for univariate distributions and time series</description></item>
    /// <item><description><c>double[,]</c> for bivariate distributions (paired samples)</description></item>
    /// <item><description><c>TimeSeries</c> for time-indexed data</description></item>
    /// </list>
    /// </typeparam>
    /// <remarks>
    /// <para>
    ///     <b>Authors:</b>
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// This interface is separate from <see cref="IModel"/> because not all models
    /// that define a likelihood can generate simulated data, and the output type
    /// varies by model type.
    /// </para>
    /// <para>
    /// Models implementing this interface can be used with:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Prior predictive checking</description></item>
    /// <item><description>Posterior predictive checking</description></item>
    /// <item><description>Simulation studies</description></item>
    /// <item><description>Bootstrap methods</description></item>
    /// </list>
    /// </remarks>
    public interface ISimulatable<TData>
    {
        /// <summary>
        /// Generates random samples from the model using its current parameter values.
        /// </summary>
        /// <param name="sampleSize">The number of random samples to generate.</param>
        /// <param name="seed">
        /// Optional PRNG seed for reproducibility. If less than or equal to zero,
        /// the computer clock is used for seeding.
        /// </param>
        /// <returns>
        /// Simulated data of type <typeparamref name="TData"/>. The exact structure
        /// depends on the model type.
        /// </returns>
        /// <exception cref="ArgumentOutOfRangeException">
        /// Thrown if <paramref name="sampleSize"/> is less than 1.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// Thrown if the model is not in a valid state for generating samples
        /// (e.g., missing required distributions or parameters).
        /// </exception>
        TData GenerateRandomValues(int sampleSize, int seed = -1);
    }
}
