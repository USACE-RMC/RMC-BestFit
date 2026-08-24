<!-- verification-status: publication-draft -->

# Time-Series Analyses

## AR, MA, ARIMA, and ARIMAX oracle design

The fixtures were generated independently in R without calling BestFit or Numerics. Each fixture discards 110 initialization steps and retains 1,000 observations. The deterministic oracle set independently reconstructs scale priors, invalid-scale behavior, transform fitting, conditional likelihood alignment, prediction recurrence, prediction variance, transformed generation, and information criteria.

| Oracle group | Independent calculation | Tolerance | Result |
|---|---|---:|---:|
| Jeffreys scale metadata | Four scalar scale-prior evaluations | `1e-12` | Passed |
| Invalid scale | Scalar and pointwise Gaussian likelihood and prior | `1e-12`; exact rejection | Passed |
| Transform parameter | R training-only Box-Cox/Yeo-Johnson profiles | Stored cross-language tolerance | Passed |
| Manual transform rebuild | Independently transformed likelihood | Stored cross-language tolerance | Passed |
| ARIMAX alignment | Date-indexed differencing, covariates, lags, and Jacobian | `1e-10` | Passed |
| ARIMA/ARIMAX reintegration | Hand recurrence for irregular holdout boundaries | `1e-10` | Passed |
| Forecast uncertainty | Boundary-conditioned variance recurrence | `1e-10` | Passed |
| Transformed forecasts | Model-scale recurrence followed by one inverse transform | `1e-10` | Passed |
| AR/MA transformed generation | Algebraic and 1,000-step moment oracles | Declared moment bounds | Passed |
| ARIMA transformed generation | Independent transformed/differenced recurrence | Declared moment bounds | Passed |
| ARIMAX transformed generation | Independent date/covariate recurrence | Declared moment bounds | Passed |
| Information criteria | Data likelihood at stored MAP, prior excluded | `1e-10` | Passed |

The ARIMA forecast oracle anchors the first prediction to the final observed transformed level and keeps AR and MA recurrences on the model scale until one inverse response transform. For the declared ARIMA(1,1,1) fixture, the next zero-innovation difference is `-0.0290494944510721` and the next original-scale response is `332.259097907995`. For the ARIMAX fixture the corresponding values are `-1.2555476231495` and `251.211255651498`.

## Recovery

| Model and estimator | Generating coordinates | Sample size | Acceptance | Result |
|---|---|---:|---|---:|
| AR(1), MLE | $\mu=10$, $\phi=0.6$, $\sigma=5$ | 1,000 | Existing coefficient and scale gates | Passed |
| AR(1), Bayesian | Same | 1,000 | Central interval, $\widehat R<1.1$, ESS greater than 100 | Passed |
| MA(1), MLE | $\mu=10$, $\theta=0.6$, $\sigma=5$ | 1,000 | 5% parameter gate | Passed |
| MA(1), Bayesian | Same | 1,000 | Central interval, $\widehat R<1.1$, ESS greater than 100 | Passed |
| ARIMA(1,1,1), MLE | log transform; $\phi=0.45$, $\theta=0.25$, $\sigma=0.04$ | 1,000 | Independent conditional optimum and forecast oracle | Passed |
| ARIMA(1,1,1), Bayesian | Same | 1,000 | Independent posterior MAP, 95% interval, diagnostics | Passed |
| ARIMAX(1,1,0), MLE | intercept 0.25, $\beta=1.5$, $\phi=0.4$, $\sigma=0.5$ | 1,000 | Date-indexed independent optimum | Passed |
| ARIMAX(1,1,0), Bayesian | Same | 1,000 | Independent posterior MAP, 95% interval, diagnostics | Passed |

An additional matrix covers 29 AR, MA, ARIMA, and ARIMAX configurations with 1,000 observations. Seven MLE cells retained their parameter tolerances. Twenty-two Bayesian cells require every generating value inside the central 90% interval and $\widehat R<1.1$. All 29 passed. The independent eight-cell matrix above remains the primary oracle-backed recovery evidence.

## Conclusion

Transform, likelihood, recurrence, forecast-boundary, uncertainty, generation, and information-criterion calculations passed twelve independent oracle groups. The eight principal recovery cells and 29 supporting recovery cells passed within their declared scope.
