<!-- verification-status: publication-draft -->

# Estimation, Model Comparison, and Diagnostics

## Verification objectives

This chapter verifies estimator objective conventions, Gaussian information combination, nuisance-parameter profiling, GMM specification and covariance, information criteria, PSIS-LOO diagnostics, and rank-normalized MCMC convergence diagnostics.

## MLE, MAP, and GMM equivalence fixture

The deterministic Log10-Normal sample in equation (D.1) has $n=7$, mean 2, and centered sum of squares 2.52. Exact likelihood inference uses divisor $n$, while the Bulletin 17C moment estimator deliberately uses $n-1$:

$$\widehat\sigma^2_{\mathrm{MLE}}=0.36,\qquad
\widehat\sigma^2_{\mathrm{GMM}}=0.42.\tag{E.1}$$

Flat-prior MAP reproduced the MLE. GMM reproduced the sample product moments. When independent Gaussian information $\mu\sim N(m,\tau^2)$ was added, both estimators reproduced inverse-variance weighting,

$$V_*=(V_L^{-1}+\tau^{-2})^{-1},\qquad
\mu_*=V_*(\widehat\mu_L/V_L+m/\tau^2).\tag{E.2}$$

Wide-centered, narrow-centered, and narrow-shifted regimes verified negligible influence, variance contraction, and the expected location shift. Mean and variance comparisons used scaled tolerances from `2e-5` through `2e-3`, depending on the numerical Hessian path; all passed.

## Profile likelihood and covariance

The MLE and flat-prior MAP profile calculations were compared with R `bbmle` and a closed-form nuisance optimum for a correlated quadratic objective. Every profile ordinate and the central 90% interval passed. A one-parameter Normal-mean covariance test reproduced the closed form $\sigma^2/n$, and an interior flat-prior MAP reported the same covariance within the declared `1e-4` numerical-Hessian tolerance.

## DIC and WAIC

R 4.4.3, `BayesianTools` 0.1.9, and `loo` 2.10.0 evaluated five exact Normal observations under forty deterministic posterior draws. The complete 40 by 5 pointwise log-likelihood matrix was preserved in the committed oracle.

| Quantity | External result | BestFit tolerance | Result |
|---|---:|---:|---:|
| Mean deviance | 13.0457484595105 | `1e-10` absolute | Passed |
| Deviance at posterior mean | 12.6377539947693 | `1e-10` absolute | Passed |
| Effective DIC parameters | 0.407994464741220 | `1e-10` absolute | Passed |
| DIC | 13.4537429242518 | `1e-10` absolute | Passed |
| lppd | -6.35851654848624 | `1e-10` absolute | Passed |
| Effective WAIC parameters | 0.351001866165060 | `1e-10` absolute | Passed |
| WAIC | 13.4190368293026 | `1e-10` absolute | Passed |

The tests first compare each pointwise BestFit value with the external matrix, preventing an aggregate match from masking a decomposition error.

## PSIS-LOO

The same fixture was passed through R `loo`. BestFit compared every pointwise ELPD value, smoothed importance weight, Pareto shape estimate, and effective sample size as well as the aggregate result.

| Quantity | R `loo` result | BestFit tolerance | Result |
|---|---:|---:|---:|
| ELPD-LOO | -6.70094841652149 | `1e-10` absolute | Passed |
| Effective LOO parameters | 0.342431868035246 | `1e-10` absolute | Passed |
| LOOIC | 13.4018968330430 | `1e-10` absolute | Passed |
| SE(LOOIC) | 1.95756902272540 | `1e-10` absolute | Passed |

The five Pareto values were `(0.0682, -0.0302, 0.4116, 0.3062, 0.3209)`. For 40 retained draws the package threshold is `0.375803649418215`, so the third observation is correctly flagged. Six additional 256-ratio fixtures covered bounded, light, moderate, high, nonfinite-mean, and degenerate tails. Smoothed weights, Pareto $k$, and effective sample sizes agreed within `1e-8`; the degenerate fixture correctly returned positive infinity.

## GMM specification and covariance

A one-parameter, two-moment fixture was evaluated independently with R `gmm` 1.9.1. For fixed identity weighting, R obtained

$$\widehat\theta=2.28992700729929,\qquad Q=0.317956204379562.\tag{E.3}$$

For efficient two-step GMM, both implementations obtained $\widehat\theta=1.93548454750500$ and $Q=1.00755078518454$. The Hansen statistic was

$$J=nQ=10.0755078518454,\qquad p=0.00150253202968641.\tag{E.4}$$

| Comparison | R or analytical value | Tolerance | Result |
|---|---:|---:|---:|
| Fixed-weight variance | 0.145652392138090 | `1e-8` | Passed |
| Efficient two-step variance | 0.132600447299456 | `1e-8` | Passed |
| Hansen $J$ | 10.0755078518454 | `1e-7` | Passed |
| Hansen p-value | 0.00150253202968641 | `1e-8` | Passed |

The external generator reconstructs the centered IID sandwich directly from the bread and meat before writing the artifact. This independently checks the package output and the production calculation.

## MCMC diagnostics

Rank-normalized split $\widehat R$, bulk ESS, and tail ESS were compared elementwise with R `posterior` 1.7.0 using committed chain arrays. BestFit uses the conservative minimum of bulk and tail ESS when a single ESS value is required. Every tested diagnostic agreed with the external oracle within its stored tolerance.

Sampler acceptance terminology was also checked. For NUTS, the generic transition counter is not used as the tuning statistic; BestFit stores the post-warmup mean Hamiltonian acceptance probability in the result field presented to users. ARWMH covariance adaptation uses realized states, including repeated states after rejection, matching the declared adaptive-chain history.

## Conclusion

The tested estimator objectives, profiles, covariance calculations, information criteria, PSIS diagnostics, and rank-normalized convergence measures agree with analytical calculations or the declared R packages. MAP-evaluated AIC and BIC use the data likelihood at the stored MAP; when priors are nonconstant they must not be interpreted as conventional MLE AIC/BIC. Exact leave-one-out refits, moment matching, and chain-relative-efficiency adjustment for PSIS are not implemented and are not claimed.
