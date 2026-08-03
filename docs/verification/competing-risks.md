<!-- verification-status: verified -->

# Competing-Risks Verification

This report records the focused Phase 4 evidence for TR-012. It does not claim that the complete `RMC.BestFit.Verification` project was run.

## Dependency-Aware Production Simulation

The pinned Numerics correction makes the established `CompetingRisks.GenerateRandomValues(int, int)` entry point delegate to the existing dependency-aware implementation. BestFit continues to call that public entry point, so all existing signatures and independent-mode seed behavior are preserved. The focused Numerics batch is commit `cafe6cf3837988341912a5aa8bfda444ea55ff77`.

Fast Numerics tests establish:

- the independent seed-12345 sequence is unchanged against a fixed ten-value golden sequence;
- both public simulation entry points agree for all four dependency modes;
- a non-positive-definite user matrix is rejected before sampling; and
- all 2,023 Numerics tests pass on the validated target.

BestFit fast tests exercise the production model entry point, deterministic seed repetition, dependency-mode differentiation, and correlation-matrix preflight validation.

## Analytical Rank and CDF Verification

For a bivariate Gaussian copula with latent correlation \(\rho\), the population Spearman correlation is

$$
\rho_S=\frac{6}{\pi}\arcsin\left(\frac{\rho}{2}\right), \tag{1}
$$

and for two standard-Normal marginals the probability that their maximum is at most zero is

$$
P[\max(X_1,X_2)\le 0]
=\frac14+\frac{\arcsin(\rho)}{2\pi}. \tag{2}
$$

Each verification method generates 40,000 observations with seed 24681357 through `CompetingRisksModel.GenerateRandomValues`. Separated-location maximum probes recover each latent marginal rank without duplicating the production sampling algorithm. The empirical maximum CDF is evaluated separately with equal standard-Normal marginals. Rank tolerance is the declared six-standard-error bound \(6/\sqrt{n-1}\); CDF tolerance is six binomial standard errors plus one observation.

| Exact method | Dependency oracle | Result |
|---|---|---|
| `CompetingRiskDependencyVerificationTests.Test_IndependentSimulation_MatchesRankDependenceAndCompositeCdf` | \(\rho_S=0\), \(F_{\max}(0)=0.25\) | Passed - 0.732 s |
| `CompetingRiskDependencyVerificationTests.Test_PerfectlyPositiveSimulation_MatchesRankDependenceAndCompositeCdf` | \(\rho_S=1\), \(F_{\max}(0)=0.5\) | Passed - 0.247 s |
| `CompetingRiskDependencyVerificationTests.Test_PerfectlyNegativeSimulation_MatchesRankDependenceAndCompositeCdf` | Numerics limiting \(\rho=-1+\sqrt{\epsilon}\), equations (1)-(2) | Passed - 0.309 s |
| `CompetingRiskDependencyVerificationTests.Test_CorrelationMatrixSimulation_MatchesRankDependenceAndCompositeCdf` | configured \(\rho=0.6\), equations (1)-(2) | Passed - 0.306 s |

All four methods were run separately through `scripts/run-verification-test.ps1`. Every invocation source-resolved one exact fully qualified method, built with zero warnings and errors, executed one test, and produced one passing TRX under `TestResults/VerificationFocused`.

## Disposition

TR-012 is complete. Every supported dependency mode now controls production simulation and has direct analytical rank/CDF evidence. No distribution formula, seed default, public signature, estimation setting, or verification tolerance was changed to obtain these results.

---

[Verification index](README.md) | [Technical treatment](../technical-reference/distributions/competing-risks.md) | [Scientific findings](../technical-reference/review-findings.md#tr-012)
