<!-- verification-status: publication-draft -->

# Distribution Fitting, Univariate Analysis, and Bulletin 17C

## Verification objectives

This chapter verifies that supported univariate families use the documented parameterizations, likelihood measure, support, distribution functions, and fitted optima; that fitting-analysis criteria and weights are computed from those fitted results; and that the specialized Bulletin 17C generalized-method-of-moments implementation reproduces the published worked-example parameters.

## Distribution-family oracles

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

The acceptance tolerances are stored with each oracle. Optimizer coordinates generally use scaled tolerances because BestFit differential evolution and external local optimizers stop by different criteria. Same-point likelihood, CDF, and quantile comparisons retain tighter absolute tolerances.

### Log10-Normal closed-form test

For transformed values $x_i=\log_{10}(y_i)$ equal to $[1.1,1.4,1.7,2.0,2.3,2.6,2.9]$, the closed-form MLE is

$$\widehat\mu=2,\qquad \widehat\sigma=0.6.\tag{D.1}$$

The original-scale likelihood includes $-\sum_i\log(y_i\ln 10)$. The analytical log likelihood is `-44.431208784723104`, the median is `100`, and the 0.9 quantile is `587.3959385303014`.

| Quantity | Independent value | BestFit acceptance | Result |
|---|---:|---:|---:|
| $\widehat\mu$ | 2.0000000000 | absolute error at most `1e-5` | Passed |
| $\widehat\sigma$ | 0.6000000000 | absolute error at most `1e-5` | Passed |
| Log likelihood | -44.431208784723104 | absolute error at most `1e-8` | Passed |
| Median CDF | 0.5 | absolute error at most `1e-12` | Passed |
| 0.9 quantile | 587.3959385303014 | absolute error at most `1e-9` | Passed |

### Common-data fitting test

Gumbel, Normal, and Logistic were fitted to the same deterministic sample by BestFit and SciPy. The optimum coordinates and information criteria were compared, and the fitted ranking had to agree exactly.

| Gumbel coordinate | SciPy optimum | BestFit | Relative error | Limit | Result |
|---|---:|---:|---:|---:|---:|
| Location | 93.11234799935337 | 93.11293212949110 | `6.27e-6` | `1e-4` | Passed |
| Scale | 13.157628567998076 | 13.157823249599968 | `1.48e-5` | `1e-4` | Passed |

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

## Bootstrap refit reliability

Ordinary and pivotal resampling were applied to each of the seven examples. Examples 1 through 6 used 1,000 refits for each method; Example 7 used 500 for each method, for 13,000 requested outputs overall. The test requires a finite result for every requested output and records retries, substitutions, bound repairs, and numerical clips.

| Resampling method | Cells | Requested outputs | Finite outputs | Retries | Parent substitutions | Exceptions |
|---|---:|---:|---:|---:|---:|---:|
| Ordinary bootstrap | 7 | 6,500 | 6,500 | 0 | 0 | 0 |
| Pivotal bootstrap | 7 | 6,500 | 6,500 | 0 | 0 | 0 |
| Total | 14 | 13,000 | 13,000 | 0 | 0 | 0 |

This result verifies numerical completion and accounting for the published examples. It is not an interval-coverage claim.

## Conclusion

All fifteen univariate families passed the declared analytical or external-package comparisons, both multi-candidate fitting cells passed, the seven Bulletin 17C example parameter vectors reproduced the published values, and all fourteen refit-reliability cells completed without substitution or retry. Cohn interval values and broad Bulletin 17C interval coverage remain outside the claims supported by this checkpoint.
