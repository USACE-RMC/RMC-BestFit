# Rating Curve Analysis Examples

This chapter demonstrates Bayesian fitting of stage-discharge rating curves using a piecewise power-law model:

$$Q = \alpha (h - \xi)^\beta$$

where Q is discharge, h is stage, $\xi$ is the gauge zero offset, and $\alpha$, $\beta$ are segment-specific power-law parameters. Multi-segment models extend this with breakpoints $h_2$, $h_3$ between segments.

## Examples

| Example | Site | Description |
|---|---|---|
| [Synthetic Rating Curve Examples](synthetic-rating-curve-examples.md) | Synthetic | One-, two-, and three-segment fits on synthetic data with known ground truth — verifies the rating-curve solver |
| [Susquehanna River at Harrisburg, PA](usgs-01570500-susquehanna-rating-curve.md) | USGS 01570500 | Rating curve fit to field-measured stage-discharge pairs |
| [Mississippi River at New Madrid, MO](usgs-07024175-mississippi-rating-curve.md) | USGS 07024175 | Rating curve fit to field-measured stage-discharge pairs |
| [MF Willamette River](usgs-14145000-willamette-rating-curve.md) | USGS 14145000 | Rating curve fit to field-measured stage-discharge pairs |

## Parameter Layout

Rating-curve parameter vectors are arranged as:

| Segments | Parameter Vector |
|---|---|
| 1 | [$\xi$, log10($\alpha$), $\beta$, $\sigma$] |
| 2 | [$\xi$, log10($\alpha_1$), $\beta_1$, $h_2$, log10($\alpha_2$), $\beta_2$, $\sigma$] |
| 3 | [$\xi$, log10($\alpha_1$), $\beta_1$, $h_2$, log10($\alpha_2$), $\beta_2$, $h_3$, log10($\alpha_3$), $\beta_3$, $\sigma$] |

The error term $\sigma$ is the standard deviation of log-discharge (log-normal residual model).

## How to Use These Examples

1. Open RMC-BestFit 2.0.
2. Select **File > Open** and navigate to this folder.
3. Open the `.bestfit` file.
4. Expand **Time Series Data** to see the measured stage and discharge series.
5. Expand **Rating Curve Analysis** to see the fitted rating curve(s).
6. Click an analysis to view the **Rating Curve** plot and **Properties** pane.
7. Open the matching `.md` tutorial for a step-by-step guide.

## Required Data

Each Rating Curve Analysis element needs **two paired Time Series Data elements** of the **MeasuredStage** and **MeasuredDischarge** entry methods (or any time series with synchronized timestamps). The user selects the two time series in the Properties panel of the analysis.

## Output

A fitted rating curve produces:

- **Posterior parameter samples** for $\xi$, $\alpha_i$, $\beta_i$, $h_i$, $\sigma$.
- **Posterior predictive bands** at user-selected stage ordinates (controlled via `MinStage`, `MaxStage`, `StageBins` in the Properties panel).
- **Application output**: feeding a stage Time Series Data element through the fitted rating curve produces a discharge time series with credible bands.

## Screenshot Images

Capture screenshots into an `images/` subfolder next to each example. Naming convention: `<example-slug>-<view-name>.png` (e.g., `susquehanna-rating-curve.png`, `synthetic-rc-residuals.png`).

## Next Steps

After fitting a rating curve:

- Apply it to a **continuous stage time series** to derive a continuous discharge time series.
- Use the derived discharge series in [examples/2-input-data/](../2-input-data/) to extract a flood-frequency sample.
- Pair upstream and downstream rating curves with [examples/5-bivariate-distribution-analysis/](../5-bivariate-distribution-analysis/) for joint reach-scale analyses.
