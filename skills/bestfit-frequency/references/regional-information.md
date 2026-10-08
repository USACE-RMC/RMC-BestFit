# Regional information at a requested location

## Resolve the study location first

Establish outlet coordinates, named stream, USGS site number if present, watershed
boundary/drainage area, state(s), hydrologic setting and flow statistic. A town
name is not a delineated basin. Confirm ambiguous outlets before selecting a
regional study. For regulated/urban/diverted basins, document why the selected
equation applies or retain a lookup-only result. Do not invent an at-site series
for an ungaged site to satisfy the API's exact-data requirement.

## Primary source sequence

1. Read the [USGS flood-frequency report index](https://www.usgs.gov/streamstats/science/flood-frequency-reports)
   and search the publication catalog for newer revisions/data releases. Select
   the applicable study region, not just a state name; regional-skew and peak-flow
   regression boundaries may differ. Save the map/table/equation and version.
2. For skew, prefer the applicable modern regional study and its prediction MSE.
   Distinguish regional, station and weighted skew. Do not reuse an at-site
   weighted estimate as an independent prior on those same observations.
   If no applicable regional study/MSE is found, report that fact and run the
   baseline; do not silently assume zero skew/uncertainty or use an old map.
3. For quantiles use [StreamStats/NSS](https://www.usgs.gov/streamstats/science/national-streamflow-statistics-nss)
   and the governing report. NSS regional regressions predict selected AEP flows
   from basin characteristics; they are not a universal formula for LP3 parameters.
   Check instantaneous peak versus N-day flow, units, predictor ranges, rural/urban
   applicability and regulation restrictions before requesting a calculation.
4. Read current [USGS web-service documentation](https://www.usgs.gov/streamstats/web-services).
   Follow its linked StreamStats and NSS schemas to discover region/scenario IDs,
   basin-characteristic definitions, units, computation URLs and request shapes.
   Never guess IDs or carry a scenario from another location. Delineation, basin
   characteristics and regional prediction are separate evidence steps.

## Repeatable service workflow

Generate searches: `python scripts/capture_source.py --study study.json --output research`.
Use the host's browser/search capability for these queries; this command plans
research and does not itself locate a basin or choose a report.

Capture each discovered metadata GET or documented **read-only calculation** POST:

```sh
python scripts/capture_source.py --url DISCOVERED_HTTPS_URL --output evidence/nss-metadata
python scripts/capture_source.py --url DISCOVERED_CALCULATION_URL --body nss-request.json --output evidence/nss-result
```

Save basin geometry/outlet, characteristic values and units, source method,
regression-region overlaps, service metadata, calculation request, raw response,
uncertainty outputs and citations. `response.bin` retains original bytes;
`receipt.json` supplies SHA-256 and retrieval time. Interpret HTTP and application
errors before using values. Successful HTTP alone does not establish scientific
validity. On service failure retain the receipt and mark unresolved; use report
equations only when all published inputs, applicable ranges and uncertainty
calculations are known. No guessed predictors, extrapolation or invented MSE.

The helper transports metadata-discovered requests rather than hard-coding changing
state/scenario IDs. An agent must read the current schema and report and document
the scientific interpretation before populating a study.

## Preserve uncertainty meaning and dependencies

Keep original AEPs (0.01 = 1%, 100-year), predicted flows and units. Label whether
uncertainty is model error, prediction error, standard error of estimate, standard
error of prediction, variance/MSE, percent error or prediction interval. These are
not interchangeable. Never treat a reported 90% prediction interval as a standard
deviation or use its width as MSE. Any conversion needs the source's distributional
assumptions, confidence level, log base and equations.

LP3 quantile information can inform an LP3 fit when applicable, but do not fit LP3
to regional quantile points and pretend the fitted parameters are measured
independent observations. Default to one supported quantile per sensitivity
candidate. Quantiles from one regression system, regional skew and station-weighted
estimates can share data/errors. BestFit's exposed quantile-prior inputs do not
carry a covariance matrix. Require a dependence assessment before combining
information; unresolved dependencies call for separate candidates.

For every adopted value record: location/region match, revision, equation/table,
predictors and limits, AEP/statistic, estimate, uncertainty units/definition, source
dependencies, transformation and rationale. See [information-recipes.md](information-recipes.md).
