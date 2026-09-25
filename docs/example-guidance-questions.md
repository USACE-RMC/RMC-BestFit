# Example guidance requested

These questions concern study evidence and teaching intent that are not established by the saved project alone. Independent documentation and plotting work continues while decisions are pending.

| Example | Guidance needed | Current handling |
|---|---|---|
| Blakely Mountain Dam, Bayesian | Full path to the existing project selected during planning; rationale and source for adopted information terms. | Project path requested; no replacement analysis invented. |
| GHCN download, Paradise snowfall | Correct the 38 nonzero saved values by a factor of ten, or retain the original as a data-quality exercise? | Explicit decision requested after comparison with NOAA's source records. Original inputs remain unchanged. |
| Sinnemahoning MOVE.3, Bayesian and B17C | Report/calculation underlying the 1914–1938 extension, measurement-error distributions and donor-gage dependence. | Provenance requested; preserve all saved distributions and distinguish exact from uncertain extension. |
| Waimea River stage frequency | Meaning of “RR Prior,” response-surface units and hydraulic model/report. | Provenance requested; do not label the response as discharge or stage solely from the filename. |
| Blakely Mountain Dam, B17C | Engineering basis for the stored parameter and quantile penalties, especially the distinction between saved one-day inflows and the published three-day case. | Inspect the supplied source material before requesting a remaining decision. |
| Brays Bayou and OC Fisher | Intended physical interpretation of trend alternatives; evidence supporting covariates or a periodic explanation. | Describe stored mathematical alternatives without attributing a cause. |
| Classic time series and manual entry, Nile | Any desired future correction of the saved 1897–1996 dates to the source CSV's 1871–1970 dates. | Approved plan preserves and discloses this discrepancy. |
| Classic time series, Airline | Whether a later study should revise the model after reviewing its weak saved diagnostics. | Preserve the fitted example and explain diagnostic limitations; no sampler/model tuning. |

The GHCN finding is based on the [NOAA daily format specification](https://www.ncei.noaa.gov/pub/data/ghcn/daily/readme.txt) and [station file](https://www.ncei.noaa.gov/pub/data/ghcn/daily/all/USC00046685.dly), retrieved on 2026-09-25. NOAA defines PRCP in tenths of millimetres and SNOW in millimetres. All 38 matched nonzero saved snowfall values equal the source SNOW integer divided by 254; inches require division by 25.4. The numerical/source-input preservation rule in the approved plan requires a decision before correcting those stored values.
