# Worked examples for frequency analysis and app plots

Read a relevant worked tutorial before configuring an unfamiliar analysis or
explaining its results. Use the examples to learn the level of evidence, data
checks and interpretation expected in a study. Their numerical settings, priors,
thresholds and engineering judgments are specific to their saved projects.

## Locate a compatible tutorial

In a BestFit checkout, start with `examples/README.md` and the matching local
tutorial. The links below address the public repository's `main` branch; local
changes may precede publication. Record the checkout commit or downloaded project
hash used. This portable skill includes this guide and the plotting utilities,
not the example databases, figures, BestFit binaries or the repository exporter.
If the repository is unavailable, report that limitation and continue with the
bundled evidence workflow; do not invent a tutorial's contents or results.

## Select the teaching case

| Task or question | Worked reference | What to learn before applying it |
|---|---|---|
| Obtain annual instantaneous flood peaks | [USGS peaks](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/2-input-data/2-usgs-peak-discharge/usgs-peak-download-example.md) | Separate downloaded annual peaks from maxima of daily mean discharge; check dates, units, missing periods and annual convention. |
| Enter systematic, interval and historical observations | [Historical inputs in Viglione](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/1-information-expansion/viglione-et-al-2013.md) | Read each observation type and completeness window; a missing year is not automatically a nonexceedance. |
| First stationary Bayesian fit | [ARR/FLIKE](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/2-arr-flike/arr-flike-examples.md) | Compare LP3/GEV settings, historical information and prior meaning, with saved diagnostics and selected point estimators. |
| Add historical/paleoflood and causal evidence | [Viglione](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/1-information-expansion/viglione-et-al-2013.md) | Distinguish time-window information, interval events and quantile priors; document source independence and prior units. |
| Explain Bayesian versus GMM results | [Bayesian Bulletin 17C datasets](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/3-bulletin17C-examples/bulletin-17c-bayesian-examples.md) and [GMM collection](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.md) | Bayesian credible limits and GMM confidence limits have different constructions. BestFit's GMM comparison is not an official EMA execution. |
| Combine information through GMM penalties | [Blakely one-day inflow](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/2-information-expansion/blakely-mountain-dam-b17c.md) | Separate parameter/quantile penalties from Bayesian priors. Check duration, log base, mean-square error, information overlap and retained optimizer warnings. |
| Model uncertain reconstructed observations | [Sinnemahoning Bayesian](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/4-measurement-errors/sinnemahoning-move3-bayesian.md) and [GMM](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/3-measurement-errors/sinnemahoning-move3-b17c.md) | Identify measured versus reconstructed years and the error-distribution scale. Marginal errors do not establish independence among reconstructed observations. |
| Consider trends and model averaging | [Brays Bayou](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/nsffa-brays-bayou-texas.md) and [OC Fisher](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/nsffa-oc-fisher-dam.md) | State evaluation time and covariates, inspect ESS, justify physical interpretation and avoid ranking different observation sets. OC Fisher's configured average has no saved result. |
| Interpret nonstationary chronology | [Synthetic trends](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/5-nonstationary-ffa/nsffa-synthetic-data.md) | The saved Alpha 0.5 chronology tracks the conditional median and uncertainty in that quantile; it is not a band containing 90% of annual observations. |
| Use peaks over threshold | [Point-process precipitation](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/3-point-process-analysis/point-process-examples.md) | Verify threshold, declustering, missing coverage, exposure and block-year starts. Event-count and annual-maximum models use different data. |
| Combine populations or mechanisms | [Mixtures](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/4-mixture-analysis/mixture-distribution-examples.md) and [snow/rain composites](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/5-composite-analysis/mixed-population-examples.md) | A weighted mixture and the maximum of competing processes answer different questions. Check weights, zero mass and which records each component uses. |
| Explain dependence and a joint response | [Copulas](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/5-bivariate-distribution-analysis/1-bivariate-distributions/bivariate-distribution-examples.md), [sum of Normals](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/5-bivariate-distribution-analysis/2-coincident-frequency/sum-two-normals.md), [Waimea](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/5-bivariate-distribution-analysis/2-coincident-frequency/waimea-river-stage-frequency.md) | Check pairing, marginal inputs, copula assumptions and response-surface units. Coincident bands can bound AEP horizontally at fixed response. Waimea's hydraulic provenance remains unresolved. |
| Plot rating curves | [Synthetic rating controls](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/6-rating-curve-analysis/synthetic-rating-curve-examples.md) and [Mississippi](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/6-rating-curve-analysis/usgs-07024175-mississippi-rating-curve.md) | Controls add; coefficients are stored in log10 space. Bands include residual variability. Check datum, measurement range and extrapolation. |
| Plot time-series or regression diagnostics | [Synthetic time series](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/7-time-series-analysis/1-synthetic-data-examples/synthetic-time-series-examples.md), [classic datasets](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/7-time-series-analysis/2-classic-time-series-examples/classic-time-series-examples.md), [regression](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/7-time-series-analysis/3-time-series-regression-example/time-series-regression-example.md) | Distinguish training, validation and future prediction; inspect diagnostics and covariate assumptions. Preserve recorded dates unless a separate correction is authorized. |

## Carry the evidence standard into the requested study

1. Record variable/duration, units, location, annual convention and the actual
   observation period. Preserve source records and qualifiers.
2. Explain each input type and completeness assumption before selecting a model.
   Keep regional applicability, reconstructed-error dependence and causal claims
   unresolved when their sources do not support them.
3. Read the saved configuration, not just the element name. Identify priors or
   penalties, parameter space, point estimator, interval width, seeds and run
   settings. Examples do not authorize changing a user's established choices.
4. Separate execution from acceptance. Report R-hat/ESS, optimizer warnings,
   missing results and conflicting legacy payloads. Do not manufacture a result,
   refit a saved project or erase an inconvenient observation for a figure.
5. Use [saved-plots.md](saved-plots.md) and [plot-contract.md](plot-contract.md) to
   render the same source arrays with the shared Python package. Inspect every
   PNG for clipped bounds, labels and legends covering data. Retain PNG, SVG,
   PlotSpec and source identity together.
6. Report the result and its limitations in language a junior engineer can
   follow: what changed between alternatives, why, which evidence supports it,
   and what still requires engineering judgment.

The [example issue log](https://github.com/USACE-RMC/RMC-BestFit/blob/main/docs/example-issues-for-haden.md)
records source-data discrepancies and unresolved study decisions. In particular,
the retained GHCN snowfall scale and Nile date offset must not be copied as
correct source handling. The [Blakely Bayesian reference](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/4-univariate-distribution-analysis/1-univariate-analysis/1-information-expansion/blakely-mountain-dam-bayesian.md)
is a study-reading guide pending the intended saved project, not a runnable
Bayesian case supplied by this package.
