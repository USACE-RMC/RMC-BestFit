# RMC-BestFit Example Projects

This folder contains tutorial example projects for RMC-BestFit 2.0. Each `.bestfit` file is a self-contained SQLite project that can be opened directly in BestFit; each `.md` file alongside is a step-by-step tutorial guide describing what's inside, how to explore it, and what the expected results look like.

## Chapters

| # | Chapter | Topic |
|---|---|---|
| 1 | [Time Series Data](1-time-series-data/) | Importing and downloading raw time series from USGS, GHCN, CHMN, ABOM, HEC-DSS, and manual entry |
| 2 | [Input Data](2-input-data/) | Extracting flood-frequency samples (block maximum, peaks-over-threshold, USGS peak-flow) from time series |
| 4 | [Univariate Distribution Analysis](4-univariate-distribution-analysis/) | Bayesian, Bulletin 17C, point-process, mixture, and composite frequency analyses |
| 5 | [Bivariate Distribution Analysis](5-bivariate-distribution-analysis/) | Copula-based bivariate fitting and coincident frequency analysis |
| 6 | [Rating Curve Analysis](6-rating-curve-analysis/) | Bayesian piecewise power-law stage-discharge rating curves |
| 7 | [Time Series Analysis](7-time-series-analysis/) | ARIMA / ARIMAX / regression fitting with autocorrelated residuals |

(Chapter 3 is reserved for distribution-fitting workflows already covered inline within chapter 4.)

## Project Naming Convention

All projects follow **lowercase kebab-case**: `example-name.bestfit`. ASCII only, hyphens between words, no spaces, no commas, no Title Case. Where two projects in different sub-folders solve the same problem with different methods (Bayesian vs. Bulletin 17C, etc.), suffixes such as `-bayesian` and `-b17c` disambiguate.

## How to Open an Example

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to one of the chapter folders above.
3. Open the `.bestfit` file.
4. The Project Explorer will show the full tree of Time Series Data, Input Data, and Analysis elements.
5. Click any element to view its plots and properties.
6. Open the matching `.md` tutorial in the same folder for a step-by-step guide.

## Screenshot Conventions

Tutorial markdown files reference screenshots in `images/` subfolders next to each example. To populate them:

1. Create an `images/` folder in the example's directory.
2. Capture screenshots from RMC-BestFit matching the alt-text descriptions.
3. Save as PNG with the filename specified in each image reference.
4. Naming convention: `<example-slug>-<view-name>.png`.

## Filling in Output Tables

The analysis-tutorial markdowns (chapters 4-7) include placeholder tables for parameter estimates, AEP / quantile values, and MCMC diagnostics. These are intentionally empty so users can paste in real values from their own re-runs:

- **Parameter table** — right-click an analysis alternative > **Open MCMC Report** > copy the parameter section.
- **Frequency / AEP table** — right-click the Frequency chart > **Copy Table**.
- **R-hat / ESS values** — copy from the MCMC Report or from the Properties panel under MCMC Diagnostics.

## Verification Status

The `synthetic-*` projects under chapters 6 and 7 are **verification fixtures** — they contain synthetic data generated from known parameters, and the corresponding fits should recover the ground truth within MCMC sampling noise. They are useful both as tutorials and as smoke tests when validating new builds.

## Reporting Issues

If an example fails to open, produces unexpected results, or has an inaccurate description, please open an issue with:

- The `.bestfit` filename.
- The element name(s) involved.
- The BestFit version (visible in **Help > About**).
- A screenshot of the error, if any.
