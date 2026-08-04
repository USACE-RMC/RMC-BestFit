using System.Linq;
using Numerics.Data;
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using RMC.BestFit.Verification.Datasets;
using RMC.BestFit.Verification.Datasets.TimeSeriesData;

namespace RMC.BestFit.Verification.TimeSeriesAnalysis;

/// <summary>
/// Verifies maximum-likelihood parameter recovery for the <see cref="ARIMAX"/> model
/// against deterministic synthetic generating parameters and committed R reference values.
/// </summary>
[TestClass]
public class ARIMAXMLERecoveryTests
{
    #region MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX(1,1) parameters against known true values from synthetic data.
    /// ARIMAX is configured with no transforms, differencing, seasonality, or covariates.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX11()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX11Data(10, 0.6, 0.3, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters (10% tolerance for mixed models)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(2,1) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX21()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX21Data(10, 0.5, -0.3, 0.3, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(1,2) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX12()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX12Data(10, 0.5, 0.3, 0.5, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 2,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX(2,2) parameters against known true values from synthetic data.
    /// Validates that the optimizer can recover the generating parameters within 10% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX22()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX22Data(10, 0.5, -0.3, 0.3, -0.2, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            MAOrderQ = 2,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure AR(1) data when configured with p=1, q=0.
    /// Validates that ARIMAX subsumes AR as a special case.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX_FitsAR1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_AR1Data(10, 0.6, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests that ARIMAX model can fit pure MA(1) data when configured with p=0, q=1.
    /// Validates that ARIMAX subsumes MA as a special case.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMAX_FitsMA1()
    {
        var data = SyntheticTimeSeriesData.GetARIMAX_MA1Data(10, 0.5, 5, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.05), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region ARIMA (Differencing) MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,1,0) parameters against known true values from synthetic data.
    /// ARIMA(1,1,0) is an AR(1) model applied to first-differenced data.
    /// Validates that the optimizer can recover the generating parameters within 15% tolerance.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA110()
    {
        var data = SyntheticTimeSeriesData.GetARIMA110Data(0.5, 0.6, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,1,0) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance for differenced models)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(0,1,1) parameters against known true values from synthetic data.
    /// ARIMA(0,1,1) is an IMA(1,1) model - MA(1) applied to first-differenced data.
    /// Common model for exponential smoothing equivalents.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA011()
    {
        var data = SyntheticTimeSeriesData.GetARIMA011Data(0.3, 0.5, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(0,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,1,1) parameters against known true values from synthetic data.
    /// ARIMA(1,1,1) is the classic Box-Jenkins model combining AR, differencing, and MA components.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA111()
    {
        var data = SyntheticTimeSeriesData.GetARIMA111Data(0.3, 0.6, 0.4, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(1,2,0) parameters against known true values from synthetic data.
    /// ARIMA(1,2,0) uses second-order differencing, suitable for data with quadratic trends.
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA120()
    {
        var data = SyntheticTimeSeriesData.GetARIMA120Data(0.1, 0.5, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            DiffOrderD = 2,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(1,2,0) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for second-order diff)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMA(2,1,1) parameters against known true values from synthetic data.
    /// Higher-order ARIMA model with AR(2), first-order differencing, and MA(1).
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARIMA211()
    {
        var data = SyntheticTimeSeriesData.GetARIMA211Data(0.2, 0.5, -0.25, 0.3, 2.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 2,
            DiffOrderD = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMA(2,1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend Model MLE Estimation Tests

    #region Standalone Trend Tests (No AR/MA Components)

    /// <summary>
    /// Tests MLE estimation of linear trend only (no AR/MA components).
    /// Parameters: [μ, γ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_LinearTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetLinearTrendData(100.0, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Linear trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (10% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of quadratic trend only (no AR/MA components).
    /// Parameters: [μ, γ1, γ2, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_QuadraticTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetQuadraticTrendData(100.0, 0.5, 0.001, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Quadratic,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Quadratic trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of cubic trend only (no AR/MA components).
    /// Parameters: [μ, γ1, γ2, γ3, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_CubicTrend_Only()
    {
        var data = SyntheticTimeSeriesData.GetCubicTrendData(100.0, 0.3, 0.001, 0.000001, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Cubic,
            AROrderP = 0,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Cubic trend only model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for more complex model)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend Tests with AR/MA Components

    /// <summary>
    /// Tests MLE estimation of AR(1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1LinearTrendData(100.0, 0.5, 0.6, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with quadratic trend parameters against known true values.
    /// Parameters: [μ, γ1 (linear), γ2 (quadratic), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_QuadraticTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1QuadraticTrendData(100.0, 0.5, 0.001, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Quadratic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Quadratic trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for quadratic)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with cubic trend parameters against known true values.
    /// Parameters: [μ, γ1 (linear), γ2 (quadratic), γ3 (cubic), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_CubicTrend()
    {
        var data = SyntheticTimeSeriesData.GetAR1CubicTrendData(100.0, 0.3, 0.001, 0.000001, 0.4, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Cubic,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Cubic trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (25% tolerance for cubic)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetMA1LinearTrendData(100.0, 0.5, 0.5, 5.0, 500);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "MA(1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with linear trend parameters against known true values.
    /// Parameters: [μ, γ (slope), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_LinearTrend()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11LinearTrendData(100.0, 0.5, 0.5, 0.3, 5.0, 1000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) + Linear trend model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #endregion

    #region Seasonality MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of AR(1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetAR1SeasonalData(100.0, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of MA(1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetMA1SeasonalData(100.0, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 0,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "MA(1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with Fourier seasonality parameters against known true values.
    /// Parameters: [μ, ψ1 (sin), ψ2 (cos), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_Seasonal()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11SeasonalData(100.0, 20.0, 10.0, 0.5, 0.3, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (15% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.15), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of AR(1) with linear trend and Fourier seasonality.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_TrendSeasonal()
    {
        var data = SyntheticTimeSeriesData.GetAR1TrendSeasonalData(100.0, 0.3, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "AR(1) + Trend + Seasonal model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance for combined)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Trend + Seasonality (No Differencing) MLE Estimation Tests


    #endregion

    #region Covariate (ARIMAX) MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and AR(1) errors.
    /// Parameters: [μ, β, φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetAR1WithCovariateData(50.0, 2.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + AR(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and MA(1) errors.
    /// Parameters: [μ, β, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_MA1_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetMA1WithCovariateData(50.0, 2.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + MA(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with a single covariate and ARMA(1,1) errors.
    /// Parameters: [μ, β, φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_WithCovariate()
    {
        var data = SyntheticTimeSeriesData.GetARIMA11WithCovariateData(50.0, 2.0, 0.5, 0.3, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(new List<TimeSeries> { data.Covariate });
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with single covariate + ARMA(1,1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARIMAX with two covariates and AR(1) errors.
    /// Parameters: [μ, β1, β2, φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_AR1_WithTwoCovariates()
    {
        var data = SyntheticTimeSeriesData.GetAR1WithTwoCovariatesData(50.0, 2.0, -1.5, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        model.SetCovariates(data.Covariates);
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "ARIMAX with two covariates + AR(1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (20% tolerance)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.20), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region Full Combination MLE Estimation Tests

    /// <summary>
    /// Tests MLE estimation of ARIMAX with trend, seasonality, and AR(1).
    /// A comprehensive test combining multiple features.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_Full_TrendSeasonalAR1()
    {
        var data = SyntheticTimeSeriesData.GetFullARIMAX_TrendSeasonalAR1Data(100.0, 0.3, 20.0, 10.0, 0.5, 5.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            AROrderP = 1,
            MAOrderQ = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Full ARIMAX (Trend + Seasonal + AR1) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (25% tolerance for complex)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.25), $"Estimated parameter {i} is incorrect.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) with trend and seasonality (no differencing).
    /// Comprehensive combination test without differencing since it removes trends.
    /// Parameters: [μ, γ (slope), ψ1 (sin), ψ2 (cos), φ, θ, σ].
    /// </summary>
    [TestMethod]
    public void Test_EstimateParameters_Full_TrendSeasonalARMA11()
    {
        var data = SyntheticTimeSeriesData.GetFullARIMAX_TrendSeasonalARIMA11Data(100.0, 0.2, 15.0, 8.0, 0.4, 0.3, 3.0, 10000);
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            TrendType = ARIMAX.Trend.Linear,
            IncludeSeasonality = true,
            DiffOrderD = 0,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
        mle.Estimate();

        // Assert that the model was fitted successfully
        Assert.AreEqual(true, mle.IsEstimated, "Full ARIMAX (Trend + Seasonal + ARMA11) model fitting failed.");
        // Assert that estimated parameters are close to true parameters (30% tolerance for most complex)
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.30), $"Estimated parameter {i} is incorrect.");
        }
    }

    #endregion

    #region R Validation Tests

    /// <summary>
    /// Tests MLE estimation of ARMA(1,1) parameters on real airline passenger data against R's arima() results.
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
    /// <para>
    /// This test validates that ARIMAX configured with AROrderP=1, MAOrderQ=1, and no covariates
    /// produces estimates comparable to R's arima() function. A 10% tolerance accounts for
    /// differences in optimization algorithms and the complexity of mixed ARMA models.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_ARMA11_RValidation()
    {
        var data = RealTimeSeriesData.GetAirlinePassengerData_ARMA11_RTest();
        var model = new ARIMAX(data.TimeSeries)
        {
            IncludeIntercept = true,
            AROrderP = 1,
            MAOrderQ = 1,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.TimeSeries.Count;
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "ARMA(1,1) model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of simple linear regression on real macroeconomic data against R's lm() results.
    /// Validates that RMC-BestFit's ARIMAX model (configured as regression) produces comparable estimates to R.
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
    /// <para>
    /// Model: Consumption = a + b × Income + ε
    /// </para>
    /// <para>
    /// This test validates ARIMAX configured with AROrderP=0, MAOrderQ=0, and Income as a single covariate.
    /// A 10% tolerance accounts for differences in optimization algorithms.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_SimpleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetSimpleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(new List<TimeSeries> { data.X });
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "Simple regression model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    /// <summary>
    /// Tests MLE estimation of multiple linear regression on real macroeconomic data against R's lm() results.
    /// Validates that RMC-BestFit's ARIMAX model produces comparable estimates to R for multiple predictors.
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
    /// <para>
    /// Model: Consumption = a + β₁×Income + β₂×Production + β₃×Savings + β₄×Unemployment + ε
    /// </para>
    /// <para>
    /// This test validates ARIMAX configured with AROrderP=0, MAOrderQ=0, and four covariates.
    /// A 10% tolerance accounts for differences in optimization algorithms.
    /// </para>
    /// </remarks>
    [TestMethod]
    public void Test_EstimateParameters_MultipleRegression_RValidation()
    {
        var data = RealTimeSeriesData.GetMultipleLinearRegression_RTest();
        var model = new ARIMAX(data.Y)
        {
            IncludeIntercept = true,
            AROrderP = 0,
            MAOrderQ = 0,
            XOrderB = 0,
            UseDefaultTrainingSteps = false
        };
        model.TrainingTimeSteps = data.Y.Count;
        model.SetCovariates(data.X);
        var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
        mle.Estimate();

        Assert.AreEqual(true, mle.IsEstimated, "Multiple regression model fitting failed.");
        for (int i = 0; i < model.NumberOfParameters; i++)
        {
            Assert.AreEqual(data.TrueParameters[i], mle.BestParameterSet.Values[i], Math.Abs(data.TrueParameters[i] * 0.10),
                $"Parameter {i} ({model.Parameters[i].Name}) differs from R estimate.");
        }
    }

    #endregion
}
