# Bivariate Distribution Analysis Examples

This chapter covers two-variable joint analyses using **copulas**, with two workflows:

- **Bivariate Distribution Fitting** — fit a copula to paired data after fitting marginals separately, to estimate joint AEPs (AND / OR / Kendall return periods).
- **Coincident Frequency Analysis (CFA)** — derive a univariate response distribution from the joint, e.g., the sum, difference, or maximum of two correlated processes.

RMC-BestFit supports six copula families: **AMH, Clayton, Frank, Gumbel, Joe, and Normal (Gaussian)**.

## Sub-Chapters

| Sub-Chapter | Workflow | What It Demonstrates |
|---|---|---|
| [1-bivariate-distributions](1-bivariate-distributions/) | Bivariate fitting | Fitting all six copula families to synthetic paired data |
| [2-coincident-frequency](2-coincident-frequency/) | CFA | Deriving a univariate response from a bivariate fit |

## Examples

| Example | Sub-Chapter | Description |
|---|---|---|
| [Bivariate Distribution Examples](1-bivariate-distributions/bivariate-distribution-examples.md) | 1- | Fits all six copula families on six synthetic paired datasets |
| [Sum of Two Normals](2-coincident-frequency/sum-two-normals.md) | 2- | CFA verification using the sum of two correlated normals (closed-form analytical reference for rho = -0.5, 0, +0.5) |
| [Waimea River Stage Frequency](2-coincident-frequency/waimea-river-stage-frequency.md) | 2- | Coincident peak-flow analysis for the Waimea and Makaweli Rivers, Kauai, HI |

## Workflow Overview

```
        +---------------------+
        |  Input Data X       |  -->  Univariate fit X (margin)
        +---------------------+                                       \
                                                                       \
        +---------------------+                                       --->  Bivariate copula fit  -->  Coincident Frequency Analysis (optional)
        |  Input Data Y       |  -->  Univariate fit Y (margin)       /
        +---------------------+                                       /
```

Each Bivariate Distribution Analysis element references two existing Univariate Distribution alternatives (marginal X and marginal Y). Each Coincident Frequency Analysis element references one Bivariate Distribution alternative.

## How to Use These Examples

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to a sub-chapter folder.
3. Open the `.bestfit` file.
4. Expand the **Univariate Distribution** collection first to see the marginal fits.
5. Expand the **Bivariate Distribution** collection to see the copula fits.
6. (Sub-chapter 2 only) Expand the **Coincident Frequency** collection to see the derived univariate response.
7. Open the matching `.md` tutorial for a step-by-step guide.

## Choosing a Copula Family

| Family | Tail Behavior | Typical Hydrologic Use |
|---|---|---|
| **Normal (Gaussian)** | No tail dependence | Default when correlation is moderate and tails behave normally |
| **Gumbel** | Upper tail dependence | Concurrent extreme floods, tropical cyclone wind / rain |
| **Clayton** | Lower tail dependence | Concurrent low-flow / drought events |
| **Frank** | Symmetric, no tail dependence | Mid-range correlation without heavy joint tails |
| **Joe** | Strong upper tail dependence | Rarely used; similar niche to Gumbel |
| **AMH** | Limited dependence range | Pedagogical / verification; low-correlation cases |

Compare candidates by AIC / BIC; Gumbel and Frank are the typical winners for hydrologic peaks.

## Screenshot Images

Capture screenshots into `images/` subfolders next to each example. Naming convention: `<example-slug>-<view-name>.png` (e.g., `waimea-joint-density.png`, `sum-two-normals-marginal-x.png`).

## Next Steps

After bivariate / CFA analysis:

- **Rating Curve:** [examples/6-rating-curve-analysis/](../6-rating-curve-analysis/) — pair a CFA-derived stage with a rating curve for derived discharge frequency.
- **Time Series Analysis:** [examples/7-time-series-analysis/](../7-time-series-analysis/) — model the joint temporal structure of two correlated time series.
