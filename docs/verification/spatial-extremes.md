<!-- verification-status: draft -->

# Spatial Extremes Verification

This chapter records the Phase 6 verification of the spatial GEV model (`SpatialGEV`,
`SpatialGEVAnalysis`, the Gaussian copula, and the spatial regression errors). Batch 6.3 covers the
likelihood core: observed-site marginalization of missing sites (TR-048), the data/prior decomposition
of the latent Gaussian-process densities (TR-049), the consistency of the Godambe estimating equations
(TR-057), and the closure of the TR-055 criteria caveats. Batches 6.4 through 6.6 (cross-validation,
prediction, uncertainty, simulation, dispatch, naming, and distance metric) follow. The technical
treatment is in the [spatial extremes chapter](../technical-reference/spatial/spatial-extremes.md);
the findings are TR-048 through TR-062 in the [review register](../technical-reference/review-findings.md#tr-048).

## Status

| Item | State | Evidence |
|---|---|---|
| TR-048 missing-site copula marginalization | Confirmed defect (21 August 2026); fix plan pending approval | Scalar and pointwise cells reproduce the zero-placeholder full-dimensional value instead of the observed-site marginalized oracle |
| TR-049 data/prior decomposition | Confirmed defect (21 August 2026); fix plan pending approval | The data log likelihood carries the Gaussian-process density (+8.464); the pointwise sums do not; the posterior kernel itself is correct |
| TR-057 Godambe estimating equations | Confirmed defect (21 August 2026); fix plan pending approval | The scalar-likelihood score for the error scale is -31.38 while the pointwise score is 0 |
| TR-055 spatial criteria | Closed with caveats in Phase 2; row/year unit to be re-verified after TR-048/TR-049 | Source audit; deterministic test planned with the corrections |
| GEV, copula, and kernel conventions | Verified | Complete-row copula likelihood, marginal-only likelihood, and the posterior-kernel invariance guard pass against the `mvtnorm` oracle |
| TR-050 through TR-054, TR-056, TR-058 through TR-062 | Planned (Batches 6.4-6.6) | - |

No production code has changed. The complete Verification project was not run.

## Oracle

`verification/r/spatial-extremes/generate_spatial_copula_oracle.R` (SHA-256
`fa0063b84a158b2bd476cd21ad5ebf6973424c22147d0ea6a342545d3f43b2e8`; R 4.4.3, `mvtnorm` 1.4.2,
seed 20260821) defines a homogeneous five-site model on projected coordinates
`(0,0), (12,3), (25,0), (8,18), (30,22)` with Euclidean distances, exponential correlation
`exp(-h / range)`, log links for location and scale, and the Numerics (Hosking) GEV convention
`y = -log(1 - kappa (x - xi)/alpha)/kappa`, `log f = -(1 - kappa) y - exp(-y) - log alpha`. The
BestFit parameter vector is `[copula range 15, location intercept log 100, scale intercept log 30,
shape -0.1]`. Twelve rows are drawn through the Gaussian copula; rows 5-8 lose one to three sites and
row 9 is fully missing. The artifact `verification/data/spatial-extremes/spatial-copula-likelihood-oracle.json`
(SHA-256 `88123c36842e2699e51018eeb0257d30eef40f8701135e60445353299ab8c365`) records:

- `missing_site_model`: per row, the observed-site marginal GEV sum, the copula log density over the
  observed-site correlation submatrix (`log phi_R(z) - sum log phi(z_j)`; zero for a single observed
  site), and the value obtained by substituting a zero latent score for each missing site and
  evaluating the full-dimensional density (the behavior under review); totals `-238.892729369730`
  (marginalized) and `-238.538215560696` (zero placeholder); marginal-only total `-238.363666518069`;
- `complete_data_model`: the seven complete rows, total `-168.321519223767`;
- `location_error_model`: the complete rows with latent location errors `(0.05, -0.03, 0.02, -0.04, 0.01)`,
  error scale `0.06`, error range `20`; data log likelihood without the process density
  `-169.071665098156`, Gaussian-process log density `8.464283737935`, sum `-160.607381360221`.

| Row | Observed sites | Marginalized row log likelihood | Zero-placeholder value |
|---|---|---|---|
| 5 | 1, 3, 4, 5 | `-23.753686` | `-23.592105` |
| 6 | 2, 3, 4 | `-14.894701` | `-14.828823` |
| 7 | 1, 2, 4, 5 | `-21.970089` | `-22.078893` |
| 8 | 1, 5 | `-9.952734` | `-9.716876` |
| 9 | none | `0` | `0` |

## Confirmation runs (21 August 2026)

`SpatialGEVLikelihoodOracleTests`, one method per guarded invocation on current source:

| Exact method | Contract | Outcome |
|---|---|---|
| `MissingSites_DataLogLikelihood_UsesObservedSiteCopulaSubmatrix` | Scalar data log likelihood equals the marginalized oracle (`1e-8`) | Failed - confirms TR-048: BestFit returns `-238.53821556069616`, the zero-placeholder value, against `-238.89272936973` |
| `MissingSites_PointwiseRows_MatchObservedSubsetOracle` | Every pointwise row equals the marginalized row (`1e-10`) and rows sum to the scalar | Failed - confirms TR-048: row 5 returns `-23.5921046123661` (placeholder) against `-23.7536861453931` |
| `CompleteRows_CopulaLikelihood_MatchesIndependentOracle` | Complete-row scalar and pointwise copula likelihood equal the oracle | Passed - copula and GEV conventions match `mvtnorm` |
| `MarginalOnly_WithoutCopula_MatchesIndependentOracle` | Without dependence the likelihood is the observed-site Hosking GEV sum | Passed |
| `LocationErrorModel_PosteriorKernel_IsInvariantToTheDecomposition` | `LogLikelihood` equals observation terms + process density + parameter priors | Passed - the posterior kernel is correct before the decomposition change and must remain so after it |
| `LocationErrorModel_DataLogLikelihood_ExcludesProcessDensity` | Data log likelihood holds observation terms only | Failed - confirms TR-049: `-160.60738136022104` = `-169.071665098156 + 8.464283737935` |
| `LocationErrorModel_ScalarAndPointwiseDecompositionsAgree` | Data equals the pointwise sum; prior equals the pointwise prior component sum | Failed - confirms TR-049: pointwise data sum `-169.07166509815573` versus scalar `-160.60738136022104` |
| `LocationErrorModel_ScalarAndPointwiseGradientsAgree` | Scalar and summed-pointwise gradients agree for every parameter (`1e-4`) | Failed - confirms TR-057: error-scale score `-31.37524267` versus pointwise `0` |

## Proposed corrections (awaiting approval)

No production file changes until Haden Smith approves each item below.

**TR-048.** Give `GaussianCopula` an additive observed-subset evaluation (`LogPDF(z, observedSites)`)
that factors the correlation submatrix of the observed sites and returns
`log phi_{R_O}(z_O) - sum_{j in O} log phi(z_j)`, zero for fewer than two observed sites; cache the
factorization per missingness pattern within one likelihood evaluation (the correlation matrix depends
on the range parameter, so no cross-evaluation cache). Use it in `SpatialGEV.ComputeLogLikelihoodInternal`
and `PointwiseDataLogLikelihood` in place of the zero placeholder. Complete rows are unchanged to
`1e-12`; rows with missing sites change, so posterior results for networks with missing data change
(documented; saved results remain readable). Fast regressions: complete-row parity between the full and
subset paths, single-observed-site rows contribute marginals only, pattern grouping, and the all-missing
row contributes nothing. Acceptance: the two TR-048 cells above, then the nine existing spatial recovery
cells (complete data; MAP expected unchanged).

**TR-049.** Move the location/scale/shape Gaussian-process log densities out of `DataLogLikelihood`
(both scalar and pointwise paths already agree on the observation terms) into a `PriorLogLikelihood`
override that adds them to the parameter priors; `PointwisePriorLogLikelihood` already emits them as
`SpatialError` components, so the prior identity closes. `LogLikelihood` (the posterior kernel) is
unchanged for every model; the sampler, MAP, and posterior are unchanged; AIC, BIC, DIC, WAIC, and
LOOIC of models with latent errors change because they now exclude the process density (documented).
Delete the "non-canonical decomposition" remarks. Fast regressions: the three identities and the
kernel pin. Acceptance: the four location-error cells above.

**TR-057.** With TR-049 the numerical Hessian of `DataLogLikelihood` and the pointwise scores derive
from the same row sum, which the gradient cell verifies. Replace the singular-Hessian fallback that
returns `J` with an explicit failure: `ComputeGodambeCovariance` returns `null`, `GodambeCovariance`
is `null`, and an additive status property reports the failure (TR-027 pattern). Fast regression: a
model with a zero covariate column yields a singular Hessian and the failure status, never `J`.

**TR-055.** After TR-048 and TR-049, add a deterministic test that BIC uses the number of nonempty
row/year blocks and that WAIC/LOO consume the row/year pointwise terms; close the finding with the
remaining caveats (weighted likelihood, dependence regularity) stated in the technical reference.

## Next steps

1. Approve or amend the four items above.
2. Implement with fast regressions, run the four unit-test projects, rerun the eight cells above and
   the nine existing spatial recovery cells one at a time, and record the outcomes here, in the test
   inventory, the manifest, the register, and the technical reference.
3. Open Batch 6.4 (cross-validation) with its own test-first confirmation.

[Verification index](README.md) | [Technical treatment](../technical-reference/spatial/spatial-extremes.md) | [Scientific findings](../technical-reference/review-findings.md#tr-048)
