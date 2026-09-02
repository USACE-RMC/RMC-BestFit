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
| Bayesian interval inflation | Width increased by the square root of the variance inflation factor | Passed |
| Godambe path | Explicit unavailable status for singular sensitivity; finite draws when available | Passed |
| Temporal block bootstrap | 20 fitted replicates with complete accounting | Passed |

Complete-data recovery added two MLE and seven Bayesian cells under production defaults. All nine passed. Bayesian durations ranged from 42 to 115 seconds.

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

The historical phase report counted 30 passing cells. The completeness reconciliation supersedes the three
same-production-path cross-validation cells with five independently targeted Chunk 14A identities, so that
historical total is not a current declaration count. Spatial likelihood, missing-data marginalization,
kriging, distance, criteria, cross-validation, prediction, regional uncertainty, dependent simulation, and
method dispatch remain covered subject to the documented network sizes, covariates, missingness patterns,
and correlation structures; spatial recovery is separately deferred to Chunk 15.
