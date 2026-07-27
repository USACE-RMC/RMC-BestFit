# nsffa-oc-fisher-dam

## Overview

Nonstationary flood-frequency analysis (NSFFA) for inflows to OC Fisher Dam, Texas. Compares constant, linear, step, and sinusoidal trend functions on the LP-III parameters and combines them via Bayesian model averaging.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `OC Fisher Inflows` | Annual peak inflow series to OC Fisher Dam, Texas. |

### Univariate Distribution

| Element | Description |
|---|---|
| `SFFA` | Stationary frequency analysis (LP-III) — baseline for comparison with the non-stationary alternatives. |
| `NSFFA - Constant` | Non-stationary fit with a constant trend (functionally equivalent to SFFA, kept for explicit comparison). |
| `NSFFA - Linear Trend` | Non-stationary fit with a linear trend on the location parameter. |
| `NSFFA - Step Function` | Non-stationary fit with a step-change on the location parameter. |
| `NSFFA - Sinusoidal Trend` | Non-stationary fit with a sinusoidal (annual cycle) trend on the location parameter. |

### Composite Distribution

| Element | Description |
|---|---|
| `Bayesian Model Average` | Bayesian model average over the five NSFFA alternatives, weighted by DIC / WAIC. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/`.
3. Open `nsffa-oc-fisher-dam.bestfit`.

### Exploring the Elements
For each Univariate Distribution anlysis:

1. Click the analysis in the Project Explorer.
2. The **Distirbution Results** tab to the left shoudld automatically open. Navigate to the **Frequency** tab. The displayed curve is conditional on the time index in the **Properties** panel — change it to see how the curve evolves.
3. Open the **Chronology** tab to view the time-varying location / scale through the record.
4. Use the **Bayesian Model Average** Composite Distribution element to view the trend-marginalized AEP curve.
 
## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any analysis to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Univariate Distribution Analysis labeled "SFFA"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Mean (of log) (µ) | 1.30722 | 0.0653592 | 1.20199 | 1.30708 | 1.41529 | 0.9998 | 9173 | 
| Std Dev (of log) (σ) | 0.710358 | 0.0451466 | 0.641823 | 0.707455 | 0.788422 | 0.9999 | 9044 | 
| Skew (of log) (γ) | 0.0292537 | 0.196149 | -0.303485 | 0.0354567 | 0.343264 | 0.9999 | 9686 | 

### Frequency / Quantile Table
While in the **Distribution Results** tab to the left, select Tabular Results to see the frequency plot's value at each return level probability,

| Probability | 95.0% CI | 5.0% CI | Posterior Predictive| Posterior Mean |
|---|---|---|---|---|
| 1E-06 | 302046.8803614846 | 11247.799806657618 | 130545.69418424394 | 57405.18099912636 | 
| 2E-06 | 214050.29079938665 | 9744.457929884586 | 91689.25194575514 | 45020.32594576404 | 
| 5E-06 | 133093.4494137589 | 8037.138869476555 | 57528.890995286885 | 32309.784195425098 | 
| 1E-05 | 92275.36551455075 | 6846.878985724154 | 40441.78593705815 | 24920.01663013401 |
| 2E-05 | 63455.7595075625 | 5756.474127766755 | 28418.02754282829 | 19060.56054273523 | 
| 5E-05 | 38368.1233392033 | 4498.58322699299 | 17792.84356584133 | 13183.254938483899 | 
| 0.0001 | 25959.82839264131 | 3703.7632737824065 | 12454.9484864471 | 9853.593903038738 | 
| 0.0002 | 17370.707110738 | 3009.79305453177 | 8688.739188699017 | 7277.219343602523 | 
| 0.0005 | 10100.999216362416 | 2223.4395070993623 | 5355.803251038273 | 4772.627452835385 | 
| 0.001 | 6622.266600951803 | 1735.5899675790176 | 3682.268564069477 | 3404.3225043920447 | 
| 0.002 | 4264.814937152258 | 1324.5807503606418 | 2504.815504430233 | 2382.2285796151045 | 
| 0.005 | 2320.30868995125 | 885.4800792787247 | 1470.0596054214902 | 1433.8008763510543 | 
| 0.01 | 1423.4128332037897 | 623.7909401110849 | 957.385423015678 | 944.1051925998491 | 
| 0.02 | 850.688679860863 | 418.2615573889831 | 603.7862131288001 | 598.7336802551114 | 
| 0.05 | 412.27046090070553 | 222.62127720905525 | 305.0004330169159 | 303.0596269617336 | 
| 0.1 | 221.72664475180224 | 124.84198204300263 | 166.92862842434786 | 165.87164656242902 | 
| 0.2 | 106.90801984518048 | 60.64808714622956 | 80.61161823482772 | 80.17439343419109 | 
| 0.3 | 63.321943588692626 | 36.10123720421038 | 47.74147209995996 | 47.555637149608884 | 
| 0.5 | 26.59748677598419 | 15.390452803634735 | 20.14723890916291 | 20.12575850397788 | 
| 0.7 | 11.316810525793144 | 6.511585897438564 | 8.55450328156172 | 8.554750725731763 | 
| 0.8 | 6.83744051412828 | 3.7935027160091006 | 5.110933724740746 | 5.10946327432983 | 
| 0.9 | 3.5226856616438345 | 1.717434490162799 | 2.5055993732941477 | 2.50673698759172 | 
| 0.95 | 2.1057335913970343 | 0.8589285604107446 | 1.3857106828817778 | 1.395459254330488 | 
| 0.98 | 1.2181058964432496 | 0.37710741463945474 | 0.701233486818966 | 0.7235809412629356 | 
| 0.99 | 0.8657450107280343 | 0.21521422228204037 | 0.4379718320865267 | 0.4677038455215546 | 

### Plots
There are also plenty of plots to explore our results with. Under the **Distribution Results** tab we have a frequency curve.
![Frequency curve (AEP versus quantile), with the credible band.](...images/nsffa-oc-fisher-frequency.png)

*Figure 1 Frequency curve (AEP versus quantile), with the credible band.*

The **Kernal Density** tab shows an estimate for the pdf of each parameter.
![Posterior kernel density for mean parameter (µ).](...images/nsffa-oc-fisher-kernel-density-mean.png)

*Figure 2: Posterior kernel density for mean parameter (µ).*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain traces for mean parameter (µ).](...images/nsffa-oc-fisher-trace-mean.png)

*Figure 3: Markov-chain traces for mean parameter (µ).*

Finally, we can investigate the autocorrelation of the chains under the **Autocorrelation** tab.
![Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).](...images/nsffa-oc-fisher-autocorrelation-mean.png)

*Figure 4: Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).*

### MCMC Diagnostics
One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps
- Compare analyses by **DIC**, **WAIC**, and **LOO-CV** information criteria.
- Average results across analyses via the **Bayesian Model Average** Composite Distribution element.
- Project **conditional return-period quantiles** for future time indices using the trend functions.
