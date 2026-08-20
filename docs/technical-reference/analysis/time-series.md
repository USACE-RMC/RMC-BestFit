<!-- technical-reference-status: complete -->

# Time-Series Models and Analysis Workflow

[Technical reference](../index.md) | [Rating curve](rating-curve.md)

## Chapter Map

| Model | Mathematical treatment | Principal API |
|---|---|---|
| AR($p$) | [Autoregressive models](autoregressive.md) | `AutoRegressive`, `ARAnalysis` |
| MA($q$) | [Moving-average models](moving-average.md) | `MovingAverage`, `MAAnalysis` |
| ARIMA($p,d,q$) | [Integrated ARMA](arima.md) | `ARIMA`, `ARIMAAnalysis` |
| ARIMAX($p,d,q,b$) | [Regression, covariates, trend, and seasonality](arimax.md) | `ARIMAX`, `ARIMAXAnalysis` |

All four models use Gaussian conditional likelihoods on an optional transformed response, bounded marginal parameter priors, Bayesian MCMC as the primary analysis estimator, and posterior-predictive simulation for uncertainty curves. They do not share one interchangeable parameter ordering; use each chapter's crosswalk.

## Shared Statistical Contract

For raw response $y_t$ and configured transform $z_t=g(y_t)$, the model target is

$$
p(\theta\mid y_{0:T-1})\propto
\left\{\prod_{t\in\mathcal I}
\frac{1}{\sigma}\varphi\!\left(\frac{e_t(\theta)}{\sigma}\right)
\left|g'(y_t)\right|\right\}\pi(\theta), \tag{TS.1}
$$

where $\mathcal I$ is the model-specific conditional-likelihood index set. None of the classes implements an exact initial-state/Kalman likelihood. Response intervals must be regular; missing timestamps are not imputed; coefficients and innovation variance are fixed through time.

For ARIMA and ARIMAX with raw training length $T$ and differencing order $d$, model step $k$
maps to raw response index $k+d$, and the training model series has $T-d$ values. ARIMAX uses
level covariates matched by exact timestamp at that raw index; covariates are never differenced.
Conditional evaluation starts at $k=\max(p,q)$, so the transform Jacobian uses raw indices
$d+\max(p,q)$ through $T-1$. Required missing or duplicate ARIMAX covariate timestamps invalidate
evaluation; extra dates outside the required window are ignored.

When the optional Jeffreys rule is enabled, each model adds the scale contribution
$\log \pi_J(\sigma)=-\log(\sigma)$ for $\sigma>0$. Pointwise prior diagnostics classify this
term as `JeffreysScalePrior`, separately from each parameter's configured marginal
`ParameterPrior`; the sum of all pointwise prior components equals the scalar prior likelihood at
valid parameter sets. This classification is diagnostic metadata and does not alter the prior.

The innovation scale domain is finite $\sigma>0$. Zero, negative, NaN, and infinite scales are
impossible numerical parameter evaluations: scalar data/prior likelihoods return negative
infinity, and pointwise data/prior decompositions retain their configured lengths and metadata
with a negative-infinity scale contribution. They do not construct a Gaussian distribution or
throw. This evaluation guard does not narrow the configured positive parameter bounds.

`Transform.None`, logarithmic Box–Cox, fitted Box–Cox, and fitted Yeo–Johnson are supported.
Transformation fitting is preprocessing, not part of $\theta$. Box-Cox and Yeo-Johnson lambda are
fit from raw observations `[0, TrainingTimeSteps)` and then frozen while the transform is applied
to the full response. Consequently, changing holdout values cannot change the training transform,
Jacobian, likelihood, or defaults. The effective read-only `TransformLambda` is fitted
automatically unless assigned through `SetTransformParameters`. A manual lambda remains fixed
when only the training window changes; a fitted lambda refits when the response, training window,
or transform type changes. None and logarithmic transforms use canonical lambda zero.

Transform state is rebuilt atomically before numerical results are reused. Existing XML remains
valid; optional invariant-culture `TransformLambda` and `TransformLambdaIsManual` attributes
preserve the effective exponent and its provenance through save/open and clone/copy workflows.
The setter's second argument, `lambda2`, remains accepted but is intentionally ignored for API
compatibility; it is not a shift or offset parameter. See [TR-036](../review-findings.md#tr-036)
and [TR-046](../review-findings.md#tr-046).

## Analysis Lifecycle

`ARAnalysis`, `MAAnalysis`, `ARIMAAnalysis`, and `ARIMAXAnalysis` follow the common `AnalysisBase` lifecycle:

1. Validate model data, orders, parameter bounds, training length, and forecast horizon.
2. Initialize `BayesianAnalysis`, ordinarily with MLE-derived screening/starting behavior managed by the analysis.
3. Run Bayesian sampling asynchronously with progress and cancellation propagation.
4. Select the posterior mean or sampled MAP state for the displayed deterministic curve.
5. Generate uncertainty realizations from joint posterior draws and seeded innovations.
6. Publish model-scale results, original-scale curves, DIC, RMSE, and analysis metadata.

`ForecastingTimeSteps` on an analysis means steps beyond the complete observed response in the published result. Internally the analysis passes `observedCount - TrainingTimeSteps + ForecastingTimeSteps` to `Predict`, so the returned curve includes the training region, held-out observed region, and future horizon. Forecasting is capped at 100 steps.

The uncertainty result is posterior predictive: parameter uncertainty, innovation noise, nonlinear inverse transformation, and—where applicable—covariate extension are combined. It is not a confidence band for the conditional mean. Report the seed, posterior draw count, transform, training endpoint, and covariate scenario.

## Model Selection and Diagnostics

Choose orders using scientific plausibility, ACF/PACF as exploratory tools, residual diagnostics, and genuinely held-out predictive performance. ACF/PACF patterns are asymptotic heuristics and are distorted by trend, seasonality, transformations, outliers, and short records. After fitting, examine:

- response and residual time plots;
- residual ACF/PACF and portmanteau checks at hydrologically relevant lags;
- innovation normality and variance stability;
- characteristic roots and their posterior uncertainty;
- chain mixing, R-hat, and ESS under the limitations in [MCMC diagnostics](../estimation/diagnostics.md);
- holdout coverage and proper scores on the original decision scale;
- sensitivity to training window, transform, order, and covariate specification.

AR, MA, ARIMA, and ARIMAX analyses compute AIC/BIC from each model's data log likelihood evaluated at the stored MAP; prior-density terms are excluded. The values agree with MLE criteria only when every active prior is constant and MAP coincides with the constrained MLE. The default Jeffreys scale option is nonconstant, so analyses using it—or any informative prior—should use DIC, WAIC, or verified PSIS-LOO for Bayesian comparison rather than treating the MAP-evaluated fields as conventional AIC/BIC. See [TR-042](../review-findings.md#tr-042).

## Current Scientific Availability

| Configuration | Estimation likelihood | Forecast/predictive simulation |
|---|---|---|
| AR/MA, no fitted transform | Available subject to conditional-likelihood assumptions | Available subject to diagnostic checks |
| AR/MA with fitted transform | Available with training-only frozen lambda | Back-transform is median-like; transform uncertainty omitted |
| ARIMA/ARIMAX with $d=0$, no transform | Available | Available, subject to ARIMAX covariate scenario |
| ARIMA with $d>0$ | Conditional likelihood can be inspected | Prediction available with verified reintegration; predictive simulation unavailable under TR-038 |
| ARIMAX with $d>0$ | Available with exact-date level covariates and conditional Jacobian alignment | Prediction available with verified reintegration/date alignment; predictive simulation unavailable under TR-039 |
| ARIMA transformed/differenced simulation | — | Unavailable: TR-038 |
| ARIMAX transformed/differenced simulation | — | Unavailable: TR-039 |

This table is deliberately conservative because the software supports life-safety work. A finite result is not evidence that a defective path is safe to publish.

## Hydrologic Reporting Checklist

State the variable and units, interval, calendar/water-year convention, missing-data treatment, training and holdout dates, transformation and fitting subset, model orders, intercept meaning, seasonal period, covariate units/lags/future scenario, priors, sampler/seed, diagnostic thresholds, residual results, parameter roots, forecast target (median/mean/realization), and uncertainty components. Distinguish operational forecasts conditional on known covariates from scenarios requiring future precipitation, regulation, or climate assumptions.

## Traceability and References

Implementation is under `Models/TimeSeries/`; orchestration is under `Analyses/TimeSeries/`. Fast
tests are under `RMC.BestFit.Tests/TimeSeriesModels/` and `TimeSeriesAnalysis/`. Focused Phase 5
Verification methods are under the matching `RMC.BestFit.Verification` directories and are run
only by exact fully qualified name; the complete Verification project is not run.

<a id="ref-1"></a>[1] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-2"></a>[2] R. H. Shumway and D. S. Stoffer, *Time Series Analysis and Its Applications*, 4th ed., Springer, 2017.

<a id="ref-3"></a>[3] R. J. Hyndman and G. Athanasopoulos, *Forecasting: Principles and Practice*, 3rd ed., OTexts, 2021.
