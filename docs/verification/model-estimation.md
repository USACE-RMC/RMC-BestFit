# Model Estimation and Diagnostics Verification

## Status

Planned after the TR-001 off-ramp is resolved.

## Log10-Normal prior and penalty experiment

A deterministic symmetric log10 dataset and an outlier variant will compare MLE, flat-prior MAP, and Bulletin 17C GMM. A Gaussian prior on \(\mu\) is mapped to the quadratic penalty using the implementation convention \(\mathrm{MSE}=\tau_\mu^2/n\), subject to the TR-033 objective-scale correction.

| Regime | Prior scale and center | Expected location effect | Expected variance effect |
|---|---|---|---|
| Wide centered | \(100SE_L\), centered at MLE | None | Negligible |
| Narrow centered | \(0.1SE_L\), centered at MLE | None | Large contraction |
| Narrow shifted | \(0.1SE_L\), shifted by \(2SE_L\) | Material shift | Large contraction |

Exact leave-one-out refits provide the observation-influence oracle. Full-Hessian and deletion covariance comparisons provide the leverage oracle; no sum-to-\(p\) assertion is accepted without a derivation.

## External model-comparison oracles

- R `loo` 2.10.0 for WAIC and PSIS-LOO.
- R `BayesianTools` 0.1.9 for the selected DIC convention.
- R `gmm` 1.9-1 for estimates, covariance, and Hansen's \(J\).
- R `bbmle` 1.0.25.1 for nuisance-reoptimized profiles.
- Python ArviZ 1.2.0 as a secondary WAIC/LOO comparison.

The fixtures will use committed deterministic posterior draws and pointwise log-likelihood matrices so package parity is not confounded with sampler randomness.
