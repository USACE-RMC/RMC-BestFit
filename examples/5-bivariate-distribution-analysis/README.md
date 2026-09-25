# Bivariate and coincident frequency analysis

A marginal distribution describes one variable. A copula describes dependence between two variables after their marginal behavior has been specified. Coincident frequency analysis combines that joint model with a response surface to estimate a response's exceedance probability.

## Choose an example

| Tutorial | What you will learn | Important distinction |
|---|---|---|
| [Six copula families](1-bivariate-distributions/bivariate-distribution-examples.md) | Inspect paired observations, simulation, log-density contours and joint exceedance contours. | Each family uses a separate synthetic dataset; information criteria do not rank families across those different samples. |
| [Sum of two Normals](2-coincident-frequency/sum-two-normals.md) | Follow margins, dependence and an X + Y response surface through three cases. | Generating correlation and fitted correlation differ; saved response bands use 95% in the near-zero case and 90% in the other two. |
| [Waimea response frequency](2-coincident-frequency/waimea-river-stage-frequency.md) | Inspect an existing study with marginal alternatives, copulas and a response surface. | Hydraulic provenance and response units remain unresolved; the filename alone does not define stage or discharge. |

## Follow the dependencies

1. Open the saved project and retain a separate working copy.
2. Inspect the two input records: units, timestamps/indexes, low-outlier flags and common observations matter. Pairing by index can use fewer observations than either marginal fit.
3. Review the selected marginal fits under **Univariate Distribution Analysis**. Confirm their observations, priors and diagnostics.
4. Open the named copula under **Bivariate Distribution Analysis**. Compare observed pairs, simulations and the requested joint probability. “Both exceed” and “either exceeds” answer different questions.
5. For a coincident analysis, inspect the full response grid, its units, range and bins. A numerical response surface needs a physical interpretation before its output can support a study.

Contour labels in the density view are natural logarithms of joint density, while joint-exceedance contours are probabilities. CDF-axis views change the coordinates used to draw the same model; check the tutorial before interpreting a label. Coincident plots place response on the vertical axis and AEP on the horizontal axis, with uncertainty bounds in probability at fixed response.

A chosen copula is a modeling assumption requiring evidence about the relevant dependence and tails. None of these examples establishes a universally preferred family. Read the [author issue log](../../docs/example-issues-for-haden.md) for unresolved source questions.

Figures use the maintained Python renderer. See [reproduction instructions](../README.md#reproducing-the-figures) or return to the [example index](../README.md).
