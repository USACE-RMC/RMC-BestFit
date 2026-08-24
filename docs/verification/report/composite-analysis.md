<!-- verification-status: publication-draft -->

# Composite Analysis

Composite analysis combines already estimated child distributions and therefore has no separate fitted parameter vector. Independent tests use direct Normal CDF formulas, published R `mistr` Table 45 quantiles, bivariate-Normal orthants, and complete enumeration of three child posterior supports.

| Verification cell | Independent reference | Tolerance | Result |
|---|---|---:|---:|
| Weighted three-Normal CDF | Analytical weighted sum | `1e-12` absolute | Passed |
| Twenty-five mixture quantiles | Published R `mistr` table | 1% relative | Passed |
| Quantile inversion | Analytical weighted CDF | Probability-scaled, floor `1e-8` | Passed |
| Independent and comonotonic maximum | Product and minimum CDF identities | `1e-10` absolute | Passed |
| Independent and comonotonic minimum | Union and maximum CDF identities | `1e-10` absolute | Passed |
| Combination-rule bracketing | Theoretical bounds | `1e-12` slack | Passed |
| Mixture posterior | Complete 20 by 20 by 20 Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Maximum posterior | Complete Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Minimum posterior | Complete Cartesian oracle | Mean 0.02; limits 0.05 | Passed |
| Correlated min/max | Bivariate-Normal orthants at $\rho=0.6$ | `1e-8` absolute | Passed |

Two additional posterior-resampling tests compare the production finite resampling policy with an empirical Cartesian product and a closed-form independent Normal sum. Posterior means pass within 0.02 and interval limits within 0.05, while deliberately raw-paired chains miss by at least 0.10. This demonstrates that each separately fitted source is independently indexed rather than coupled by retained-array position. All twelve reported cells passed.
