# usgs-01570500-susquehanna-rating-curve

## Overview

Stage-discharge rating curve for the Susquehanna River at Harrisburg, PA (USGS gage 01570500), fit using the piecewise power-law rating curve model with Bayesian MCMC.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `USGS - 01570500 - Measured Discharge` | Field-measured (rated) discharge for Susquehanna River at Harrisburg, PA. |
| `USGS - 01570500 - Measured Stage` | Field-measured gage height for Susquehanna River at Harrisburg, PA. |

### Rating Curve Analysis

| Element | Description |
|---|---|
| `USGS 01570500 Rating Curve` | Bayesian piecewise power-law rating-curve fit to the field-measured stage-discharge pairs at Susquehanna River at Harrisburg, PA (USGS gage 01570500). |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/6-rating-curve-analysis/`.
3. Open `usgs-01570500-susquehanna-rating-curve.bestfit`.

### Exploring the Elements

For each Rating Curve Analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Rating Curve** tab to view the fit overlaid on the measured stage / discharge pairs.
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
Below are the expected results for Rating Curve Analysis labeled "USGS 01570500 Rating Curve"; this should be the only analysis in the list.

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Zero-Flow Stage (h₁) | 2.44039 | 0.0164549 | 2.41197 | 2.44128 | 2.46579 | 1.0001 | 9139 | 
| Coefficient (α₁) | 3.96436 | 0.0122525 | 3.94376 | 3.96477 | 3.98379 | 1.0000 | 9574 | 
| Exponent (β₁) | 1.38494 | 0.0151449 | 1.36035 | 1.38458 | 1.4101 | 0.9999 | 9974 | 
| Scale (σ) | 0.0701198 | 0.00309255 | 0.0652849 | 0.0699853 | 0.0752692 | 0.9998 | 9889 | 


### Frequency / Quantile Table
While in the **Rating Curve Results** tab to the left, select Tabular Results to see the fitted curve values. Below are the first 30 rows of the table.

| Stage	| 95.0% CI | 5.0% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| -0.45600000000000085 | 0 | 0 | 0 | 0 | 
| -0.08436363636363714 | 0 | 0 | 0 | 0 | 
| 0.28727272727272657 | 0 | 0 | 0 | 0 | 
| 0.6589090909090902 | 0 | 0 | 0 | 0 | 
| 1.030545454545454 | 0 | 0 | 0 | 0 | 
| 1.4021818181818178 | 0 | 0 | 0 | 0 | 
| 1.7738181818181815 | 0 | 0 | 0 | 0 | 
| 2.145454545454545 | 0 | 0 | 0 | 0 | 
| 2.517090909090909 | 410.12960132743984 | 151.87508813768804 | 266.15623259502286 | 262.9657731506839 | 
| 2.8887272727272726 | 3971.982016764386 | 2325.0262202060535 | 3065.360870705832 | 3032.890388385699 | 
| 3.2603636363636364 | 9151.094538089374 | 5375.701338618381 | 7074.5486749976335 | 6998.084920858722 | 
| 3.632 | 15340.455815440519 | 9020.179796275253 | 11872.480974922595 | 11743.611844830442 | 
| 4.003636363636364 | 22333.748844913265 | 13142.060614056078 | 17291.030763407693 | 17103.16337676947 | 
| 4.375272727272727 | 30009.06156052017 | 17660.4544658233 | 23232.95384647871 | 22980.49385557292 | 
| 4.7469090909090905 | 38265.817325339776 | 22512.86837712238 | 29633.181250236117 | 29311.190395279562 | 
| 5.118545454545454 | 47061.69238012302 | 27700.58551851628 | 36444.42793122674 | 36048.441324163694 | 
| 5.490181818181817 | 56338.554683823895 | 33178.007983298536 | 43630.432762026285 | 43156.34763762324 | 
| 5.8618181818181805 | 66064.57709621015 | 38915.18433552818 | 51162.310025553525 | 50606.312046937375 | 
| 6.233454545454544 | 76195.61450852503 | 44908.96799324281 | 59016.38645260097 | 58374.89792307115 | 
| 6.605090909090907 | 86748.24265848288 | 51098.9660734954 | 67172.82836438731 | 66442.47020035432 | 
| 6.9767272727272704 | 97665.47830414843 | 57531.15808242689 | 75614.72371260155 | 74792.28655537555 | 
| 7.348363636363634 | 108886.05380527019 | 64174.45102292412 | 84327.4424959677 | 83409.86416204533 | 
| 7.719999999999997 | 120478.71283824365 | 71005.36161485847 | 93298.17600172696 | 92282.52348551288 | 
| 8.091636363636361 | 132428.09441208516 | 78011.73185585815 | 102515.59556253898 | 101399.0504039863 | 
| 8.463272727272726 | 144703.78185673998 | 85155.71858423622 | 111969.59386799404 | 110749.44006799415 | 
| 8.83490909090909 | 157180.90060800588 | 92494.72237830091 | 121651.0849140589 | 120324.69881875388 | 
| 9.206545454545454 | 169960.86250388614 | 99968.03791720078 | 131551.8466116676 | 130116.6883454302 | 
| 9.578181818181818 | 183059.71322994496 | 107662.33096474277 | 141664.3950811694 | 140118.00121654128 | 
| 9.949818181818182 | 196413.87538077153 | 115431.68111328108 | 151981.88291419522 | 150321.8601432342 | 
| 10.321454545454547 | 210025.9171024459 | 123454.0092924765 | 162498.01585849313 | 160722.03548458245 | 
| 10.69309090909091 | 223740.3142318154 | 131602.71860607626 | 173206.98386802667 | 171312.7769771015 | 

### Plots
There are plenty of plots to expore with our Rating Curve Analysis. Under the **Rating Curve Results** tab we have our fitted stage-discharge curve plotted with the orginal data.
![Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.](../images/susquehanna-rating-curve.png)

*Figure 1: Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.*

To explore how good the fit is we can look at our residuals in the **Residual Diagnostics** tab. The Residuals Plot shows the residuals of the dicharge values against the fitted stage values.
![Residuals of measured discharge minus rating-curve estimate, plotted against stage.](../images/susquehanna-residuals.png)

*Figure 2: Residuals of measured discharge minus rating-curve estimate, plotted against stage.*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain trace for the rating-curve parameter h₁, zero-flow stage.](../images/susquehanna-trace-h1.png)

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