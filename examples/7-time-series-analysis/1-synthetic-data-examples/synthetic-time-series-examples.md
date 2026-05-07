# synthetic-time-series-examples

## Overview

Synthetic time-series datasets covering trend (linear, quadratic, cubic, sinusoidal), AR / MA / ARMA processes, and a log-transformed series with linear trend. Each series has a paired Time Series Analysis fit using the corresponding model for verifying parameter recovery.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Intercept` | Synthetic series with constant mean only (no trend, no autocorrelation). |
| `Intercept + Linear Trend` | Synthetic series with constant mean + linear trend. |
| `Intercept + Quadratic Trend` | Synthetic series with constant mean + quadratic trend. |
| `Intercept + Cubic Trend` | Synthetic series with constant mean + cubic trend. |
| `Intercept + Sinusoidal Trend` | Synthetic series with constant mean + sinusoidal (annual cycle) trend. |
| `AR(1)` | Synthetic AR(1) series for AR-fit verification. |
| `AR(3)` | Synthetic AR(3) series for AR-fit verification. |
| `MA(1)` | Synthetic MA(1) series for MA-fit verification. |
| `MA(3)` | Synthetic MA(3) series for MA-fit verification. |
| `ARMA(1,1)` | Synthetic ARMA(1,1) series for ARMA-fit verification. |
| `ARMA(2,2)` | Synthetic ARMA(2,2) series for ARMA-fit verification. |
| `Intercept + Linear Trend + ARMA(1,1)` | Synthetic series with constant mean + linear trend + ARMA(1,1) residuals. |
| `Intercept + Linear Trend + LogTransform` | Log-normal synthetic series with constant mean + linear trend (in log space). |

### Time Series Analysis

| Element | Description |
|---|---|
| `Intercept` | Constant-mean fit on the intercept-only synthetic series. |
| `Intercept + Linear Trend` | Linear-trend fit on the intercept + linear trend synthetic series. |
| `Intercept + Quadratic Trend` | Quadratic-trend fit on the intercept + quadratic trend synthetic series. |
| `Intercept + Cubic Trend` | Cubic-trend fit on the intercept + cubic trend synthetic series. |
| `Intercept + Sinusoidal Trend` | Sinusoidal-trend fit on the intercept + sinusoidal trend synthetic series. |
| `AR(1)` | AR(1) fit on the AR(1) synthetic series. |
| `AR(3)` | AR(3) fit on the AR(3) synthetic series. |
| `MA(1)` | MA(1) fit on the MA(1) synthetic series. |
| `MA(3)` | MA(3) fit on the MA(3) synthetic series. |
| `ARMA(1,1)` | ARMA(1,1) fit on the ARMA(1,1) synthetic series. |
| `ARMA(2,2)` | ARMA(2,2) fit on the ARMA(2,2) synthetic series. |
| `Intercept + Linear Trend + ARMA(1,1)` | Linear-trend + ARMA(1,1) fit on the corresponding synthetic series. |
| `Intercept + Linear Trend + LogTransform` | Linear-trend fit with logarithmic transform on the log-normal synthetic series. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/7-time-series-analysis/1-synthetic-data-examples/`.
3. Open `synthetic-time-series-examples.bestfit`.

### Exploring the Elements

For each Time Series Analysis alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Time Series** tab to view the fitted mean (and trend, if any) overlaid on the observations.
3. Open the **Residual ACF / PACF** tabs to confirm white-noise residuals.
4. Adjust **ForecastSteps** in the Properties panel to extend the forecast horizon.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any alternative to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results

<!-- Replace placeholder content as you capture screenshots and copy values out of the BestFit GUI. -->

### Parameter Estimates

<!-- TODO: paste the parameter-estimate table from the MCMC report (right-click the alternative > Open MCMC Report) -->

| Alternative | Parameter | Mean | Median | Lower CI | Upper CI | R-hat | ESS |
|---|---|---|---|---|---|---|---|
| _(placeholder)_ |  |  |  |  |  |  |  |

### Frequency / Quantile Table

<!-- TODO: paste the AEP / return-period table from the Frequency tab (right-click the chart > Copy Table). -->

| AEP (%) | Return Period (yr) | Median | Lower CI | Upper CI |
|---|---|---|---|---|
| 50    | 2     |  |  |  |
| 10    | 10    |  |  |  |
| 1     | 100   |  |  |  |
| 0.5   | 200   |  |  |  |
| 0.2   | 500   |  |  |  |

### Plots

![Time-series fit overlaid on the observations, with credible band.](images/synthetic-ts-time-series.png)
*Figure: Time-series fit overlaid on the observations, with credible band.*

![Multi-step forecast extension with credible band.](images/synthetic-ts-forecast.png)
*Figure: Multi-step forecast extension with credible band.*

![Residual autocorrelation function.](images/synthetic-ts-acf.png)
*Figure: Residual autocorrelation function.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Use the fitted ARIMA / regression model for **multi-step forecasting** with credible bands.
- Inspect residual ACF / PACF to confirm no remaining temporal structure.
- For non-stationary trend cases, project the trend function out beyond the observation window.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
