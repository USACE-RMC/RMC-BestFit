using Numerics.Data;
using RMC.BestFit.Models;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.Datasets
{
    /// <summary>
    /// Synthetic rating curve data generator for unit testing.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Generates stage-discharge pairs using the BaRatin addition-mode rating curve
    /// (Le Coz et al. 2014): Q(h) = Σₖ αₖ · (h − ξₖ)^βₖ · 𝟙{h &gt; ξₖ} in real space,
    /// with multiplicative log-space noise Q · 10^ε, ε ~ N(0, σ²).
    /// </para>
    /// <para>
    /// Multi-segment generators exercise the compound-channel overbank use case
    /// where the main channel continues carrying flow above bankfull and additional
    /// controls (overbank floodplain, etc.) activate and contribute additively.
    /// </para>
    /// <para>
    /// Parameter layout for the BestFitRatingCurve model:
    /// <list type="bullet">
    /// <item>1 segment: [h₁, log₁₀(α₁), β₁, σ]</item>
    /// <item>2 segments: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, σ]</item>
    /// <item>3 segments: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, h₃, log₁₀(α₃), β₃, σ]</item>
    /// </list>
    /// Under BaRatin addition mode the breakpoint hₖ is both the activation stage of
    /// control k and the "b" offset of its power-law term — continuity is automatic.
    /// </para>
    /// </remarks>
    public static class SyntheticRatingCurveData
    {
        #region Single Segment Rating Curves

        /// <summary>
        /// Generates synthetic single-segment rating curve data with known parameters.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Uses <c>GenerateSyntheticData</c> internally to generate
        /// stage-discharge pairs from the rating curve model.
        /// </para>
        /// </remarks>
        /// <param name="xi">Zero-flow stage (gauge datum offset). Default = 0.5.</param>
        /// <param name="alpha">Coefficient (in real space, will be stored as log10). Default = 10.0.</param>
        /// <param name="beta">Exponent (typically 1.5-2.5 for open channel flow). Default = 2.0.</param>
        /// <param name="sigma">Log10-space standard deviation. Default = 0.05.</param>
        /// <param name="minStage">Minimum stage value. Default = 1.0.</param>
        /// <param name="maxStage">Maximum stage value. Default = 10.0.</param>
        /// <param name="sampleSize">Number of observations. Default = 200.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 12345.</param>
        /// <returns>
        /// A tuple containing:
        /// - StageData: TimeSeries of stage values
        /// - DischargeData: TimeSeries of discharge values
        /// - TrueParameters: Array [ξ, log10(α), β, σ]
        /// </returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetSingleSegmentData(
            double xi = 0.5,
            double alpha = 10.0,
            double beta = 2.0,
            double sigma = 0.05,
            double minStage = 1.0,
            double maxStage = 10.0,
            int sampleSize = 200,
            int seed = 12345)
        {
            // True parameters in model format: [ξ, log10(α), β, σ]
            var trueParameters = new double[] { xi, Math.Log10(alpha), beta, sigma };

            // Create a rating curve model and set parameters
            var model = new BestFitRatingCurve { UseDefaultFlatPriors = false };
            model.SetParameterValues(trueParameters);

            // Generate synthetic data using the model
            var (stageTS, dischargeTS) = model.GenerateSyntheticData(sampleSize, minStage, maxStage, seed);

            return (stageTS, dischargeTS, trueParameters);
        }

        /// <summary>
        /// Generates synthetic rating curve data with low noise for testing parameter recovery.
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 500.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 54321.</param>
        /// <returns>Stage and discharge time series with true parameters [ξ, log10(α), β, σ].</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetLowNoiseData(
            int sampleSize = 500,
            int seed = 54321)
        {
            return GetSingleSegmentData(
                xi: 0.3,
                alpha: 15.0,
                beta: 1.8,
                sigma: 0.02,
                minStage: 1.0,
                maxStage: 12.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        /// <summary>
        /// Generates synthetic rating curve data with high noise for robustness testing.
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 500.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 67890.</param>
        /// <returns>Stage and discharge time series with true parameters [ξ, log10(α), β, σ].</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetHighNoiseData(
            int sampleSize = 500,
            int seed = 67890)
        {
            return GetSingleSegmentData(
                xi: 0.5,
                alpha: 8.0,
                beta: 2.2,
                sigma: 0.15,
                minStage: 1.0,
                maxStage: 10.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        /// <summary>
        /// Generates synthetic rating curve data typical of steep mountain streams (high exponent).
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 300.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 11111.</param>
        /// <returns>Stage and discharge time series with true parameters [ξ, log10(α), β, σ].</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetSteepChannelData(
            int sampleSize = 300,
            int seed = 11111)
        {
            return GetSingleSegmentData(
                xi: 0.2,
                alpha: 5.0,
                beta: 2.8,
                sigma: 0.06,
                minStage: 0.5,
                maxStage: 6.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        /// <summary>
        /// Generates synthetic rating curve data typical of wide floodplains (low exponent).
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 300.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 22222.</param>
        /// <returns>Stage and discharge time series with true parameters [ξ, log10(α), β, σ].</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetWideChannelData(
            int sampleSize = 300,
            int seed = 22222)
        {
            return GetSingleSegmentData(
                xi: 0.0,
                alpha: 50.0,
                beta: 1.5,
                sigma: 0.04,
                minStage: 1.0,
                maxStage: 15.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        #endregion

        #region Two Segment Rating Curves

        /// <summary>
        /// Generates synthetic two-segment rating curve data with known parameters using
        /// the BaRatin addition-mode parameterization: control 1 is the main channel
        /// active at all stages; control 2 activates at h₂ and ADDS its contribution
        /// on top of the continuing main-channel flow.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Parameter order: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, σ].
        /// Discharge model: Q(h) = α₁·(h − h₁)^β₁ + α₂·(h − h₂)^β₂·𝟙{h &gt; h₂}.
        /// </para>
        /// <para>
        /// Default α₂, β₂ are the hand-computable Manning-physics values for a
        /// 20-ft-wide rectangular overbank at n_ob = 0.035, S = 0.05:
        /// α₂ = (1.49/n_ob)·W_ob·S^(1/2) ≈ 190.4, β₂ = 5/3 ≈ 1.667.
        /// This makes parameter recovery meaningful: the MLE/MCMC should recover these
        /// values, and deviation indicates model misspecification or identifiability
        /// problems.
        /// </para>
        /// </remarks>
        /// <param name="h1">Main-channel zero-flow stage. Default = 0.5.</param>
        /// <param name="alpha1">Main-channel coefficient. Default = 10.0.</param>
        /// <param name="beta1">Main-channel exponent. Default = 2.0.</param>
        /// <param name="h2">Activation stage of the second control (e.g., bankfull). Default = 5.0.</param>
        /// <param name="alpha2">Second-control coefficient. Default = 190.4 (Manning overbank).</param>
        /// <param name="beta2">Second-control exponent. Default = 1.667 (≈ 5/3).</param>
        /// <param name="sigma">Log10-space standard deviation. Default = 0.05.</param>
        /// <param name="minStage">Minimum stage value. Default = 0.7.</param>
        /// <param name="maxStage">Maximum stage value. Default = 10.0.</param>
        /// <param name="sampleSize">Number of observations. Default = 300.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 33333.</param>
        /// <returns>
        /// A tuple containing:
        /// - StageData: TimeSeries of stage values
        /// - DischargeData: TimeSeries of discharge values
        /// - TrueParameters: Array [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, σ]
        /// </returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetTwoSegmentData(
            double h1 = 0.5,
            double alpha1 = 10.0,
            double beta1 = 2.0,
            double h2 = 5.0,
            double alpha2 = 190.4,  // Manning overbank: (1.49/0.035)·20·√0.05
            double beta2 = 1.6667,  // 5/3 (wide rectangular overbank)
            double sigma = 0.05,
            double minStage = 0.7,  // Close to h₁ for better identifiability
            double maxStage = 10.0,
            int sampleSize = 300,
            int seed = 33333)
        {
            // True parameters: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, σ]
            var trueParameters = new double[]
            {
                h1,
                Math.Log10(alpha1),
                beta1,
                h2,
                Math.Log10(alpha2),
                beta2,
                sigma
            };

            // Create a two-segment rating curve model and set parameters
            var model = new BestFitRatingCurve { NumberOfSegments = 2, UseDefaultFlatPriors = false };
            model.SetParameterValues(trueParameters);

            // Generate synthetic data using the model
            var (stageTS, dischargeTS) = model.GenerateSyntheticData(sampleSize, minStage, maxStage, seed);

            return (stageTS, dischargeTS, trueParameters);
        }

        /// <summary>
        /// Generates two-segment data simulating an in-bank-to-overbank flow transition
        /// (compound channel, the dominant flood-frequency use case) under the BaRatin
        /// addition-mode parameterization.
        /// </summary>
        /// <remarks>
        /// Scenario: triangular main channel (n=0.035, S=0.05) with 20-ft-wide
        /// rectangular overbank (n_ob=0.035, S=0.05). The overbank α₂ ≈ 190.4 and
        /// β₂ = 5/3 are hand-computed from Manning's equation, so parameter recovery
        /// tests can assert against exact physical values.
        /// </remarks>
        /// <param name="sampleSize">Number of observations. Default = 400.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 44444.</param>
        /// <returns>Stage and discharge time series with true parameters.</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetBankfullTransitionData(
            int sampleSize = 400,
            int seed = 44444)
        {
            return GetTwoSegmentData(
                h1: 0.3,
                alpha1: 12.0,   // Main-channel coefficient (triangular cross-section)
                beta1: 2.2,     // Main-channel exponent (depth-dominated geometry)
                h2: 6.0,        // Bankfull stage (overbank activates here)
                alpha2: 190.4,  // Overbank: (1.49/0.035)·20·√0.05 from Manning wide-rectangle
                beta2: 1.6667,  // Overbank: 5/3 exactly
                sigma: 0.04,
                minStage: 0.5,  // Close to h₁ for better identifiability
                maxStage: 12.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        #endregion

        #region Three Segment Rating Curves

        /// <summary>
        /// Generates synthetic three-segment rating curve data with known parameters using
        /// the BaRatin addition-mode parameterization: control 1 is the main channel
        /// active at all stages; controls 2 and 3 activate at h₂ and h₃ respectively
        /// and each ADDS its contribution on top of the already-active controls.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Parameter order: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, h₃, log₁₀(α₃), β₃, σ].
        /// Discharge model:
        /// Q(h) = α₁·(h − h₁)^β₁ + α₂·(h − h₂)^β₂·𝟙{h &gt; h₂} + α₃·(h − h₃)^β₃·𝟙{h &gt; h₃}.
        /// </para>
        /// <para>
        /// A typical physical scenario: main channel + inner floodplain + outer
        /// floodplain (three controls, each adding their Manning contribution as stage
        /// rises onto successive floodplain terraces).
        /// </para>
        /// </remarks>
        /// <param name="h1">Main-channel zero-flow stage. Default = 0.3.</param>
        /// <param name="alpha1">Main-channel coefficient. Default = 8.0.</param>
        /// <param name="beta1">Main-channel exponent. Default = 2.2.</param>
        /// <param name="h2">Activation stage of the second control. Default = 4.0.</param>
        /// <param name="alpha2">Second-control coefficient. Default = 100.0.</param>
        /// <param name="beta2">Second-control exponent. Default = 1.667 (≈ 5/3).</param>
        /// <param name="h3">Activation stage of the third control. Default = 8.0.</param>
        /// <param name="alpha3">Third-control coefficient. Default = 200.0.</param>
        /// <param name="beta3">Third-control exponent. Default = 1.667 (≈ 5/3).</param>
        /// <param name="sigma">Log10-space standard deviation. Default = 0.05.</param>
        /// <param name="minStage">Minimum stage value. Default = 0.5.</param>
        /// <param name="maxStage">Maximum stage value. Default = 12.0.</param>
        /// <param name="sampleSize">Number of observations. Default = 500.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 55555.</param>
        /// <returns>
        /// A tuple containing:
        /// - StageData: TimeSeries of stage values
        /// - DischargeData: TimeSeries of discharge values
        /// - TrueParameters: Array [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, h₃, log₁₀(α₃), β₃, σ]
        /// </returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetThreeSegmentData(
            double h1 = 0.3,
            double alpha1 = 8.0,
            double beta1 = 2.2,
            double h2 = 4.0,
            double alpha2 = 100.0,   // Inner floodplain (plausible Manning coefficient)
            double beta2 = 1.6667,   // 5/3 for wide-rectangular approximation
            double h3 = 8.0,
            double alpha3 = 200.0,   // Outer/main floodplain
            double beta3 = 1.6667,   // 5/3
            double sigma = 0.05,
            double minStage = 0.5,   // Close to h₁ for better identifiability
            double maxStage = 12.0,
            int sampleSize = 500,
            int seed = 55555)
        {
            // True parameters: [h₁, log₁₀(α₁), β₁, h₂, log₁₀(α₂), β₂, h₃, log₁₀(α₃), β₃, σ]
            var trueParameters = new double[]
            {
                h1,
                Math.Log10(alpha1),
                beta1,
                h2,
                Math.Log10(alpha2),
                beta2,
                h3,
                Math.Log10(alpha3),
                beta3,
                sigma
            };

            // Create a three-segment rating curve model and set parameters
            var model = new BestFitRatingCurve { NumberOfSegments = 3, UseDefaultFlatPriors = false };
            model.SetParameterValues(trueParameters);

            // Generate synthetic data using the model
            var (stageTS, dischargeTS) = model.GenerateSyntheticData(sampleSize, minStage, maxStage, seed);

            return (stageTS, dischargeTS, trueParameters);
        }

        /// <summary>
        /// Generates three-segment data simulating a low-flow channel, main channel,
        /// and overbank floodplain — three hydraulic controls that successively
        /// activate and add their Manning contributions as stage rises. Uses the
        /// BaRatin addition-mode parameterization.
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 600.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 66666.</param>
        /// <returns>Stage and discharge time series with true parameters.</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetMultipleControlData(
            int sampleSize = 600,
            int seed = 66666)
        {
            return GetThreeSegmentData(
                h1: 0.2,
                alpha1: 5.0,     // Low-flow channel: narrow, steep, lower capacity
                beta1: 2.5,
                h2: 3.0,         // Main channel activates (adds to low-flow)
                alpha2: 50.0,    // Main-channel Manning coefficient (plausible)
                beta2: 1.6667,   // 5/3 main-channel rectangular approximation
                h3: 7.0,         // Overbank floodplain activates (adds to main)
                alpha3: 150.0,   // Overbank wide-rectangular coefficient
                beta3: 1.6667,   // 5/3
                sigma: 0.04,
                minStage: 0.5,
                maxStage: 10.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        #endregion

        #region Special Cases

        /// <summary>
        /// Generates data with exact (no noise) observations for deterministic testing.
        /// </summary>
        /// <remarks>
        /// <para>
        /// Unlike other methods that generate randomly distributed stages, this method
        /// generates evenly spaced stages for deterministic testing. Uses
        /// <c>Predict</c> for discharge calculation.
        /// </para>
        /// </remarks>
        /// <param name="xi">Zero-flow stage. Default = 0.5.</param>
        /// <param name="alpha">Coefficient. Default = 10.0.</param>
        /// <param name="beta">Exponent. Default = 2.0.</param>
        /// <param name="minStage">Minimum stage value. Default = 1.0.</param>
        /// <param name="maxStage">Maximum stage value. Default = 10.0.</param>
        /// <param name="sampleSize">Number of observations. Default = 100.</param>
        /// <returns>Stage and discharge time series with true parameters (σ = 0).</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetExactData(
            double xi = 0.5,
            double alpha = 10.0,
            double beta = 2.0,
            double minStage = 1.0,
            double maxStage = 10.0,
            int sampleSize = 100)
        {
            // True parameters with σ = 0 (no noise)
            var trueParameters = new double[] { xi, Math.Log10(alpha), beta, 0.0 };

            // Create a rating curve model and set parameters
            var model = new BestFitRatingCurve { UseDefaultFlatPriors = false };
            model.SetParameterValues(trueParameters);

            var stages = new double[sampleSize];
            var discharges = new double[sampleSize];

            // Generate evenly spaced stages (not random)
            double step = (maxStage - minStage) / (sampleSize - 1);
            for (int i = 0; i < sampleSize; i++)
            {
                stages[i] = minStage + i * step;
                // Use BestFitRatingCurve.Predict for consistent discharge calculation (no noise)
                discharges[i] = model.Predict(trueParameters, stages[i]);
            }

            var startDate = new DateTime(2000, 1, 1);
            var stageTS = new TimeSeries(TimeInterval.OneDay, startDate, stages);
            var dischargeTS = new TimeSeries(TimeInterval.OneDay, startDate, discharges);

            return (stageTS, dischargeTS, trueParameters);
        }

        /// <summary>
        /// Generates minimal data (15 observations) for edge case testing.
        /// </summary>
        /// <param name="seed">Random seed for reproducibility. Default = 77777.</param>
        /// <returns>Stage and discharge time series with true parameters.</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetMinimalData(
            int seed = 77777)
        {
            return GetSingleSegmentData(
                xi: 0.5,
                alpha: 10.0,
                beta: 2.0,
                sigma: 0.03,
                minStage: 1.0,
                maxStage: 8.0,
                sampleSize: 15,
                seed: seed);
        }

        /// <summary>
        /// Generates data with large sample size for convergence testing.
        /// </summary>
        /// <param name="seed">Random seed for reproducibility. Default = 88888.</param>
        /// <returns>Stage and discharge time series with true parameters.</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetLargeSampleData(
            int seed = 88888)
        {
            return GetSingleSegmentData(
                xi: 0.4,
                alpha: 12.0,
                beta: 1.9,
                sigma: 0.04,
                minStage: 1.0,
                maxStage: 15.0,
                sampleSize: 1000,
                seed: seed);
        }

        /// <summary>
        /// Generates data with wide stage range for extrapolation testing.
        /// </summary>
        /// <param name="sampleSize">Number of observations. Default = 400.</param>
        /// <param name="seed">Random seed for reproducibility. Default = 99999.</param>
        /// <returns>Stage and discharge time series with true parameters.</returns>
        public static (TimeSeries StageData, TimeSeries DischargeData, double[] TrueParameters) GetWideRangeData(
            int sampleSize = 400,
            int seed = 99999)
        {
            return GetSingleSegmentData(
                xi: 0.0,
                alpha: 5.0,
                beta: 2.0,
                sigma: 0.05,
                minStage: 1.0,
                maxStage: 25.0,
                sampleSize: sampleSize,
                seed: seed);
        }

        #endregion
    }
}
