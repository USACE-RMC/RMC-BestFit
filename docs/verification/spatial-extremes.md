<!-- verification-status: finalized -->

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
| TR-050 through TR-053 leave-one-site-out cross-validation | Corrected (approved and implemented 22 August 2026) | Reduced training model per fold (`SpatialGEV.CreateReducedModel`), fold analyses with the main settings and seed, held-out covariate rows, explicit fold accounting; three guarded cells and the fast contracts pass; see [Batch 6.4](#batch-64-leave-one-site-out-cross-validation-22-august-2026) |
| TR-054, TR-056, TR-058, TR-061, TR-062 prediction, bootstrap, regional bounds, simulation, dispatch | Corrected (approved and implemented 22 August 2026) | Conditional Gaussian-process prediction per draw, temporal block bootstrap with MAP refits, per-draw regional posterior, Cholesky-dependent simulation, method dispatch with recorded applied method; R conditional-GP oracle, seven guarded cells, and fast contracts pass; see [Batch 6.5](#batch-65-prediction-uncertainty-simulation-and-dispatch-22-august-2026) |
| TR-092, TR-093 (found by the Batch 6.5 runs) | Corrected (approved and implemented 22 August 2026) | Non-finite site parameters return negative-infinite likelihood; latent-error default bounds follow the link space |
| TR-059, TR-060 site-weight naming and distance metric | Corrected (approved and implemented 22 August 2026) | `ComputeCorrelationHeuristicSiteWeights` with an obsolete alias and corrected remarks; additive `SpatialDistanceMetric` (Cartesian default bitwise, geodesic haversine kilometres) verified against the R haversine oracle; see [Batch 6.6](#batch-66-site-weight-naming-and-distance-metric-22-august-2026) |

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

The second artifact, `verification/data/spatial-extremes/spatial-conditional-gp-oracle.json` (SHA-256
`9a6233dd32c1473a36076183ae33c9e6e1c1584fa644138d9aad90c1cc8259fc`; generator
`generate_spatial_conditional_gp_oracle.R`, SHA-256 `ec1738d3337f0553ed78af3bc5736a66cda004716098a387591ddfd4f4986215`;
R 4.4.3, no sampling), records the conditional (simple-kriging) mean `k*'K^-1 eps` and variance
`sigma^2 - k*'K^-1 k*` of the latent-error process at five target locations for three (scale, range,
errors) parameter sets on the same five-site network, computed with dense `solve`; it is the oracle of
`SpatialRegressionErrors.GetKrigingPrediction`, the predictor the analysis-level ungauged prediction uses
since Batch 6.5 (tolerance `1e-10`).

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

## Batch 6.4 leave-one-site-out cross-validation (22 August 2026)

Confirmation on the Batch 6.3 source (21-22 August 2026, guarded runner): both new cross-validation cells
threw `InvalidOperationException: Analysis must be run before predicting at ungauged locations` from
`RunCrossValidationAsync` on a fresh analysis, because the fold refit never set the analysis's own estimated
flag; the fast mechanism contracts showed that a zero site weight leaves the held-out observations in the
copula likelihood and the held-out latent error in the Gaussian-process prior (TR-051), and that a covariate
trend evaluated with null covariates returned the intercept only (TR-052). Source audit confirmed the
restoration refit clearing the results (TR-050) and the zero-initialized error arrays (TR-053).

Haden Smith approved the three fix plans one at a time (TR-050/051 reduced training models with fold
analyses and no restoration refit; TR-052 held-out covariate rows plus `PredictWithCovariates` throwing
for a covariate trend without covariates; TR-053 fold status, messages, counts, NaN metrics, aggregates over
successful folds, and an exception only when no fold succeeds). Implementation (additive API:
`SpatialGEVCrossValidationFoldStatus`, `SpatialGEVCrossValidationResults.FoldStatus/FoldMessages/SuccessfulFolds/TotalFolds`;
internal `SpatialGEV.CreateReducedModel`; the prediction machinery shared by `PredictAtUngaugedLocation`
and the folds; no serialization change; `PredictWithCovariates` now throws for null/empty covariates on a
covariate trend):

- each fold builds the network without the held-out site (data column, coordinate row, covariate rows of
  every trend, copula coordinate, latent error) with the flags, links, remaining weights, and every
  remaining parameter's value, bounds, and prior copied, validates it, fits it with a fold
  `BayesianAnalysis` carrying the main sampler type, defaults policy (resolved against the fold's own
  dimension), seed, interval width, output length, point estimator, and explicit settings when the defaults
  are off, and predicts the held-out site from the fold posterior with the site's own covariate rows;
- the analysis model and its posterior are never modified, so the results survive the run;
- a fold without observations, with an invalid or unfittable reduced model, or with a non-finite prediction
  is recorded with its reason and NaN metrics; the aggregates average the successful folds; no successful
  fold throws.

Fast contracts added: `SpatialGEVTests.CreateReducedModel_WithCopula_RemovesTheHeldOutSite`,
`..._WithCopula_IsIndependentOfTheHeldOutSite`, `..._WithCovariateTrend_RemovesTheHeldOutRow`,
`..._WithLatentErrors_RemovesTheHeldOutLatentError`, `..._InvalidSite_Throws`;
`SpatialGEVAnalysisTests.SiteWeightZero_WithCopula_DoesNotExcludeTheHeldOutSite`,
`SiteWeightZero_WithLatentErrors_KeepsTheHeldOutLatentError`, `PredictWithCovariates_NullForCovariateTrend_Throws`,
`RunCrossValidationAsync_WhenNoFoldSucceeds_ThrowsAndReportsNothing`;
`GeneralLinearFunctionTests.Test_PredictWithCovariates_NullOrEmpty_Throws`;
`SpatialGEVResultsTests.CrossValidation_FoldAccounting_RoundTrips`. Fast gates Core 3,309, UI 579,
App 438, API 498, 0 failures; strict XML-documentation builds clean.

The independent reduced model of the parity cells is built from the data, coordinates, covariates, and
copula alone and adopts the full model's parameter values, bounds, and priors, because the fold keeps the
full model's prior specification by design; the first regression run, whose independent model derived its
own data-based intercept bounds from the three-site data, differed from the fold by 0.6% and was the reason
for stating that contract explicitly (the copula cell matched either way).

Guarded acceptance runs (one method per invocation, production defaults, 40 rows at four sites):

| Exact method | Contract | Outcome |
|---|---|---|
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_WithCopula_RetainsResultsAndMatchesReducedModel` | Results retained; fold 1 prediction error equals an independently reduced three-site copula model fitted through the production path with the same defaults and seed (`1e-6` relative) | Passed (112.0 s) |
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_WithLocationRegression_UsesHeldOutCovariates` | Results retained; fold 1 equals the reduced location-regression model evaluated at the held-out covariate row (`1e-6` relative) | Passed (113.0 s) |
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_SiteWithoutObservations_IsReportedNotScored` | A fully missing site is `NoObservations` with NaN metrics; the other three folds succeed; aggregates average the successful folds | Passed (54.3 s) |
| Spot checks after the change: `SpatialGEVLikelihoodOracleTests.MissingSites_DataLogLikelihood_UsesObservedSiteCopulaSubmatrix`, `LocationErrorModel_ScalarAndPointwiseDecompositionsAgree`, `SpatialGEVInformationCriteriaTests.MissingSiteModel_InformationCriteria_UseRowYearBlocks`, `SpatialGEVBayesianRecoveryTests.Bayesian_WithCopula_RecoversRangeParameter`, `Bayesian_WithLocationRegression_RecoversIntercept` | Batch 6.3 likelihood, criteria, and recovery contracts unchanged | Passed 5/5 (3.4 s, 3.4 s, 17.7 s, 94.5 s, 74.8 s) |

Behavior changes for users: leave-one-site-out no longer refits the full model or mutates site weights, its
results survive the run, folds are genuine reduced-network fits evaluated at the held-out covariate rows,
failed folds are visible with NaN metrics instead of zero errors, and the ungauged-prediction methods reject
a missing covariate vector for covariate trends. The per-fold prediction still interpolates latent errors by
inverse distance (TR-054, Batch 6.5).

## Batch 6.5 prediction, uncertainty, simulation, and dispatch (22 August 2026)

Confirmation on the Batch 6.4 source (guarded runner and fast contracts): the regional-bounds cell on a
five-site location-regression network found the lower bound at p = 1e-6 equal to 66,486 where the posterior
5% quantile of the per-draw regional mean is 66,526 (TR-058); the dependence contract found a normal-score
correlation of −0.02 between two sites whose copula correlation is 0.57 (TR-061); the ungauged-prediction
cell could not run because a default-configured latent-error model threw inside the sampler ("The location
parameter ξ (Xi) must be a number"), which exposed TR-092 (the likelihood threw instead of rejecting a
non-finite proposal) and TR-093 (latent-error default bounds in raw units under the log link); the
bootstrap data wiring and the undispatched uncertainty method were confirmed by source audit. The new R
oracle `spatial-conditional-gp-oracle.json` (`generate_spatial_conditional_gp_oracle.R`; manifest row
recorded before the run) verified the model-level simple-kriging predictor to `1e-10` in 15 cases before any
production change.

Haden Smith approved the fix plans one at a time: TR-092 (negative-infinite likelihood for non-finite site
parameters), TR-093 (link-space spread × 3, floor 1.0), TR-054 (kriging per draw plus a seeded conditional
residual, `SampleConditionalResidual` default true), TR-058 (per-draw regional posterior, endpoint averages
removed), TR-061 (Cholesky-dependent simulation), TR-056 (temporal block bootstrap keeping all sites, MAP
refit per replicate, NaN failures, 50% success floor, accounting DTO), and TR-062 (dispatch in `RunAsync`
with Gaussian parameter draws N(MAP, Σ) for the Godambe path, sqrt-VIF inflation, bootstrap settings, and the
applied method recorded). The implementation is additive to the public API (`GaussianCopula.GetCorrelationMatrix`,
`SpatialGEVAnalysis.SampleConditionalResidual`, `BootstrapReplicates`, `BootstrapBlockSize`,
`AppliedUncertaintyMethod`, `BootstrapResults`, `SpatialGEVBootstrapResults`,
`SpatialGEVSiteResults.UncertaintyMethod`; recorded in the baseline) with optional serialization attributes
(legacy projects read the defaults).

Fast contracts added: `SpatialGEVTests.SetDefaultParameters_LatentErrorBounds_FollowTheLinkSpace`,
`DataLogLikelihood_NonFiniteSiteParameters_IsNegativeInfinity`, `CreateResampledModel_ReplacesRowsAndKeepsTheNetwork`,
`GenerateRandomValues_WithoutCopula_MatchesTheHistoricalSiteMajorAlgorithm`,
`GenerateRandomValues_WithCopula_IsReproducibleAndKeepsTheMarginals`, `GenerateRandomValues_WithCopula_ReproducesTheFittedDependence`,
`GenerateRandomValues_WithoutCopula_SimulatesIndependentSites`; `GaussianCopulaTests.GetCorrelationMatrix_ReturnsCopyOfTheFittedMatrix`;
`SpatialGEVAnalysisTests.PredictAtUngaugedLocation_UsesConditionalGaussianProcessPerDraw`,
`RegionalCurve_FromInjectedDraws_IsPosteriorOfTheRegionalMean`, `ApplyUncertaintyMethod_RecordsTheAppliedMethod`,
`BuildBlockBootstrapRows_DrawsContiguousWrappingBlocks`, `UncertaintySettings_ValidateAndRoundTrip`;
`SpatialGEVResultsTests.SiteResultsAndBootstrapResults_DefaultsAndRoundTrip`. Fast gates Core 3,323, UI 579,
App 438, API 498, 0 failures; strict XML-documentation builds clean.

Guarded acceptance runs (one method per invocation, production defaults):

| Exact method | Contract | Outcome |
|---|---|---|
| `SpatialGEVKrigingOracleTests.KrigingPrediction_MatchesConditionalGaussianProcessOracle` | `SpatialRegressionErrors.GetKrigingPrediction` reproduces the R conditional mean and variance in 15 cases (`1e-10`); zero variance at a site | Passed (3.9 s) |
| `SpatialGEVPredictionVerificationTests.UngaugedPrediction_UsesConditionalGaussianProcessPerDraw` | Five-site copula + location-error network: the deterministic prediction equals the posterior mean of the model-level kriging prediction over the retained draws (`1e-9`); the residual option is reproducible and at least as wide | Passed (111.9 s) |
| `SpatialGEVPredictionVerificationTests.RegionalCurve_IsPosteriorOfTheRegionalMeanQuantile` | Five-site location-regression network: regional mean curve and bounds equal the posterior mean and quantiles of the per-draw regional mean quantile (`1e-9`) | Passed (41.5 s) |
| `SpatialGEVSimulationVerificationTests.GenerateRandomValues_WithCopula_ReproducesTheFittedIntersiteDependence` | Seeded 20,000 rows: every intersite normal-score correlation within ±0.02 of the fitted matrix; site quantiles within 3% of the GEV quantiles | Passed (3.2 s) |
| `SpatialGEVUncertaintyMethodVerificationTests.RunAsync_BayesianInflated_WidensThePosteriorIntervals` | Applied method recorded; site and regional intervals widened by sqrt(VIF) relative to the posterior run with the same seed | Passed (63.0 s) |
| `SpatialGEVUncertaintyMethodVerificationTests.RunAsync_GodambeSandwich_BuildsResultsFromGaussianDraws` | Copula network: the sensitivity matrix is singular at the sampled MAP, so the run fails explicitly (`GodambeCovarianceStatus = Failed`, no method recorded); homogeneous network: covariance available, applied method recorded, finite ordered bounds, MAP quantile inside the Gaussian-draw interval, mode curve at the MAP | Passed (49.0 s) |
| `SpatialGEVUncertaintyMethodVerificationTests.RunAsync_SpatialBootstrap_FitsResampledReplicatesAndReportsAccounting` | Twenty replicates: accounting, automatic block size, finite ordered bounds, seed sensitivity | Passed (74.4 s) |
| Regression set (15 cells): the eight `mvtnorm` oracle cells, the criteria cell, the three cross-validation cells, and three recovery cells | Batch 6.3 and 6.4 contracts unchanged after the prediction change (the cross-validation folds now predict with kriging plus residual, and the independent reduced models follow the same production path) | Passed 15/15 (3-118 s) |

Behavior changes for users: ungauged-site predictions use the conditional Gaussian process with a seeded
residual (set `SampleConditionalResidual = false` for the conditional mean); regional credible bounds are
posterior quantiles of the regional mean quantile; copula simulations are spatially dependent; the bootstrap
fits resampled data and reports its accounting; the selected uncertainty method is applied and recorded;
default latent-error bounds under the log link are a few log units instead of the raw spread.

## Batch 6.6 site-weight naming and distance metric (22 August 2026)

Confirmation by source audit: `ComputeEffectiveSampleSizeWeights` rescales `w*_j = 1/(1+(S-1)ρ̄_j)` to sum
S and the likelihood applies the weights to the marginal terms only (TR-059); `GaussianCopula` and
`SpatialRegressionErrors` built every separation with planar `Tools.Distance` while advertising (Lat, Lon)
input (TR-060). The R haversine oracle `geodesic-distance-oracle.json` (generator
`generate_geodesic_distance_oracle.R`; manifest row recorded before the run) defines the great-circle
distances of a five-site latitude/longitude network, the exponential correlation at 150 km, and the
simple-kriging moments at five targets.

Haden Smith approved the name `ComputeCorrelationHeuristicSiteWeights` (obsolete forwarding alias kept)
and the `SpatialDistanceMetric` option with the range prior Uniform(ε, 500) unchanged in both metrics
(projected units for Cartesian, kilometres for geodesic). Implementation (additive API: the enum,
`SpatialGEV.DistanceMetric`, the third-argument copula constructor, the fourth-argument error-model
constructor, `DistanceMetric` properties on both components, the renamed method; optional serialization
attribute; legacy projects read Cartesian): the Cartesian default is bitwise the former behavior; the
geodesic metric validates latitude/longitude ranges and uses haversine kilometres for the copula and
latent-error separations, kriging, and the inverse-distance fallback; `ConfigureForProperCoverage`,
`Clone`, and the reduced/resampled factories carry the metric and `Validate` rejects mismatched
components; the remarks of the weights, of `ConfigureForProperCoverage`, and the technical reference state
the heuristic nature of the weights.

Fast contracts added: `SpatialGEVTests.ComputeCorrelationHeuristicSiteWeights_PinsTheFormulaAndTheObsoleteAlias`
(plus the renamed update/custom-matrix/mismatch contracts), `DistanceMetric_DefaultsToCartesianAndPropagatesToComponents`,
`DistanceMetric_RoundTripsThroughSerializationAndFactories`; `GaussianCopulaTests.GeodesicMetric_BuildsCorrelationFromGreatCircleKilometres`;
`SpatialRegressionErrorsTests.GeodesicMetric_UsesGreatCircleKilometresForCovarianceAndKriging`. Fast gates
Core 3,328, UI 579, App 438, API 498, 0 failures; strict XML-documentation builds clean.

Guarded acceptance runs (one method per invocation):

| Exact method | Contract | Outcome |
|---|---|---|
| `SpatialGEVDistanceOracleTests.GeodesicMetric_MatchesHaversineOracle` | Geodesic distance matrix (`1e-9` km), exponential correlation at 150 km and simple-kriging moments at five targets (`1e-10`) equal the R haversine oracle | Passed (3.9 s) |
| `SpatialGEVDistanceOracleTests.CartesianMetric_IsPlanarEuclidean` | The default metric reproduces the planar Euclidean distances exactly | Passed (3.5 s) |
| Regression set (9 cells: kriging, likelihood, simulation, prediction, cross-validation, recovery) | Batches 6.3-6.5 contracts unchanged | Passed 9/9 |

Behavior changes for users: none for existing projects (Cartesian default); latitude/longitude networks
can select the geodesic metric; `ComputeEffectiveSampleSizeWeights` is obsolete in favor of
`ComputeCorrelationHeuristicSiteWeights`.

## Next steps

Phase 6 is complete for the spatial family. Phase 7 (closeout) disposes TR-084 through TR-090 and
reconciles the plan, README, and chapter status markers; the regression set for later spatial changes is the
eight `mvtnorm` oracle cells, the kriging and geodesic oracle cells, the criteria cell, the three
cross-validation cells, the prediction/regional/simulation cells, the three dispatch cells, and the nine
recovery cells.

[Verification index](README.md) | [Technical treatment](../technical-reference/spatial/spatial-extremes.md) | [Scientific findings](../technical-reference/review-findings.md#tr-048)
