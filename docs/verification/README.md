<!-- verification-status: draft -->

# RMC.BestFit 2.0 Verification Report

This is the living formal verification and validation record for `RMC.BestFit.Verification`. It separates scientific numerical evidence from fast unit and regression coverage.

The active handoff and batching plan is maintained in [Verification Finalization Plan](verification-finalization-plan.md). Start there when continuing this program in a new session.

## Current status

| Program area | Status | Evidence |
|---|---|---|
| Repository integration | Operational - validated 4 August 2026 | Strict Debug XML build and Verification compilation: 0 warnings/errors; Release solution excludes Verification; the method-level ownership cleanup is complete |
| Public API baseline | Captured; UI/App boundary refreshed 20 August 2026 | Core exported API plus exact UI/App public-and-protected signatures enforced by `PublicApiCompatibilityTests`; see [Time-Series Verification](time-series.md#ui-and-app-compatibility-baseline) |
| Fast regression gate | Phase 5 final gate refreshed 21 August 2026 | Core 3,237; UI 578; App 444; API 498; 0 failures; strict Debug XML build 0 warnings/errors |
| External environments | Locked | R 4.4.3 with 131 packages; Python with 15 packages |
| Test ownership audit | Complete - 4 August 2026 | All 84 Verification C# files were reviewed; 435 of 1,196 methods remain, 761 redundant/non-verification methods were removed, and 35 missing deterministic contracts were added to the fast core project. Core line/branch/method coverage increased from 64.73/59.46/86.94% to 66.03/60.43/88.25%. See [Test Inventory](test-inventory.md) |
| Distribution fitting | Closed - Phase 1 | All 15 family-specific and both multi-candidate external-oracle methods passed; TR-001, TR-009, and TR-063 are verified; TR-002 and TR-064 are rejected non-defects; TR-010 is fixed by regression. All nine Phase 1 artifact hashes match the manifest. See [Distribution Fitting](distribution-fitting.md) |
| Model estimation and diagnostics | Closed - Phase 2 | Log10-Normal estimator equivalence, fit/variance/combined influence, and external-package parity for DIC, WAIC, PSIS-LOO, MLE/MAP nuisance profiling, Hansen J, overidentified one-step fitting, fixed-weight/efficient GMM sandwich covariance, and rank-normalized R-hat/bulk-tail ESS passed. TR-023 and TR-032 retain their public signatures; TR-024 through TR-027 and TR-029 through TR-031 and TR-034 are fixed in their approved scopes. TR-028 remains an accepted documented limitation; TR-033 is rejected as a non-defect. All seven Phase 2 artifact hashes match the manifest. See [Model Estimation](model-estimation.md) |
| Data handling and Bulletin 17C | Closed - Phase 3 | TR-003 documents the accepted grouped-threshold disaggregation and most-recent-time prior reference. All seven formal worked-example GMM methods passed published mean/standard-deviation/skew parity at `1E-3`. Exact-LP3 Cohn scope guards passed fast tests; Cohn value verification is deferred. The 14 ordinary/pivotal reliability cells retain their 13,000 finite outputs with zero retries, substitutions, or exceptions. See [Bulletin 17C Verification](bulletin-17c.md), the [Phase 3 ledger](verification-finalization-plan.md#phase-3---data-handling-and-bulletin-17c), and [Scientific Review Findings](../technical-reference/review-findings.md). |
| Point-process correction | Closed - Phase 4 point-process subset | TR-004/TR-005 are complete in the approved scope. All ten guarded cells pass with 1,000-observation recovery fixtures and untouched DEMCzs defaults, including calendar/water-year block-origin parity. See [Point-Process Verification](point-process.md). |
| Mixture correction | Closed - Phase 4 mixture subset; identified-sampler recovery passed 21 August 2026 | Public full-$K$ EM/Numerics parity remains unchanged. New BestFit chains store $K-1$ weights, derive the final weight inside the posterior target, and use the identified EM covariance directly without mixture MAP/Hessian refinement. Deterministic gates pass, and the three parity and three Bayesian recovery methods passed separately under production DEMCzs defaults (6/6). See [Mixture Verification](mixture.md). |
| Competing-risk simulation and recovery | TR-012 closed; six Bayesian recovery cells deferred as research | Fast seed/matrix contracts and four analytical rank/CDF methods pass. All ten MLE recovery methods and four Default-DEMCzs methods with MAP-centered initialization pass; the six remaining Bayesian cells (four maximum, two correlated) expose convergence, identifiability, ESS, or uncertainty-curve findings and are explicitly deferred research items that gate no later phase. See [Competing-Risks Verification](competing-risks.md). |
| Composite and cross-analysis posterior propagation | TR-013/TR-014/TR-015 closed; recovery supplement passes 10 of 10 | Fast weighting/matrix/seed/cache contracts and both guarded posterior-resampling methods pass. The unchanged extreme-tail inversion cell passed after the approved `XTransform.None` correction. `BivariateAnalysis` remains conditional on fixed marginals. See [Composite Verification](composite.md). |
| Time-series models | Phase 5 complete | TR-035 through TR-041 and TR-046 are closed; TR-042 remains closed with refreshed evidence. UI/App signatures and serialization remain compatible, every named numerical method passes, and the eight-cell MLE/Bayesian recovery matrix passes with 1,000 retained observations and untouched Bayesian defaults. See [Time-Series Verification](time-series.md). |
| Rating curve | Phase 6 in progress - Batch 6.1 confirmation complete | TR-043 through TR-045 confirmed on 21 August 2026 by exact guarded cells and a fast contract; the example fixtures, discharge-space likelihood oracle, and six replication recovery cells are committed, and the fix plans and acceptance rule await approval. See [Rating-Curve Verification](rating-curve.md). |
| Bivariate and coincident frequency | Closed - Phase 6 Batch 6.2 (21 August 2026) | TR-047 closed. Twelve exact `CopulaEstimationOracleTests` methods pass against the independent Python copula-estimation oracle (six families, MPL and IFM; historical R targets retained), Bayesian copula recovery passes 7/7 and the closed-form coincident-frequency cells 3/3 under production defaults, and the bivariate TR-047 routing regression runs in the fast gate. See [Bivariate Verification](bivariate.md). |
| Spatial extremes | Phase 6 in progress - Batch 6.3 confirmation complete | TR-048, TR-049, and TR-057 confirmed on 21 August 2026 by exact guarded cells against the R `mvtnorm` missing-site and location-error oracle (conventions and the posterior kernel verified); fix plans await approval; TR-050 through TR-054, TR-056, and TR-058 through TR-062 follow in Batches 6.4-6.6. See [Spatial Extremes Verification](spatial-extremes.md). |

## Evidence rule

A passing build, estimator invocation, optimizer convergence, finite result, result shape, or reproduction of an internal result is not verification. A claim is verified only when its test has an analytical, independently implemented, external-package, published/real-source, recovery, or coverage oracle with declared provenance and tolerance. Deterministic state, validation, serialization, guard, caching, and regression contracts belong in the fast test projects even when they exercise an object restored to an estimated state.

## Execution rule

The full Verification project is never run as one suite. Each result in this report is produced by exactly one fully qualified method through:

```powershell
.\scripts\run-verification-test.ps1 -Test Namespace.Class.Method
```

The runner rejects broad filters and requires exactly one TRX result.

## Traceability states

- **Planned:** no executable scientific test yet.
- **Ready - focused run:** the test compiles and may be executed only as one exact fully qualified method through the guarded runner.
- **Passed:** the focused TRX and oracle artifact have been reviewed.
- **Failed:** the focused test contradicted the claim or exposed a defect.
- **Blocked:** an external dependency, upstream correction, or scientific decision prevents completion.

All 30 recovery-supplement methods and both existing TR-014 methods have been run individually
under the execution rule. Twenty-four supplement methods and both TR-014 methods pass. The six
remaining Bayesian competing-risk findings have an approved deferred-research disposition, so
Phase 5 is complete for its approved scope. The supplement pins Numerics
`c361f2864428a98a33d6072ffa9bc11ac360839d` and RMC-TotalRisk
`d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7`.

[Finalization Plan](verification-finalization-plan.md) | [Methodology](methodology.md) | [References](references.md)
