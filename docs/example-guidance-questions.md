# Example guidance requested

These questions concern study evidence and teaching intent that are not established by the saved project alone. Documentation and figures for the available projects are complete; the items below remain for Haden's follow-up without changing the retained results.

| Example | Guidance needed | Current handling |
|---|---|---|
| Blakely Mountain Dam, Bayesian | Full path to the existing project selected during planning; rationale and source for adopted information terms. | Project path requested; no replacement analysis invented. |
| GHCN download, Paradise snowfall | Correct the 38 nonzero saved values by a factor of ten, or retain the original as a data-quality exercise? | Explicit decision requested after comparison with NOAA's source records. Original inputs remain unchanged. |
| Sinnemahoning MOVE.3, Bayesian and B17C | Report/calculation underlying the 1914–1938 extension, measurement-error distributions and donor-gage dependence. | Provenance requested; preserve all saved distributions and distinguish exact from uncertain extension. |
| Waimea River stage frequency | Meaning of “RR Prior,” response-surface units and hydraulic model/report. | Provenance requested; do not label the response as discharge or stage solely from the filename. |
| Blakely Mountain Dam, B17C | Engineering basis for the stored parameter and quantile penalties, especially the distinction between saved one-day inflows and the published three-day case. | Supplied report and workbook reviewed; duration-specific transfer of each adopted information term still requires the author's rationale. |
| Brays Bayou and OC Fisher | Intended physical interpretation of trend alternatives; evidence supporting covariates or a periodic explanation. | Describe stored mathematical alternatives without attributing a cause. |
| Classic time series and manual entry, Nile | Any desired future correction of the saved 1897–1996 dates to the source CSV's 1871–1970 dates. | Approved plan preserves and discloses this discrepancy. |
| Classic time series, Airline | Whether a later study should revise the model after reviewing its weak saved diagnostics. | Preserve the fitted example and explain diagnostic limitations; no sampler/model tuning. |
| Point-process precipitation | Rationale for thresholds and event separation, missing/partial-year exposure, and different AMS/point-process block-year starts. | Preserve settings and explicitly distinguish inferred exposure from verified complete observation. |
| Mississippi rating curve | Stage datum, measurement qualifiers, hydraulic stability and extrapolation basis. | Saved single-control teaching fit; not an official USGS rating. |
| Synthetic rating curves | Intended emphasis on weak third-control parameter precision. | Cite archived evidence and distinguish separate generated discharge datasets. |
| Synthetic mixtures and composites | Generating recipe/seed, fixed weights, separate realizations and pooled-sample units. | Preserve saved values and explain that cross-dataset curve comparisons are not a model-selection or recovery experiment. |
| Synthetic time series | Whether to rename historical labels such as ARMA(2,2), whose saved orders are p=2, q=1. | Document actual settings; do not rename or rerun without a separate decision. |
| Time-series regression | Original source, variable transformations/units and intended future-covariate scenario. | State the unresolved units and separate-bootstrap dependence limitation. The app hydration defect is recorded in the issue log. |

Back Creek's tree restoration was explicitly approved and completed. Its older result format still requires a separate app-loading repair; the tutorial distinguishes original saved-array figures from current app display. See the [consolidated issue log](example-issues-for-haden.md).

The GHCN finding is based on the [NOAA daily format specification](https://www.ncei.noaa.gov/pub/data/ghcn/daily/readme.txt) and [station file](https://www.ncei.noaa.gov/pub/data/ghcn/daily/all/USC00046685.dly), retrieved on 2026-09-25. NOAA defines PRCP in tenths of millimetres and SNOW in millimetres. All 38 matched nonzero saved snowfall values equal the source SNOW integer divided by 254; inches require division by 25.4. The numerical/source-input preservation rule in the approved plan requires a decision before correcting those stored values.
