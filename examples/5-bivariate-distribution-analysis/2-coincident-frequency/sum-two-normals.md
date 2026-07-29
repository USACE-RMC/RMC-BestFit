# sum-two-normals

## Overview

Coincident frequency analysis (CFA) verification using the sum of two correlated normals. Tests the bivariate copula machinery against the closed-form analytical answer for three correlation coefficients (-0.5, 0, +0.5).

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `X Data - Rho = 0.0` | Synthetic X-margin data with correlation rho = 0.0 between X and Y. |
| `Y Data - Rho = 0.0` | Synthetic Y-margin data with correlation rho = 0.0 between X and Y. |
| `X+Y Data - Rho = 0.0` | Synthetic sum X+Y for the rho = 0.0 case (closed-form Normal with combined variance). |
| `X Data - Rho = -0.5` | Synthetic X-margin data with correlation rho = -0.5 between X and Y. |
| `Y Data - Rho = -0.5` | Synthetic Y-margin data with correlation rho = -0.5 between X and Y. |
| `X+Y Data - Rho = -0.5` | Synthetic sum X+Y for the rho = -0.5 case (analytical comparison reference). |
| `X Data - Rho = +0.5` | Synthetic X-margin data with correlation rho = +0.5 between X and Y. |
| `Y Data - Rho = +0.5` | Synthetic Y-margin data with correlation rho = +0.5 between X and Y. |
| `X+Y Data - Rho = +0.5` | Synthetic sum X+Y for the rho = +0.5 case (analytical comparison reference). |

### Univariate Distribution

| Element | Description |
|---|---|
| `X Marginal - Rho = 0.0` | Univariate Normal fit of the X-margin (rho = 0.0 case). |
| `Y Marginal - Rho = 0.0` | Univariate Normal fit of the Y-margin (rho = 0.0 case). |
| `X Marginal - Rho = -0.5` | Univariate Normal fit of the X-margin (rho = -0.5 case). |
| `Y Marginal - Rho = -0.5` | Univariate Normal fit of the Y-margin (rho = -0.5 case). |
| `X Marginal - Rho = +0.5` | Univariate Normal fit of the X-margin (rho = +0.5 case). |
| `Y Marginal - Rho =+0.5` | Univariate Normal fit of the Y-margin (rho = +0.5 case). |

### Bivariate Distribution

| Element | Description |
|---|---|
| `Normal Copula - Rho = 0.0` | Normal copula fit with rho = 0.0 (independence baseline). |
| `Normal Copula - Rho = -0.5` | Normal copula fit with rho = -0.5 (negative correlation). |
| `Normal Copula - Rho =+0.5` | Normal copula fit with rho = +0.5 (positive correlation). |

### Coincident Frequency

| Element | Description |
|---|---|
| `CFA - Rho = 0.0` | Coincident frequency analysis using the rho = 0.0 Normal copula — should match the analytical X+Y distribution. |
| `CFA - Rho = -0.5` | Coincident frequency analysis using the rho = -0.5 Normal copula — should match the analytical X+Y distribution. |
| `CFA - Rho = +0.5` | Coincident frequency analysis using the rho = +0.5 Normal copula — should match the analytical X+Y distribution. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/5-bivariate-distribution-analysis/2-coincident-frequency/`.
3. Open `sum-two-normals.bestfit`.

### Exploring the Elements

For each Coincident Frequency Analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Frequency Plot** tab at the top to view the derived response-variable AEP curve.
3. Adjust the **X / Y ordinates** in the Properties panel to refine the response surface.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any Bivariate Distribution Analysis (NOT the Coincident Frequency Analysis element) to inspect:

- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Coincident Frequency Analysis labeled "CFA - Rho = 0/0"; this should be the first CFA in the list.
Be sure to explore all of the analyses provided!

### Frequency / Quantile Table
Select the **Tabular Results** tab at the top to see the frequency plot's value at each return level probability.

| Response Value | 97.5% CI | 2.5% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| 87 | 0.9999575164826484 | 0.9986818487735876 | 0.999613995636363 | 0.9996934913620849 | 
| 96.78947368421052 | 0.9996787406955139 | 0.9952157677102006 | 0.9982686516147767 | 0.9984855324680553 | 
| 106.57894736842105 | 0.998366757951269 | 0.9860986620898814 | 0.994043386684129 | 0.9944813911685078 | 
| 116.36842105263158 | 0.9939550615286463 | 0.9676112368720589 | 0.9837406725518051 | 0.9844151226179727 | 
| 126.15789473684211 | 0.9828923754505756 | 0.9362476453725623 | 0.9635531943756771 | 0.964400332661703 | 
| 135.94736842105263 | 0.9611124039143327 | 0.8906757145159339 | 0.9304756574653961 | 0.9313730567425466 | 
| 145.73684210526315 | 0.9241556517523287 | 0.8283796634903918 | 0.8811213175678487 | 0.8818441882346706 | 
| 155.5263157894737 | 0.8643601366355986 | 0.7445962955334277 | 0.8085886653308365 | 0.8090326043542164 | 
| 165.31578947368422 | 0.7737423169912419 | 0.6358350551505616 | 0.7076660199675067 | 0.7079204843246665 | 
| 175.10526315789474 | 0.6565437404710043 | 0.5081224804755793 | 0.5835296155575668 | 0.5836123596998999 | 
| 184.89473684210526 | 0.525089963663616 | 0.37584797803875436 | 0.4496218873383308 | 0.4495368249067684 | 
| 194.68421052631578 | 0.39445447945846746 | 0.25387100652883515 | 0.32176132362284177 | 0.32153482475909334 | 
| 204.4736842105263 | 0.280416509051367 | 0.15614761940576735 | 0.21464997198347982 | 0.21427673016789273 | 
| 214.26315789473682 | 0.19052389556551255 | 0.08926616637168196 | 0.1356419024545678 | 0.1350025330429513 | 
| 224.05263157894737 | 0.1234644119804129 | 0.04707315167289306 | 0.08089664285995847 | 0.08004867303350849 | 
| 233.8421052631579 | 0.07337327012781823 | 0.021469575628803795 | 0.04347994191646845 | 0.04264350481368828 | 
| 243.6315789473684 | 0.03818934071234933 | 0.00794936677105736 | 0.019974200826754922 | 0.01927516685952424 | 
| 253.42105263157893 | 0.01689613766156331 | 0.0022476626007764794 | 0.007558749480297321 | 0.00707633362796023 | 
| 263.2105263157895 | 0.006082820643538663 | 0.0004665446086391608 | 0.0022762189355344316 | 0.002021765011468002 | 
| 273 | 0.0017213102993187649 | 6.475197382132259E-05 | 0.0005270202407436539 | 0.0004277727678921872

### Plots
There are plenty of plots to explore in the Coincident Frequency Anaylsis. First we can look at the Bivariate Distribution Analysis used to build the Coincident Frequency Anaylsis. 
Select the Bivarate Distribution Analysis labeled "Normal Copula - Rho = 0.0". From there, under **Distribution Results** is simulated data from the joint copula denisty overliad on the X-Y data.
![Joint copula density contour overlaid on the X-Y scatter.](..images/sum-two-normals-joint-density.png)

*Figure 1: Joint copula density contour overlaid on the X-Y scatter.*

Going back to the Coincident Frequency Analysis, there is the frequency plot of annual exccedance probabilities.
![Derived coincident-response frequency curve.](..images/sum-two-normals-frequency.png)

*Figure 2: Derived coincident-response frequency curve.*

### MCMC Diagnostics

One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs of the Bivariate Distribution Analysis (NOT the Coincident Frequency Analysis element).

## Next Steps

- Use the joint AEP table to size structures whose response depends on two correlated drivers (e.g., coincident streamflow and downstream stage).
- Compare results against a closed-form analytical answer where one is available.