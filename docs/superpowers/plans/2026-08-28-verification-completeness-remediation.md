# Verification Completeness Remediation Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Give every RMC.BestFit scientific analysis and accompanying model complete, consistently governed numerical verification while removing unit and regression tests from `RMC.BestFit.Verification`.

**Architecture:** A machine-readable catalog maps every Verification method to one scientific analysis, model, evidence kind, oracle, recovery design, and report section. A repository validator enforces inventory completeness throughout the program, while focused session chunks move engineering contracts fast, normalize N=1000 recovery, add independent evidence, and update the report without changing production algorithms.

**Tech Stack:** C# 14, .NET 10, MSTest, PowerShell, Markdown, JSON, frozen R/Python oracle artifacts, and the existing guarded focused-test runner.

**Spec:** `docs/verification/verification-completeness-audit.md`

## Global Constraints

- Haden Smith is the final technical and numerical authority; do not change an established algorithm, likelihood, formula, prior, sampler, convergence rule, seed behavior, default tolerance, serialization shape, or public contract without explicit prior approval.
- `RMC.BestFit.Verification` contains only analytical, independent, external-package, published/real-source, recovery, or coverage evidence with a declared oracle and acceptance rule.
- Deterministic state, validation, exception, serialization, dispatch, caching, and regression contracts belong in the fast test projects and may not run optimizers or MCMC.
- Every genuine recovery design uses exactly N=1000 observational units as defined in the audit specification.
- Recovery acceptance follows the common 95% interval/standardized-error rule and the conditional 5% secondary rule in the audit specification; source-specific external parity tolerances remain separate.
- Verification methods are run only when Haden Smith explicitly authorizes the exact method, using `scripts/run-verification-test.ps1 -Test <fully-qualified-method>`; never run the full Verification suite.
- Do not execute Bulletin 17C confidence-interval coverage methods in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests`. Inventory and ownership-review those sources and preserve historical results as reruns-on-request; other Verification methods still require exact explicit authorization.
- Preserve unrelated dirty work, stage explicit paths only, review staged diffs, and do not commit or push without explicit authorization.
- All new and modified classes and methods require professional XML documentation; code comments explain scientific intent, parameterization, or non-obvious constraints rather than restating mechanics.

---

## Session-size execution map

| Chunk | Deliverable | Primary acceptance gate |
|---:|---|---|
| 1 | Durable audit baseline and implementation plan | Counts/current HEAD reconciled; links and direct new-file checks clean |
| 2A | Validator behavioral tests and catalog schema | Red/green validator fixtures; schema accepted |
| 2B1 | Catalog ModelEstimation | 65 current declarations classified |
| 2B2 | Catalog DistributionFitting | 45 current declarations classified |
| 2C1 | Catalog Univariate validation/report sources | 60 current declarations classified |
| 2C2 | Catalog Bulletin 17C | 88 declarations/117 units classified; coverage execution exclusions explicit |
| 2C3 | Catalog PointProcess, CompetingRisk, Mixture, and Composite | 53 current declarations classified |
| 2D1 | Catalog Bivariate and RatingCurve | 69 current declarations classified |
| 2D2 | Catalog TimeSeriesAnalysis | 96 current declarations classified |
| 2D3 | Catalog SpatialExtremes and close the baseline | All 506 declarations and 535 execution units accounted for |
| 3 | Model-estimation ownership cleanup | Fast core tests pass; no scientific oracle lost |
| 4A | Bulletin 17C and point-process ownership cleanup | Fast destinations pass; retained oracles clearly named |
| 4B | Composite and rating-curve ownership cleanup | Analytical cells retained; engineering contracts moved fast |
| 4C | Spatial ownership cleanup | Status/dispatch/accounting contracts fast; numerical gaps remain explicit |
| 5 | Shared recovery design/acceptance infrastructure and estimator recovery | N=1000 enforced; exact authorized estimator cells pass |
| 6A | Distribution-fitting verification | All 15 fitting families mapped and accepted |
| 6B | Univariate family and trend verification | All 15 families and every trend type mapped and accepted |
| 7 | Bulletin 17C verification completion | Six-family recovery plus published/PeakFQ evidence |
| 8 | Point-process verification completion | Analytical, external-compatible, and N=1000 recovery evidence |
| 9 | Competing-risk verification completion | Every design N=1000; identified response-space acceptance |
| 10A | Mixture verification completion | Independent mixture parity and identified N=1000 recovery |
| 10B | Composite verification completion | Analytical composition plus N=1000 predictive recovery |
| 11A | Bivariate verification completion | Seven copulas at N=1000 with Student-t external parity |
| 11B | Coincident-frequency verification completion | Closed-form and nonlinear independent recovery evidence |
| 12 | Rating-curve verification normalization | Common recovery policy and compatible external oracles |
| 13A | AR and MA normalization | Two dedicated evidence/report rows; consolidated acceptance |
| 13B | ARIMA and ARIMAX normalization | Two dedicated evidence/report rows; R crosswalk explicit |
| 14A | Spatial correlation and cross-validation oracles | Three correlation models and independent fold results |
| 14B | Spatial prediction and uncertainty oracles | Independent GP, Godambe, bootstrap, and VIF evidence |
| 15 | Spatial N=1000 recovery completion | All spatial recovery uses 1000 row/year vectors |
| 16A1 | XML/comments: ModelEstimation | Professional scientific documentation in 65 current declarations |
| 16A2 | XML/comments: DistributionFitting | Professional scientific documentation in 45 current declarations |
| 16A3 | XML/comments: Univariate validation/report | Professional scientific documentation in 60 current declarations |
| 16A4 | XML/comments: Bulletin 17C | Professional scientific documentation in 88 current declarations |
| 16A5 | XML/comments: point, competing, mixture, composite | Professional scientific documentation in 53 current declarations |
| 16B1 | XML/comments: Bivariate and RatingCurve | Professional scientific documentation in 69 current declarations |
| 16B2 | XML/comments: TimeSeriesAnalysis | Professional scientific documentation in 96 current declarations |
| 16B3 | XML/comments: SpatialExtremes | Professional scientific documentation in 30 current declarations |
| 16C1 | Report: estimation, fitting, univariate, B17C | Four explicit reconciled sections |
| 16C2 | Report: point, competing, mixture, composite | Four explicit reconciled sections |
| 16D1 | Report: bivariate, coincident, rating | Three explicit reconciled sections |
| 16D2 | Report: AR, MA, ARIMA, ARIMAX | Four explicit reconciled sections |
| 16D3 | Report: spatial and book order | Spatial section and corrected book order |
| 16E | Final strict gates and report artifact QA | Strict catalog, XML, fast suites, focused evidence ledger, and PDF QA pass |

### Chunk 1: Freeze the active audit baseline

**Files:**
- Create: `docs/verification/verification-completeness-audit.md`
- Create: `docs/superpowers/plans/2026-08-28-verification-completeness-remediation.md`
- Modify: `docs/verification/README.md`

**Interfaces:**
- Consumes: current source inventory, `AGENTS.md`, `docs/verification/methodology.md`, and the historical finalization ledger.
- Produces: the authoritative remediation spec and this resumable session map.

- [x] Record the current commit, branch, clean-state observation, C# file count, test-bearing file count, and method count without running Verification.
- [x] Record the 15-analysis/model coverage matrix, N=1000 sample-unit definitions, common recovery acceptance, ownership candidates, report gaps, and completion conditions.
- [x] Link the active remediation from `docs/verification/README.md` without rewriting historical passed-result claims.
- [x] Check every local link, run `git diff --check` for tracked changes, and directly scan new untracked Markdown files for whitespace defects.
- [x] Review the diff against the user request and the non-negotiable algorithm-change boundary.

### Chunk 2A: Build the validator harness and schema

**Files:**
- Create: `docs/verification/verification-catalog.schema.json`
- Create: `scripts/validate-verification-catalog.ps1`
- Create: `scripts/test-verification-catalog-validator.ps1`

**Interfaces:**
- Consumes: the Chunk 1 ownership boundary and recovery design.
- Produces: `validate-verification-catalog.ps1 -Catalog <path> -SourceRoot <path> [-RequireComplete]`; default mode validates structure and reports open gaps, while strict mode rejects them.

- [x] Write fixture-driven tests for a missing source entry, duplicate entry, unknown evidence kind, recovery N other than 1000, missing report anchor, unlisted ordinary method, unlisted data-driven method, and mismatched DataRow count.
- [x] Run the fixture script before implementation and confirm it fails because the validator does not exist.
- [x] Implement schema fields `source`, `namespace`, `class`, `analysis`, `model`, `primaryEvidenceKind`, `evidenceTags`, `disposition`, `methodOverrides`, `executesEstimator`, `sampleUnit`, `sampleSize`, `seed`, `oracle`, `acceptanceRule`, `artifact`, `reportAnchor`, `status`, and `gap`.
- [x] Implement discovery for `[TestMethod]`, `[DataTestMethod]`, and named `[DataRow]` execution units; allow multiple evidence tags but exactly one primary claim and disposition.
- [x] Run the fixture script green and document the exact exit-code/message contract in its XML/help comments.

### Chunk 2B1: Catalog ModelEstimation

**Files:**
- Create: `docs/verification/verification-catalog.json`
- Inspect and catalog: `src/RMC.BestFit.Verification/ModelEstimation/`

**Interfaces:**
- Consumes: the Chunk 2A schema.
- Produces: method-level classifications for the current 65 ModelEstimation declarations.

- [x] Enumerate every declaration and record primary claim, evidence tags, disposition, oracle, acceptance, estimator-execution flag, and report anchor.
- [x] Mark smoke, same-path, qualitative, or engineering-contract methods `Open` with the exact Chunk 3 disposition.
- [x] Record JointPrior as analytical accepted-limitation evidence while separating its same-path fitness assertion.
- [x] Run default catalog validation and compare entries with the focused-result inventory without copying unsupported “Passed” status.

### Chunk 2B2: Catalog DistributionFitting

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `src/RMC.BestFit.Verification/DistributionFitting/`

**Interfaces:**
- Produces: method-level classifications for the current 45 DistributionFitting declarations.

- [x] Enumerate every declaration and identify the exact theoretical, external, published, or recovery claim.
- [x] Record family parameterization, dataset/artifact provenance, tolerance, and report anchor.
- [x] Mark missing N=1000 end-to-end family recovery as open analysis-level gaps rather than inventing method evidence.
- [x] Run default catalog validation and reconcile entries with the DistributionFitting report.

### Chunk 2C1: Catalog Univariate validation and report sources

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `Univariate/ValidationTests/` and `Univariate/VerificationReportTests/`.

**Interfaces:**
- Produces: classifications for the current 60 declarations in these sources.

- [x] Enumerate every method and record distribution/trend model, sample unit/N, seed, oracle, acceptance, and report anchor.
- [x] Mark N=300 family recovery and missing ReciprocalTrend evidence `Open`.
- [x] Distinguish report-generation arithmetic from scientific verification and mark engineering-only cells for ownership review.
- [x] Run default catalog validation for these paths.

### Chunk 2C2: Catalog Bulletin 17C

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `Univariate/Bulletin17CTests/`.

**Interfaces:**
- Produces: classifications for 88 declarations and 117 current execution units, including explicit execution exclusion for all B17C confidence-interval coverage methods.

- [x] Enumerate the 30 Cohn DataRows and every ordinary method.
- [x] Mark `B17CCoverageTests`, `B17CCensoredCoverageTests`, and `B17CCohnEtAlCoverageTests` as execution-excluded historical coverage evidence.
- [x] Mark bootstrap completion/accounting and low-outlier setter contracts for Chunk 4A ownership disposition.
- [x] Record six-family N=1000 recovery and published/PeakFQ value gaps without scheduling any coverage run.
- [x] Run default catalog validation for Bulletin 17C.

### Chunk 2C3: Catalog PointProcess, CompetingRisk, Mixture, and Composite

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog the four corresponding subdirectories under `Univariate/`.

**Interfaces:**
- Produces: classifications for the current 53 declarations in these analyses.

- [x] Enumerate each method and distinguish likelihood/theory, external, recovery, and simulation-calibration evidence.
- [x] Record recovery N and seeds; mark CompetingRisk N=1500 and misnamed Composite/PointProcess recovery cells `Open`.
- [x] Mark same-ecosystem or same-path comparisons accurately rather than external/independent.
- [x] Run default catalog validation for the four directories.

### Chunk 2D1: Catalog Bivariate and RatingCurve

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `Bivariate/` and `RatingCurve/`.

**Interfaces:**
- Produces: classifications for the current 69 declarations.

- [x] Record all copula/rating models, external artifacts, recovery sample units, seeds, and acceptance rules.
- [x] Mark N=100 bivariate recovery, Student-t external parity, inconsistent rating tolerances, and default-bound contracts `Open`.
- [x] Run default catalog validation for both directories.

### Chunk 2D2: Catalog TimeSeriesAnalysis

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `TimeSeriesAnalysis/`.

**Interfaces:**
- Produces: classifications for the current 96 declarations across AR, MA, ARIMA, and ARIMAX.

- [x] Record model order, transform/differencing, external R target, recovery N/seed, Bayesian diagnostics, and acceptance for every method.
- [x] Mark duplicated legacy acceptance and inconsistent interval rules `Open` while preserving current historical results.
- [x] Run default catalog validation for TimeSeriesAnalysis.

### Chunk 2D3: Catalog SpatialExtremes and close the baseline

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Inspect and catalog: `SpatialExtremes/` and any remaining root test-bearing source.
- Modify: `docs/verification/methodology.md`

**Interfaces:**
- Produces: complete coverage of the current 506 declarations and 535 execution units.

- [x] Catalog the current 30 spatial declarations with correlation model, oracle provenance, recovery rows/sites, tolerance, and disposition.
- [x] Mark same-production-path CV/prediction/uncertainty and non-1000 spatial recovery `Open`.
- [x] Run default validation and confirm exactly 506 declarations and 535 units resolve at this baseline checkpoint.
- [x] Document catalog fields, multi-evidence claims, accepted limitations, default/strict behavior, and B17C coverage execution exclusions in methodology.
- [x] Run the fixture tests and default repository validation after reconciliation.

### Chunk 3: Clean model-estimation ownership

**Files:**
- Modify: `src/RMC.BestFit.Verification/ModelEstimation/MLEIntegrationTests.cs`
- Modify: `src/RMC.BestFit.Verification/ModelEstimation/GeneralizedMethodOfMomentsRecoveryTests.cs`
- Modify: `src/RMC.BestFit.Verification/ModelEstimation/ProfileLikelihoodGridPointFailureTests.cs`
- Modify: `src/RMC.BestFit.Verification/ModelEstimation/JointPriorSamplingVerificationTests.cs`
- Modify: `src/RMC.BestFit.Verification/ModelEstimation/BayesianAnalysisRecoveryTests.cs`
- Modify or create focused files under: `src/RMC.BestFit.Tests/ModelEstimation/`
- Modify: `docs/verification/verification-catalog.json`

**Interfaces:**
- Consumes: catalog classifications from Chunk 2.
- Produces: Verification files containing only numerical claims and inline-fixture fast contracts that do not run an estimator.

- [x] For every proposed fast contract, name the production regression it catches and confirm equivalent fast coverage does not already exist.
- [x] Write or identify the fast test first; for new behavior protection, observe the expected failure before extracting the minimal deterministic seam.
- [x] Move only deterministic state, strategy, failure-policy, or marginal-sampling behavior; remove redundant smoke assertions rather than duplicating them.
- [x] Preserve the 13 current N=1000 MLE family recovery methods, analytical profile likelihood, analytical information criteria, and any independently derived oracle; remove the small-sample completion, likelihood-sign, and repeated-run comparisons without porting optimizer smoke into fast tests.
- [x] Remove the four current GMM success/same-path cells after confirming the stronger specification and objective-gradient oracles remain; add true N=1000 GMM recovery in Chunk 5 rather than relabeling smoke tests.
- [x] Retain the joint-prior independent-marginal moment calculation as analytical evidence for an accepted limitation; split or remove only its same-path fitness equality.
- [x] Replace the qualitative informative-prior assertion with the exact Normal-Normal conjugate result or classify it for fast removal.
- [x] Run the affected fast test classes and the complete `RMC.BestFit.Tests` project.
- [x] Update catalog entries and ownership inventory; do not run Verification methods in this chunk unless exact methods are separately authorized.

### Chunk 4A: Clean Bulletin 17C and point-process ownership

**Files:**
- Modify ownership candidates under `Univariate/Bulletin17CTests/` and `Univariate/PointProcessTests/`.
- Modify or create matching fast tests under `src/RMC.BestFit.Tests/Univariate/`.
- Remove: `src/RMC.BestFit.Verification/extract_tests.py`
- Modify: `docs/verification/verification-catalog.json`

**Interfaces:**
- Produces: Bulletin 17C accounting/setter contracts in fast tests and point-process likelihood oracles separated from recovery.

- [x] Extract setter, retry, substitution, completion, and accounting behavior without moving optimizer execution into fast tests.
- [x] Retain annual-maximum and seasonal likelihood calculations in an explicitly named oracle class.
- [x] Convert the 30 historical Cohn DataRows into separately named exact methods sharing one private scientific helper so catalog ownership is unambiguous; keep every coverage method execution-excluded.
- [x] Remove completion-only cells when no source-backed value or coverage assertion remains.
- [x] Run affected fast classes and default catalog validation; do not run any Cohn coverage method. Only separately added non-coverage value-oracle methods are eligible for exact execution when explicitly authorized.

### Chunk 4B: Clean Composite and RatingCurve ownership

**Files:**
- Modify ownership candidates under `Univariate/CompositeTests/` and `RatingCurve/`.
- Modify or create matching fast tests under the corresponding `src/RMC.BestFit.Tests/` directories.
- Modify: `docs/verification/verification-catalog.json`

**Interfaces:**
- Produces: analytical Composite and RatingCurve evidence clearly separated from state/default-bound contracts.

- [x] Rename Composite analytical/resampling cells so `Recovery` denotes generated-data recovery only.
- [x] Retain analytical composition and power-law continuity in Verification.
- [x] Move default-bound, setter, and deterministic result-state behavior to small inline-fixture fast tests.
- [x] Run affected fast classes, then all three fast projects serially.
- [x] Run default catalog validation and confirm the moved methods no longer resolve in Verification.

### Chunk 4C: Clean spatial ownership

**Files:**
- Modify: `SpatialGEVCrossValidationVerificationTests.cs`, `SpatialGEVUncertaintyMethodVerificationTests.cs`, and `SpatialGEVPredictionVerificationTests.cs`.
- Modify or create matching fast tests under `src/RMC.BestFit.Tests/SpatialExtremes/`.
- Modify: `docs/verification/verification-catalog.json`

**Interfaces:**
- Produces: spatial status, dispatch, accounting, and same-path aggregation contracts in fast tests while preserving independent numerical cells.

- [x] Identify the production regression each proposed fast test catches and reuse existing coverage when present.
- [x] Move result retention, selected-method, fold accounting, failure status, and same-path aggregation contracts without running estimation fast.
- [x] Retain or mark open every independent kriging, Gaussian-process, covariance, or numerical-integration claim.
- [x] Run affected fast classes and all three fast projects serially.
- [x] Run default catalog validation and leave missing independent spatial evidence `Open` for Chunks 14A and 14B.

### Chunk 5: Normalize estimator recovery infrastructure

**Files:**
- Create: `src/RMC.BestFit.Verification/Recovery/RecoveryDesign.cs`
- Create: `src/RMC.BestFit.Verification/Recovery/RecoveryAcceptance.cs`
- Modify estimator recovery sources under: `src/RMC.BestFit.Verification/ModelEstimation/`
- Modify: `docs/verification/verification-catalog.json`
- Modify: `docs/verification/methodology.md`

**Interfaces:**
- Produces: `RecoveryDesign.SampleSize = 1000`; typed sample-unit descriptions; frequentist interval/standardized-error assertions; Bayesian interval, R-hat, and ESS assertions; response-grid assertions for weakly identified models.

- [x] Add the test helpers without production dependencies or direct unit tests in Verification; prove them through the first scientific recovery cell that fails under the old N/design and passes under the approved design.
- [x] Convert Bayesian, MAP, GMM, Profile-Q, and generic MLE recovery to N=1000.
- [x] Split analytical AIC/BIC and conjugate-posterior claims from recovery-named sources.
- [x] Predeclare seeds, parent parameters, standard-error source, interval width, and secondary 5% applicability in XML remarks and catalog entries.
- [x] Run only exact estimator Verification methods explicitly authorized for this chunk.

### Chunk 6A: Complete DistributionFitting verification

**Files:**
- Modify verification sources under `DistributionFitting/`.
- Modify fitting-specific synthetic datasets only where a missing N=1000 generator is required.
- Modify the fitting catalog entries and report section.

**Interfaces:**
- Produces: theoretical/external evidence plus N=1000 end-to-end recovery for all 15 candidate families.

- [x] Add the failing N=1000 recovery cell before each missing family path.
- [x] Judge the true family through recovered parameters and predeclared quantiles; do not require it to win model selection.
- [x] Record seeds, distribution parameterization, fit method, interval/standard-error source, and acceptance in XML and catalog entries.
- [x] Run each authorized exact method separately and update results only from reviewed TRX evidence.

### Chunk 6B: Complete Univariate family and trend verification

**Files:**
- Modify: `Univariate/ValidationTests/UnivariateValidationTests.cs`, `NonstationaryValidationTests.cs`, and their focused dataset helpers.
- Modify MLE recovery sources for missing family cells.
- Modify Univariate catalog and report entries.

**Interfaces:**
- Produces: N=1000 Univariate recovery for all 15 families and theory/recovery for every trend including `ReciprocalTrend`.

- [x] Raise the family recovery count from N=300 to N=1000 and use shared recovery acceptance.
- [x] Add LogNormal and KappaFour MLE recovery if absent after current-source reconciliation.
- [x] Add independently calculated ReciprocalTrend values/derivatives and N=1000 nonstationary recovery.
- [x] Apply response-space checks where trend coefficients are near zero or weakly identified.
- [x] Run each authorized exact method separately and reconcile catalog/report evidence.

### Chunk 7: Complete Bulletin 17C verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/`.
- Add frozen PeakFQ or published Cohn artifacts under `verification/data/bulletin17c/` with generator/version metadata.
- Modify Bulletin 17C catalog and report sections.

**Interfaces:**
- Produces: six N=1000 family recovery cells and source-backed value assertions replacing completion-only claims; coverage studies remain execution-excluded history.

- [x] Establish the exact family/parameterization crosswalk before generating any external artifact.
- [x] Add N=1000 family recovery with the common acceptance rule.
- [x] Add published or PeakFQ expected values and manifest hashes without executing any Bulletin 17C confidence-interval coverage method.
- [x] Preserve the three B17C confidence-interval coverage classes and their historical results as execution-excluded reruns-on-request, distinct from recovery N.
- [x] Run only approved exact non-coverage Bulletin 17C value/recovery methods and record failures as findings rather than tuning production or tolerances; do not run any of the three B17C coverage classes.

### Chunk 8: Complete point-process verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/Univariate/PointProcessTests/`.
- Add compatible external artifacts under `verification/data/point-process/`.
- Modify catalog and report sections.

**Interfaces:**
- Produces: separate theory, external-compatible, and N=1000 recovery evidence for stationary and seasonal point-process behavior.

- [x] Keep analytical likelihood and block-origin evidence distinct from recovery.
- [x] Establish the extRemes or alternative-package parameterization/threshold/exposure crosswalk before comparison.
- [x] Convert retained recovery fixtures to the shared N=1000 design.
- [x] Reclassify or replace prior-range sampling characterization so it is not presented as estimator recovery.
- [x] Run authorized exact methods and update the report from TRX evidence.

### Chunk 9: Complete competing-risk verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/`.
- Modify catalog and report sections.

**Interfaces:**
- Produces: five recovery cells over three N=1000 cause-balanced dog-leg designs, with full-likelihood coordinate uncertainty and explicit fixed-correlation Bayesian exclusion.

- [x] Thin the oversized estimator cross-product and predeclare theoretical shares, hard wins, soft likelihood responsibilities, dominance mass, and dog-leg crossovers for every retained fixture.
- [x] Apply parameter recovery only after cause-balance and coordinate-identification evidence; order same-family Weibull coordinates by increasing shape.
- [x] Exclude Bayesian MCMC from the numerically expensive correlated design and retain matching BestFit/Numerics MLE fixtures as a cross-implementation diagnostic.
- [x] Keep 20,000/40,000-draw simulation-calibration designs distinct from recovery N and retain their theoretical Monte Carlo tolerances.
- [x] Preserve the independent maximum MLE optimizer finding as open without tuning a fixture, tolerance, likelihood, optimizer, or initialization.

### Chunk 10A: Complete Mixture verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/Univariate/MixtureTests/`.
- Add frozen independent mixture artifacts under `verification/data/mixture/`.
- Modify Mixture catalog and report entries.

**Interfaces:**
- Produces: independent Normal-mixture package parity and label-identified N=1000 recovery.

- [x] Define ordered component parameters and predictive CDF/quantile checks before running recovery.
- [x] Generate and hash an external mixture artifact using an exact likelihood and parameterization overlap.
- [x] Keep zero-inflated and non-package-compatible designs on independently implemented likelihood or response oracles.
- [x] Run approved exact methods and update claims only from reviewed evidence.

### Chunk 10B: Complete Composite verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/Univariate/CompositeTests/`.
- Modify Composite catalog and report entries.

**Interfaces:**
- Produces: analytical composition/resampling evidence plus N=1000 end-to-end predictive recovery.

- [x] Generate 1000 observations per parent component, fit each child analysis, construct the CompositeAnalysis, and compare the output curve with the analytical parent composite.
- [x] Cover mixture, competing-risk maximum/minimum, and equal-weight model averaging with identified predictive quantities.
- [x] Keep analytical formulas and posterior-resampling parity categorized as theory/independent evidence, not recovery.
- [x] Run approved exact methods and update claims only from reviewed TRX evidence.

### Chunk 11A: Complete Bivariate verification

**Files:**
- Modify BivariateAnalysis and copula recovery/oracle sources under `src/RMC.BestFit.Verification/Bivariate/`.
- Update frozen copula artifacts under `verification/data/bivariate/`.
- Modify Bivariate catalog and report entries.

**Interfaces:**
- Produces: N=1000 recovery for all seven copulas and Student-t external parity.

- [x] Change the synthetic bivariate default from 100 paired observations to 1000.
- [x] Add Student-t MPL/IFM external targets with correlation and degrees-of-freedom parameterization documented.
- [x] Replace observed-delta tolerances with interval/standardized-error and predictive tail-dependence checks.
- [x] Run approved exact methods and update artifacts, hashes, catalog, and report.

### Chunk 11B: Complete CoincidentFrequency verification

**Files:**
- Modify CoincidentFrequency sources under `src/RMC.BestFit.Verification/Bivariate/`.
- Add a frozen nonlinear response artifact if independent quadrature is generated out of process.
- Modify CoincidentFrequency catalog and report entries.

**Interfaces:**
- Produces: retained closed-form Normal-sum evidence plus N=1000 nonlinear response recovery.

- [x] Define a monotone nonlinear response function and its independent two-dimensional numerical integration or Monte Carlo oracle.
- [x] Generate exactly 1000 paired parent observations for the recovery path.
- [x] Predeclare the probability/quantile grid and integration or Monte Carlo error bound.
- [x] Run approved exact methods and update artifacts, catalog, and report.

### Chunk 12: Normalize rating-curve verification

**Files:**
- Modify sources under `src/RMC.BestFit.Verification/RatingCurve/`.
- Update compatible external artifacts under `verification/data/rating-curve/`.
- Modify catalog and report sections.

**Interfaces:**
- Produces: consistent N=1000 MLE/Bayesian recovery and source-specific external parity separated from recovery acceptance.

- [x] Predeclare uncertainty and curve grids for standard, segmented, and error-model configurations.
- [x] Replace parameter bands ranging from 5% to 50% with the common interval/standardized-error rule and conditional 5% reporting.
- [x] Retain tighter deterministic independent-optimum parity as external evidence, not recovery tolerance.
- [x] Use bdrc or another package only where the rating equation and residual model exactly match; otherwise document incompatibility.
- [x] Run approved exact cells and reconcile XML comments with executable thresholds.

**Chunk 12 checkpoint (31 August 2026):** The pre-run matrix consolidated ten redundant declarations
and retained five fixtures for each estimator. Haden Smith identified that response recovery must include
the declared log10 residual; the superseded pointwise checks had propagated parameter uncertainty only and
also treated six or nine pointwise intervals as one 95% statement. MLE now propagates 20,000 bounded
observed-information draws plus independent draw-specific log10 residuals into a simultaneous max-|t| 95%
predictive band. Bayesian recovery uses posterior MAP, retained posterior draws, independent draw-specific
log10 residuals, and the analogous simultaneous posterior-predictive band. Exact segmented allocations are
495/505 and 270/406/324 rather than nominal N=1000 per control. All ten exact recovery identities pass with
one result each. No parent, generator seed, prior, sampler, response grid, optimizer default, convergence
rule, or fitted realization was tuned. Chunk 13A implementation was not started.

**Whole-library estimator audit:** 149 direct `MaximumLikelihood`/`MaximumAPosteriori` constructions
were reconciled. The 144 non-profile constructions use Differential Evolution with untouched default
tolerances; the five intentional profile-likelihood constructions retain BFGS because Brent/BFGS are
part of those profile oracles. Three current exact methods remain open under the required defaults:
two existing Log10-Normal analytical MAP/GMM cells and the AR(1) MLE recovery cell. The AR(1) MLE
identity was the sole later-chunk execution under the whole-library audit; no Chunk 13A recovery design
or remediation was begun.

### Chunk 13A: Normalize AR and MA verification

**Files:**
- Modify AR/MA sources and shared assertions under `src/RMC.BestFit.Verification/TimeSeriesAnalysis/`.
- Update AR/MA R fixture metadata and catalog/report entries.

**Interfaces:**
- Produces: canonical N=1000 recovery and external parity for `ARAnalysis` and `MAAnalysis`.

- [ ] Preserve exactly 1000 post-burn-in observations in every AR/MA recovery fixture.
- [ ] Apply central 95% posterior intervals, R-hat below 1.10, ESS at least 100, and the frequentist interval/standardized-error rule.
- [ ] Document coefficient sign, intercept, and innovation-scale crosswalks for R comparisons.
- [ ] Run approved exact methods and update the two analysis entries independently.

### Chunk 13B: Normalize ARIMA and ARIMAX verification

**Files:**
- Modify ARIMA/ARIMAX sources and shared assertions under `src/RMC.BestFit.Verification/TimeSeriesAnalysis/`.
- Update ARIMA/ARIMAX R fixture metadata and catalog/report entries.

**Interfaces:**
- Produces: canonical N=1000 recovery and external parity for `ARIMAAnalysis` and `ARIMAXAnalysis`.

- [ ] Preserve exactly 1000 post-burn-in observations in every ARIMA/ARIMAX recovery fixture.
- [ ] Apply central 95% posterior intervals, R-hat below 1.10, ESS at least 100, and the frequentist interval/standardized-error rule.
- [ ] Document differencing, coefficient sign, intercept/drift, innovation scale, transformed-scale anchoring, and xreg alignment.
- [ ] Run approved exact methods and update the two analysis entries independently.

### Chunk 14A: Complete spatial correlation and cross-validation oracles

**Files:**
- Modify spatial correlation and cross-validation Verification sources.
- Add or update fold/correlation artifacts under `verification/data/spatial-extremes/`.
- Modify spatial catalog and report entries.

**Interfaces:**
- Produces: explicit analytical evidence for Basic, Powered Exponential, and Spherical correlations plus independent held-out fold results.

- [ ] Generate independent reduced-fold predictions without calling the production reduction path for expected values.
- [ ] Add analytical covariance values for all three correlation functions and an external Gaussian-copula comparison where parameterizations overlap.
- [ ] Predeclare held-out sites, training dimensions, coordinate metric, tolerance, and artifact hashes.
- [ ] Run fast gates and only authorized exact spatial oracle methods.

### Chunk 14B: Complete spatial prediction and uncertainty oracles

**Files:**
- Modify spatial prediction and uncertainty Verification sources.
- Add or update GP/Godambe/bootstrap/VIF artifacts under `verification/data/spatial-extremes/`.
- Modify spatial catalog and report entries.

**Interfaces:**
- Produces: independent conditional-GP prediction, Godambe H/J covariance, temporal block-bootstrap quantiles, and analytic VIF transformation evidence.

- [ ] Generate independent prediction and uncertainty targets without reusing production aggregation code.
- [ ] Record package/runtime versions, input data, random seeds, block definition, formulas, tolerance rationale, and SHA-256 hashes.
- [ ] Keep dispatch and result-state behavior in fast tests from Chunk 4C.
- [ ] Run only authorized exact spatial oracle methods.

### Chunk 15: Normalize spatial recovery

**Files:**
- Modify: `src/RMC.BestFit.Verification/SpatialExtremes/SpatialGEVMLERecoveryTests.cs`
- Modify: `src/RMC.BestFit.Verification/SpatialExtremes/SpatialGEVBayesianRecoveryTests.cs`
- Modify spatial synthetic data helpers, catalog, and report sections.

**Interfaces:**
- Produces: every spatial recovery fixture with 1000 row/year vectors across the complete site network.

- [ ] Change only the generated observation count and test acceptance first; do not alter production sampler defaults or parameter bounds.
- [ ] Apply scalar recovery only to identified trend/correlation coordinates and use site quantiles/regional curves for weak latent components.
- [ ] Require the common Bayesian diagnostics on every monitored coordinate.
- [ ] If runtime or convergence exposes a finding, preserve the failing fixture and request technical direction rather than reducing N or loosening the rule.
- [ ] Run each authorized exact recovery method separately and record elapsed time and TRX provenance.

### Chunk 16A1: Rewrite ModelEstimation XML/comments

**Files:**
- Modify touched sources under `ModelEstimation/` and their catalog descriptions.

**Interfaces:**
- Produces: professional scientific documentation for the current ModelEstimation declarations.

- [ ] Replace tautological summaries/boilerplate with claim, oracle, sample/source design, compared quantity, acceptance rationale, and limitations.
- [ ] Remove mechanical comments and reconcile every stated tolerance with code.
- [ ] Correct test/private spelling without renaming compatibility-sensitive production members.
- [ ] Run the strict XML/private-method gate and `git diff --check` for this directory.

### Chunk 16A2: Rewrite DistributionFitting XML/comments

**Files:**
- Modify touched sources under `DistributionFitting/` and their catalog descriptions.

**Interfaces:**
- Produces: professional family-specific documentation for DistributionFitting.

- [ ] Document family parameterization, source artifact or generator, N/seed where applicable, compared statistics, and acceptance rationale.
- [ ] Remove repeated generic fitting prose and mechanical comments.
- [ ] Run the strict XML/private-method gate and `git diff --check` for this directory.

### Chunk 16A3: Rewrite Univariate validation/report XML/comments

**Files:**
- Modify touched sources under `Univariate/ValidationTests/` and `Univariate/VerificationReportTests/`.

**Interfaces:**
- Produces: professional distribution/trend and report-calculation documentation.

- [ ] Document each recovery generator, N/seed, parameter ordering, oracle, diagnostics, and response-space limitation.
- [ ] Distinguish scientific verification from report calculation and remove mechanical comments.
- [ ] Run the strict XML/private-method gate and `git diff --check` for these directories.

### Chunk 16A4: Rewrite Bulletin 17C XML/comments

**Files:**
- Modify touched sources under `Univariate/Bulletin17CTests/` and their catalog descriptions.

**Interfaces:**
- Produces: professional B17C worked-example, recovery, bootstrap, and historical-coverage documentation.

- [ ] Document source table/software, family, record design, seed, oracle, tolerance, and evidence boundary for every retained method.
- [ ] Mark all three B17C confidence-interval coverage classes execution-excluded in class/method remarks without changing historical results.
- [ ] Remove completion-only language and mechanical comments.
- [ ] Run the strict XML/private-method gate and `git diff --check` for this directory.

### Chunk 16A5: Rewrite point, competing, mixture, and composite XML/comments

**Files:**
- Modify touched sources in the four corresponding Univariate subdirectories.

**Interfaces:**
- Produces: professional likelihood, recovery, simulation, mixture-identification, and composition documentation.

- [ ] State oracle provenance, sample unit/N/seed, label/identifiability convention, compared predictive quantity, and tolerance rationale.
- [ ] Ensure theory/resampling cells are not described as recovery.
- [ ] Run the strict XML/private-method gate and `git diff --check` for these directories.

### Chunk 16B1: Rewrite Bivariate and RatingCurve XML/comments

**Files:**
- Modify touched sources under `Bivariate/` and `RatingCurve/`.

**Interfaces:**
- Produces: professional copula, coincident-response, and rating-curve documentation.

- [ ] Document parameterization crosswalks, N/seed, external artifact, response grids, and acceptance rationale.
- [ ] Reconcile every stated tolerance with executable assertions and remove default-bound language from scientific claims.
- [ ] Run the strict XML/private-method gate and `git diff --check` for these directories.

### Chunk 16B2: Rewrite TimeSeriesAnalysis XML/comments

**Files:**
- Modify touched sources under `TimeSeriesAnalysis/`.

**Interfaces:**
- Produces: professional AR/MA/ARIMA/ARIMAX generator, R-crosswalk, recovery, and diagnostics documentation.

- [ ] Document order, sign, differencing, transform, innovation scale, xreg alignment, N/seed, oracle, and acceptance for every retained method.
- [ ] Replace duplicated legacy boilerplate with model-specific rationale.
- [ ] Run the strict XML/private-method gate and `git diff --check` for this directory.

### Chunk 16B3: Rewrite SpatialExtremes XML/comments

**Files:**
- Modify touched sources under `SpatialExtremes/`.

**Interfaces:**
- Produces: professional correlation, likelihood, CV, prediction, uncertainty, simulation, and recovery documentation.

- [ ] Document row/year and site dimensions, coordinate metric, correlation/link model, oracle provenance, seed, and acceptance rationale.
- [ ] Remove same-path claims and mechanical dispatch/accounting language from retained scientific methods.
- [ ] Run the strict XML/private-method gate and `git diff --check` for this directory.

### Chunk 16C1: Reconcile estimation, fitting, univariate, and B17C report sections

**Files:**
- Modify report chapters/inventories, catalog anchors, and manifests for these four areas.

**Interfaces:**
- Produces: four explicit sections aligned to current code, artifacts, and focused results.

- [ ] Give each area claim/model, theory, external/published evidence, N=1000 recovery, results, limitations, and provenance subsections.
- [ ] Preserve B17C confidence-interval coverage only as execution-excluded historical evidence.
- [ ] Remove stale counts/passed claims lacking a focused TRX or artifact record and validate links/hashes.

### Chunk 16C2: Reconcile point, competing, mixture, and composite report sections

**Files:**
- Modify report chapters/inventories, catalog anchors, and manifests for these four analyses.

**Interfaces:**
- Produces: four explicit sections aligned to current code, artifacts, and focused results.

- [ ] Give each analysis claim/model, theory, external evidence, N=1000 recovery, results, limitations, and provenance subsections.
- [ ] Separate verified claims from deferred research, accepted limitations, and reruns-on-request.
- [ ] Validate links, hashes, report anchors, and default catalog mode.

### Chunk 16D1: Reconcile bivariate, coincident-frequency, and rating report sections

**Files:**
- Modify report chapters/inventories, catalog anchors, and manifests for these three analyses.

**Interfaces:**
- Produces: three explicit sections aligned to current copula/response/rating evidence.

- [ ] Give each analysis claim/model, theory, external evidence, N=1000 recovery, results, limitations, and provenance subsections.
- [ ] Remove stale counts and same-path/regression overclaims.
- [ ] Validate links, hashes, report anchors, and default catalog mode.

### Chunk 16D2: Reconcile AR, MA, ARIMA, and ARIMAX report sections

**Files:**
- Modify time-series report chapters/inventories, catalog anchors, and manifests.

**Interfaces:**
- Produces: four separately headed analysis sections.

- [ ] Give each model its own theory, R crosswalk, N=1000 recovery, diagnostics, results, limitations, and provenance subsections.
- [ ] Remove grouped wording that hides model-specific evidence boundaries.
- [ ] Validate links, hashes, report anchors, and default catalog mode.

### Chunk 16D3: Reconcile SpatialGEV report and book order

**Files:**
- Modify spatial report chapters/inventory, catalog anchors, and manifests.
- Modify: `docs/verification/book-order.txt`

**Interfaces:**
- Produces: one complete SpatialGEV section and corrected report assembly order.

- [ ] Reconcile correlation, likelihood, CV, prediction, uncertainty, simulation, and N=1000 recovery evidence.
- [ ] Add `report/estimation-diagnostics.md` to the real book-order path.
- [ ] Validate links, hashes, report anchors, and default catalog mode.

### Chunk 16E: Run final strict gates and inspect report artifacts

**Files:**
- Modify final status/summary tables only when supported by the accumulated evidence ledger.
- Modify: `docs/verification/verification-catalog.json` so strict mode contains no unresolved gap except explicitly approved limitations or execution-excluded Bulletin 17C confidence-interval coverage studies.

**Interfaces:**
- Produces: final traceability, build/test evidence, and publication artifact QA.

- [ ] Run strict catalog validation and confirm every declaration and execution unit discovered in the post-remediation source tree resolves; do not hard-code the original 506-declaration count after moves and DataRow conversion.
- [ ] Run strict XML validation, all three fast test projects serially, and public API compatibility.
- [ ] Confirm every newly claimed executable Verification result has an explicitly authorized one-method TRX; do not execute the three Bulletin 17C confidence-interval coverage classes or the full suite.
- [ ] Rebuild the Verification report, inspect headings/TOC/links, render the PDF, and perform visual and structural QA.
- [ ] Run `git diff --check`, inspect explicit staged paths if staging is authorized, and present unresolved defects separately from accepted limitations, execution-excluded Bulletin 17C confidence-interval coverage studies, and reruns-on-request.

## Progress convention

At the end of each session, check completed boxes only when code, oracle, artifact, documentation, and required test evidence for that box exist. Add a dated checkpoint below this section containing the commit or uncommitted-diff base, exact commands run, results, unresolved findings, and the next unchecked step. Historical passed claims are not rewritten without new focused evidence.

## Session checkpoints

### 28 August 2026 — Chunk 1 complete

- **Base:** `df8ccf4` on `documentation-verification-updates`; changes remain uncommitted.
- **Inventory:** 99 Verification C# files, 79 test-bearing files, 505 `[TestMethod]` declarations, one `[DataTestMethod]` declaration with 30 rows, 535 execution units, and 15 scientific analysis types.
- **Validation:** local Markdown links passed; tracked `git diff --check` and a direct whitespace scan of the two new Markdown files passed; the plan placeholder scan returned no matches; `scripts/validate-code-xml-docs.ps1 -SkipBuild` passed with 921 source files scanned.
- **Verification execution:** none; no focused or full Verification test was run.
- **Ruling:** retain `JointPriorSamplingVerificationTests` as analytical evidence for the accepted independent-marginal limitation, but split or remove its same-production-path fitness equality during Chunk 3.
- **Ruling:** the confidence-interval coverage methods in `B17CCoverageTests`, `B17CCensoredCoverageTests`, and `B17CCohnEtAlCoverageTests` are execution-excluded; retain their historical evidence as reruns-on-request. Other exact Verification methods remain approval-gated.
- **Next:** Chunk 2A begins with failing fixture tests for the catalog validator and schema; Chunks 2B-2D populate the current-method catalog in non-strict audit mode.

### 28 August 2026 — Chunk 2A complete

- **Base:** work began from `df8ccf4` on `documentation-verification-updates`; a concurrent external commit advanced `HEAD` to `d29f87a` during validation. This session did not stage or commit anything. Chunk 1 and Chunk 2A changes remain uncommitted on the `d29f87a` base.
- **TDD:** `.\scripts\test-verification-catalog-validator.ps1` first exited 1 because `validate-verification-catalog.ps1` did not exist. Additional pre-fix pressure and fixture runs exposed and pinned CRLF `[DataRow]` discovery, scalar `evidenceTags`, nested-helper class capture, scoped-root resolution, partial test classes, schema coercion/shape, and process-message defects. The final harness covers 21 cases, including all eight required invalid fixtures, default-versus-strict gaps, multiple evidence tags, nested/multiple/partial class identities, scoped source validation, recursive schema enforcement, and exact output prefixes/final lines.
- **Implementation:** `verification-catalog.schema.json` defines method identity, scientific claim, evidence tags, disposition, recovery design, oracle, artifact, report trace, status, gap, and named data-row override contracts. `validate-verification-catalog.ps1` recursively enforces that schema without PowerShell coercion, performs brace-aware two-pass method/class discovery across partial classes, resolves repository-relative paths from full or scoped source roots, requires recovery N=1000, verifies Markdown anchors, reports open gaps by default, and rejects them with `-RequireComplete`.
- **Validation:** the fixture harness passed 21/21 under PowerShell 7.6.4 and Windows PowerShell 5.1. Production discovery found 506 method declarations and 535 execution units with zero discovery errors and zero assignments to the two previously misidentified nested helper classes; a full catalog entry passed `Test-Json` against the schema. Both scripts parsed with zero PowerShell syntax errors; help rendered the exact exit/message contract; `scripts/validate-code-xml-docs.ps1 -SkipBuild` passed with 921 source files scanned; tracked `git diff --check`, the direct new-file whitespace scan, and the placeholder/debug scan passed. PSScriptAnalyzer was not installed, so that optional static check was unavailable.
- **Commands:** `.\scripts\test-verification-catalog-validator.ps1`; `& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoLogo -NoProfile -ExecutionPolicy Bypass -File '.\scripts\test-verification-catalog-validator.ps1'`; `.\scripts\validate-code-xml-docs.ps1 -SkipBuild`; `Get-Help .\scripts\validate-verification-catalog.ps1 -Full`; `$json = '{"schemaVersion":1,"entries":[]}'; Test-Json -Json $json -SchemaFile '.\docs\verification\verification-catalog.schema.json' -ErrorAction Stop`; `git diff --check`. Parser validation used `[System.Management.Automation.Language.Parser]::ParseFile` on both scripts with a shared error list. The read-only production pressure command extracted `Get-CSharpMatchingBraceIndex` and `Get-DiscoveredMethods` from the validator AST, invoked them on `src\RMC.BestFit.Verification`, and asserted 506 declarations, 535 execution units, zero discovery errors, and zero `TestGMMModelNoPointwise`/`SimpleNormalModel` assignments. The direct whitespace command read every line of all untracked Chunk 1/2A files with `[System.IO.File]::ReadLines`, rejected `[ \t]+$`, and required a final newline.
- **Verification execution:** none. The full Verification suite and the confidence-interval coverage classes `B17CCoverageTests`, `B17CCensoredCoverageTests`, and `B17CCohnEtAlCoverageTests` were not run.
- **Open scope:** `docs/verification/verification-catalog.json` intentionally does not exist until Chunk 2B1, so no claim is made that the current source inventory is catalog-complete.
- **Next:** Chunk 2B1 creates the catalog and classifies the current 65 `ModelEstimation` declarations.

### 28 August 2026 — Chunk 2B1 complete

- **Base:** `3276b8b` on `documentation-verification-updates`; this session did not stage, commit, push, discard, move, rename, or delete anything. All prior Chunk 1 and Chunk 2A work remains uncommitted.
- **Catalog:** `verification-catalog.json` records all 65 current `ModelEstimation` `[TestMethod]` declarations and 65 execution units. The scope contains no `[DataTestMethod]` declarations, so all 65 `methodOverrides` arrays are empty. The method-level statuses are 32 `verified`, 31 `open`, and two `accepted-limitation` entries.
- **Ownership:** smoke, finiteness, same-production-path, qualitative, engineering, and mixed-contract cells remain `open` with exact Chunk 3 dispositions. Twelve genuine N=1000 MLE family-recovery cells are identified but remain open for common acceptance normalization; the LnNormal method is recorded as a loose same-sample analytical comparison rather than parent recovery. The two accepted limitations are the `JointPrior` independent-marginal characterization, with its same-path fitness assertion split for Chunk 3, and the current GMM deletion-influence magnitude characterization.
- **Validator correction:** the first production catalog run exposed PowerShell pipeline unwrapping of one-element `evidenceTags` arrays. A failing single-tag fixture reproduced it; `Get-EffectiveValue` now preserves array identity, and the harness passes 22/22 under PowerShell 7.6.4 and Windows PowerShell 5.1.
- **Validation:** direct JSON Schema validation passed; all 65 identities are unique; report anchors resolve; both PowerShell scripts parse with zero syntax errors; direct whitespace validation passed. Default scoped validation passed with 31 reported open gaps and exactly 65 discovered declarations / 65 execution units. Strict completeness mode was not run.
- **Commands:** `.\scripts\test-verification-catalog-validator.ps1`; `& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoLogo -NoProfile -ExecutionPolicy Bypass -File '.\scripts\test-verification-catalog-validator.ps1'`; `Test-Json -Json $json -SchemaFile '.\docs\verification\verification-catalog.schema.json'`; `.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification\ModelEstimation`; `git diff --check`. Parser and direct-whitespace checks used read-only PowerShell APIs.
- **Verification execution:** none. No Verification test method, Verification suite, or Bulletin 17C confidence-interval coverage class was run.
- **Next:** Chunk 2B2 appends the current `DistributionFitting` declarations when separately authorized; it was not started in this session.

### 28 August 2026 — Chunk 2 catalog baseline complete

- **Base:** `73cf9beb34a7b2d8ac16edb68c93474425280337` on `documentation-verification-updates`; this session did not stage, commit, push, discard, move, rename, or delete any repository work. Existing Chunk 1 and Chunk 2A/2B1 changes remain uncommitted.
- **Catalog:** all 506 current declarations resolve to 535 execution units. Declaration statuses are 226 `verified`, 251 `open`, two `accepted-limitation`, and 27 `execution-excluded`. The scoped declaration counts are DistributionFitting 45; Univariate validation/report 60; Bulletin 17C 88 declarations / 117 units; PointProcess/CompetingRisk/Mixture/Composite 53; Bivariate/RatingCurve 69; TimeSeriesAnalysis 96; and SpatialExtremes 30. No root-level Verification C# file contains a test declaration.
- **Ownership:** nonqualifying smoke, finiteness/shape, same-production-path, qualitative, engineering-contract, state, and deterministic-contract claims are open with their exact future disposition. Recovery entries record their current observational unit, N, and seed; nonconforming N is an open gap rather than a test change. The 30 Cohn rows have named overrides, and all three Bulletin 17C confidence-interval coverage classes remain execution-excluded historical evidence.
- **Validator correction:** default audit mode originally rejected accurately cataloged open recovery designs with N other than 1000. The recovery-size rule now remains an error for non-open entries while open nonconformance is reported as a gap; the new fixture proves default mode accepts that ledger state and strict mode still rejects it through `OPEN_GAP`.
- **Methodology:** the catalog field contract, primary-versus-multi-evidence semantics, status meanings, accepted-limitation boundary, default/strict behavior, spatial row/year recovery unit, named-row inheritance, and B17C execution exclusions are documented in `docs/verification/methodology.md`.
- **Validation:** catalog fixtures passed 22/22 under PowerShell 7.6.4 and Windows PowerShell 5.1.26100.9168. JSON Schema validation and both PowerShell parser checks passed. Final default repository validation passed with 251 reported open gaps and exactly 506 declarations / 535 execution units. Direct dirty-file whitespace and tracked `git diff --check` passed.
- **Commands:** `.\scripts\test-verification-catalog-validator.ps1`; `& 'C:\Windows\System32\WindowsPowerShell\v1.0\powershell.exe' -NoLogo -NoProfile -ExecutionPolicy Bypass -File '.\scripts\test-verification-catalog-validator.ps1'`; `Test-Json -Json $catalogJson -SchemaFile '.\docs\verification\verification-catalog.schema.json'`; `.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification`; scoped default validator runs used catalog copies filtered to each requested source root; parser validation used `[System.Management.Automation.Language.Parser]::ParseFile`; `git diff --check` and a direct dirty-file whitespace/final-newline scan completed the structural gate.
- **Verification execution:** none. No Verification test method, full Verification suite, or Bulletin 17C confidence-interval coverage class was run. Strict catalog completeness was not run because Chunk 2 intentionally records future remediation gaps.
- **Next:** Chunk 3 is the first unchecked implementation chunk. It must use these catalog dispositions and must not infer authorization from this catalog-only baseline.

### 28 August 2026 — Chunk 3 complete

- **Base and preservation:** `73cf9beb34a7b2d8ac16edb68c93474425280337` on `documentation-verification-updates`; this session did not create a branch/worktree or stage, commit, push, discard, revert, or overwrite prior Chunk 1/2 work. No production-library file changed.
- **Ownership:** retained the 12 N=1000 MLE generating-family methods and renamed the thirteenth relevant LnNormal cell as an analytical same-sample closed-form MLE comparison. Removed three MLE smoke/same-path declarations and all four declarations plus now-empty helpers in `GeneralizedMethodOfMomentsRecoveryTests.cs`; the four R-backed GMM specification cells and two independent objective-gradient cells remain. Genuine N=1000 GMM recovery remains assigned to Chunk 5.
- **Split evidence:** the two profile methods now compare only their 10 supported grid ordinates with independently derived correlated-quadratic MLE and flat-prior MAP profiles. NaN placement and confidence-interval throw assertions were removed; no fast replacement was added because there is no public estimator-free seam, and no production extraction was justified. JointPrior retains only independent-marginal analytical moments as an accepted limitation; the same-production-path fitness equality moved to a fast deterministic full-prior sign/inclusion contract.
- **Bayesian evidence:** both Normal recovery fixtures now use 1,000 scalar observations with the established data seed. The interval cell asserts actual generating-parameter inclusion under the unchanged default interval. The qualitative informative-prior ordering was replaced by an independently calculated known-scale Normal-Normal posterior mean, standard deviation, and central interval while retaining the Normal(120,5) prior and default sampler/seed. Chunk 5 still owns central-95%, R-hat, and ESS normalization.
- **Fast-test protection:** repository search found no equivalent protection for prior-sample `ParameterSet.Fitness`. `PriorPredictiveSamplingContractTests.SampleFromPriors_StoresNegativeFullModelPriorLogLikelihood` catches a wrong sign or marginal-only substitution through an inline constant-joint-prior model and invokes no optimizer, estimator, or MCMC sampler. It was written before the Verification equality was removed. The first authored run exposed test-only namespace/API compile errors; after correction, the focused executable filter passed 1/1 against the unchanged production behavior, so no behavioral red or production seam was applicable.
- **Catalog:** deleted entries for the seven removed declarations and updated every renamed/split identity, claim, oracle, acceptance rule, disposition, source, execution flag, report anchor, status, and gap. Default cumulative validation passed with 244 open gaps, 499 declarations, and 528 execution units. Declaration statuses are 226 verified, 244 open, two accepted limitations, and 27 execution-excluded; ModelEstimation contains 58 declarations (32 verified, 24 open, two accepted limitations).
- **Validation:** focused fast executable filter passed 1/1; `dotnet test .\src\RMC.BestFit.Tests -c Debug` passed 3,361/3,361; `dotnet build .\src\RMC.BestFit.Verification -c Debug` succeeded with zero warnings/errors; `scripts\validate-code-xml-docs.ps1 -SkipBuild` passed with 921 source files; the catalog validator harness passed 22/22; direct JSON/schema validation passed; cumulative default catalog validation passed; `git diff --check` passed. Final status/diff inspection found only preserved Chunk 1/2 files and the authorized Chunk 3 Verification, fast-test, catalog, inventory, and checkpoint changes.
- **Verification execution:** none. No Verification method, full Verification suite, or Bulletin 17C confidence-interval coverage class was run. Renamed/new numerical cells remain open until an exact approval-gated focused run supplies new result evidence; historical passed claims were not rewritten as current executions.
- **Next:** Chunk 4A is the first unchecked implementation chunk. It was not started here.

### 29 August 2026 — Chunk 4A complete

- **Base and preservation:** `73cf9beb34a7b2d8ac16edb68c93474425280337` on `documentation-verification-updates`; this session worked directly in the current checkout and did not create a branch/worktree or stage, commit, push, discard, revert, rename, or overwrite prior Chunk 1-3 work. The only production-library change is the approved internal bootstrap-policy seam in `Bulletin17CAnalysis.cs`; no UI or App production file changed.
- **Fast ownership:** removed all 14 completion/accounting methods in `B17CBootstrapRefitReliabilityTests` and both same-parent uncertain-bootstrap stability methods. Two deterministic `ResolveBootstrapReplicate_*` fast tests pin exception containment, bounded retry counts, parent-fit substitution, delivered non-null output, and failure accounting without sampling data or invoking an optimizer, estimator, bootstrap refit, or MCMC sampler. The authored red run failed with `CS0117` before the internal seam existed. Existing fast tests already protected MGBT setter recomputation, B17C pointwise/aggregate identities at fixed valid parameters, bootstrap diagnostic/retained accounting, and seasonal point-process likelihood order invariance, so no duplicates or estimator-backed fast tests were added.
- **Verification ownership:** deleted the low-outlier setter/GMM cell and the optimizer-backed pointwise identity while preserving the published Bulletin 17C and PeakFQ methods. Moved the independently calculated nonseasonal and exposure-weighted seasonal annual-maximum mixed-observation likelihoods into `PointProcessLikelihoodOracleTests`, preserving the `2e-7` tolerance and exact event/censoring counts; deleted the redundant combined order-invariance partial. Both renamed likelihood cells remain `open` until exact approval-gated focused runs provide current evidence. Removed the obsolete unreferenced `extract_tests.py` helper.
- **Historical coverage:** replaced the single 30-row Cohn `DataTestMethod` with 30 ordinary exact methods sharing `RunCohnEtAlCoverage`. Every gamma/Ns/Nh scenario, `B=1000`, nominal coverage `0.90`, master seed `12345`, at-least-800-completions rule, historical remarks, and rerun-on-request policy are preserved. All 30 remain `execution-excluded` and point to the current exact-method inventory section.
- **Catalog and inventory:** removed every deleted declaration, reconciled the moved/renamed methods and the 30 Cohn identities method by method, and added a current ownership section without rewriting dated historical outcomes. Default cumulative validation resolves 509 declarations and 509 execution units: 224 `verified`, 227 `open`, 56 `execution-excluded`, and two `accepted-limitation` entries.
- **Validation:** the focused estimator-free runner filter passed 6/6; `dotnet test .\src\RMC.BestFit.Tests -c Debug` passed 3,363/3,363; `dotnet test .\src\RMC.BestFit.UI.Tests -c Debug --no-build` passed 581/581; `dotnet test .\src\RMC.BestFit.App.Tests -c Debug` passed 443/443. `dotnet build .\src\RMC.BestFit.Verification -c Debug` succeeded with zero warnings/errors. `scripts\validate-code-xml-docs.ps1 -SkipBuild` and the full `-Configuration Debug` gate both passed across 918 source files. The catalog validator harness passed 22/22, JSON Schema validation returned true, and default catalog validation passed with 227 reported open gaps and 509 declarations / 509 execution units. `git diff --check`, the direct untracked-file whitespace/final-newline scan, final source/catalog identity checks, and independent read-only code review all passed; the reviewer reported no Critical, Important, or Minor findings.
- **Commands:** `dotnet exec .\src\RMC.BestFit.Tests\bin\Debug\net10.0\RMC.BestFit.Tests.dll --filter "FullyQualifiedName~ResolveBootstrapReplicate|FullyQualifiedName~Test_SetLowOutliersFromMGBT_RecomputesPlottingPositions|FullyQualifiedName~PointwiseMomentConditions_ColumnMeans_MatchMomentConditionsG|FullyQualifiedName~Test_Seasonal_DataLogLikelihood_IsInvariantToInputOrder" --minimum-expected-tests 6`; `dotnet test .\src\RMC.BestFit.Tests -c Debug`; `dotnet test .\src\RMC.BestFit.UI.Tests -c Debug --no-build`; `dotnet test .\src\RMC.BestFit.App.Tests -c Debug`; `dotnet build .\src\RMC.BestFit.Verification -c Debug`; `.\scripts\validate-code-xml-docs.ps1 -SkipBuild`; `.\scripts\validate-code-xml-docs.ps1 -Configuration Debug`; `.\scripts\test-verification-catalog-validator.ps1`; `Test-Json -Json $json -SchemaFile .\docs\verification\verification-catalog.schema.json -ErrorAction Stop`; `.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification`; `git diff --check`; `git status --short`; `git diff --name-only -- src/RMC.BestFit src/RMC.BestFit.UI src/RMC.BestFit.App`.
- **Verification execution:** none. No Verification method, full Verification suite, retained point-process likelihood oracle, or method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests` was executed. Verification was compiled only; historical results were not rewritten as current executions.
- **Open scope:** Chunk 7 still owns independently supported ordinary and uncertain-data bootstrap accuracy plus the remaining Bulletin 17C scientific gaps. Chunk 8 still owns point-process recovery/prior normalization. No Chunk 4B, 4C, 5, 7, or 8 implementation was started.
- **Next:** Chunk 4B is the first unchecked implementation unit.

### 29 August 2026 — Chunk 4B complete

- **Base and preservation:** `73cf9beb34a7b2d8ac16edb68c93474425280337` on `documentation-verification-updates`; work continued directly in the current checkout without a branch/worktree and without staging, committing, pushing, discarding, reverting, or overwriting the dependent Chunk 1-4A diff. Chunk 4B changed no production-library, UI, or App file.
- **Composite ownership:** renamed `CompositeRecoveryTests.cs` and its helper partial to `CompositeOracleVerificationTests.cs` and the matching class identity. Normalized comparisons against the pre-task sources confirmed an identity-only rename: all ten analytical, published, and complete-Cartesian posterior methods, method bodies, seeds, and tolerances are unchanged. The ten catalog entries remain `retain-verification` and `open` because no exact approval-gated method was executed after the rename; Chunk 10B still owns genuine N=1000 fitted-child predictive recovery.
- **RatingCurve ownership:** removed the three one-/two-/three-segment default-exponent-bound declarations and their private fixture helper from Verification while retaining exactly three analytical power-law/continuity methods. The existing estimator-free `RatingCurveTests.DefaultFlatPriors_BetaBounds_ArePositiveForAllSegments` fast contract already uses inline fixtures, covers segment counts one through three, and checks both positive lower bounds and prior support, so no duplicate fast test was added. RatingCurve recovery normalization remains Chunk 12 work.
- **Catalog and references:** removed the three moved RatingCurve entries, reconciled all ten Composite source/class identities, and updated only the current audit and technical-reference source path. Historical method-result tables remain historical and were not rewritten as current executions. Default cumulative validation resolves 506 declarations and 506 execution units: 224 `verified`, 224 `open`, 56 `execution-excluded`, and two `accepted-limitation` entries.
- **Validation:** the exact fast owner passed 1/1. Serial project gates passed Core 3,363/3,363, UI 581/581, and App 443/443. Verification compilation succeeded with zero warnings/errors. The quick XML scan and the strict Debug XML gate passed across 918 source files; the first sandboxed full XML attempt was denied read access to the existing Windows SDK/NuGet configuration, and the identical elevated retry passed. Catalog validator fixtures passed 22/22, JSON Schema validation returned true, and default catalog validation passed with 224 reported open gaps and 506 declarations / 506 execution units. Direct identity checks found ten open Composite oracle entries, zero moved RatingCurve methods under Verification, and three retained RatingCurve continuity entries. `git diff --check` and a direct whitespace/final-newline scan of all ten untracked repository files passed. The task-scoped read-only reviewer found no Critical, Important, or Minor defects.
- **Commands:** `dotnet exec .\src\RMC.BestFit.Tests\bin\Debug\net10.0\RMC.BestFit.Tests.dll --filter "FullyQualifiedName~DefaultFlatPriors_BetaBounds_ArePositiveForAllSegments" --minimum-expected-tests 1`; `dotnet test .\src\RMC.BestFit.Tests -c Debug`; `dotnet test .\src\RMC.BestFit.UI.Tests -c Debug --no-build`; `dotnet test .\src\RMC.BestFit.App.Tests -c Debug`; `dotnet build .\src\RMC.BestFit.Verification -c Debug --no-restore`; `.\scripts\validate-code-xml-docs.ps1 -SkipBuild`; `.\scripts\validate-code-xml-docs.ps1 -Configuration Debug`; `.\scripts\test-verification-catalog-validator.ps1`; `Test-Json -Json $json -SchemaFile .\docs\verification\verification-catalog.schema.json -ErrorAction Stop`; `.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification`; `git diff --check`; `git status --short`.
- **Verification execution:** none. No Composite oracle, RatingCurve continuity or recovery method, Bulletin 17C confidence-interval coverage method, or other Verification method was run. Verification was compiled only, and historical results were not transferred to the renamed exact identities.
- **Open scope:** the ten renamed Composite oracle methods remain open pending separately authorized exact runs; Chunk 10B owns N=1000 predictive recovery and Chunk 12 owns RatingCurve recovery normalization. No Chunk 4C, 5, 10B, or 12 implementation was started.
- **Next:** Chunk 4C is the first unchecked implementation unit.

### 29 August 2026 — Chunk 4C complete

- **Base and preservation:** `73cf9beb34a7b2d8ac16edb68c93474425280337` on `documentation-verification-updates`; work continued directly in the current checkout without a branch/worktree and without staging, committing, pushing, discarding, reverting, or overwriting the dependent Chunk 1-4B diff. The only Chunk 4C production-library edit is a documented internal extraction of the existing cross-validation finalization block; no formula, metric, exception, progress, notification, estimator, sampler, tolerance, seed, public API, serialization contract, UI production file, or App production file changed.
- **Fast ownership:** existing sampler-free tests already cover reduced-model construction, held-out covariate handling, no-fold failure, injected-draw conditional prediction, regional posterior aggregation, uncertainty dispatch and selected method, Godambe availability/failure, block-bootstrap row construction, uncertainty settings, and site/bootstrap result DTO state. The new inline-fixture `CompleteCrossValidation_PublishesSuccessfulFoldAccountingWithoutChangingEstimateState` covers the sole missing contract: result retention, successful-fold count, hand-calculated MAE/RMSE/mean bias, preserved statuses/messages, one property notification, and untouched analysis/Bayesian estimate state. It invokes no optimizer, estimator, bootstrap, or MCMC sampler. Its authored red build reported `CS1061` before the internal completion seam existed; the exact current green runner passed 1/1.
- **Verification ownership:** the three spatial Verification class remarks now state that their current estimator-backed comparisons are same-production-path cells, not independent evidence. All eight declarations remain `open`: five `replace` cells for Chunk 14A held-out predictions and Chunk 14B conditional-GP/regional-posterior artifacts, plus three `split` cells for Chunk 14B independent Godambe H/J covariance, temporal block-bootstrap quantiles, and analytic VIF transformation evidence. No historical result was promoted or claimed as fresh execution.
- **Catalog:** declaration, execution-unit, status, and disposition counts are unchanged at 506 declarations / 506 units: 224 `verified`, 224 `open`, 56 `execution-excluded`, and two `accepted-limitation`. The eight spatial gaps name their exact fast owners and preserve only the missing independent evidence; the three uncertainty gaps point to `SpatialGEVResultsTests.SiteResultsAndBootstrapResults_DefaultsAndRoundTrip` for site/bootstrap DTO state.
- **Validation:** the exact new fast method passed 1/1. Fresh serial controller gates passed Core 3,364/3,364, UI 581/581, and App 443/443. Verification compilation succeeded with zero warnings/errors and executed no test. The strict Debug XML gate passed across 918 source files after the sandboxed attempt was denied access to the existing Windows SDK/NuGet configuration and the identical elevated retry was allowed. Catalog validator fixtures passed 22/22; default catalog validation passed with 224 open gaps and 506 declarations / 506 execution units. `git diff --check` passed with line-ending warnings only. Task-scoped review and its post-correction re-review reported no Critical, Important, or Minor findings.
- **Commands:** `dotnet exec .\src\RMC.BestFit.Tests\bin\Debug\net10.0\RMC.BestFit.Tests.dll --filter "FullyQualifiedName~CompleteCrossValidation_PublishesSuccessfulFoldAccountingWithoutChangingEstimateState" --minimum-expected-tests 1`; `dotnet test .\src\RMC.BestFit.Tests -c Debug`; `dotnet test .\src\RMC.BestFit.UI.Tests -c Debug`; `dotnet test .\src\RMC.BestFit.App.Tests -c Debug`; `dotnet build .\src\RMC.BestFit.Verification -c Debug`; `.\scripts\validate-code-xml-docs.ps1 -Configuration Debug`; `.\scripts\test-verification-catalog-validator.ps1`; `.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification`; `git diff --check`; `git status --short`.
- **Verification execution:** none. No spatial Verification method, full Verification suite, Bulletin 17C confidence-interval coverage method, or other Verification method was run. Verification was compiled only.
- **Open scope:** Chunk 14A owns independent held-out fold predictions. Chunk 14B owns independent conditional-GP/regional-posterior, Godambe H/J, temporal block-bootstrap quantile, and analytic VIF evidence. No Chunk 5, 14A, 14B, or later implementation was started.
- **Next:** Chunk 5 is the first unchecked implementation unit.

### 30 August 2026 — current-source reconciliation through Chunk 7

- **Stale-checkpoint correction:** current source, catalog, `test-inventory.md`, and exact TRX evidence show Chunk 5, Chunk 6A, and Chunk 6B complete; their plan boxes are checked without redoing or rerunning those completed units. The prior "Chunk 5 is next" sentence is retained as dated history, not current state.
- **Chunk 7 recovery:** replaced six same-sample product-moment identities with exactly N=1000, seed-12345 generated-parent recovery in the declared natural/base-10 coordinates. All six current identities passed exact guarded runs using the shared recovery design and approved parameter/quantile-variance acceptance.
- **Chunk 7 covariance:** replaced the same-ecosystem Numerics covariance target with an independent central-moment/Jacobian sandwich and a Python 3.12.13 standard-library frozen artifact. Eleven of thirteen current covariance identities passed. Natural-space Pearson III N=25 and N=100 failed covariance[0,0] against the independent oracle and remain open; no production conditioning policy or tolerance changed.
- **Artifacts:** `verification/data/bulletin17c/b17c-gmm-covariance-oracle.json` SHA-256 `bbaf3d0c616b550135a32a098a611bd16c301995a147700745c5ec956b68c2cf`; generator `verification/python/bulletin17c/generate_b17c_covariance_oracle.py` SHA-256 `61dcb3280c41ece88e6c7dc1c0c1b8aa5579ea9012ed2a39df1cf5a5d84fb25b`.
- **Scoped validation:** Verification compiled with zero warnings/errors; every one of the 19 selected B17C methods ran individually through the guarded runner and every latest TRX was inspected; JSON Schema validation returned true; default catalog validation passed with 174 open gaps and 530 declarations / 530 execution units; `git diff --check` passed with line-ending warnings only.
- **Execution boundary:** no method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests` ran. The full Verification project did not run. This Chunk 7/8 work staged, committed, pushed, reverted, or discarded nothing and moved or renamed no file.
- **Next:** Chunk 8 is the first unchecked implementation unit. Chunk 9 and later work have not started.

### 30 August 2026 — Chunk 8 complete; stop before Chunk 9

- **Point-process ownership:** retained the two analytical likelihood identities and the three analytical simulation identities as distinct scientific claims. Reclassified the two N=4000 PERT-histogram cells as independently calculated occurrence-histogram/prior-placement claims, not estimator recovery; deterministic default-prior construction, containment, fallbacks, and serialization remain fast-test responsibilities.
- **Recovery normalization:** all five total-N=1000 fixtures retain their established generators, seeds, sampler, and defaults. They diagnose prior support, likelihood discrimination, exposure/threshold/block-origin agreement, the Numerics-Kappa versus Coles-shape sign, and seasonal annualization before MCMC. Nonseasonal coordinates use empirical central-95% parent inclusion. Seasonal recovery uses likelihood-native threshold intensity, GPA scale, and Hosking Kappa with effective component count `N_s=N*p_s`, `p_s=w_s*Lambda_s/sum(w_j*Lambda_j)`; intensity uses its Poisson standard error, GPA scale/Kappa use Numerics analytical MLE covariance scaled to `N_s`, and absolute standardized error must be at most 1.96. Every raw fitted coordinate retains R-hat below 1.10 and ESS at least 100; changepoints and response-space checks retain central-95% inclusion.
- **Exact evidence:** all thirteen current Chunk 8 identities passed individual guarded runs: both likelihood oracles, both prior-placement oracles, the stationary SciPy oracle, all five recoveries, and all three analytical simulations. Final exact reruns of the four seasonal recoveries passed from isolated one-result TRXs at `20260830-090050`, `090132`, `090216`, and `090303`. Equal-intensity effective counts are `508.1967213114754` and `491.8032786885246`; the 12-versus-4 unequal-intensity event-mixture counts are `756.09756097561` and `243.90243902439`. No seed, prior, sampler, likelihood, generator, production algorithm, or convergence rule changed.
- **External artifact:** the predeclared stationary crosswalk uses annual exposure, threshold `u=80`, annual Poisson intensity, and `Kappa=-xi`; it explicitly makes no seasonal package-parity claim. `verification/data/point-process/stationary-poisson-gpa-scipy-oracle.json` was generated with Python 3.12.13, SciPy 1.17.1, and NumPy 2.5.2 and has SHA-256 `54a57ea459ba1a71dc9e828672dda83386ed5e137348f9c2740567ba242d7d5b`; generator `verification/python/point-process/generate_point_process_scipy_oracle.py` has SHA-256 `0d085434034e484a94dc5db3e212127f9043372e376dcbc2904ae7e650a9af56`.
- **Catalog and validation:** the catalog validator harness passed 22/22, JSON Schema validation returned true, and default catalog validation passed with 531 declarations / 531 execution units and 165 open gaps. Declaration statuses are 308 verified, 165 open, 56 execution-excluded, and two accepted limitations. Core, UI, and App fast gates passed 3,375/3,375, 581/581, and 443/443; Verification compiled with zero warnings/errors; the strict Debug XML gate passed across 931 source files. Final whitespace/newline and task-scoped status review are recorded in the handoff.
- **Repository-state note:** after the supplied `73cf9beb` baseline, the reflog records user-authored commit `0a2703a5` (`Complete verification remediation through Chunk 6`) at 07:28:59 MDT. This Chunk 7/8 work remains entirely unstaged on top of that commit and did not create it.
- **Execution boundary:** no method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests` ran. The full Verification project did not run. Chunk 9 was not started. This Chunk 7/8 work staged, committed, pushed, reverted, or discarded nothing and moved or renamed no file.
- **Next:** Chunk 9 is the first unchecked implementation unit and is outside this execution scope.

### 30 August 2026 — Chunk 7 covariance remediation complete; Chunk 8 boundary retained

- **Approved shared correction:** Haden Smith approved changing Numerics `MatrixRegularization.MakeSymmetricPositiveDefinite` to test the symmetric candidate before adding a ridge. TDD pinned unchanged well-conditioned, scale-separated, asymmetric-SPD, rank-deficient, indefinite-fallback, and Gaussian-mixture caller behavior. Accepted candidates now return unchanged; rejected candidates retain the prior ridge magnitude, escalation, and fallback. No Boolean bypass overload was added, and no B17C covariance formula, independent oracle, tolerance, seed, or estimator behavior changed.
- **Chunk 7 exact evidence:** all 13 independent covariance identities and all six generated-parent recovery identities passed current individual guarded runs with one executed result in each inspected TRX. Downstream exact guarded regression runs also passed all 12 B17C penalty identities, all 12 example/plotting-position identities, and all seven selected general-GMM identities. Three wrong-class plotting-position attempts produced zero-result TRXs, were discarded as evidence, and were replaced by passing runs under the exact `HirschStedingerPlottingPositionVerificationTests` identities.
- **Numerics validation:** the Release build passed on net481, net8, net9, and net10 with zero warnings/errors. All focused matrix/GMM tests passed on every framework, and the deterministic suite excluding the live time-series download fixture passed 2,430/2,430 on every framework. The unfiltered suite's only persistent exact rerun failure was the external Australian BOM Cotter River HTTP 500; the other initially affected BOM identities passed on exact framework-specific reruns.
- **Chunk 8 retained:** all 13 current point-process identities remain passed under the completed Chunk 8 evidence, including the user-approved seasonal effective-count rule `N_s=N*p_s`. No Chunk 8 seed, prior, sampler, likelihood, generator, convergence rule, or acceptance threshold changed during this remediation.
- **Catalog:** current default validation passes with 531 declarations / 531 execution units and 163 open gaps. Declaration statuses are 310 verified, 163 open, 56 execution-excluded, and two accepted limitations.
- **Execution boundary:** no method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests` ran. The full Verification project did not run. Chunk 9 was not started. Nothing was staged, committed, pushed, reverted, discarded, moved, or renamed.
- **Stop:** Chunks 7 and 8 are reconciled. Chunk 9 remains the first unchecked implementation unit and is outside this execution scope.

### 30 August 2026 — Chunk 9 reconciled; proceed to Chunk 10A

- **Design:** the historical 20-cell cross-product is replaced by five BestFit cells over three N=1000, seed-12345 dog-leg fixtures. The labeled Verification generator matches production draws and predeclares theoretical cause shares, hard wins, soft likelihood responsibilities, dominance mass, and interior crossovers while retaining any extreme-tail re-entry. Same-family Weibulls use increasing-shape ordering. MLE uncertainty comes from the full observed-information matrix without a ridge; both independent Bayesian cells use central-95-percent coordinate intervals, R-hat below 1.10, and ESS at least 100, with the maximum cell disabling the optional Jeffreys scale multiplier.
- **Thinning:** independent minimum, independent maximum, and fixed-rho=0.6 minimum designs remain. A balanced three-Weibull candidate passed cause-share gates but was removed after BestFit MLE hit a shape bound, Numerics information was not positive definite, and BestFit Bayesian recovery excluded the first scale. Cause balance is necessary but not sufficient for coordinate identification.
- **Disposition:** all five retained BestFit identities pass exact one-result TRXs. The redesigned independent maximum, Weibull(100,3) + Gumbel(80,20), passes MLE and Bayesian recovery with hard wins 495/505, soft counts 488.2/511.8, an interior crossover at 0.474, and expected Gumbel tail re-entry at 0.987. The Bayesian cell retains bounded parameter priors but disables the optional Jeffreys scale multiplier. The former Weibull(80,2) + Gumbel(60,10) fixture remains a documented single-start local-mode example; direct cause-sum parity ruled out its log-PDF formulation.
- **Numerics:** three matching MLE fixtures replace the old estimator/recovery matrix. Independent minimum, redesigned independent maximum, and correlated minimum each pass individually on net481, net8, net9, and net10. The test helper uses `Tools.IsFinite` and `Tools.Clamp`; no Numerics competing-risk production file changed.
- **Boundary:** no correlated Bayesian method exists or ran. A default-prior Bayesian maximum diagnostic showed that the optional Jeffreys scale multiplier and MAP initialization select a disappeared-Weibull boundary mode with rank-deficient information. The retained identity disables only that multiplier and passes without changing the production default. The four 40,000-draw dependency calibrations remain unchanged and were not conflated with N=1000 recovery. No Bulletin 17C confidence-interval coverage identity or full Verification suite ran. Chunk 10A follows; Chunk 11A remains outside scope.

### 30 August 2026 — Chunk 10A reconciled; proceed to Chunk 10B

- **Crosswalk:** the six current identities retain N=1000 and their declared fixture/estimator seeds. Full-K physical weights, stored K-1 weights, final-weight reconstruction, ascending-mean labels, Normal mean/standard-deviation coordinates, zero-atom mass, positive-component mass, and expected component counts are explicit. Same-ecosystem BestFit/Numerics parity is no longer the scientific acceptance oracle.
- **Independent artifact:** `verification/data/mixture/normal-mixture-sklearn-oracle.json` freezes a NumPy-PCG64 seed-12345 N=1000 sample, scikit-learn 1.9.0 diagonal-covariance fit, and independently evaluated likelihood/CDF values. The generator is `verification/python/mixture/generate_sklearn_normal_mixture_oracle.py`. Artifact SHA-256 is `20d0bb7f8b351b18e01135fbdce00d8090a5ecb7dfa9709f9f32b61571ce5d4b`; generator SHA-256 is `7ff9f480aa696efe488b62370fbde4d74aff4880440e4ef2d007e5ca6215eb93`.
- **Disposition:** all six current recovery identities and the new external-package identity passed. The first post-edit Bayesian runs incorrectly declared unsorted retained arrays as sorted to `Statistics.Percentile`; those two false exclusions and one nonfinite interval were discarded, and the exact methods passed after the Verification-only arrays were sorted. No seed, prior, sampler, likelihood, parent, or acceptance rule changed.
- **Evidence:** all seven final exact guarded invocations produced one inspected TRX result. The three superseded percentile-oracle failures also produced one-result TRXs but are not scientific evidence. The complete Verification project and Bulletin 17C confidence-interval coverage classes did not run. Chunk 10B is next; Chunk 11A remains outside scope.

### 30 August 2026 — Chunk 10B complete; stop before Chunk 11A

- **Current oracle identities:** all ten exact `CompositeOracleVerificationTests` methods passed under their current post-rename names with one inspected TRX result each. Historical `CompositeRecoveryTests` results were not transferred. Fixed analytical grids, 8,000 Cartesian combinations, and 5,000 resampling draws retain their theory/published/independent classifications and are not treated as recovery N.
- **Predictive coverage:** four new `CompositePredictiveRecoveryTests` methods generate exactly N=1000 observations for each Normal child, fit children serially through unchanged Bayesian defaults, and cover an unequal-weight mixture, independent maximum, independent minimum, and equal-weight model average. Every child coordinate satisfies central-95% parent inclusion, R-hat below 1.10, and ESS at least 100; analytical parent quantiles at nonexceedance 0.10, 0.25, 0.50, 0.75, and 0.90 lie in every central 95% propagated band. All four pass in 14.958-15.711 seconds.
- **Discarded runs:** the first predictive-mixture attempt used an unsorted posterior array with `dataIsSorted: true`; the next used the same probabilities in descending exceedance order. Both one-result helper-construction failure TRXs are discarded, and the final exact method passes after Verification-only sorting/order corrections. No production or scientific setting changed.
- **Stop boundary:** Chunk 10B is reconciled. Chunk 11A was not inspected or started. No Bulletin 17C confidence-interval coverage identity or full Verification suite ran.
- **Final catalog after the approved Bayesian maximum follow-up:** default validation passes with 521 declarations / 521 execution units and 135 open gaps. Declaration statuses are 328 verified, 135 open, 56 execution-excluded, and two accepted limitations. JSON Schema validation returned true and the validator fixture harness passed 22/22.
- **Final gates:** Core, UI, and App passed 3,375/3,375, 581/581, and 443/443. Verification compiled with zero warnings/errors. The initial strict XML command was blocked by sandbox access to the existing Windows SDK/NuGet profile; the identical elevated retry passed across 935 source files. `git diff --check` passed with line-ending warnings only, and the direct untracked-file whitespace/final-newline scan passed for all 13 untracked files.
- **Numerics validation:** the four-framework Release build passed with zero warnings/errors. On each of net481, net8, net9, and net10, all three exact retained competing-risk MLE identities passed and the deterministic suite excluding the live `Data.TimeSeriesAnalysis.Test_TimeSeriesDownload` class passed 2,423/2,423. No live-network fixture ran in the final sweep.
- **Repository preservation:** the RMC-BestFit staged diff is empty. The sibling Numerics checkout remains at `9b66ad7f77d91dd60e3870104dc0edee907a830b` on `bug-fixes-and-enhancements` with its four preserved Chunk 7 files plus the approved `Test_CompetingRisks.cs` change and an empty staged diff. This work staged, committed, pushed, reverted, discarded, moved, or renamed nothing.

### 30 August 2026 — Chunk 11A reconciled; proceed to Chunk 11B

- **Identification and ownership:** all seven generated-parent copula fixtures now contain exactly N=1000 matched pairs with their established generator offsets. Normal margins are fitted in `[mu, sigma]` order and judged with Normal MLE covariance; they remain fixed during conditional copula estimation and are not described as posterior coordinates. The six one-coordinate copulas use unchanged DEMCzs defaults, central-95% parent inclusion, R-hat below 1.10, ESS at least 100, prior support, and parent-versus-independence discrimination.
- **Student-t disposition:** Haden Smith directed stopping and removing `RecoverStudentTCopulaParameters` because repeated beta-function evaluation made the MCMC realization impractical. The interrupted directory contains no TRX and is not evidence. The replacement `RecoverStudentTCopulaParametersWithMaximumLikelihood` uses the unchanged default production MLE, an unregularized observed-information covariance for `rho`, and a full-covariance delta-method standardized-error check for symmetric tail dependence; weak raw `nu` recovery is not claimed. The exact replacement passed in one inspected result.
- **Independent Student-t evidence:** two new `CopulaEstimationOracleTests` identities cover MPL and IFM on an independently generated N=1000 Student-t sample in physical `[rho, nu]` order. Python 3.12.13, NumPy 2.3.5, and SciPy 1.18.1 perform deterministic differential evolution followed by L-BFGS-B and independently check the density against the bivariate-t/univariate-t ratio. Artifact SHA-256 is `28edfbd28e392df1ac540766f3ba5c3f1ce57d8a442facbcd6541866f683956e`; generator SHA-256 is `358dca1910f1091e1f9f07978f38662444575f9a0373e39cd31189c37918307b`.
- **Exact evidence:** the six final Bayesian recovery reruns, the Student-t MLE recovery, and both Student-t external optimum cells passed individually through the guarded runner; every final TRX contains exactly one result. The first MLE run failed only because covariance status was inspected before computation, and the first Student-t MPL run was the expected TDD missing-fixture failure; neither superseded failure is accepted evidence.
- **Scoped checkpoint:** Verification builds with zero warnings/errors. JSON Schema validation passes. Default catalog validation passes with 523 declarations / 523 execution units and 128 open gaps: 337 verified, 128 open, 56 execution-excluded, and two accepted limitations. All Chunk 11A changes remain test/oracle/artifact/catalog/documentation-only, so the approved workflow proceeds automatically to Chunk 11B. No Bulletin 17C confidence-interval coverage method or full Verification suite ran.

### 30 August 2026 — Chunk 11B reconciled; proceed to Chunk 12

- **Coverage matrix:** the three existing analytical Normal-sum identities retain their classification and cover zero, positive, and negative dependence in a linear response. One new N=1000, seed-13055 identity covers the distinct monotone nonlinear response `Z=exp(0.01X+0.01Y)` under positive Gaussian-copula dependence. A full correlation-sign-by-response Cartesian expansion was rejected as scientifically redundant.
- **Independent oracle and uncertainty:** `log(Z)` has an exact Normal law, so no frozen artifact is needed. The response-table approximation has a separate predeclared 0.015 AEP bound. Normal marginal MLE recovery uses observed-information standardized errors; copula `rho` uses central-95% inclusion, R-hat below 1.10, and ESS at least 100. Two thousand independent asymptotic Normal-MLE draws at seeds 24680/24681 propagate marginal-fit uncertainty and are explicitly not posterior draws. Parent response AEP is inside the central 95% propagated band at nonexceedance 0.10, 0.25, 0.50, 0.75, and 0.90.
- **Exact evidence:** the new nonlinear identity and all three source-affected analytical identities passed individually through the guarded runner; every latest TRX contains exactly one result. The only authored-red issue was a build-time shared-constant ownership error, corrected entirely in Verification source.
- **Scoped checkpoint:** Verification builds with zero warnings/errors. JSON Schema validation passes. Default catalog validation passes with 524 declarations / 524 execution units and 128 open gaps: 338 verified, 128 open, 56 execution-excluded, and two accepted limitations. Chunk 11B changed no production source or scientific contract, so the approved workflow proceeds to Chunk 12. No Bulletin 17C confidence-interval coverage method or full Verification suite ran.

### 31 August 2026 — Chunk 12 and Verification-wide MLE/MAP normalization complete; stop before Chunk 13A

- **Optimizer policy:** the final source audit found 149 direct MLE/MAP constructions. All 144 non-profile constructions use Differential Evolution with untouched default tolerances. Five deliberate profile-likelihood constructions retain BFGS because their nuisance optimizations are paired with Brent roots and are part of the independent profile oracles. No GMM-only optimizer or tolerance was changed.
- **Chunk 11A:** all seven N=1000 generated-parent recovery identities, all twelve bivariate MLE cells, and all fourteen independent copula optimum cells pass their current exact identities. Student-t MCMC was removed at Haden Smith's direction; its MLE replacement and both independent Student-t MPL/IFM cells pass. The current Chunk 11A matrix is 33/33 verified.
- **Chunk 11B:** the three analytical Normal-sum cells and the distinct N=1000 nonlinear Lognormal-response recovery cell pass. The current matrix is 4/4 verified.
- **Chunk 12:** five fixtures per estimator remain after ten redundant declarations were consolidated. MLE uses DE/default; Bayesian recovery publishes posterior MAP without changing coordinate intervals, R-hat, or ESS. Parameter uncertainty and the declared draw-specific log10 residual uncertainty are propagated separately into simultaneous max-|t| 95% predictive bands over the predeclared grids. Exact segmented allocations are 495/505 and 270/406/324. All ten recovery identities pass; including continuity, example recovery, and likelihood oracles, rating curve is 22 verified / 0 open.
- **Additional current gaps exposed by the optimizer audit:** two Log10-Normal analytical MAP/GMM cells and the AR(1) MLE recovery cell remain open under the unchanged fixtures and acceptance rules. No tolerance, bound, prior, seed, fixture, or production/scientific contract was tuned to clear them. The exact AR(1) MLE method was run once under Haden Smith's later whole-library MLE/MAP audit direction; no other Chunk 13A method ran and no Chunk 13A recovery design or remediation was begun.
- **Final catalog:** default validation passes with 514 declarations / 514 execution units and 111 open gaps: 345 verified, 111 open, 56 execution-excluded, and two accepted limitations. JSON Schema validation is true; catalog-validator fixtures pass 22/22.
- **Final gates:** Core, UI, and App pass 3,375/3,375, 581/581, and 443/443. Verification builds with zero warnings/errors. The first strict XML attempt was blocked by sandbox access to existing Windows SDK/NuGet profile directories; the identical elevated command passes across 936 source files. `git diff --check` and the direct untracked-file whitespace/final-newline scan pass.
- **Boundary:** no Bulletin 17C confidence-interval coverage method or full Verification suite ran. Chunk 13A implementation/design did not start; the sole later-chunk execution was the AR(1) MLE audit run disclosed above. No file was staged, committed, pushed, reverted, discarded, moved, or renamed.
