# arr-flike-examples

## Overview

Bayesian replication of the Australian Rainfall and Runoff (ARR) flood-frequency worked examples. Demonstrates the LP-III distribution applied to systematic, censored, and historical flood records following the FLIKE software conventions.
These examples follows the methodology provided by Australian Rainfall and Runoff using the Flike software, as included in the RMC-BestFit Verification Report for version 1.0.

## What's Inside

### Input Data

| Element | Description |
|---|---|
| `Example #3` | Hunter River at Singleton |
| `Example #4` | ARR Example #4 input data (peak-flow record from the Australian Rainfall and Runoff worked examples). |
| `Example #6a` | I do not have the source data for this gauge. I am using the table of flows provided in ARR for the Wimmera River at Glynwylin. The flows are provided in descending order, with no years. This is why the chronology plot looks odd. |
| `Example #6b` | I do not have the source data for this gauge. I am using the table of flows provided in ARR for the Wimmera River at Glynwylin. The flows are provided in descending order, with no years. This is why the chronology plot looks odd. |

### Univariate Distribution

| Element | Description |
|---|---|
| `Example #3` | Bayesian LP-III fit replicating ARR Example #3 (Hunter River at Singleton). |
| `Example #4` | Bayesian LP-III fit replicating ARR Example #4. |
| `Example #5` | Bayesian LP-III fit replicating ARR Example #5 (with low-outlier censoring). |
| `Example #6a` | Bayesian LP-III fit replicating ARR Example #6a (Wimmera River at Glynwylin, with historical record extension). |
| `Example #6b` | Bayesian LP-III fit replicating ARR Example #6b (Wimmera River, alternative historical interpretation). |

### Bulletin 17C

| Element | Description |
|---|---|
| `B17C - Example 5 - MVN` | B17C fit of ARR Example #5 using multivariate-normal quantile confidence intervals. |
| `B17C - Example 5 - Bootstrap` | B17C fit of ARR Example #5 using bias-corrected bootstrap confidence intervals. |
| `B17C - Example 3 - MVN` | B17C fit of ARR Example #3 using multivariate-normal quantile confidence intervals. |
| `B17C - Example 3 - Bootstrap` | B17C fit of ARR Example #3 using bias-corrected bootstrap confidence intervals. |
| `B17C - Example 5 - Bootstrap_copy` | Duplicate of the Example #5 bootstrap fit (kept for comparison; safe to delete). |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/1-univariate-analysis/2-arr-flike/`.
3. Open `arr-flike-examples.bestfit`.

### Exploring the Elements

For each Univariate Distribution anlysis:

1. Click the analysis in the Project Explorer.
2. The **Distirbution Results** tab to the left shoudld automatically open. Navigate to the **Frequency** tab to view the AEP-vs-quantile plot.
3. Open the **Markov Chain Traces** tab on the left to confirm chain mixing (well-mixed traces look like fuzzy caterpillars).
4. Open the **Autocorrelation** tab on the left to check effective sample size.
5. Inspect the **Properties** panel on the right for sampler settings (iterations, warmup, point estimator, credible-interval width).

These will be explored more below in the Expected results section.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel to the right of any analysis to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Univariate Distribution Analysis labeled "Example #3"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Mean (of log) (µ) | 2.79105 | 0.115766 | 2.60569| 2.78975 | 2.98087 | 1.0001 | 9161 |
| Std Dev (of log) (σ) | 0.625022 | 0.0944821 | 0.494548 | 0.613411 | 0.793603 | 0.9999 | 8792 |
| Skew (of log) (γ) | 0.117887 | 0.479936 | -0.647196 | 0.105425 | 0.916886 | 0.9998 | 8474 |

### Frequency / Quantile Table
While in the **Distribution Results** tab to the left, select Tabular Results to see the frequency plot's value at each return level probability,

| Probability | 95.0% CI | 5.0% CI | Posterior Predictive| Posterior Mean |
|---|---|---|---|---|
| 1E-06	| 507894873.72939694 | 31400.078902128 | 28240390327.448273	| 1075113.0276012735 |
| 2E-06	| 276498072.0650721 | 29703.731020245366 | 4178308800.2617283 | 843070.6337592058 |
| 5E-06	| 123143469.32011892 | 27446.509484261216 | 468992685.5418231 | 605824.2507058325 |
| 1E-05	| 66897069.18047385 | 25785.637852707303 | 113467310.2610285 | 468273.54718754545 |
| 2E-05	| 36110754.31549659 | 23960.6771624511 | 32503412.322293572 | 359351.65119201073 |
| 5E-05	| 15490445.31614817 | 21533.357889403625 | 7655298.062862985 | 250120.05487908743 |
| 0.0001 | 8194152.477189892 | 19763.849546885987 | 2900441.373266232 | 188156.07098303808 |
| 0.0002 | 4313801.305197655 | 17889.172856339643 | 1196930.0344285506 | 140086.57779670059 |
| 0.0005 | 1808397.596807069 | 15415.097597258653 |413730.23095646885 | 93128.56376379418 |
| 0.001 | 935210.4917356665 | 13490.83667465741	| 198574.1128616403	| 67286.61199721164 |
| 0.002	| 481340.9695161464 | 11421.750844987131 | 100359.01765326156 | 47818.47878708741 |
| 0.005	| 196545.8043277691	| 8967.912598595607	| 43682.00991930671	| 29519.941628798613 |
| 0.01 | 97540.94370518686	| 7143.213278992808	| 24396.56901115398	| 19907.0627403773 |
| 0.02 | 47267.37407231437	| 5454.21743072416 | 14080.975182438142	| 12997.168821870711 |
| 0.05 | 17711.242944315694	| 3447.045402600443	| 6950.820553226646	| 6912.468882893855 |
| 0.1 | 8033.157444202306 | 2205.4649474226485 | 3944.4710282473447	| 3976.6793706847543 |
| 0.2 | 3531.667258731276 | 1235.542928477663 | 2044.9496448434477 | 2056.7470089856315 |
| 0.3 | 2099.5519839106696 | 797.0496176357684 | 1283.7201345980825	| 1287.3508688852967 |
| 0.5 | 953.5864424919969 | 378.1418507798334 | 599.9661367871256 | 600.8571704801963 |
| 0.7 | 456.1352344190664 | 178.38187411251013 | 284.578089119132 | 284.83869585941505 |
| 0.8 | 298.46299667716636 | 111.14612101564778 | 183.0319428884676	| 182.70759329641467 |
| 0.9 | 170.79926405224717 | 53.23615358318095 | 100.37478638288647	| 99.62163322187797 |
| 0.95 | 113.02739780461646 | 26.612825620081423 | 61.037092310896966 | 60.862042588817005 |
| 0.98 | 75.38460109045634 | 11.557231103937012	| 33.158380600761035 | 35.25834219121622 |
| 0.99 | 60.3582851999637 | 6.436732082882738 | 20.60363776336522 | 24.62723628788043 |


### Plots
There are also plenty of plots to explore our results with. Under the **Distribution Results** tab we have a frequency curve.
![Frequency curve (AEP versus quantile), with the credible band.](.../images/arr-flike-examples-frequency.png)

*Figure 1: Frequency curve (AEP versus quantile), with the credible band.*

The **Kernal Density** tab shows an estimate for the pdf of each parameter.
![Posterior kernel density for mean parameter (µ).](.../images/arr-flike-examples-kernel-density-mean.png)

*Figure 2: Posterior kernel density for mean parameter (µ).*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain traces for mean parameter (µ).](.../images/arr-flike-examples-trace-mean.png)

*Figure 3: Markov-chain traces for mean parameter (µ).*

Finally, we can investigate the autocorrelation of the chains under the **Autocorrelation** tab.
![Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).](.../images/arr-flike-examples-autocorrelation-mean.png)

*Figure 4: Autocorrelation function of the chains, used to estimate effective sample size, for mean parameter (µ).*

### MCMC Diagnostics

One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps
- Compare analyses via the **Bayesian Model Average** element to combine results from multiple distributions. 
	- Right click the **Univariate Distribution Analysis** drop down and select "New Composite Distribution Analysis"
	- Select from the available options in the **Properties** panel to the right
- Compute **return-period quantiles** (1%, 0.5%, 0.2% AEP) from the frequency-curve table.
- Re-run with informative **quantile priors** if engineering judgment suggests specific upper-bound flood magnitudes.
- Cross-check the LP-III fit against a **Bulletin 17C** fit on the same input data.