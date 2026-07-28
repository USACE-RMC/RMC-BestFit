<!-- verification-status: draft -->

# RMC.BestFit 2.0 Verification Report

This is the living formal verification and validation record for `RMC.BestFit.Verification`. It separates scientific numerical evidence from fast unit and regression coverage.

The active handoff and batching plan is maintained in [Verification Finalization Plan](verification-finalization-plan.md). Start there when continuing this program in a new session.

## Current status

| Program area | Status | Evidence |
|---|---|---|
| Repository integration | Validated 24 July 2026 | Debug Verification rebuild: 0 warnings/errors; Release solution log: 0 Verification project references |
| Public API baseline | Captured | Exact exported type/member/enum baseline enforced by `PublicApiCompatibilityTests` |
| Fast regression gate | Passed 28 July 2026 | Core 3,057; UI 564; App 428; 0 failures |
| External environments | Locked | R 4.4.3 with 131 packages; Python with 15 packages |
| Test ownership audit | In progress | Initial duplicate removals and FittingAnalysis split are recorded in [Test Inventory](test-inventory.md) |
| Distribution fitting | Closed - Phase 1 | All 15 family-specific and both multi-candidate external-oracle methods passed; TR-001, TR-009, and TR-063 are verified; TR-002 and TR-064 are rejected non-defects; TR-010 is fixed by regression. All nine Phase 1 artifact hashes match the manifest. See [Distribution Fitting](distribution-fitting.md) |
| Model estimation and diagnostics | Closed - Phase 2 | Log10-Normal estimator equivalence, fit/variance/combined influence, and external-package parity for DIC, WAIC, PSIS-LOO, MLE/MAP nuisance profiling, Hansen J, overidentified one-step fitting, fixed-weight/efficient GMM sandwich covariance, and rank-normalized R-hat/bulk-tail ESS passed. TR-023 and TR-032 retain their public signatures; TR-024 through TR-027 and TR-029 through TR-031 and TR-034 are fixed in their approved scopes. TR-028 remains an accepted documented limitation; TR-033 is rejected as a non-defect. All seven Phase 2 artifact hashes match the manifest. See [Model Estimation](model-estimation.md) |
| Bulletin 17C / Phase 3 | TR-016 through TR-021 closed in approved scopes | All seven formal worked-example GMM methods passed published mean/standard-deviation/skew parity at `1E-3`. Exact-LP3 Cohn scope guards passed fast tests; Cohn value verification is deferred. The 14 ordinary/pivotal reliability cells retain their 13,000 finite outputs with zero retries, substitutions, or exceptions. See [Bulletin 17C Verification](bulletin-17c.md), the [Phase 3 ledger](verification-finalization-plan.md#phase-3---data-handling-and-bulletin-17c), and [Scientific Review Findings](../technical-reference/review-findings.md). |

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

[Finalization Plan](verification-finalization-plan.md) | [Methodology](methodology.md) | [References](references.md)
