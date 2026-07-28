# Progress

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
