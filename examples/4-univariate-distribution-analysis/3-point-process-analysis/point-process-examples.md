# point-process-examples

## Overview

Peaks-over-threshold (POT) modeling using non-homogeneous Poisson point processes for daily precipitation at two GHCN stations (USC00040741 Big Bear Lake CA, USC00042402). Compares stationary and seasonal point-process fits against GEV block-maximum analyses on the same record.

## What's Inside

### Time Series Data

| Element | Description |
|---|---|
| `USC00040741` | Daily precipitation for GHCN station USC00040741 (Big Bear Lake, CA). |
| `USC00042402` | Daily precipitation for GHCN station USC00042402. |

### Input Data

| Element | Description |
|---|---|
| `USC00040741 - POT` | Peaks-over-threshold precipitation series extracted from USC00040741. |
| `USC00040741 - AMS` | Annual maximum precipitation series extracted from USC00040741. |
| `USC00042402 - POT` | Peaks-over-threshold precipitation series extracted from USC00042402. |
| `USC00042402 - AMS` | Annual maximum precipitation series extracted from USC00042402. |

### Univariate Distribution

| Element | Description |
|---|---|
| `USC00040741 - GEV` | Bayesian GEV fit on the AMS series at USC00040741 — comparison baseline for the point-process fits. |
| `USC00042402 - GEV` | Bayesian GEV fit on the AMS series at USC00042402 — comparison baseline for the point-process fits. |

### Point Process

| Element | Description |
|---|---|
| `USC00040741 - Point Process` | Stationary non-homogeneous Poisson point-process fit on the POT series at USC00040741. |
| `USC00042402 - Point Process` | Stationary non-homogeneous Poisson point-process fit on the POT series at USC00042402. |
| `USC00040741 - Seasonal Point Process` | Seasonal (sinusoidal-rate) Poisson point-process fit on the POT series at USC00040741. |

## Step-by-Step Walkthrough

### Opening the Project

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to `examples/4-univariate-distribution-analysis/3-point-process-analysis/`.
3. Open `point-process-examples.bestfit`.

### Exploring the Elements

For each Point Process analysis:

1. Click the analysis in the Project Explorer.
2. Open the **Frequency** tab to view the AEP curve derived from the fitted intensity function.
3. Compare against the **GEV block-maximum** alternative on the same record.
4. For seasonal point processes, inspect the rate function over the annual cycle.

## Analysis Settings

Each Bayesian analysis in this project uses the DEMCzs sampler with project-specific iteration / warm-up settings. Open the **Properties** panel of any alternative to inspect:

- **Sampler type** (DEMCzs, ARWMH, HMC).
- **Iterations / Warm-up Iterations** — total post-warmup samples per chain.
- **Number of Chains** — typically 6 for routine work.
- **Thinning Interval** — keeps every Nth sample to reduce storage / autocorrelation.
- **Point Estimator** — Posterior Mean (default), Posterior Median, or Posterior Mode (MAP).
- **Credible Interval Width** — typically 0.90 or 0.95.

## Expected Results
Below are the expected results for Point Process Analysis labeled "USC00040741 - Point Process"; this should be the first analysis in the list.
Be sure to explore all of the analyses provided!

### Parameter Estimates
The parameter estimates are found under the **MCMC Report** tab to the left as parameter summary statistics.

| Parameter | Mean | Std Dev | 5% | Median | 95% |R-hat | ESS |
|---|---|---|---|---|---|---|---|
| Location (ξ) | 2.36446 | 0.119512 | 2.17884 | 2.35997 | 2.56968 | 0.9998 | 9299 | 
| Scale (α) | 1.1211 | 0.0890772 | 0.988996 | 1.11566 | 1.27624 | 1.0001 | 9452 | 
| Shape (κ) | -0.132656 | 0.0679166 | -0.250412 | -0.128314 | -0.0296323 | 1.0002 | 9655 | 

### Frequency / Quantile Table
While in the **Distribution Results** tab to the left, select Tabular Results to see the frequency plot's value at each return level probability,

| Probability | 95.0% CI | 5.0% CI | Posterior Predictive| Posterior Mean |
|---|---|---|---|---|
| 1E-06 | 150.84742737241032 | 20.653979425471444 | 118.64024523334363 | 46.7397957632871 | 
| 2E-06 | 126.36944349162967 | 19.546371503627217 | 94.81941905017696 | 42.099022259113205 | 
| 5E-06 | 100.24184536687025 | 18.122746805249296 | 71.29990628356329 | 36.58394425811294 | 
| 1E-05 | 83.95977937371478 | 17.05293749465014 | 57.94724500771058 | 32.83534514226579 | 
| 2E-05 | 69.99612120141246 | 15.992409439227242 | 47.41944026371997 | 29.41604706697312 | 
| 5E-05 | 55.255274556819735 | 14.64347424419414 | 36.74132263154507 | 25.352537238078842 | 
| 0.0001 | 46.14234693981825 | 13.65581705014883 | 30.505369026003066 | 22.59052678246136 | 
| 0.0002 | 38.41303546097958 | 12.655700558655765 | 25.46553900386915 | 20.071078507880806 | 
| 0.0005 | 30.057464804325864 | 11.385035786594663 | 20.204697475682224 | 17.07674290064117 | 
| 0.001 | 24.911630438443996 | 10.44583441542861 | 17.036302326564794 | 15.04114888110213 | 
| 0.002 | 20.51019183389008 | 9.542945332733689 | 14.40380614941757 | 13.183801619839143 | 
| 0.005 | 15.812371222605194 | 8.362681989850216 | 11.558593508979538 | 10.97480093075537 | 
| 0.01 | 12.879593110421727 | 7.505224291199614 | 9.776332964052276 | 9.470766350191022 | 
| 0.02 | 10.46497115795248 | 6.639410491573149 | 8.236700760100655 | 8.094524352155947 | 
| 0.05 | 7.842946001602478 | 5.517392191102903 | 6.483370080771518 | 6.445702646629233 | 
| 0.1 | 6.177040717516899 | 4.667457792424369 | 5.3114482136768 | 5.3043503691108835 | 
| 0.2 | 4.7403199750165586 | 3.806013007347718 | 4.221888392945112 | 4.225001004399712 | 
| 0.3 | 3.9812707798818154 | 3.2813687340211 | 3.5982540068529874 | 3.602988811768916 | 
| 0.5 | 3.0340724951059537 | 2.559296911433748 | 2.7799484304483886 | 2.7855044187904583 | 
| 0.7 | 2.3451789920629706 | 1.9893989044219564 | 2.1526838875417478 | 2.158890695863173 | 
| 0.8 | 2.00515709772634 | 1.7014038001299205 | 1.8410610349876277 | 1.8474328607209767 | 
| 0.9 | 1.6050129281077634 | 1.3625560915017505 | 1.4732448309439914 | 1.4792931120449715 | 
| 0.95 | 1.3268729985339036 | 1.1182551063325952 | 1.2143033558814498 | 1.2197266540063072 | 
| 0.98 | 1.0634446975101701 | 0.8679460645015797 | 0.960578974786177 | 0.9655937519053599 | 
| 0.99 | 0.911384148668507 | 0.7125284717251967 | 0.8091127253526231 | 0.8146247420912087 | 

### Plots
There are also plenty of plots to explore our results with. Under the **Distribution Results** tab we have a frequency curve.
![Frequency curve (AEP versus quantile), with the credible band.](...images/point-process-frequency.png)

*Figure 1: Frequency curve (AEP versus quantile), with the credible band.*

The **Kernal Density** tab shows an estimate for the pdf of each parameter.
![Posterior kernel density for location parameter (ξ).](...images/point-process-kernel-density-location.png)

*Figure 2: Posterior kernel density for location parameter (ξ).*

The **Markov Chain Traces** tab explores the traces of each Markov chain as it explores the posterior space in order to converge.
![Markov-chain traces for location parameter (ξ).](...images/point-process-trace-location.png)

*Figure 3: Markov-chain traces for location parameter (ξ).*

Finally, we can investigate the autocorrelation of the chains under the **Autocorrelation** tab.
![Autocorrelation function of the chains, used to estimate effective sample size, for location parameter (ξ).](...images/point-process-autocorrelation-location.png)

*Figure 4: Autocorrelation function of the chains, used to estimate effective sample size, for location parameter (ξ).*

### MCMC Diagnostics
One should always verify chain convergence before interpreting any results. There are a variety of ways including:

- **R-hat** — should be < 1.01 for every parameter.
- **Effective Sample Size (ESS)** — at least a few hundred per parameter.
- **Trace plots** — should look like fuzzy, well-mixed caterpillars (no drift, no sticking).
- **Posterior** — overall posterior log-likelihood should be visually stationary in the mean-likelihood plot.

These can be found under the **Markov Chain Traces** and **MCMC Reports** tabs.

## Next Steps

- Compare the **Point Process** AEP curve to the **GEV block-maximum** AEP curve on the same record.
- Add a **seasonal** sinusoidal-rate point process to capture annual-cycle clustering.
- Compute **expected number of exceedances** above engineering thresholds from the fitted intensity.