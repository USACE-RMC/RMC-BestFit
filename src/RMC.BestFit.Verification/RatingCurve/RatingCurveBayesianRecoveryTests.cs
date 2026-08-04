using Numerics;
using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using System.Xml.Linq;
using BestFitRatingCurve = RMC.BestFit.Models.RatingCurve;

namespace RMC.BestFit.Verification.RatingCurve;

/// <summary>
/// Verifies Bayesian MCMC parameter recovery for rating-curve models against
/// deterministic synthetic generating parameters.
/// </summary>
[TestClass]
public class RatingCurveBayesianRecoveryTests
{
    #region Parameter Recovery Tests - Single Segment

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve parameters against known true values.
    /// Uses low-noise synthetic data for accurate parameter recovery.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The rating curve model: Q = α * (h - ξ)^β with log-normal error σ.
    /// Parameters: [ξ, log10(α), β, σ] = [0.3, log10(15), 1.8, 0.02]
    /// </para>
    /// <para>
    /// Low noise (σ=0.02) enables 15% tolerance on Bayesian parameter recovery.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_LowNoise()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetLowNoiseData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.15));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve with default parameters.
    /// Uses standard synthetic data with moderate noise.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Default parameters: [ξ, log10(α), β, σ] = [0.5, log10(10), 2.0, 0.05]
    /// </para>
    /// <para>
    /// Moderate noise (σ=0.05) requires 20% tolerance for Bayesian estimation.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetSingleSegmentData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve for steep channel (mountain stream).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Steep channel parameters: [ξ, log10(α), β, σ] = [0.1, log10(5), 2.8, 0.05]
    /// High exponent (β=2.8) represents steep mountain stream.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_SteepChannel()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetSteepChannelData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of single-segment rating curve for wide channel (floodplain).
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wide channel parameters: [ξ, log10(α), β, σ] = [0.2, log10(50), 1.3, 0.05]
    /// Low exponent (β=1.3) represents wide floodplain.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_WideChannel()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetWideChannelData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with large sample size for improved precision.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Uses 1000 observations for tighter parameter recovery (15% tolerance).
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_LargeSample()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetLargeSampleData();
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.15));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation with wide stage range data.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Wide stage range (0.5 to 15 m) tests extrapolation behavior.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SingleSegment_WideRange()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetWideRangeData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 1)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued parameters (xi=0)
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.20));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion

    #region Parameter Recovery Tests - Two Segments

    /// <summary>
    /// Tests Bayesian MCMC estimation of two-segment rating curve parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Two-segment model: Q = α₁(h-ξ)^β₁ for h &lt; h₂, Q = α₂(h-ξ)^β₂ for h ≥ h₂
    /// Parameters: [ξ, log10(α₁), β₁, h₂, log10(α₂), β₂, σ]
    /// </para>
    /// <para>
    /// Two-segment models require more data and larger tolerance (25%) due to
    /// the increased number of parameters, breakpoint estimation, and MCMC variability.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_TwoSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetTwoSegmentData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 2)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            // Multi-segment models have more complex likelihood surfaces
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.25));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of two-segment rating curve with bankfull transition.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bankfull transition represents in-bank to overbank flow transition.
    /// Sharp change in exponent at bankfull stage.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_TwoSegment_BankfullTransition()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetBankfullTransitionData(sampleSize: 500);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 2)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 0.5 to handle zero-valued or small parameters
            // Multi-segment models have more complex likelihood surfaces
            double tolerance = Math.Max(0.5, Math.Abs(trueParams[i] * 0.25));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion

    #region Parameter Recovery Tests - Three Segments

    /// <summary>
    /// Tests Bayesian MCMC estimation of three-segment rating curve parameters.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Three-segment model has 10 parameters: [ξ, log10(α₁), β₁, h₂, log10(α₂), β₂, h₃, log10(α₃), β₃, σ]
    /// </para>
    /// <para>
    /// Three-segment models are challenging to estimate and require 30% tolerance
    /// due to the high dimensionality and MCMC variability.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ThreeSegment_Default()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetThreeSegmentData(sampleSize: 800);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 3)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 1.0 for three-segment models
            // High-dimensional MCMC has significant variability
            double tolerance = Math.Max(1.0, Math.Abs(trueParams[i] * 0.30));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of three-segment rating curve with multiple controls.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Multiple control data represents section control, channel control, and floodplain control.
    /// Each segment has distinct hydraulic characteristics.
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ThreeSegment_MultipleControl()
    {
        var (stageTS, dischargeTS, trueParams) = SyntheticRatingCurveData.GetMultipleControlData(sampleSize: 800);
        var model = new BestFitRatingCurve(stageTS, dischargeTS, numberOfSegments: 3)
        {
            UseJeffreysRuleForScale = false,
            UseDefaultFlatPriors = false
        };

        var analysis = new RatingCurveAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            // Use minimum absolute tolerance of 1.0 for three-segment models
            // High-dimensional MCMC has significant variability
            double tolerance = Math.Max(1.0, Math.Abs(trueParams[i] * 0.30));
            Assert.AreEqual(trueParams[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], tolerance,
                $"Estimated parameter {i} ({model.Parameters[i].Name}) is incorrect.");
        }
    }

    #endregion
}
