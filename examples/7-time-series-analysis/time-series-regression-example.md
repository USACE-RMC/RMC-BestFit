# time-series-regression-example

## Overview

Multivariate time-series regression on classic US macroeconomic indicators (consumption, income, production, savings, unemployment). Demonstrates simple and multiple linear regression with autocorrelated residuals.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Consumption` | Quarterly US personal consumption expenditures (response variable for the regression examples). |
| `Income` | Quarterly US personal disposable income (regressor). |
| `Production` | Quarterly US industrial production index (regressor). |
| `Savings` | Quarterly US personal savings rate (regressor). |
| `Unemployment` | Quarterly US unemployment rate (regressor). |

### Time Series Analysis

| Element | Description |
|---|---|
| `Simple Linear Regression` | Simple linear regression of consumption on income, with autocorrelated residuals modeled as ARMA. |
| `Multiple Linear Regression` | Multiple linear regression of consumption on income, production, savings, and unemployment, with autocorrelated residuals modeled as ARMA. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/7-time-series-analysis/`.
3. Open `time-series-regression-example.bestfit`.

### Exploring the Elements

For each Time Series Analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Time Series Results** tab to view the fitted mean (and trend, if any) overlaid on the observations.
3. Open the **Residual Diagnostics** tab to view the  ACF and PACF plots to confirm white-noise residuals.
4. Adjust **ForecastSteps** in the **Properties** panel under **Output** to extend the forecast horizon.


## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any Time Series Analysis to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Time Series Analysis labeled "Simple Linear Regression"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Intercept (μ) | 0.581308 | 0.06669 | 0.472763 | 0.581872 | 0.692189 | 1.0003 | 9747 | 
| Covariate (β₁) | 0.32473 | 0.0574467 | 0.230149 | 0.324924 | 0.416953 | 1.0000 | 9422 | 
| Scale (σ) | 0.604545 | 0.0350395 | 0.5497 | 0.603117 | 0.66489 | 1.0000 | 9446 | 


### Frequency / Quantile Table
While in the **Time Series Results** tab to the left, select Tabular Results to see the fitted curve values. Below are the first 30 rows of the table.

| Date Time	| 95.0% CI | 5.0% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| 1/1/1970 12:00:00 AM | 1.8947967439767812 | -0.08878776398652398 | 0.8887434844939699 | 0.897030287594515 | 
| 4/1/1970 12:00:00 AM | 1.959509460950857 | -0.035207498646478416 | 0.9615312807695431 | 0.9609447677632177 | 
| 7/1/1970 12:00:00 AM | 2.103883210152694 | 0.08210326467655665 | 1.0935854275675025 | 1.0857012889939535 | 
| 10/1/1970 12:00:00 AM | 1.4912828064776469 | -0.5410753358916635 | 0.4880024850919481 | 0.4984138181279866 | 
| 1/1/1971 12:00:00 AM | 2.197477919467802 | 0.21177767713762502 | 1.2087580214909495 | 1.226595979674443 | 
| 4/1/1971 12:00:00 AM | 2.0481764325470753 | 0.045591224947840264 | 1.0476641494475256 | 1.0513006078429146 | 
| 7/1/1971 12:00:00 AM | 1.7439809007184266 | -0.2492498112689949 | 0.7522165843167626 | 0.7540034124420042 | 
| 10/1/1971 12:00:00 AM | 1.9501939864959803 | -0.04002912542873376 | 0.9601734488165874 | 0.9580353266927637 | 
| 1/1/1972 12:00:00 AM | 1.7083225077155817 | -0.25621023137652815 | 0.7228842059184816 | 0.729713496717202 | 
| 4/1/1972 12:00:00 AM | 1.9034111649832308 | -0.07737956769735911 | 0.9218215912295864 | 0.9114363881820144 | 
| 7/1/1972 12:00:00 AM | 2.192788616849879 | 0.17910740905670403 | 1.1982901584014605 | 1.1996264145523563 | 
| 10/1/1972 12:00:00 AM | 2.8733959666254334 | 0.8049833282255875 | 1.841359241946771 | 1.8445905659909538 | 
| 1/1/1973 12:00:00 AM | 1.811933586944315 | -0.17824681496894523 | 0.8040869552646184 | 0.8112989448623736 | 
| 4/1/1973 12:00:00 AM | 1.8354761535350508 | -0.18895806036359272 | 0.8271318730889791 | 0.8392441633957352 | 
| 7/1/1973 12:00:00 AM | 1.7264599592271277 | -0.2622279819682317 | 0.7232152818125525 | 0.722181967793024 | 
| 10/1/1973 12:00:00 AM | 1.9238596595936186 | -0.07167677726105205 | 0.9372133166245797 | 0.9365007678304581 | 
| 1/1/1974 12:00:00 AM | 1.0529754866217218 | -0.975922390456731 | 0.045869642911720965 | 0.04171003123914929 | 
| 4/1/1974 12:00:00 AM | 1.2837895246284106 | -0.7349352587644401 | 0.28244658832559466 | 0.2765972350692004 | 
| 7/1/1974 12:00:00 AM | 1.6078955836694329 | -0.40652708731113396 | 0.6139470689246684 | 0.6119913076316229 | 
| 10/1/1974 12:00:00 AM | 1.5306087639646597 | -0.45460877399805993 | 0.5421552205214099 | 0.5414977727492499 | 
| 1/1/1975 12:00:00 AM | 1.5339752553006398 | -0.45026544187014966 | 0.5327265584878059 | 0.5281515571909432 | 
| 4/1/1975 12:00:00 AM | 3.1044046405555696 | 0.9877488499845604 | 2.0617173608233292 | 2.0544473796226073 | 
| 7/1/1975 12:00:00 AM | 1.122666115653703 | -0.9046251667002961 | 0.10742333564655293 | 0.10598035662758598 | 
| 10/1/1975 12:00:00 AM | 1.8345530439272693 | -0.1819291098935081 | 0.8328262644184481 | 0.8286430302545305 | 
| 1/1/1976 12:00:00 AM | 1.9423207482175358 | -0.06113694111504899 | 0.9499031945955801 | 0.960676180641524 | 
| 4/1/1976 12:00:00 AM | 1.7442966055329143 | -0.24294762657036303 | 0.7454331590098825 | 0.7492906535818611 | 
| 7/1/1976 12:00:00 AM | 1.8026756871512377 | -0.15350308094661344 | 0.8224136870951197 | 0.8195625345155143 | 
| 10/1/1976 12:00:00 AM | 1.7722238818252865 | -0.22678748322095407 | 0.7695961146030798 | 0.7743871652268851 | 
| 1/1/1977 12:00:00 AM | 1.5819214817338088 | -0.42724202225557156 | 0.5683582070140585 | 0.5712157162941516 | 
| 4/1/1977 12:00:00 AM | 2.006543903320349 | -0.02295309101369193 | 0.9826707951014787 | 0.9833526804624401 | 
| 7/1/1977 12:00:00 AM | 2.07848769800467 | 0.08160724804220226 | 1.074194940119109 | 1.0745086316117098 | 

### Plots
There are plenty of plots to expore with our Time Series Analysis. Under the **Time Series Results** tab there is fitted curve plotted with the orginal data. This plot has both training and prediction estimates.
![Time-series fit overlaid on the observations, with credible band.](../images/ts-regression-time-series.png)

*Figure 1: Time-series fit overlaid on the observations, with credible band.*

To take a closer look at the forecasting capability, extend the Forecast Steps in the **Properties** panel under **Options** from 0 to 50. Return the **General** section under the **Properties** panel and select **Estimate**. Then you will have the plot below.
![Multi-step forecast extension with credible band.](../images/ts-regression-forecast.png)

*Figure 2: Multi-step forecast extension with credible band.*

To explore how good the fit is we can look at our residuals in the **Residual Diagnostics** tab. Investigate the ACF plot of the resodiauls to ensure residuals are not correlated.
![Residual autocorrelation function.](../images/ts-regression-acf.png)

*Figure 3: Residual autocorrelation function.*

### MCMC Diagnostics

One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps

- Use the fitted ARIMA / regression model for **multi-step forecasting** with credible bands.
- Inspect residual ACF / PACF to confirm no remaining temporal structure.
- For non-stationary trend cases, project the trend function out beyond the observation window.