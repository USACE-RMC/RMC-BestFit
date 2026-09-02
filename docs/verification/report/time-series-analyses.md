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
| AR(1), MLE | $\mu=10$, $\phi=0.6$, $\sigma=5$ | 1,000 | Observed-information absolute standardized error no greater than 1.96; BestFit Differential Evolution with minimum population 100, midpoint boundary repair, and unchanged Numerics tolerances | Passed, exactly one final guarded result |
| AR(1), Bayesian | Same | 1,000 | Central interval, $\widehat R<1.1$, ESS greater than 100 | Passed |
| MA(1), MLE | $\mu=10$, $\theta=0.6$, $\sigma=5$ | 1,000 | Observed-information absolute standardized error no greater than 1.96 | Passed, exactly one guarded result |
| MA(1), Bayesian | Same | 1,000 | Central interval, $\widehat R<1.1$, ESS greater than 100 | Passed |
| ARIMA(1,1,1), MLE | log transform; $\phi=0.45$, $\theta=0.25$, $\sigma=0.04$ | 1,000 | Production/R optima in the joint 95% likelihood-ratio region; truth inside all three independent one-coordinate 95% profiles; same-point likelihood and forecast oracle | Passed |
| ARIMA(1,1,1), Bayesian | Same | 1,000 | Independent posterior MAP, 95% interval, diagnostics | Passed |
| ARIMAX(1,1,0), MLE | intercept 0.25, $\beta=1.5$, $\phi=0.4$, $\sigma=0.5$ | 1,000 | Production/R optima and generating truth inside the independent joint 95% likelihood-ratio region; same-point likelihood and date-indexed forecast oracle | Passed |
| ARIMAX(1,1,0), Bayesian | Same | 1,000 | Independent posterior MAP, 95% interval, diagnostics | Passed |

The historical 29-cell matrix is no longer current evidence. The completeness reconciliation removed its redundant
Cartesian identities rather than transferring old passes. The retained eight recovery cells remain
the estimator evidence and all now pass. Four new independent Python cells cover first-order conditional objectives, identified
higher-order AR/MA responses, pure AR/MA behavior through ARIMA, and a date-discriminating combined
ARIMAX trend-seasonality-two-covariate interaction.

## Current time-series coverage

| Family | Current distinct coverage | Disposition |
|---|---|---|
| AR | MLE/Bayesian AR(1) recovery; independent AR(1) objective/optimum; N=1000 AR(2) recurrence and root response | Passed after the approved DE reliability correction; two new oracle identities pass |
| MA | MLE/Bayesian MA(1) recovery; independent MA(1) CSS objective/optimum; N=1000 MA(2) recurrence and invertibility response | Passed |
| ARIMA | Direct independent ARIMA(2,0,0) and ARIMA(0,0,2) conditional cells; mixed ARMA, differencing, log transform, reintegration, conditional likelihood, and MLE/Bayesian ARIMA(1,1,1) recovery | Passed retained and new identities; redundant order grid removed |
| ARIMAX | One dated level covariate with differenced-scale drift plus a separate ARMA(1,1), conditional/regression intercept, linear trend, monthly seasonality, and two offset-start covariates whose correct date alignment is discriminated from index alignment | Passed retained and new identities; Cartesian variants removed |

## Conclusion

Transform, likelihood, recurrence, forecast-boundary, uncertainty, generation, and information-criterion calculations pass their independent oracle groups. All eight retained estimator-recovery identities are verified. The 29-cell historical support grid is deliberately not counted as current evidence.
