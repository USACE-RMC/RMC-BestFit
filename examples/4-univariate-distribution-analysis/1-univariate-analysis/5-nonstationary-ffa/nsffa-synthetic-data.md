# nsffa-synthetic-data

## Overview

Synthetic NSFFA datasets covering nine trend types (constant, cubic, exponential, linear, logistic, power, quadratic, sinusoidal, step). Each dataset has a paired NSFFA fit using the matching trend function for verifying nonstationary parameter recovery.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Constant Trend Data` | Synthetic LP-III sample with a constant location parameter (no trend) — control case. |
| `Cubic Trend Data` | Synthetic LP-III sample with a cubic trend on the location parameter. |
| `Exponential Trend Data` | Synthetic LP-III sample with an exponential trend on the location parameter. |
| `Linear Trend Data` | Synthetic LP-III sample with a linear trend on the location parameter. |
| `Logistic Trend Data` | Synthetic LP-III sample with a logistic trend on the location parameter. |
| `Power Trend Data` | Synthetic LP-III sample with a power-law trend on the location parameter. |
| `Quadratic Trend Data` | Synthetic LP-III sample with a quadratic trend on the location parameter. |
| `Sinusoidal Trend Data` | Synthetic LP-III sample with a sinusoidal (annual cycle) trend on the location parameter. |
| `Step Function Data` | Synthetic LP-III sample with a step-change on the location parameter. |

### Univariate Distribution

| Element | Description |
|---|---|
| `NSFFA - Constant Trend` | NSFFA fit with a constant trend on the constant-trend synthetic data — should recover the ground-truth parameters. |
| `NSFFA - Cubic Trend` | NSFFA fit with a cubic trend on the cubic-trend synthetic data. |
| `NSFFA - Exponential Trend` | NSFFA fit with an exponential trend on the exponential-trend synthetic data. |
| `NSFFA - Linear Trend` | NSFFA fit with a linear trend on the linear-trend synthetic data. |
| `NSFFA - Logistic Trend` | NSFFA fit with a logistic trend on the logistic-trend synthetic data. |
| `NSFFA - Power Trend` | NSFFA fit with a power-law trend on the power-trend synthetic data. |
| `NSFFA - Quadratic Trend` | NSFFA fit with a quadratic trend on the quadratic-trend synthetic data. |
| `NSFFA - Sinusoidal Trend` | NSFFA fit with a sinusoidal trend on the sinusoidal-trend synthetic data. |
| `NSFFA - Step Function` | NSFFA fit with a step-change trend on the step-function synthetic data. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/`.
3. Open `nsffa-synthetic-data.bestfit`.

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
Below are the expected results for Univariate Distribution Analysis labeled "NSFFA - Constant Trend"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Mean (µ) (α) | 99.5411 | 1.57796 | 97.0008 | 99.5288 | 102.129 | 0.9998 | 9148 | 
| Std Dev (σ) (α) | 15.8357 | 1.13377 | 14.0828 | 15.7845 | 17.805 | 0.9998 | 9502 | 

### Frequency / Quantile Table
While in the **Distribution Results** tab to the left, select Tabular Results to see the frequency plot's value at each return level probability,

| Probability | 95.0% CI | 5.0% CI | Posterior Predictive| Posterior Mean |
|---|---|---|---|---|
| 1E-06 | 184.3986811306106 | 166.09160370885257 | 179.33533268307318 | 174.81488380694753 | 
| 2E-06 | 181.88080292982076 | 164.08918745976325 | 176.67763039698977 | 172.56554944105454 | 
| 5E-06 | 178.44582262855764 | 161.31244993389606 | 173.08662954494156 | 169.49011381427005 | 
| 1E-05 | 175.75424923029334 | 159.1469807991118 | 170.30410710057805 | 167.0786111709531 | 
| 2E-05 | 173.00438342411556 | 156.91484360629414 | 167.45756643293393 | 164.58589480096975 |
| 5E-05 | 169.14392802962823 | 153.84694126315034 | 163.58167869595133 | 161.15132408095576 |
| 0.0001 | 166.11598067273457 | 151.40596229309372 | 160.5503627912524 | 158.43430664208742 |
| 0.0002 | 162.93926571455387 | 148.8749121767023 | 157.4210416299446 | 155.60078140355856 |
| 0.0005 | 158.54315230335658 | 145.34262025093224 | 153.1062034254563 | 151.64886929267988 |
| 0.001 | 155.0204741418768 | 142.4920320734264 | 149.6808122127199 | 148.47706585537478 |
| 0.002 | 151.27759829038555 | 139.46741034431665 | 146.0892696102765 | 145.1187789110287 |
| 0.005 | 145.980630113438 | 135.1639741876048 | 141.0245624714157 | 140.33113164264498 |
| 0.01 | 141.60790311061032 | 131.59322710581668 | 136.8899450490493 | 136.38041731608803 |
| 0.02 | 136.8604898357548 | 127.6689386205512 | 132.4127272347358 | 132.06362054180232 |
| 0.05 | 129.77236484978837 | 121.76240421762942 | 125.76625179360431 | 125.58847544389704 |
| 0.1 | 123.50434331143296 | 116.45857822114345 | 119.91810394534339 | 119.83533116499021 |
| 0.2 | 116.0014118466962 | 109.92979217138674 | 112.89030351568675 | 112.86872419694997 |
| 0.3 | 110.66639394648573 | 105.16344498655792 | 107.8486229350258 | 107.84531043395849 |
| 0.5 | 102.12866281849715 | 97.00083250682037 | 99.5392154882996 | 99.54105860048675 |
| 0.7 | 93.9480914020765 | 88.45153227188185 | 91.23085573275128 | 91.23680676701501 |
| 0.8 | 89.17261482814797 | 83.13769648779389 | 86.19093384076564 | 86.21339300402353 |
| 0.9 | 82.61900231978896 | 75.6086621579995 | 79.16663086368398 | 79.24678603598329 |
| 0.95 | 77.30162024358943 | 69.3085800266881 | 73.32217762282431 | 73.49364175707646 |
| 0.98 | 71.36971153508752 | 62.20214921843593 | 66.68084525691921 | 67.01849665917116 |
| 0.99 | 67.44003985262658 | 57.443486957319735 | 62.207857879084564 | 62.70169988488546 |

### Plots
There are also plenty of plots to explore our results with. Under the **Distribution Results** tab we have a frequency curve.
![Frequency curve (AEP versus quantile), with the credible band.](.../images/nsffa-synthetic-frequency.png)

*Figure 1: Frequency curve (AEP versus quantile), with the credible band.*

The **Kernal Density** tab shows an estimate for the pdf of each parameter.
![Posterior kernel density for mean parameter (µ).](.../images/nsffa-synthetic-kernel-density-mean.png)

*Figure 2: Posterior kernel density for mean parameter (µ).*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain traces for mean parameter (µ).](.../images/nsffa-synthetic-trace-mean.png)

*Figure 3: Markov-chain traces for mean parameter (µ).*

Finally, we can investigate the autocorrelation of the chains under the **Autocorrelation** tab.
![Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).](.../images/nsffa-synthetic-autocorrelation-mean.png)

*Figure 4: Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).*

### MCMC DiagnosticsDiagnostics
One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs

## Next Steps

- Compare analyses via the **Bayesian Model Average** element to combine results from multiple distributions. 
	- Right click the **Univariate Distribution Analysis** drop down and select "New Composite Distribution Analysis"
	- Select from the available options in the **Properties** panel to the right
- Compare analyses by **DIC**, **WAIC**, and **LOO-CV** information criteria.
- Project **conditional return-period quantiles** for future time indices using the trend functions.