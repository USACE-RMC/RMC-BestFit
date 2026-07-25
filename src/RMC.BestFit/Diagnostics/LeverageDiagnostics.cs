using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Xml.Linq;
using Numerics.Mathematics;
using Numerics.Mathematics.LinearAlgebra;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

namespace RMC.BestFit.Diagnostics
{
    /// <summary>
    /// Provides fitted-estimate diagnostics that decompose each observation's and prior's impact
    /// into fit influence (Cook's Distance) and variance influence.
    /// </summary>
    /// <remarks>
    /// <para>
    ///     Authors:
    ///     Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil
    /// </para>
    /// <para>
    /// At the MAP estimate θ̂, the posterior Fisher information decomposes additively:
    /// </para>
    /// <para>
    /// J_post = Σᵢ Jᵢ + J_prior,  where Jᵢ = −∇²θ log f(yᵢ|θ)
    /// </para>
    /// <para>
    /// Three complementary views of influence are provided:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// <b>Combined Leverage</b> = FitInfluence + VarianceInfluence. This additive ranking index
    /// is displayed as stacked bars; it is not classical hat-matrix leverage and need not sum to p.
    /// </description></item>
    /// <item><description>
    /// <b>Fit Influence (Cook's Distance)</b> = gᵢᵀ J⁻¹_post gᵢ / p, where gᵢ = ∇θ log f(yᵢ|θ)
    /// is the score vector. Measures how much removing the component shifts the MAP parameters.
    /// Zero when the component is perfectly consistent with the model.
    /// </description></item>
    /// <item><description>
    /// <b>Variance Influence</b> measures a component's effect on parameter uncertainty.
    /// Observation entries use the local curvature trace; prior and penalty entries use the
    /// finite change in log generalized variance after removing that component. The reported
    /// magnitudes are non-negative and remain distinct from Cook fit influence.
    /// </description></item>
    /// </list>
    /// <para>
    /// The score vectors are computed from the same forward/backward perturbation data used for Hessian
    /// computation, adding zero extra model evaluations: gᵢ[j] = (fwd[j][i] − bwd[j][i]) / (2h).
    /// </para>
    /// <para>
    /// At the MAP, the total score is zero: Σᵢ gᵢ + g_prior = 0. Each individual gᵢ is non-zero —
    /// they balance each other. Cook's Distance measures how much removing a component unbalances
    /// the score and shifts parameters.
    /// </para>
    /// <para>
    /// References:
    /// </para>
    /// <list type="bullet">
    /// <item><description>
    /// Cook, R. D. (1977). Detection of influential observation in linear regression. Technometrics, 19(1), 15-18.
    /// </description></item>
    /// <item><description>
    /// Marshall, E. C. and Spiegelhalter, D. J. (2025). Bayesian Measures of Leverage and Influence. arXiv:2503.19996.
    /// </description></item>
    /// <item><description>
    /// Wei, B. C., Hu, Y. Q., and Fung, W. K. (1998). Generalized leverage and its applications.
    /// Scandinavian Journal of Statistics, 25(1), 25-36.
    /// </description></item>
    /// </list>
    /// </remarks>
    public class LeverageDiagnostics
    {
        #region Construction

        /// <summary>
        /// Constructs an empty leverage diagnostics instance with no observations or prior components.
        /// </summary>
        public LeverageDiagnostics()
        {
            Observations = Array.Empty<ObservationLeverage>();
            PriorComponents = Array.Empty<PriorComponentLeverage>();
            NumberOfParameters = 0;
        }

        /// <summary>
        /// Constructs leverage diagnostics by computing the Hessian numerically at the given MAP parameter values.
        /// </summary>
        /// <param name="model">The model implementing IModel with log-likelihood functions.</param>
        /// <param name="mapValues">The MAP parameter values at which to evaluate the Hessian.</param>
        /// <exception cref="ArgumentNullException">Thrown when model or mapValues is null.</exception>
        /// <exception cref="ArgumentException">Thrown when mapValues length does not match model parameter count.</exception>
        /// <remarks>
        /// The Hessian of the full posterior log-likelihood (data + prior) is computed via central differences.
        /// No optimization is performed — the provided mapValues are used directly. This constructor is used
        /// by <see cref="Estimation.BayesianAnalysis.ComputeLeverageDiagnostics"/> which passes the MCMC MAP values.
        /// </remarks>
        public LeverageDiagnostics(IModel model, double[] mapValues)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));
            if (mapValues == null) throw new ArgumentNullException(nameof(mapValues));
            if (mapValues.Length != model.Parameters.Count)
                throw new ArgumentException($"mapValues length ({mapValues.Length}) must match model parameter count ({model.Parameters.Count}).", nameof(mapValues));

            Observations = Array.Empty<ObservationLeverage>();
            PriorComponents = Array.Empty<PriorComponentLeverage>();
            NumberOfParameters = mapValues.Length;
            ComputeLeverages(model, mapValues);
        }

        /// <summary>
        /// Constructs leverage diagnostics from pre-computed observation and prior component leverages.
        /// </summary>
        /// <param name="observations">The per-observation leverage values.</param>
        /// <param name="priorComponents">The per-prior-component leverage values.</param>
        /// <param name="numberOfParameters">The number of model parameters (p).</param>
        /// <remarks>
        /// Used by <see cref="Estimation.MaximumAPosteriori.ComputeLeverageDiagnostics"/> which already has
        /// the optimizer Hessian and computes leverages directly.
        /// </remarks>
        public LeverageDiagnostics(ObservationLeverage[] observations, PriorComponentLeverage[] priorComponents, int numberOfParameters)
        {
            Observations = observations ?? Array.Empty<ObservationLeverage>();
            PriorComponents = priorComponents ?? Array.Empty<PriorComponentLeverage>();
            NumberOfParameters = numberOfParameters;
            ComputeSummaryStatistics();
            UpdatePercentages();
        }

        /// <summary>
        /// Constructs leverage diagnostics from a serialized XML element.
        /// </summary>
        /// <param name="xElement">The XML element containing serialized leverage diagnostics.</param>
        public LeverageDiagnostics(XElement xElement)
        {
            Observations = Array.Empty<ObservationLeverage>();
            PriorComponents = Array.Empty<PriorComponentLeverage>();

            if (xElement == null) return;

            NumberOfParameters = int.TryParse(xElement.Attribute(nameof(NumberOfParameters))?.Value, out int p) ? p : 0;
            TotalObservationLeverage = double.TryParse(xElement.Attribute(nameof(TotalObservationLeverage))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tol) ? tol : 0;
            TotalPriorLeverage = double.TryParse(xElement.Attribute(nameof(TotalPriorLeverage))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tpl) ? tpl : 0;
            TotalLeverage = double.TryParse(xElement.Attribute(nameof(TotalLeverage))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tl) ? tl : 0;
            TotalFitInfluence = double.TryParse(xElement.Attribute(nameof(TotalFitInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tfi) ? tfi : 0;
            TotalVarianceInfluence = double.TryParse(xElement.Attribute(nameof(TotalVarianceInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double tvi) ? tvi : 0;

            var obsElements = xElement.Elements("Observation").ToList();
            Observations = new ObservationLeverage[obsElements.Count];
            for (int i = 0; i < obsElements.Count; i++)
                Observations[i] = new ObservationLeverage(obsElements[i]);

            var priorElements = xElement.Elements("PriorComponent").ToList();
            PriorComponents = new PriorComponentLeverage[priorElements.Count];
            for (int i = 0; i < priorElements.Count; i++)
                PriorComponents[i] = new PriorComponentLeverage(priorElements[i]);
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets the per-observation leverage values.
        /// </summary>
        public ObservationLeverage[] Observations { get; private set; }

        /// <summary>
        /// Gets the per-prior-component leverage values.
        /// </summary>
        public PriorComponentLeverage[] PriorComponents { get; private set; }

        /// <summary>
        /// Gets the number of fitted model parameters (p), used to scale the diagnostic quadratics.
        /// </summary>
        public int NumberOfParameters { get; private set; }

        /// <summary>
        /// Gets the total observation count.
        /// </summary>
        public int Count => Observations.Length;

        /// <summary>
        /// Gets the total leverage attributable to observations.
        /// </summary>
        public double TotalObservationLeverage { get; private set; }

        /// <summary>
        /// Gets the total leverage attributable to prior components.
        /// </summary>
        public double TotalPriorLeverage { get; private set; }

        /// <summary>
        /// Gets the total leverage across all components (observations + priors). Approximately equals p.
        /// </summary>
        public double TotalLeverage { get; private set; }

        /// <summary>
        /// Gets the total fit influence (Cook's Distance) across all observations.
        /// </summary>
        public double ObservationFitInfluence { get; private set; }

        /// <summary>
        /// Gets the total variance influence across all observations.
        /// </summary>
        public double ObservationVarianceInfluence { get; private set; }

        /// <summary>
        /// Gets the total fit influence across all prior components.
        /// </summary>
        public double PriorFitInfluence { get; private set; }

        /// <summary>
        /// Gets the total variance influence across all prior components.
        /// </summary>
        public double PriorVarianceInfluence { get; private set; }

        /// <summary>
        /// Gets the total fit influence across all components (observations + priors).
        /// </summary>
        public double TotalFitInfluence { get; private set; }

        /// <summary>
        /// Gets the total variance influence across all components (observations + priors).
        /// </summary>
        public double TotalVarianceInfluence { get; private set; }

        #endregion

        #region Methods

        /// <summary>
        /// Returns the most influential observations sorted by leverage in descending order.
        /// </summary>
        /// <param name="topN">The maximum number of observations to return.</param>
        /// <returns>An array of observation leverages sorted by descending leverage value.</returns>
        public ObservationLeverage[] GetMostInfluentialObservations(int topN)
        {
            return Observations
                .OrderByDescending(o => o.Leverage)
                .Take(Math.Min(topN, Observations.Length))
                .ToArray();
        }

        /// <summary>
        /// Returns a human-readable summary of the leverage diagnostics with fit/variance decomposition.
        /// </summary>
        /// <returns>A summary string describing the data/prior information split for both fit and variance.</returns>
        public string GetSummary()
        {
            if (TotalLeverage <= 0) return "No leverage diagnostics available.";

            double totalFV = TotalFitInfluence + TotalVarianceInfluence;
            if (totalFV <= 0) totalFV = TotalLeverage; // fallback

            double obsVarPct = totalFV > 0 ? ObservationVarianceInfluence / totalFV * 100.0 : 0;
            double obsFitPct = totalFV > 0 ? ObservationFitInfluence / totalFV * 100.0 : 0;
            double priorVarPct = totalFV > 0 ? PriorVarianceInfluence / totalFV * 100.0 : 0;
            double priorFitPct = totalFV > 0 ? PriorFitInfluence / totalFV * 100.0 : 0;

            return $"p = {NumberOfParameters}. Data: {obsVarPct:F1}% of variance influence, {obsFitPct:F1}% of fit influence. " +
                   $"Priors: {priorVarPct:F1}% of variance influence, {priorFitPct:F1}% of fit influence.";
        }

        /// <summary>
        /// Serializes the leverage diagnostics to an XML element.
        /// </summary>
        /// <returns>An XML element containing the serialized diagnostics.</returns>
        public XElement ToXElement()
        {
            var element = new XElement("LeverageDiagnostics",
                new XAttribute(nameof(NumberOfParameters), NumberOfParameters),
                new XAttribute(nameof(TotalObservationLeverage), TotalObservationLeverage.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(TotalPriorLeverage), TotalPriorLeverage.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(TotalLeverage), TotalLeverage.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(TotalFitInfluence), TotalFitInfluence.ToString(CultureInfo.InvariantCulture)),
                new XAttribute(nameof(TotalVarianceInfluence), TotalVarianceInfluence.ToString(CultureInfo.InvariantCulture)));

            foreach (var obs in Observations)
                element.Add(obs.ToXElement());

            foreach (var prior in PriorComponents)
                element.Add(prior.ToXElement());

            return element;
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Computes leverages from the model and MAP values using numerical Hessian via central differences.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="mapValues">The MAP parameter values.</param>
        private void ComputeLeverages(IModel model, double[] mapValues)
        {
            int p = mapValues.Length;

            try
            {
                // Step 1: Compute posterior Hessian via central differences on Model.LogLikelihood
                var hessian = ComputeNumericalHessian(model.LogLikelihood, mapValues, p);

                // Step 2: Invert the negative Hessian (Fisher information at MAP)
                Matrix negHessian = hessian * -1d;
                Matrix hessianInv;
                try
                {
                    hessianInv = negHessian.Inverse();
                }
                catch (Exception directEx)
                {
                    // Try regularization if direct inversion fails.
                    Debug.WriteLine($"LeverageDiagnostics: direct Hessian inversion failed, attempting regularization: {directEx.Message}");
                    try
                    {
                        negHessian = MatrixRegularization.MakeSymmetricPositiveDefinite(negHessian);
                        hessianInv = negHessian.Inverse();
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"Hessian inversion failed in LeverageDiagnostics: {ex.Message}");
                        Observations = Array.Empty<ObservationLeverage>();
                        PriorComponents = Array.Empty<PriorComponentLeverage>();
                        return;
                    }
                }

                // Step 3: Compute per-observation Cook's D and variance influence
                ComputeObservationLeverages(model, mapValues, p, hessianInv);

                // Step 4: Compute per-prior-component Cook's D and variance influence
                ComputePriorComponentLeverages(model, mapValues, p, hessianInv);

                // Step 5: Compute summary statistics and percentages
                ComputeSummaryStatistics();

                // Update percentages now that TotalLeverage is known
                UpdatePercentages();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LeverageDiagnostics computation failed: {ex.Message}");
                Observations = Array.Empty<ObservationLeverage>();
                PriorComponents = Array.Empty<PriorComponentLeverage>();
            }
        }

        /// <summary>
        /// Computes the numerical Hessian of a scalar function via central differences.
        /// </summary>
        /// <param name="function">The function to differentiate (e.g., Model.LogLikelihood).</param>
        /// <param name="parameters">The point at which to evaluate the Hessian.</param>
        /// <param name="p">The number of parameters.</param>
        /// <returns>The p × p Hessian matrix.</returns>
        /// <remarks>
        /// Uses the central difference formula:
        /// H[j,k] = (f(θ+hⱼeⱼ+hₖeₖ) - f(θ+hⱼeⱼ-hₖeₖ) - f(θ-hⱼeⱼ+hₖeₖ) + f(θ-hⱼeⱼ-hₖeₖ)) / (4hⱼhₖ)
        /// Step size h = max(|θⱼ| × 1e-4, 1e-3). The 1e-3 minimum is critical for distributions
        /// that use Normal approximations near zero (e.g., LP3 switches to Normal when |γ| &lt; 1e-4).
        /// </remarks>
        private static Matrix ComputeNumericalHessian(Func<double[], double> function, double[] parameters, int p)
        {
            return NumericalDiff.ComputeHessian(function, parameters, p);
        }

        /// <summary>
        /// Computes per-observation score vectors and their leverages.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="mapValues">The MAP parameter values.</param>
        /// <param name="p">The number of parameters.</param>
        /// <param name="hessianInv">The inverted negative Hessian (Fisher information inverse).</param>
        /// <remarks>
        /// Score vectors are computed via central differences on Model.PointwiseDataLogLikelihood,
        /// following the exact pattern in MaximumLikelihood.ComputePointwiseGradients.
        /// Leverage = gᵢᵀ H⁻¹ gᵢ (the quadratic form, same as Cook's D × p).
        /// </remarks>
        private void ComputeObservationLeverages(IModel model, double[] mapValues, int p, Matrix hessianInv)
        {
            double[] basePointwiseLL = model.PointwiseDataLogLikelihood(mapValues);
            int n = basePointwiseLL.Length;

            // Compute per-observation Hessians in bulk via central differences.
            // For each parameter pair (j,k), perturb θ once and get ALL n second derivatives.
            // Total: ~p² + p calls to PointwiseDataLogLikelihood (not n × p²).
            double[] perturbedParams = (double[])mapValues.Clone();
            var step = NumericalDiff.ComputeStepSizes(mapValues);

            // Cache forward/backward evaluations for diagonal terms,
            // with flat-spot detection and step escalation per parameter.
            var fwdVals = new double[p][];
            var bwdVals = new double[p][];
            for (int j = 0; j < p; j++)
            {
                double h = step[j];
                while (h <= NumericalDiff.MaxStep)
                {
                    perturbedParams[j] = mapValues[j] + h;
                    fwdVals[j] = model.PointwiseDataLogLikelihood(perturbedParams);
                    perturbedParams[j] = mapValues[j] - h;
                    bwdVals[j] = model.PointwiseDataLogLikelihood(perturbedParams);
                    perturbedParams[j] = mapValues[j];

                    // Check for flat spot across all observations
                    double totalDiff = 0;
                    double totalScale = 0;
                    for (int i = 0; i < n; i++)
                    {
                        totalDiff += Math.Abs(fwdVals[j][i] - bwdVals[j][i]);
                        totalScale += Math.Max(Math.Abs(fwdVals[j][i]), Math.Abs(bwdVals[j][i]));
                    }
                    if (totalDiff >= NumericalDiff.FlatSpotRelTol * Math.Max(totalScale, 1.0))
                        break;

                    h *= NumericalDiff.StepGrowthFactor;
                }
                step[j] = Math.Min(h, NumericalDiff.MaxStep);

                // If initial step > MaxStep, the loop never ran — compute with capped step
                if (fwdVals[j] == null || bwdVals[j] == null)
                {
                    perturbedParams[j] = mapValues[j] + step[j];
                    fwdVals[j] = model.PointwiseDataLogLikelihood(perturbedParams);
                    perturbedParams[j] = mapValues[j] - step[j];
                    bwdVals[j] = model.PointwiseDataLogLikelihood(perturbedParams);
                    perturbedParams[j] = mapValues[j];
                }
            }

            // Get data components for observation metadata
            List<DataComponent>? dataComponents = null;
            try
            {
                dataComponents = model.PointwiseDataLogLikelihoodComponents(mapValues);
            }
            catch (NotImplementedException) { /* Optional — not all models provide component metadata */ }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed to get data components for leverage diagnostics: {ex.Message}");
            }

            // Compute Cook's Distance and variance influence for each observation.
            // Fit Influence = gᵢᵀ J_post⁻¹ gᵢ / p  (Cook's Distance, score-based)
            // Variance Influence = |tr(J_post⁻¹ · Jᵢ)| / p  (trace method — accurate for individual observations)
            Observations = new ObservationLeverage[n];
            for (int i = 0; i < n; i++)
            {
                // Build Jᵢ (per-obs Fisher info) from cached perturbation data — diagonal only for trace
                var Ji = new Matrix(p, p);
                for (int j = 0; j < p; j++)
                    Ji[j, j] = -(fwdVals[j][i] - 2.0 * basePointwiseLL[i] + bwdVals[j][i]) / (step[j] * step[j]);

                // Variance Influence: |tr(J_post⁻¹ · Jᵢ)| / p
                double varianceInfluence = Math.Abs(TraceProduct(hessianInv, Ji, p)) / p;

                // Score vector: gᵢ[j] = (fwd[j][i] - bwd[j][i]) / (2h) — extracted from cached data
                var scoreVector = new double[p];
                for (int j = 0; j < p; j++)
                    scoreVector[j] = (fwdVals[j][i] - bwdVals[j][i]) / (2.0 * step[j]);

                // Fit Influence (Cook's D): gᵢᵀ J_post⁻¹ gᵢ / p
                double fitInfluence = p > 0 ? ComputeQuadraticForm(scoreVector, hessianInv, p) / p : 0;

                // Total leverage = fit + variance (combined effect)
                double leverage = fitInfluence + varianceInfluence;

                double value = 0;
                var dataType = DataComponentType.Exact;
                int count = 1;
                string? name = null;
                if (dataComponents != null && i < dataComponents.Count)
                {
                    value = dataComponents[i].Value;
                    dataType = dataComponents[i].Type;
                    count = dataComponents[i].Count;
                    name = dataComponents[i].Name;
                }

                Observations[i] = new ObservationLeverage(i, leverage, 0, fitInfluence, varianceInfluence, 0, 0,
                    value, dataType, count, name);
            }
        }

        /// <summary>
        /// Computes per-prior-component score vectors and their leverages.
        /// </summary>
        /// <param name="model">The model.</param>
        /// <param name="mapValues">The MAP parameter values.</param>
        /// <param name="p">The number of parameters.</param>
        /// <param name="hessianInv">The inverted negative Hessian (Fisher information inverse).</param>
        /// <remarks>
        /// For each parameter perturbation, calls Model.PointwisePriorLogLikelihood and matches
        /// components by name across forward/backward evaluations to compute gradients.
        /// </remarks>
        private void ComputePriorComponentLeverages(IModel model, double[] mapValues, int p, Matrix hessianInv)
        {
            // Get baseline prior components
            List<PriorComponent> baseComponents;
            try
            {
                baseComponents = model.PointwisePriorLogLikelihood(mapValues);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"LeverageDiagnostics.ComputePriorComponentLeverages baseline failed: {ex.Message}");
                PriorComponents = Array.Empty<PriorComponentLeverage>();
                return;
            }

            if (baseComponents == null || baseComponents.Count == 0)
            {
                PriorComponents = Array.Empty<PriorComponentLeverage>();
                return;
            }

            int numComponents = baseComponents.Count;

            // Compute per-prior-component Hessians and score vectors.
            // Variance Influence = |log(|det(Σ_{-k})| / |det(Σ)|)| / p  (generalized variance change)
            // Fit Influence = gₖᵀ J_post⁻¹ gₖ / p  (Cook's Distance for priors)
            PriorComponents = new PriorComponentLeverage[numComponents];
            for (int k = 0; k < numComponents; k++)
            {
                int capturedK = k;
                try
                {
                    // Scalar function for score vector: f(θ) = PointwisePriorLogLikelihood(θ)[k].LogLikelihood
                    Func<double[], double> priorFunc = theta =>
                    {
                        var comps = model.PointwisePriorLogLikelihood(theta);
                        return capturedK < comps.Count ? comps[capturedK].LogLikelihood : 0.0;
                    };

                    // Variance Influence (generalized variance): compute actual Hessian of the full
                    // log-likelihood with this prior removed, invert, compare determinants.
                    // GenVar is used for priors because they can contribute a large fraction of total
                    // information, making the linear trace approximation inaccurate.
                    Func<double[], double> llWithoutPrior = theta =>
                    {
                        double dataLL = model.DataLogLikelihood(theta);
                        var priorComps = model.PointwisePriorLogLikelihood(theta);
                        double priorLL = 0;
                        for (int idx = 0; idx < priorComps.Count; idx++)
                            if (idx != capturedK) priorLL += priorComps[idx].LogLikelihood;
                        return dataLL + priorLL;
                    };
                    double varianceInfluence = ComputeGeneralizedVarianceInfluence(llWithoutPrior, hessianInv, mapValues, p);

                    // Fit Influence (Cook's D): score vector via central differences
                    var scoreVector = new double[p];
                    double[] perturbedParams = (double[])mapValues.Clone();
                    for (int j = 0; j < p; j++)
                    {
                        double h = Math.Max(Math.Abs(mapValues[j]) * 1e-4, 1e-3);
                        perturbedParams[j] = mapValues[j] + h;
                        double fwd = priorFunc(perturbedParams);
                        perturbedParams[j] = mapValues[j] - h;
                        double bwd = priorFunc(perturbedParams);
                        scoreVector[j] = (fwd - bwd) / (2.0 * h);
                        perturbedParams[j] = mapValues[j];
                    }

                    double fitInfluence = p > 0 ? ComputeQuadraticForm(scoreVector, hessianInv, p) / p : 0;
                    double leverage = fitInfluence + varianceInfluence;

                    PriorComponents[k] = new PriorComponentLeverage(baseComponents[k].Name, baseComponents[k].Type,
                        leverage, 0, fitInfluence, varianceInfluence, 0, 0);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Prior component {k} leverage computation failed: {ex.Message}");
                    PriorComponents[k] = new PriorComponentLeverage(baseComponents[k].Name, baseComponents[k].Type,
                        0, 0, 0, 0, 0, 0);
                }
            }
        }

        /// <summary>
        /// Public accessor for <see cref="ComputeNumericalHessian"/> used by GMM penalty diagnostics.
        /// </summary>
        public static Matrix ComputeNumericalHessianPublic(Func<double[], double> function, double[] parameters, int p)
        {
            return ComputeNumericalHessian(function, parameters, p);
        }

        /// <summary>
        /// Public accessor for generalized variance influence computation, used by GMM penalty diagnostics.
        /// Computes |log(|det(Σ_{-k})| / |det(Σ)|)| / p by evaluating the reduced objective's Hessian.
        /// </summary>
        /// <param name="logLikelihoodWithout">Objective function with the component removed.</param>
        /// <param name="Sigma">Full posterior/GMM covariance.</param>
        /// <param name="mapValues">Parameter values at the optimum.</param>
        /// <param name="p">Number of parameters.</param>
        /// <returns>Generalized variance influence.</returns>
        public static double ComputeGenVarPublic(Func<double[], double> logLikelihoodWithout, Matrix Sigma,
            double[] mapValues, int p)
        {
            return ComputeGeneralizedVarianceInfluence(logLikelihoodWithout, Sigma, mapValues, p);
        }

        /// <summary>
        /// Computes tr(A · B) without forming the full matrix product.
        /// Used for Hessian trace decomposition: ℓ = tr(H_post⁻¹ · Hᵢ).
        /// </summary>
        /// <param name="A">First matrix (typically H_post⁻¹).</param>
        /// <param name="B">Second matrix (typically -Hᵢ or -Hₖ).</param>
        /// <param name="p">The dimension.</param>
        /// <returns>The trace of A·B.</returns>
        private static double TraceProduct(Matrix A, Matrix B, int p)
        {
            double trace = 0;
            for (int i = 0; i < p; i++)
                for (int j = 0; j < p; j++)
                    trace += A[i, j] * B[j, i];
            return trace;
        }

        /// <summary>
        /// Computes the variance influence of a prior component using the generalized variance (determinant).
        /// Evaluates the actual model log-likelihood with the component removed, computes the Hessian of
        /// that reduced function, inverts, and compares the determinant against the full posterior covariance.
        /// </summary>
        /// <param name="logLikelihoodWithout">A function that returns the log-likelihood with the component removed.</param>
        /// <param name="Sigma">The full posterior covariance (inverse of posterior Fisher information).</param>
        /// <param name="mapValues">The MAP parameter values.</param>
        /// <param name="p">Number of parameters.</param>
        /// <returns>The generalized variance influence: |log(|det(Sigma_{-k})| / |det(Sigma)|)| / p.</returns>
        /// <remarks>
        /// This method is used for prior components because priors can contribute substantial posterior curvature,
        /// making the linear trace approximation inaccurate. For individual
        /// observations (small perturbations), the trace method is used instead — see
        /// <see cref="ComputeObservationLeverages"/>.
        /// </remarks>
        private static double ComputeGeneralizedVarianceInfluence(
            Func<double[], double> logLikelihoodWithout, Matrix Sigma,
            double[] mapValues, int p)
        {
            try
            {
                double detSigma = Sigma.Determinant();
                double absDetSigma = Math.Abs(detSigma);

                // Compute Hessian of the reduced log-likelihood, negate, invert to get Sigma_{-k}
                var hessianReduced = ComputeNumericalHessian(logLikelihoodWithout, mapValues, p);
                Matrix negHessianReduced = hessianReduced * -1d;
                Matrix SigmaReduced;
                try
                {
                    SigmaReduced = negHessianReduced.Inverse();
                }
                catch
                {
                    negHessianReduced = MatrixRegularization.MakeSymmetricPositiveDefinite(negHessianReduced);
                    SigmaReduced = negHessianReduced.Inverse();
                }

                double detSigmaReduced = SigmaReduced.Determinant();
                double absDetReduced = Math.Abs(detSigmaReduced);

                // Guard against log(0): 1e-300 threshold is above double.MinValue (~5e-324)
                // but well below any physically meaningful determinant value.
                double result = 0;
                if (absDetSigma > 1e-300 && absDetReduced > 1e-300)
                {
                    result = Math.Abs(Math.Log(absDetReduced) - Math.Log(absDetSigma)) / p;
                }

                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"ComputeGeneralizedVarianceInfluence failed: {ex.Message}");
                return 0;
            }
        }


        /// <summary>
        /// Computes the quadratic form gT A g.
        /// </summary>
        /// <param name="g">The vector.</param>
        /// <param name="A">The matrix.</param>
        /// <param name="p">The dimension.</param>
        /// <returns>The scalar value gᵀ A g.</returns>
        private static double ComputeQuadraticForm(double[] g, Matrix A, int p)
        {
            double result = 0;
            for (int j = 0; j < p; j++)
            {
                double tmp = 0;
                for (int k = 0; k < p; k++)
                    tmp += A[j, k] * g[k];
                result += g[j] * tmp;
            }
            // Clamp to non-negative: the quadratic form gᵀAg should be non-negative for
            // positive-definite A, but numerical rounding can produce small negative values.
            return Math.Max(0, result);
        }

        /// <summary>
        /// Computes summary statistics from observation and prior component leverages.
        /// </summary>
        private void ComputeSummaryStatistics()
        {
            TotalObservationLeverage = Observations.Sum(o => o.Leverage);
            TotalPriorLeverage = PriorComponents.Sum(pc => pc.Leverage);
            TotalLeverage = TotalObservationLeverage + TotalPriorLeverage;

            ObservationFitInfluence = Observations.Sum(o => o.FitInfluence);
            ObservationVarianceInfluence = Observations.Sum(o => o.VarianceInfluence);
            PriorFitInfluence = PriorComponents.Sum(pc => pc.FitInfluence);
            PriorVarianceInfluence = PriorComponents.Sum(pc => pc.VarianceInfluence);
            TotalFitInfluence = ObservationFitInfluence + PriorFitInfluence;
            TotalVarianceInfluence = ObservationVarianceInfluence + PriorVarianceInfluence;

        }

        /// <summary>
        /// Updates percentage values on observations and prior components after totals are known.
        /// </summary>
        private void UpdatePercentages()
        {
            if (TotalLeverage <= 0) return;

            double totalFV = TotalFitInfluence + TotalVarianceInfluence;
            if (totalFV <= 0) totalFV = TotalLeverage; // fallback for pre-computed leverages without fit/variance

            for (int i = 0; i < Observations.Length; i++)
            {
                var obs = Observations[i];
                Observations[i] = new ObservationLeverage(
                    obs.Index, obs.Leverage, obs.Leverage / TotalLeverage * 100.0,
                    obs.FitInfluence, obs.VarianceInfluence,
                    totalFV > 0 ? obs.FitInfluence / totalFV * 100.0 : 0,
                    totalFV > 0 ? obs.VarianceInfluence / totalFV * 100.0 : 0,
                    obs.Value, obs.DataType, obs.Count, obs.Name);
            }

            for (int i = 0; i < PriorComponents.Length; i++)
            {
                var pc = PriorComponents[i];
                PriorComponents[i] = new PriorComponentLeverage(
                    pc.Name, pc.Type, pc.Leverage, pc.Leverage / TotalLeverage * 100.0,
                    pc.FitInfluence, pc.VarianceInfluence,
                    totalFV > 0 ? pc.FitInfluence / totalFV * 100.0 : 0,
                    totalFV > 0 ? pc.VarianceInfluence / totalFV * 100.0 : 0);
            }
        }

        #endregion

        #region Structs

        /// <summary>
        /// Represents the influence of a single observation decomposed into fit and variance components.
        /// </summary>
        public readonly struct ObservationLeverage
        {
            /// <summary>
            /// Creates a new observation leverage entry with fit/variance decomposition.
            /// </summary>
            /// <param name="index">The zero-based observation index.</param>
            /// <param name="leverage">The total leverage (FitInfluence + VarianceInfluence).</param>
            /// <param name="percentOfTotal">The leverage as a percentage of total combined influence.</param>
            /// <param name="fitInfluence">Cook's Distance: gᵢᵀ J⁻¹ gᵢ / p.</param>
            /// <param name="varianceInfluence">Observation variance influence: tr(J⁻¹ Jᵢ) / p.</param>
            /// <param name="percentFitOfTotal">Fit influence as a percentage of total (fit + variance).</param>
            /// <param name="percentVarianceOfTotal">Variance influence as a percentage of total (fit + variance).</param>
            /// <param name="value">The representative data value.</param>
            /// <param name="dataType">The type of observation.</param>
            /// <param name="count">The count (typically 1, higher for threshold data).</param>
            /// <param name="name">An optional label for the observation.</param>
            public ObservationLeverage(int index, double leverage, double percentOfTotal,
                double fitInfluence, double varianceInfluence,
                double percentFitOfTotal, double percentVarianceOfTotal,
                double value, DataComponentType dataType, int count = 1, string? name = null)
            {
                Index = index;
                Leverage = leverage;
                PercentOfTotal = percentOfTotal;
                FitInfluence = fitInfluence;
                VarianceInfluence = varianceInfluence;
                PercentFitOfTotal = percentFitOfTotal;
                PercentVarianceOfTotal = percentVarianceOfTotal;
                Value = value;
                DataType = dataType;
                Count = count;
                Name = name;
            }

            /// <summary>
            /// Creates an observation leverage from a serialized XML element.
            /// </summary>
            /// <param name="xElement">The XML element.</param>
            public ObservationLeverage(XElement xElement)
            {
                Index = int.TryParse(xElement.Attribute(nameof(Index))?.Value, out int idx) ? idx : 0;
                Leverage = double.TryParse(xElement.Attribute(nameof(Leverage))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lev) ? lev : 0;
                PercentOfTotal = double.TryParse(xElement.Attribute(nameof(PercentOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct) ? pct : 0;
                FitInfluence = double.TryParse(xElement.Attribute(nameof(FitInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fi) ? fi : 0;
                VarianceInfluence = double.TryParse(xElement.Attribute(nameof(VarianceInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double vi) ? vi : 0;
                PercentFitOfTotal = double.TryParse(xElement.Attribute(nameof(PercentFitOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pfi) ? pfi : 0;
                PercentVarianceOfTotal = double.TryParse(xElement.Attribute(nameof(PercentVarianceOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pvi) ? pvi : 0;
                Value = double.TryParse(xElement.Attribute(nameof(Value))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double val) ? val : 0;
                Enum.TryParse(xElement.Attribute(nameof(DataType))?.Value, out DataComponentType dataType);
                DataType = dataType;
                Count = int.TryParse(xElement.Attribute(nameof(Count))?.Value, out int cnt) ? cnt : 1;
                Name = xElement.Attribute(nameof(Name))?.Value;
            }

            /// <summary>
            /// Gets the zero-based observation index.
            /// </summary>
            public int Index { get; }

            /// <summary>
            /// Gets the total leverage (FitInfluence + VarianceInfluence).
            /// </summary>
            public double Leverage { get; }

            /// <summary>
            /// Gets the leverage as a percentage of total combined influence.
            /// </summary>
            public double PercentOfTotal { get; }

            /// <summary>
            /// Gets the fit influence (Cook's Distance): gᵢᵀ J⁻¹_post gᵢ / p.
            /// Measures how much removing this observation shifts the MAP parameters.
            /// Zero when the observation is perfectly consistent with the model.
            /// </summary>
            public double FitInfluence { get; }

            /// <summary>
            /// Gets the variance influence (normalized leverage): tr(J⁻¹_post Jᵢ) / p.
            /// Measures this observation's contribution to posterior precision.
            /// Always non-negative. Large for threshold data with many counts.
            /// </summary>
            public double VarianceInfluence { get; }

            /// <summary>
            /// Gets the fit influence as a percentage of total (fit + variance) across all components.
            /// </summary>
            public double PercentFitOfTotal { get; }

            /// <summary>
            /// Gets the variance influence as a percentage of total (fit + variance) across all components.
            /// </summary>
            public double PercentVarianceOfTotal { get; }

            /// <summary>
            /// Gets the representative data value.
            /// </summary>
            public double Value { get; }

            /// <summary>
            /// Gets the type of observation.
            /// </summary>
            public DataComponentType DataType { get; }

            /// <summary>
            /// Gets the count (typically 1, higher for threshold data).
            /// </summary>
            public int Count { get; }

            /// <summary>
            /// Gets the optional observation label.
            /// </summary>
            public string? Name { get; }

            /// <summary>
            /// Serializes this observation leverage to an XML element.
            /// </summary>
            /// <returns>An XML element containing the serialized values.</returns>
            public XElement ToXElement()
            {
                var el = new XElement("Observation",
                    new XAttribute(nameof(Index), Index),
                    new XAttribute(nameof(Leverage), Leverage.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentOfTotal), PercentOfTotal.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(FitInfluence), FitInfluence.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(VarianceInfluence), VarianceInfluence.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentFitOfTotal), PercentFitOfTotal.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentVarianceOfTotal), PercentVarianceOfTotal.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(Value), Value.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(DataType), DataType),
                    new XAttribute(nameof(Count), Count));
                if (Name != null)
                    el.Add(new XAttribute(nameof(Name), Name));
                return el;
            }
        }

        /// <summary>
        /// Represents the influence of a single prior component decomposed into fit and variance components.
        /// </summary>
        public readonly struct PriorComponentLeverage
        {
            /// <summary>
            /// Creates a new prior component leverage entry with fit/variance decomposition.
            /// </summary>
            /// <param name="name">The prior component name.</param>
            /// <param name="type">The prior component type.</param>
            /// <param name="leverage">The total leverage (FitInfluence + VarianceInfluence).</param>
            /// <param name="percentOfTotal">The leverage as a percentage of total combined influence.</param>
            /// <param name="fitInfluence">Cook's Distance for this prior component.</param>
            /// <param name="varianceInfluence">Normalized leverage for this prior component.</param>
            /// <param name="percentFitOfTotal">Fit influence as a percentage of total.</param>
            /// <param name="percentVarianceOfTotal">Variance influence as a percentage of total.</param>
            public PriorComponentLeverage(string name, PriorComponentType type,
                double leverage, double percentOfTotal,
                double fitInfluence, double varianceInfluence,
                double percentFitOfTotal, double percentVarianceOfTotal)
            {
                Name = name;
                Type = type;
                Leverage = leverage;
                PercentOfTotal = percentOfTotal;
                FitInfluence = fitInfluence;
                VarianceInfluence = varianceInfluence;
                PercentFitOfTotal = percentFitOfTotal;
                PercentVarianceOfTotal = percentVarianceOfTotal;
            }

            /// <summary>
            /// Creates a prior component leverage from a serialized XML element.
            /// </summary>
            /// <param name="xElement">The XML element.</param>
            public PriorComponentLeverage(XElement xElement)
            {
                Name = xElement.Attribute(nameof(Name))?.Value ?? "";
                Enum.TryParse(xElement.Attribute(nameof(Type))?.Value, out PriorComponentType type);
                Type = type;
                Leverage = double.TryParse(xElement.Attribute(nameof(Leverage))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double lev) ? lev : 0;
                PercentOfTotal = double.TryParse(xElement.Attribute(nameof(PercentOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pct) ? pct : 0;
                FitInfluence = double.TryParse(xElement.Attribute(nameof(FitInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double fi) ? fi : 0;
                VarianceInfluence = double.TryParse(xElement.Attribute(nameof(VarianceInfluence))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double vi) ? vi : 0;
                PercentFitOfTotal = double.TryParse(xElement.Attribute(nameof(PercentFitOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pfi) ? pfi : 0;
                PercentVarianceOfTotal = double.TryParse(xElement.Attribute(nameof(PercentVarianceOfTotal))?.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out double pvi) ? pvi : 0;
            }

            /// <summary>
            /// Gets the prior component name.
            /// </summary>
            public string Name { get; }

            /// <summary>
            /// Gets the prior component type.
            /// </summary>
            public PriorComponentType Type { get; }

            /// <summary>
            /// Gets the total leverage (FitInfluence + VarianceInfluence).
            /// </summary>
            public double Leverage { get; }

            /// <summary>
            /// Gets the leverage as a percentage of total combined influence.
            /// </summary>
            public double PercentOfTotal { get; }

            /// <summary>
            /// Gets the fit influence (Cook's Distance) for this prior component.
            /// Measures how much this prior shifts the MAP parameters.
            /// Near zero when the prior is centered at the data-driven mode.
            /// </summary>
            public double FitInfluence { get; }

            /// <summary>
            /// Gets the variance influence (normalized leverage) for this prior component.
            /// Measures this prior's contribution to posterior precision.
            /// </summary>
            public double VarianceInfluence { get; }

            /// <summary>
            /// Gets the fit influence as a percentage of total (fit + variance) across all components.
            /// </summary>
            public double PercentFitOfTotal { get; }

            /// <summary>
            /// Gets the variance influence as a percentage of total (fit + variance) across all components.
            /// </summary>
            public double PercentVarianceOfTotal { get; }

            /// <summary>
            /// Serializes this prior component leverage to an XML element.
            /// </summary>
            /// <returns>An XML element containing the serialized values.</returns>
            public XElement ToXElement()
            {
                return new XElement("PriorComponent",
                    new XAttribute(nameof(Name), Name),
                    new XAttribute(nameof(Type), Type),
                    new XAttribute(nameof(Leverage), Leverage.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentOfTotal), PercentOfTotal.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(FitInfluence), FitInfluence.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(VarianceInfluence), VarianceInfluence.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentFitOfTotal), PercentFitOfTotal.ToString(CultureInfo.InvariantCulture)),
                    new XAttribute(nameof(PercentVarianceOfTotal), PercentVarianceOfTotal.ToString(CultureInfo.InvariantCulture)));
            }
        }

        #endregion
    }
}
