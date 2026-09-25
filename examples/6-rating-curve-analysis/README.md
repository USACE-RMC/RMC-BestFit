# Rating curve analysis

A rating curve relates measured stage to discharge at a location. These projects use Bayesian estimation of an additive hydraulic-control model. For each active control, discharge contributes alpha times (stage minus activation stage) raised to beta; the active contributions add. A new control does not replace the earlier controls.

## Choose an example

| Tutorial | Data | What to inspect |
|---|---|---|
| [Synthetic rating curves](synthetic-rating-curve-examples.md) | One stage series and three distinct generated discharge series, 300 observations each. | One, two and three additive controls, stored log10 coefficients, parameter uncertainty and weak third-control precision. |
| [Mississippi River at New Madrid](usgs-07024175-mississippi-rating-curve.md) | 96 paired stage/discharge measurements at USGS 07024175. | A saved single-control fit, extrapolation beyond measurements and predictive uncertainty. |

The field example is a teaching fit, not an official USGS rating or an adopted hydraulic relationship. Confirm stage datum, measurement quality, channel changes and applicability before using it in another study.

## Read the model and results

1. Open the linked `.bestfit` project and save a separate working copy.
2. Under **Time Series Data**, check stage/discharge units and matching timestamps. Generic series labels do not supersede the documented units.
3. Under **Rating Curve Analysis**, select the named alternative. Read each activation stage, stored log10(alpha), exponent and error scale. Sigma describes residual variability in log10 discharge space.
4. Inspect parameter chains and uncertainty, then the rating curve and residual diagnostics. Curves that look similar can arise from poorly identified parameter combinations.
5. Compare the prediction grid with the observed stage range. Values beyond that range are extrapolations. Prediction bands include residual variation as well as parameter uncertainty.

The plots follow the desktop orientation: discharge is horizontal and stage is vertical. The Python legend identifies prediction intervals explicitly; saved result cells retain their original field names. No estimator was rerun to produce these figures.

See the [author issue log](../../docs/example-issues-for-haden.md), [figure reproduction instructions](../README.md#reproducing-the-figures) and [input-data chapter](../2-input-data/README.md). Converting a continuous stage record into discharge requires a separately justified applicable rating; these examples do not perform that conversion automatically.
