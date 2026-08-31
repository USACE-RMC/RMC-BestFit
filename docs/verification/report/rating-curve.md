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

MLE acceptance requires same-point likelihood agreement, optimizer optimality, coordinate tolerances, curve parity within 0.5%, and the fitted curve within 10% of the generating curve. Bayesian acceptance requires $\widehat R<1.1$, ESS greater than 100, sampled-MAP parity, and MAP-curve parity within 2%. Central 90% curve-band inclusion is reported for this independent-optimum replication evidence, not asserted as generated-parent recovery.

| Bayesian model | Grid stages containing truth | Grid stages tested | Result |
|---|---:|---:|---:|
| One control | 36 | 36 | Passed |
| Two controls | 36 | 36 | Passed |
| Three controls | 34 | 36 | Passed |

## Reconciled recovery

The former ten-by-two synthetic matrix used arbitrary 5-50% coordinate bands and included redundant
width, slope, range, and sample-size names. Chunk 12 retained five scientifically distinct N=1000
fixtures for each estimator: standard single-control, low-noise error behavior, wide calibration range,
bankfull two-control activation, and three-control activation. `SingleSegment_LargeSample`,
`SingleSegment_SteepChannel`, `SingleSegment_WideChannel`, `TwoSegment_Default`, and
`ThreeSegment_Default` were removed from both classes as deliberate design consolidations; their
historical pass claims were not transferred.

The single-control MLE cells require every physical coordinate within 1.96 unregularized
observed-information standard errors. Multi-control MLE cells apply that raw-coordinate rule only to
the identified scale. Every MLE fit uses production Differential Evolution with untouched default
tolerances. For response recovery, 20,000 bounded multivariate-Normal draws propagate the full MLE
covariance through the nonlinear log10 curve (seed 20260831), and independent draw-specific log10
residuals supply observation uncertainty (seed 20260901). A max-|t| critical value calibrated across the
complete response grid gives one simultaneous 95% predictive band rather than several pointwise bands.

Bayesian cells require R-hat below 1.10 and ESS at least 100 for every coordinate; identifiable coordinate
truths must lie inside central 95% posterior intervals. Posterior MAP is the recovery point estimator.
Retained posterior draws propagate parameter uncertainty, independent draw-specific log10 residuals
(seed 20260902) propagate observation uncertainty, and the same max-|t| construction gives a MAP-centered
simultaneous 95% posterior-predictive band over the complete grid. The point estimator does not replace
the coordinate interval, diagnostic, or predictive-band rules. Parent values must lie inside every prior
support.

| Exact current identity | Result | Current evidence or gap |
|---|---:|---|
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_Default` | Passed | 1 result; 0.652 s; result directory `20260831-101709-...` |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_LowNoise` | Passed | 1 result; 0.744 s; result directory `20260831-101715-...` |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_WideRange` | Passed | 1 result; 0.695 s; result directory `20260831-101722-...` |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_TwoSegment_BankfullTransition` | Passed | 1 result; 2.947 s; exact allocation 495/505; stage-6.5 simultaneous band `[578.013, 950.262]` contains parent 724.391 |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_ThreeSegment_MultipleControl` | Passed | 1 result; 10.573 s; exact allocation 270/406/324; stage-8.5 simultaneous band `[1695.479, 2851.016]` contains parent 2144.093 |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_Default` | Passed | 1 result; 22.289 s; result directory `20260831-101837-...` |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_LowNoise` | Passed | 1 result; 23.979 s; result directory `20260831-101905-...` |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_WideRange` | Passed | 1 result; 23.058 s; result directory `20260831-101935-...` |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_TwoSegment_BankfullTransition` | Passed | 1 result; 57.632 s; stage-6.5 simultaneous band `[576.299, 953.454]` contains parent 724.391 |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_ThreeSegment_MultipleControl` | Passed | 1 result; 88.537 s; stage-8.5 simultaneous band `[1701.739, 2838.807]` contains parent 2144.093 |

The superseded pointwise checks omitted the residual term from the declared log10 observation model and
required every one of six or nine pointwise 95% intervals to pass, which is not one 95% grid-wide
statement. The corrected predictive construction was approved before its final evidence runs. It changes
no parent, fixture seed, prior, sampler, chain, convergence rule, response grid, production optimizer,
likelihood, or fitted realization. Exact equivalence with R `bdrc` is not claimed because its
additive-control law, base-10 error density, segmentation, and parameter order have not been established
as identical.

## Conclusion

Rating-curve discharge-space likelihood, activation continuity, all six example-replication cells, and
all ten current reconciled recovery identities are verified. The current rating-curve inventory therefore
has 22 verified declarations and no open rating-curve gaps. The evidence does not establish `bdrc` parity,
arbitrary control-matrix parity, or predictive coverage in extrapolation.
