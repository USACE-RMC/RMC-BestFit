# Saved app plots

The canonical `bestfit_plots` Python package is bundled beside `SKILL.md` and used
by the BestFit Python notebooks. Its PlotSpec v1 contract separates numerical
source preparation from Matplotlib display. The renderer never estimates a model.

## Choose the source

| Supplied artifact | Action |
|---|---|
| PlotSpec JSON (`version: 1`, `plotId`) | Render directly. |
| Desktop geometry JSON (`formatVersion: 1`, `sourceSha256`) | Render directly. The bundled desktop adapter preserves exported coordinates, styles and interval orientation. |
| Notebook case JSON or JSON.gz (`schemaVersion: 1`, `plots`) | List its views, then select one. Retain its source and runtime hashes. |
| Completed API plot-source JSON | Normalize its same-run arrays, then render. Use `includeSamples=true` if trace/pair views are needed. |
| Only the older `results.json` and `input.json` | Use the legacy frequency command for supported univariate/B17C curves; obtain plot-source for other views. |
| `.bestfit` project | Use `tools/PlotReferenceExporter` in the BestFit repository on a disposable copy, then render its JSON with this skill. A compatible notebook loader is another option. The plotting CLI does not open arbitrary project databases. |

For an existing API analysis, request
`GET /api/analyses/{analysisId}/plot-source?includeSamples=true` or MCP
`get_analysis_plot_source`. Save the response unchanged as `plot-source.json`.
This read-only export does not run estimation. Busy, unsuccessful, stale dependency,
or missing results must be resolved explicitly, not silently refitted for a plot.

## List and display

Replace `SKILL_DIR` with the directory containing `SKILL.md`.

```sh
python SKILL_DIR/scripts/plot_source.py --source RUN/plot-source.json --list
python SKILL_DIR/scripts/plot_source.py --source RUN/plot-source.json --plot residual_qq --output RUN/residual-qq
python SKILL_DIR/scripts/plot_source.py --source RUN/plot-source.json --plot chronology --output RUN/chronology
python SKILL_DIR/scripts/plot_source.py --source RUN/plot-source.json --plot diagnostic_pair_heatmap --parameter 0 --second-parameter 1 --output RUN/parameter-pair
```

Use the exact name reported by `--list`. Unavailable diagnostic views include a
reason; an empty view does not establish coverage. The command writes PNG, SVG,
and the exact PlotSpec JSON. Inspect the PNG with the host's image tool and display
it to the user, with links to SVG/JSON and the source identity. Surface omission
messages and numerical diagnostic warnings. A successful export is not a
convergence assessment.

Known units can be supplied with `--unit-label`; absent API labels remain visibly
unavailable. Do not guess units from a station name. Date grids, historical bounds,
low-outlier flags, estimator labels and interval types belong to the source.
Parameters index the original saved order, starting at zero.
For API snapshots, `--include-warmup`, `--show-prior`, and `--influence-view`
select the corresponding saved diagnostic views. Run `--help` for the exact
names. Frequentist GMM views retain their separate fit/variance semantics;
unavailable LOO data is reported rather than estimated during display.

To add a frequency alternative from a second completed source:

```sh
python SKILL_DIR/scripts/plot_source.py --source RUN/base.json --plot frequency --compare-source RUN/alternative.json --compare-name "Alternative analysis" --output RUN/comparison
```

The axes and units must match. This uses the app's first comparison color and
retains both run identities. Python callers can use
`bestfit_plots.source.add_frequency_comparison(base, alternative, name)`.

## Frozen teaching cases

The repository's maintained [worked examples](examples.md) are the first reference
for a matching engineering workflow. Their Markdown embeds Python PNGs and links
SVG plus compressed PlotSpec. Follow the repository's
[build and reproduction prerequisites](https://github.com/USACE-RMC/RMC-BestFit/blob/main/examples/README.md#reproducing-the-figures),
then run `python tools/ExampleDocumentation/render_examples.py --only <tutorial-stem>`
from that checkout to reproduce one example using disposable source copies.
The exporter is a repository tool, not a bundled standalone-skill executable.

Use the compatible BestFit-Python-Examples checkout's README and its twelve
notebooks. `saved_case(project_slug, analysis_name)` uses checksum-checked saved
results by default; `case.show(view_name)` calls this same renderer. Full-settings
reruns are an explicit separate branch, `RUN_ANALYSES = True`.

For Nile, notebook 10 documents the saved project dates (1897–1996) and the source
CSV dates (1871–1970). Its explicit display correction preserves original source
bytes and values, records the transformation, and shifts all date plots together.
An arbitrary Nile plot-source artifact must retain its dates unless the supplied
source evidence supports that same correction.

## Coverage and limits

[app-plot-map.json](app-plot-map.json) maps 45 app slots to their factories,
population methods, adapters, variants, and evidence. Read each status before
claiming parity. The notebook repository contains the human-readable gallery and
comparisons against independently exported WPF geometry. Default data, scales,
curves, intervals and markers are the target; font rasterization, interaction,
and user-customized desktop styles are outside the contract.

Python records display corrections in the PlotSpec: time-series residuals use a
Date axis; fitting Q–Q labels identify observed X and model Y; GMM ensembles and
quantile penalties use frequentist labels. Rating/time-series result bands are
prediction intervals because they include residual/process variation. Monthly
sample percentiles are observed ranges, and month labels do not imply observations
occurred in the plotting anchor year. Contours include numeric levels. Trace
legends sit outside the axes; other legends can move after visual inspection.

The repository exporter also handles a documented regression-loading issue:
covariate hydration resets live parameter values. For affected residual views it
reads the saved coefficient vector without modifying it and calls BestFit's
existing residual calculation. This display correction is recorded in the source
snapshot and PlotSpec. The source database and saved fitted/prediction arrays are
unchanged. Do not silently treat an arbitrary API snapshot's residuals as corrected;
check the matching source version and saved-fit consistency.

The restored Back Creek teaching row is another disclosed legacy-loading case.
Its tutorial overlays the exact stored probability, point, expected-probability
and confidence-bound arrays on desktop-owned observation positions. The snapshot's
`legacySavedFrequency` data enables this only for an observation-only B17C view;
its source columns and correction are retained in PlotSpec. This does not migrate
the project or establish that the current app can display that legacy fit.

Raw-series/input-data preparation uses the portable BestFit/Numerics runtime when
building fixtures. Cached PlotSpec display and API-source display use Python only.
Do not substitute a SciPy fit, reconstruct a missing credible interval, shorten
sampling, or mix arrays from different runs to fill an unavailable view.
