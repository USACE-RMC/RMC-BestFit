# Identifiable Competing-Risk Recovery Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use `superpowers:executing-plans` to implement this plan. Use `superpowers:test-driven-development` for test/helper changes, `superpowers:systematic-debugging` for every failure, and `superpowers:verification-before-completion` before any completion claim.

**Goal:** Replace the oversized, poorly identified competing-risk recovery matrix with five clearly identified BestFit recovery methods and three matching Numerics MLE sanity checks, while preserving all production scientific contracts.

**Architecture:** Keep production generation, likelihoods, optimizers, priors, and samplers unchanged. Add Verification-only fixture diagnostics that reproduce labeled generation, quantify hard and soft component information, and reject an experiment before fitting unless it has enough cause balance and interior dog-leg behavior. Use the full competing-risk likelihood for MLE uncertainty and retain Bayesian coordinate intervals plus R-hat/ESS for both independent designs. The maximum Verification fixture disables only the optional Jeffreys scale multiplier because its default-prior diagnostic selected a disappeared-component boundary mode; bounded parameter priors and the production default remain unchanged. Mirror only the three MLE fixtures in Numerics because its MLE machinery differs from BestFit's.

**Technology:** C#/.NET, MSTest, Numerics distributions and random generators, BestFit recovery helpers, PowerShell guarded Verification runner, JSON catalog and Markdown verification documentation.

**Execution constraints:** Work directly in the existing dirty checkouts. Do not create a branch/worktree; stage, commit, push, revert, discard, move, or rename files; or alter the existing four-file Numerics Chunk 7 diff. Do not change production code in either repository. Run Verification methods only through `scripts/run-verification-test.ps1`, individually and serially. Do not run the full Verification project, Bulletin 17C confidence-interval coverage methods, or Chunk 11A methods.

---

### Task 1: Freeze the reduced matrix with failing BestFit design assertions

**Files:**
- Create: `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/CompetingRiskRecoveryDesign.cs`
- Create: `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/CompetingRiskDesignDiagnostics.cs`
- Modify: `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/CompetingRiskRecoveryTests.Helpers.cs`

**Step 1: Define the three immutable verification fixtures**

Move fixture construction out of the oversized helper into `CompetingRiskRecoveryDesign.cs`. Define exactly:

- independent minimum: Weibull(50, 1), Weibull(80, 3);
- independent maximum: Weibull(100, 3), Gumbel(80, 20);
- fixed-correlation minimum: Weibull(50, 1), Weibull(80, 3), rho=0.6.

Each fixture must declare operation, dependency, component names/order, generating parameters/order, fixed seed `12345`, `RecoveryDesign.SampleSize`, minimum theoretical/realized contribution, minimum dominance mass, acceptable dog-leg composite-probability range, and whether Bayesian execution is authorized.

**Step 2: Add diagnostic assertions before implementing them**

Make `PrepareRecovery` require these fixture diagnostics before returning a sample:

- every theoretical winner share is at least 0.15;
- every hard winner count is at least 100;
- every component dominance mass is at least 0.10;
- K-1 ordered interior responsibility crossovers lie in [0.10, 0.90], while every additional
  extreme-tail re-entry crossover remains recorded;
- the labeled diagnostic sample exactly equals production generation at all 1,000 observations;
- parent parameters lie inside fitted bounds/priors.

Build Verification without execution and confirm the new helper references fail until diagnostic implementation exists:

```powershell
dotnet build src/RMC.BestFit.Verification -c Debug
```

Expected: compilation fails only on the intentionally missing diagnostic members.

**Step 3: Implement labeled generation and responsibility diagnostics**

In `CompetingRiskDesignDiagnostics.cs`:

- reproduce independent latent draws using one `MersenneTwister(12345)` stream and component inverse CDFs in production order;
- reproduce the fixed-correlation latent normal transform using the declared 2x2 correlation matrix;
- record latent component values, hard winner index, theoretical cause shares, observed soft counts `sum(r_i(y_n))`, dominance probability mass, and responsibility crossover probabilities;
- use independent min/max cause-density formulas and the bivariate conditional-Gaussian formula for fixed-correlation minimum;
- compare the labeled composite sequence with `GenerateRandomValues(1000, 12345)` before accepting labels.

Keep all new types internal/private to Verification and fully XML-document every class and method.

**Step 4: Rebuild without execution**

```powershell
dotnet build src/RMC.BestFit.Verification -c Debug
```

Expected: zero warnings and zero errors.

### Task 2: Replace the BestFit estimator matrix and normalize coordinate acceptance

**Files:**
- Modify: `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/CompetingRiskRecoveryTests.cs`
- Modify: `src/RMC.BestFit.Verification/Univariate/CompetingRiskTests/CompetingRiskRecoveryTests.Helpers.cs`
- Modify if needed: `src/RMC.BestFit.Verification/Recovery/RecoveryAcceptance.cs`

**Step 1: Replace twenty methods with the four finalized identities**

Retain exactly:

```text
MLE_Minimum_TwoWeibullDogLeg_RecoversParent
Bayesian_Minimum_TwoWeibullDogLeg_RecoversParent
MLE_Maximum_WeibullGumbelDogLeg_RecoversParent
MLE_Minimum_CorrelatedTwoWeibullDogLeg_RecoversParent
```

Do not add a correlated Bayesian method.

**Step 2: Make MLE coordinate recovery the primary oracle**

For every fitted component coordinate:

- evaluate the full data log-likelihood around the fitted optimum;
- calculate the observed-information Hessian with parameter-scaled finite differences;
- invert only a symmetric positive-definite information matrix;
- report matrix rank/conditioning failures as scientific findings, not as a ridge-tuning opportunity;
- require `abs(fitted - truth) / standardError <= 1.96` or an equivalent central 95 percent profile interval.

Do not use hard winner count as covariance. Include theoretical share, hard wins, soft event count, dominance mass, and crossover locations in assertion messages.

**Step 3: Retain strict Bayesian coordinate recovery for both independent designs**

For each monitored coordinate require:

- generating truth inside its central 95 percent posterior interval;
- R-hat below 1.10;
- MCMC ESS at least 100;
- declared component order preserved.

Keep the composite response-band check as secondary corroboration. Preserve all production priors, sampler settings, estimator seeds, convergence rules, and defaults. For the independent maximum Verification cell only, set `UseJeffreysRuleForScale = false` while retaining its bounded parameter priors.

**Step 4: Build without execution**

```powershell
dotnet build src/RMC.BestFit.Verification -c Debug
```

Expected: zero warnings and zero errors.

### Task 3: Replace Numerics recovery fixtures with the three matching MLE designs

**Files:**
- Modify: `C:/GIT/Numerics/Test_Numerics/Distributions/Univariate/Test_CompetingRisks.cs`
- Do not modify: `C:/GIT/Numerics/Numerics/Distributions/Univariate/CompetingRisks.cs`

**Step 1: Remove the redundant or poorly identified estimator fixtures**

Remove the two generic MLE smoke tests and eight current matrix recovery methods. Preserve analytical CDF/PDF, dependency simulation, cache invalidation, seed, clone, and serialization tests.

**Step 2: Add the three matching MLE recovery methods**

Add exactly:

```text
Test_MLE_MinRule_2Dist_Weibull_DogLeg
Test_MLE_MaxRule_2Dist_Weibull_Gumbel_DogLeg
Test_MLE_CorrelatedMinRule_2Dist_Weibull_DogLeg
```

Use `N=1000`, seed `12345`, and the same parent/component order as BestFit. Add local test-only design diagnostics or a shared test helper in this file so the three fixtures assert the same theoretical-share, hard-win, dominance, and crossover gates before calling `CompetingRisks.MLE`.

Replace arbitrary 25/30 percent relative tolerances with central-95-percent coordinate acceptance from the full Numerics competing-risk likelihood. If reliable observed-information inversion is not possible for a fixture, preserve the failure and stop for Haden rather than changing a production constraint or accepting an arbitrary tolerance.

**Step 3: Run each new Numerics MLE identity on all target frameworks**

Run serially for `net481`, `net8.0`, `net9.0`, and `net10.0`:

```powershell
foreach ($framework in @('net481', 'net8.0', 'net9.0', 'net10.0')) {
    dotnet test Test_Numerics/Test_Numerics.csproj -c Debug -f $framework --filter "FullyQualifiedName~Test_MLE_MinRule_2Dist_Weibull_DogLeg"
    dotnet test Test_Numerics/Test_Numerics.csproj -c Debug -f $framework --filter "FullyQualifiedName~Test_MLE_MaxRule_2Dist_Weibull_Gumbel_DogLeg"
    dotnet test Test_Numerics/Test_Numerics.csproj -c Debug -f $framework --filter "FullyQualifiedName~Test_MLE_CorrelatedMinRule_2Dist_Weibull_DogLeg"
}
```

Require exactly one executed result for every command and preserve failures for systematic diagnosis.

### Task 4: Execute the exact BestFit identities and reconcile failures only

**Files:**
- Modify only test/oracle files if a failure reveals an implementation defect in the new diagnostics or acceptance.
- Do not modify production algorithms, fixtures, seeds, priors, sampler settings, tolerances, or convergence rules to clear a realization.

**Step 1: Run MLE methods individually and serially**

```powershell
.\scripts\run-verification-test.ps1 -Test 'RMC.BestFit.Verification.Univariate.CompetingRiskTests.CompetingRiskRecoveryTests.MLE_Minimum_TwoWeibullDogLeg_RecoversParent'
.\scripts\run-verification-test.ps1 -Test 'RMC.BestFit.Verification.Univariate.CompetingRiskTests.CompetingRiskRecoveryTests.MLE_Maximum_WeibullGumbelDogLeg_RecoversParent'
.\scripts\run-verification-test.ps1 -Test 'RMC.BestFit.Verification.Univariate.CompetingRiskTests.CompetingRiskRecoveryTests.MLE_Minimum_CorrelatedTwoWeibullDogLeg_RecoversParent'
```

After each command, inspect its isolated TRX and require exactly one executed result.

**Step 2: Run both retained independent Bayesian methods individually**

```powershell
.\scripts\run-verification-test.ps1 -Test 'RMC.BestFit.Verification.Univariate.CompetingRiskTests.CompetingRiskRecoveryTests.Bayesian_Minimum_TwoWeibullDogLeg_RecoversParent'
.\scripts\run-verification-test.ps1 -Test 'RMC.BestFit.Verification.Univariate.CompetingRiskTests.CompetingRiskRecoveryTests.Bayesian_Maximum_WeibullGumbelDogLeg_RecoversParent'
```

Inspect each isolated TRX and require exactly one executed result. The earlier default-prior
Bayesian maximum TRX remains failure evidence for the collapsed-boundary posterior and must not be
transferred to the current non-Jeffreys fixture. Do not execute any correlated Bayesian recovery.

**Step 3: Diagnose only current failures**

Use the systematic-debugging workflow. Check design gates, parameter order, prior support, parent-versus-collapsed likelihood, observed-information calculation, label order, posterior interval extraction, R-hat, and MCMC ESS. A failure may authorize a test/oracle implementation correction, but not production or scientific-contract changes. Stop and present evidence before any such change.

### Task 5: Reconcile the catalog and competing-risk documentation

**Files:**
- Modify: `docs/verification/verification-catalog.json`
- Modify: `docs/verification/competing-risks.md`
- Modify: `docs/verification/test-inventory.md`
- Modify: `docs/verification/report/competing-risk-analysis.md`
- Modify: `docs/verification/verification-completeness-audit.md` if its current counts/claims change
- Modify: `docs/superpowers/plans/2026-08-28-verification-completeness-remediation.md`
- Modify: `verification/data/MANIFEST.md` only if an independent artifact is added

**Step 1: Replace historical recovery identities**

Remove the sixteen discarded BestFit estimator identities from current catalog ownership and add the five exact retained identities. Do not transfer historical pass evidence across renames or prior configurations.

**Step 2: Record design and evidence**

Document the three retained fixture crosswalks, theoretical and realized shares, hard and soft effective counts, dog-leg crossovers, estimator matrix, the rejected three-component candidate, MLE uncertainty source, Bayesian diagnostics, and explicit exclusion of correlated Bayesian MCMC.

Mark an identity verified only if its current exact TRX contains exactly one passing result. Preserve every failure as an open finding.

**Step 3: Run catalog/schema checks**

```powershell
.\scripts\test-verification-catalog-validator.ps1
.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification
```

Run the repository's existing JSON Schema validation command discovered in the master plan. Do not use `-RequireComplete`.

### Task 6: Complete scoped regression validation

**Files:**
- Review all task-scoped modified/untracked files in both repositories.

**Step 1: BestFit validation**

Run serially:

```powershell
dotnet test src/RMC.BestFit.Tests -c Debug
dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build
dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build
dotnet build src/RMC.BestFit.Verification -c Debug
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
.\scripts\test-verification-catalog-validator.ps1
.\scripts\validate-verification-catalog.ps1 -Catalog .\docs\verification\verification-catalog.json -SourceRoot .\src\RMC.BestFit.Verification
git diff --check
```

Scan every new untracked file directly for trailing whitespace and a final newline because `git diff --check` does not cover it.

**Step 2: Numerics validation**

Run the three exact changed recovery methods on all four target frameworks, then:

```powershell
dotnet build Numerics.sln -c Release
```

Run the established deterministic regression sweep on net481, net8.0, net9.0, and net10.0 while excluding the live Australian BOM download fixture. Treat any live-network behavior separately. Do not change the existing Gaussian-mixture or matrix-regularization work.

**Step 3: Final dirty-state and evidence review**

In both repositories:

- run `git status --short`;
- confirm `git diff --cached --stat` and `git diff --cached` are empty;
- inspect task-scoped unstaged diffs;
- confirm the approved `MatrixRegularization.MakeSymmetricPositiveDefinite` policy remains intact;
- confirm no production competing-risk file changed.

Report exact BestFit and Numerics pass/fail lists, every TRX result directory, replaced historical identities, final catalog totals, all validation outcomes, unresolved identification/conditioning findings, and the explicit confirmation that no full Verification suite, Bulletin 17C coverage method, correlated Bayesian recovery, or Chunk 11A method ran.
