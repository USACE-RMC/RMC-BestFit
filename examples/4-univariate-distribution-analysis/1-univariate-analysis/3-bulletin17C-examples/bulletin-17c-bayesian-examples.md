# bulletin-17c-bayesian-examples

## Overview

Bayesian re-fits of the seven Bulletin 17C example datasets (Moose River, Orestimba Creek, Back Creek, Arkansas River, Bear Creek, Santa Cruz River, American River) using the Log-Pearson Type III distribution. Companion to bulletin-17c-examples.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Example #1` | Systematic Record – Moose River at Victory, VT |
| `Example #2` | Analysis with Low Outliers – Orestimba Creek near Newman, CA |
| `Example #3` | Broken Record – Back Creek near Jones Springs, WV |
| `Example #4` | Historical Data – Arkansas River at Pueblo, CO |
| `Example #5` | Crest Stage Gage Censored Data – Bear Creek at Ottumwa, IA |
| `Example #6` | Historic Data and Low Outliers – Santa Cruz River at Lochiel, AZ |
| `Example #7` | Paleoflood Record Example – American River at Fair Oaks, California |

### Univariate Distribution

| Element | Description |
|---|---|
| `Bayes Example #1` | Bayesian LP-III fit of Example #1 (Moose River systematic record). |
| `Bayes Example #2` | Bayesian LP-III fit of Example #2 (Orestimba Creek with low outliers). |
| `Bayes Example #3` | Bayesian LP-III fit of Example #3 (Back Creek broken record). |
| `Bayes Example #4` | Bayesian LP-III fit of Example #4 (Arkansas River with historical data). |
| `Bayes Example #5` | Bayesian LP-III fit of Example #5 (Bear Creek crest-stage gage censored data). |
| `Bayes Example #6` | Bayesian LP-III fit of Example #6 (Santa Cruz River with historic data and low outliers). |
| `Bayes Example #7` | Bayesian LP-III fit of Example #7 (American River paleoflood record). |

### Bulletin 17C

| Element | Description |
|---|---|
| `B17C Example #1` | B17C fit of Example #1 (Moose River systematic record). |
| `B17C Example #2` | B17C fit of Example #2 (Orestimba Creek with low outliers). |
| `B17C Example #3` | B17C fit of Example #3 (Back Creek broken record). |
| `B17C Example #4` | B17C fit of Example #4 (Arkansas River with historical data). |
| `B17C Example #5` | B17C fit of Example #5 (Bear Creek crest-stage gage censored data). |
| `B17C Example #6` | B17C fit of Example #6 (Santa Cruz River with historic data and low outliers). |
| `B17C Example #7` | B17C fit of Example #7 (American River paleoflood record). |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/3-bulletin17C-examples/`.
3. Open `bulletin-17c-bayesian-examples.bestfit`.

### Exploring the Elements

For each Univariate Distribution alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Frequency** tab to view the AEP-vs-quantile plot.
3. Open the **Markov Chain Trace** tab to confirm chain mixing (well-mixed traces look like fuzzy caterpillars).
4. Open the **Autocorrelation** tab to check effective sample size.
5. Inspect the **Properties** panel for sampler settings (iterations, warmup, point estimator, credible-interval width).

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

![Frequency curve (AEP versus quantile) for each alternative, with the credible band.](images/bulletin-17c-bayesian-frequency.png)
*Figure: Frequency curve (AEP versus quantile) for each alternative, with the credible band.*

![Posterior kernel density for each parameter.](images/bulletin-17c-bayesian-kernel-density.png)
*Figure: Posterior kernel density for each parameter.*

![Markov-chain traces for each parameter.](images/bulletin-17c-bayesian-trace.png)
*Figure: Markov-chain traces for each parameter.*

![Autocorrelation function of the chains, used to estimate effective sample size.](images/bulletin-17c-bayesian-autocorrelation.png)
*Figure: Autocorrelation function of the chains, used to estimate effective sample size.*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Compare alternatives via the **Bayesian Model Average** element to combine results from multiple distributions.
- Compute **return-period quantiles** (1%, 0.5%, 0.2% AEP) from the frequency-curve table.
- Re-run with informative **quantile priors** if engineering judgment suggests specific upper-bound flood magnitudes.
- Cross-check the LP-III fit against a **Bulletin 17C** fit on the same input data.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
