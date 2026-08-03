# Progress

## 2026-08-03

- Closed TR-012 with a focused Numerics correction (`cafe6cf3837988341912a5aa8bfda444ea55ff77`) that preserves the independent golden seed sequence and routes the existing `CompetingRisks.GenerateRandomValues` signature through dependency-aware simulation. Numerics Release build and all 2,023 tests passed on the validated target.
- Added four narrowly scoped competing-risk verification methods. Independent, perfectly positive, perfectly negative, and correlation-matrix modes each passed separately through the exact guarded runner against Gaussian-copula Spearman-rank and analytical maximum-CDF targets; the full Verification project was not run.
- Closed TR-013 with explicit invalid/unavailable criterion classification, exact-zero weights for excluded children, named diagnostics, an all-invalid failure, and a division-free exact-zero RMSE branch. Bulletin 17C is not type-rejected: it participates in Equal/AIC/BIC/RMSE and receives zero weight with a warning only when a selected posterior criterion is unavailable and another child is usable.
- Closed TR-015 by adding the authorized defensively owned `CorrelationMatrix` property to core and UI composite analyses, validating structure/dimension/positive definiteness, preserving optional legacy loading, serializing invariant XML and SQLite data, and propagating the matrix into point estimates and frequency results without changing existing public method signatures.
- Deferred TR-014 by direction. Raw child-posterior index pairing remains unchanged; the independence/resampling policy must be reviewed with affected bivariate posteriors before implementation. Phase 4 therefore remains open.
- Passed the strict Debug XML-documentation solution build and Release solution build with zero warnings/errors, the explicit public API baseline, Verification compilation, and all mandatory fast suites: Core 3,116/3,116, UI 568/568, and App 428/428. The named XML-validation wrapper remains absent, so the deleted-namespace scan was run directly and found no matches.

## 2026-07-31

- Implemented the approved TR-006/TR-007/TR-008 mixture correction without changing public weight-related signatures: Numerics retains $K$ physical weights, while BestFit uses $K-1$ direct physical weights and derives the final weight.
- Corrected the exact-zero positive-hurdle law across density, CDF, quantiles, simulation, mixed-observation likelihoods, and EM; exact atom estimation uses exact annual records only, and impossible EM rows now fail with row context.
- Committed the focused Numerics batch as `1462e35`; Release builds passed with zero warnings/errors and all 2,016 tests passed independently on net481, net8.0, net9.0, and net10.0.
- Added BestFit fast coverage and three compiled recovery-parity methods. The strict XML Debug build, Release solution build, public API baseline, and Verification compilation passed with zero warnings/errors; mandated fast suites passed Core 3,101/3,101, UI 564/564, and App 428/428.
- Closed the Phase 4 mixture subset after six exact guarded runs: three BestFit-generated Numerics/BestFit recovery-parity methods and three BestFit-generated Bayesian `MixtureAnalysis` recovery methods all passed with one TRX each. Bayesian closeout includes posterior recovery, label ordering, finite split R-hat below 1.1, conservative ESS above 100, and the positive-hurdle atom check.
- No legacy mixture-posterior migration or parameterization version is included; affected saved results require re-estimation. The broader Phase 4 remains open for competing-risk and composite work.
- Closed the Phase 4 point-process subset. TR-004 is complete after both independent mixed-likelihood cells passed; the seasonal fixture was corrected from an all-above threshold to one below/two above without changing production code or the `2E-7` tolerance.
- Re-ran all Bayesian recovery fixtures with the untouched `BayesianAnalysis` defaults and 1,000 observations. Calendar-year uniform recovery, nonseasonal production recovery, and seasonal production recovery passed; the former seasonal second-Kappa miss disappeared. The water-year recovery failure was a verification-coordinate error: it changed `K1/K2` from `170/350` to `80/260` instead of changing only the block origin. Holding the block-day parameters fixed and shifting only the dates to an October water year passed the exact method. All ten point-process cells now pass without a production formula, sampler default, prior, or tolerance change.

## 2026-07-30

- Implemented the TR-004/TR-005 point-process batch: preserved source exposure, retained manual year/index-span fallback and warnings, separated empirical count/rate from fitted intensity, and corrected fixed-size and duration simulation to the approved empirical-Lambda Poisson/Hosking-GPA process.
- Added the fast monthly-histogram changepoint-prior heuristic with calendar/water-year rotation, month-length flatness guard, circular smoothing, deterministic valleys, five-month windows, broad fallbacks `[1,251)` and `[200,367)`, custom-prior preservation, and ordinary parameter serialization.
- Kept all six seasonal GEV defaults, likelihood equations, DEMCzs settings, tolerances, and seed contracts unchanged.
- Consolidated independent exponential-clock Poisson, analytical Hosking-GPA, PERT placement, and uniform recovery fixtures. Removed the intentionally failing PERT recovery experiments after they demonstrated the expected interior-timing mismatch.
- Corrected the water-year automatic-prior recovery fixture to use shifted block-day changepoints 80 and 260 rather than calendar coordinates 170 and 350.
- Audited `PointProcessModel`, `PointProcessAnalysis`, fast tests, and focused verification for state bugs, stale XML, encoding damage, low-value narration, and obsolete fixture references. Strict builds, all fast suites, and Verification compilation pass with zero warnings.
- Passed the strict Debug solution build with `EnforceXmlDocumentation=true`, the Release solution build, and all mandated fast suites: Core 3,089/3,089, UI 564/564, and App 428/428. The `validate-code-xml-docs.ps1` wrapper named in `AGENTS.md` is not present in the tracked scripts inventory, so its private-method scan could not be invoked; the touched point-process files were audited directly.
- Resolved the seasonal point-process input and annualization decisions: every exact seasonal POT observation now requires a valid date, while annual/block-indexed uncertain, interval, and threshold records use the maximum of the two independent exposure-adjusted seasonal processes. Exposure fractions weight process intensities, not annual mixture probabilities. Fast Core tests pass at 3,089/3,089; ten exact current-source verification methods remain to be run under repository policy.
## 2026-07-28

- Closed TR-003 as a documentation-only decision: grouped perception thresholds use the existing deterministic earlier-below/terminal-above disaggregation after preserving explicit indexes, and distribution-dependent priors are evaluated at the last, most-recent observed time step consistent with the published quantile-prior workflow. No code or permutation-invariance change was required.
- Closed TR-016 as a documented architecture decision: Bulletin 17C reuses the Bayesian analysis result-storage shape for stable persistence and reprocessing, with GMM/frequentist meanings for legacy member names and no code/API redesign.
- Closed TR-020 in its approved unit scope by guarding Cohn intervals and asymptotic variance to exact-data LP3; all unsupported parent/data cases now fail explicitly, while numerical Cohn verification remains deferred.
- Closed TR-021 with seven exact formal Bulletin 17C worked-example runs: `Test_Example1` through `Test_Example7` all passed published LP3 mean, standard deviation, and skewness comparisons at absolute tolerance `1E-3`.
- Reviewed and repaired XML documentation/comments throughout `Bulletin17CDistribution` and `Bulletin17CAnalysis`, including damaged mathematical notation, stale Bayesian wording, malformed XML, and low-value comments; no AI/tool breadcrumbs remain.
- Closed TR-017 without renaming `BiasCorrectedBootstrap`; technical documentation identifies it as the second-order bias-corrected pivotal bootstrap and distinguishes it from scalar BC/BCa intervals.
- Reworked Bulletin 17C bootstrap initialization with bounded midpoint, ROS, distribution-default, and parent starts ranked against the same penalized GMM target; confirmed-converged iterative GMM results are accepted even when the final inner pass reaches its evaluation cap.
- Kept Yeo-Johnson pivotal links and added post-inverse parameter repair against existing model bounds; Example 5 replicate 695 exercises the skew-bound repair without throwing.
- Removed the obsolete Mahalanobis refit rejection after the guarded sweep showed it caused every remaining outer retry. Fourteen exact Examples 1-7 ordinary/pivotal cells then produced 13,000 unguarded finite outputs from 13,000 realizations with zero retries, substitutions, failed candidates, optimizer fallbacks, and BestFit/Numerics first-chance exceptions.
- Added and pinned the Examples 5-7 reliability cells (500 outputs per method for highly censored Example 7) and retained the parent-fit fallback solely to guarantee configured downstream output length.

## 2026-07-17

- Prepared v2.0.0 release metadata: switched central dependencies to `RMC.Numerics` 2.1.4 and `RMC.Wpf.Framework.*` 1.0.4, removed the local project-reference override file, and synchronized final UI/App/project metadata to `2.0.0`.
- Added official release messaging for the PR body, GitHub Release body, LinkedIn post, and screenshot gallery suggestions in `docs/release-messaging-v2.0.0.md`.
- Updated release-facing README, documentation index, and example index wording from beta/pre-release language to official v2.0.0 release language.
- Added a BestFit-specific release workflow to local `AGENTS.md` and `CLAUDE.md`; both files are ignored by the repository unless force-added intentionally.
- Validated release readiness with `dotnet restore`, Debug and Release builds, all four fast test projects, and `dotnet pack src/RMC.BestFit/RMC.BestFit.csproj -c Release -o packages /p:Version=2.0.0`.
- Inspected `packages/RMC.BestFit.2.0.0.nupkg`: package id/version are `RMC.BestFit`/`2.0.0`, release notes are present, README and LICENSE are included, and the package dependency metadata points to `RMC.Numerics` 2.1.4.
## 2026-07-16

- Diagnosed the B17C bootstrap regression: assigning a resampled frame through the public `Bulletin17CDistribution.DataFrame` setter ran `SetDefaultParameters`, which wiped the cloned parent initials, disabled every parameter penalty (silently dropping regional-skew prior propagation), and — when default-parameter derivation threw for threshold-heavy boot frames — emptied the parameter list so every replicate failed and fell back to the parent (uncertainty collapsed to a point mass; the progress bar sat at 1% while retries crawled).
- Fixed GMM `Status` coherence: multi-pass strategies restore `Status = Success` when abandoning a failed refinement pass and returning an earlier valid solution; `Estimate()` resets stale outputs up front.
- Added `Bulletin17CDistribution.CloneWithDataFrame` (XElement round-trip preserves parameters, penalties, and links) and warm-started every bootstrap replicate at the parent fit; aligned the replicate acceptance gate with the parent fit (reject only hard failures and non-finite estimates).
- Redesigned failure handling across the uncertainty samplers: failed or rejected realizations are discarded after retries — never substituted with the parent vector — with abort guards (fewer than two survivors or >50% discards) surfaced through the new `UncertaintyDiagnosticMessage` and the UI warning message.
- Extended `BootstrapDiagnostics` (retained count, transform failures, per-attempt GMM status distribution; backward-compatible XML), made the plain MVN sampler report the same diagnostics, rewrote the report section for discard semantics with retention warnings, and persisted diagnostics with the analysis.
- Fixed sampling-loop progress: first tick after the first completed replicate (`AnalysisProgress.ShouldReportLoopProgress`) and monotone pivot-phase mapping (55/56/44) replacing the backwards jump.
- Hardened WPF dispatch: `B17CAnalysisPropertiesControl.Element_PropertyChanged` now marshals to the dispatcher; the UI wrapper suppresses its own run's mid-flight `ThresholdSeries` recompute notification (narrow guard) so `ClearResults` cannot fire on a worker thread.
- Verified end-to-end with a headless harness on B17C Examples 1 and 4: penalties now survive on all boot clones and previously all-failing threshold-heavy replicates fit successfully; all four unit suites pass with zero warnings under XML-docs-as-errors builds.

## 2026-07-14

- Replaced the `MainProjectNode` local Quick Start Guide PDF action with the online RMC-BestFit User Guide.
- Added a testable connectivity and default-browser launcher that reuses `TimeSeriesDownload.IsConnectedToInternet()`.
- Added deterministic App tests for online, offline, validation, failure propagation, and Help-menu wiring behavior.
- Added main-branch Technical Reference and Example Projects links through the generalized online Help launcher.
- Added active-development note boxes to the technical-reference and example-project indexes.
- Reserved the type-compatible `HelpImage` resource for the User Guide and separated the text-only online resources from application items.
- Replaced the framework's default Terms and Conditions document with the verbatim 0BSD license and optional Zenodo citation guidance using a copyable, clickable concept-DOI URL.
- Corrected the `YeoJohnsonLink.FitLambda` XML reference to use the Numerics method's `IList<double>` signature.
- Added a README Documentation callout identifying the RMC-BestFit 2.0 documentation and verification materials as under active development.
- Validated the affected App projects with zero warnings and all fast Core, UI, and App tests passing.
- The required repository XML validation script is absent; equivalent scoped XML-as-errors builds passed for all affected projects.
