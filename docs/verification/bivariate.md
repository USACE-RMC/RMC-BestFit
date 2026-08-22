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
| Copula maximum pseudo-likelihood and inference-from-margins estimation (six families) | `CopulaEstimationOracleTests` (12 exact methods) against the independent `copula-estimation-oracle.json` optimum; historical R `copula` targets retained | Passed 12/12 (21 August 2026) |
| Bayesian bivariate copula recovery (seven families) | `BivariateAnalysisParameterRecoveryTests` under production DEMCzs defaults | Passed 7/7 (21 August 2026; Student t 470 s) |
| Coincident-frequency response surface | `CoincidentFrequencyAnalysisTests` closed-form sum-of-Normals cells under production defaults | Passed 3/3 (21 August 2026) |
| Independent product-posterior propagation (TR-014) | `PosteriorResamplingVerificationTests.CoincidentFrequencyPosteriorResampling_MatchesIndependentClosedFormOracle` | Passed (3 August 2026) |
| AIC/BIC use the copula data likelihood at the stored MAP (TR-047) | Fast routing regression `AnalysisInformationCriteriaRoutingTests.BivariateCriteria_UseOneDataLikelihoodCallAtMap` | Passed (fast gate) |

## Copula estimation oracle

`BivariateDistributionMLETests` fits the Ali-Mikhail-Haq, Clayton, Frank, Gumbel, Joe, and Gaussian
copulas to twelve embedded fixtures (one hundred paired observations each) by maximum pseudo-likelihood
(Weibull plotting-position complements `rank/(n + 1)`) and by inference from margins (Normal marginals
fitted by maximum likelihood) and compares the dependence parameter with historical R `copula` values
at `1e-3`. The package version behind those values was never recorded. The generator
`verification/python/bivariate/generate_copula_estimation_oracle.py` (SHA-256
`4c90189d85864c7c2d84b6a5fbfb34cda64e7a7cac7eb72b0b3966f65d0b8fcb`) transcribes the fixtures from
the test source, implements every copula density independently in closed form, self-checks each density
against the numerical mixed partial derivative of its distribution function (maximum relative error
`1.1e-6`, tolerance `1e-5`), maximizes the pseudo- or IFM log likelihood with a 401-point grid scan
and bounded refinement (`xatol 1e-12`), and writes `verification/data/bivariate/copula-estimation-oracle.json`
(SHA-256 `9d802581f5a6057b6582f80a2fe11f7b79c8a554c571139ca7864e009610b836`).

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

The pseudo-likelihood optima agree with the historical R values to `1e-7`-`1e-6`, which confirms that
those values used Weibull pseudo-observations; the inference-from-margins optima differ by up to
`3.4e-4`, consistent with a different marginal standard-deviation convention in the historical fits, and
remain inside the historical `1e-3` tolerance. `CopulaEstimationOracleTests` rebuilds each fixture,
sets the Normal marginals to the closed-form maximum-likelihood estimates, fits with the production
Brent path, and requires the log likelihood at the independent optimum within `1e-8`, the fitted
dependence parameter within `1e-5` relative (`1e-6` floor), the production maximum at least the
independent optimum, and the historical R value within `1e-3`. All twelve exact methods passed on
21 August 2026 through the guarded runner.

## Bayesian recovery and coincident frequency

`BivariateAnalysisParameterRecoveryTests` generates paired Normal-marginal samples from the Normal,
Joe, Gumbel, Frank, Clayton, Ali-Mikhail-Haq, and Student t copulas and requires the posterior MAP to
recover the copula parameter within 15% relative and each marginal parameter within 5.0 absolute under
the production DEMCzs defaults (seed 12345). All seven methods passed separately on 21 August 2026
(4-10 s each, Student t 470 s). `CoincidentFrequencyAnalysisTests` fits two Normal marginals and a
Gaussian copula to simulated pairs and requires the mode curve of the coincident-frequency response
surface to match the closed-form distribution of the sum of two correlated standard Normals within
maximum absolute error 0.05 and mean absolute error 0.01 for rho = 0, positive, and negative; all three
cells passed on 21 August 2026 under production defaults. The TR-014 product-posterior oracle is
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

The copula estimation oracle covers the one-parameter Archimedean and Gaussian families with Normal
marginals; the Student t copula is covered by Bayesian recovery only. The bivariate analysis remains
conditional on fixed marginal fits, and comparisons of its criteria are valid only across models with
identical marginals, paired events, and likelihood convention.

[Verification index](README.md) | [Technical treatment](../technical-reference/analysis/bivariate.md) | [Scientific findings](../technical-reference/review-findings.md#tr-047)
