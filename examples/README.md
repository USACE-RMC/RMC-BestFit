# RMC-BestFit Example Projects

> [!NOTE]
> These example projects and tutorials are published for RMC-BestFit 2.0.0 and will continue to expand with additional screenshots, output tables, and validation notes.

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

The saved projects are teaching records, not accepted design studies. Known limitations are explained in the relevant tutorial. In particular, the GHCN snowfall scale, Nile dates, some study-specific prior provenance and several saved diagnostics require attention. [The guidance register](../docs/example-guidance-questions.md) tracks the remaining author decisions.

## Reading the figures

Figures are Python plots generated from BestFit desktop geometry. Their observations, curves, interval bounds and diagnostics come from BestFit.UI and BestFit.App. Matplotlib supplies the drawing, using the shared plotting package shipped with the frequency-analysis skill. PNG is embedded in each guide; SVG and compressed PlotSpec files accompany the figure for inspection and reuse.

Frequency plots distinguish a Bayesian credible interval from a Bulletin 17C confidence interval. Plotting positions describe the sample; they are not fitted probabilities. Logarithmic plots cannot display zero or negative magnitudes, so also inspect the chronology and the original data. Each figure's PlotSpec identifies its project hash and selected element.

## Reproducing the figures

From the repository root, on Windows with the .NET SDK required by this checkout and Python 3.10 or later:

```powershell
python -m venv .venv-examples
.venv-examples/Scripts/python -m pip install -r skills/bestfit-frequency/requirements.txt
dotnet build tools/PlotReferenceExporter -c Debug -p:UseLocalRmcNumerics=false
.venv-examples/Scripts/python tools/ExampleDocumentation/render_examples.py --refresh
```

The [figure manifest](figure-manifest.json) selects each project, element and view. Add `--only usgs-download-example` to render one example. The exporter opens a disposable copy, checks the original file hash and captures the desktop plot coordinates. It does not rerun the saved analysis. Threshold-stability views do invoke the app's diagnostic GPD fits; these are plot calculations on the saved input, not replacement analysis results. Python does no distribution estimation or uncertainty reconstruction.

Render receipts and original desktop snapshots are written under `artifacts/example-documentation/`. Use `--refresh` when the desktop runtime changes. To inspect saved descriptions and configuration without opening the app, run `tools/ExampleDocumentation/project_inventory.py`. The [maintenance guide](../tools/ExampleDocumentation/README.md) explains the metadata preservation audit.

## Report an issue

Include the project filename, element name, software version, figure or tutorial section, and the smallest steps that reproduce the discrepancy. State whether you used the saved project or reran/edited a working copy.
