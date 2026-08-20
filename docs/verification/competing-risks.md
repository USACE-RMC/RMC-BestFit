<!-- verification-status: draft -->

# Competing-Risks Verification

This report records the focused Phase 4 evidence for TR-012. It does not claim that the complete `RMC.BestFit.Verification` project was run.

## Dependency-Aware Production Simulation

The pinned Numerics correction makes the established `CompetingRisks.GenerateRandomValues(int, int)` entry point delegate to the existing dependency-aware implementation. BestFit continues to call that public entry point, so all existing signatures and independent-mode seed behavior are preserved. The focused Numerics batch is commit `cafe6cf3837988341912a5aa8bfda444ea55ff77`.

Fast Numerics tests establish:

- the independent seed-12345 sequence is unchanged against a fixed ten-value golden sequence;
- both public simulation entry points agree for all four dependency modes;
- a non-positive-definite user matrix is rejected before sampling; and
- all 2,024 Numerics tests pass on the validated .NET 10 target.

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

## Recovery Supplement - Focused Results

The source-audited recovery supplement pins Numerics commit
`c361f2864428a98a33d6072ffa9bc11ac360839d`, specifically
`Test_Numerics/Distributions/Univariate/Test_CompetingRisks.cs`. It reproduces all eight
two- and three-component minimum/maximum fixtures through both BestFit MLE and Bayesian
analysis, then adds two fixed-correlation cases. After the authorized MAP-initialization change,
all 20 methods were rerun individually through the guarded runner. Fourteen passed and six
Default-DEMCzs methods exposed supplemental findings. On 20 August 2026, the technical authority
approved deferring those six findings for separate research without changing production behavior;
they no longer block Phase 5 and were not rerun for that disposition.

BestFit deliberately limits competing-risk models and analyses to one through three component
distributions as an identifiability guard, matching the mixture-analysis limit. Constructor,
model-validation, and analysis-validation unit tests cover rejection of a fourth component; the
recovery supplement therefore exercises the full supported component-count range without adding
an unsupported four-component fixture.

Every fixture generates through `CompetingRisksModel.GenerateRandomValues` with seed 12345.
Two-component cases use 1,000 observations and three-component cases use 1,500. A fresh
`CompetingRisksModel` preserves the selection rule, component family and distribution base,
dependency mode, and fixed correlation matrix. Before fitting, the BestFit exact-data
`DataLogLikelihood` must equal the flattened parent Numerics log likelihood at absolute
tolerance `1E-10`.

MLE is constructed with the unmodified `MaximumLikelihood` Differential Evolution default.
Bayesian recovery is constructed through `CompetingRiskAnalysis` without assigning any
simulation, advanced-sampler, output, point-estimator, interval, or seed property. Each method
asserts the resolved production defaults before and after sampling and confirms that the completed
sampler retains both the configured output count and the analysis-specific MAP population:

- DEMCzs, simulation defaults enabled, and advanced defaults enabled;
- 3,500 sampled iterations, 1,750 warmup iterations, 10,000 retained outputs, 90% intervals,
  posterior mean, and seed 12345;
- for 4/5/6 parameters respectively, 8/10/12 chains, thinning 40/50/60, and initial population
  length 400/500/600; and
- jump `2.38/sqrt(2d)`, jump and snooker thresholds 0.1, and proposal noise `1E-12`.

`CompetingRiskAnalysis` now changes only the initialization stage. It runs the production
Differential Evolution MAP estimator with its random seed synchronized to the Bayesian seed,
computes the bounded posterior Hessian, and draws the sampler's full initial population from a
multivariate Normal centered at the MAP with the established mixture-analysis covariance factor
`1.5`. Nonfinite draws receive at most 20 replacement draws, and the best finite population
members seed the chains. A singular information matrix uses an initialization-only regularized
Moore-Penrose covariance, which anchors its null directions at the MAP; the public MAP covariance
contract continues to report that singular covariance as unavailable. Any MAP, covariance, or
population failure resets the sampler and falls back to randomized initialization. Every Bayesian
recovery method that reaches its assertions requires the sampler to remain `UserDefined`, so a
silent fallback cannot count as validation of this change. All DEMCzs sampling, proposal, output,
interval, point-estimator, and seed defaults remain unchanged.

Both estimators require successful estimation and finite parameters. The maximum absolute
parent-versus-fitted CDF error is evaluated at the sorted sample's empirical quantile locations
0.01 through 0.99. MLE uses its fitted point distribution. Bayesian recovery averages the
combined CDF over all 10,000 retained draws, which preserves the label-invariant identifiable
target for the same-family fixtures; its default posterior-mean point distribution is separately
checked for finite parameters. Approved Bayesian component gates order the exchangeable pair
within each retained draw before averaging the ordered ranks, so label switching cannot distort
the comparison. The bound is `0.05` for ordinary
two-component fixtures and `0.06` for three-component or correlated fixtures. Bayesian output
additionally requires finite split
R-hat below `1.1` and conservative ESS above `100` for every parameter. Component parameters
are gated only where the Numerics fixture establishes identification: the sorted shapes of
Weibull(30, 0.8) plus Weibull(100, 3) must be within 25%, and the sorted means of Normal(50, 8)
plus Normal(85, 12) must be within 30%. All other fixtures gate the identifiable combined CDF.

| Fixture | MLE method and result | Default-DEMCzs method and result |
|---|---|---|
| Minimum: Weibull(50, 1) + Weibull(80, 3) | `MLE_Minimum_TwoWeibullConstantIncreasing_RecoversParent` - Passed, 3.488 s | `Bayesian_Minimum_TwoWeibullConstantIncreasing_RecoversParent` - Passed, 3:21.394 |
| Minimum: Weibull(30, 0.8) + Weibull(100, 3) | `MLE_Minimum_TwoWeibullContrastingShapes_RecoversParent` - Passed, 2.487 s | `Bayesian_Minimum_TwoWeibullContrastingShapes_RecoversParent` - Passed, 2:52.558 |
| Minimum: Weibull(20, 0.7) + Weibull(200, 1) + Weibull(150, 4) | `MLE_Minimum_ThreeWeibullBathtub_RecoversParent` - Passed, 39.592 s | `Bayesian_Minimum_ThreeWeibullBathtub_RecoversParent` - Passed, 5:37.609 |
| Minimum: Weibull(15, 0.5) + Weibull(60, 1.5) + Weibull(120, 4) | `MLE_Minimum_ThreeWeibullSeparatedShapes_RecoversParent` - Passed, 16.884 s | `Bayesian_Minimum_ThreeWeibullSeparatedShapes_RecoversParent` - Passed, 5:49.246 |
| Maximum: Normal(50, 8) + Normal(85, 12) | `MLE_Maximum_TwoSeparatedNormals_RecoversParent` - Passed, 8.474 s | `Bayesian_Maximum_TwoSeparatedNormals_RecoversParent` - Failed, 2:34.639; draw-ordered lower mean missed by 1,086.09% |
| Maximum: Weibull(50, 2) + Gumbel(70, 15) | `MLE_Maximum_WeibullAndGumbel_RecoversParent` - Passed, 2.276 s | `Bayesian_Maximum_WeibullAndGumbel_RecoversParent` - Failed, 1:40.019; scale R-hat 1.10899 |
| Maximum: Normal(40, 6) + Normal(70, 8) + Normal(100, 10) | `MLE_Maximum_ThreeSeparatedNormals_RecoversParent` - Passed, 5.581 s | `Bayesian_Maximum_ThreeSeparatedNormals_RecoversParent` - Failed, 6:02.620; standard-deviation R-hat 1.44785 |
| Maximum: Exponential(0.05) + Gamma(3, 15) + natural-base LogNormal(4.2, 0.4) | `MLE_Maximum_ThreeDifferentFamilies_RecoversParent` - Passed, 14.076 s | `Bayesian_Maximum_ThreeDifferentFamilies_RecoversParent` - Failed, 6:16.455; Gamma inverse-CDF arithmetic exception during uncertainty-curve construction |
| Correlated minimum: Weibull(50, 1) + Weibull(80, 3), latent rho 0.6 | `MLE_Minimum_CorrelatedTwoWeibulls_RecoversParent` - Passed, 26.354 s | `Bayesian_Minimum_CorrelatedTwoWeibulls_RecoversParent` - Failed, 28:48.056; scale ESS 77.5203 |
| Correlated maximum: Normal(50, 10) + Normal(65, 12), latent rho 0.6 | `MLE_Maximum_CorrelatedTwoNormals_RecoversParent` - Passed, 12.083 s | `Bayesian_Maximum_CorrelatedTwoNormals_RecoversParent` - Failed, 17:32.062; standard-deviation R-hat 1.17331 |

The first mixed-family MLE run exposed that Numerics `LogNormal.Clone()` dropped a configured
natural logarithm base. Numerics commit `e57af20` preserves `Base` and adds clone-contract tests
for both `LogNormal` and the already-correct `LogPearsonTypeIII`; all 2,072 Numerics tests passed
on each supported target framework. The mixed-family MLE then passed through BestFit's native
clone path. No BestFit-side base-copy workaround remains.

The methods live in
`Univariate/CompetingRiskTests/CompetingRiskRecoveryTests.cs` and its helper partial. The
authorized MAP-centered initialization and bounded-Hessian correction were implemented before
this rerun. No prior, DEMCzs sampling default, seed, likelihood, acceptance tolerance, or fixture
was changed in response to the results.

## Disposition

TR-012 remains complete. Every supported dependency mode controls production simulation and has
direct analytical rank/CDF evidence. The recovery supplement completed all 20 exact focused runs.
Its six Default-DEMCzs findings are explicitly deferred, nonblocking research items covering
separated-component/aggregate identification, heterogeneous ridges, correlated-dependence R-hat
or ESS, and Gamma inverse-CDF uncertainty postprocessing. MAP initialization resolved the former
separated three-Weibull R-hat finding. No distribution formula, seed default, public signature,
DEMCzs sampling setting, fixture, or verification tolerance changed in response to a failed cell
or in granting the deferral.

---

[Verification index](README.md) | [Technical treatment](../technical-reference/distributions/competing-risks.md) | [Scientific findings](../technical-reference/review-findings.md#tr-012)
