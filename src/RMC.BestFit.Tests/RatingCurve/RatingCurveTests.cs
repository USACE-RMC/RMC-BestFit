using Numerics.Data;
using RMC.BestFit.Models;
using NumericsTimeSeries = Numerics.Data.TimeSeries;

namespace RMC.BestFit.Tests.RatingCurve;

/// <summary>
/// Structural and smoke unit tests for the <c>RMC.BestFit.Models.RatingCurve</c> class.
/// </summary>
/// <remarks>
/// Tests cover: default and multi-segment construction, parameter counts, parameter names,
/// <c>SetParameterValues</c>, <c>Predict</c>, <c>GenerateSyntheticData</c>,
/// and <c>GenerateRandomValues</c>.  No Bayesian MCMC or MLE estimation is invoked.
/// </remarks>
[TestClass]
public class RatingCurveTests
{
    // Synthetic stage values (5–15 ft)
    private static readonly double[] s_stage = { 5.0, 6.5, 7.5, 9.0, 10.5, 12.0, 14.0 };

    // Synthetic discharge values (100–5000 cfs)
    private static readonly double[] s_discharge = { 110.0, 300.0, 600.0, 1200.0, 2000.0, 3500.0, 5100.0 };

    /// <summary>
    /// Creates a small stage NumericsTimeSeries fixture.
    /// </summary>
    private static Numerics.Data.TimeSeries MakeStage() =>
        new(TimeInterval.OneDay, new DateTime(2000, 1, 1), s_stage);

    /// <summary>
    /// Creates a small discharge NumericsTimeSeries fixture.
    /// </summary>
    private static Numerics.Data.TimeSeries MakeDischarge() =>
        new(TimeInterval.OneDay, new DateTime(2000, 1, 1), s_discharge);

    /// <summary>
    /// Creates a daily time series with deterministic positive values.
    /// </summary>
    /// <param name="startDate">The first date in the generated time series.</param>
    /// <param name="count">The number of observations to generate.</param>
    /// <param name="offset">The value added to each generated observation.</param>
    /// <returns>A daily time series with <paramref name="count"/> observations.</returns>
    private static NumericsTimeSeries MakeDailySeries(DateTime startDate, int count, double offset = 1.0)
    {
        var values = Enumerable.Range(0, count).Select(i => offset + i).ToArray();
        return new NumericsTimeSeries(TimeInterval.OneDay, startDate, values);
    }

    #region Default Constructor

    /// <summary>
    /// Default constructor creates 1-segment model with non-null parameters.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_Creates1SegmentModel()
    {
        var model = new RMC.BestFit.Models.RatingCurve();

        Assert.AreEqual(1, model.NumberOfSegments);
        Assert.IsNotNull(model.Parameters);
        Assert.IsTrue(model.Parameters.Count > 0);
    }

    /// <summary>
    /// Default 1-segment model has 4 parameters: ξ, log10(α), β, σ.
    /// </summary>
    [TestMethod]
    public void Constructor_Default_1Segment_Has4Parameters()
    {
        var model = new RMC.BestFit.Models.RatingCurve();

        // ξ(1) + α(1) + β(1) + σ(1) = 4
        Assert.AreEqual(4, model.Parameters.Count,
            "Single-segment rating curve should have 4 parameters: ξ, α, β, σ.");
    }

    #endregion

    #region Parameterized Constructor — Segment Counts

    /// <summary>
    /// 2-segment model has 7 parameters: ξ, α1, β1, h2, α2, β2, σ.
    /// </summary>
    [TestMethod]
    public void Constructor_2Segments_Has7Parameters()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);

        // ξ(1) + seg1(2) + breakpoint(1) + seg2(2) + σ(1) = 7
        Assert.AreEqual(7, model.Parameters.Count,
            "2-segment rating curve should have 7 parameters.");
    }

    /// <summary>
    /// 3-segment model has 10 parameters: ξ, α1, β1, h2, α2, β2, h3, α3, β3, σ.
    /// </summary>
    [TestMethod]
    public void Constructor_3Segments_Has10Parameters()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);

        // ξ(1) + seg1(2) + h2(1) + seg2(2) + h3(1) + seg3(2) + σ(1) = 10
        Assert.AreEqual(10, model.Parameters.Count,
            "3-segment rating curve should have 10 parameters.");
    }

    /// <summary>
    /// NumberOfSegments changes from 1 to 2 correctly rebuilds parameter list.
    /// </summary>
    [TestMethod]
    public void NumberOfSegments_Change_From1To2_RebuildParameters()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        Assert.AreEqual(4, model.Parameters.Count, "Before: 1 segment = 4 params.");

        model.NumberOfSegments = 2;

        Assert.AreEqual(7, model.Parameters.Count, "After: 2 segments = 7 params.");
    }

    #endregion

    #region Parameter Names

    /// <summary>
    /// First parameter is the main-channel zero-flow stage (h₁).
    /// </summary>
    [TestMethod]
    public void Parameters_FirstParam_IsZeroFlowStageH1()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);

        Assert.IsTrue(model.Parameters[0].Name.Contains("Zero-Flow"),
            $"First parameter should be Zero-Flow Stage (h₁), got '{model.Parameters[0].Name}'.");
        Assert.IsTrue(model.Parameters[0].Name.Contains("h₁") || model.Parameters[0].Name.Contains("h1"),
            $"First parameter name should include h₁, got '{model.Parameters[0].Name}'.");
    }

    /// <summary>
    /// Last parameter is scale (σ) and is constrained positive.
    /// </summary>
    [TestMethod]
    public void Parameters_LastParam_IsScaleSigma_AndPositive()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        var sigma = model.Parameters.Last();

        Assert.IsTrue(sigma.Name.Contains("σ"), "Last parameter should be Scale (σ).");
        Assert.IsTrue(sigma.IsPositive, "Scale parameter must be constrained positive.");
    }

    /// <summary>
    /// 2-segment model contains an activation-stage parameter (h₂).
    /// </summary>
    [TestMethod]
    public void Parameters_2Segment_ContainsActivationStageH2()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);

        bool hasH2 = model.Parameters.Any(p => p.Name.Contains("h2") || p.Name.Contains("h₂") || p.Name.Contains("Activation"));
        Assert.IsTrue(hasH2, "2-segment model should contain an activation-stage parameter.");
    }

    /// <summary>
    /// 2-segment BaRatin addition-mode layout: index 4 is control 2's Coefficient (α₂).
    /// All αₖ and βₖ are fit directly; h₂ is both control 2's activation stage and
    /// the "b" offset of its power-law term.
    /// </summary>
    [TestMethod]
    public void Parameters_2Segment_Index4_IsCoefficientAlpha2()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);

        Assert.IsTrue(model.Parameters[4].Name.Contains("α2") || model.Parameters[4].Name.Contains("Coefficient"),
            $"Index 4 should be control-2 coefficient α₂, got '{model.Parameters[4].Name}'.");
        Assert.IsFalse(model.Parameters[4].Name.Contains("ξ2") || model.Parameters[4].Name.Contains("Location"),
            "Index 4 must not be a free location ξ₂ — under addition mode ξ₂ = h₂.");
    }

    /// <summary>
    /// 3-segment BaRatin addition-mode layout: indices 4 and 7 are the coefficients
    /// for controls 2 and 3.
    /// </summary>
    [TestMethod]
    public void Parameters_3Segment_Indices4And7_AreCoefficientsAlpha2Alpha3()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);

        Assert.IsTrue(model.Parameters[4].Name.Contains("α2") || model.Parameters[4].Name.Contains("Coefficient"),
            $"Index 4 should be α₂, got '{model.Parameters[4].Name}'.");
        Assert.IsTrue(model.Parameters[7].Name.Contains("α3") || model.Parameters[7].Name.Contains("Coefficient"),
            $"Index 7 should be α₃, got '{model.Parameters[7].Name}'.");
    }

    #endregion

    #region Default Flat Priors

    /// <summary>
    /// Default flat priors for log10(alpha) are wide enough for major-river rating curves.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_AlphaBounds_AreWideForAllSegments()
    {
        for (int segmentCount = 1; segmentCount <= 3; segmentCount++)
        {
            var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), segmentCount);
            int[] alphaIndexes = segmentCount switch
            {
                1 => new[] { 1 },
                2 => new[] { 1, 4 },
                _ => new[] { 1, 4, 7 }
            };

            foreach (int index in alphaIndexes)
            {
                var parameter = model.Parameters[index];
                Assert.AreEqual(-10.0, parameter.LowerBound, 1e-12,
                    $"Alpha parameter {index} lower bound should be -10.");
                Assert.AreEqual(10.0, parameter.UpperBound, 1e-12,
                    $"Alpha parameter {index} upper bound should be 10.");
                Assert.IsFalse(double.IsNegativeInfinity(parameter.PriorDistribution.LogPDF(0.0)),
                    $"Alpha parameter {index} prior should include log10(alpha)=0.");
                Assert.IsTrue(double.IsNegativeInfinity(parameter.PriorDistribution.LogPDF(10.1)),
                    $"Alpha parameter {index} prior should exclude values above 10.");
            }
        }
    }

    /// <summary>
    /// Default flat priors for beta allow near-zero exponents and are not strictly positive.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_BetaBounds_AllowZeroForAllSegments()
    {
        for (int segmentCount = 1; segmentCount <= 3; segmentCount++)
        {
            var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), segmentCount);
            int[] betaIndexes = segmentCount switch
            {
                1 => new[] { 2 },
                2 => new[] { 2, 5 },
                _ => new[] { 2, 5, 8 }
            };

            foreach (int index in betaIndexes)
            {
                var parameter = model.Parameters[index];
                Assert.AreEqual(0.0, parameter.LowerBound, 1e-12,
                    $"Beta parameter {index} lower bound should be 0.");
                Assert.AreEqual(5.0, parameter.UpperBound, 1e-12,
                    $"Beta parameter {index} upper bound should be 5.");
                Assert.IsFalse(parameter.IsPositive,
                    $"Beta parameter {index} should be nonnegative, not strictly positive.");
                Assert.IsFalse(double.IsNegativeInfinity(parameter.PriorDistribution.LogPDF(0.0)),
                    $"Beta parameter {index} prior should include beta=0.");
            }
        }
    }

    /// <summary>
    /// Default parameter values are finite and lie inside their default bounds.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_DefaultValues_AreFiniteAndInsideBounds()
    {
        for (int segmentCount = 1; segmentCount <= 3; segmentCount++)
        {
            var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), segmentCount);

            foreach (var parameter in model.Parameters)
            {
                Assert.IsFalse(double.IsNaN(parameter.Value), $"{parameter.Name} value should not be NaN.");
                Assert.IsFalse(double.IsInfinity(parameter.Value), $"{parameter.Name} value should be finite.");
                Assert.IsTrue(parameter.LowerBound < parameter.UpperBound,
                    $"{parameter.Name} bounds should not collapse.");
                Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
                    $"{parameter.Name} value should be inside its bounds.");
            }
        }
    }

    /// <summary>
    /// Constant stage data uses a finite span fallback instead of collapsing stage priors.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_ConstantStageData_DoesNotCollapseStagePriors()
    {
        var stage = MakeSeries(new DateTime(2000, 1, 1), Enumerable.Repeat(10.0, 20).ToArray());
        var discharge = MakeSeries(new DateTime(2000, 1, 1),
            Enumerable.Range(0, 20).Select(i => 100.0 + 5.0 * i).ToArray());
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 3);

        foreach (int index in new[] { 0, 3, 6 })
        {
            var parameter = model.Parameters[index];
            Assert.IsTrue(parameter.LowerBound < parameter.UpperBound,
                $"{parameter.Name} should have non-collapsed bounds.");
            Assert.IsTrue(parameter.Value >= parameter.LowerBound && parameter.Value <= parameter.UpperBound,
                $"{parameter.Name} value should be inside its bounds.");
        }

        Assert.IsTrue(model.Parameters[0].Value < model.Parameters[3].Value);
        Assert.IsTrue(model.Parameters[3].Value < model.Parameters[6].Value);
    }

    /// <summary>
    /// Constant positive discharge data uses a positive sigma fallback instead of collapsing the scale prior.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_ConstantPositiveDischarge_DoesNotCollapseSigmaPrior()
    {
        var stage = MakeSeries(new DateTime(2000, 1, 1),
            Enumerable.Range(0, 20).Select(i => 1.0 + i).ToArray());
        var discharge = MakeSeries(new DateTime(2000, 1, 1), Enumerable.Repeat(1000.0, 20).ToArray());
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1);
        var sigma = model.Parameters.Last();

        Assert.IsTrue(sigma.LowerBound > 0.0, "Sigma lower bound should be positive.");
        Assert.IsTrue(sigma.UpperBound > sigma.LowerBound, "Sigma bounds should not collapse.");
        Assert.IsTrue(sigma.Value > sigma.LowerBound && sigma.Value < sigma.UpperBound,
            "Sigma default value should be strictly inside its bounds.");
    }

    /// <summary>
    /// Date-aligned observations drive default prior calibration; unpaired outliers are ignored.
    /// </summary>
    [TestMethod]
    public void DefaultFlatPriors_UseAlignedObservations_NotUnpairedOutliers()
    {
        var start = new DateTime(2000, 1, 1);
        var stageValues = Enumerable.Range(0, 20).Select(i => 1.0 + i).Concat(new[] { 10000.0 }).ToArray();
        var dischargeValues = Enumerable.Range(0, 20).Select(i => 100.0 + i).Concat(new[] { 1e30 }).ToArray();
        var stage = MakeSeries(start, stageValues);
        var discharge = MakeSeries(start, dischargeValues);
        stage[20] = new Numerics.Data.SeriesOrdinate<DateTime, double>(new DateTime(2050, 1, 1), 10000.0);
        discharge[20] = new Numerics.Data.SeriesOrdinate<DateTime, double>(new DateTime(2060, 1, 1), 1e30);

        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 3);

        Assert.AreEqual(-18.0, model.Parameters[0].LowerBound, 1e-12,
            "h1 lower bound should use the aligned stage range 1..20.");
        Assert.AreEqual(20.0, model.Parameters[6].UpperBound, 1e-12,
            "h3 upper bound should use the aligned stage range 1..20.");
        Assert.AreEqual(1.0, model.Parameters.Last().UpperBound, 1e-12,
            "Sigma upper bound should ignore the unpaired discharge outlier.");
    }

    #endregion

    #region Addition-mode discharge composition

    /// <summary>
    /// Two-segment Predict at the activation stage h₂: because the second term is
    /// α₂·(h−h₂)^β₂·𝟙{h>h₂} which vanishes at h = h₂, Q(h₂) must equal the
    /// main-channel contribution alone.
    /// </summary>
    [TestMethod]
    public void Predict_2Seg_AtBreakpoint_EqualsMainChannelAlone()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        // [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, σ]
        double[] parms = { 0.2, 1.0, 2.5, 8.0, 2.0, 1.6, 0.05 };
        model.SetParameterValues(parms);

        double h2 = parms[3];
        double qMainAtH2 = Math.Pow(10, parms[1]) * Math.Pow(h2 - parms[0], parms[2]);
        double qAtH2 = model.Predict(parms, h2);

        Assert.AreEqual(qMainAtH2, qAtH2, 1e-9,
            $"At h₂ the overbank term is zero, so Q(h₂) = α₁(h₂−h₁)^β₁. Main={qMainAtH2}, Predict={qAtH2}.");
    }

    /// <summary>
    /// Two-segment Predict above h₂: Q(h) must equal the SUM of the main-channel and
    /// overbank contributions (addition mode — not a replacement power law).
    /// </summary>
    [TestMethod]
    public void Predict_2Seg_AboveBreakpoint_SumsMainAndOverbank()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        double[] parms = { 0.2, 1.0, 2.5, 8.0, 2.0, 1.6, 0.05 };
        model.SetParameterValues(parms);

        double h = 11.0; // above h₂ = 8
        double qMain = Math.Pow(10, parms[1]) * Math.Pow(h - parms[0], parms[2]);
        double qOverbank = Math.Pow(10, parms[4]) * Math.Pow(h - parms[3], parms[5]);
        double expected = qMain + qOverbank;

        double qPredict = model.Predict(parms, h);

        Assert.AreEqual(expected, qPredict, 1e-9,
            $"Above h₂, Q must sum main + overbank. Expected {expected}, got {qPredict}.");
    }

    /// <summary>
    /// Three-segment Predict at an interior stage h₂ &lt; h &lt; h₃: Q must equal
    /// main-channel + control-2 contributions, with control 3 not yet active.
    /// </summary>
    [TestMethod]
    public void Predict_3Seg_BetweenBreakpoints_SumsFirstTwoControls()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);
        double[] parms = { 0.2, 1.0, 2.5, 6.0, 1.5, 2.0, 10.0, 2.0, 1.5, 0.05 };
        model.SetParameterValues(parms);

        double h = 8.0; // h₂ < h < h₃
        double q1 = Math.Pow(10, parms[1]) * Math.Pow(h - parms[0], parms[2]);
        double q2 = Math.Pow(10, parms[4]) * Math.Pow(h - parms[3], parms[5]);
        double expected = q1 + q2; // control 3 not active

        double qPredict = model.Predict(parms, h);

        Assert.AreEqual(expected, qPredict, 1e-9,
            $"Between breakpoints, Q = control1 + control2. Expected {expected}, got {qPredict}.");
    }

    /// <summary>
    /// Three-segment Predict above h₃: Q must equal the full three-way sum.
    /// </summary>
    [TestMethod]
    public void Predict_3Seg_AboveLastBreakpoint_SumsAllThreeControls()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);
        double[] parms = { 0.2, 1.0, 2.5, 6.0, 1.5, 2.0, 10.0, 2.0, 1.5, 0.05 };
        model.SetParameterValues(parms);

        double h = 13.0; // above h₃
        double q1 = Math.Pow(10, parms[1]) * Math.Pow(h - parms[0], parms[2]);
        double q2 = Math.Pow(10, parms[4]) * Math.Pow(h - parms[3], parms[5]);
        double q3 = Math.Pow(10, parms[7]) * Math.Pow(h - parms[6], parms[8]);
        double expected = q1 + q2 + q3;

        double qPredict = model.Predict(parms, h);

        Assert.AreEqual(expected, qPredict, 1e-9,
            $"Above h₃, Q = control1 + control2 + control3. Expected {expected}, got {qPredict}.");
    }

    /// <summary>
    /// Below h₂ the Predict value must depend only on the main-channel parameters —
    /// changing α₂, β₂ must not affect it.
    /// </summary>
    [TestMethod]
    public void Predict_2Seg_BelowBreakpoint_IndependentOfSecondControl()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        double[] parms1 = { 0.2, 1.0, 2.5, 8.0, 2.0, 1.6, 0.05 };
        double[] parms2 = { 0.2, 1.0, 2.5, 8.0, 4.0, 1.2, 0.05 }; // very different α₂, β₂

        double stage = 5.0; // below h₂ = 8
        model.SetParameterValues(parms1);
        double q1 = model.Predict(parms1, stage);
        model.SetParameterValues(parms2);
        double q2 = model.Predict(parms2, stage);

        Assert.AreEqual(q1, q2, 1e-12,
            $"Below h₂, changing α₂/β₂ must not change Q. Got {q1} vs {q2}.");
    }

    #endregion

    #region GetLog10Alpha — indexer into fit parameters

    /// <summary>
    /// Single-segment: GetLog10Alpha(1) returns the fit log₁₀α₁ value.
    /// </summary>
    [TestMethod]
    public void GetLog10Alpha_SingleSegment_ReturnsFitValue()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 1.23, 2.0, 0.05 };
        model.SetParameterValues(parms);

        Assert.AreEqual(1.23, model.GetLog10Alpha(1, parms), 1e-12);
    }

    /// <summary>
    /// Two-segment: GetLog10Alpha(2) returns the fit log₁₀α₂ value at index 4.
    /// </summary>
    [TestMethod]
    public void GetLog10Alpha_TwoSegment_ReturnsIndex4()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        double[] parms = { 0.2, 1.0, 2.5, 8.0, 2.15, 1.6, 0.05 };
        model.SetParameterValues(parms);

        Assert.AreEqual(2.15, model.GetLog10Alpha(2, parms), 1e-12);
    }

    /// <summary>
    /// Three-segment: GetLog10Alpha(3) returns the fit log₁₀α₃ value at index 7.
    /// </summary>
    [TestMethod]
    public void GetLog10Alpha_ThreeSegment_ReturnsIndex7()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);
        double[] parms = { 0.2, 1.0, 2.5, 6.0, 1.5, 2.0, 10.0, 2.4, 1.5, 0.05 };
        model.SetParameterValues(parms);

        Assert.AreEqual(2.4, model.GetLog10Alpha(3, parms), 1e-12);
    }

    /// <summary>
    /// GetLog10Alpha throws ArgumentOutOfRangeException for out-of-range segment indices.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetLog10Alpha_SegmentZero_Throws()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 1.23, 2.0, 0.05 };
        _ = model.GetLog10Alpha(0, parms);
    }

    /// <summary>
    /// GetLog10Alpha throws when requesting a segment beyond NumberOfSegments.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GetLog10Alpha_SegmentAboveCount_Throws()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 1.23, 2.0, 0.05 };
        _ = model.GetLog10Alpha(2, parms);
    }

    #endregion

    #region GetLocation — b_k offsets in addition mode

    /// <summary>
    /// Single-segment: GetLocation(1) returns the fit h₁ value.
    /// </summary>
    [TestMethod]
    public void GetLocation_SingleSegment_ReturnsFitValue()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 1.23, 2.0, 0.05 };
        model.SetParameterValues(parms);

        Assert.AreEqual(0.5, model.GetLocation(1, parms), 1e-12);
    }

    /// <summary>
    /// Two-segment addition mode: GetLocation(2) returns h₂ (the activation stage
    /// of control 2 equals its "b" offset because continuity collapses to b₂ = κ₂).
    /// </summary>
    [TestMethod]
    public void GetLocation_TwoSegment_ReturnsH2()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        double[] parms = { 0.2, 1.0, 2.5, 8.0, 2.0, 1.6, 0.05 };
        model.SetParameterValues(parms);

        double b2 = model.GetLocation(2, parms);
        Assert.AreEqual(parms[3], b2, 1e-12,
            $"Under addition mode b₂ = h₂. Expected {parms[3]}, got {b2}.");
    }

    /// <summary>
    /// Three-segment addition mode: GetLocation(3) returns h₃.
    /// </summary>
    [TestMethod]
    public void GetLocation_ThreeSegment_ReturnsH3()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);
        double[] parms = { 0.2, 1.0, 2.5, 6.0, 1.5, 2.0, 10.0, 2.0, 1.5, 0.05 };
        model.SetParameterValues(parms);

        double b2 = model.GetLocation(2, parms);
        double b3 = model.GetLocation(3, parms);

        Assert.AreEqual(parms[3], b2, 1e-12, $"b₂ should equal h₂ = {parms[3]}.");
        Assert.AreEqual(parms[6], b3, 1e-12, $"b₃ should equal h₃ = {parms[6]}.");
    }

    #endregion

    #region Ordering constraints (ValidateSegmentOrdering via public Validate())

    /// <summary>
    /// 2-segment: ordering is invalid when h₁ ≥ h₂ (segment 1 domain would be empty).
    /// </summary>
    [TestMethod]
    public void Validate_2Segment_Xi1GreaterThanOrEqualH2_IsInvalid()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 2);
        // h₁ = 9.0 > h₂ = 8.0 — invalid
        double[] parms = { 9.0, 1.0, 2.5, 8.0, 2.0, 1.6, 0.05 };
        model.SetParameterValues(parms);

        var (isValid, messages) = model.Validate();
        Assert.IsFalse(isValid, "Model with h₁ ≥ h₂ should be invalid.");
        Assert.IsTrue(messages.Any(m => m.Contains("Segment ordering")),
            "Validation message should mention segment ordering.");
    }

    /// <summary>
    /// 3-segment: ordering is invalid when h₂ ≥ h₃.
    /// </summary>
    [TestMethod]
    public void Validate_3Segment_H2GreaterThanOrEqualH3_IsInvalid()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 3);
        // h₂ = 10.0 >= h₃ = 10.0 — invalid
        double[] parms = { 0.2, 1.0, 2.5, 10.0, 1.5, 2.0, 10.0, 2.0, 1.5, 0.05 };
        model.SetParameterValues(parms);

        var (isValid, _) = model.Validate();
        Assert.IsFalse(isValid, "Model with h₂ ≥ h₃ should be invalid.");
    }

    /// <summary>
    /// Regression: incrementing <c>RMC.BestFit.Models.RatingCurve.NumberOfSegments</c> from 1 to 2
    /// must not throw. Earlier builds raised <c>PropertyChanged</c> before rebuilding the parameter
    /// list, causing downstream handlers (<c>ClearResults</c> → <c>SetIsValid</c> → <c>Validate</c>
    /// → <c>ValidateSegmentOrdering</c>) to index past the end of a stale 4-element array.
    /// </summary>
    [TestMethod]
    public void NumberOfSegments_Change_1To2_DoesNotThrowDuringPropertyChangeCascade()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        // Subscribe a handler that reads Parameters by index via Validate().
        // If the setter raises PropertyChanged before rebuilding Parameters, this handler
        // will observe a stale list and Validate() will throw IndexOutOfRangeException.
        model.PropertyChanged += (_, _) => model.Validate();

        // Act — must not throw.
        model.NumberOfSegments = 2;

        Assert.AreEqual(7, model.Parameters.Count, "2-segment model should have 7 parameters after bump.");
    }

    #endregion

    #region SetParameterValues

    /// <summary>
    /// SetParameterValues stores the supplied values in the parameter list.
    /// </summary>
    [TestMethod]
    public void SetParameterValues_StoresAllValues()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        // 1 segment: ξ, α1, β1, σ
        double[] vals = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(vals);

        for (int i = 0; i < vals.Length; i++)
            Assert.AreEqual(vals[i], model.Parameters[i].Value, 1e-10, $"Parameter {i} mismatch.");
    }

    /// <summary>
    /// SetParameterValues with null throws ArgumentNullException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentNullException))]
    public void SetParameterValues_Null_ThrowsArgumentNullException()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        model.SetParameterValues(null!);
    }

    /// <summary>
    /// SetParameterValues with wrong count throws ArgumentException.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentException))]
    public void SetParameterValues_WrongCount_ThrowsArgumentException()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        model.SetParameterValues(new double[] { 1.0, 2.0 }); // Need 4
    }

    #endregion

    #region Predict

    /// <summary>
    /// Predict returns positive discharge for a valid set of parameters.
    /// </summary>
    [TestMethod]
    public void Predict_WithValidParameters_ReturnsPositiveDischarge()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        // ξ=4, α=0.5 (log10 coeff), β=1.8, σ=0.1
        double[] parms = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(parms);

        double q = model.Predict(parms, stage: 8.0);

        Assert.IsTrue(q > 0, $"Predict should return positive discharge, got {q}.");
    }

    /// <summary>
    /// Predict returns a larger discharge for a higher stage (monotonicity check).
    /// </summary>
    [TestMethod]
    public void Predict_HigherStage_YieldsHigherDischarge()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        double[] parms = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(parms);

        double qLow  = model.Predict(parms, stage: 7.0);
        double qHigh = model.Predict(parms, stage: 10.0);

        Assert.IsTrue(qHigh > qLow,
            $"Higher stage should produce higher discharge: Q(10)={qHigh}, Q(7)={qLow}.");
    }

    #endregion

    #region GenerateSyntheticData

    /// <summary>
    /// GenerateSyntheticData returns two NumericsTimeSeries of the requested sample size.
    /// </summary>
    [TestMethod]
    public void GenerateSyntheticData_ReturnsTwoSeriesOfRequestedSize()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 0.3, 1.8, 0.05 }; // ξ=0.5, α, β, σ
        model.SetParameterValues(parms);

        var (stageTs, dischargeTs) = model.GenerateSyntheticData(
            sampleSize: 20,
            minStage: 1.0,
            maxStage: 5.0,
            seed: 42);

        Assert.AreEqual(20, stageTs.Count, "Stage series should have 20 points.");
        Assert.AreEqual(20, dischargeTs.Count, "Discharge series should have 20 points.");
    }

    /// <summary>
    /// GenerateSyntheticData: all discharge values are positive.
    /// </summary>
    [TestMethod]
    public void GenerateSyntheticData_AllDischargesPositive()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 0.3, 1.8, 0.05 };
        model.SetParameterValues(parms);

        var (_, dischargeTs) = model.GenerateSyntheticData(
            sampleSize: 50,
            minStage: 1.0,
            maxStage: 5.0,
            seed: 123);

        for (int i = 0; i < dischargeTs.Count; i++)
            Assert.IsTrue(dischargeTs[i].Value > 0, $"Discharge at index {i} should be positive.");
    }

    /// <summary>
    /// GenerateSyntheticData throws ArgumentOutOfRangeException when sampleSize is zero.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateSyntheticData_ZeroSampleSize_ThrowsArgumentOutOfRangeException()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        model.GenerateSyntheticData(sampleSize: 0, minStage: 1.0, maxStage: 5.0);
    }

    /// <summary>
    /// GenerateSyntheticData is deterministic when same seed is used.
    /// </summary>
    [TestMethod]
    public void GenerateSyntheticData_SameSeed_ProducesSameResult()
    {
        var model = new RMC.BestFit.Models.RatingCurve();
        double[] parms = { 0.5, 0.3, 1.8, 0.05 };
        model.SetParameterValues(parms);

        var (_, discharge1) = model.GenerateSyntheticData(10, 1.0, 5.0, seed: 999);
        var (_, discharge2) = model.GenerateSyntheticData(10, 1.0, 5.0, seed: 999);

        for (int i = 0; i < discharge1.Count; i++)
            Assert.AreEqual(discharge1[i].Value, discharge2[i].Value, 1e-10,
                $"Discharge at index {i} should be identical for same seed.");
    }

    #endregion

    #region GenerateRandomValues

    /// <summary>
    /// GenerateRandomValues returns an array of the requested size with all positive values.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_ReturnsSizedArrayWithPositiveValues()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        double[] parms = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(parms);

        double[] samples = model.GenerateRandomValues(sampleSize: 30, seed: 42);

        Assert.AreEqual(30, samples.Length, "Result array should have 30 samples.");
        Assert.IsTrue(samples.All(v => v > 0), "All generated discharges must be positive.");
    }

    /// <summary>
    /// GenerateRandomValues is deterministic for the same seed.
    /// </summary>
    [TestMethod]
    public void GenerateRandomValues_SameSeed_Deterministic()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        double[] parms = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(parms);

        double[] run1 = model.GenerateRandomValues(20, seed: 7);
        double[] run2 = model.GenerateRandomValues(20, seed: 7);

        CollectionAssert.AreEqual(run1, run2,
            "GenerateRandomValues with the same seed must be deterministic.");
    }

    /// <summary>
    /// GenerateRandomValues throws ArgumentOutOfRangeException when sampleSize is zero.
    /// </summary>
    [TestMethod]
    [ExpectedException(typeof(ArgumentOutOfRangeException))]
    public void GenerateRandomValues_ZeroSampleSize_Throws()
    {
        var model = new RMC.BestFit.Models.RatingCurve(MakeStage(), MakeDischarge(), numberOfSegments: 1);
        double[] parms = { 4.0, 0.5, 1.8, 0.1 };
        model.SetParameterValues(parms);

        model.GenerateRandomValues(sampleSize: 0);
    }

    #endregion

    #region UseJeffreysRuleForScale

    /// <summary>
    /// UseJeffreysRuleForScale defaults to true.
    /// </summary>
    [TestMethod]
    public void UseJeffreysRuleForScale_Default_IsTrue()
    {
        var model = new RMC.BestFit.Models.RatingCurve();

        Assert.IsTrue(model.UseJeffreysRuleForScale);
    }

    /// <summary>
    /// UseJeffreysRuleForScale can be set to false.
    /// </summary>
    [TestMethod]
    public void UseJeffreysRuleForScale_SetFalse_Persists()
    {
        var model = new RMC.BestFit.Models.RatingCurve { UseJeffreysRuleForScale = false };

        Assert.IsFalse(model.UseJeffreysRuleForScale);
    }

    #endregion

    #region Stage / Discharge date-alignment (inner-join) behavior

    /// <summary>
    /// Builds a NumericsTimeSeries whose ordinate at index i has Index = startDate + i days
    /// and Value = values[i].
    /// </summary>
    private static Numerics.Data.TimeSeries MakeSeries(DateTime startDate, double[] values)
    {
        var ts = new Numerics.Data.TimeSeries(TimeInterval.OneDay, startDate, new double[values.Length]);
        for (int i = 0; i < values.Length; i++)
            ts[i] = new Numerics.Data.SeriesOrdinate<DateTime, double>(startDate.AddDays(i), values[i]);
        return ts;
    }

    /// <summary>
    /// Mismatched series lengths are OK as long as ≥ 10 dates are shared.
    /// </summary>
    [TestMethod]
    public void Validate_MismatchedCounts_WithEnoughCommonDates_IsValid()
    {
        var start = new DateTime(2000, 1, 1);
        // Stage covers 120 days starting 2000-01-01
        var stageVals = Enumerable.Range(0, 120).Select(i => 1.0 + 0.05 * i).ToArray();
        // Discharge covers 100 days starting 2000-01-01 (shares first 100 dates)
        var dischargeVals = Enumerable.Range(0, 100).Select(i => 10.0 + 2.0 * i).ToArray();

        var model = new RMC.BestFit.Models.RatingCurve(
            MakeSeries(start, stageVals), MakeSeries(start, dischargeVals), numberOfSegments: 1);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Expected valid with 100 common dates. Messages: {string.Join(";", messages)}");
        Assert.IsFalse(messages.Any(m => m.Contains("common dates")),
            "Should not emit a common-dates error when 100 dates are shared.");
    }

    /// <summary>
    /// Fewer than 10 common dates triggers the new alignment error.
    /// </summary>
    [TestMethod]
    public void Validate_FewerThanTenCommonDates_IsInvalid()
    {
        var start = new DateTime(2000, 1, 1);
        // Stage 2000-01-01 .. 2000-02-19 (50 days)
        var stage = MakeSeries(start, Enumerable.Range(0, 50).Select(i => 1.0 + 0.1 * i).ToArray());
        // Discharge 2000-06-01 .. 2000-07-20 (50 days, zero overlap with stage)
        var discharge = MakeSeries(new DateTime(2000, 6, 1),
            Enumerable.Range(0, 50).Select(i => 10.0 + 2.0 * i).ToArray());

        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1);
        var (isValid, messages) = model.Validate();

        Assert.IsFalse(isValid, "Expected invalid when 0 common dates.");
        Assert.IsTrue(messages.Any(m => m.Contains("common dates")),
            $"Should emit the common-dates error. Messages: {string.Join(";", messages)}");
    }

    /// <summary>
    /// DataLogLikelihood iterates the inner-join of dates — observations in only
    /// one series must be dropped from the likelihood computation.
    /// </summary>
    [TestMethod]
    public void DataLogLikelihood_UsesOnlyCommonDates()
    {
        var start = new DateTime(2000, 1, 1);

        // Both series have 30 observations, perfectly aligned.
        var stageVals = Enumerable.Range(0, 30).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = stageVals.Select(h => 2.0 * Math.Pow(h - 0.2, 1.8)).ToArray();

        var stageAligned = MakeSeries(start, stageVals);
        var dischargeAligned = MakeSeries(start, dischargeVals);
        var modelAligned = new RMC.BestFit.Models.RatingCurve(stageAligned, dischargeAligned, numberOfSegments: 1);
        double[] parms = { 0.2, Math.Log10(2.0), 1.8, 0.05 };
        modelAligned.SetParameterValues(parms);
        double aligned_ll = modelAligned.DataLogLikelihood(parms);

        // Append 15 extra stage observations with dates that don't appear in discharge.
        var extendedStageVals = stageVals.Concat(Enumerable.Range(0, 15).Select(i => 5.0 + 0.1 * i)).ToArray();
        var stageExtended = MakeSeries(start, extendedStageVals);
        // Place the extra days after the original run; they will not match any discharge date.
        for (int i = 30; i < 45; i++)
            stageExtended[i] = new Numerics.Data.SeriesOrdinate<DateTime, double>(
                start.AddYears(5).AddDays(i - 30), extendedStageVals[i]);

        var modelExtended = new RMC.BestFit.Models.RatingCurve(stageExtended, dischargeAligned, numberOfSegments: 1)
        {
            UseDefaultFlatPriors = false
        };
        modelExtended.SetParameterValues(parms);

        double extended_ll = modelExtended.DataLogLikelihood(parms);

        Assert.AreEqual(aligned_ll, extended_ll, 1e-10,
            "Log-likelihood must only count the 30 aligned dates; extra stage obs should be dropped.");
    }

    /// <summary>
    /// Residuals() returns one value per common date, not per stage entry.
    /// </summary>
    [TestMethod]
    public void Residuals_ReturnsOneValuePerCommonDate()
    {
        var start = new DateTime(2000, 1, 1);
        var stageVals = Enumerable.Range(0, 40).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = stageVals.Take(25).Select(h => 2.0 * Math.Pow(h - 0.2, 1.8)).ToArray();

        var stage = MakeSeries(start, stageVals);       // 40 dates
        var discharge = MakeSeries(start, dischargeVals); // 25 dates, subset of stage
        var model = new RMC.BestFit.Models.RatingCurve(stage, discharge, numberOfSegments: 1)
        {
            UseDefaultFlatPriors = false
        };
        double[] parms = { 0.2, Math.Log10(2.0), 1.8, 0.05 };
        model.SetParameterValues(parms);

        var residuals = model.Residuals(parms);

        Assert.AreEqual(25, residuals.Length,
            $"Expected one residual per aligned pair (25), got {residuals.Length}.");
    }

    /// <summary>
    /// Regression: the 100 %-overlap case remains valid — no accidental tightening.
    /// </summary>
    [TestMethod]
    public void Validate_IdenticalSeries_StillValid()
    {
        var start = new DateTime(2000, 1, 1);
        var stageVals = Enumerable.Range(0, 20).Select(i => 1.0 + 0.1 * i).ToArray();
        var dischargeVals = stageVals.Select(h => 2.0 * Math.Pow(h - 0.2, 1.8)).ToArray();

        var model = new RMC.BestFit.Models.RatingCurve(
            MakeSeries(start, stageVals), MakeSeries(start, dischargeVals), numberOfSegments: 1);

        var (isValid, messages) = model.Validate();

        Assert.IsTrue(isValid, $"Expected valid. Messages: {string.Join(";", messages)}");
    }

    /// <summary>
    /// Matching stage and discharge dates report all observations as aligned pairs.
    /// </summary>
    [TestMethod]
    public void GetDataAlignmentCounts_FullOverlap_ReturnsAllCounts()
    {
        var startDate = new DateTime(2000, 1, 1);
        var model = new RMC.BestFit.Models.RatingCurve(
            MakeDailySeries(startDate, 100, 1.0),
            MakeDailySeries(startDate, 100, 100.0));

        var counts = model.GetDataAlignmentCounts();

        Assert.AreEqual(100, counts.StageCount);
        Assert.AreEqual(100, counts.DischargeCount);
        Assert.AreEqual(100, counts.PairedCount);
    }

    /// <summary>
    /// Equal-length time series with staggered dates report only common dates as aligned pairs.
    /// </summary>
    [TestMethod]
    public void GetDataAlignmentCounts_PartialOverlap_ReturnsPairedCount()
    {
        var startDate = new DateTime(2000, 1, 1);
        var model = new RMC.BestFit.Models.RatingCurve(
            MakeDailySeries(startDate, 100, 1.0),
            MakeDailySeries(startDate.AddDays(60), 100, 100.0));

        var counts = model.GetDataAlignmentCounts();

        Assert.AreEqual(100, counts.StageCount);
        Assert.AreEqual(100, counts.DischargeCount);
        Assert.AreEqual(40, counts.PairedCount);
    }

    #endregion
}
