# RMC-BestFit Example Projects

> [!NOTE]
> These example projects and tutorials are published for RMC-BestFit 2.0.0 and will continue to expand with additional screenshots, output tables, and validation notes.

The current tutorials accompany RMC-BestFit 2.0.1. Inspect the named saved project and its documented limitations before refreshing data or estimating a new result.

These tutorials teach how to inspect data, configure an analysis and interpret saved results in RMC-BestFit. Each project is a SQLite database with the `.bestfit` extension. Start with a working copy: refreshing a download or running an analysis can change the results you are comparing with the guide.

## Choose a starting point

| Chapter | What you will learn |
|---|---|
| [1. Time-series data](1-time-series-data/README.md) | Identify sources, units, sampling intervals and missing observations before analysis. |
| [2. Input data](2-input-data/README.md) | Distinguish annual instantaneous peaks, maxima of daily means and peaks over a threshold. |
| [4. Univariate analysis](4-univariate-distribution-analysis/README.md) | Explore Bayesian estimation, historical evidence, Bulletin 17C, trends, point processes and model combinations. |
| [5. Bivariate analysis](5-bivariate-distribution-analysis/README.md) | Interpret dependence, joint probabilities and coincident responses. |
| [6. Rating curves](6-rating-curve-analysis/README.md) | Relate stage and discharge and inspect predictive uncertainty. |
| [7. Time-series analysis](7-time-series-analysis/README.md) | Work with serial dependence, forecasts, covariates and residuals. |

Chapter 3 is reserved. Distribution-fitting comparisons appear within Chapter 4.

For a first flood-frequency study, read the USGS annual-peak tutorial, then a stationary Bayesian example, before adding historical data or more complex models. AEP means **annual exceedance probability**. An AEP of 0.01 means a 1% chance of exceedance in one year under the stated model; it does not schedule a flood once every 100 years.

## Follow a tutorial

1. Open the linked `.bestfit` project using **File > Open** and save a separate working copy.
2. Read the tutorial's source and saved-data tables. Confirm the variable, units, dates and observation type in the project.
3. Select the named input or analysis element in the Project Explorer. Compare its plots and settings with the guide before rerunning anything.
4. For Bayesian results, inspect chains, R-hat, effective sample size and uncertainty as well as the fitted curve. A completed run alone does not establish convergence or suitability.
5. Record any change you make to the data, assumptions or settings, and retain the original project for comparison.

The saved projects are teaching records, not accepted design studies. Known limitations are explained in the relevant tutorial. The [example limitations](../docs/example-limitations.md) collect unresolved source questions and interpretation boundaries, including the GHCN snowfall scale, Nile dates, study-specific prior provenance and saved diagnostics.

## Reading the figures

Figures under `screenshots/` are current native BestFit plot exports or captures of the current Project Explorer, Properties panel or DSS selector. They retain the app's presentation. Display-only corrections recorded in each capture make B17C sampling-density labels, fractional tick precision and short-record date ticks explicit; no plotted values change. The [screenshot manifest](screenshot-manifest.json) records the source project hash, exact selected element, plot ID and variant or UI panel, output hashes and compressed capture snapshot. It also records a disposition for every contributor PNG, including excluded images. Contributor screenshots are not presented as current saved-result evidence.

Additional Python figures retain broader scientific and diagnostic coverage. Their observations, curves, interval bounds and diagnostics come from BestFit.UI and BestFit.App; Matplotlib supplies the drawing through the shared plotting package. The [figure manifest](figure-manifest.json) identifies these views. Their SVG and compressed PlotSpec links accompany the figure for inspection and reuse. A Python companion linked beside a native PNG remains a separate rendering, not the source of that native image.

Frequency plots distinguish a Bayesian credible interval from a Bulletin 17C confidence interval. Plotting positions describe the sample; they are not fitted probabilities. Logarithmic plots cannot display zero or negative magnitudes, so also inspect the chronology and the original data. Each native capture snapshot or Python PlotSpec identifies its project hash and selected element. Inspect whether a probability band is vertical quantile uncertainty or horizontal probability uncertainty at a fixed CFA response. An app legend label alone does not establish the scientific interval type.

Two source-preserving display exceptions are documented: the restored Back Creek GMM figure overlays its original saved arrays because the app omits that legacy result format; older regression residual figures pass saved coefficients to BestFit's residual method to bypass the covariate-loading defect fixed in v2.0.1. These figure-generation exceptions do not change any database result. Rating/time-series prediction bands include residual/process variation as well as parameter uncertainty. Parameter-chain autocorrelation, residual autocorrelation and observed-series autocorrelation are distinct diagnostics.

## Reproducing the figures

From the repository root, on Windows with the .NET SDK required by this checkout and Python 3.10 or later:

```powershell
python -m venv .venv-examples
.venv-examples/Scripts/python -m pip install -r skills/bestfit-frequency/requirements.txt
dotnet build tools/PlotReferenceExporter -c Debug -p:UseLocalRmcNumerics=false
.venv-examples/Scripts/python tools/ExampleDocumentation/render_screenshots.py --refresh
.venv-examples/Scripts/python tools/ExampleDocumentation/render_examples.py --refresh
```

The screenshot and figure manifests select each project, element and view. Add `--only usgs-download-example` to either renderer to limit the selection. `render_screenshots.py` verifies saved source/output hashes by default and overwrites captures only with `--refresh`. The exporter opens a disposable copy, checks the original file hash and captures the desktop plot coordinates. It does not rerun the saved analysis. Threshold-stability views do invoke the app's diagnostic GPD fits; these are plot calculations on the saved input, not replacement analysis results. Python does no distribution estimation or uncertainty reconstruction.

Native PNGs and compressed capture snapshots live alongside each tutorial under `screenshots/`, with source and output hashes in `screenshot-manifest.json`. Integration checks are recorded under `artifacts/sadie-integration/`. Python render receipts and original desktop snapshots are written under `artifacts/example-documentation/`. Use `--refresh` when the desktop runtime changes. To inspect saved descriptions and configuration without opening the app, run `tools/ExampleDocumentation/project_inventory.py`. The [maintenance guide](../tools/ExampleDocumentation/README.md) explains the metadata preservation audit.

Sadie Niblett contributed the GUI walkthroughs and example views integrated here; her citation credit is retained in [CITATION.cff](../CITATION.cff). The [screenshot manifest](screenshot-manifest.json) records each contributor image's disposition and the native replacement's source, selected view and output hashes. The [data disposition](../docs/sadie-data-disposition.json) records retained target projects and omitted obsolete contributor data. Current native captures and older Python companions retain separate provenance; neither establishes convergence or engineering acceptance.

## Report an issue

Include the project filename, element name, software version, figure or tutorial section, and the smallest steps that reproduce the discrepancy. State whether you used the saved project or reran/edited a working copy.
