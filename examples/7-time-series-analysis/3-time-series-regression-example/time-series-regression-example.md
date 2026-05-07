# time-series-regression-example

## Overview

Multivariate time-series regression on classic US macroeconomic indicators (consumption, income, production, savings, unemployment). Demonstrates simple and multiple linear regression with autocorrelated residuals.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Consumption` | Quarterly US personal consumption expenditures (response variable for the regression examples). |
| `Income` | Quarterly US personal disposable income (regressor). |
| `Production` | Quarterly US industrial production index (regressor). |
| `Savings` | Quarterly US personal savings rate (regressor). |
| `Unemployment` | Quarterly US unemployment rate (regressor). |

### Time Series Analysis

| Element | Description |
|---|---|
| `Simple Linear Regression` | Simple linear regression of consumption on income, with autocorrelated residuals modeled as ARMA. |
| `Multiple Linear Regression` | Multiple linear regression of consumption on income, production, savings, and unemployment, with autocorrelated residuals modeled as ARMA. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/7-time-series-analysis/3-time-series-regression-example/`.
3. Open `time-series-regression-example.bestfit`.

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

![Time-series fit overlaid on the observations, with credible band.](images/ts-regression-time-series.png)
*Figure: Time-series fit overlaid on the observations, with credible band.*

![Multi-step forecast extension with credible band.](images/ts-regression-forecast.png)
*Figure: Multi-step forecast extension with credible band.*

![Residual autocorrelation function.](images/ts-regression-acf.png)
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
