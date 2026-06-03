using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{

    /// <summary>
    /// Model interface.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IModel : INotifyPropertyChanged
    {

        /// <summary>
        /// The list of model parameters.
        /// </summary>
        List<ModelParameter> Parameters { get; }

        /// <summary>
        /// Returns the number of model parameters.
        /// </summary>
        int NumberOfParameters { get; }

        /// <summary>
        /// Determines whether to use default flat priors for the distribution parameters. 
        /// </summary>
        bool UseDefaultFlatPriors { get; set; }

        /// <summary>
        /// The log-likelihood function to evaluate.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>The log-Likelihood given the parameter set.</returns>
        /// <remarks>
        /// This function should account for the data likelihood 
        /// as well as the prior likelihood of the model parameters.
        /// </remarks>
        double LogLikelihood(double[] parameters);

        /// <summary>
        /// The data log-likelihood function to evaluate.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>The data log-Likelihood given the parameter set.</returns>
        /// <remarks>
        /// This function should account for only the data likelihood,
        /// and should not include the prior likelihood of the model parameters.
        /// </remarks>
        double DataLogLikelihood(double[] parameters);

        /// <summary>
        /// Computes the pointwise data log-likelihood for each observation.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>
        /// An array of log-likelihood values, one for each observation in the data.
        /// The sum of these values equals <see cref="DataLogLikelihood(double[])"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// Pointwise log-likelihoods are required for computing the Watanabe-Akaike
        /// Information Criterion (WAIC) and leave-one-out cross-validation (LOO-CV).
        /// </para>
        /// <para>
        /// For models with different data types (exact, uncertain, interval, threshold),
        /// the array contains one entry per logical observation, regardless of type.
        /// </para>
        /// </remarks>
        double[] PointwiseDataLogLikelihood(double[] parameters);

        /// <summary>
        /// Computes the pointwise data log-likelihood with metadata for each observation.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>
        /// A list of <see cref="DataComponent"/> structs, each containing the log-likelihood
        /// contribution and metadata for an observation (type, value, count, name).
        /// The sum of all LogLikelihood values equals <see cref="DataLogLikelihood(double[])"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method provides full metadata for influence diagnostics, allowing
        /// identification of which observations have the largest impact on estimates.
        /// </para>
        /// <para>
        /// Each <see cref="DataComponent"/> contains:
        /// </para>
        /// <list type="bullet">
        /// <item><description><c>Type</c>: The data type (Exact, Uncertain, Interval, LeftCensored, RightCensored)</description></item>
        /// <item><description><c>Value</c>: Representative value (exact, mean, midpoint, or threshold)</description></item>
        /// <item><description><c>Count</c>: Number of observations (1 for most types, n for thresholds)</description></item>
        /// <item><description><c>Name</c>: Optional label (e.g., "1900-1950" for threshold periods)</description></item>
        /// </list>
        /// <para>
        /// For efficient WAIC/PSIS-LOO computation where metadata is not needed,
        /// use <see cref="PointwiseDataLogLikelihood(double[])"/> instead.
        /// </para>
        /// </remarks>
        List<DataComponent> PointwiseDataLogLikelihoodComponents(double[] parameters);

        /// <summary>
        /// The prior log-likelihood function to evaluate.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>The prior log-Likelihood given the parameter set.</returns>
        /// <remarks>
        /// This function should account for only the prior likelihood of the model parameters.
        /// </remarks>
        double PriorLogLikelihood(double[] parameters);

        /// <summary>
        /// Computes the pointwise prior log-likelihood for each prior component.
        /// </summary>
        /// <param name="parameters">The list of parameters to evaluate.</param>
        /// <returns>
        /// A list of <see cref="PriorComponent"/> objects, each containing the name
        /// and log-likelihood contribution of a prior component.
        /// The sum of all LogLikelihood values equals <see cref="PriorLogLikelihood(double[])"/>.
        /// </returns>
        /// <remarks>
        /// <para>
        /// This method enables prior sensitivity analysis and influence diagnostics by
        /// separating the contribution of each prior component:
        /// </para>
        /// <list type="bullet">
        /// <item><description>Parameter priors (one per parameter with a non-uniform prior)</description></item>
        /// <item><description>Quantile priors (for univariate distribution models)</description></item>
        /// <item><description>Jeffreys scale priors (if enabled)</description></item>
        /// <item><description>Other penalty terms (parameter penalties, spatial error terms, etc.)</description></item>
        /// </list>
        /// </remarks>
        List<PriorComponent> PointwisePriorLogLikelihood(double[] parameters);

        /// <summary>
        /// Set the model parameter values.
        /// </summary>
        /// <param name="parameters">The list of model parameters.</param>
        void SetParameterValues(IList<double> parameters);

        /// <summary>
        /// Set the default parameters and priors for this model. 
        /// </summary>
        void SetDefaultParameters();

        /// <summary>
        /// Return a deep copy of the model.
        /// </summary>
        IModel Clone();

        /// <summary>
        /// Returns the model as XElement.
        /// </summary>
        XElement ToXElement();

        /// <summary>
        /// Validates the current state of the object and reports any issues found.
        /// </summary>
        /// <returns>
        /// A tuple containing:
        /// <list type="bullet">
        /// <item>
        /// <description><c>IsValid</c>: <c>true</c> if the object passes all validation checks; otherwise <c>false</c>.</description>
        /// </item>
        /// <item>
        /// <description><c>ValidationMessages</c>: a list of messages describing any validation errors or warnings. This list is empty when the object is valid.</description>
        /// </item>
        /// </list>
        /// </returns>
        (bool IsValid, List<string> ValidationMessages) Validate();

    }
}
