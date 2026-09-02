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

### Autoregressive model

The AR(1) fixture retains 1,000 observations after 110 discarded initialization values and uses physical
order `[intercept, phi, sigma] = [10, 0.6, 5]`. MLE requires absolute standardized error no greater than
1.96 from unregularized observed information. Bayesian recovery requires every parent inside its central
95% interval, $\widehat R<1.10$, and ESS at least 100. The Bayesian identity passed a fresh exact
one-result run in 28.046 seconds; the MLE identity retains its final guarded evidence from Chunk 13.

### Moving-average model

The MA(1) fixture uses physical order `[intercept, theta, sigma] = [10, 0.6, 5]` and the same retained
N=1000/initialization design. MLE uses the observed-information 1.96-standard-error rule; Bayesian recovery
uses central-95% parent inclusion plus $\widehat R$ and ESS diagnostics. The source-affected Bayesian
identity passed a fresh exact one-result run in 19.855 seconds.

### ARIMA model

The ARIMA(1,1,1) fixture applies a log transform, one difference, and physical order
`[phi, theta, sigma] = [0.45, 0.25, 0.04]`; no separate intercept or drift is fitted. MLE requires the
production and R optima to occupy the four-dimensional joint 95% likelihood-ratio region, generating
truth inside each independently generated one-coordinate 95% profile, and same-point likelihood and
forecast recurrence parity. Bayesian recovery requires generating truth inside every central 95% posterior
interval, $\widehat R<1.10$, and ESS at least 100; sampled-MAP percentage bands are not used. The fresh
MLE and Bayesian identities passed in 0.661 and 32.295 seconds.

### ARIMAX model

The ARIMAX(1,1,0) fixture uses level-space covariate dates, one difference, regression/innovation order
`[intercept, beta, phi, sigma] = [0.25, 1.5, 0.4, 0.5]`, and no MA coordinate. MLE requires the production
and R optima and generating truth inside the independent four-coordinate joint 95% likelihood-ratio
region, plus same-point likelihood and date-indexed forecast parity. Bayesian recovery requires central-95%
truth inclusion, $\widehat R<1.10$, and ESS at least 100 without a sampled-MAP percentage rule. The fresh
MLE and Bayesian identities passed in 1.612 and 48.718 seconds.

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
