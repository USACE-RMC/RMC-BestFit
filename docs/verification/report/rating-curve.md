<!-- verification-status: publication-draft -->

# Rating-Curve Analysis

## Discharge-space likelihood

The rating curve is a one-, two-, or three-control piecewise power law. Measurement error is Normal in base-10 log discharge, but the public data likelihood is a discharge-space density. The independent SciPy artifact therefore includes the change of variables for every positive observed discharge.

| Model | Independent discharge-space log likelihood at truth | BestFit tolerance | Result |
|---|---:|---:|---:|
| One control | -1577.7594156516989 | `1e-8` | Passed |
| Two controls | -1850.3134085800252 | `1e-8` | Passed |
| Three controls | -1888.5687896304166 | `1e-8` | Passed |

Scalar, pointwise, and component likelihoods agree with the SciPy density. The Numerics base-10 LogNormal density independently reproduces the same terms.

## Activation continuity

At an activation stage $h_k$, an added control contributes $10^{a_k}(h-h_k)^{\beta_k}$. The analytical cells evaluate offsets from `1e-3` through `1e-9` and exponents 0.1, 1, 1.67, and 2.5. The two-sided increment agrees with the power-law formula within `1e-10` relative tolerance. With the default lower bound $\beta=0.1$, the increment ratio between offsets `1e-12` and `1e-6` is

$$(10^{-12}/10^{-6})^{0.1}=0.2511886432.\tag{R.1}$$

The contribution therefore vanishes continuously at activation. All seven continuity and bound cells passed.

## Recovery

The publication recovery fixture uses 1,000 observations generated from the same source-project parameterization as the worked examples. The independent optimum uses SciPy multistart least squares on base-10 residuals with a profiled scale.

| Model | Generating parameters | Independent MLE parameters | Result |
|---|---|---|---:|
| One control | `[1, 0.243897, 2.666667, 0.05]` | `[1.000633, 0.244804, 2.664527, 0.052251]` | Passed |
| Two controls | `[1, 0.243897, 2.666667, 10, 2.946300, 1.67, 0.05]` | `[1.000989, 0.246160, 2.661335, 9.981416, 2.936743, 1.678032, 0.052240]` | Passed |
| Three controls | `[1, 0.243897, 2.666667, 10, 2.946300, 1.67, 15, 3.609533, 1.67, 0.05]` | `[1.001032, 0.246300, 2.660905, 9.936705, 2.899111, 1.730227, 15.332091, 3.828382, 1.341175, 0.051935]` | Passed |

MLE acceptance requires same-point likelihood agreement, optimizer optimality, coordinate tolerances, curve parity within 0.5%, and the fitted curve within 10% of the generating curve. Bayesian acceptance requires $\widehat R<1.1$, ESS greater than 100, sampled-MAP parity, MAP-curve parity within 2%, and coverage of the generating curve by the central 90% band.

| Bayesian model | Grid stages containing truth | Grid stages tested | Result |
|---|---:|---:|---:|
| One control | 36 | 36 | Passed |
| Two controls | 36 | 36 | Passed |
| Three controls | 34 | 36 | Passed |

Twenty additional synthetic recovery cells, ten MLE and ten Bayesian, used 1,000 observations and production defaults; all passed.

## Conclusion

Rating-curve discharge-space likelihood, activation continuity, example replication, and the twenty supporting recovery cells passed all 36 reported cells. The evidence does not establish parity for arbitrary control matrices or uncertainty coverage in extrapolation.
