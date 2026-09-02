<!-- verification-status: publication-draft -->

# Composite Analysis

## Claim, model, and independent theory

Composite analysis combines already estimated child distributions and therefore has no separate fitted parameter vector. Independent tests use direct Normal CDF formulas, published R `mistr` Table 45 quantiles, bivariate-Normal orthants, and complete enumeration of three child posterior supports. Four end-to-end cells additionally generate N=1000 observations for each of two Normal child analyses, fit both children through unchanged Bayesian defaults, and require the analytical parent composite quantiles inside central 95% propagated bands.

## Deterministic and recovery results

| Verification cell | Independent reference | Tolerance | Result |
|---|---|---:|---:|
| Weighted three-Normal CDF | Analytical weighted sum | `1e-12` absolute | Passed |
| Twenty-five mixture quantiles | Published R `mistr` table | 1% relative parity envelope for the rounded published table; not a recovery rule | Passed |
| Quantile inversion | Analytical weighted CDF | Probability-scaled, floor `1e-8` | Passed |
| Independent and comonotonic maximum | Product and minimum CDF identities | `1e-10` absolute | Passed |
| Independent and comonotonic minimum | Union and maximum CDF identities | `1e-10` absolute | Passed |
| Combination-rule bracketing | Theoretical bounds | `1e-12` slack | Passed |
| Mixture posterior | Complete 20 by 20 by 20 Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Maximum posterior | Complete Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Minimum posterior | Complete Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Correlated min/max | Bivariate-Normal orthants at $\rho=0.6$ | `1e-8` absolute | Passed |
| Unequal-weight predictive mixture | Analytical 0.35/0.65 Normal-mixture parent | Child truth and parent quantiles in central 95%; R-hat below 1.10; ESS at least 100 | Passed |
| Predictive maximum | Analytical independent-product parent | Same | Passed |
| Predictive minimum | Analytical independent survival-product parent | Same | Passed |
| Equal-weight predictive model average | Analytical equal-weight Normal-mixture parent and exact 0.5/0.5 weights | Same | Passed |

The ten oracle results were executed under their current `CompositeOracleVerificationTests` names; historical `CompositeRecoveryTests` results were not transferred. Two additional posterior-resampling tests compare the production finite resampling policy with an empirical Cartesian product and a closed-form independent Normal sum. Posterior means pass within 0.02 and interval limits within 0.05, while deliberately raw-paired chains miss by at least 0.10. This demonstrates that each separately fitted source is independently indexed rather than coupled by retained-array position. The four predictive interactions passed fresh exact one-result runs in 18.718, 16.845, 17.320, and 17.426 seconds. Initial predictive-mixture helper failures from an unsorted percentile array and descending probability grid were discarded and replaced by the current passing exact TRX; no scientific setting changed.

## Limitations and provenance

Composite recovery is a response-space claim at the predeclared ordinates. It does not establish a separate
composite parameter vector, repeated-realization simultaneous coverage, or correctness from dispatch and
finite output alone. Child generator seeds 51001 and 51002, sampler seed 12345, posterior ordering, and the
analytical mixture/minimum/maximum/model-average parents are fixed in source and catalog.
