# usgs-07024175-mississippi-rating-curve

## Overview

Stage-discharge rating curve for the Mississippi River at New Madrid, MO (USGS gage 07024175), fit using the piecewise power-law rating curve model with Bayesian MCMC.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `USGS 07024175 Measured Stage` | Field-measured (rated) gage height for the Mississippi River at New Madrid, MO (USGS gage 07024175). |
| `USGS 07024175 Measured Discharge` | Field-measured (rated) discharge for the Mississippi River at New Madrid, MO (USGS gage 07024175). |

### Rating Curve Analysis

| Element | Description |
|---|---|
| `USGS 07024175 Rating Curve` | Bayesian piecewise power-law rating-curve fit to the field-measured stage-discharge pairs at the Mississippi River at New Madrid, MO. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/6-rating-curve-analysis/`.
3. Open `usgs-07024175-mississippi-rating-curve.bestfit`.

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
Below are the expected results for Rating Curve Analysis labeled "USGS 07024175 Rating Curve"; this should be the only analysis in the list.

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Zero-Flow Stage (h₁) | -50.9737 | 1.41209 | -52.4399 | -51.3797 | -48.1138 | 1.0003 | 7963 | 
| Coefficient (α₁) | -0.247074 | 0.167744 | -0.446818 | -0.285831 | 0.0819723 | 1.0001 | 8212 | 
| Exponent (β₁) | 3.25476 | 0.0765488 | 3.10656 | 3.27074 | 3.34947 | 1.0001 | 8212 | 
| Scale (σ) | 0.0232005 | 0.00174484 | 0.0205575 | 0.0230843 | 0.0262762 | 1.0000 | 8839 | 

### Frequency / Quantile Table
While in the **Rating Curve Results** tab to the left, select Tabular Results to see the fitted curve values. Below are the first 30 rows of the table.

| Stage	| 95.0% CI | 5.0% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| -10.32 | 107233.48891328025 | 89252.44853582536 | 97882.67401248094 | 97759.59878226908 | 
| -9.751515151515152 | 112172.76090829527 | 93377.98975839319 | 102409.32164524312 | 102279.5145581598 | 
| -9.183030303030304 | 117253.96542707036 | 97640.07304896398 | 107078.94336402623 | 106942.1838946469 | 
| -8.614545454545455 | 122500.95875572132 | 102054.03832586182 | 111894.01551078865 | 111750.08131880655 | 
| -8.046060606060607 | 127890.56193725689 | 106606.6364913999 | 116857.02301816594 | 116705.6899486076 | 
| -7.477575757575758 | 133438.95034031325 | 111309.80867250207 | 121970.45932662637 | 121811.50140683203 | 
| -6.90909090909091 | 139180.48496136305 | 116120.46020672606 | 127236.82630351723 | 127070.01573698898 | 
| -6.340606060606062 | 145096.5650285967 | 121118.93112461768 | 132658.63416393162 | 132483.74132115053 | 
| -5.772121212121213 | 151180.01910969758 | 126219.27592831891 | 138238.40139333345 | 138055.19479964004 | 
| -5.203636363636365 | 157427.82875562625 | 131494.12946011117 | 143978.6546718787 | 143786.90099250973 | 
| -4.635151515151517 | 163884.41464408263 | 136892.50742681848 | 149881.92880037543 | 149681.3928227443 | 
| -4.066666666666668 | 170512.41057897243 | 142467.8016645793 | 155950.76662782748 | 155741.2112411322 | 
| -3.49818181818182 | 177330.61671481933 | 148176.6060519974 | 162187.7189805075 | 161968.90515274883 | 
| -2.9296969696969715 | 184338.32190598545 | 154043.2946927888 | 168595.34459251 | 168367.03134499607 | 
| -2.361212121212123 | 191544.69904533925 | 160057.6919361752 | 175176.2100377358 | 174938.1544171493 | 
| -1.7927272727272747 | 198929.62166599787 | 166221.51788061377 | 181932.8896632624 | 181684.8467113616 | 
| -1.2242424242424264 | 206522.26096658685 | 172551.67600688594 | 188867.9655240547 | 188609.68824507756 | 
| -0.6557575757575779 | 214262.00326850175 | 179085.99821003486 | 195984.02731897478 | 195715.26664481362 | 
| -0.08727272727272939 | 222213.24909972525 | 185772.66122881282 | 203283.67232805074 | 203004.1770812607 | 
| 0.4812121212121191 | 230375.92431337215 | 192627.1154367028 | 210769.50535096496 | 210479.0222056684 | 
| 1.0496969696969676 | 238769.8553911058 | 199651.54465301434 | 218444.1386467255 | 218142.4120874721 | 
| 1.618181818181816 | 247359.77250322487 | 206855.60670217167 | 226310.19187448363 | 225996.9641531231 | 
| 2.1866666666666643 | 256134.09194474702 | 214269.8842207569 | 234370.2920354657 | 234045.30312608884 | 
| 2.7551515151515127 | 265118.53833764564 | 221924.51001557292 | 242627.07341598434 | 242290.0609679854 | 
| 3.323636363636361 | 274355.3979176643 | 229689.27004171046 | 251083.17753149863 | 250733.87682081136 | 
| 3.8921212121212094 | 283857.78469559737 | 237565.3026476537 | 259741.25307169303 | 259379.3969502485 |
| 4.460606060606058 | 293473.7527909951 | 245676.13656637978 | 268603.95584654587 | 268229.27469000156 | 
| 5.029090909090907 | 303341.5539648264 | 254024.34969453176 | 277673.9487333584 | 277286.1703871448 | 
| 5.597575757575755 | 313490.936832673 | 262533.73442931875 | 286953.9016247211 | 286552.7513484489 | 
| 6.166060606060603 | 323883.36784028145 | 271216.1227831109 | 296446.49137738615 | 296031.6917876595 | 
| 6.734545454545452 | 334437.7400930983 | 280120.2952789973 | 306154.40176202514 | 305725.6727737018 | 

### Plots
There are plenty of plots to expore with our Rating Curve Analysis. Under the **Rating Curve Results** tab we have our fitted stage-discharge curve plotted with the orginal data.
![Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.](../images/mississippi-rating-curve.png)

*Figure 1: Stage-discharge rating curve fit, with measured pairs and Bayesian credible band.*

To explore how good the fit is we can look at our residuals in the **Residual Diagnostics** tab. The Residuals Plot shows the residuals of the dicharge values against the fitted stage values.
![Residuals of measured discharge minus rating-curve estimate, plotted against stage.](../images/mississippi-residuals.png)

*Figure 2: Residuals of measured discharge minus rating-curve estimate, plotted against stage.*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain trace for the rating-curve parameter h₁, zero-flow stage.](../images/mississippi-trace-h1.png)

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
