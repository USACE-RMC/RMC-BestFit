<!-- verification-plan-status: phase-5-in-progress -->

# RMC.BestFit Verification Finalization Plan

This document is the durable handoff plan for finishing the RMC.BestFit 2.0 verification program. It is intentionally a work-control document: it records scope, sequencing, gates, off-ramps, and the current checkpoint so a future session can continue without relying on chat history.

The scientific source of truth remains the verification report chapters and the review-finding register. This plan tells the next worker where to look and how to proceed.

## Start Here

A new session should read these files in this order:

1. `docs/verification/verification-finalization-plan.md`
2. `docs/technical-reference/review-findings.md`
3. `docs/verification/README.md`
4. `docs/verification/model-estimation.md`
5. `docs/verification/test-inventory.md`
6. `verification/data/MANIFEST.md`
7. The relevant technical-reference chapter for the current finding

Implementation checkpoint (3 August 2026): BestFit scientific behavior is closed through `9d252f6` plus the current TR-014 and recovery-supplement working-tree changes, including the authorized competing-risk MAP initializer. The recovery supplement pins Numerics `c361f2864428a98a33d6072ffa9bc11ac360839d` and RMC-TotalRisk `d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7`; Numerics clone correction `e57af20` preserves the configured logarithm base. The canonical Phase 4 correction anchors remain `3e69a93` for mixtures and `cafe6cf` for competing-risk simulation. Phase 2 diagnostic anchors remain `76f7dd0`, `5c693a8`, and `b3f14b0`.

## Summary

The verification program is building a traceable numerical validation record for RMC.BestFit. Phase 0 infrastructure is operational, and the method-level test-ownership audit completed on 4 August 2026. Phases 1, 2, 3, and the original Phase 4 findings are closed for their approved scopes. The 30-method competing-risk/composite recovery supplement now records 24 passes and six explicitly deferred Bayesian findings after the unchanged extreme-tail Composite inversion method passed its 20 August 2026 focused rerun. The approved disposition makes those six supplemental research findings nonblocking and opens Phase 5 without changing a sampler, seed, prior, formula, or tolerance. TR-014 implements the approved product-posterior resampling policy in Composite and CFA while leaving `BivariateAnalysis` unchanged.

Reconciled checkpoint (3 August 2026): Core 3,134/3,134, UI 571/571, App 428/428, and Numerics 2,072/2,072 on each of net481/net8/net9/net10 pass with zero failures. Strict XML documentation and Verification compilation gates pass. Both exact TR-014 methods pass separately through the guarded runner. All 16 Phase 1/2 oracle hashes match `verification/data/MANIFEST.md`.

Ownership-cleanup checkpoint (4 August 2026): all 84 Verification C# files were reviewed, 435 of 1,196 methods remain, and 35 missing deterministic contracts were added to the fast core project. Core 3,175/3,175, UI 571/571, App 428/428, and API 496/496 pass; the strict Debug solution build reports zero warnings/errors. No Verification method was executed for this repository-hygiene change.

The report is a living Markdown book under `docs/verification/`. Every scientific claim must link to a test, oracle artifact, package/version, tolerance, result, review finding, and relevant technical-reference chapter. Markdown is the source of truth during development. PDF rendering is deferred until explicit release or visual-QA checkpoints.

The mandatory workflow for each finding is:

1. Construct an independent or analytical test.
2. Classify the finding as confirmed, rejected, accepted limitation, inconclusive, or waived with user-approved rationale.
3. If a defect is confirmed, stop and present a focused fix plan.
4. Implement an API-compatible fix only after approval.
5. Add fast regression coverage when production code changes.
6. Build and run the three unit-test projects after production changes.
7. Run only the focused verification command required for the claim.
8. Record the result in `review-findings.md`, the technical reference, and the verification report before continuing.

## Non-Negotiable Gates

- Preserve existing public method, constructor, property, event, interface, enum-value, and serialization signatures wherever possible.
- Preserve the public API baseline. A compatibility test detects removed or changed signatures and enum values; additive APIs require explicit review.
- If a breaking API change is scientifically unavoidable, stop and obtain approval for a compatibility and migration plan.
- Preserve unrelated modified and untracked user files.
- Never execute the complete Verification project or a broad verification filter.
- Focused verification must use `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`.
- The verification runner must require one exact fully qualified method, place the MSTest filter after `--`, refuse class-only/wildcard/empty/multiple-match requests, require exactly one TRX result, and fail on zero matched tests.
- External R/Python tools generate committed oracle artifacts; C# verification tests never download packages and never require R or Python at runtime.
- A finding cannot be marked fixed until code, unit regression, focused numerical verification, finding text, report chapter, and technical-reference chapter agree.
- Do not compile or visually QA PDFs during ordinary development turns. Update Markdown only unless PDF QA is explicitly requested.
- Do not change production code in `RMC.BestFit`, `RMC.BestFit.UI`, `RMC.BestFit.App`, or `C:\GIT\Numerics` until the proposed fix has been explained and approved.
- For `C:\GIT\Numerics`, use only .NET 10 during this verification program.

## Verification Classification

Move tests into the correct project based on their role.

Unit/regression tests belong in the fast test projects when they cover constructors, property round-trips, validation, serialization, events, state transitions, exception paths, simple deterministic calculations, labels, or estimator smoke tests without an independent numerical oracle.

Verification tests belong in `RMC.BestFit.Verification` when they use an analytical solution, independently implemented algorithm, external package, published table, coverage study, scientifically meaningful numerical recovery test, or fitted diagnostic comparison.

Shared `TestData.cs` and `Datasets/` remain owned by `RMC.BestFit.Verification`. Unit test projects must use small inline fixtures and must not depend on verification datasets.

## Current Status

### Completed

- Phase 0 repository and documentation foundation is established.
- `RMC.BestFit.Verification` is included in `RMC.BestFit.sln`, included in Debug, and excluded from Release builds.
- Verification source is not git ignored.
- The guarded focused verification runner exists.
- External validation folder structure and manifests exist.
- Public API baseline tests exist.
- The review-finding register contains summary rows and detailed sections for TR-001 through TR-065.
- Phase 1 distribution fitting is complete for the currently scoped claims.
- Phase 2 model estimation and diagnostics are closed for the approved scope.
- Phase 3 data handling and Bulletin 17C are closed for the approved scope.
- Phase 4 closes TR-004 through TR-008 and TR-012 through TR-015.

### Current Phase Checkpoint

Phases 1 through 3 and Phase 4 are formally closed for their approved scopes. Phase 4 includes point-process TR-004/TR-005, mixture TR-006/TR-007/TR-008, competing-risk simulation TR-012, composite criterion handling TR-013, independent Composite/CFA posterior resampling TR-014, and correlation-matrix configuration TR-015. The recovery supplement has run all 20 competing-risk and ten composite methods individually through the guarded runner. All ten Composite methods pass, and the six remaining Bayesian competing-risk findings have an approved deferred-research disposition. Phase 5 is open and in progress.

Completed Phase 2 findings:

- TR-011: confirmed defect, fixed, passed focused regression and source audit.
- TR-022: confirmed documentation/API inventory defect, fixed and passed source/API inventory.
- TR-023: MLE and MAP nuisance profiling fixed and passed R `bbmle`, closed-form, and informative-prior posterior-profile parity.
- TR-027: covariance failure signaling fixed and passed deterministic failure, available, and regularized paths.
- TR-028: confirmed joint-prior sampling limitation, documented and passed source/contract audit.
- TR-029: rank-normalized split/folded R-hat and conservative bulk/tail ESS implemented in existing fields and passed R `posterior` 1.7.0 parity without public API or serialization changes.
- TR-025: complete realized-state ARWMH covariance fixed and passed focused Numerics/BestFit regression.
- TR-030: generic and Hamiltonian acceptance contracts, gradient routes, stale-JSON compatibility, and acceptance-only report integration fixed and passed focused regression.
- TR-026: selected-weight Hansen J fixed and passed R `gmm::specTest` parameter, objective, J, and p-value parity.
- TR-031: confirmed defect, fixed, passed analytical and R-parity evidence.
- TR-032: legacy PSIS-shaped GMM overloads marked obsolete; supported Cook and leverage APIs retain correct labels and compatibility regressions pass.
- TR-033: rejected non-defect, passed objective/gradient/covariance scaling evidence.
- TR-034: overidentified fixed-weight one-step fitting and covariance fixed; parameter, objective, fixed-weight sandwich, and efficient two-step covariance pass R plus analytical parity.
- TR-065: confirmed defect, fixed in the GMM diagnostic Hessian path only, passed R `gmm` parity.

Accepted Phase 2 limitation:

- TR-028 remains an accepted documented joint-prior-sampling limitation rather than an active fix.

DIC, WAIC, PSIS-LOO/Pareto-k, MLE/MAP profiling, explicit covariance failure status, selected-weight Hansen J, overidentified GMM fitting/covariance, GMM Cook labeling, ARWMH realized-state covariance, NUTS acceptance/gradient routing and live-sampler diagnostics, and modern R-hat/ESS are corrected and verified. Joint-prior sampling remains the documented Phase 2 limitation.

The same approved criteria correction also removed prior-density terms from MAP AIC/BIC call sites tracked by TR-042, TR-047, and TR-055. TR-042 and TR-047 are closed, and the prior-density part of TR-055 is closed; separate time-series, rating-curve, bivariate, and spatial findings remain assigned to later phases.

## Phase 0 - Repository and Documentation Foundation

Status: operational; keep these gates alive after completion of the test-ownership migration audit.

Required state:

1. `src/RMC.BestFit.Verification/RMC.BestFit.Verification.csproj` is restored to `RMC.BestFit.sln` under the `Verification` solution folder.
2. Debug builds include Verification; Release solution builds omit the Verification `Build.0` entry.
3. The assembly-wide `TestCategory("Verification")` is retained.
4. `src/RMC.BestFit.Verification/` is not blanket git ignored.
5. Legitimate verification source, datasets, scripts, and oracle artifacts are tracked. Generated `.vs`, `bin`, `obj`, `TestResults`, and similar files remain ignored.
6. Unrelated scratch files, especially `src/RMC.BestFit.Verification/extract_tests.py`, are left untouched unless the user explicitly decides otherwise.
7. Verification methods are inventoried and classified as unit/regression or verification.
8. Fast unit/regression tests are migrated out of Verification when they do not use scientific oracles.
9. `docs/verification/` contains the formal Markdown report structure.
10. `verification/r/`, `verification/python/`, and `verification/data/` contain external validation assets and oracle manifests.
11. `docs/technical-reference/review-findings.md` tracks all findings with severity, disposition, implementation status, verification status, evidence link, and last-reviewed date.

Initial migration targets included DTO/serialization portions of influence diagnostics, pointwise log likelihood, predictive checks, and nonnumerical fitting/GMM/MAP/MLE/Bayesian-analysis tests.

Off-ramp: stop if Verification cannot remain excluded from Release/normal gates, legitimate project files remain ignored, test ownership is ambiguous, or the public API baseline cannot reproduce the current public surface.

## Phase 1 - Distribution Fitting

Status: complete for the scoped Phase 1 claims.

Numerical verification requirements:

- Verify all 15 supported distribution families against independent implementations using fixed datasets, parameterization crosswalks, and maximum log-likelihood comparisons.
- Use analytical base-10 Log-Normal MLE as a primary closed-form case: with `x_i = log10(y_i)`, `mu_hat = mean(x)`, and `sigma_hat^2 = n^-1 sum((x_i - mean(x))^2)`.
- Verify parameters, data log likelihood, CDF, quantiles, and transformation Jacobian.
- Use R `lmomco::mle2par` for hydrologic families and SciPy for overlapping families, including documented Kappa Four parameterization.
- Preserve parameter conversions in oracle artifacts and documentation.
- Reproduce meaningful v1 datasets, replacing generic fit-succeeded assertions with parameter, likelihood, quantile, goodness-of-fit, and selected-model assertions.
- Verify FittingAnalysis ranking, AIC/BIC, RMSE, inverse-RMSE weights, partial failure, all-candidate failure, cancellation, and deterministic result ordering.

Resolved findings:

- TR-001: Kappa Four zero-shape PDF/CDF/quantile defect confirmed and fixed upstream in RMC.Numerics. Passed analytical verification.
- TR-002: finite Kappa shape validation rejected as non-defect. Passed finite-shape/support regression.
- TR-009: RMSE residual omission confirmed and fixed. Passed hand calculation and permutation tests.
- TR-010: all-candidate failure success state confirmed and fixed. Passed regression.
- TR-063: whole-series replacement plotting positions confirmed and fixed. Passed analytical and serialization regression.
- TR-064: DE/BFGS parameter tolerance concern rejected as non-defect. Passed SciPy parity using documented `1e-4` scaled parameter tolerance while retaining tight likelihood and criterion tolerances.

Closeout evidence reconciled 28 July 2026:

- Production and regression anchors include Numerics Kappa Four and RMSE corrections, BestFit candidate-success and DataFrame refresh corrections, `KappaFourZeroShapeVerificationTests`, `GoodnessOfFitRmseVerificationTests`, `FittingAnalysisRegressionTests`, `FittingAnalysisCriteriaVerificationTests`, and the fast `DataFrameTests` coverage.
- The nine Phase 1 artifacts in `verification/data/distribution-fitting/` retain exact manifest hash matches and cover Kappa limits/finite shapes, RMSE, fit success, Log10-Normal, SciPy, `lmomco`, DataFrame plotting positions, and optimizer tolerance.
- Primary commit anchors are Numerics `3e058eb`, `bc11849`, and `24bf9f9`, plus BestFit `5d975b7`, `e585bc2`, and `ebb640a`.

Phase exit criteria:

- All distribution families have named oracles and parameterization notes.
- TR-001, TR-002, TR-009, TR-010, TR-063, and TR-064 have final dispositions.
- Focused verification results and the distribution-fitting report chapter are complete.

## Phase 2 - Model Estimation and Diagnostics

Status: closed for the approved Phase 2 scope.

### Log10-Normal Equivalence Experiment

Use deterministic symmetric data in log10 space plus an injected high-outlier variant.

Required checks:

1. Establish unpenalized MLE, MAP-with-flat-prior, and B17C GMM baselines.
2. Match a Gaussian prior on `mu` to the B17C quadratic penalty using `MSE = tau_mu^2`; the penalty performs its own division by $n$. Preserve the objective, gradient, Hessian, and covariance scaling verified under TR-033.
3. Exercise three prior regimes relative to the likelihood standard error:
   - wide and centered: `tau_mu = 100 SE_L`, no material location or variance effect;
   - narrow and centered: `tau_mu = 0.1 SE_L`, no location shift but large variance contraction;
   - narrow and shifted: `tau_mu = 0.1 SE_L`, center displaced by `2 SE_L`, producing location and variance influence.
4. Compare UnivariateDistribution MAP and B17CDistribution penalized GMM for `mu`, `sigma`, objective minimum, covariance, standard errors, design quantiles, and prior/penalty deletion displacement.
5. Validate observation fit influence using estimator-appropriate Cook-distance definitions.
6. Validate variance influence with analytical/full-Hessian Log10-Normal checks and deletion covariance.
7. Do not enforce a sum-to-p leverage rule unless derived and demonstrated.
8. Reserve Pareto-k exclusively for PSIS.
9. Keep MAP and GMM Cook magnitudes estimator-specific. They can each rank fit influence, but their absolute magnitudes are not cross-estimator equivalent because MAP is likelihood-based and GMM is least-squares on moments.
10. Use generalized variance for prior/penalty variance influence because it captures the joint covariance-volume effect and matched the planned prior-regime tests better than trace.

Completed for this experiment:

- MAP/GMM fit influence, variance influence, and combined leverage plot semantics are verified for the scoped Log10-Normal fixtures.
- The GMM diagnostic Hessian scale defect was fixed only in the diagnostic path.
- Quadratic penalties remain Gaussian priors; no objective, gradient, penalty Hessian, covariance, or estimator behavior changes were made for TR-033.

### External Model-Comparison Validation

Use deterministic posterior-draw and pointwise-log-likelihood fixtures so package comparisons do not depend on MCMC randomness.

Completed 25 July 2026:

- DIC agrees with R `BayesianTools` for `D(theta_bar)`, `D_bar`, `p_D`, and the documented DIC convention.
- WAIC agrees with R `loo::waic` for the complete pointwise log-likelihood matrix, lppd, `p_waic`, expected log predictive density, and WAIC. The artifact also records the R package standard error for traceability; BestFit does not currently expose a WAIC standard-error field.
- The versioned [oracle artifact](../../verification/data/model-estimation/model-comparison-oracle.json) is consumed by two exact focused C# methods with a `1e-10` absolute tolerance.
- R `loo::psis` and `loo::loo` 2.10.0 independently define PSIS weights, effective sample size, pointwise and aggregate LOO quantities, standard errors, Pareto $k$, tail length, and sample-size diagnostic thresholds over the 40-draw matrix and six 256-ratio tail regimes.
- The versioned [PSIS-LOO oracle](../../verification/data/model-estimation/psis-loo-oracle.json) passes all R identities and six exact focused C# methods establish corrected BestFit aggregate, pointwise, tail, classification, and single-pass performance parity.

Current scoped external parity is complete. ArviZ would be redundant secondary WAIC/LOO evidence, not a Phase 2 exit requirement. The overidentified GMM artifact now covers fixed-weight and efficient sandwich covariance with analytical self-checks.

### Phase 2 Findings

- TR-011: Bayesian AIC/BIC use the data likelihood evaluated at MAP, never the posterior kernel. They are comparable with MLE criteria only when every active prior is flat; posterior criteria remain the appropriate choice with informative priors.
- TR-022: fixed. Capability documentation now lists DEMCz, DEMCzs, ARWMH, and NUTS; plain HMC remains a Numerics-only capability.
- TR-023: fixed. MLE reoptimizes nuisance parameters against the data likelihood; MAP performs the same bounded profiling against the full posterior kernel. Flat-prior MAP matches the R/analytical MLE profile up to an additive constant, and an informative-prior fixture verifies full posterior nuisance optimization. MAP cutoff intervals are not Bayesian credible intervals.
- TR-024: fixed. The PSIS tail matches pinned R `loo` 2.10.0 and `posterior` 1.7.0 for `r_eff = 1`; WAIC/PSIS share one pointwise evaluation pass and influence reuses cached $O(n)$ summaries.
- TR-025: fixed. The original continual Adaptive Metropolis schedule is preserved, and covariance receives every realized state from accepted, rejected, and infeasible transitions.
- TR-026: fixed. Unpenalized overidentified two-step/iterative fits use selected-weighting Hansen `J = n g^T W g` with chi-squared degrees of freedom; generic fixed-weight one-step and penalized fits leave Hansen fields unset.
- TR-027: fixed. MLE, MAP, and GMM now expose public covariance status and diagnostics plus non-throwing `Try` paths; existing getters throw when covariance is unavailable, and covariance-dependent MLE/MAP influence paths no longer substitute zeros. Deterministic fast tests cover failure, available, regularized, and enum-stability contracts.
- TR-028: a 20,000-draw coupled-prior fixture confirms the documented independent-marginal behavior. Any general solution should add optional joint-prior-sampling capability without changing `IModel`; coupled-prior models must implement it or report prior-predictive sampling as unavailable.
- TR-029: fixed. Existing R-hat/ESS internals now use rank-normalized split/folded R-hat and the minimum of rank-normalized bulk and pooled 0.05/0.95 tail ESS. Existing public methods, fields, serialization, concise report labels, and the 51-lag plotting ACF are preserved. The readiness threshold is 1.01, FFT/Geyer processing adds no target evaluations, and deterministic fixtures pass R `posterior` 1.7.0 parity.
- TR-030: fixed surgically. `MCMCSampler.AcceptanceRates` retains accepted-transition/sample-count semantics, while `NUTS.HamiltonianAcceptanceRates` exposes the post-warmup Hamiltonian statistic. `MCMCResults` has no NUTS-specific properties; its existing acceptance field receives Hamiltonian acceptance for NUTS so BestFit can persist and report only that value. Other constant-memory diagnostics remain on the live sampler. Analytic-gradient initialization routing, stale-JSON compatibility, baseline result nullability, and BestFit's complete-posterior finite-difference route are focused-tested.
- TR-031: fixed. Unsupported hat-matrix claims were removed; combined leverage is an additive fit-plus-variance ranking index.
- TR-032: fixed compatibly. The two legacy PSIS-shaped GMM overloads remain callable but emit non-error obsolete warnings directing users to correctly labeled leverage or raw Cook APIs.
- TR-033: rejected non-defect. The optimized GMM/penalty stack must preserve the Gaussian-prior inverse-variance weighting behavior. Do not change the gradient or penalty Hessian without tracing the full stack and obtaining approval.
- TR-034: fixed. Overidentified one-step GMM uses the initial identity or caller-supplied fixed weighting matrix for fit, bread, and meat. Two-step/iterative covariance refreshes the efficient weight at the final parameters. Both covariance strategies match the self-checking R `gmm` oracle.
- TR-065: fixed. GMM influence Hessian scale no longer depends on penalty presence.

Closeout evidence reconciled 28 July 2026:

- Production and regression anchors include `InformationCriterionOracleTests`, `PsisLooOracleVerificationTests`, `ProfileLikelihoodFindingTests`, `GmmSpecificationFindingTests`, `GmmInfluenceDiagnosticsVerificationTests`, `McmcNumericalVerificationTests`, and `CovarianceFailureStatusTests`.
- The seven Phase 2 artifacts in `verification/data/model-estimation/` retain exact manifest hash matches and cover estimator equivalence, model comparison, PSIS-LOO, MCMC diagnostics, GMM influence, profile likelihood, and GMM specification/covariance.
- Primary BestFit commit anchors are `c1e343a`, `4f91691`, `6ffab67`, and `1a848ef`; Numerics anchors are `5c693a8`, `76f7dd0`, and `b3f14b0`.

Phase exit criteria:

- Log10-Normal prior/penalty behavior is demonstrated.
- MAP/GMM equivalence is resolved for the intended Gaussian-prior/quadratic-penalty questions.
- DIC, WAIC, and PSIS-LOO have reproducible external artifacts and corrected BestFit aggregate, pointwise, Pareto-k, and performance parity.
- Every TR-011 and TR-022 through TR-034 finding has a disposition and linked evidence.
- Rank-normalized R-hat and conservative bulk/tail ESS have a reproducible R `posterior` artifact, focused Numerics/BestFit parity, and report-threshold regressions.

## Phase 3 - Data Handling and Bulletin 17C

Status: complete for the approved scope. TR-003 documents the accepted grouped-threshold disaggregation and final-time prior-reference assumptions. TR-016 documents the intentional shared Bayesian/Bulletin 17C result-storage architecture without an API change. TR-020 restricts Cohn diagnostics to exact LP3 data with fast guard regressions; numerical Cohn verification is deferred. TR-021 records all seven formal GMM worked-example methods passing at absolute parameter tolerance `1E-3`. TR-017 documentation reconciliation and TR-018/TR-019 bootstrap-refit reliability remain closed; the final unguarded Examples 1-7 sweep produced 13,000 outputs from 13,000 realizations without retries or failures.

The repository contains no separate Phase 3/B17C JSON or CSV oracle under `verification/data/`; the published targets are encoded with the formal fixtures in `Bulletin17CData.cs`. `B17CExampleTests.Test_Example1` through `Test_Example7` were executed one method at a time and all seven passed current LP3 GMM mean, standard deviation, and skewness parity at absolute tolerance `1E-3`. TR-003 required documentation rather than a new oracle: source establishes the deterministic terminal-above threshold allocation and evaluation of distribution-dependent priors at the most-recent observed index.

TR-017 is closed without a production change after concordance review against Smith and Stedinger's pending methods paper and reference engine (paper checkpoint `aaec860e875e`). `BiasCorrectedBootstrap` implements the paper's bias-corrected pivotal bootstrap: the correction is intrinsic to the replicate-covariance standardization and parent-covariance re-inflation. The enum/XML value and practitioner-facing GUI label remain unchanged; technical documentation uses the full method name and distinguishes it from scalar BC/BCa intervals.

Implementation ledger:

1. **TR-020 - Cohn diagnostic scope (complete).** Guard both Cohn paths to Log-Pearson Type III with exact data only. Fast unit tests cover every rejected parent and data condition. Cohn value/parity verification is deferred.
2. **TR-016 - shared result storage (complete).** Document that Bulletin 17C deliberately reuses the Bayesian analysis result architecture for persistence and reprocessing. Legacy member names keep context-specific frequentist meanings; no code/API/serialization redesign is required.
3. **TR-021 - formal example verification (complete).** `B17CExampleTests.Test_Example1` through `Test_Example7` are the current GMM worked-example suite. All seven exact methods passed the published mean, standard deviation, and skewness comparisons at absolute tolerance `1E-3`.
4. **TR-003 - nonstationary assumptions (complete).** Document that grouped threshold counts are disaggregated with below-threshold status in the earlier portion and above-threshold status in the terminal portion, after preserving explicit indexes. Document that distribution-dependent priors use the last, most-recent observed time step, consistent with the published quantile-prior workflow. No code change or permutation-invariance claim is required.


Findings and required direction:

- TR-003: closed as accepted documentation. Nonstationary results are conditional on the deterministic grouped-threshold allocation and the final-time reference for distribution-dependent priors. Known event dates should be represented explicitly, and materially ambiguous allocations require sensitivity analysis. A chronology-marginalized likelihood is a future enhancement, not Phase 3 work.
- TR-016: closed as an accepted architecture. Bulletin 17C reuses `BayesianAnalysis` and `MCMCResults` storage for persistence, reprocessing, and UI integration; documentation defines the GMM/frequentist meaning of legacy member names. No code/API change is required.
- TR-017: closed as a rejected naming defect. Retain `BiasCorrectedBootstrap`; document its full technical name, bias-correction mechanism, second-order scope conditions, and distinction from BC/BCa.
- TR-018: closed. Ranked midpoint/ROS/default/parent starts, convergence-aware GMM acceptance, and pivotal bounds repair produced 13,000 finite outputs from 13,000 realizations with zero retries, parent substitutions, failed candidates, or final first-chance exceptions. Retain substitution only to guarantee configured output length and continue counting every use.
- TR-019: closed. The Mahalanobis guard caused every remaining outer retry in the guarded Examples 1-7 sweep. It was removed, and the unguarded sweep passed all 14 cells with zero retries or malformed fits. Retain the direct numerical safeguards and no-guard reliability regression.
- TR-020: closed in the approved unit scope. Cohn-style intervals and asymptotic quantile variance are available only for exact-data LP3; unsupported parents and censoring/uncertainty are rejected before base-10 LP3 calculations. Numerical Cohn verification is deferred.
- TR-021: closed with executed formal-example evidence. The seven exact `B17CExampleTests` methods passed LP3 GMM mean, standard deviation, and skewness against published values at `1E-3`. No claim is made for broader diagnostics, interval values, covariance, or coverage.

Phase exit criteria:

- Grouped-threshold chronology, nonstationary prior reference time, B17C result-storage terminology, bootstrap identity, failed-refit handling, truncation policy, exact-LP3 Cohn scope, and formal worked-example traceability are coherent across code, unit tests, technical reference, and verification inventory. Numerical Cohn interval verification remains separately approval-gated.

## Phase 4 - Point Processes and Composite Models

Status: complete for the approved scope. The point-process subset (TR-004/TR-005), mixture subset (TR-006/TR-007/TR-008), competing-risk simulation (TR-012), criterion handling (TR-013), independent posterior resampling (TR-014), and correlation-matrix configuration (TR-015) remain closed with direct evidence. The Composite supplement now passes all ten methods. The six remaining Bayesian competing-risk findings have an approved deferred-research disposition and do not block Phase 5.

Findings and required direction:

- TR-004: complete. Empirical count/rate, fitted threshold intensity, exposure metadata, and manual year/index fallback are implemented. Seasonal exact records require dates; annual/block-indexed non-exact records use the annual maximum of the two exposure-adjusted seasonal processes. Both guarded independent mixed-likelihood calculations pass.
- TR-005: complete in the approved scope. All recovery fixtures use 1,000 observations and untouched `BayesianAnalysis` defaults. Calendar-year uniform, October-water-year block-origin parity, nonseasonal production, and seasonal production recovery pass. The initial water-year failure changed block-day parameters from `170/350` to `80/260`; the corrected parity cell keeps the parameters fixed and changes only the block origin. No sampler default, seed, prior, production formula, or tolerance changed.
- TR-006: BestFit uses direct physical $K-1$ weights, derives the final weight, applies the normalized flat-simplex prior, and never mutates proposals. Public weight-related signatures are unchanged. No legacy posterior migration or parameterization version is provided; affected saved mixture results require re-estimation. Three exact parity methods remain passed; the three Bayesian `MixtureAnalysis` methods have prior guarded results that predate the initializer change.
- Mixture Bayesian initialization now retains public EM as an approximate-MLE/Numerics-parity contract, uses its solution to start bounded Nelder-Mead refinement of the full posterior, and builds the seeded population from the MAP covariance with an EM-population fallback. The informative-prior initializer method and the three Bayesian recovery methods are `Ready - focused run`; their earlier Bayesian runtimes predate this initializer and are not current passing evidence.
- TR-007: Numerics and BestFit implement one exact-zero positive-hurdle mixed measure with every continuous contribution conditioned on $X>0$; BestFit derives the fixed atom from exact annual records only. Both zero-inflated parity and Bayesian recovery passed, including the production-generator atom check.
- TR-008: Numerics and BestFit EM fail explicitly with row context when any required total row probability is zero or nonfinite. Fast impossible-row regressions and all six guarded recovery methods pass without changing tolerances.
- TR-012: complete. Numerics commit `cafe6cf3837988341912a5aa8bfda444ea55ff77` routes the existing simulation entry point through dependency-aware sampling while preserving the independent seeded sequence. Fast contracts and four separately guarded analytical rank/CDF methods cover Independent, PerfectlyPositive, PerfectlyNegative, and CorrelationMatrix modes.
- TR-013: complete. Invalid or unavailable criteria receive exactly zero weight with a named warning when another usable child remains; no-usable-criterion averages fail explicitly. Exact-zero RMSE children split unit weight without division by zero. Bulletin 17C remains eligible for Equal/AIC/BIC/RMSE and is zero-weighted, not type-rejected, when DIC/WAIC/LOOIC is unavailable.
- TR-014: complete. Composite and CFA use one independently randomized, full-range, without-replacement index row per actual retained source and the shortest actual count. A fixed owning seed and source order are reproducible; reversed-chain and swapped-child variants pass the same product-posterior targets without requiring bitwise equality. CFA caches the semantic copula/X/Y map for aggregate/accessor identity. Child posteriors and point estimates are immutable, UI seed lifecycle is preserved, index arrays are not serialized, and `BivariateAnalysis` remains unchanged.
- TR-015: complete. Core and UI expose the authorized defensively owned `CorrelationMatrix` property, validate its structure and child dimension, persist it with invariant optional fields, and propagate it to point-estimate and uncertainty-result competing-risk distributions. Existing public method signatures and result contracts are preserved.

Recovery supplement implementation:

- `CompetingRiskRecoveryTests` adds ten MLE and ten Bayesian methods reproducing the eight pinned Numerics min/max fixtures plus two latent-correlation-0.6 cases. Generation uses seed 12345 and 1,000/1,500 observations. Every case requires true-parameter likelihood parity at `1E-10`, successful finite estimation, and parent-CDF recovery at `0.05` or `0.06`. The approved hybrid parameter gate is limited to the identifiable two-Weibull shapes and separated two-Normal means.
- Bayesian recovery always uses the unchanged production DEMCzs sampling defaults and asserts them before and after sampling. Current resolved defaults are 3,500 iterations, 1,750 warmup, 10,000 outputs, 90% intervals, posterior mean, seed 12345, and dimension-scaled chain/thinning/initialization values. `CompetingRiskAnalysis` supplies an authorized MAP-centered `UserDefined` population: bounded posterior Hessian, initialization-only regularized Moore-Penrose fallback for singular information, fixed covariance inflation 1.5, sampler-seed draws, and randomized fallback. Focused methods assert that successful post-run results retained MAP initialization.
- `CompositeRecoveryTests` adds ten analytical, published-table, closed-form, Cartesian-posterior, and correlation-orthant methods. Three posterior cells use explicit 5,000-draw child `MCMCResults`, 20-point mean supports, seed 20260803, five probabilities, mean tolerance `0.02`, limit tolerance `0.05`, and fixed-parent band containment.
- All 30 new methods were initially rerun one at a time after MAP initialization, producing 23 passes and seven findings. After `CompositeAnalysis` adopted its user-visible `XTransform.None` contract, the unchanged `CompositeRecoveryTests.MixtureQuantiles_InvertAnalyticWeightedNormalCdf` method passed an exact guarded rerun on 20 August 2026 in 0.770 s at its original probability-dependent tolerance. The supplement therefore records 24 passing methods and six deferred Bayesian competing-risk findings. MAP initialization resolved the former separated three-Weibull R-hat failure, while the remaining findings cover aggregate identification, heterogeneous ridges, correlated-dependence diagnostics, and Gamma uncertainty postprocessing. Analytical formulas and the short R `mistr` table are embedded, so no new generated artifact or manifest entry is required.

Phase exit criteria:

- Point-process simulation, mixture likelihoods, zero-inflation, composite weighting, and competing-risk dependency handling are internally coherent and verified against analytical or simulation fixtures.
- Independent Composite/CFA posterior propagation passes fast seed/range/immutability/cache contracts and separately guarded Cartesian and closed-form oracles. Saved pre-TR-014 summaries require reprocessing.
- The Composite finding is resolved by its unchanged exact rerun, and the six Bayesian competing-risk findings have an approved deferred-research disposition. No algorithm, default, seed, prior, or tolerance changed to open Phase 5. The two existing TR-014 methods pass their repeated exact runs.

## Phase 5 - Time-Series Models

Status: in progress. The Phase 4 prerequisite was satisfied on 20 August 2026: the exact Composite rerun passed and the six Bayesian competing-risk findings were explicitly deferred for separate research.

Compatibility checkpoint (20 August 2026): Package 1 captures 853 UI and 1,657 App public/protected signature lines with committed SHA-256 hashes. Legacy AR, MA, ARIMA, ARIMAX, and pre-v2 `ARMAX` persistence contracts pass; the App transform selector retains its existing XAML/property path and enum values. Core passes 3,180/3,180, UI 576/576, App 431/431, and API 496/496. The strict Debug solution build with `EnforceXmlDocumentation=true` passes with zero warnings/errors; the separately documented validation script is absent from this checkout. No production code or Verification method changed in this package.

Findings and required direction:

- TR-035: complete. AR, MA, and ARIMA now emit `JeffreysScalePrior`; ARIMAX is unchanged. Fast metadata/decomposition regressions and the four-case analytical `-log(sigma)` oracle pass at `1E-12`.
- TR-036: complete. All four models fit Box-Cox/Yeo-Johnson lambda on the raw training prefix,
  freeze it for the full response, persist fitted/manual provenance, and pass holdout-isolation
  regressions plus the independently implemented R profile-likelihood oracle.
- TR-037: complete. ARIMA and ARIMAX predict exactly `T-d+h` model-scale differences, rebuild
  `T+h` transformed levels from the first `d` observed transformed anchors, inverse-transform
  once, and map component `k` to raw slot `k+d`. Hand `d=1`/`d=2`, transformed, component,
  horizon, and exact `d=0` fixed-seed regressions pass, as does the analytical recurrence oracle
  at `1E-10`.
- TR-038: simulate ARIMA on the transformed/differenced scale, integrate, then inverse-transform once.
- TR-039: keep ARIMAX regression and ARMA recursion on one model scale and inverse-transform only at the end.
- TR-040: complete. AR, MA, ARIMA, and ARIMAX scalar, pointwise, component, and prior paths reject non-finite or non-positive innovation scales with exact negative infinity while preserving decomposed shape/metadata. Fast parity and the analytical Gaussian/prior oracle pass.
- TR-041: complete. ARIMAX model step `k` maps to raw response index `k+d`; training contains
  exactly `T-d` differences, later raw timestamps are retained, level covariates are matched by
  exact date without differencing, and the Jacobian spans raw indices `d+max(p,q)` through `T-1`.
  Missing/duplicate required dates fail validation and return negative infinity. Fast alignment,
  holdout, decomposition, and App residual-index regressions pass, as does the independent R
  likelihood oracle for `d=0,1,2` at `1E-10`.
- TR-042: criteria defect closed in Phase 2. Retain regressions proving that AR, MA, ARIMA, ARIMAX, and rating-curve AIC/BIC use data likelihood at MAP; the later phase must not restate prior-density removal as open work.
- TR-046: complete. The unchanged setter atomically rebuilds transform-dependent state;
  `lambda2` is documented and tested as an ignored compatibility placeholder. Clone, UI
  copy/save/open/undo/redo, API mapping, and the independent likelihood oracle pass.

Verification must include algebraic fixtures, scalar/pointwise decomposition, training/holdout isolation, seeded Monte Carlo moments, transformed simulation, exclusion of prior-density terms from AIC/BIC, and flat-prior parity with MLE criteria.

Phase exit criteria:

- Time-series likelihood, forecasting, simulation, preprocessing, criteria, and pointwise decomposition behavior are consistent with the stated model scale and data indexing.

## Phase 6 - Rating Curve, Bivariate, and Spatial Models

Status: planned.

Findings and required direction:

- TR-043: include the base-10 change-of-variables term in discharge-space scalar and pointwise likelihoods.
- TR-044: enforce a defensible strictly positive exponent lower bound and test two-sided continuity.
- TR-045: validate only date-aligned likelihood pairs; report unrelated invalid records separately.
- TR-047: criteria defect closed in Phase 2. Retain the bivariate data-likelihood-at-MAP regression and its flat-prior/common-marginal comparability limits; do not restate prior-density removal as open work.
- TR-048: marginalize missing spatial sites with observed-site correlation submatrices cached by missingness pattern.
- TR-049: use row/year observed-data contributions for marginals and copula terms; classify latent Gaussian-process density as prior structure and enforce scalar/pointwise identities.
- TR-050: preserve completed cross-validation results across the restoration refit.
- TR-051: build an actual reduced training model for every held-out site.
- TR-052: pass held-out covariates consistently for location, scale, and shape trends.
- TR-053: record failed folds as failed/NaN, aggregate successful folds only, and report the success count.
- TR-054: use conditional Gaussian-process prediction per posterior draw and propagate conditional spatial variance.
- TR-055: prior-density criteria defect closed in Phase 2. Remaining work is to verify nonempty spatial row/year blocks for BIC and resolve the Gaussian-process, missing-site, weighting, dependence, and MAP-versus-MLE caveats; prefer explicitly defined posterior-predictive criteria.
- TR-056: fit each bootstrap replicate to its resampled data and reject incomplete replicate sets.
- TR-057: derive Hessian and score variability from the same estimating equations; report failure rather than substitute `J`.
- TR-058: compute regional statistics within each joint posterior draw before taking interval quantiles.
- TR-059: rename the current site-weight method as a weighting heuristic and remove composite-likelihood claims. Treat true pairwise composite likelihood and Godambe correction as a separately approved enhancement.
- TR-060: add explicit distance metric and coordinate-unit configuration, preserving Cartesian behavior by default and supporting validated geodesic coordinates additively.
- TR-061: simulate correlated normals through the fitted spatial correlation matrix and map them through site-specific inverse GEV CDFs.
- TR-062: make `RunAsync` dispatch the selected uncertainty method and record the method in result metadata.

Phase exit criteria:

- Rating-curve likelihoods, bivariate criteria, and spatial missingness/dependence/prediction/uncertainty workflows are mathematically explicit and verified against analytical, external, or simulation oracles.

## Completed Phase 2 Batch Record

The following batches record the approved work that closed Phase 2. They are retained for traceability, not as an active work queue.

### Batch 1 - Criteria, Profiles, GMM, and Covariance

Completed through surgical approvals:

- TR-011 and TR-022 documentation/API inventory corrections;
- TR-023 MLE and MAP nuisance profiling;
- TR-026 selected-weight Hansen J;
- TR-027 explicit covariance status/failure contracts;
- TR-032 obsolete compatibility adapters for mislabeled GMM influence;
- TR-034 overidentified one-step fit plus fixed-weight and efficient covariance parity.

Retain the focused regressions and committed artifacts. No production item in this batch remains open.

### Batch 2 - External Model-Comparison Oracles

Completed 25-27 July 2026:

- DIC against R `BayesianTools`.
- WAIC against R `loo`.
- PSIS-LOO aggregate/pointwise values, smoothed weights, effective sample size, Pareto $k$, tail regimes, and diagnostic thresholds against R `loo`.
- True profile likelihood against R `bbmle` and closed-form nuisance optimization.
- GMM fitting, Hansen J, and fixed-weight/efficient covariance against R `gmm` plus analytical sandwiches.

Retain the pinned artifacts and exact focused regressions. Optional ArviZ agreement is not required.

### Batch 3 - PSIS Implementation

Completed 26 July 2026:

- TR-024 fixed without public API or Numerics changes.
- Pinned R `loo` and `posterior` parity passed for aggregate and pointwise LOO, six tail regimes, Pareto-k thresholds, and XML classification persistence.
- Default WAIC and PSIS share one transient pointwise matrix; influence diagnostics reuse $O(n)$ cached summaries.
- Exact and moment-matched refits remain outside the default runtime path; `r_eff = 1` is documented.
### Batch 4 - Numerics MCMC Diagnostics

Completed findings:

- TR-025
- TR-030
- TR-029

TR-025 records every realized ARWMH state while preserving continual adaptation. TR-030 preserves generic accepted-transition rates, exposes Hamiltonian acceptance and other constant-memory diagnostics on `NUTS`, transfers only Hamiltonian acceptance through the existing result field, and uses acceptance-only BestFit reporting. The NUTS trajectory-selection algorithm is unchanged. The existing Numerics analytic-gradient initialization regression and a BestFit coupled-prior finite-difference verification cover both gradient routes. TR-029 modernizes only existing diagnostic internals and the report threshold, retaining public and serialization compatibility.

TR-029 uses pooled midranks, inverse-normal scores, split and folded chains, and FFT/Geyer autocorrelation processing. Its committed R `posterior` fixtures cover IID, autocorrelated, shifted, scale-mismatched, sticky-tail, tied, constant, warmup, and permutation cases. The implementation performs no model-target evaluations.

### Batch 5 - Joint Prior Sampling

Accepted limitation:

- TR-028

Characterization is complete and Phase 2 is closed. A future, separately approved enhancement may add an optional joint-prior-sampling capability without changing `IModel`; it is not unfinished Phase 2 work.

## Verification and Documentation Acceptance

- Deterministic algebra uses approximately `1e-10` absolute tolerance.
- Cross-language likelihood and criterion comparisons use `1e-8` absolute and `1e-7` relative tolerance unless the oracle manifest documents a stricter or algorithmically necessary exception.
- Optimizer parity uses scaled parameter tolerances no looser than `1e-5`, plus maximum-log-likelihood parity. When comparing objective-converged global optimizers with parameter-converged local optimizers, documented exceptions such as `1e-4` scaled parameter tolerance are allowed if likelihood parity and scientific conclusions are unaffected.
- PSIS aggregate and pointwise values use `1e-10` absolute tolerance; smoothed weights, Pareto-k, and importance-sampling effective sample size use `1e-8`.
- Coverage studies pass only when nominal coverage lies within the predeclared binomial confidence interval and the minimum successful-replicate count is met.
- Seeds, runtime versions, package versions, source commit, generation command, dataset hash, and tolerance rationale are recorded before observing C# results.
- Every confirmed production fix updates, in the same completed batch, its `review-findings.md` row and detailed section, corresponding technical-reference chapter, relevant verification-report chapter, and traceability table.
- Every core-library change passes Debug compilation, the XML/namespace documentation gate when available, `RMC.BestFit.Tests`, `RMC.BestFit.UI.Tests`, `RMC.BestFit.App.Tests`, public API compatibility, and serialization regression checks.
- Final conclusions are claim-specific. No subsystem is called verified merely because it compiled, converged, or reproduced its own prior output.

## Fresh-Session Prompt

Use this prompt to continue from a clean session:

```text
We are continuing RMC.BestFit verification finalization after completing the original Phase 1-4 finding scopes and running the Phase 4 competing-risk/composite recovery supplement. BestFit implementation checkpoint: 9d252f6 plus the current TR-014, recovery-test, and competing-risk MAP-initialization working-tree changes. Recovery sources are pinned to Numerics c361f28 and RMC-TotalRisk d4d43e6; the Numerics logarithmic-base clone correction is e57af20.

Read docs/verification/verification-finalization-plan.md first, then docs/technical-reference/review-findings.md, docs/verification/README.md, docs/verification/model-estimation.md, docs/verification/test-inventory.md, and verification/data/MANIFEST.md.

Do not compile PDFs unless I explicitly request PDF QA. Update Markdown source only.

Current checkpoint: Phase 0 infrastructure is operational; Phases 1 through 4 are closed for their approved scopes, and Phase 5 is in progress. TR-014 independently resamples actual retained Composite and CFA sources with the existing seed, while `BivariateAnalysis` remains unchanged and conditional on fixed marginals. The 30-method recovery supplement records 24 passes and six explicitly deferred Bayesian competing-risk findings after the unchanged extreme-tail Composite method passed its 20 August 2026 guarded rerun. Bulletin 17C remains a valid composite child for Equal/AIC/BIC/RMSE and receives zero weight rather than type rejection when DIC/WAIC/LOOIC is unavailable and another child is usable.

Constraints:
- Never run the full RMC.BestFit.Verification suite.
- Run verification only with scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>.
- Run the three unit-test projects after any RMC.BestFit/UI/App code change.
- Do not change RMC.BestFit, UI, App, or Numerics production code until you first explain the proposed fix and I approve it.
- Preserve public API signatures wherever possible.
- Numerics reference should use local C:\GIT\Numerics, and Numerics work should use .NET 10 only.
- C# verification tests must use committed oracle artifacts, not R/Python at runtime.
- Markdown docs are the source of truth during development. Do not render PDFs unless requested.
- Write technical reference docs as current technical documentation, not as a historical issue log.
- Preserve unrelated modified/untracked files.

First task:
Obtain an approved technical disposition for the seven recovery-supplement findings before beginning Phase 5. Preserve the closed Phase 1-4 numerical and compatibility contracts. Never run the full Verification project.
```

## Off-Ramps

Stop and ask the user before continuing if:

- a proposed fix requires a public API or serialization breaking change;
- an external package oracle disagrees with an analytical oracle and the discrepancy is not yet understood;
- a focused verification result exposes a production defect outside the approved batch;
- a Numerics fix is needed but the impact on BestFit cannot be isolated with local project references;
- a finding requires a scientific policy choice rather than an implementation correction;
- unrelated user changes overlap the files that must be edited.
