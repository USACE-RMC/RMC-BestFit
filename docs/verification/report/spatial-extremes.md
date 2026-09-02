<!-- verification-status: publication-draft -->

# Spatial-Extremes Analysis

## Likelihood oracle

The fixture has five sites, twelve row/year blocks, and structured missing observations. An R `mvtnorm` generator evaluates Hosking-sign GEV margins, the Gaussian copula on the observed-site correlation submatrix, latent Gaussian-process densities, and finite-difference gradients.

| Row | Observed sites | Independent marginalized log likelihood |
|---:|---|---:|
| 5 | 1, 3, 4, 5 | -23.7536861453931 |
| 6 | 2, 3, 4 | -14.894701 |
| 7 | 1, 2, 4, 5 | -21.970089 |
| 8 | 1, 5 | -9.952734 |
| 9 | None | 0 |

The full marginalized data log likelihood is `-238.89272936973`. BestFit agrees within `1e-8`; every row agrees within `1e-10` and sums to the scalar result.

| Spatial likelihood comparison | Acceptance | Result |
|---|---:|---:|
| Missing-site scalar marginalization | `1e-8` | Passed |
| Missing-site pointwise rows | `1e-10` | Passed |
| Complete-row copula likelihood | Stored oracle tolerance | Passed |
| Marginal-only GEV likelihood | Stored oracle tolerance | Passed |
| Posterior-kernel decomposition | Exact data plus prior identity | Passed |
| Data likelihood excludes process density | Stored oracle tolerance | Passed |
| Scalar and pointwise decomposition | Stored oracle tolerance | Passed |
| Scalar and pointwise gradients | `1e-4` | Passed |

The criteria cell used eleven nonempty row/year blocks. AIC and BIC were recomputed from the observation likelihood at the sampled MAP within `1e-8`; WAIC and its effective-parameter term agreed with a row/year recomputation within `1e-9` relative; one Pareto value was produced per row/year.

## Prediction, distance, uncertainty, and recovery

| Verification area | Independent reference or design | Result |
|---|---|---:|
| Conditional prediction | R Gaussian-process conditional mean and variance, 15 cases, `1e-10` | Passed |
| Geodesic distance | R haversine distance, correlation, and kriging moments | Passed |
| Cartesian distance | Exact planar Euclidean distances | Passed |
| Leave-one-site-out | Independently reduced models with held-out covariates | 3 of 3 passed |
| Ungauged prediction | Posterior mean of per-draw conditional GP prediction | Passed |
| Regional curve | Posterior of the per-draw regional mean quantile | Passed |
| Dependent simulation | 20,000 rows; correlations within 0.02; quantiles within 3% | Passed |
| Bayesian interval inflation | Exact centre-plus-`sqrt(VIF)` analytical transformation | Passed |
| Godambe path | Independently differentiated sensitivity H, row-score J, and sandwich covariance | Passed |
| Temporal block bootstrap | Independent whole-row MT19937 resamples and five SciPy bounded MAP fits | Passed |

### N=1000 recovery

The approved recovery design uses ten sites with 100 observations each, so total scalar N is 1,000. The
estimator sees 100 complete ten-site row/year vectors and therefore 100 multivariate likelihood contributions;
MCMC draws are not counted as N. Location and scale use log links, shape is physical, and copula range is
physical. All partial Cartesian grids contain ten sites. The location-regression design uses the X and Y coordinate columns in
`log(location)=beta0+betaX X+betaY Y`. The copula cells use exponential Gaussian dependence
`rho(h)=exp(-h/range)`.

| Retained recovery design | Coordinate order | Generator seed | Acceptance | Result |
|---|---|---:|---|---:|
| MLE homogeneous, 10 sites | `[log(location), log(scale), shape]` | 54321 | Unregularized observed-information absolute standardized error at most 1.96 for every coordinate | Passed, 1.716 s |
| MLE copula, 10 sites | `[range, log(location), log(scale), shape]` | 66666 | Same; singular or regularized information is failure | Passed, 4.931 s |
| Bayesian homogeneous, 10 sites | `[log(location), log(scale), shape]` | 12345 | Central 95% parent inclusion, R-hat below 1.10, ESS at least 100 for every coordinate | Passed, 68.472 s |
| Bayesian copula, 10 sites | `[range, log(location), log(scale), shape]` | 33333 | Same | Passed, 165.125 s |
| Bayesian location regression, 10 sites | `[beta0, betaX, betaY, log(scale), shape]` | 66666 | Same | Passed, 130.429 s |
| Bayesian positive shape 0.1, 10 sites | `[log(location), log(scale), shape]` | 11111 | Same | Passed, 71.070 s |
| Bayesian zero shape, 10 sites | `[log(location), log(scale), shape]` | 22222 | Same | Passed, 68.439 s |
| Bayesian negative shape -0.2, 10 sites | `[log(location), log(scale), shape]` | 33333 | Same | Passed, 78.168 s |

Every latest run produced exactly one executed passing TRX. MLE retains default Differential Evolution and
seed 12345. Bayesian recovery retains dimension-dependent production DEMCzs defaults and estimator seed
12345. Three-, four-, and five-coordinate models use 6/8/10 chains, thinning 30/40/50, and initial populations
300/400/500; all use 3,500 iterations, 1,750 warmup iterations, and output length 10,000. Recovery is
reported through central 95% parameter intervals with the declared R-hat and ESS diagnostics.
Priors, parameter bounds, initialization, and production defaults were not changed. These fixtures have no latent spatial-error field, so their intervals
represent parameter uncertainty rather than conditional-GP residual or predictive uncertainty.

The former `Bayesian_LargeSample_HasTighterEstimates` identity was consolidated without execution. Under the
approved 10-by-100 design it duplicated the homogeneous cell, and interval tightening by itself supplied no
predeclared precision-scaling oracle. No historical pass was transferred. The eight passes from the superseded
1,000-row design are not evidence for this design; all eight revised identities received fresh one-result TRXs.

## Chunk 14 independent oracles

The current completeness reconciliation separates scientific oracles from fast implementation contracts.
The former three cross-validation methods compared two BestFit production paths or fold accounting and are
retained only as non-executable design history; their historical passes are not current independent evidence.

| Current identity | Independent construction | Result |
|---|---|---:|
| `BasicExponentialCorrelation_MatchesAnalyticalGrid` | `rho(h)=exp(-h/range)` on five predeclared distances | Passed |
| `PoweredExponentialCorrelation_MatchesAnalyticalGrid` | `rho(h)=exp(-(h/range)^nu)`, `nu=1.6` | Passed |
| `SphericalCorrelation_MatchesAnalyticalGridAndCompactSupport` | spherical cubic plus exact support boundary | Passed |
| `HeldOutCopulaFold_MatchesIndependentFittedOracle` | independent SciPy three-site Gaussian-copula optimum, finite sign-safe four-coordinate joint 95% likelihood-ratio acceptance, and unregularized held-out quantile uncertainty | Passed fresh 1 September 2026 (`20260901-144228-...`) |
| `HeldOutCovariateFold_MatchesIndependentRegressionOracle` | executable normal-equation two-covariate OLS prediction and uncertainty split | Passed |

All five Chunk 14A identities passed individually with exactly one executed TRX result. The complete versus
missing fold distinction is covered by the independent complete-fold targets above and the fast missing-fold
status/accounting contract.

Chunk 14B removes the two same-production posterior recomputations and the three dispatch/accounting cells
from the current Verification declaration set. Their method bodies remain non-executable design history and
their fast owners are unchanged. Five independent replacements passed individually:

| Current identity | Independent construction | Result |
|---|---|---:|
| `UngaugedDrawSpecificPrediction_MatchesIndependentGeodesicGaussianOracle` | four draw-specific geodesic conditional-GP moments and physical log-link locations | Passed |
| `RegionalFixedDrawAggregation_MatchesIndependentPosteriorOracle` | nine fixed draws, three site covariates, three ordinates, central 95% Type-7 summaries | Passed |
| `GodambeSensitivityVariabilityAndSandwich_MatchIndependentOracle` | explicit finite-difference H, row-score J, and unregularized sandwich covariance | Passed |
| `TemporalBlockBootstrap_MatchesIndependentWholeRowOracle` | independent MT19937 whole-row wrapping blocks, five bounded SciPy flat-prior MAP fits, and fitted physical-parameter/site/regional quantile intervals | Passed |
| `VarianceInflation_UsesExactIndependentAnalyticalTransformation` | independent Pearson VIF and exact site/regional endpoint transformation | Passed |

Every latest Chunk 14B TRX contains exactly one executed passing result.

## Conclusion

The historical phase report counted 30 passing cells, but that number is not a current declaration count.
The completeness reconciliation replaces same-production-path cross-validation, prediction, and uncertainty
claims with ten independently targeted Chunk 14 cells and reconciles recovery to the eight current N=1000
experiments above. Spatial likelihood, missing-data marginalization, kriging, distance, criteria,
cross-validation, prediction, regional uncertainty, dependent simulation, and recovery remain bounded by the
documented network sizes, covariates, missingness patterns, correlation structures, and uncertainty sources.
Large-network performance, latent-error recovery, and simultaneous predictive coverage are not established by
the recovery matrix.
