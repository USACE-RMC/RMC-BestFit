# waimea-river-stage-frequency

## Overview

Coincident peak-flow analysis for the Waimea River and Makaweli River, Kauai, Hawaii. Demonstrates joint Bayesian peak-flow estimation with regional skew, drainage-area scaling, and historical-record conditioning, then derives a coincident peak-flow distribution via the Normal copula.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `16031000_Waimea_Peaks` | Annual peak discharge for the Waimea River at Waimea, Kauai, HI (USGS gage 16031000). |
| `16036000_Makaweli_Peaks` | Annual peak discharge for the Makaweli River, Kauai, HI (USGS gage 16036000). |
| `16036000_Makaweli_Conditional_Peaks` | Conditional Makaweli peak series — Makaweli peaks observed coincidentally with Waimea peaks. |
| `Simulated Proof` | Simulated dataset used to verify the conditional copula machinery against ground truth. |

### Distribution Fitting Analysis

| Element | Description |
|---|---|
| `16031000_WaimeaPk` | Distribution-fitting comparison across LP-III, GEV, Gumbel, etc. on the Waimea peak series. |

### Bayesian Estimation Analysis

| Element | Description |
|---|---|
| `16031000_WaimeaPk` | Bayesian peak-flow estimation for Waimea (legacy element — see <Univariate Distribution> for current fits). |
| `16036000_MakaweliPk` | Bayesian peak-flow estimation for Makaweli (legacy element). |
| `16031000_WaimeaPk_RgSkew` | Bayesian Waimea fit with regional skew prior (legacy element). |
| `16036000_MakaweliPk_RgSkew` | Bayesian Makaweli fit with regional skew prior (legacy element). |
| `16031000_WaimeaPk_RgSkew_MGBT` | Bayesian Waimea fit with regional skew + Multiple Grubbs-Beck Test low-outlier detection (legacy). |
| `16036000_MakaweliPk_RgSkew_Censored` | Bayesian Makaweli fit with regional skew + low-outlier censoring (legacy). |
| `16036000_MakaweliPk_SCALED2Waimea` | Makaweli fit drainage-area scaled to the Waimea reference area (legacy). |
| `16031000_WaimeaPk_SCALED2Makaweli` | Waimea fit drainage-area scaled to the Makaweli reference area (legacy). |
| `16031000_WaimeaPk_SCALED2_85sqmi` | Waimea fit drainage-area scaled to a 85-sqmi reference (legacy). |
| `16036000_MakaweliPk_SCALED2_85sqmi` | Makaweli fit drainage-area scaled to a 85-sqmi reference (legacy). |

### Univariate Distribution

| Element | Description |
|---|---|
| `MakaweliPk - Exact` | Bayesian LP-III fit on the Makaweli systematic peak record (exact data only). |
| `WaimeaPk - Exact + Historical + RR Prior` | Bayesian LP-III fit for Waimea with historical record extension and a runoff-ratio prior. |
| `WaimeaPk - Exact + Historical` | Bayesian LP-III fit for Waimea with historical record extension only. |
| `MakaweliPk - Exact + RR Prior` | Bayesian LP-III fit for Makaweli using exact data and a runoff-ratio prior. |
| `MakaweliPk - Exact + RR Prior_RSkew` | Bayesian Makaweli fit with runoff-ratio prior + regional skew weighting. |
| `WaimeaPk - Exact + Historical + RR Prior_RSkew` | Bayesian Waimea fit with historical record + runoff-ratio prior + regional skew weighting. |
| `MakaweliPk - Cond - Exact` | Bayesian LP-III fit on the conditional Makaweli peak series (Makaweli peaks at Waimea events). |

### Bivariate Distribution

| Element | Description |
|---|---|
| `Normal Copula` | Bivariate Normal copula fit linking the Waimea and Makaweli peak marginals. |
| `Gumbel Copula` | Bivariate Gumbel copula fit linking the Waimea and Makaweli peak marginals. |
| `Clayton Copula` | Bivariate Clayton copula fit linking the Waimea and Makaweli peak marginals. |
| `Joe Copula` | Bivariate Joe copula fit linking the Waimea and Makaweli peak marginals. |
| `Frank Copula` | Bivariate Frank copula fit linking the Waimea and Makaweli peak marginals. |
| `AMH Copula` | Bivariate AMH copula fit linking the Waimea and Makaweli peak marginals. |
| `Normal Copula - Conditional` | Conditional Normal copula fit using the conditional Makaweli marginal. |

### Coincident Frequency

| Element | Description |
|---|---|
| `CFA - Normal - Conditional` | Coincident frequency analysis combining the conditional copula and marginals to produce the joint Waimea + Makaweli peak-flow distribution. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/5-bivariate-distribution-analysis/2-coincident-frequency/`.
3. Open `waimea-river-stage-frequency.bestfit`.

### Exploring the Elements

For each Coincident Frequency Analysis alternative:

1. Click the alternative in the Project Explorer.
2. Open the **Joint Density** tab to view the bivariate fit.
3. Open the **Frequency** tab to view the derived response-variable AEP curve.
4. Adjust the **X / Y ordinates** in the Properties panel to refine the response surface.

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

![Joint copula density contour overlaid on the X-Y scatter.](images/waimea-joint-density.png)
*Figure: Joint copula density contour overlaid on the X-Y scatter.*

![Marginal X frequency curve.](images/waimea-marginal-x.png)
*Figure: Marginal X frequency curve.*

![Marginal Y frequency curve.](images/waimea-marginal-y.png)
*Figure: Marginal Y frequency curve.*

![Derived coincident-response frequency curve (CFA only).](images/waimea-frequency.png)
*Figure: Derived coincident-response frequency curve (CFA only).*

### MCMC Diagnostics

Verify chain convergence before interpreting any results:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

## Next Steps

- Use the joint AEP table to size structures whose response depends on two correlated drivers (e.g., coincident streamflow and downstream stage).
- Compare results against a closed-form analytical answer where one is available.

## References

<!-- Cite published case studies / source datasets here. Example format:

> Viglione, A., Merz, R., Salinas, J. L., & Bloeschl, G. (2013). Flood frequency hooves of a galloping horse. *Water Resources Research*, 49(2), 675-692.
-->

---

_This tutorial was generated from the project's `.bestfit` file metadata. The narrative and figure / table sections are placeholders — capture screenshots from the BestFit GUI and paste output tables to complete the guide._
