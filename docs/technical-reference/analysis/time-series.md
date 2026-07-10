# Time Series Analysis

[<- Previous: Rating Curve](rating-curve.md) | [Back to Index](../../index.md) | [Next: Spatial Extremes ->](../spatial/spatial-extremes.md)

Time series analysis is the statistical study of data points collected sequentially over time. Unlike cross-sectional data where observations are assumed independent, time series data exhibit temporal dependence — today's observation is related to yesterday's, and tomorrow's will be related to today's. Understanding and modeling this dependence is essential for forecasting future values and understanding the dynamics of the system being studied.

This chapter introduces the theory and practice of time series modeling in ***RMC-BestFit***, covering autoregressive (AR), moving average (MA), and integrated (ARIMA/ARIMAX) models. We emphasize both the mathematical foundations and practical implementation for readers new to time series methods.

## Why Time Series Analysis Matters

Consider a water resources engineer tasked with forecasting next month's streamflow for reservoir operations. Simply using the long-term average ignores valuable information: if this month's flow is unusually high, next month's flow is likely to also be above average (rivers don't jump randomly between extremes). Time series models capture this persistence, producing better forecasts than naive methods.

Time series analysis is fundamental to:

- **Streamflow forecasting**: Predict future flows for reservoir operations, water supply planning
- **Climate projections**: Model temperature, precipitation, and drought indices
- **Demand forecasting**: Predict water demand, electricity load, or economic variables
- **Quality control**: Detect anomalies in sensor data through residual monitoring
- **Understanding dynamics**: Quantify how quickly systems respond to perturbations

The ***RMC.BestFit*** library provides a comprehensive suite of time series models with both Maximum Likelihood (MLE) and Bayesian MCMC estimation, enabling full uncertainty quantification for forecasts.

## Core Concepts

Before diving into specific models, let's establish key concepts that underpin all time series analysis.

### Stationarity

A time series is **stationary** if its statistical properties (mean, variance, autocorrelation structure) don't change over time. Most classical time series models assume stationarity because:

1. Non-stationary series have time-varying parameters, making estimation ill-defined
2. Forecasts from non-stationary models can diverge to infinity
3. Standard statistical tests assume stationarity

**Weak stationarity** (the practical definition) requires:
- Constant mean: $E[Y_t] = \mu$ for all $t$
- Constant variance: $\text{Var}(Y_t) = \sigma^2$ for all $t$
- Autocovariance depends only on lag: $\text{Cov}(Y_t, Y_{t+h}) = \gamma(h)$ for all $t$

Many real-world series are non-stationary due to trends or seasonality. The ARIMA framework handles this by differencing the series to achieve stationarity before modeling.

### Autocorrelation

**Autocorrelation** measures the correlation between a time series and lagged versions of itself. The autocorrelation function (ACF) at lag $h$ is:

```math
\rho(h) = \frac{\text{Cov}(Y_t, Y_{t+h})}{\text{Var}(Y_t)} = \frac{\gamma(h)}{\gamma(0)}
```

The ACF reveals the memory structure of the series:
- **Fast decay**: Short-memory process (AR models with small coefficients)
- **Slow decay**: Long-memory or near unit-root process
- **Sharp cutoff**: MA structure (ACF drops to zero after lag $q$)
- **Sinusoidal pattern**: Seasonal or cyclical component

The **partial autocorrelation function (PACF)** measures the correlation between $Y_t$ and $Y_{t+h}$ after removing the linear effect of intermediate lags $Y_{t+1}, \ldots, Y_{t+h-1}$. The PACF is crucial for identifying AR order:
- AR(p) process: PACF cuts off after lag $p$
- MA(q) process: PACF decays gradually

### White Noise

**White noise** is a sequence of uncorrelated random variables with constant mean and variance:

```math
\varepsilon_t \stackrel{iid}{\sim} N(0, \sigma^2)
```

White noise has:
- $\rho(0) = 1$ (correlation with itself)
- $\rho(h) = 0$ for $h \neq 0$ (no correlation at any lag)

The goal of time series modeling is to transform the observed series into white noise residuals. If residuals still show autocorrelation, the model is missing structure.

### The Backshift Operator

The **backshift (lag) operator** $L$ simplifies notation:

```math
L Y_t = Y_{t-1}, \quad L^2 Y_t = Y_{t-2}, \quad L^k Y_t = Y_{t-k}
```

The **difference operator** is:

```math
\Delta Y_t = Y_t - Y_{t-1} = (1 - L) Y_t
```

Higher-order differences:

```math
\Delta^2 Y_t = \Delta(\Delta Y_t) = Y_t - 2Y_{t-1} + Y_{t-2} = (1-L)^2 Y_t
```

Using these operators, model equations become compact polynomial expressions that reveal the mathematical structure.

---

## Model Overview

The ***RMC.BestFit*** library implements four primary time series model classes:

| Model | Class | Parameters | When to Use |
|-------|-------|------------|-------------|
| AR(p) | `AutoRegressive` | $p + 2$ | Gradual ACF decay, sharp PACF cutoff |
| MA(q) | `MovingAverage` | $q + 2$ | Sharp ACF cutoff, gradual PACF decay |
| ARIMA(p,d,q) | `ARIMA` | $p + q + 2$ | Non-stationary series needing differencing |
| ARIMAX(p,d,q,b) | `ARIMAX` | $p + q + K + 2+$ | External predictors available |

Each model class has a corresponding analysis class for Bayesian MCMC estimation:
- `ARAnalysis`
- `MAAnalysis`
- `ARIMAAnalysis`
- `ARIMAXAnalysis`

---

## Autoregressive Model — AR(p)

The autoregressive model is the workhorse of time series analysis. It expresses the current value as a linear combination of past values plus random noise — essentially a regression of the series on its own lagged values.

### Intuition

Imagine predicting tomorrow's river flow. If today's flow is high, tomorrow's is likely high too (persistence). If yesterday's was also high, that provides additional information. An AR model formalizes this: predict today using a weighted combination of recent past values.

The "order" $p$ specifies how many past values to include. An AR(1) uses only yesterday; AR(2) uses yesterday and the day before; and so on.

### Mathematical Formulation

The AR(p) model is defined as [[1]](#1):

```math
Y_t = \mu + \sum_{i=1}^{p} \phi_i (Y_{t-i} - \mu) + \varepsilon_t
```

where:
- $Y_t$ is the observation at time $t$
- $\mu$ is the process mean (intercept)
- $\phi_1, \phi_2, \ldots, \phi_p$ are the AR coefficients
- $\varepsilon_t \stackrel{iid}{\sim} N(0, \sigma^2)$ is white noise

An equivalent formulation that's often more intuitive:

```math
Y_t = c + \phi_1 Y_{t-1} + \phi_2 Y_{t-2} + \ldots + \phi_p Y_{t-p} + \varepsilon_t
```

where $c = \mu(1 - \phi_1 - \phi_2 - \ldots - \phi_p)$ is a constant term.

Using the backshift operator:

```math
\Phi(L)(Y_t - \mu) = \varepsilon_t
```

where $\Phi(L) = 1 - \phi_1 L - \phi_2 L^2 - \ldots - \phi_p L^p$ is the **AR polynomial**.

### Parameters

For an AR(p) model with intercept:
- $\mu$: Process mean (intercept)
- $\phi_1, \phi_2, \ldots, \phi_p$: AR coefficients
- $\sigma$: Error standard deviation

**Total parameters:** $p + 2$ (with intercept) or $p + 1$ (without intercept)

### Stationarity Conditions

For a stationary AR process, all roots of the characteristic polynomial must lie **outside** the unit circle [[1]](#1):

```math
\Phi(z) = 1 - \phi_1 z - \phi_2 z^2 - \ldots - \phi_p z^p = 0
```

This ensures the process doesn't explode to infinity.

**Simplified conditions for low orders:**
- **AR(1)**: $|\phi_1| < 1$
- **AR(2)**: $\phi_1 + \phi_2 < 1$, $\phi_2 - \phi_1 < 1$, $|\phi_2| < 1$

A sufficient (but not necessary) general condition: $\sum_{i=1}^{p} |\phi_i| < 1$

**Interpretation of coefficients:**
- $\phi_1 > 0$: Positive persistence (high values followed by high values)
- $\phi_1 < 0$: Oscillating behavior (high values followed by low values)
- $\phi_1 \approx 1$: Near unit root, very persistent (slow mean reversion)
- $\phi_1 \approx 0$: Little dependence on immediate past

### ACF and PACF Patterns

The AR(p) process has distinctive correlation patterns:

| Statistic | Pattern |
|-----------|---------|
| ACF | Exponential decay (or damped sinusoid for complex roots) |
| PACF | Cuts off sharply after lag $p$ |

This PACF cutoff is the key diagnostic for identifying AR order.

### Likelihood Function

The conditional log-likelihood, conditioning on the first $p$ observations, is [[2]](#2):

```math
\ell(\theta) = -\frac{n-p}{2}\log(2\pi) - (n-p)\log(\sigma) - \frac{1}{2\sigma^2}\sum_{t=p+1}^{n} \varepsilon_t^2
```

where the residuals are:

```math
\varepsilon_t = Y_t - \mu - \sum_{i=1}^{p} \phi_i (Y_{t-i} - \mu)
```

### Implementation Example

Let's fit an AR(2) model to monthly streamflow data:

```cs
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

// Create time series from monthly streamflow data
var startDate = new DateTime(1990, 1, 1);
var ts = new TimeSeries(TimeInterval.OneMonth, startDate, monthlyFlows);

// Create AR(2) model
var model = new AutoRegressive(ts, order: 2, includeIntercept: true)
{
    UseDefaultTrainingSteps = false,  // Use all data for training
    UseJeffreysRuleForScale = true    // Non-informative prior on sigma
};
model.TrainingTimeSteps = ts.Count;

// Perform MLE estimation using MaximumLikelihood class
var mle = new MaximumLikelihood(model);
mle.Estimate();

// Display estimated parameters (access via Parameters list)
if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    // Parameter indices for AR(2) with intercept: [0]=μ, [1]=φ₁, [2]=φ₂, [3]=σ
    Console.WriteLine($"Mean (μ):     {model.Parameters[0].Value:F2}");
    Console.WriteLine($"AR(1) (φ₁):   {model.Parameters[1].Value:F4}");
    Console.WriteLine($"AR(2) (φ₂):   {model.Parameters[2].Value:F4}");
    Console.WriteLine($"Sigma (σ):    {model.Parameters[3].Value:F4}");
}
```

For Bayesian estimation with uncertainty quantification and forecasting:

```cs
// Create analysis object
var analysis = new ARAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;
analysis.ForecastingTimeSteps = 12;  // Forecast 12 months ahead

// Run MCMC
await analysis.RunAsync();

// Access posterior summaries
var results = analysis.BayesianAnalysis.Results;
Console.WriteLine("\nPosterior Summary:");
Console.WriteLine($"{"Parameter",-12} {"Mean",10} {"Std Dev",10} {"2.5%",10} {"97.5%",10}");

for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    Console.WriteLine($"{model.Parameters[i].Name,-12} {stats.Mean,10:F4} {stats.StandardDeviation,10:F4} " +
                      $"{stats.LowerCI,10:F4} {stats.UpperCI,10:F4}");
}

// Access forecasts with uncertainty
var forecasts = analysis.AnalysisResults;
Console.WriteLine("\nForecasts:");
Console.WriteLine($"{"Month",-8} {"Forecast",12} {"Lower 95%",12} {"Upper 95%",12}");

for (int h = 0; h < 12; h++)
{
    var mode = forecasts.ModeCurve[h];
    var lower = forecasts.LowerCurve[h];
    var upper = forecasts.UpperCurve[h];
    Console.WriteLine($"{h + 1,-8} {mode.Value,12:F1} {lower.Value,12:F1} {upper.Value,12:F1}");
}
```

### Practical Considerations

**Choosing the order $p$:**
1. Plot the PACF — it should cut off after lag $p$
2. Start with low orders (1-3) for most applications
3. Use information criteria (AIC, BIC) to compare models
4. Prefer parsimony — simpler models often forecast better

**Common issues:**
- **Near unit root** ($\phi_1 \approx 1$): Consider differencing instead
- **Seasonal patterns**: PACF may show spikes at seasonal lags; consider seasonal terms or Fourier basis
- **Outliers**: Can inflate variance estimates; consider robust methods or data cleaning

---

## Moving Average Model — MA(q)

The moving average model expresses the current value as a linear combination of current and past random shocks (error terms). While AR models capture persistence through lagged observations, MA models capture persistence through the lingering effects of past innovations.

### Intuition

Imagine a river where upstream rainfall events cause flow perturbations that take several days to pass the gauge. Today's flow depends on today's rainfall plus echoes of the past few days' rainfall. An MA model captures this: the current value depends on the current shock plus weighted past shocks.

The "order" $q$ specifies how many past shocks influence the current value. An MA(1) uses only today's and yesterday's shocks; MA(2) adds the day before; and so on.

### Mathematical Formulation

The MA(q) model is defined as [[1]](#1):

```math
Y_t = \mu + \varepsilon_t + \sum_{j=1}^{q} \theta_j \varepsilon_{t-j}
```

where:
- $Y_t$ is the observation at time $t$
- $\mu$ is the process mean
- $\theta_1, \theta_2, \ldots, \theta_q$ are the MA coefficients
- $\varepsilon_t \stackrel{iid}{\sim} N(0, \sigma^2)$ is white noise

Using the backshift operator:

```math
Y_t - \mu = \Theta(L) \varepsilon_t
```

where $\Theta(L) = 1 + \theta_1 L + \theta_2 L^2 + \ldots + \theta_q L^q$ is the **MA polynomial**.

### Parameters

For an MA(q) model with intercept:
- $\mu$: Process mean
- $\theta_1, \theta_2, \ldots, \theta_q$: MA coefficients
- $\sigma$: Error standard deviation

**Total parameters:** $q + 2$ (with intercept) or $q + 1$ (without)

### Invertibility Conditions

For an **invertible** MA process, all roots of the MA polynomial must lie outside the unit circle [[1]](#1):

```math
\Theta(z) = 1 + \theta_1 z + \theta_2 z^2 + \ldots + \theta_q z^q = 0
```

**Simplified conditions:**
- **MA(1)**: $|\theta_1| < 1$

Invertibility ensures:
1. A unique model representation (non-invertible models have equivalent invertible forms)
2. The MA process can be written as an infinite-order AR process
3. Past shocks can be recovered from observed data

### ACF and PACF Patterns

The MA(q) process has distinctive correlation patterns:

| Statistic | Pattern |
|-----------|---------|
| ACF | Cuts off sharply after lag $q$ |
| PACF | Exponential decay (or damped sinusoid) |

This ACF cutoff is the key diagnostic for identifying MA order — it's the opposite pattern from AR.

### Likelihood Function

The exact likelihood for MA models requires iterative computation of residuals. Given parameters, residuals are computed recursively:

```math
\varepsilon_t = Y_t - \mu - \sum_{j=1}^{\min(t-1, q)} \theta_j \varepsilon_{t-j}
```

with initialization $\varepsilon_0 = \varepsilon_{-1} = \ldots = 0$.

The conditional log-likelihood is:

```math
\ell(\theta) = -\frac{n}{2}\log(2\pi) - n\log(\sigma) - \frac{1}{2\sigma^2}\sum_{t=1}^{n} \varepsilon_t^2
```

### Forecasting Properties

MA(q) forecasts converge to the mean $\mu$ after $q$ steps ahead:

- **Short horizon** ($h \leq q$): Forecast uses recent shocks

```math
\hat{Y}_{n+h|n} = \mu + \sum_{j=h}^{q} \theta_j \varepsilon_{n+h-j}
```

- **Long horizon** ($h > q$): Forecast equals the mean

```math
\hat{Y}_{n+h|n} = \mu
```

This makes MA models suitable for **transient shocks** — perturbations that affect the system for exactly $q$ periods then disappear.

### Implementation Example

```cs
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

// Create MA(2) model
var model = new MovingAverage(ts, order: 2, includeIntercept: true)
{
    UseDefaultTrainingSteps = false,
    UseJeffreysRuleForScale = true
};
model.TrainingTimeSteps = ts.Count;

// MLE estimation using MaximumLikelihood class
var mle = new MaximumLikelihood(model);
mle.Estimate();

if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    // Parameter indices for MA(2) with intercept: [0]=μ, [1]=θ₁, [2]=θ₂, [3]=σ
    Console.WriteLine($"Mean (μ):     {model.Parameters[0].Value:F2}");
    Console.WriteLine($"MA(1) (θ₁):   {model.Parameters[1].Value:F4}");
    Console.WriteLine($"MA(2) (θ₂):   {model.Parameters[2].Value:F4}");
    Console.WriteLine($"Sigma (σ):    {model.Parameters[3].Value:F4}");
}

// Bayesian estimation
var analysis = new MAAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;
await analysis.RunAsync();
```

---

## ARIMA Model — ARIMA(p,d,q)

ARIMA (AutoRegressive Integrated Moving Average) combines differencing with ARMA modeling to handle non-stationary series. Many real-world series exhibit trends or stochastic wandering that violates stationarity — ARIMA handles this by differencing the series before applying AR and MA components [[1]](#1).

### Intuition

Consider a stock price that wanders randomly over time (a "random walk"). The price level is non-stationary, but the daily *changes* (differences) might be stationary noise. ARIMA works by:
1. Differencing the series $d$ times to achieve stationarity
2. Fitting an ARMA(p,q) model to the differenced series
3. Integrating forecasts back to the original scale

The "I" in ARIMA stands for "Integrated" — the opposite of differencing.

### Mathematical Formulation

The ARIMA(p,d,q) model is defined as:

```math
\Phi(L)(1-L)^d (Y_t - \mu) = \Theta(L) \varepsilon_t
```

where:
- $(1-L)^d$ is the differencing operator applied $d$ times
- $\Phi(L)$ is the AR polynomial
- $\Theta(L)$ is the MA polynomial

Let $W_t = \Delta^d Y_t = (1-L)^d Y_t$ be the differenced series. Then:

```math
W_t = \mu + \sum_{i=1}^{p} \phi_i (W_{t-i} - \mu) + \varepsilon_t + \sum_{j=1}^{q} \theta_j \varepsilon_{t-j}
```

### Differencing

The differencing operation transforms the series:

- **First difference** ($d=1$): $\Delta Y_t = Y_t - Y_{t-1}$ — removes linear trend
- **Second difference** ($d=2$): $\Delta^2 Y_t = \Delta(\Delta Y_t) = Y_t - 2Y_{t-1} + Y_{t-2}$ — removes quadratic trend

**Effect on series length:** After $d$ differences, $n$ observations become $n-d$ observations.

### When to Difference

Differencing is appropriate when the series shows:
- **Trending behavior**: Systematic upward or downward movement
- **Unit root**: ACF decays very slowly (series is very persistent)
- **Non-constant mean**: Mean appears to change over time

**Warning:** Over-differencing can introduce artificial patterns. If the differenced series shows negative autocorrelation at lag 1, you may have over-differenced.

### Parameters

For an ARIMA(p,d,q) model:
- $\mu$: Mean of the differenced series (drift term if $d > 0$)
- $\phi_1, \ldots, \phi_p$: AR coefficients
- $\theta_1, \ldots, \theta_q$: MA coefficients
- $\sigma$: Error standard deviation

**Total parameters:** $p + q + 2$ (with intercept)

### Likelihood

Computed on the differenced series $W_t = \Delta^d Y_t$ using the ARMA likelihood:

```math
\ell(\theta) = -\frac{n-d-m}{2}\log(2\pi) - (n-d-m)\log(\sigma) - \frac{1}{2\sigma^2}\sum_{t=m+1}^{n-d} \varepsilon_t^2
```

where $m = \max(p, q)$ accounts for conditioning.

### Implementation Example

Fitting an ARIMA(1,1,1) model (differenced AR(1) with MA(1) errors):

```cs
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

// Create ARIMA(1,1,1) model
var model = new ARIMA(ts)
{
    IncludeIntercept = true,
    AROrderP = 1,
    DiffOrderD = 1,
    MAOrderQ = 1,
    UseDefaultTrainingSteps = false
};
model.TrainingTimeSteps = ts.Count;

// MLE estimation using MaximumLikelihood class
var mle = new MaximumLikelihood(model);
mle.Estimate();

if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    // Parameter indices for ARIMA(1,1,1) with intercept: [0]=μ, [1]=φ₁, [2]=θ₁, [3]=σ
    Console.WriteLine($"Drift (μ):    {model.Parameters[0].Value:F4}");
    Console.WriteLine($"AR(1) (φ₁):   {model.Parameters[1].Value:F4}");
    Console.WriteLine($"MA(1) (θ₁):   {model.Parameters[2].Value:F4}");
    Console.WriteLine($"Sigma (σ):    {model.Parameters[3].Value:F4}");
}

// Bayesian estimation with forecasting
var analysis = new ARIMAAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;
analysis.ForecastingTimeSteps = 24;  // 24-month forecast

await analysis.RunAsync();
```

### Special Cases

| Model | ARIMA Notation | Description |
|-------|---------------|-------------|
| White noise | ARIMA(0,0,0) | No structure, just noise |
| Random walk | ARIMA(0,1,0) | $Y_t = Y_{t-1} + \varepsilon_t$ |
| Random walk with drift | ARIMA(0,1,0) + intercept | $Y_t = c + Y_{t-1} + \varepsilon_t$ |
| AR(p) | ARIMA(p,0,0) | Autoregressive only |
| MA(q) | ARIMA(0,0,q) | Moving average only |
| ARMA(p,q) | ARIMA(p,0,q) | Mixed, no differencing |

---

## ARIMAX Model — ARIMAX(p,d,q,b)

ARIMAX extends ARIMA by incorporating **exogenous (external) predictor variables**. When external factors influence the response — climate indices affecting streamflow, economic indicators affecting demand, upstream flows affecting downstream gauges — ARIMAX captures both the internal dynamics and external effects [[3]](#3).

### Intuition

Predicting reservoir inflow using only past inflows ignores valuable information: upstream precipitation, snowpack, temperature. ARIMAX includes these external predictors while still modeling the temporal dynamics of inflow itself.

The model separates two sources of predictability:
1. **Internal dynamics**: How the series depends on its own past (AR/MA terms)
2. **External effects**: How external predictors influence the series (regression terms)

### Mathematical Formulation

The ARIMAX model is:

```math
Y_t = \mu + \gamma(t) + \psi(t) + \sum_{k=1}^{K} \beta_k X_{k,t-b} + \sum_{i=1}^{p} \phi_i (Y_{t-i} - \mu) + \varepsilon_t + \sum_{j=1}^{q} \theta_j \varepsilon_{t-j}
```

where:
- $\gamma(t)$ is an optional deterministic trend
- $\psi(t)$ is an optional seasonal component (Fourier series)
- $X_{k,t-b}$ is the $k$-th exogenous variable at lag $b$
- $\beta_k$ is the regression coefficient for covariate $k$

### Trend Components

***RMC-BestFit*** supports polynomial trend functions:

| Trend Type | Formula | Parameters |
|------------|---------|------------|
| None | $\gamma(t) = 0$ | 0 |
| Linear | $\gamma(t) = \gamma_1 t$ | 1 |
| Quadratic | $\gamma(t) = \gamma_1 t + \gamma_2 t^2$ | 2 |
| Cubic | $\gamma(t) = \gamma_1 t + \gamma_2 t^2 + \gamma_3 t^3$ | 3 |

**Warning:** Including both differencing ($d > 0$) and trend terms is usually over-specified. Choose one or the other.

### Seasonality

When `IncludeSeasonality = true`, a Fourier basis captures periodic patterns:

```math
\psi(t) = \psi_1 \sin\left(\frac{2\pi t}{S}\right) + \psi_2 \cos\left(\frac{2\pi t}{S}\right)
```

where $S$ is the seasonal period inferred from data frequency:
- Monthly data: $S = 12$
- Quarterly data: $S = 4$
- Daily data: $S = 365$

This captures seasonal patterns without requiring seasonal differencing or multiplicative seasonal ARIMA.

### Exogenous Variables

Covariates enter with an optional lag $b$:
- $b = 0$: **Contemporaneous** effect — $X_t$ affects $Y_t$
- $b > 0$: **Lagged** effect — $X_{t-b}$ affects $Y_t$

**Covariate extension for forecasting:**

When forecasting beyond available covariate data, ***RMC-BestFit*** offers:
1. `None`: No extension (throws exception if insufficient data)
2. `BlockBootstrap`: Resamples blocks of covariate data, preserving temporal autocorrelation
3. `KNN`: K-nearest neighbors imputation, preserving local covariate structure

### Implementation Example

Modeling streamflow with precipitation as a covariate:

```cs
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

// Time series of streamflow (response) and precipitation (covariate)
var flowTS = new TimeSeries(TimeInterval.OneMonth, startDate, monthlyFlows);
var precipTS = new TimeSeries(TimeInterval.OneMonth, startDate, monthlyPrecip);

// Create ARIMAX model
var model = new ARIMAX(flowTS)
{
    IncludeIntercept = true,
    AROrderP = 1,
    DiffOrderD = 0,
    MAOrderQ = 0,
    XOrderB = 0,              // Contemporaneous effect
    IncludeSeasonality = true, // Capture seasonal patterns
    UseDefaultTrainingSteps = false
};
model.TrainingTimeSteps = flowTS.Count;

// Add covariate(s)
model.SetCovariates(new List<TimeSeries> { precipTS });

// MLE estimation using MaximumLikelihood class
var mle = new MaximumLikelihood(model);
mle.Estimate();

if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    // Parameter order depends on model configuration
    // For ARIMAX with intercept, AR(1), seasonality, and 1 covariate:
    // Parameters are ordered: intercept, AR coefficients, seasonal terms, covariate betas, sigma
    Console.WriteLine("Estimated parameters:");
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        Console.WriteLine($"  {model.Parameters[i].Name}: {model.Parameters[i].Value:F4}");
    }
}

// Bayesian estimation
var analysis = new ARIMAXAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;
analysis.ForecastingTimeSteps = 12;
analysis.ARIMAX.CovariateExtension = ARIMAX.CovariateExtensionMethod.BlockBootstrap;

await analysis.RunAsync();
```

---

## Bayesian Estimation

All time series models in ***RMC-BestFit*** support Bayesian MCMC estimation through their corresponding analysis classes. Bayesian estimation provides complete uncertainty quantification — not just point estimates, but full probability distributions for parameters and forecasts [[4]](#4).

### Why Bayesian?

MLE gives you the "best" parameter values, but doesn't tell you how uncertain those estimates are. Bayesian estimation provides:

1. **Parameter uncertainty**: Full posterior distributions, not just point estimates
2. **Forecast uncertainty**: Prediction intervals that account for parameter uncertainty
3. **Probabilistic inference**: Answer questions like "What's the probability AR(1) > 0.5?"
4. **Prior incorporation**: Include expert knowledge when appropriate

### MCMC Sampler

The default sampler is **DEMCzs** (Differential Evolution MCMC with snooker update) [[5]](#5), which is:
- **Self-tuning**: No manual proposal distribution tuning required
- **Robust**: Handles multimodal posteriors and correlated parameters
- **Parallelizable**: Multiple chains run simultaneously

### Posterior Distribution

The posterior distribution combines the likelihood with prior information:

```math
\pi(\theta | Y) \propto \mathcal{L}(Y | \theta) \cdot \pi(\theta)
```

where:
- $\mathcal{L}(Y | \theta)$ is the likelihood function
- $\pi(\theta)$ is the prior (product of individual parameter priors)

### Prior Distributions

***RMC-BestFit*** uses weakly informative default priors:

| Parameter | Default Prior | Rationale |
|-----------|--------------|-----------|
| $\mu$ | $\text{Uniform}(L_\mu, U_\mu)$ | Bounds from data range |
| $\phi_i$ | $\text{Uniform}(-2, 2)$ | Allows non-stationary exploration |
| $\theta_j$ | $\text{Uniform}(-2, 2)$ | Allows non-invertible exploration |
| $\sigma$ | $\text{Uniform}(\epsilon, U_\sigma)$ | Positive scale parameter |

**Jeffreys' prior for scale:**

When `UseJeffreysRuleForScale = true`, the scale parameter receives the non-informative Jeffreys' prior:

```math
\pi(\sigma) \propto \frac{1}{\sigma}
```

This adds $-\log(\sigma)$ to the log-posterior, producing posteriors invariant to rescaling.

### Convergence Diagnostics

Always check MCMC convergence before using results [[4]](#4):

```cs
var results = analysis.BayesianAnalysis.Results;

Console.WriteLine($"{"Parameter",-12} {"R-hat",8} {"ESS",8} {"Converged?",12}");
for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    bool converged = stats.Rhat < 1.1 && stats.ESS > 100;

    Console.WriteLine($"{model.Parameters[i].Name,-12} {stats.Rhat,8:F3} {stats.ESS,8:F0} {(converged ? "Yes" : "NO"),12}");
}
```

**Diagnostic thresholds:**
- **R-hat** (Gelman-Rubin): Should be < 1.1 (preferably < 1.05)
- **ESS** (Effective Sample Size): Should be > 100 for reliable inference

**If convergence fails:**
1. Increase `Iterations` and `WarmupIterations`
2. Check for model misspecification
3. Consider reparameterization
4. Examine trace plots for pathological behavior

---

## Model Diagnostics

Fitting a model is only the beginning. Proper diagnostics verify that the model adequately captures the data structure.

### Residual Analysis

If the model is correctly specified, residuals should be white noise:

```cs
var mapParams = analysis.BayesianAnalysis.Results.MAP.Values;
var residuals = model.Residuals(mapParams);

// Check residual statistics
double mean = residuals.Average();
double variance = residuals.Select(r => r * r).Average() - mean * mean;

// Get sigma from the last parameter (always the scale parameter)
double sigma = mapParams.Last();

Console.WriteLine($"Residual mean: {mean:F4} (should be ≈ 0)");
Console.WriteLine($"Residual std:  {Math.Sqrt(variance):F4} (should be ≈ σ = {sigma:F4})");
```

### ACF of Residuals

Residuals should show no significant autocorrelation:

```cs
// Compute residual autocorrelations
var residualAcf = Statistics.AutoCorrelation(residuals, maxLag: 20);

Console.WriteLine("Lag    ACF");
for (int lag = 1; lag <= 20; lag++)
{
    double bound = 1.96 / Math.Sqrt(residuals.Length);  // 95% confidence bound
    string sig = Math.Abs(residualAcf[lag]) > bound ? " *" : "";
    Console.WriteLine($"{lag,3}  {residualAcf[lag],7:F3}{sig}");
}
```

Significant autocorrelation (marked with `*`) suggests the model is missing structure. Consider:
- Increasing AR or MA order
- Adding seasonal terms
- Checking for outliers or structural breaks

### Ljung-Box Test

The Ljung-Box test formally tests for residual autocorrelation:

```math
Q = n(n+2) \sum_{h=1}^{H} \frac{\hat{\rho}(h)^2}{n-h}
```

Under the null hypothesis of no autocorrelation, $Q \sim \chi^2_{H-p-q}$.

```cs
// Ljung-Box test (using Numerics.Data.Statistics)
int H = 20;  // Number of lags to test
double Q = Statistics.LjungBoxStatistic(residuals, H);
int df = H - model.Order;  // Degrees of freedom
double pValue = 1 - new ChiSquared(df).CDF(Q);

Console.WriteLine($"Ljung-Box Q({H}): {Q:F2}");
Console.WriteLine($"p-value: {pValue:F4}");
Console.WriteLine(pValue < 0.05 ? "Warning: Significant residual autocorrelation" : "OK: No significant autocorrelation");
```

### Normality Check

Residuals should be approximately normally distributed:

```cs
// Jarque-Bera test for normality
double skewness = Statistics.Skewness(residuals);
double kurtosis = Statistics.Kurtosis(residuals);  // Excess kurtosis
double jb = (residuals.Length / 6.0) * (skewness * skewness + kurtosis * kurtosis / 4.0);

Console.WriteLine($"Skewness: {skewness:F3} (should be ≈ 0)");
Console.WriteLine($"Excess kurtosis: {kurtosis:F3} (should be ≈ 0)");
Console.WriteLine($"Jarque-Bera: {jb:F2}");
```

Non-normality may indicate:
- Outliers (high kurtosis)
- Asymmetric shocks (non-zero skewness)
- Need for transformation (log, Box-Cox)

---

## Data Transformations

Many hydrologic variables (streamflow, precipitation, concentrations) are positively skewed with variance that increases with the mean. Transformations can stabilize variance and improve normality.

### Available Transformations

| Transform | Formula | Inverse | Use Case |
|-----------|---------|---------|----------|
| None | $Y_t^* = Y_t$ | $Y_t = Y_t^*$ | Data already Gaussian |
| Logarithmic | $Y_t^* = \log(Y_t)$ | $Y_t = \exp(Y_t^*)$ | Positive, right-skewed data |
| Box-Cox | $Y_t^* = \frac{Y_t^\lambda - 1}{\lambda}$ | $Y_t = (\lambda Y_t^* + 1)^{1/\lambda}$ | General power transform |

### Logarithmic Transformation

The most common transformation for hydrologic data:

```cs
var model = new AutoRegressive(ts, order: 2)
{
    TransformType = RMC.BestFit.Models.Transform.Logarithmic
};
```

**Interpretation:** The model is fit in log-space, so AR coefficients describe multiplicative dynamics. A forecast of $\hat{Y}^*_{t+1}$ in log-space corresponds to $\exp(\hat{Y}^*_{t+1})$ in original units.

**Warning:** Requires all values to be positive. Add a small constant if zeros are present.

### Box-Cox Transformation

The Box-Cox family includes log ($\lambda = 0$) and square root ($\lambda = 0.5$) as special cases:

```math
Y_t^* = \begin{cases}
\frac{Y_t^\lambda - 1}{\lambda} & \lambda \neq 0 \\
\log(Y_t) & \lambda = 0
\end{cases}
```

```cs
var model = new AutoRegressive(ts, order: 2)
{
    TransformType = RMC.BestFit.Models.Transform.BoxCox
};
model.SetTransformParameters(lambda1: 0.5);  // Square root transform
```

### Jacobian Adjustment

When using transformations, the likelihood must include the log-Jacobian for proper inference. For Box-Cox:

```math
\log J = \sum_{t=1}^{n} (\lambda - 1) \log|Y_t|
```

***RMC-BestFit*** handles this automatically.

---

## Model Selection

Choosing the right model orders (p, d, q) is both art and science. Here's a systematic approach.

### The Box-Jenkins Methodology

The classic approach [[1]](#1):

1. **Identification**: Use ACF/PACF patterns to guess orders
2. **Estimation**: Fit the model
3. **Diagnostics**: Check residuals
4. **Refinement**: Adjust orders if diagnostics fail

### ACF/PACF Patterns

| Pattern | Suggested Model |
|---------|-----------------|
| ACF: exponential decay; PACF: cuts off at $p$ | AR(p) |
| ACF: cuts off at $q$; PACF: exponential decay | MA(q) |
| ACF: exponential decay; PACF: exponential decay | ARMA(p,q) |
| ACF: very slow decay | Differencing needed (ARIMA) |

### Information Criteria

Compare models using penalized likelihood:

- **AIC**: $-2\ell(\hat{\theta}) + 2k$ — favors predictive accuracy
- **BIC**: $-2\ell(\hat{\theta}) + k\log(n)$ — stronger penalty, favors parsimony

Lower values are better. BIC typically selects simpler models than AIC.

```cs
// Compare AR(1), AR(2), AR(3)
var results = new List<(int p, double AIC, double BIC)>();

foreach (int p in new[] { 1, 2, 3 })
{
    var model = new AutoRegressive(ts, order: p, includeIntercept: true)
    {
        UseDefaultTrainingSteps = false
    };
    model.TrainingTimeSteps = ts.Count;
    model.Estimate();

    int k = p + 2;  // Number of parameters
    int n = ts.Count - p;  // Effective sample size
    double logLik = model.DataLogLikelihood(model.Parameters.Select(x => x.Value).ToArray());

    double aic = -2 * logLik + 2 * k;
    double bic = -2 * logLik + k * Math.Log(n);

    results.Add((p, aic, bic));
    Console.WriteLine($"AR({p}): AIC = {aic:F1}, BIC = {bic:F1}");
}

var bestAIC = results.OrderBy(r => r.AIC).First();
var bestBIC = results.OrderBy(r => r.BIC).First();
Console.WriteLine($"\nBest by AIC: AR({bestAIC.p})");
Console.WriteLine($"Best by BIC: AR({bestBIC.p})");
```

### Practical Guidelines

| Scenario | Recommendation |
|----------|---------------|
| Stationary series, gradual ACF decay | Start with AR(1) or AR(2) |
| Stationary series, sharp ACF cutoff | Try MA(1) or MA(2) |
| Trending series | Difference first (ARIMA with d=1) |
| Seasonal patterns | Use ARIMAX with `IncludeSeasonality = true` |
| External predictors available | Use ARIMAX |
| Uncertain about order | Prefer parsimony — simpler models often forecast better |

---

## Practical Examples

### Example 1: Monthly Streamflow Forecasting

Forecast monthly streamflow using historical data:

```cs
using Numerics.Data;
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using RMC.BestFit.Estimation;

// Load historical monthly streamflow (log-transformed for variance stabilization)
var ts = new TimeSeries(TimeInterval.OneMonth, new DateTime(1980, 1, 1), logMonthlyFlows);

// Fit AR(2) model — streamflow often shows strong lag-1 persistence
var model = new AutoRegressive(ts, order: 2, includeIntercept: true)
{
    UseDefaultTrainingSteps = false,
    UseJeffreysRuleForScale = true
};
model.TrainingTimeSteps = ts.Count;

// Bayesian estimation with 12-month forecast
var analysis = new ARAnalysis(model);
analysis.BayesianAnalysis.Iterations = 15000;
analysis.BayesianAnalysis.WarmupIterations = 7500;
analysis.ForecastingTimeSteps = 12;

await analysis.RunAsync();

// Check convergence
var results = analysis.BayesianAnalysis.Results;
bool converged = results.ParameterResults.All(p => p.SummaryStatistics.Rhat < 1.1);
Console.WriteLine($"Converged: {converged}");

// Display forecasts (transform back from log scale)
Console.WriteLine("\n12-Month Forecast (original units):");
Console.WriteLine($"{"Month",-8} {"Median",10} {"5th %ile",10} {"95th %ile",10}");

var forecasts = analysis.AnalysisResults;
for (int h = 0; h < 12; h++)
{
    double median = Math.Exp(forecasts.ModeCurve[h].Value);
    double lower = Math.Exp(forecasts.LowerCurve[h].Value);
    double upper = Math.Exp(forecasts.UpperCurve[h].Value);

    Console.WriteLine($"{h + 1,-8} {median,10:F0} {lower,10:F0} {upper,10:F0}");
}
```

### Example 2: ARIMAX with Climate Index

Predict streamflow using ENSO (El Niño Southern Oscillation) as a covariate:

```cs
// Monthly streamflow and ENSO index (Niño 3.4)
var flowTS = new TimeSeries(TimeInterval.OneMonth, startDate, monthlyFlows);
var ensoTS = new TimeSeries(TimeInterval.OneMonth, startDate, nino34Index);

// ARIMAX: AR(1) + ENSO effect + seasonality
var model = new ARIMAX(flowTS)
{
    IncludeIntercept = true,
    AROrderP = 1,
    MAOrderQ = 0,
    DiffOrderD = 0,
    XOrderB = 1,  // ENSO affects flow with 1-month lag
    IncludeSeasonality = true,
    UseDefaultTrainingSteps = false
};
model.TrainingTimeSteps = flowTS.Count;
model.SetCovariates(new List<TimeSeries> { ensoTS });

// MLE estimation
var mle = new MaximumLikelihood(model);
mle.Estimate();

if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    // Find the beta coefficient for ENSO in the parameters list
    // Parameter order: intercept, AR coeffs, seasonal terms, beta coeffs, sigma
    Console.WriteLine("Estimated parameters:");
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        if (model.Parameters[i].Name.Contains("Beta"))
        {
            Console.WriteLine($"ENSO effect ({model.Parameters[i].Name}): {model.Parameters[i].Value:F3}");
            Console.WriteLine($"Interpretation: A 1-unit increase in Niño 3.4 is associated with");
            Console.WriteLine($"                a {model.Parameters[i].Value:F1} unit change in streamflow");
        }
    }
}

// Bayesian estimation for uncertainty
var analysis = new ARIMAXAnalysis(model);
analysis.BayesianAnalysis.Iterations = 15000;
analysis.BayesianAnalysis.WarmupIterations = 7500;
await analysis.RunAsync();

// ENSO effect uncertainty
var ensoStats = analysis.BayesianAnalysis.Results.ParameterResults
    .First(p => p.Name?.Contains("Beta") ?? false).SummaryStatistics;
Console.WriteLine($"ENSO effect: {ensoStats.Mean:F3} (95% CI: [{ensoStats.LowerCI:F3}, {ensoStats.UpperCI:F3}])");
```

### Example 3: Detecting Non-Stationarity

Test whether a series needs differencing:

```cs
// Plot ACF to check for slow decay
var acf = Statistics.AutoCorrelation(data, maxLag: 20);

Console.WriteLine("Lag    ACF     (slow decay suggests non-stationarity)");
for (int lag = 1; lag <= 10; lag++)
{
    Console.WriteLine($"{lag,3}  {acf[lag],7:F3}");
}

// If ACF decays very slowly, try differencing
var diffData = new double[data.Length - 1];
for (int t = 0; t < diffData.Length; t++)
{
    diffData[t] = data[t + 1] - data[t];
}

var diffAcf = Statistics.AutoCorrelation(diffData, maxLag: 20);

Console.WriteLine("\nAfter differencing:");
Console.WriteLine("Lag    ACF");
for (int lag = 1; lag <= 10; lag++)
{
    Console.WriteLine($"{lag,3}  {diffAcf[lag],7:F3}");
}

// Fit ARIMA(1,1,0) if differencing helped
var model = new ARIMA(ts)
{
    AROrderP = 1,
    DiffOrderD = 1,
    MAOrderQ = 0
};
```

---

## Training Configuration

By default, ***RMC-BestFit*** uses 80% of data for training, reserving 20% for out-of-sample validation. To use all data (matching R's `arima()` behavior):

```cs
model.UseDefaultTrainingSteps = false;
model.TrainingTimeSteps = timeSeries.Count;
```

**When to use each approach:**
- **Default (80/20 split)**: Good for model validation, prevents overfitting
- **All data**: Use when validating against other software or when sample size is limited

---

## Validation Against R

All time series models have been validated against R's `arima()` function using the Box-Jenkins airline passenger dataset [[1]](#1).

### Test Data

The **airline passenger dataset** contains 144 monthly observations (1949-1960) of international airline passengers. It's log-transformed for variance stabilization and exhibits both trend and seasonality.

### Validation Results

| Model | Parameter | R Value | RMC-BestFit | Within Tolerance? |
|-------|-----------|---------|-------------|-------------------|
| AR(1) | φ₁ | 0.9646 | ≈0.96 | Yes (10%) |
| AR(1) | μ | 5.5392 | ≈5.54 | Yes (10%) |
| MA(1) | θ₁ | 0.4018 | ≈0.40 | Yes (10%) |
| ARIMA(1,1,1) | φ₁ | 0.8822 | ≈0.88 | Yes (10%) |

Bayesian estimates use 15% tolerance to account for MCMC variability.

---

## Assumptions and Limitations

### Model Assumptions

1. **Gaussian errors**: $\varepsilon_t \sim N(0, \sigma^2)$
2. **Constant variance**: Homoscedasticity over time
3. **Linear dynamics**: Current value is a linear function of past values/shocks
4. **No structural breaks**: Parameters are constant over time

### Violations and Remedies

| Violation | Symptom | Remedy |
|-----------|---------|--------|
| Non-normality | Heavy tails, skewness | Transformation (log, Box-Cox) |
| Heteroscedasticity | Variance changes with level | Transformation or GARCH models |
| Nonlinearity | Residual patterns | Threshold models, neural nets |
| Structural break | Sudden parameter change | Split sample, regime-switching |

### Limitations

1. **Maximum order constraints**: $p, q \leq 10$; $d \leq 3$
2. **Minimum data requirements**: At least 10 observations
3. **No seasonal ARIMA**: Use Fourier basis via ARIMAX instead
4. **No automatic model selection**: User specifies orders (use AIC/BIC for guidance)
5. **Linear models only**: Nonlinear dynamics require other approaches

---

## Class Reference

### Model Classes (`RMC.BestFit.Models`)

| Class | Key Properties | Key Methods |
|-------|----------------|-------------|
| `AutoRegressive` | `Order`, `TimeSeries`, `TransformType`, `TrainingTimeSteps`, `Parameters` | `Residuals()`, `DataLogLikelihood()`, `SetParameterValues()` |
| `MovingAverage` | `Order`, `TimeSeries`, `TransformType`, `TrainingTimeSteps`, `Parameters` | `Residuals()`, `DataLogLikelihood()`, `SetParameterValues()` |
| `ARIMA` | `AROrderP`, `DiffOrderD`, `MAOrderQ`, `IncludeIntercept`, `Parameters` | `Residuals()`, `DataLogLikelihood()`, `SetParameterValues()` |
| `ARIMAX` | `AROrderP`, `DiffOrderD`, `MAOrderQ`, `XOrderB`, `IncludeSeasonality`, `CovariateExtension` | `SetCovariates()`, `Residuals()`, `DataLogLikelihood()` |

**Note:** MLE estimation is performed using the `MaximumLikelihood` class:
```cs
var mle = new MaximumLikelihood(model);
mle.Estimate();
if (mle.IsEstimated)
    model.SetParameterValues(mle.BestParameterSet.Values);
```

### Analysis Classes (`RMC.BestFit.Analyses`)

| Class | Key Properties | Key Methods |
|-------|----------------|-------------|
| `ARAnalysis` | `AutoRegressive`, `BayesianAnalysis`, `AnalysisResults`, `ForecastingTimeSteps` | `RunAsync()`, `CancelAnalysis()`, `Validate()` |
| `MAAnalysis` | `MovingAverage`, `BayesianAnalysis`, `AnalysisResults`, `ForecastingTimeSteps` | `RunAsync()`, `CancelAnalysis()`, `Validate()` |
| `ARIMAAnalysis` | `ARIMA`, `BayesianAnalysis`, `AnalysisResults`, `ForecastingTimeSteps` | `RunAsync()`, `CancelAnalysis()`, `Validate()` |
| `ARIMAXAnalysis` | `ARIMAX`, `BayesianAnalysis`, `AnalysisResults`, `ForecastingTimeSteps`, `CovariateExtensionMethod` | `RunAsync()`, `CancelAnalysis()`, `Validate()` |

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/TimeSeries`, `src/RMC.BestFit/Analyses/TimeSeries`, `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`, and `src/RMC.BestFit/Estimation/MaximumLikelihood.cs`.

---

## References

<a id="1">[1]</a> G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Hoboken, NJ: Wiley, 2015.

<a id="2">[2]</a> P. J. Brockwell and R. A. Davis, *Introduction to Time Series and Forecasting*, 3rd ed., New York: Springer, 2016.

<a id="3">[3]</a> J. D. Hamilton, *Time Series Analysis*, Princeton, NJ: Princeton University Press, 1994.

<a id="4">[4]</a> A. Gelman, J. B. Carlin, H. S. Stern, D. B. Dunson, A. Vehtari, and D. B. Rubin, *Bayesian Data Analysis*, 3rd ed., Boca Raton, FL: Chapman and Hall/CRC, 2013.

<a id="5">[5]</a> C. J. F. ter Braak and J. A. Vrugt, "Differential Evolution Markov Chain with snooker updater and fewer chains," *Statistics and Computing*, vol. 18, no. 4, pp. 435-446, 2008.

<a id="6">[6]</a> R. Prado and M. West, *Time Series: Modeling, Computation, and Inference*, Boca Raton, FL: Chapman and Hall/CRC, 2010.

<a id="7">[7]</a> R. J. Hyndman and Y. Khandakar, "Automatic time series forecasting: The forecast package for R," *Journal of Statistical Software*, vol. 27, no. 3, pp. 1-22, 2008.

<a id="8">[8]</a> C. Chatfield, *The Analysis of Time Series: An Introduction*, 6th ed., Boca Raton, FL: Chapman and Hall/CRC, 2004.

---

[<- Previous: Rating Curve](rating-curve.md) | [Back to Index](../../index.md) | [Next: Spatial Extremes ->](../spatial/spatial-extremes.md)

*Last updated: 2026-02-01*
*RMC-BestFit v2.0*
