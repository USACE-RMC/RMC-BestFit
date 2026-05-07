# Bulletin-17C-examples

## Overview

B17C-method fits of the seven Bulletin 17C example datasets, including both multivariate-normal (MVN) quantile confidence intervals and bias-corrected bootstrap (BCB) intervals. Companion to bulletin-17c-bayesian-examples.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Example #1 - Data` | Systematic Record – Moose River at Victory, VT |
| `Example #2 - Data` | Analysis with Low Outliers – Orestimba Creek near Newman, CA |
| `Example #3 - Data` | Broken Record – Back Creek near Jones Springs, WV.  Manual threshold value is used to match PeakFQ because PeakFQ treated historical data and systematic data differently, even though both were exact data types. The 1936 flood is coded as historical in PeakFQ even though it is entered as exact data. |
| `Example #4 - Data` | Historical Data — Arkansas River at Pueblo, CO (Bulletin 17C Example #4). |
| `Example #5 - Data` | Crest Stage Gage Censored Data – Bear Creek at Ottumwa, IA.  Manual threshold is used to match PeakFQ, which uses different data types. In PeakFQ several systematic values are coded with lower bound = 0, which results in different data being used in the MGBT test. |
| `Example #6 - Data` | Historic Data and Low Outliers – Santa Cruz River at Lochiel, AZ |
| `Example #7 - Data` | Paleoflood Record Example – American River at Fair Oaks, California |

### Bulletin 17C

| Element | Description |
|---|---|
| `Example #1` | B17C fit of Example #1 (Moose River systematic record) using MVN quantile confidence intervals. |
| `Example #2` | B17C fit of Example #2 (Orestimba Creek with low outliers) using MVN quantile confidence intervals. |
| `Example #3` | B17C fit of Example #3 (Back Creek broken record) using MVN quantile confidence intervals. |
| `Example #4` | B17C fit of Example #4 (Arkansas River with historical data) using MVN quantile confidence intervals. |
| `Example #5` | B17C fit of Example #5 (Bear Creek crest-stage censored data) using MVN quantile confidence intervals. |
| `Example #6` | B17C fit of Example #6 (Santa Cruz River with historic data and low outliers) using MVN quantile confidence intervals. |
| `Example #7` | B17C fit of Example #7 (American River paleoflood record) using MVN quantile confidence intervals. |
| `Example #1 - BCB` | B17C fit of Example #1 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #2 - BCB` | B17C fit of Example #2 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #3 - BCB` | B17C fit of Example #3 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #4 - BCB` | B17C fit of Example #4 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #5 - BCB` | B17C fit of Example #5 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #6 - BCB` | B17C fit of Example #6 using bias-corrected bootstrap (BCB) confidence intervals. |
| `Example #7 - BCB` | B17C fit of Example #7 using bias-corrected bootstrap (BCB) confidence intervals. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/1-bulletin17C-examples/`.
3. Open `bulletin-17c-examples.bestfit`.

### Exploring the Elements

For each B17C alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab — the central LP-III curve plus the chosen confidence intervals are shown.
3. Switch the confidence-interval type in the Properties panel between **MVN** and **BCB** (Bias-Corrected Bootstrap) to compare.
4. Use the **Information Expansion** Properties pane to add or remove historical and paleoflood records.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any alternative to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results

<!-- Replace placeholder content as you capture screenshots and copy values out of the BestFit GUI. -->

### Parameter Estimates

<!-- TODO: paste the parameter-estimate table from the MCMC report (right-click the alternative > Open MCMC Report) -->

| Alternative | Parameter | Mean | Median | Lower CI | Upper CI | R-hat | ESS |
|---|---|---|---|---|---|---|---|
| _(placeholder)_ |  |  |  |  |  |  |  |

### Frequency / Quantile Table

<!-- TODO: paste the AEP / return-period table from the Frequency tab (right-click the chart > Copy Table). -->

| AEP (%) | Return Period (yr) | Median | Lower CI | Upper CI |
|---|---|---|---|---|
| 50    | 2     |  |  |  |
| 10    | 10    |  |  |  |
| 1     | 100   |  |  |  |
| 0.5   | 200   |  |  |  |
| 0.2   | 500   |  |  |  |

### Plots

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/bulletin-17c-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/bulletin-17c-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/bulletin-17c-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/bulletin-17c-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare MVN-quantile and bias-corrected bootstrap (BCB) confidence intervals on the same dataset.
- Re-run on the same input data using the **Bayesian Univariate** workflow and compare AEPs and confidence intervals.
- Add a **regional skew** weighting if a regional skew estimate is available.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
