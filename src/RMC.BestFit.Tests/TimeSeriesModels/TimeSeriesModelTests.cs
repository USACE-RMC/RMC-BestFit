using Numerics.Data;
using RMC.BestFit.Models;

namespace RMC.BestFit.Tests.TimeSeriesModels;

/// <summary>
/// Structural unit tests verifying that the Predict methods draw residual noise across
/// the full horizon (training + forecast), not just in the forecast region.
/// </summary>
/// <remarks>
/// Before the fix, Predict only added stochastic error for <c>t &gt;= TrainingTimeSteps</c>,
/// which caused Monte Carlo CI bands to collapse to zero width in the training period.
/// These tests guard the posterior-predictive contract by confirming that two calls to
/// Predict with different seeds produce different values at indices inside the fit window.
/// </remarks>
[TestClass]
public class PredictNoiseInTrainingTests
{
    private static readonly double[] s_values = new double[]
    {
        122, 244, 214, 173, 229, 156, 212, 263, 146, 183,
        161, 205, 135, 331, 225, 174,  99, 149, 238, 262,
        132, 235, 216, 240, 230, 192, 195, 172, 173, 172,
        153, 142, 317, 161, 201, 204, 194, 164, 183, 161,
        167, 179, 185, 117, 192, 337, 125, 166,  99, 202
    };

    private static Numerics.Data.TimeSeries MakeTimeSeries() =>
        new(TimeInterval.OneYear, new DateTime(1970, 1, 1), s_values);

    /// <summary>
    /// AutoRegressive.Predict with different seeds must produce different y[t] for
    /// t inside the training window (maxOrder &lt;= t &lt; TrainingTimeSteps).
    /// </summary>
    [TestMethod]
    public void AutoRegressive_Predict_DifferentSeeds_DifferInTrainingRegion()
    {
        var ts = MakeTimeSeries();
        var model = new AutoRegressive(ts, order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        // Use explicit parameters so we can compute a non-trivial prediction. Sigma > 0
        // is required for the Normal(0, sigma) residual draw.
        double[] pars = { 190.0, 0.4, 22.0 }; // mu, phi1, sigma
        model.SetParameterValues(pars);

        var y1 = model.Predict(pars, forecastSteps: 0, seed: 1).Y;
        var y2 = model.Predict(pars, forecastSteps: 0, seed: 2).Y;

        // Expect at least one training-region value to differ between the two seed draws.
        bool anyDiffer = false;
        for (int t = 1; t < ts.Count; t++)
        {
            if (Math.Abs(y1[t] - y2[t]) > 1e-9)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.IsTrue(anyDiffer,
            "Predict must inject residual noise throughout the training window, not only in the forecast region.");
    }

    /// <summary>
    /// ARIMAX.Predict with AR(1,0,0), no differencing, must produce different y[t]
    /// for different seeds at indices inside the training window.
    /// </summary>
    [TestMethod]
    public void ARIMAX_Predict_DifferentSeeds_DifferInTrainingRegion()
    {
        var ts = MakeTimeSeries();
        var model = new ARIMAX(ts)
        {
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            IncludeIntercept = true,
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        double[] pars = { 190.0, 0.4, 22.0 }; // mu, phi1, sigma
        model.SetParameterValues(pars);

        var y1 = model.Predict(pars, forecastSteps: 0, seed: 1).Y;
        var y2 = model.Predict(pars, forecastSteps: 0, seed: 2).Y;

        bool anyDiffer = false;
        for (int t = 1; t < ts.Count; t++)
        {
            if (Math.Abs(y1[t] - y2[t]) > 1e-9)
            {
                anyDiffer = true;
                break;
            }
        }

        Assert.IsTrue(anyDiffer,
            "ARIMAX.Predict must inject residual noise throughout the training window.");
    }

    /// <summary>
    /// ARIMAX.Predict with DiffOrderD=1 must produce a training-region CI with roughly
    /// constant width (bounded by a small multiple of sigma) instead of a random-walk
    /// cone that grows like sqrt(t). This guards the integration-anchoring fix.
    /// </summary>
    [TestMethod]
    public void ARIMAX_Predict_Differenced_TrainingCIBoundedBySigma()
    {
        var ts = MakeTimeSeries();
        var model = new ARIMAX(ts)
        {
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            IncludeIntercept = true,
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        // Reasonable ARIMA(1,1,1) parameters for a noisy series with moderate variance.
        // Layout: intercept, phi1, theta1, sigma
        double[] pars = { 0.5, 0.3, -0.2, 30.0 };
        model.SetParameterValues(pars);
        double sigma = pars[3];

        // Generate 200 realizations at fixed parameters; empirical width = 97.5% - 2.5%.
        const int realz = 200;
        int n = ts.Count; // no future forecast for this assertion
        var samples = new double[n, realz];
        for (int r = 0; r < realz; r++)
        {
            var y = model.Predict(pars, forecastSteps: 0, seed: r + 1).Y;
            for (int t = 0; t < n; t++)
                samples[t, r] = y[t];
        }

        // Width at an early training index should be comparable to width at a late one.
        // A random-walk cone would have late/early ratio ~ sqrt(t_late / t_early).
        double widthAt(int t)
        {
            var row = new double[realz];
            for (int r = 0; r < realz; r++) row[r] = samples[t, r];
            Array.Sort(row);
            return row[(int)(0.975 * realz)] - row[(int)(0.025 * realz)];
        }

        // Use a reasonable pair of training indices well past the seeding region.
        double earlyWidth = widthAt(5);
        double lateWidth = widthAt(n - 5);

        // Upper bound: late width should not exceed ~6 * sigma. A random-walk cone over
        // 50 points with sigma=30 would produce width ≈ 1.96 * 2 * sqrt(50) * 30 ≈ 830.
        // After the anchoring fix it should be roughly constant ≈ 4 * sigma ≈ 120.
        Assert.IsTrue(lateWidth < 6.0 * sigma * Math.Sqrt(2),
            $"Late-training CI width {lateWidth:F1} indicates the integration anchor was bypassed (expected <= ~{6.0 * sigma * Math.Sqrt(2):F1}).");

        // Sanity: width should not be zero either (ensures noise was injected at all).
        Assert.IsTrue(earlyWidth > 0.1 * sigma,
            $"Early-training CI width {earlyWidth:F1} is near zero; noise injection likely failed.");
    }
}

/// <summary>
/// Structural tests for transform back-transformation: every <c>Predict</c> method must
/// return values on the original user-facing scale (not the transformed or differenced
/// model scale) when <c>TransformType != None</c> or <c>DOrder &gt; 0</c>.
/// </summary>
/// <remarks>
/// Before the fix AR/MA/ARIMA returned transformed-scale predictions and ARIMA never
/// reversed differencing, so plotted predictions silently collapsed to log-space values
/// well outside the raw data range. These tests guard the invariant that Predict returns
/// values in the same order of magnitude as the input time series.
/// </remarks>
[TestClass]
public class PredictBackTransformTests
{
    // Positive-valued fixture appropriate for Box-Cox / Logarithmic transforms.
    private static readonly double[] s_positive = new double[]
    {
        122, 244, 214, 173, 229, 156, 212, 263, 146, 183,
        161, 205, 135, 331, 225, 174,  99, 149, 238, 262,
        132, 235, 216, 240, 230, 192, 195, 172, 173, 172,
        153, 142, 317, 161, 201, 204, 194, 164, 183, 161,
        167, 179, 185, 117, 192, 337, 125, 166,  99, 202
    };

    private static Numerics.Data.TimeSeries MakeTimeSeries() =>
        new(TimeInterval.OneYear, new DateTime(1970, 1, 1), s_positive);

    /// <summary>
    /// AutoRegressive with Logarithmic transform must return values in the original data
    /// range (~100..400), not log-space values (~5).
    /// </summary>
    [TestMethod]
    public void AutoRegressive_Predict_LogTransform_ReturnsOriginalScale()
    {
        var ts = MakeTimeSeries();
        var model = new AutoRegressive(ts, order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        // Parameter scale is now log-space: mu ≈ mean(log(y)) ≈ 5.2, sigma ≈ 0.2.
        double[] pars = { 5.2, 0.3, 0.2 }; // mu, phi1, sigma
        model.SetParameterValues(pars);

        var y = model.Predict(pars, forecastSteps: 0, seed: -1).Y;

        // Raw data lives in ~100..400. Log-space predictions would be ~5.
        // Post-fix predictions must be back on the original scale.
        for (int t = 1; t < ts.Count; t++)
        {
            Assert.IsTrue(y[t] > 10.0 && y[t] < 10000.0,
                $"AR.Predict with Logarithmic must return original-scale values; y[{t}] = {y[t]:F3} looks like log-space.");
        }
    }

    /// <summary>
    /// MovingAverage with Logarithmic transform must return original-scale values.
    /// </summary>
    [TestMethod]
    public void MovingAverage_Predict_LogTransform_ReturnsOriginalScale()
    {
        var ts = MakeTimeSeries();
        var model = new MovingAverage(ts, order: 1, includeIntercept: true)
        {
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        double[] pars = { 5.2, -0.2, 0.2 }; // mu, theta1, sigma
        model.SetParameterValues(pars);

        var y = model.Predict(pars, forecastSteps: 0, seed: -1).Y;

        for (int t = 0; t < ts.Count; t++)
        {
            Assert.IsTrue(y[t] > 10.0 && y[t] < 10000.0,
                $"MA.Predict with Logarithmic must return original-scale values; y[{t}] = {y[t]:F3} looks like log-space.");
        }
    }

    /// <summary>
    /// ARIMA(1,1,0) without transform must integrate (reverse differencing) predictions
    /// back to the original level scale rather than returning per-step differences.
    /// </summary>
    [TestMethod]
    public void ARIMA_Predict_DOrder1_IntegratesBack()
    {
        var ts = MakeTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, dOrder: 1, qOrder: 0, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        // Reasonable ARIMA(1,1,0) parameters: phi_1, sigma.
        double[] pars = { 0.2, 30.0 };
        model.SetParameterValues(pars);

        var y = model.Predict(pars, forecastSteps: 0, seed: -1).Y;

        // After integration, predictions should be on the level scale (≈100..400),
        // NOT on the differenced scale (≈-100..+100 around zero).
        double mean = 0; for (int t = 1; t < ts.Count; t++) mean += y[t];
        mean /= (ts.Count - 1);
        Assert.IsTrue(mean > 50.0,
            $"ARIMA(1,1,0) Predict must integrate to the level scale; average = {mean:F3} looks differenced.");
    }

    /// <summary>
    /// ARIMA(1,1,0) with Logarithmic transform must both integrate and inverse-transform.
    /// </summary>
    [TestMethod]
    public void ARIMA_Predict_DOrder1_LogTransform_ReturnsOriginalScale()
    {
        var ts = MakeTimeSeries();
        var model = new ARIMA(ts, pOrder: 1, dOrder: 1, qOrder: 0, includeIntercept: false)
        {
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        double[] pars = { 0.2, 0.2 }; // phi_1, sigma (log-differenced scale)
        model.SetParameterValues(pars);

        var y = model.Predict(pars, forecastSteps: 0, seed: -1).Y;

        for (int t = 1; t < ts.Count; t++)
        {
            Assert.IsTrue(y[t] > 10.0 && y[t] < 10000.0,
                $"ARIMA(1,1,0) with Logarithmic must return original-scale values; y[{t}] = {y[t]:F3}.");
        }
    }

    /// <summary>
    /// ARIMAX(1,1,0) with Logarithmic transform must return original-scale predictions
    /// covering both the training window and the future forecast tail.
    /// </summary>
    [TestMethod]
    public void ARIMAX_Predict_DiffOrder1_LogTransform_ReturnsOriginalScale()
    {
        var ts = MakeTimeSeries();
        var model = new ARIMAX(ts)
        {
            IncludeIntercept = false,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            TransformType = RMC.BestFit.Models.Transform.Logarithmic
        };
        model.TrainingTimeSteps = ts.Count;
        model.SetDefaultParameters();

        double[] pars = { 0.2, 0.2 }; // phi_1, sigma (log-differenced scale)
        model.SetParameterValues(pars);

        var y = model.Predict(pars, forecastSteps: 5, seed: -1).Y;

        Assert.AreEqual(ts.Count + 5, y.Length);
        for (int t = 0; t < y.Length; t++)
        {
            Assert.IsTrue(y[t] > 10.0 && y[t] < 10000.0,
                $"ARIMAX(1,1,0) with Logarithmic must return original-scale values; y[{t}] = {y[t]:F3}.");
        }
    }
}

/// <summary>
/// Structural tests for ARIMAX covariate extension. Bootstrap/KNN must preserve observed
/// covariate values exactly across realizations so the training-period CI is driven only
/// by the AR/MA noise structure, not by resampled covariate uncertainty.
/// </summary>
/// <remarks>
/// Before the fix <c>ResampleWithBlockBootstrap</c> and <c>ResampleWithKNN</c> were called
/// with the full target length, which regenerated the whole synthetic covariate series,
/// including the observed portion. Different MCMC seeds produced different X[t] in the
/// training window, and <c>beta*X[t]</c> inflated the training-period CI beyond the
/// ±1.96σ predictive-interval width implied by the model alone.
/// </remarks>
[TestClass]
public class ARIMAXCovariateExtensionPreservesObservedTests
{
    private static readonly double[] s_y = new double[]
    {
        1, 3, 2, 4, 5, 3, 4, 5, 6, 4,
        5, 7, 6, 5, 6, 7, 8, 6, 7, 8
    };

    private static readonly double[] s_x = new double[]
    {
        10, 12, 15, 11, 13, 14, 16, 12, 13, 14,
        15, 17, 18, 14, 15, 16, 18, 14, 15, 16
    };

    private static Numerics.Data.TimeSeries MakeY() =>
        new(TimeInterval.OneYear, new DateTime(1970, 1, 1), s_y);

    private static Numerics.Data.TimeSeries MakeX() =>
        new(TimeInterval.OneYear, new DateTime(1970, 1, 1), s_x);

    /// <summary>
    /// Build an ARIMAX(1,0,0) + X with the given extension method and return the covariate
    /// part of the prediction for two different seeds.
    /// </summary>
    private static (double[] covA, double[] covB) RunTwoSeeds(ARIMAX.CovariateExtensionMethod method)
    {
        var y = MakeY();
        var x = MakeX();
        int train = y.Count;

        var model = new ARIMAX(y)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            CovariateExtension = method
        };
        model.TrainingTimeSteps = train;
        model.SetCovariates(new List<Numerics.Data.TimeSeries> { x });
        model.SetDefaultParameters();

        // Layout: mu, beta, phi1, sigma
        double[] pars = { 0.0, 0.3, 0.2, 0.5 };
        model.SetParameterValues(pars);

        int forecast = 10;
        var rA = model.Predict(pars, forecastSteps: forecast, seed: 42);
        var rB = model.Predict(pars, forecastSteps: forecast, seed: 1337);
        return (rA.CovariatePart, rB.CovariatePart);
    }

    /// <summary>
    /// BlockBootstrap must preserve observed covariate values (and therefore the beta*X[t]
    /// contribution) exactly in the training window. Only the forecast tail may differ
    /// between seeds.
    /// </summary>
    [TestMethod]
    public void ARIMAX_BlockBootstrap_PreservesObservedCovariates()
    {
        var (covA, covB) = RunTwoSeeds(ARIMAX.CovariateExtensionMethod.BlockBootstrap);
        int train = s_y.Length;

        for (int t = 0; t < train; t++)
        {
            Assert.AreEqual(covA[t], covB[t], 1e-12,
                $"Training-period covariate contribution must not vary with seed at t={t} (BlockBootstrap).");
        }

        // At least one forecast-period index should differ so we know bootstrap is actually
        // drawing different tails per seed (otherwise the test would pass vacuously).
        bool anyDiffer = false;
        for (int t = train; t < covA.Length; t++)
            if (Math.Abs(covA[t] - covB[t]) > 1e-9) { anyDiffer = true; break; }
        Assert.IsTrue(anyDiffer, "Expected bootstrap to produce different forecast-tail covariates between seeds.");
    }

    /// <summary>
    /// KNN must preserve observed covariate values exactly across seeds in the training
    /// window. Only the forecast tail may differ.
    /// </summary>
    [TestMethod]
    public void ARIMAX_KNN_PreservesObservedCovariates()
    {
        var (covA, covB) = RunTwoSeeds(ARIMAX.CovariateExtensionMethod.KNN);
        int train = s_y.Length;

        for (int t = 0; t < train; t++)
        {
            Assert.AreEqual(covA[t], covB[t], 1e-12,
                $"Training-period covariate contribution must not vary with seed at t={t} (KNN).");
        }

        bool anyDiffer = false;
        for (int t = train; t < covA.Length; t++)
            if (Math.Abs(covA[t] - covB[t]) > 1e-9) { anyDiffer = true; break; }
        Assert.IsTrue(anyDiffer, "Expected KNN to produce different forecast-tail covariates between seeds.");
    }

    /// <summary>
    /// In deterministic mode (Predict with seed = -1), the forecast trace must NOT depend on
    /// the CovariateExtension choice. Bootstrap and KNN inject different stochastic covariate
    /// paths in the forecast tail; the "Posterior Mean" / "Posterior Mode" line in the App
    /// reads <c>ModeCurve</c> from a seed = -1 Predict, so a method-dependent deterministic
    /// trace would just be displaying a single random covariate draw. After the fix, seed = -1
    /// fills the forecast tail with each covariate's empirical mean and yields exactly the
    /// same Y values regardless of CovariateExtension.
    /// </summary>
    [TestMethod]
    public void ARIMAX_Deterministic_CovariateExtension_IsInvariant()
    {
        var (yBootstrap, _) = RunDeterministicPredict(ARIMAX.CovariateExtensionMethod.BlockBootstrap);
        var (yKNN, _)       = RunDeterministicPredict(ARIMAX.CovariateExtensionMethod.KNN);

        Assert.AreEqual(yBootstrap.Length, yKNN.Length);
        for (int t = 0; t < yBootstrap.Length; t++)
        {
            Assert.AreEqual(yBootstrap[t], yKNN[t], 1e-12,
                $"Deterministic Predict (seed = -1) must be invariant to CovariateExtension at t={t}; " +
                $"BlockBootstrap={yBootstrap[t]:F6}, KNN={yKNN[t]:F6}.");
        }
    }

    /// <summary>
    /// Build the canonical ARIMAX(1,0,0)+X test fixture, run a deterministic Predict, and
    /// return Y plus the covariate contribution.
    /// </summary>
    private static (double[] Y, double[] CovariatePart) RunDeterministicPredict(ARIMAX.CovariateExtensionMethod method)
    {
        var y = MakeY();
        var x = MakeX();
        int train = y.Count;

        var model = new ARIMAX(y)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false,
            UseJeffreysRuleForScale = false,
            CovariateExtension = method
        };
        model.TrainingTimeSteps = train;
        model.SetCovariates(new List<Numerics.Data.TimeSeries> { x });
        model.SetDefaultParameters();

        double[] pars = { 0.0, 0.3, 0.2, 0.5 };
        model.SetParameterValues(pars);

        var r = model.Predict(pars, forecastSteps: 10, seed: -1);
        return (r.Y, r.CovariatePart);
    }

    /// <summary>
    /// Regression test against the KNN off-by-one bug in
    /// <c>Numerics.Data.TimeSeries.ResampleWithKNN</c>. The pre-fix implementation took the
    /// neighbor's own value rather than x[j+1], which collapsed the resampled tail to a
    /// near-constant. As a result, the <c>beta * X[t]</c> contribution had near-zero variance
    /// in the forecast tail under KNN, while BlockBootstrap retained the full marginal
    /// variability — producing visibly different forecast envelopes (KNN cone vs Bootstrap
    /// flat band) on the same data. Post-fix, the two methods should produce forecast-tail
    /// covariate variability of comparable magnitude.
    /// </summary>
    [TestMethod]
    public void ARIMAX_BlockBootstrap_vs_KNN_ForecastsComparable()
    {
        var (covBootstrap, _) = RunTwoSeeds(ARIMAX.CovariateExtensionMethod.BlockBootstrap);
        var (covKNN, _)       = RunTwoSeeds(ARIMAX.CovariateExtensionMethod.KNN);

        int train = s_y.Length;
        var bootstrapTail = covBootstrap.Skip(train).ToArray();
        var knnTail       = covKNN.Skip(train).ToArray();

        double varB = SampleVariance(bootstrapTail);
        double varK = SampleVariance(knnTail);
        double ratio = Math.Max(varB, varK) / Math.Max(1e-9, Math.Min(varB, varK));

        // Pre-fix: ratio is large (KNN tail collapses to near-constant ⇒ varK ≪ varB).
        // Post-fix: both tails carry the same marginal scale ⇒ ratio ≲ 5x even on the
        //           short n=20 fixture where small-sample variance is noisy.
        Assert.IsTrue(ratio < 5.0,
            $"BlockBootstrap and KNN should produce forecast-tail covariate variability of comparable magnitude " +
            $"after the KNN off-by-one fix. varBootstrap={varB:F4}, varKNN={varK:F4}, ratio={ratio:F2}.");
    }

    private static double SampleVariance(double[] x)
    {
        if (x.Length < 2) return 0;
        double m = x.Average();
        double sumSq = 0;
        for (int i = 0; i < x.Length; i++)
        {
            double d = x[i] - m;
            sumSq += d * d;
        }
        return sumSq / (x.Length - 1);
    }
}

/// <summary>
/// Validation tests for <see cref="RMC.BestFit.Analyses.TimeSeries.ARIMAXAnalysis.Validate"/>.
/// Forecasting with <c>CovariateExtension = None</c> is only valid when the covariate is
/// long enough to cover the full (training + validation + forecast) horizon; otherwise
/// the analysis must surface an error message so the UI disables Run rather than
/// throwing at Predict time.
/// </summary>
[TestClass]
public class ARIMAXAnalysisValidateCovariateExtensionTests
{
    /// <summary>
    /// When CovariateExtension = None and ForecastingTimeSteps > 0 with a covariate shorter
    /// than (TimeSeries.Count + ForecastingTimeSteps), Validate must return isValid = false
    /// and a message referencing CovariateExtension.
    /// </summary>
    [TestMethod]
    public void Validate_NoneAndForecast_WithShortCovariate_ReturnsError()
    {
        var values = Enumerable.Range(0, 40).Select(i => 50.0 + 0.1 * i).ToArray();
        var y = new Numerics.Data.TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);
        var x = new Numerics.Data.TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 40).Select(i => 1.0 + 0.05 * i).ToArray());

        var model = new ARIMAX(y)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.TrainingTimeSteps = y.Count;
        model.SetCovariates(new List<Numerics.Data.TimeSeries> { x });
        model.SetDefaultParameters();

        var analysis = new RMC.BestFit.Analyses.ARIMAXAnalysis(model)
        {
            ForecastingTimeSteps = 5
        };

        var result = analysis.Validate();

        Assert.IsFalse(result.IsValid, "Validate should return false for None + forecast with insufficient covariate.");
        Assert.IsTrue(result.ValidationMessages.Any(m => m.Contains("CovariateExtension")),
            "Validation message should reference CovariateExtension. Messages: " + string.Join(" | ", result.ValidationMessages));
    }

    /// <summary>
    /// When CovariateExtension = None but the covariate already covers the full horizon,
    /// Validate must NOT raise the covariate extension error.
    /// </summary>
    [TestMethod]
    public void Validate_NoneAndForecast_WithLongEnoughCovariate_NoCovariateError()
    {
        var values = Enumerable.Range(0, 40).Select(i => 50.0 + 0.1 * i).ToArray();
        var y = new Numerics.Data.TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1), values);

        // Covariate is 45 long — covers 40 observed + 5 forecast.
        var x = new Numerics.Data.TimeSeries(TimeInterval.OneMonth, new DateTime(2000, 1, 1),
            Enumerable.Range(0, 45).Select(i => 1.0 + 0.05 * i).ToArray());

        var model = new ARIMAX(y)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false,
            CovariateExtension = ARIMAX.CovariateExtensionMethod.None
        };
        model.TrainingTimeSteps = y.Count;
        model.SetCovariates(new List<Numerics.Data.TimeSeries> { x });
        model.SetDefaultParameters();

        var analysis = new RMC.BestFit.Analyses.ARIMAXAnalysis(model)
        {
            ForecastingTimeSteps = 5
        };

        var result = analysis.Validate();

        Assert.IsFalse(result.ValidationMessages.Any(m => m.Contains("CovariateExtension")),
            "Validate should not emit a covariate-extension error when the covariate is long enough. Messages: " + string.Join(" | ", result.ValidationMessages));
    }
}
