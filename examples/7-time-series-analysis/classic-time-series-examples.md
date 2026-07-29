# Manual Data Entry Example

## Overview

This example demonstrates how to enter time series data manually in RMC-BestFit by copy-pasting from CSV files. The project contains three classic datasets widely used in time series analysis textbooks and statistical software documentation.

Manual entry is useful when your data comes from published tables, spreadsheets, or other sources not covered by the built-in download options (USGS, GHCN, CHMN, ABOM, HEC-DSS).

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `Airline Passengers` | Montly Box-Jenkins airline data from 1949--1960 on passengers (thousands) |
| `Nile River Flows` | Nile annual flow at Aswan from 1871--1970 | 
| `Mauna Loa CO2` | Monthly atmospheric CO2 at Mauna Loa from 1958--2023 


### Time Series Analysis

| Element | Description |
|---|---|
| `Airline Passengers - TSA` | ARIMA(1,1) fit on the airline passengers series|
| `Nile River Flows - TSA` | ARIMA(1,1) fit on the nile river flows series|
| `Mauna Loa CO2 - TSA` | ARIMA(1,1) fit on the mauna loa CO2 series|


## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/7-time-series-analysis/`.
3. Open `classic-time-series-examples.bestfit`.

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
Below are the expected results for Time Series Analysis labeled "Airline Passengers TSA"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Intercept (μ) | 3.69058 | 2.46234 | 0.500619 | 3.33337 | 8.21983 | 1.0009 | 384 | 
| AR (φ₁) | -0.452556 | 0.167228 | -0.687214 | -0.463899 | -0.163509 | 1.0009 | 314 | 
| MA (θ₁) | 0.858079 | 0.143573 | 0.603049 | 0.880236 | 1.07974 | 1.0009 | 216 | 
| Scale (σ) | 24.4462 | 1.89685 | 21.6432 | 24.4837 | 27.5085 | 0.9998 | 249 | 

### Frequency / Quantile Table
While in the **Time Series Results** tab to the left, select Tabular Results to see the fitted curve values. Below are the first 30 rows of the table.

| Date Time	| 95.0% CI | 5.0% CI | Posterior Predictive | Posterior Mean |
|---|---|---|---|---|
| 1/1/1949 12:00:00 AM | 112 | 112 | 112 | 112 | 
| 2/1/1949 12:00:00 AM | 155.50306848218966 | 73.96644756646296 | 114.20892346256238 | 114.64543595749552 | 
| 3/1/1949 12:00:00 AM | 167.3155538869901 | 86.4070617978895 | 126.96653894287915 | 126.7680944533226 | 
| 4/1/1949 12:00:00 AM | 169.72828181564307 | 87.54195370242937 | 128.6129271751446 | 128.62049223674185 | 
| 5/1/1949 12:00:00 AM | 174.00565933306817 | 92.62326543872076 | 134.00042757986066 | 134.01647979442254 | 
| 6/1/1949 12:00:00 AM | 166.8148882163111 | 85.59903614038397 | 126.30528188045956 | 127.73355216867266 | 
| 7/1/1949 12:00:00 AM | 180.7188589828192 | 100.12349917295025 | 140.50590398518474 | 139.85464658098547 | 
| 8/1/1949 12:00:00 AM | 188.94633622663878 | 107.66053261586009 | 148.1799630065681 | 149.1951055332268 | 
| 9/1/1949 12:00:00 AM | 188.286054795453 | 107.84718071742975 | 148.56196298416728 | 147.4690122051194 | 
| 10/1/1949 12:00:00 AM | 174.04256827220283 | 92.92159571096944 | 133.45238592584684 | 134.92252464984122 | 
| 11/1/1949 12:00:00 AM | 160.48418972782565 | 79.98254706703221 | 120.85101511243971 | 119.2024986078328 | 
| 12/1/1949 12:00:00 AM | 154.5728703457039 | 72.3902061857265 | 113.436934091467 | 114.8643270320847 | 
| 1/1/1950 12:00:00 AM | 154.5297922662331 | 73.79194842556835 | 114.08896078149476 | 112.82175964752082 | 
| 2/1/1950 12:00:00 AM | 168.4861587309925 | 87.28596071704018 | 127.57268914025985 | 129.2648577704603 | 
| 3/1/1950 12:00:00 AM | 166.98502839095863 | 84.60129563854622 | 125.91325659205985 | 125.20323804458738 | 
| 4/1/1950 12:00:00 AM | 184.87765809446537 | 103.64993702041437 | 143.66249927013277 | 144.61132516193476 | 
| 5/1/1950 12:00:00 AM | 174.37328701862427 | 92.6152461375387 | 133.99688082787912 | 133.2067501449578 | 
| 6/1/1950 12:00:00 AM | 181.77134786901934 | 101.28513325983644 | 141.09385279064193 | 141.6320593914075 | 
| 7/1/1950 12:00:00 AM | 190.34999141034933 | 108.91432155353085 | 149.45009743106198 | 148.6051265153419 | 
| 8/1/1950 12:00:00 AM | 216.7065362172883 | 133.93478789670115 | 175.42392077666298 | 175.69960704489537 | 
| 9/1/1950 12:00:00 AM | 205.61450311163844 | 124.93514445276162 | 165.75551791788746 | 165.60379526816178 | 
| 10/1/1950 12:00:00 AM | 198.03577670554847 | 116.88942294217028 | 157.13391286144875 | 156.99500664861785 | 
| 11/1/1950 12:00:00 AM | 171.37175200603218 | 91.64251054433261 | 131.56194830863174 | 131.5182141253022 | 
| 12/1/1950 12:00:00 AM | 171.87155032951324 | 90.68522051065791 | 131.37330028571628 | 131.17584338520726 | 
| 1/1/1951 12:00:00 AM | 174.1600135056469 | 91.4742655898628 | 132.54359934277184 | 132.65016015222736 | 
| 2/1/1951 12:00:00 AM | 199.46821233214465 | 116.80058477417379 | 158.82200405657542 | 158.69512727611598 | 
| 3/1/1951 12:00:00 AM | 195.10582442549153 | 113.86190478015185 | 153.92314428742432 | 154.96390163214244 | 
| 4/1/1951 12:00:00 AM | 214.70921713294896 | 133.87932013889747 | 174.35900825103218 | 173.01852151868164 | 
| 5/1/1951 12:00:00 AM | 215.60750656364547 | 134.67773349892434 | 174.65054085798272 | 176.28497598338666 | 
| 6/1/1951 12:00:00 AM | 210.6083648359847 | 128.49453053692375 | 169.62520833910395 | 168.39435301671406 | 
| 7/1/1951 12:00:00 AM | 234.88506227865088 | 152.62997898803158 | 193.7027022598576 | 194.97067190188676 | 
| 8/1/1951 12:00:00 AM | 231.6487515720483 | 150.21055678225923 | 190.69281753137443 | 189.79860191176218 | 

### Plots
There are plenty of plots to expore with our Time Series Analysis. Under the **Time Series Results** tab there is fitted curve plotted with the orginal data. This plot has both training and prediction estimates.
![Time-series fit overlaid on the observations, with credible band.](..images/classic-ts-time-series.png)

*Figure 1: Time-series fit overlaid on the observations, with credible band.*

To take a closer look at the forecasting capability, extend the Forecast Steps in the **Properties** panel under **Options** from 0 to 50. Return the **General** section under the **Properties** panel and select **Estimate**. Then you will have the plot below.
![Multi-step forecast extension with credible band.](..images/classic-ts-forecast.png)

*Figure 2: Multi-step forecast extension with credible band.*

To explore how good the fit is we can look at our residuals in the **Residual Diagnostics** tab. Investigate the ACF plot of the resodiauls to ensure residuals are not correlated.
![Residual autocorrelation function.](..images/classic-ts-acf.png)

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
