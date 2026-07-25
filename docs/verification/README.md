<!-- verification-status: draft -->

# RMC.BestFit 2.0 Verification Report

This is the living formal verification and validation record for `RMC.BestFit.Verification`. It separates scientific numerical evidence from fast unit and regression coverage.

## Current status

| Program area | Status | Evidence |
|---|---|---|
| Repository integration | Validated 24 July 2026 | Debug Verification rebuild: 0 warnings/errors; Release solution log: 0 Verification project references |
| Public API baseline | Captured | Exact exported type/member/enum baseline enforced by `PublicApiCompatibilityTests` |
| Fast regression gate | Passed | Core 3,032; UI 564; App 428; 0 failed and 0 skipped |
| External environments | Locked | R 4.4.3 with 131 packages; Python with 15 packages |
| Test ownership audit | In progress | Initial duplicate removals and FittingAnalysis split are recorded in [Test Inventory](test-inventory.md) |
| Distribution fitting | Passed | All 15 family-specific and both multi-candidate external-oracle methods passed; TR-001, TR-009, and TR-063 are verified; TR-002 and TR-064 are rejected non-defects; TR-010 is fixed by regression. See [Distribution Fitting](distribution-fitting.md) |
| Model estimation and diagnostics | In progress | Log10-Normal estimator equivalence and fit/variance/combined influence passed; R `gmm` parity fixes TR-065 and terminology fixes TR-031; TR-033 is rejected as a non-defect. Pareto-k/LOO, model comparison, profiles, Hansen J, and remaining diagnostics remain active. See [Model Estimation](model-estimation.md) |
| Bulletin 17C and later phases | Planned | [Scientific Review Findings](../technical-reference/review-findings.md) |
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

[Methodology](methodology.md) | [References](references.md)
