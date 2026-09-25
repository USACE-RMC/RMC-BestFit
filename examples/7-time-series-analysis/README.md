# Time-series analysis

Time-series models retain observation order and can describe trends, serial dependence and covariate relationships. These teaching projects contain saved Bayesian fits and predictive results. They are distinct from an annual-maximum frequency analysis.

## Choose an example

| Tutorial | Saved contents | What you will learn |
|---|---|---|
| [Synthetic models](1-synthetic-data-examples/synthetic-time-series-examples.md) | Twelve 300-observation datasets and fits. | Compare trend, autoregressive, moving-average and transformed models; identify settings that differ from element names. |
| [Classic datasets](2-classic-time-series-examples/classic-time-series-examples.md) | Airline passengers, Nile flow and Mauna Loa CO2. | Read training/validation splits, seasonality, saved convergence limitations and the retained Nile date discrepancy. |
| [Time-series regression](3-time-series-regression-example/time-series-regression-example.md) | Five quarterly macroeconomic series, two regression fits and 30 future steps. | Distinguish simple/multiple regression, covariate assumptions and prediction uncertainty. Both saved fits have zero ARMA orders. |

## Follow a saved analysis

1. Open the project's linked file and save a working copy. Inspect the response's units, interval, dates and gaps under **Time Series Data**.
2. Select the named analysis under **Time Series Analysis**. Read its actual trend, transformation and ARIMA orders; an element name may be an older label.
3. Identify the training observations, withheld validation observations and future steps separately. The synthetic and classic projects have zero future steps; their held-out curves are validation predictions.
4. Inspect every parameter's trace, R-hat and effective sample size. The Airline example has weak saved diagnostics, which remain part of the lesson.
5. Inspect residuals, autocorrelation and Q–Q views in their stated residual scale. A model can track a trend while leaving unexplained serial structure.
6. For a future prediction with covariates, determine where future covariate values come from. Fixed means and bootstrap scenarios carry different assumptions, and separately bootstrapping covariates does not preserve their joint dependence.

The plotted bands are prediction intervals: they include process/residual variability and parameter uncertainty. They are not just uncertainty about a mean trend. The vertical training boundary and the colors distinguish the fitted period from withheld or future prediction periods.

Read the [author issue log](../../docs/example-issues-for-haden.md) before adopting an example configuration. Saved settings and results are preserved; these figures do not constitute a new recovery or forecasting validation experiment. See [figure reproduction](../README.md#reproducing-the-figures) or return to the [example index](../README.md).
