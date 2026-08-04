<!-- verification-status: draft -->

# RMC.BestFit 2.0 Verification Report

This is the living formal verification and validation record for `RMC.BestFit.Verification`. It separates scientific numerical evidence from fast unit and regression coverage.

The active handoff and batching plan is maintained in [Verification Finalization Plan](verification-finalization-plan.md). Start there when continuing this program in a new session.

## Current status

| Program area | Status | Evidence |
|---|---|---|
| Repository integration | Operational - validated 3 August 2026 | Strict Debug XML build and Verification compilation: 0 warnings/errors; Release solution excludes Verification; test-ownership migration remains an active hygiene backlog |
| Public API baseline | Captured | Exact exported type/member/enum baseline enforced by `PublicApiCompatibilityTests` |
| Fast regression gate | Passed 3 August 2026 | Core 3,134; UI 571; App 428; Numerics 2,072 on each of net481/net8/net9/net10; 0 failures |
| External environments | Locked | R 4.4.3 with 131 packages; Python with 15 packages |
| Test ownership audit | In progress | Initial duplicate removals and FittingAnalysis split are recorded in [Test Inventory](test-inventory.md) |
| Distribution fitting | Closed - Phase 1 | All 15 family-specific and both multi-candidate external-oracle methods passed; TR-001, TR-009, and TR-063 are verified; TR-002 and TR-064 are rejected non-defects; TR-010 is fixed by regression. All nine Phase 1 artifact hashes match the manifest. See [Distribution Fitting](distribution-fitting.md) |
| Model estimation and diagnostics | Closed - Phase 2 | Log10-Normal estimator equivalence, fit/variance/combined influence, and external-package parity for DIC, WAIC, PSIS-LOO, MLE/MAP nuisance profiling, Hansen J, overidentified one-step fitting, fixed-weight/efficient GMM sandwich covariance, and rank-normalized R-hat/bulk-tail ESS passed. TR-023 and TR-032 retain their public signatures; TR-024 through TR-027 and TR-029 through TR-031 and TR-034 are fixed in their approved scopes. TR-028 remains an accepted documented limitation; TR-033 is rejected as a non-defect. All seven Phase 2 artifact hashes match the manifest. See [Model Estimation](model-estimation.md) |
| Data handling and Bulletin 17C | Closed - Phase 3 | TR-003 documents the accepted grouped-threshold disaggregation and most-recent-time prior reference. All seven formal worked-example GMM methods passed published mean/standard-deviation/skew parity at `1E-3`. Exact-LP3 Cohn scope guards passed fast tests; Cohn value verification is deferred. The 14 ordinary/pivotal reliability cells retain their 13,000 finite outputs with zero retries, substitutions, or exceptions. See [Bulletin 17C Verification](bulletin-17c.md), the [Phase 3 ledger](verification-finalization-plan.md#phase-3---data-handling-and-bulletin-17c), and [Scientific Review Findings](../technical-reference/review-findings.md). |
| Point-process correction | Closed - Phase 4 point-process subset | TR-004/TR-005 are complete in the approved scope. All ten guarded cells pass with 1,000-observation recovery fixtures and untouched DEMCzs defaults, including calendar/water-year block-origin parity. See [Point-Process Verification](point-process.md). |
| Mixture correction | TR-006/TR-007/TR-008 closed; initialization rerun pending | Public EM/Numerics parity remains unchanged. `MixtureAnalysis` now uses EM-seeded, prior-aware local MAP population construction with fast structural coverage. The new informative-prior method and three affected Bayesian recovery methods are `Ready - focused run`. See [Mixture Verification](mixture.md). |
| Competing-risk simulation and recovery | TR-012 closed; recovery supplement failed 6 of 20 | Fast seed/matrix contracts and four analytical rank/CDF methods pass. All ten MLE recovery methods and four Default-DEMCzs methods with MAP-centered initialization pass; six Bayesian cells expose convergence, identifiability, ESS, or uncertainty-curve findings. See [Competing-Risks Verification](competing-risks.md). |
| Composite and cross-analysis posterior propagation | TR-013/TR-014/TR-015 closed; recovery supplement failed 1 of 10 | Fast weighting/matrix/seed/cache contracts and both guarded posterior-resampling methods pass. Nine supplement methods pass; the extreme-tail inversion cell exceeds its fixed bound by `2.566838E-10`. `BivariateAnalysis` remains conditional on fixed marginals. See [Composite Verification](composite.md). |

## Evidence rule

A passing build, optimizer convergence, or reproduction of an internal result is not verification. A claim is verified only when its test has an analytical, independently implemented, external-package, or published-result oracle with declared provenance and tolerance.

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
under the execution rule. Twenty-three supplement methods and both TR-014 methods pass; seven
supplement findings remain unresolved, so Phase 5 stays gated. The supplement pins Numerics
`c361f2864428a98a33d6072ffa9bc11ac360839d` and RMC-TotalRisk
`d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7`.

[Finalization Plan](verification-finalization-plan.md) | [Methodology](methodology.md) | [References](references.md)
