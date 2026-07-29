# synthetic-rating-curve-examples

## Overview

Synthetic stage-discharge rating curve fits using one-, two-, and three-segment piecewise power-law models. Used to verify the rating-curve solver against ground-truth synthetic data.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Stage Data` | Synthetic stage time series used to drive all three rating-curve verification cases. |
| `1 Segment - Flow Data` | Synthetic discharge time series generated from a known one-segment power-law rating curve. |
| `2 Segment - Flow Data` | Synthetic discharge time series generated from a known two-segment power-law rating curve. |
| `3 Segment - Flow Data` | Synthetic discharge time series generated from a known three-segment power-law rating curve. |

### Rating Curve Analysis

| Element | Description |
|---|---|
| `1 Segment Rating Curve` | One-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |
| `2 Segment Rating Curve` | Two-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |
| `3 Segment Rating Curve` | Three-segment power-law rating-curve fit on the corresponding synthetic data — should recover the ground-truth parameters. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/6-rating-curve-analysis/`.
3. Open `synthetic-rating-curve-examples.bestfit`.

### Exploring the Elements

For each Rating Curve Analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Rating Curve Results** tab to the left to view the fit overlaid on the measured stage / discharge pairs.
3. Adjust **MinStage**, **MaxStage**, and **StageBins** in the **Properties** panel under **Output** to set the prediction range.
4. Inspect breakpoints (h2, h3) behavior for multi-segment fits.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any Rating Curve Analysis to inspect:

- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results

Below are the expected results for Rating Curve Analysis labeled "1 Segment Rating Curve"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Zero-Flow Stage (h₁) | 0.986588 | 0.00618086 | 0.976184 | 0.986718 | 0.996666 | 1.0003 | 9570 | 
| Coefficient (α₁) | 0.220643 | 0.0122755 | 0.200534 | 0.220647 | 0.240935 | 0.9998 | 9427 | 
| Exponent (β₁) | 2.69251 | 0.0119837 | 2.67274 | 2.69241 | 2.71208 | 0.9998 | 9396 | 
| Scale (σ) | 0.0510602 | 0.00211105 | 0.0477149 | 0.0509902 | 0.0546349 | 0.9999 | 9581 |   

### Frequency / Quantile Table
While in the **Rating Curve Results** tab to the left, select Tabular Results to see the fitted curve values. Below are the first 30 rows of the table.

| Stage	| 95.0% CI | 5.0% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| -0.7326817927 | 0 | 0 | 0 | 0 |
| -0.5053910586757575 | 0 | 0 | 0 | 0 | 
| -0.2781003246515151 | 0 | 0 | 0 | 0 | 
| -0.05080959062727264 | 0 | 0 | 0 | 0 | 
| 0.1764811433969698 | 0 | 0 | 0 | 0 | 
| 0.40377187742121223 | 0 | 0 | 0 | 0 | 
| 0.6310626114454547 | 0 | 0 | 0 | 0 | 
| 0.8583533454696972 | 0 | 0 | 0 | 0 | 
| 1.0856440794939397 | 0.004373040596761412 | 0.0024502071394302714 | 0.0033181357135632474 | 0.003288796086243071 | 
| 1.3129348135181822 | 0.09953251883342444 | 0.06676688004068045 | 0.08194946486631781 | 0.08151122081152615 | 
| 1.5402255475424247 | 0.41258379667686623 | 0.2787909602693334 | 0.34009199751969216 | 0.3382779019238859 | 
| 1.7675162815666672 | 1.040815338027164 | 0.7043488656440638 | 0.85866246487436 | 0.8540757155696653 | 
| 1.9948070155909097 | 2.0694147773526477 | 1.4018022880408287 | 1.7081998052886895 | 1.6990799175830091 | 
| 2.222097749615152 | 3.5754621210435054 | 2.4225480514979942 | 2.952980403012079 | 2.937238656168783 | 
| 2.449388483639394 | 5.629716271267176 | 3.819705958953274 | 4.652883297611153 | 4.628126242840219 | 
| 2.6766792176636365 | 8.304748012039896 | 5.632753893882063 | 6.864448059729694 | 6.827994739946989 | 
| 2.9039699516878787 | 11.658948498136947 | 7.914164276686367 | 9.641550437265021 | 9.590445775496226 | 
| 3.131260685712121 | 15.760705467442335 | 10.700024516122776 | 13.035867911942523 | 12.966893547160572 | 
| 3.3585514197363633 | 20.66935427188291 | 14.03336024608513 | 17.097218045933538 | 17.006901360934076 | 
| 3.5858421537606056 | 26.446690457981603 | 17.957229993405374 | 21.873814320864742 | 21.758436130859106 | 
| 3.813132887784848 | 33.14025069370375 | 22.510521678584603 | 27.41246562560744 | 27.268066840042437 | 
| 4.04042362180909 | 40.80336296381522 | 27.71729992592343 | 33.75873567145005 | 33.58112314692881 | 
| 4.267714355833332 | 49.49627653802831 | 33.62982414726785 | 40.95707296970886 | 40.741824711408505 | 
| 4.495005089857575 | 59.277370431256216 | 40.28298497140692 | 49.0509185982613 | 48.793388426908514 | 
| 4.722295823881817 | 70.19173971504746 | 47.688444274424846 | 58.08279682961652 | 57.77811860321969 | 
| 4.949586557906059 | 82.28418674644324 | 55.91116293434267 | 68.09439228002755 | 67.73748373974462 | 
| 5.176877291930301 | 95.61480106950536 | 64.96776364903921 | 79.12661628213944 | 78.71218257720365 | 
| 5.404168025954544 | 110.21125322790957 | 74.90790585009226 | 91.21966451767037 | 90.742201453523 | 
| 5.631458759978786 | 126.1353341808193 | 85.7370427422531 | 104.41306747208405 | 103.86686451768131 | 
| 5.858749494003028 | 143.45745074915777 | 97.51424855078517 | 118.74573492802975 | 118.12487801197535 | 
| 6.0860402280272705 | 162.22128746428191 | 110.26530060290307 | 134.25599545861493 | 133.554369578823 | 
| 6.313330962051513 | 182.43749387245308 | 124.0007042994976 | 150.9816316890409 | 150.1929233567062 | 


### Plots
There are plenty of plots to expore with our Rating Curve Analysis. Under the **Rating Curve Results** tab we have our fitted stage-discharge curve plotted with the orginal data.
![Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.](../images/synthetic-rc-rating-curve.png)

*Figure 1: Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.*

To explore how good the fit is we can look at our residuals in the **Residual Diagnostics** tab. The Residuals Plot shows the residuals of the dicharge values against the fitted stage values.
![Residuals of measured discharge minus rating-curve estimate, plotted against stage.](../images/synthetic-rc-residuals.png)

*Figure 2: Residuals of measured discharge minus rating-curve estimate, plotted against stage.*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain trace for the rating-curve parameter h₁, zero-flow stage.](../images/synthetic-rc-trace-h1.png)

*Figure 3: Markov-chain trace for the rating-curve parameter h₁, zero-flow stage.*

### MCMC Diagnostics

One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps

- Use the Bayesian credible intervals to bound the rating curve at extreme stages.
- Apply the rating curve to a stage time series (Time Series Data element) to derive a discharge time series.
- Refit with **more segments** if structural breaks in the data are visible in the residual plot.