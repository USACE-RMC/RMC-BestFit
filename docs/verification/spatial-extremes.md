<!-- verification-status: phase-6-spatial-likelihood-core-complete -->

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
| TR-048 missing-site copula marginalization | Corrected (approved and implemented 21 August 2026) | `GaussianCopula.LogPDF(z, observedSites)` marginalizes the unobserved sites through the observed-site correlation submatrix (factorization cached per missingness pattern and parameter vector) and both likelihood paths use it; the two missing-site cells now match the `mvtnorm` oracle and the complete-row cell is unchanged |
| TR-049 data/prior decomposition | Corrected (approved and implemented 21 August 2026) | `SpatialGEV.PriorLogLikelihood` holds the parameter priors plus the Gaussian-process densities of the enabled latent errors; `DataLogLikelihood` holds the observation terms only; the four location-error cells pass (kernel invariant, data excludes the process density, scalar/pointwise identities) |
| TR-057 Godambe estimating equations | Corrected (approved and implemented 21 August 2026) | Sensitivity and variability matrices derive from the same row/year estimating equations (gradient cell passes); a singular or non-finite computation returns `null` with `GodambeCovarianceStatus = Failed` and a diagnostic instead of the variability matrix; fast contracts cover failure, success, validation, and reset |
| TR-055 spatial criteria | Closed (21 August 2026) | Guarded criteria cell after an MCMC run with production defaults: AIC/BIC from the observation log likelihood at the sampled MAP with the eleven nonempty row/year blocks, WAIC/PSIS-LOO from the row/year pointwise terms; fast contracts on `SpatialGEVAnalysis.ComputeInformationCriteria` and on injected-draw WAIC |
| GEV, copula, and kernel conventions | Verified | Complete-row copula likelihood, marginal-only likelihood, and the posterior-kernel invariance guard pass against the `mvtnorm` oracle before and after the corrections |
| Nine existing spatial recovery cells | Passed after the corrections | Complete-data MLE (2) and Bayesian (7) recovery cells pass under production defaults (the copula cell only after the TR-091 clone fix) |
| TR-091 spatial clone structure (found by these runs) | Corrected (approved and implemented 21 August 2026) | `SpatialGEV.Clone()` dropped the copula/error parameter blocks and reset the trend intercepts, so every copula or latent-error Bayesian run failed in post-processing; the clone now rebuilds its list from the cloned components and copies values, bounds, and priors; three fast contracts and the two blocked cells pass |
| TR-050 through TR-054, TR-056, TR-058 through TR-062 | Planned (Batches 6.4-6.6) | - |

Production changes (approved 21 August 2026): `Models/SpatialExtremes/CopulaModels/GaussianCopula.cs`,
`Models/SpatialExtremes/SpatialGEV.cs`, and `Analyses/SpatialExtremes/SpatialGEVAnalysis.cs`; see
[Corrections and acceptance runs](#corrections-and-acceptance-runs-21-august-2026). The complete
Verification project was not run; fast gates Core 3,299, UI 579, App 438, API 498, 0 failures.

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

## Corrections and acceptance runs (21 August 2026)

Haden Smith approved the four items as proposed; the implementation is additive to the public API
(`GaussianCopula.LogPDF(IList<double>, IReadOnlyList<int>)`, `SpatialGEV.PriorLogLikelihood` override,
`SpatialGEVAnalysis.GodambeCovarianceStatus` and `GodambeCovarianceDiagnostic`; recorded in the public
API baseline) and leaves project serialization unchanged.

**TR-048.** `GaussianCopula.LogPDF(z, observedSites)` returns `log phi_{R_O}(z_O) - sum_{j in O} log phi(z_j)`
over the observed-site correlation submatrix `R_O`, zero for fewer than two observed sites, and the
full-dimensional value for a complete row; the Cholesky factorization of each observed-site pattern is
cached on the instance until the correlation parameters change (the copula is cloned per likelihood
evaluation, so parallel chains never share a cache). `SpatialGEV.ComputeLogLikelihoodInternal` and
`PointwiseDataLogLikelihood` pass the observed-site index list instead of substituting a zero latent
score. Complete rows are bitwise unchanged; rows with missing sites change, so posterior results of
copula models fitted to networks with missing data change (release note; saved projects remain readable).

**TR-049.** `SpatialGEV.PriorLogLikelihood` overrides the base prior with the parameter priors plus the
Gaussian-process log densities of the enabled location, scale, and shape error vectors, evaluated on
local clones of the error models (thread-safe); `DataLogLikelihood` no longer adds the process densities.
The posterior kernel `LogLikelihood` is identical for every model (the kernel-invariance cell passed
before and after), so the sampler, MAP, and posterior are unchanged; AIC, BIC, DIC, WAIC, and LOOIC of
models with latent errors change because they now exclude the process densities. The "non-canonical
decomposition" remarks were deleted and the three identities (`LogLikelihood == Data + Prior`,
`Data == sum of pointwise rows`, `Prior == sum of pointwise prior components`) are stated in the XML
documentation and enforced by fast contracts.

**TR-057.** `ComputeGodambeCovariance` derives the sensitivity matrix from the observation log likelihood
and the variability matrix from the row/year pointwise scores (each parameter's pointwise perturbations
are evaluated once and shared by every row); because the scalar likelihood is the row sum, both
matrices derive from the same estimating equations (the gradient cell passes). A non-finite evaluation, a
singular sensitivity matrix, or a non-finite or non-positive-variance sandwich returns `null`, clears
`GodambeCovariance`, and reports `CovarianceComputationStatus.Failed` through the additive
`GodambeCovarianceStatus` and `GodambeCovarianceDiagnostic` properties (TR-027 pattern); the method
validates the parameter count and `ClearResults` resets the state. The variability matrix is never
returned as a substitute.

**TR-091 (found by the acceptance runs).** The first guarded runs of the criteria cell and of
`SpatialGEVBayesianRecoveryTests.Bayesian_WithCopula_RecoversRangeParameter` reported `IsEstimated == false`
with the sampler complete and the post-processing throwing `Expected 3 parameters but got 4`:
`SpatialGEV.Clone()` built its parameter list in the base constructor before the copula and error
components and flags were attached, so the clone of a copula model had 3 parameters instead of 4 (17
with latent errors) and its trend intercepts were reset to data-derived defaults. With Haden Smith's
approval the clone now rebuilds its flat list from the cloned components (`RebuildParameterList`) and
copies every source parameter's value, bounds, and prior (no numerical change); the fast contracts
`Clone_WithCopula_PreservesParameterStructure`, `Clone_WithSpatialErrors_PreservesParameterStructure`,
and `Clone_PreservesParameterValuesBoundsAndPriors` failed before and pass after, and both blocked
cells pass after the fix (18.2 s and 93.7 s). Before the fix no copula or latent-error
spatial Bayesian analysis produced site results.

**TR-055.** `SpatialGEVAnalysis.ComputeInformationCriteria` (internal, used by the result builder)
computes AIC and BIC from the observation log likelihood at the MAP with one nonempty row/year block per
BIC observation; WAIC and PSIS-LOO consume `SpatialGEV.PointwiseDataLogLikelihood`, one term per row/year.
The remaining caveats (the MAP is not an MLE under nonconstant priors; weighted and dependent likelihoods
do not automatically satisfy AIC/BIC regularity) stay in the technical reference.

Fast regressions added (`RMC.BestFit.Tests`): `GaussianCopulaTests.LogPDF_ObservedSubset_*` (complete-row
parity, fewer-than-two-sites zero, equality with the copula built on the observed sites, cache
invalidation on parameter change, argument validation);
`SpatialGEVTests.DataLogLikelihood_WithCopulaAndMissingSites_UsesObservedSiteCopulaSubmatrix`,
`..._SingleObservedSiteRow_HasNoDependenceTerm`, `..._FullyMissingRow_ContributesNothing`,
`PriorLogLikelihood_WithSpatialErrors_HoldsGaussianProcessDensities`,
`PriorLogLikelihood_WithSpatialErrors_DoesNotMutateModelState`;
`SpatialGEVAnalysisTests.ComputeInformationCriteria_UsesNonEmptyRowYearBlocks`,
`PredictiveCriteria_FromInjectedDraws_UseRowYearPointwiseTerms`,
`ComputeGodambeCovariance_SingularHessian_ReportsFailureWithoutSubstitute`,
`ComputeGodambeCovariance_WellConditioned_ReportsAvailableCovariance`,
`ComputeGodambeCovariance_WrongParameterCount_Throws`; and, for the clone defect found by these runs (TR-091),
`SpatialGEVTests.Clone_WithCopula_PreservesParameterStructure`, `Clone_WithSpatialErrors_PreservesParameterStructure`,
`Clone_PreservesParameterValuesBoundsAndPriors`. Fast core 3,299/3,299; strict Debug
XML-documentation builds of the core, Verification, and the four unit projects report zero warnings.

Guarded acceptance runs, one method per `scripts/run-verification-test.ps1` invocation on the corrected source:

| Exact method | Contract | Outcome |
|---|---|---|
| `SpatialGEVLikelihoodOracleTests.MissingSites_DataLogLikelihood_UsesObservedSiteCopulaSubmatrix` | Scalar data log likelihood equals the marginalized oracle (`1e-8`) | Passed (3.6 s) |
| `SpatialGEVLikelihoodOracleTests.MissingSites_PointwiseRows_MatchObservedSubsetOracle` | Every pointwise row equals the marginalized row (`1e-10`); rows sum to the scalar | Passed (3.5 s) |
| `SpatialGEVLikelihoodOracleTests.CompleteRows_CopulaLikelihood_MatchesIndependentOracle` | Complete-row copula likelihood equals the oracle | Passed (3.2 s) |
| `SpatialGEVLikelihoodOracleTests.MarginalOnly_WithoutCopula_MatchesIndependentOracle` | Marginal-only likelihood equals the Hosking GEV oracle | Passed (3.3 s) |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_PosteriorKernel_IsInvariantToTheDecomposition` | `LogLikelihood` equals observation terms + process density + parameter priors | Passed (3.3 s) |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_DataLogLikelihood_ExcludesProcessDensity` | Data log likelihood holds the observation terms only | Passed (3.4 s) |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_ScalarAndPointwiseDecompositionsAgree` | Data equals the pointwise sum; prior equals the pointwise prior sum; kernel is data plus prior | Passed (3.4 s) |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_ScalarAndPointwiseGradientsAgree` | Scalar and summed-pointwise gradients agree for every parameter (`1e-4`) | Passed (3.2 s) |
| `SpatialGEVInformationCriteriaTests.MissingSiteModel_InformationCriteria_UseRowYearBlocks` | MCMC with production defaults on the oracle's missing-site model; AIC/BIC from the observation log likelihood at the sampled MAP with eleven nonempty row/year blocks (`1e-8`); WAIC and `WAIC_pD` equal the row/year recomputation over the retained draws (`1e-9` relative); one Pareto k per row/year; finite LOOIC | Passed (18.2 s) |
| `SpatialGEVMLERecoveryTests.MLE_BasicHomogeneous_RecoversParameters`, `MLE_WithCopula_RecoversRangeParameter` | Complete-data MLE recovery, unchanged tolerances | Passed 2/2 (4.1-4.4 s) |
| `SpatialGEVBayesianRecoveryTests` (7 methods) | Complete-data Bayesian recovery under production defaults, unchanged tolerances | Passed 7/7 (42-115 s) |

Behavior changes for users: posteriors of copula models with missing site values change (the
observed-data likelihood replaces the zero-placeholder evaluation); AIC/BIC/DIC/WAIC/LOOIC of models with
latent spatial errors exclude the process densities; `ComputeGodambeCovariance` reports failure instead
of returning the variability matrix. Parameter estimates and posteriors of complete-data models and of
models without latent errors are unchanged.

## Next steps

1. Open Batch 6.4 (leave-one-site-out cross-validation: TR-050 through TR-053) with its own test-first
   confirmation, then Batches 6.5 and 6.6.
2. Keep the eight oracle cells, the criteria cell, and the nine recovery cells as the regression set for
   every later spatial change.

[Verification index](README.md) | [Technical treatment](../technical-reference/spatial/spatial-extremes.md) | [Scientific findings](../technical-reference/review-findings.md#tr-048)
