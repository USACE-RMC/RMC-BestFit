<!-- verification-status: publication-draft -->

# Composite Analysis

## Question and combination rules

Composite analysis combines distributions that have already been estimated in separate child
analyses. It does not fit a new set of component parameters to pooled observations. Verification
therefore asks whether the chosen combination rule produces the correct probabilities and whether
uncertainty from the child analyses is carried into the combined frequency curve.

Three rules have different physical meanings. A **mixture** selects a child population with a specified
probability. A **maximum** describes the largest of the child outcomes, and a **minimum** describes
the smallest. **Model averaging** weights alternative models; an equal-weight average has the same
mathematical distribution as an equal-weight mixture, although the interpretation of the weights
differs. The tests check these distinctions explicitly.

Let $F_i(x)$ be the probability that child $i$ is at or below magnitude $x$.

| Combination | Independent reference probability at or below $x$ |
|---|---|
| Mixture, with nonnegative weights summing to one | $\sum_i w_iF_i(x)$ |
| Maximum of independent children | $\prod_iF_i(x)$ |
| Minimum of independent children | $1-\prod_i[1-F_i(x)]$ |
| Maximum under perfect positive dependence | $\min_i F_i(x)$ |
| Minimum under perfect positive dependence | $\max_i F_i(x)$ |

Perfect positive dependence means all children occupy the same percentile together.
The minimum and maximum rules cannot be substituted for a mixture without changing the result.

## Deterministic and recovery results

### Fixed-distribution setup and procedure

The fixed calculations use three synthetic Normal children from Tables 44-45 of the 2024
*Verification of the RMC-TotalRisk Software* report. Magnitudes have arbitrary, consistent units.

| Child | Mean | Standard deviation | Mixture weight |
|---|---:|---:|---:|
| First | 10 | 2 | 0.30 |
| Second | 20 | 1 | 0.20 |
| Third | 30 | 5 | 0.50 |

The tests supply these known child distributions directly, without fitting data. They calculate the
reference probabilities with individual Normal formulas, then compare BestFit's combined distribution
over magnitudes 0 to 55. Quantiles are values associated with specified nonexceedance probabilities;
their verification uses both independent numerical inversion and a published R *mistr* table.

The table below reproduces all 25 rounded reference quantiles. Annual exceedance probability (AEP)
is the probability of exceeding the listed magnitude. BestFit passed the published table's 1%
relative comparison allowance at every row. The allowance is for this rounded published comparison,
not an estimation or uncertainty-coverage rule.

| AEP | Published R *mistr* mixture quantile | AEP | Published R *mistr* mixture quantile |
|---:|---:|---:|---:|
| 0.000001 | 53.10 | 0.01 | 40.27 |
| 0.000002 | 52.34 | 0.02 | 38.75 |
| 0.000005 | 51.33 | 0.05 | 36.40 |
| 0.00001 | 50.54 | 0.10 | 34.21 |
| 0.00002 | 49.72 | 0.20 | 31.26 |
| 0.00005 | 48.60 | 0.30 | 28.72 |
| 0.0001 | 47.70 | 0.50 | 21.38 |
| 0.0002 | 46.76 | 0.70 | 15.58 |
| 0.0005 | 45.45 | 0.80 | 10.88 |
| 0.001 | 44.39 | 0.90 | 9.15 |
| 0.002 | 43.26 | 0.95 | 8.07 |
| 0.005 | 41.63 | 0.98 | 7.00 |
| - | - | 0.99 | 6.34 |

A separate inversion check substitutes each BestFit quantile back into the independent weighted
Normal probability. Its probability-error bound is
$\max[10^{-8},0.005\min(\mathrm{AEP},1-\mathrm{AEP})]$.
This checks inversion more directly than the rounded magnitude table.

### Controlled propagation of uncertainty

Three additional experiments give each child 20 equally spaced possible mean values: 9.8-10.2,
19.8-20.2, and 29.8-30.2. Standard deviations remain 2, 1, and 5. Repeating those supports supplies
5,000 retained parameter draws per child; these are controlled uncertainty inputs, not 5,000
observed events or newly fitted posterior samples.

For mixture, maximum, and minimum separately, the independent calculation enumerates every one
of the $20\times20\times20=8{,}000$ combinations. BestFit's resampled result, using seed 20260803,
is compared with that complete calculation at nonexceedance probabilities 0.05, 0.25, 0.50, 0.75,
and 0.95. Reported mean-curve magnitudes must agree within 0.02; endpoints of the central 90%
intervals must agree within 0.05. Each fixed-parent quantile must also lie within its interval.

A separate correlated calculation uses two Normal children, each with mean 10 and standard
deviation 1, linked by Gaussian correlation 0.6. At their common median 10, the exact maximum
probability is $1/4+\arcsin(0.6)/(2\pi)$; the minimum probability is its complement.
This checks the correlation setting against a known joint probability.

| Fixed-distribution or controlled-uncertainty comparison | Number of tests | Acceptance | Result |
|---|---:|---|---|
| Weighted three-Normal probability | 1 | Absolute error at most $10^{-12}$ | Passed |
| Published R *mistr* quantiles | 1 | 1% relative magnitude difference at all 25 probabilities | Passed |
| Quantile inversion | 1 | Probability-dependent bound stated above | Passed |
| Independent and perfectly positively dependent maxima | 1 | Probability error at most $10^{-10}$ | Passed |
| Independent and perfectly positively dependent minima | 1 | Probability error at most $10^{-10}$ | Passed |
| Distinction and theoretical ordering of combination rules | 1 | Bounds with $10^{-12}$ slack; at least 0.10 probability separation somewhere on the comparison grid | Passed |
| Mixture, maximum, and minimum uncertainty propagation | 3 | Mean curve 0.02; central-90% endpoints 0.05; parent quantile inside interval | Passed |
| Correlated minimum and maximum at the median | 1 | Probability error at most $10^{-8}$ | Passed |

### End-to-end experiments with fitted children

Four experiments then check the full path from data to propagated uncertainty. They use two
independent samples, each of 1,000 observations: the first Normal parent has mean 10 and standard
deviation 2, and the second has mean 22 and standard deviation 3. Generation seeds are 51001 and
51002. These parents differ from the three-child published example above.

1. Fit each child by Bayesian analysis with the production simulation settings and seed 12345.
   Require its true mean and standard deviation inside central 95% intervals, $\widehat R<1.10$,
   and ESS at least 100.
2. Construct the selected composite from the fitted child analyses and propagate their uncertainty.
3. Independently calculate the generating composite's quantiles from the Normal formulas at
   nonexceedance probabilities 0.10, 0.25, 0.50, 0.75, and 0.90.
4. Require each generating quantile inside the corresponding central 95% propagated interval.

| Combination tested | Weights or dependence | Result |
|---|---|---|
| Mixture | First child 0.35; second child 0.65 | Passed child recovery and all five response comparisons |
| Maximum | Independent children | Passed child recovery and all five response comparisons |
| Minimum | Independent children | Passed child recovery and all five response comparisons |
| Equal-weight model average | Computed weights exactly 0.50 and 0.50 | Passed weight, child-recovery, and all five response comparisons |

The ten fixed/controlled comparisons and four fitted-child experiments passed. The BestFit child
estimates, combined quantiles, and interval endpoints are not available for a numerical results table.

### Independence of child uncertainty

An additional experiment tests the maximum of two Normal children, both with standard deviation
0.20. Each receives 100 equally spaced possible means: 10-14 for the first child and 11-15 for the
second, repeated to form 5,000 supplied draws per child. The independent reference considers all
10,000 combinations of means at nonexceedance probability 0.50.

With resampling seed 20260803, BestFit's mean-curve magnitude must agree within 0.02 and its central
50% interval endpoints within 0.05. The comparison is repeated after reversing the second child's
draw order and after exchanging the children. All three variants passed. As a negative control,
pairing draws only by their stored positions misses the independent reference by at least 0.10.
This demonstrates that the check detects artificial dependence caused by matching unrelated draws.
The companion coincident-frequency experiment is described in the next chapter.

## Interpretation and limitations

These results support the specified combination rules, dependence settings, and propagation at the
declared response probabilities. The controlled 8,000 combinations are not independent repetitions
of data collection, and the four fitted-child examples do not establish repeated-sample simultaneous
coverage of an entire curve. [Supporting calculations](../composite.md) provide the published
reference, analytical comparisons, and resampling details.
