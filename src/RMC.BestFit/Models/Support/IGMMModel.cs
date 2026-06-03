using RMC.BestFit.Estimation;
using System.Collections.Generic;
using System.ComponentModel;
using System.Xml.Linq;

namespace RMC.BestFit.Models
{
    /// <summary>
    /// Model interface for the Generalized Method of Moments (GMM) estimation method.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     This is the GMM analog of <see cref="IModel"/>. Where <see cref="IModel"/> defines
    ///     likelihood-based estimation contracts (LogLikelihood, DataLogLikelihood, etc.),
    ///     <c>IGMMModel</c> defines moment-based estimation contracts (MomentConditionFunction,
    ///     PointwiseMomentConditions, etc.).
    /// </para>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// </remarks>
    public interface IGMMModel : INotifyPropertyChanged
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
        /// Returns the number of moment conditions used in the GMM estimation.
        /// </summary>
        int NumberOfMomentConditions { get; }

        /// <summary>
        /// Gets the total sample size (number of observations) used in GMM estimation.
        /// </summary>
        int SampleSize { get; }

        /// <summary>
        /// Gets the moment condition function that returns the sample mean of moment conditions
        /// and their covariance matrix.
        /// </summary>
        MomentConditionFunction MomentConditionFunction { get; }

        /// <summary>
        /// Gets the optional analytical Jacobian function (∂g/∂θ).
        /// Returns null if no analytical Jacobian is available (numerical differentiation will be used).
        /// </summary>
        JacobianFunction? JacobianFunction { get; }

        /// <summary>
        /// Gets the optional penalty function for regularization.
        /// Returns null if no penalty is used.
        /// </summary>
        PenaltyFunction? PenaltyFunction { get; }

        /// <summary>
        /// Gets the optional pointwise moment condition function that returns per-observation
        /// moment condition vectors.
        /// </summary>
        /// <remarks>
        /// <para>
        /// When provided, this function returns a matrix of dimension [n × q] where n is the sample size
        /// and q is the number of moment conditions. Row i contains observation i's contribution to the
        /// moment conditions: g_i(θ). The sample mean of all rows equals the G vector from
        /// <see cref="MomentConditionFunction"/>.
        /// </para>
        /// <para>
        /// This function is required for computing influence diagnostics (observation influence,
        /// Cook's distance). Returns null if pointwise decomposition is not available for this model.
        /// </para>
        /// </remarks>
        PointwiseMomentConditionFunction? PointwiseMomentConditions { get; }

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
        IGMMModel Clone();

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
