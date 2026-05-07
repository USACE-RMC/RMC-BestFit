# Time Series Analysis Examples

This chapter demonstrates ARIMA / ARIMAX / regression fitting on hydrologic and economic time-series data. Unlike the univariate-frequency workflow (chapter 4), Time Series Analysis preserves the temporal structure of the observations and produces parameter estimates plus multi-step forecasts.

## Sub-Chapters

| Sub-Chapter | Data Source | What It Demonstrates |
|---|---|---|
| [1-synthetic-data-examples](1-synthetic-data-examples/) | Synthetic | Trend, AR, MA, ARMA, and log-transformed series with known ground truth |
| [2-classic-time-series-examples](2-classic-time-series-examples/) | Box-Jenkins, NOAA GML, Cobb (1978) | Three published classics: airline passengers, Mauna Loa CO2, Nile River flows |
| [3-time-series-regression-example](3-time-series-regression-example/) | US macroeconomic indicators | Simple and multiple linear regression with autocorrelated residuals |

## Examples

| Example | Sub-Chapter | Description |
|---|---|---|
| [Synthetic Time Series Examples](1-synthetic-data-examples/synthetic-time-series-examples.md) | 1- | Thirteen synthetic series + matched fits for verifying parameter recovery |
| [Classic Time Series Examples](2-classic-time-series-examples/classic-time-series-examples.md) | 2- | Three classic datasets used throughout the time-series literature |
| [Time Series Regression Example](3-time-series-regression-example/time-series-regression-example.md) | 3- | US macro indicators fit with simple and multiple regression |

## Supported Models

| Model | Order | Use |
|---|---|---|
| **Constant Mean** | — | Stationary series with constant level |
| **Linear / Quadratic / Cubic Trend** | — | Polynomial trends |
| **Sinusoidal Trend** | — | Annual / sub-annual cycles |
| **AR(p)** | p | Autoregressive |
| **MA(q)** | q | Moving average |
| **ARMA(p, q)** | p, q | Combined autoregressive + moving average |
| **ARIMA(p, d, q)** | p, d, q | ARMA with d differencing steps |
| **ARIMAX(p, d, q)** | p, d, q + covariates | ARIMA with exogenous covariates |
| **Linear Regression with ARMA residuals** | — | Time-series regression on covariates |

## Workflow Overview

```
Time Series Data (raw observations)
         |
         |  -->  Time Series Analysis (model selection + fit)
         |          |
         |          +-->  Parameter posterior
         |          +-->  Residual diagnostics (ACF / PACF)
         |          +-->  Multi-step forecast with credible band
```

Each Time Series Analysis element references **one** Time Series Data element as the response. ARIMAX and regression elements additionally reference one or more covariate Time Series Data elements.

## How to Use These Examples

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to a sub-chapter folder.
3. Open the `.bestfit` file.
4. Expand **Time Series Data** to see the input series.
5. Expand **Time Series Analysis** to see the fits.
6. Click an analysis to view the fitted mean, residual ACF / PACF, and forecast.
7. Open the matching `.md` tutorial for a step-by-step guide.

## Training-Window Configuration

By default, RMC-BestFit reserves the **last 20% of each series for validation** (`UseDefaultTrainingSteps = true`). For maximum-likelihood parity with R's `arima()` and statsmodels, set `UseDefaultTrainingSteps = false` and `TrainingTimeSteps = data length` in the Properties panel.

## Screenshot Images

Capture screenshots into `images/` subfolders next to each example. Naming convention: `<example-slug>-<view-name>.png` (e.g., `nile-time-series.png`, `synthetic-ts-arma11-residuals.png`).

## Next Steps

After fitting a time-series model:

- **Use forecasts as input** to a Univariate Distribution Analysis (chapter 4) for forward-looking flood-frequency.
- **Pair two correlated series** in a Bivariate Distribution Analysis (chapter 5) for joint inference.
- **Re-fit with informative priors** when prior engineering judgment is available about parameter ranges.
