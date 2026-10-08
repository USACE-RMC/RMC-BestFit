# Shared Conditional Positive-Definite Conditioning Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Remove avoidable trace-scaled ridge contamination from matrices that are already symmetric positive definite while preserving the established Numerics ridge fallback for matrices that genuinely require repair.

**Architecture:** Correct the shared `Numerics.Mathematics.LinearAlgebra.MatrixRegularization.MakeSymmetricPositiveDefinite` contract. Symmetrize the candidate, return it without a ridge when the existing Cholesky decomposition accepts it, and enter the unchanged trace-scaled ridge loop only after failure. Do not add RMC.BestFit-specific conditioning logic. The common successful path still performs one Cholesky decomposition per helper call, matching the current call count; only a rejected un-ridged candidate adds one failed attempt.

**Technology:** C#, .NET multi-targeting, MSTest, Numerics linear algebra, PowerShell verification guard.

## Approval and scope boundary

- Haden Smith approved the shared Numerics behavior change on 30 August 2026.
- Preserve both dirty-worktree chains. Do not stage, commit, push, revert, discard, rename, or overwrite unrelated work.
- Do not change any ridge magnitude, escalation count, last-resort policy, Cholesky tolerance, GMM fitting weight, optimization path, B17C formula, likelihood, prior, sampler, seed, convergence rule, default, tolerance, or reference result.
- Execute only the selected non-coverage Verification methods through `scripts/run-verification-test.ps1` and inspect each TRX.
- Never execute a method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or `B17CCohnEtAlCoverageTests`, and never execute the full Verification project.
- Stop at the Chunk 8 boundary. Do not begin Chunk 9.

## Root-cause evidence

`GeneralizedMethodOfMoments.ComputeCovariance` uses `MakeSymmetricPositiveDefinite` for the moment covariance, bread, inverse bread, and final covariance. The helper currently adds `1E-10 * trace / dimension` before its first Cholesky attempt even when the symmetric candidate is already positive definite. In scale-separated real-space Pearson Type III moment matrices, the high-order-moment coordinate dominates the trace, making that unconditional ridge scientifically material to the location-coordinate covariance. Eleven independent Chunk 7 covariance cells pass; only the Pearson Type III `N=25` and `N=100` cells fail in the direction and magnitude predicted by the ridge.

The shared correction benefits all callers. Numerics has two direct consumers: Gaussian-mixture covariance repair and pivotal-bootstrap link covariance. RMC.BestFit has GMM, MLE/MAP fallback, leverage, and B17C uncertainty callers. The normal SPD path does not add a factorization: the old helper factorizes the first ridged candidate once, while the corrected helper factorizes the un-ridged candidate once and returns.

### Rejected alternatives

1. **RMC.BestFit-only conditioning.** This duplicates the SPD decision and leaves every other caller exposed to the same unnecessary perturbation.
2. **Boolean `Cholesky already failed` overload.** It saves at most one failed factorization on rare callers that already tested the exact candidate, does not reduce normal bootstrap cost, and can bypass the helper's safety invariant if misused.
3. **Loosen the independent B17C tolerance.** This weakens the scientific oracle instead of correcting the source discrepancy.

## Task 1: Pin shared no-ridge and fallback contracts

**Files:**

- Modify: `C:\GIT\Numerics\Test_Numerics\Mathematics\Linear Algebra\Test_MatrixRegularization.cs`
- Modify: `C:\GIT\Numerics\Test_Numerics\Machine Learning\Unsupervised\Test_GMM.cs`

- [x] Replace the well-conditioned base-ridge expectation with exact un-ridged equality.
- [x] Add a scale-separated `diag(1, 1E4, 1E8)` SPD regression that detects trace contamination of the smallest coordinate.
- [x] Require an asymmetric candidate with an SPD symmetric part to be symmetrized without a ridge.
- [x] Preserve the exact rank-deficient base-ridge and existing indefinite fallback contracts.
- [x] Split the Gaussian-mixture caller contract into an un-ridged well-conditioned path and a rank-deficient path that proves the returned repair is stored.
- [x] Run the changed no-ridge methods on all four Numerics frameworks before production editing and retain their failures as red evidence.

## Task 2: Add a ridge only after Cholesky failure

**Files:**

- Modify: `C:\GIT\Numerics\Numerics\Mathematics\Linear Algebra\MatrixRegularization.cs`
- Modify: `C:\GIT\Numerics\Numerics\Machine Learning\Unsupervised\GaussianMixtureModel.cs`

- [x] Add a private, documented Cholesky-acceptance helper.
- [x] Return the symmetric candidate unchanged when the existing decomposition accepts it.
- [x] Reuse the acceptance helper inside the existing ridge loop without changing ridge calculations or fallback behavior.
- [x] Update the Gaussian-mixture caller comment to describe conditional repair.
- [x] Run all changed and fallback-focused Numerics tests on all four frameworks.

## Task 3: Validate Numerics broadly

- [x] Run `dotnet build -c Release` in `C:\GIT\Numerics`; all four target frameworks passed with zero errors and warnings.
- [x] Run `dotnet test -c Release --no-build`, isolate its Australian BOM live-service failures, rerun every affected fixture/framework exactly, and run the deterministic suite with that live fixture excluded. The deterministic suite passed 2,430/2,430 on every framework; only the Cotter River upstream HTTP 500 remained on exact rerun.
- [x] Run `git diff --check`, inspect the four-file Numerics diff, and preserve the changes uncommitted.

## Task 4: Prove the RMC covariance correction

**Files:**

- Verify: `src/RMC.BestFit.Tests/ModelEstimation/GeneralizedMethodOfMomentsCovarianceScaleTests.cs`
- Verify: `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CCovarianceTests.cs`
- Verify: `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CSyntheticDataTests.cs`

- [x] Run the exact fast Pearson III covariance-scale method; the current test platform broadened the filter to the full Core project, which passed 3,375/3,375 including the target.
- [x] Build Verification without executing it.
- [x] Run all 13 current exact B17C covariance identities individually through the guarded runner and inspect each TRX.
- [x] Run all six generated-parent B17C recovery identities individually and inspect each TRX.
- [x] Keep any failure open. No tolerance, estimator, oracle, formula, or reference result changed.
- [x] Do not run any B17C confidence-interval coverage identity.

## Task 5: Check downstream scientific regressions

- [x] Run the current exact B17C penalty and example identities individually; all 24 passed from one-result TRXs.
- [x] Run the seven previously selected exact general-GMM Verification identities individually and inspect each TRX; all seven passed.
- [x] Preserve any failure as a finding rather than broadening the change; no scientific regression failed.

## Task 6: Reconcile Chunk 7 and retain the completed Chunk 8 boundary

**Files:**

- Modify only as current evidence requires: `docs/verification/bulletin-17c.md`
- Modify only as current evidence requires: `docs/verification/test-inventory.md`
- Modify only as current evidence requires: `docs/verification/verification-catalog.json`
- Modify: `docs/superpowers/plans/2026-08-28-verification-completeness-remediation.md`

- [x] Mark B17C covariance entries verified only from current exact passing TRX evidence.
- [x] Keep the independent covariance oracle and acceptance tolerances unchanged.
- [x] Recompute declaration, execution-unit, status, and gap totals through the default validator.
- [x] Preserve the already completed Chunk 8 evidence and stop before Chunk 9.

## Task 7: Run RMC repository regression gates

- [x] Run serially:

```powershell
dotnet test src/RMC.BestFit.Tests -c Debug
dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build
dotnet build src/RMC.BestFit.Verification -c Debug
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
.\scripts\test-verification-catalog-validator.ps1
.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification
git diff --check
git status --short
```

- [x] Run the repository's JSON Schema validation for `verification-catalog.json`.
- [x] Directly scan untracked task files for trailing whitespace and final newlines.
- [x] Review task-scoped diffs in both repositories and confirm nothing was staged, committed, pushed, reverted, or discarded.
