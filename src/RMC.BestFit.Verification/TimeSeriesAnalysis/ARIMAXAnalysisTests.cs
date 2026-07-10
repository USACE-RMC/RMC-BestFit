using Numerics.Data;
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Computational verification tests for the <see cref="ARIMAXAnalysis"/> class.
/// </summary>
/// <remarks>
/// <para>
///     <b>Authors:</b>
/// </para>
/// <list type="bullet">
///     <item>Haden Smith, USACE Risk Management Center, cole.h.smith@usace.army.mil</item>
/// </list>
/// <para>
/// The ARIMAX (Autoregressive Moving Average with eXogenous variables) model extends ARMA by
/// adding exogenous predictors, deterministic trend, Fourier seasonality, ARIMA differencing,
/// and Box-Cox / Yeo-Johnson transformations. Model structure:
/// Y(t) = μ + γ(t) + ψ(t) + β·X(t) + φ·Y(t-p) + θ·ε(t-q) + ε(t).
/// </para>
/// <para>
/// All tests in this class run a Bayesian MCMC chain. They are SLOW and live in the
/// Verification project. Programmatic tests (constructor, property round-trip,
/// validation, serialization, model-property propagation, trend / seasonality /
/// covariate / differencing / transform configuration, event wiring, cancellation)
/// live in <c>RMC.BestFit.Verification.TimeSeriesAnalysis</c>.
/// </para>
/// </remarks>
[TestClass]
public class ARIMAXAnalysisTests
{
    #region Estimation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMAX(1,1) parameters against known true values from synthetic data.
    /// Uses a 500-observation time series and validates that the posterior mode recovers the generating
    /// parameters within 25% tolerance, accounting for Monte Carlo variability.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The ARIMAX(1,1) model: Y(t) = μ + φ₁(Y(t-1) - μ) + ε(t) + θ₁ε(t-1), where ε(t) ~ N(0, σ²).
    /// </para>
    /// <para>True parameters: μ = 10, φ₁ = 0.6, θ₁ = 0.3, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX11()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX11Data(10, 0.6, 0.3, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMAX(2,1) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX21()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX21Data(10, 0.5, -0.3, 0.3, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMAX(1,2) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, φ₁ = 0.5, θ₁ = 0.3, θ₂ = 0.5, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX12()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX12Data(10, 0.5, 0.3, 0.5, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 2,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMAX(2,2) parameters against known true values from synthetic data.
    /// 40% tolerance because mixed higher-order ARMA models are harder to identify.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, φ₁ = 0.5, φ₂ = -0.3, θ₁ = 0.3, θ₂ = -0.2, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX22()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX22Data(10, 0.5, -0.3, 0.3, -0.2, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 2,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.40), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure AR(1) data when configured with p=1, q=0 using Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, φ₁ = 0.6, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX_FitsAR1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_AR1Data(10, 0.6, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure MA(1) data when configured with p=0, q=1 using Bayesian estimation.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 10, θ₁ = 0.5, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMAX_FitsMA1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_MA1Data(10, 0.5, 5, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(1,1,0) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 0.5, φ₁ = 0.6, σ = 2.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMA110()
    {
        var data = SyntheticTimeSeriesData.GetARIMA110Data(0.5, 0.6, 2.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "ARIMA(1,1,0) Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.30), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(0,1,1) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 0.3, θ₁ = 0.5, σ = 2.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMA011()
    {
        var data = SyntheticTimeSeriesData.GetARIMA011Data(0.3, 0.5, 2.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "ARIMA(0,1,1) Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.30), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARIMA(1,1,1) parameters against known true values from synthetic data.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 0.3, φ₁ = 0.6, θ₁ = 0.4, σ = 2.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARIMA111()
    {
        var data = SyntheticTimeSeriesData.GetARIMA111Data(0.3, 0.6, 0.4, 2.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "ARIMA(1,1,1) Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.40), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of linear trend parameters using ARIMAX.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 100, γ = 0.5, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_LinearTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetLinearTrendData(100.0, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 0,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Linear trend Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of AR(1) model with linear trend using ARIMAX.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 100, γ = 0.5, φ₁ = 0.6, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_AR1_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1LinearTrendData(100.0, 0.5, 0.6, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "AR(1) with linear trend Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of AR(1) model with Fourier seasonality using ARIMAX.
    /// </summary>
    /// <remarks>
    /// <para>True parameters: μ = 100, ψ₁ = 20, ψ₂ = 10, φ₁ = 0.5, σ = 5.</para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_AR1_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetAR1SeasonalData(100.0, 20.0, 10.0, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "AR(1) with seasonality Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests Bayesian MCMC estimation of ARMA(1,1) parameters on real airline passenger data against R's arima() results.
    /// Validates that RMC-BestFit's ARIMAX model (configured as ARMA) produces comparable parameter estimates to R.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Classic Box-Jenkins airline passenger dataset (144 monthly observations, 1949-1960).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- arima(AirPassengers, order = c(1, 0, 1))
    /// # intercept = 315.2158, ar1 = 0.9325, ma1 = 0.4273, sigma = 30.99516
    /// </code>
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_ARMA11_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_ARMA11_RTest();
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "ARMA(1,1) Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.15),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of simple linear regression on real macroeconomic data against R's lm() results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Quarterly US macroeconomic data - Consumption regressed on Income (188 observations, 1970-2016).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- lm(consumption ~ income)
    /// # intercept = 0.54510, income = 0.28060, sigma = 0.6026
    /// </code>
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_SimpleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetSimpleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(new List<TimeSeries> { data.X });

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Simple regression Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.15),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests Bayesian MCMC estimation of multiple linear regression on real macroeconomic data against R's lm() results.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Data: Quarterly US macroeconomic data - Consumption regressed on Income, Production, Savings, and
    /// Unemployment (188 observations, 1970-2016).
    /// </para>
    /// <para>
    /// R code:
    /// <code>
    /// fit &lt;- lm(consumption ~ income + production + savings + unemployment)
    /// # intercept = 0.26729, income = 0.71449, production = 0.04589,
    /// # savings = -0.04527, unemployment = -0.20477, sigma = 0.3286
    /// </code>
    /// </para>
    /// </remarks>
    [TestMethod]
    public async Task Test_EstimateParameters_MultipleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetMultipleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseJeffreysRuleForScale = false,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(data.X);

        var analysis = new ARIMAXAnalysis(model);
        await analysis.RunAsync();

        Assert.AreEqual(true, analysis.IsEstimated, "Multiple regression Bayesian estimation failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], analysis.BayesianAnalysis.Results!.MAP.Values[i], Math.Abs(data.TrueParameters[i] * 0.15),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    #endregion
}
