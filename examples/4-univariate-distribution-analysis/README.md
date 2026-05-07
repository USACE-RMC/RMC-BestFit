# Univariate Distribution Analysis Examples

This chapter covers single-variable flood-frequency and probability analyses on annual maximum, peaks-over-threshold, and other extreme-value samples. RMC-BestFit's univariate workflow is **Bayesian-first**: every example here uses MCMC posterior estimation as the primary method, with Bulletin 17C, mixture, point-process, and composite alternatives as supporting workflows.

## Sub-Chapters

| Sub-Chapter | Method | What It Demonstrates |
|---|---|---|
| [1-univariate-analysis](1-univariate-analysis/) | Bayesian MCMC | Information expansion, ARR/FLIKE replications, Bulletin 17C parity fits, MOVE.3 record extension, non-stationary FFA |
| [2-bulletin-17C-analysis](2-bulletin-17C-analysis/) | Bulletin 17C | Bulletin 17C method with MVN-quantile and bias-corrected bootstrap (BCB) intervals; companion to the Bayesian fits in 1- |
| [3-point-process-analysis](3-point-process-analysis/) | Non-homogeneous Poisson | Peaks-over-threshold (POT) with stationary and seasonal point processes; comparison against GEV block-maximum |
| [4-mixture-analysis](4-mixture-analysis/) | Mixture distribution | Two- and three-component normal mixtures, including a zero-inflated variant |
| [5-composite-analysis](5-composite-analysis/) | Composite distribution | Mixed-population (snow + rain) flood frequency via competing-risks and AMS-mixture formulations |

## Examples

### 1-univariate-analysis

| Example | Description |
|---|---|
| [Blakely Mountain Dam (Bayesian)](1-univariate-analysis/1-information-expansion/blakely-mountain-dam-bayesian.md) | Information expansion using systematic + historical + paleoflood records (LP-III) |
| [Viglione et al. (2013)](1-univariate-analysis/1-information-expansion/viglione-et-al-2013.md) | Replicates the published Kamp at Zwettl temporal- and causal-expansion examples |
| [ARR FLIKE Examples](1-univariate-analysis/2-arr-flike/arr-flike-examples.md) | Bayesian replication of the Australian Rainfall and Runoff worked examples |
| [Bulletin 17C Examples (Bayesian)](1-univariate-analysis/3-bulletin17C-examples/bulletin-17c-bayesian-examples.md) | Bayesian re-fits of the seven Bulletin 17C example datasets |
| [Sinnemahoning MOVE.3 (Bayesian)](1-univariate-analysis/4-measurement-errors/sinnemahoning-move3-bayesian.md) | Record extension via MOVE.3 regression with measurement-error propagation |
| [NSFFA — Brays Bayou, Texas](1-univariate-analysis/5-nonstationary-ffa/nsffa-brays-bayou-texas.md) | Non-stationary flood-frequency at USGS gage 08075000 with multiple trend functions |
| [NSFFA — OC Fisher Dam](1-univariate-analysis/5-nonstationary-ffa/nsffa-oc-fisher-dam.md) | Non-stationary inflows with constant/linear/step/sinusoidal trends |
| [NSFFA — Synthetic Data](1-univariate-analysis/5-nonstationary-ffa/nsffa-synthetic-data.md) | Nine synthetic NSFFA datasets used to verify nonstationary parameter recovery |

### 2-bulletin-17C-analysis

| Example | Description |
|---|---|
| [Bulletin 17C Examples](2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.md) | The seven Bulletin 17C example datasets fit with MVN and bias-corrected bootstrap intervals |
| [Blakely Mountain Dam (B17C)](2-bulletin-17C-analysis/2-information-expansion/blakely-mountain-dam-b17c.md) | B17C information expansion with regional skew and quantile penalties |
| [Sinnemahoning MOVE.3 (B17C)](2-bulletin-17C-analysis/3-measurement-errors/sinnemahoning-move3-b17c.md) | B17C version of the MOVE.3 measurement-error example |

### 3-point-process-analysis, 4-mixture-analysis, 5-composite-analysis

| Example | Description |
|---|---|
| [Point Process Examples](3-point-process-analysis/point-process-examples.md) | Stationary and seasonal Poisson point processes on daily-precipitation POT series |
| [Mixture Distribution Examples](4-mixture-analysis/mixture-distribution-examples.md) | Two- and three-component normal mixtures + zero-inflated variant |
| [Mixed-Population Examples](5-composite-analysis/mixed-population-examples.md) | Composite snow + rain flood-type combination via competing-risks and AMS-mixture |

## How to Use These Examples

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to a sub-chapter folder (e.g., `1-univariate-analysis/3-bulletin17C-examples/`).
3. Open the `.bestfit` file.
4. Expand the analysis collections in the Project Explorer to see the alternatives.
5. Click an alternative to view its Frequency, Trace, Autocorrelation, and Properties panes.
6. Open the matching `.md` tutorial in this folder for a step-by-step guide.

## MCMC Convergence Reminder

Before interpreting any result:

- **R-hat** should be < 1.01 for every parameter.
- **Effective Sample Size** should be at least a few hundred per parameter.
- Trace plots should look like fuzzy, well-mixed caterpillars — no drift, no sticking.

The DEMCzs sampler is the default workhorse and converges reliably on routine LP-III, GEV, and Gumbel fits within 2,000-5,000 iterations after a 1,000-2,000 warmup.

## Screenshot Images

Tutorial screenshot placeholders reference images in an `images/` subfolder under each example. To add screenshots:

1. Create an `images/` folder in the example's directory.
2. Capture screenshots from RMC-BestFit matching the alt-text descriptions.
3. Save as PNG with the filename specified in each image reference.
4. Naming convention: `<example-slug>-<view-name>.png` (e.g., `viglione-frequency.png`, `blakely-trace.png`).

## Next Steps

After univariate frequency analysis:

- **Bivariate / Coincident Frequency:** [examples/5-bivariate-distribution-analysis/](../5-bivariate-distribution-analysis/) — pair two univariate fits via a copula and derive joint AEPs.
- **Rating Curve:** [examples/6-rating-curve-analysis/](../6-rating-curve-analysis/) — derive a discharge series from a stage time series for downstream univariate analysis.
- **Time Series Analysis:** [examples/7-time-series-analysis/](../7-time-series-analysis/) — fit ARIMA / regression models to non-extreme time-series records.
