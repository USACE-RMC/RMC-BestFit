<!-- verification-status: finalized -->

# Bivariate and Coincident-Frequency Verification

This chapter consolidates the Phase 6 (Batch 6.2) verification evidence for copula-based bivariate
distributions, Bayesian bivariate analysis, and coincident-frequency analysis. No review finding is
open in this area: TR-047 (bivariate AIC/BIC posterior kernel) was corrected and closed in Phase 2,
and TR-014 (cross-analysis posterior draw coupling) was closed in Phase 4. The technical treatment is
in the [bivariate](../technical-reference/analysis/bivariate.md) and
[coincident-frequency](../technical-reference/analysis/coincident-frequency.md) chapters.

## Status

| Claim | Evidence | State |
|---|---|---|
| Copula maximum pseudo-likelihood and inference-from-margins estimation (seven families) | `CopulaEstimationOracleTests` (14 exact methods) against the independent `copula-estimation-oracle.json` optimum; historical R `copula` targets retained for the original six families | Passed 14/14 fresh (1 September 2026) |
| Generated-parent conditional copula recovery | Six `BivariateAnalysisParameterRecoveryTests` identities under production DEMCzs defaults, plus one Student-t MLE identity | Passed 7/7 (31 August 2026) |
| Coincident-frequency response surface | Three analytical Normal-sum cells plus one N=1000 nonlinear Lognormal response recovery | Passed 4/4 (31 August 2026) |
| Independent product-posterior propagation (TR-014) | `PosteriorResamplingVerificationTests.CoincidentFrequencyPosteriorResampling_MatchesIndependentClosedFormOracle` | Passed (3 August 2026) |
| AIC/BIC use the copula data likelihood at the stored MAP (TR-047) | Fast routing regression `AnalysisInformationCriteriaRoutingTests.BivariateCriteria_UseOneDataLikelihoodCallAtMap` | Passed (fast gate) |

## Chunk 11 identification and ownership design

Every generated-parent bivariate recovery cell uses exactly 1,000 matched pairs. The physical
marginals are `X ~ Normal(mu=100, sigma=15)` and `Y ~ Normal(mu=80, sigma=25)`, in X-then-Y
coordinate order. Each marginal is fitted separately by maximum likelihood in `[mu, sigma]` order;
its generating coordinates are judged with the Normal distribution's maximum-likelihood covariance
at N=1000. The fitted marginal distributions are then held fixed while the conditional copula is
estimated. Marginal MLE uncertainty is therefore not described as posterior uncertainty and is not
propagated into either the copula posterior intervals or the Student-t conditional MLE covariance.

| Family | Parent copula coordinates | Generator seed | Estimator and support | Identified coordinates and response |
|---|---|---:|---|---|
| Ali-Mikhail-Haq | `[theta=0.8]` | 13050 | Bayesian; Numerics AMH constraint, approximately `(-1, 1)` | `theta`; parent likelihood must exceed independence |
| Clayton | `[theta=1.5]` | 13049 | Bayesian; Numerics Clayton constraint | `theta`; lower-tail dependence is implied by the identified coordinate |
| Frank | `[theta=8]` | 13048 | Bayesian; positive branch selected from sample Kendall tau | `theta`; parent likelihood must exceed the near-independence boundary |
| Gumbel | `[theta=2]` | 13047 | Bayesian; `[1, 100]` | `theta`; upper-tail dependence is implied by the identified coordinate |
| Joe | `[theta=3]` | 13046 | Bayesian; `[1, 100]` | `theta`; upper-tail dependence is implied by the identified coordinate |
| Gaussian | `[rho=0.8]` | 13045 | Bayesian; approximately `(-1, 1)` | `rho`; parent likelihood must exceed `rho=0` |
| Student t | `[rho=0.8, nu=4]` | 13051 | MLE; `rho` approximately `(-1, 1)`; `2+1e-10 <= nu <= 30` | observed-information `rho`; weak `nu` is evaluated only through symmetric tail dependence |

The six one-coordinate copula posterior coordinates use the unchanged DEMCzs configuration and seed
12345, a central 95% interval, R-hat below 1.10, and ESS at least 100. Haden Smith directed removal of
the Student-t MCMC identity because repeated beta-function evaluation made that realization
impractical. Its replacement uses production Differential Evolution with untouched default tolerances, requires an unregularized
observed-information covariance, judges `rho` by absolute standardized error no greater than 1.96,
and applies the same standardized-error threshold to the closed-form symmetric tail-dependence
response via the full covariance delta method. It makes no raw recovery claim for weak `nu`.
Every cell first checks support and parent-versus-independence likelihood discrimination. The
separate external artifact owns both maximum-pseudo-likelihood and inference-from-margins parity.

## Coincident-frequency coverage matrix

The three retained analytical cells and one nonlinear recovery cell form the minimal interaction
matrix. Repeating every correlation sign for the nonlinear transformation would add runtime without
a new response mechanism.

| Response | Parent design | Oracle and uncertainty separation | Scientific interaction |
|---|---|---|---|
| `Z=X+Y`, `rho=0` | N=1000 pairs, seed 12345 | Exact Normal-sum law conditional on fitted Normal marginals and fitted `rho`; point response only | independence and linear additivity |
| `Z=X+Y`, `rho=0.5` | N=1000 pairs, seed 12345 | Same exact law | positive dependence in a linear response |
| `Z=X+Y`, `rho=-0.5` | N=1000 pairs, seed 12345 | Same exact law | negative dependence in a linear response |
| `Z=exp(0.01X+0.01Y)`, `rho=0.5` | N=1000 pairs, generator seed 13055; `X ~ Normal(100,15)`, `Y ~ Normal(80,25)` | Exact Lognormal law. Response-table numerical error is bounded separately at 0.015 AEP. Marginal MLE uncertainty uses 2,000 independent asymptotic Normal-MLE draws (seeds 24680/24681), explicitly not posterior draws; copula uncertainty uses retained DEMCzs draws with central-95% parent inclusion, R-hat below 1.10, and ESS at least 100. The response uses the minimum available chain length, and the parent AEP must be inside the central 95% propagated band at nonexceedance 0.10, 0.25, 0.50, 0.75, and 0.90. | monotone nonlinear transformation, parameter-fitting propagation, and response prediction |

For the nonlinear parent, `log(Z)` has mean `0.01(mu_X+mu_Y)` and variance
`0.01^2(sigma_X^2+sigma_Y^2+2 rho sigma_X sigma_Y)`. This closed form is independent of the
production response-table constructor. Numerical response integration, parent fitting, and propagated
predictive uncertainty therefore have separate acceptance checks; no fitted empirical response is used
as the scientific oracle. No frozen nonlinear artifact is needed because the complete oracle is
analytical and reproduced directly from the predeclared inputs.

## Copula estimation oracle

`BivariateDistributionMLETests` retains the twelve embedded fixture bodies (one hundred paired
observations each) as provenance only. Their historical `1e-3` coordinate assertions were removed
from discovery because the tolerance was not statistically derived and the R package version was
never recorded. The retained evidence is `CopulaEstimationOracleTests`, which covers maximum
pseudo-likelihood (Weibull complements `rank/(n + 1)`) and inference from margins. The generator
`verification/python/bivariate/generate_copula_estimation_oracle.py` (SHA-256
`358dca1910f1091e1f9f07978f38662444575f9a0373e39cd31189c37918307b`) transcribes those fixtures and
adds a NumPy-PCG64 seed-20260830 Student-t sample of exactly 1,000 pairs with parent `[rho=0.8, nu=4]`,
`X ~ Normal(100,15)`, and `Y ~ Normal(80,25)`. Python 3.12.13, NumPy 2.3.5, and SciPy 1.18.1 implement
the Student-t density in physical `[rho, nu]` order, self-check it against SciPy's bivariate-t over
univariate-t density ratio (maximum relative error `8.5e-16`), and use deterministic differential
evolution followed by L-BFGS-B. The generated `verification/data/bivariate/copula-estimation-oracle.json`
has SHA-256 `28edfbd28e392df1ac540766f3ba5c3f1ce57d8a442facbcd6541866f683956e`.

| Fixture | Method | Independent optimum | Historical R target | Difference |
|---|---|---|---|---|
| Ali-Mikhail-Haq | MPL | 0.8321521 | 0.8321504 | `+1.8e-6` |
| Ali-Mikhail-Haq | IFM | 0.8392378 | 0.8392506 | `-1.3e-5` |
| Clayton | MPL | 1.5340162 | 1.5340160 | `+1.9e-7` |
| Clayton | IFM | 1.4852343 | 1.4851670 | `+6.7e-5` |
| Frank | MPL | 7.7187608 | 7.7187610 | `-2.2e-7` |
| Frank | IFM | 8.1303210 | 8.1302590 | `+6.2e-5` |
| Gumbel | MPL | 2.0979531 | 2.0979530 | `+6.6e-8` |
| Gumbel | IFM | 2.0311985 | 2.0310340 | `+1.6e-4` |
| Joe | MPL | 2.6643256 | 2.6643260 | `-4.5e-7` |
| Joe | IFM | 2.9656127 | 2.9652690 | `+3.4e-4` |
| Gaussian | MPL | 0.8000853 | 0.8000820 | `+3.3e-6` |
| Gaussian | IFM | 0.7871334 | 0.7871479 | `-1.5e-5` |
| Student t | MPL | `[rho=0.8157372, nu=5.0704726]` | Not claimed | N/A |
| Student t | IFM | `[rho=0.8142171, nu=5.0055361]` | Not claimed | N/A |

The pseudo-likelihood optima agree with the historical R values to `1e-7`-`1e-6`, which confirms that
those values used Weibull pseudo-observations; the inference-from-margins optima differ by up to
`3.4e-4`, consistent with a different marginal standard-deviation convention in the historical fits, and
remain close to the historical values as provenance only. `CopulaEstimationOracleTests` rebuilds each fixture,
sets the Normal marginals to the closed-form maximum-likelihood estimates, fits with production
Differential Evolution and untouched default tolerances, and requires same-point log-likelihood parity
within `1e-8` as a deterministic parameterization check and the production optimum inside the joint
95% likelihood-ratio region (`chi-square(1)=3.841458820694124`). The distance uses
`2*abs(LL_independent-LL_production)` after both likelihoods are required finite, so a worse
production optimum cannot pass through a negative statistic. The two Student-t cells use the
artifact's same-point numerical tolerance and the joint two-coordinate cutoff
`chi-square(2)=5.991464547107979`. All 14 retained methods passed fresh guarded one-result runs on
1 September 2026 (`20260901-143648-...` through `20260901-143950-...`); the twelve historical
identities were not rerun after consolidation.

## Bayesian recovery and coincident frequency

`BivariateAnalysisParameterRecoveryTests` generates paired Normal-marginal samples from the Normal,
Joe, Gumbel, Frank, Clayton, Ali-Mikhail-Haq, and Student-t copulas. The six one-coordinate Bayesian
identities apply the common central-95%/R-hat/ESS rule; the renamed Student-t MLE identity applies
observed-information rules to `rho` and tail dependence. All seven current identities passed
separately on 31 August 2026. The removed `RecoverStudentTCopulaParameters` MCMC attempt was interrupted
at Haden Smith's direction and produced no TRX, so it is not evidence. `CoincidentFrequencyAnalysisTests` fits two Normal marginals and a
Gaussian copula to simulated pairs and requires central-95% parent inclusion, R-hat below 1.10, and
ESS at least 100 for rho. Separately, the mode curve of the coincident-frequency response surface must
match the closed-form distribution of the sum of two correlated fitted Normals within the declared
5x5 response-table discretization error for rho = 0, positive, and negative. The
fourth cell uses `exp(0.01X+0.01Y)`, the exact Lognormal law, separate response-table error and parent-
fit checks, and central-95% propagated parent-response bands at five predeclared ordinates. All four
current cells passed separately on 31 August 2026. The TR-014 product-posterior oracle is
recorded in the [composite chapter](composite.md#independent-posterior-resampling).

## Criteria

`BivariateAnalysis` evaluates AIC and BIC from `BivariateDistribution.DataLogLikelihood` at the stored
MAP with the number of fitted copula parameters and the matched-pair count; copula-prior densities are
excluded (TR-047, Phase 2). With a flat copula prior the MAP coincides with the constrained copula MLE
and the criteria have their usual likelihood interpretation conditional on the fixed marginals; with an
informative copula prior use DIC, WAIC, or verified PSIS-LOO. The fast routing regression
`AnalysisInformationCriteriaRoutingTests.BivariateCriteria_UseOneDataLikelihoodCallAtMap` injects a
stored MAP, an informative copula prior, and a single retained draw and proves that point-estimate
result construction evaluates the data likelihood once at the MAP and that AIC/BIC equal the data-only
formulas rather than the posterior-kernel formulas.

## Limitations

The copula estimation oracle covers the one-parameter Archimedean, Gaussian, and Student-t families
with Normal marginals. No historical R-package parity is claimed for Student t, and its weak raw
degrees-of-freedom coordinate is not claimed recovered. The bivariate analysis remains
conditional on fixed marginal fits, and comparisons of its criteria are valid only across models with
identical marginals, paired events, and likelihood convention.

[Verification index](README.md) | [Technical treatment](../technical-reference/analysis/bivariate.md) | [Scientific findings](../technical-reference/review-findings.md#tr-047)
