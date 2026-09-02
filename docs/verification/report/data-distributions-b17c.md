<!-- verification-status: publication-draft -->

# Distribution Fitting, Univariate Analysis, and Bulletin 17C

## Verification objectives

This chapter verifies that supported univariate families use the documented parameterizations, likelihood measure, support, distribution functions, and fitted optima; that fitting-analysis criteria and weights are computed from those fitted results; and that the specialized Bulletin 17C generalized-method-of-moments implementation reproduces the published worked-example parameters.

## Distribution-family oracles

### Nonstationary univariate recovery

The retained constant-trend Normal baseline generates exactly 1,000 response/covariate rows from physical
parents `[mean=100, scale=15]` with MT19937 seed 12345. Both directly identified coordinates must contain
their generating parent in the central 95% posterior interval, have R-hat below 1.10, and have ESS at least
100. The exact guarded method passed 1/1 in 17.849 s under `20260902-082205-...`.

Fifteen legacy Normal-only Cartesian trend combinations were consolidated rather than transferred. Their
former four-posterior-standard-deviation rule is superseded by the five-cell multi-family parent/trend
covering array, response-space recovery for correlated trend coefficients, and the fast 380-assignment
default matrix.

### Stationary Bayesian family recovery

Each of the fifteen supported univariate families also has a generating-parent recovery cell with exactly
1,000 scalar observations. The generator, physical parameter order, seed, priors, parameter bounds, and
production DEMCzs settings are fixed in the source and catalog. Every identifiable generating coordinate
must lie inside its central 95% posterior interval, with $\widehat R<1.10$ and ESS at least 100. All fifteen
retained identities produced one exact passing TRX during the 2 September 2026 refresh; this is recovery
evidence, distinct from the deterministic distribution-function and external fitted-optimum comparisons
below.

The fifteen `UnivariateAnalysisMAPTests` report calculations are retained only as non-discovered historical
methods. Their reference points mix fitted real-data summaries, MLE or L-moment estimates, and weak-prior
Bayesian results, so the former 5-10% point bands were not an independent posterior oracle. A diagnostic
central-95% rewrite produced four passes and then an Exponential miss: parent `13100` lay just outside
`[11528.7492, 13089.7937]`. That failure was preserved, no sampler or fixture was tuned, the remaining ten
historical methods were not executed, and none of the fifteen identities is cataloged as current evidence.

The family tests use deterministic data committed with the report. Each test compares the fitted parameter vector, maximized data log likelihood, representative CDF values, and representative quantiles with an independent implementation. SciPy 1.17.1 supplies thirteen overlapping families. R 4.4.3 with `lmomco` 2.5.7 supplies Generalized Logistic and Generalized Normal. Closed-form calculations supplement Normal, Log10-Normal, Exponential, and Logistic, and analytical branch identities supplement Kappa Four.

| Family | Primary independent reference | Quantities compared | Result |
|---|---|---|---:|
| Normal | Closed-form MLE and SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Log10-Normal | Closed-form transformed MLE | Parameters, original-scale likelihood, CDF, quantiles | Passed |
| Ln-Normal | SciPy with parameter conversion | Parameters, likelihood, CDF, quantiles | Passed |
| Exponential | Closed form and SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Gamma | SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Generalized Extreme Value | SciPy with shape-sign conversion | Parameters, likelihood, CDF, quantiles | Passed |
| Generalized Logistic | R `lmomco` | Parameters, likelihood, CDF, quantiles | Passed |
| Generalized Normal | R `lmomco` | Parameters, likelihood, CDF, quantiles | Passed |
| Generalized Pareto | SciPy differential evolution | Global optimum, CDF, quantiles | Passed |
| Gumbel | SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Kappa Four | Analytical zero-shape branch and SciPy finite-shape cases | PDF derivative, CDF-quantile inversion, support | Passed |
| Logistic | Closed form and SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Log-Pearson Type III | Transformed SciPy Pearson III | Parameters, likelihood, CDF, quantiles | Passed |
| Pearson Type III | SciPy | Parameters, likelihood, CDF, quantiles | Passed |
| Weibull | SciPy | Parameters, likelihood, CDF, quantiles | Passed |

The acceptance rules are stored with each oracle. External optimizer points and the 15 published
real-data reference points are evaluated through joint 95% likelihood-ratio regions with
chi-square degrees of freedom equal to the fitted coordinate count. Same-point likelihood, CDF,
and quantile comparisons retain only tight numerical round-off tolerances.

### Log10-Normal closed-form test

For transformed values $x_i=\log_{10}(y_i)$ equal to $[1.1,1.4,1.7,2.0,2.3,2.6,2.9]$, the closed-form MLE is

$$\widehat\mu=2,\qquad \widehat\sigma=0.6.\tag{D.1}$$

The original-scale likelihood includes $-\sum_i\log(y_i\ln 10)$. The analytical log likelihood is `-44.431208784723104`, the median is `100`, and the 0.9 quantile is `587.3959385303014`.

| Quantity | Independent value | BestFit acceptance | Result |
|---|---:|---:|---:|
| $\widehat\mu$ | 2.0000000000 | absolute standardized error at most 1.96 using $\widehat\sigma/\sqrt{N}$ | Passed |
| $\widehat\sigma$ | 0.6000000000 | absolute standardized error at most 1.96 using $\widehat\sigma/\sqrt{2N}$ | Passed |
| Joint fitted point | analytical MLE | two-coordinate 95% likelihood-ratio statistic at most 5.991464547107979 | Passed |
| Log likelihood at the analytical point | -44.431208784723104 | absolute error at most `1e-8` | Passed |
| Median CDF | 0.5 | absolute error at most `1e-12` | Passed |
| 0.9 quantile | 587.3959385303014 | absolute error at most `1e-9` | Passed |

### Common-data fitting test

Gumbel, Normal, and Logistic were fitted to the same deterministic sample by BestFit and SciPy.
For each family, the two fitted optima must occupy the same two-coordinate joint 95%
likelihood-ratio region; no fixed coordinate-percentage tolerance is used. The fitted AIC and
RMSE rankings must agree exactly.

Maximum log likelihood, AIC, and BIC agreed under `1e-8` absolute plus `1e-7` relative tolerance. The RMSE equation and inverse-RMSE weights were independently recomputed from BestFit's fitted coordinates at `1e-10` and `1e-12`, respectively.

### Parameter-adjusted RMSE

For residuals $[1,2,3,4]$, $n=4$, and $k=1$, the independent value is

$$\sqrt{\frac{1^2+2^2+3^2+4^2}{4-1}}=\sqrt{10}=3.1622776601683795.\tag{D.2}$$

BestFit reproduced this value within `1e-12`, and paired permutation of observed and fitted values left the statistic unchanged.

## Bulletin 17C published examples

The seven official examples exercise systematic records, low outliers, zero-flow years, broken records, perception thresholds, historical intervals, and paleoflood information. Each cell fits the LP3 parent through the specialized GMM path and compares the base-10 log-space mean, standard deviation, and skewness with the published values at absolute tolerance `1e-3`.

| Example | Principal data condition | Published mean | Published standard deviation | Published skewness | Result |
|---:|---|---:|---:|---:|---:|
| 1 | 68-year systematic record | 3.328623159 | 0.140287994 | 0.396626124 | Passed |
| 2 | Low outliers and zero-flow years | 3.022663041 | 0.682087092 | -0.929108050 | Passed |
| 3 | Broken record, thresholds, and low outliers | 3.759834285 | 0.243406211 | 0.144442997 | Passed |
| 4 | Historical intervals and perception thresholds | 3.885777246 | 0.245920859 | 0.817849937 | Passed |
| 5 | Variable crest-stage thresholds and low outliers | 3.278686106 | 0.233135027 | -0.925407257 | Passed |
| 6 | Historical information and multiple low-outlier detection | 3.069106533 | 0.489820622 | -0.462278724 | Passed |
| 7 | Paleoflood intervals and long perception thresholds | 4.653457000 | 0.376721000 | -0.101163000 | Passed |

Aggregate result: 21 of 21 parameter comparisons passed. Three additional plotting-position cells reproduced PeakFQ results for Examples 4, 5, and 7.

The former `Test_Example4_UncertainData` and `Test_Example7_UncertainData` calculations are non-discovered
historical methods. They altered the published data law and compared fitted coordinates through arbitrary
percentage bands without an independent uncertain-data oracle. The seven exact published examples above
remain the current Bulletin 17C parameter evidence; deterministic uncertain-data behavior remains fast-test
owned.

## Bootstrap refit reliability

Ordinary and pivotal resampling were applied to each of the seven examples. Examples 1 through 6 used 1,000 refits for each method; Example 7 used 500 for each method, for 13,000 requested outputs overall. The test requires a finite result for every requested output and records retries, substitutions, bound repairs, and numerical clips.

| Resampling method | Cells | Requested outputs | Finite outputs | Retries | Parent substitutions | Exceptions |
|---|---:|---:|---:|---:|---:|---:|
| Ordinary bootstrap | 7 | 6,500 | 6,500 | 0 | 0 | 0 |
| Pivotal bootstrap | 7 | 6,500 | 6,500 | 0 | 0 | 0 |
| Total | 14 | 13,000 | 13,000 | 0 | 0 | 0 |

This result verifies numerical completion and accounting for the published examples. It is not an interval-coverage claim.

## Conclusion

All fifteen univariate families passed the declared generating-parent recovery and analytical or external-package comparisons, both multi-candidate fitting cells passed, and the seven Bulletin 17C example parameter vectors reproduced the published values. The bootstrap table is a delivery and accounting record rather than scientific accuracy evidence. Cohn interval values and broad Bulletin 17C interval coverage remain outside the claims supported by this checkpoint; all three governed coverage classes remain execution-excluded historical evidence.
