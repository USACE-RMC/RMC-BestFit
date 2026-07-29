# bivariate-distribution-examples

## Overview

Bivariate distribution fitting using six copula families (AMH, Clayton, Frank, Gumbel, Joe, Normal) on synthetic paired data. Demonstrates marginal estimation followed by copula selection across the standard Archimedean and elliptical families.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `AMH - X Data` | Synthetic X-margin data for the AMH copula example. |
| `AMH - Y Data` | Synthetic Y-margin data for the AMH copula example. |
| `Clayton - X Data` | Synthetic X-margin data for the Clayton copula example. |
| `Clayton - Y Data` | Synthetic Y-margin data for the Clayton copula example. |
| `Frank - X Data` | Synthetic X-margin data for the Frank copula example. |
| `Frank - Y Data` | Synthetic Y-margin data for the Frank copula example. |
| `Gumbel - X Data` | Synthetic X-margin data for the Gumbel copula example. |
| `Gumbel - Y Data` | Synthetic Y-margin data for the Gumbel copula example. |
| `Joe - X Data` | Synthetic X-margin data for the Joe copula example. |
| `Joe - Y Data` | Synthetic Y-margin data for the Joe copula example. |
| `Normal - X Data` | Synthetic X-margin data for the Normal copula example. |
| `Normal - Y Data` | Synthetic Y-margin data for the Normal copula example. |

### Univariate Distribution

| Element | Description |
|---|---|
| `AMH - Marginal X` | Univariate fit of the X-margin for the AMH copula example. |
| `AMH - Marginal Y` | Univariate fit of the Y-margin for the AMH copula example. |
| `Clayton - Marginal X` | Univariate fit of the X-margin for the Clayton copula example. |
| `Clayton - Marginal Y` | Univariate fit of the Y-margin for the Clayton copula example. |
| `Frank - Marginal X` | Univariate fit of the X-margin for the Frank copula example. |
| `Frank - Marginal Y` | Univariate fit of the Y-margin for the Frank copula example. |
| `Gumbel - Marginal X` | Univariate fit of the X-margin for the Gumbel copula example. |
| `Gumbel - Marginal Y` | Univariate fit of the Y-margin for the Gumbel copula example. |
| `Joe - Marginal X` | Univariate fit of the X-margin for the Joe copula example. |
| `Joe - Marginal Y` | Univariate fit of the Y-margin for the Joe copula example. |
| `Normal - Marginal X` | Univariate fit of the X-margin for the Normal copula example. |
| `Normal - Marginal Y` | Univariate fit of the Y-margin for the Normal copula example. |

### Bivariate Distribution

| Element | Description |
|---|---|
| `AMH Copula` | Bivariate fit using the AMH (Ali-Mikhail-Haq) copula on the AMH marginals. |
| `Clayton Copula` | Bivariate fit using the Clayton copula on the Clayton marginals. |
| `Frank Copula` | Bivariate fit using the Frank copula on the Frank marginals. |
| `Gumbel Copula` | Bivariate fit using the Gumbel copula on the Gumbel marginals. |
| `Joe Copula` | Bivariate fit using the Joe copula on the Joe marginals. |
| `Normal Copula` | Bivariate fit using the Normal (Gaussian) copula on the Normal marginals. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/5-bivariate-distribution-analysis/1-bivariate-distributions/`.
3. Open `bivariate-distribution-examples.bestfit`.

### Exploring the Elements

For each Bivariate Distribution analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Distribution Results** tab to see the fitted copula contour overlay on the data scatter.
3. Inspect the dependence parameter (θ) in the parameter table.
4. Compare AIC / BIC across copula families to select the best fit. The copula method can be changed from the Method dropdown in the **Properties** panel.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any Bivariate Distribution analysis to inspect:

- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Bivaraite Distribution Analysis labeled "AMH Copula"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Dependency (θ) | 0.814271 | 0.127172 | 0.572653 | 0.84314 | 0.960294 | 1.0001 | 7748 |

### Plots
There are plenty of plots to explore in the Bivariate Distribution Analysis. Under **Distribution Results** is simulated data from the joint copula denisty overliad on the X-Y data.
![Joint copula density contour overlaid on the X-Y scatter.](..images/bivariate-joint-density.png)

*Figure 1: Joint copula density contour overlaid on the X-Y scatter.*

The **Kernal Density** tab shows an estimate for the pdf of a parameter.
![Kernel density of dependency (θ).](..images/bivariate-kernal-density.png)

*Figure 2: Kernel density of dependency (θ).*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge to a parameter.
![Markov chain trace of dependency (θ).](..images/bivariate-trace.png)

*Figure 3: Markov chain trace of dependency (θ).*


### MCMC Diagnostics

One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps

- Use the joint distribution to estimate **AND / OR / Kendall-return-period** quantiles.
- Compare copula families via AIC / BIC; the heavy-tail families (Gumbel, Joe) often win for hydrologic peaks.
- Pair with a **Coincident Frequency Analysis** to derive a sum / difference / max distribution.