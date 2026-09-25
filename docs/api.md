# RMC-BestFit REST API + MCP Server

## Agentic FFA data review and provenance

The portable [FFA skill](../skills/bestfit-frequency/SKILL.md) now covers collection,
justification, preparation and comparison of historical, regional and causal
information. Official [Bulletin 17C](https://pubs.usgs.gov/publication/tm4B5) is its
primary data-collection/entry resource for both Bayesian and B17C analyses.

| Contract | Purpose |
|---|---|
| `GET /api/inputdata/{id}/chronology` / MCP `get_inputdata_chronology` | Before fitting, returns `schemaVersion:1`, `inputData`, exact/uncertain/interval observations and inclusive threshold windows; bounds/counts come from the model |
| `GET /api/inputdata/{id}/source` / MCP `get_inputdata_source` | Returns detached original creation `request`, `capturedUtc`, optional USGS `rawText`, and SHA-256 of its UTF-8 text. Raw dates/qualifiers remain available for review |
| Univariate `useJeffreysRuleForScale` | Nullable request/MCP option exposing the existing switch; omission preserves its model default |
| Univariate/B17C resource `configuration` | Effective parent distribution, AEP ordinates, priors/penalties, exposed sampler settings and relevant switches; save before and after running |

These are additive contracts. Univariate requests now reject a quantile-prior count
the model cannot apply: one with `useSingleQuantile=true`, otherwise one per parent
distribution parameter. Priors, estimators, formulas and defaults are unchanged.

`thresholdData.numberAbove` means additional aggregate exceedances not already
entered as explicit exact/uncertain/interval observations. Responses contain
processed counts; the source endpoint preserves submitted counts. An explicit
1882 event inside 1870–1922 with aggregate above count zero leaves 52 censored years.
Do not copy processed responses back as original scientific evidence. Date-only
exact/uncertain observations use calendar year in the API; clients must supply
explicit indexes for a declared water-year convention. Raw USGS preservation does
not change the downloader's classification or interpret peak qualifiers.

See [study workflow](../skills/bestfit-frequency/references/study-workflow.md) and
[chronology contract](../skills/bestfit-frequency/references/plot-contract.md).
The source/chronology endpoints never run an estimator. Their resources share the
existing in-memory lifetime; save artifacts before stopping the host. Original
requests are retained for manual, USGS, block-maxima and POT inputs; raw downloads
are retained here for direct USGS peaks. Older programmatically built resources
without a stored request return only the available provenance.

## Overview

`src/RMC.BestFit.Api` hosts a headless REST API and an MCP (Model Context Protocol) server over
the RMC-BestFit model library (`RMC.BestFit.dll`), enabling programmatic and agentic AI
flood-frequency workflows: download USGS data, build input data, fit distributions with Bayesian
MCMC / Bulletin 17C / rating curves, and retrieve results as JSON.

## Running

```bash
dotnet run --project src/RMC.BestFit.Api          # http://localhost:5210 (Development)
```

- OpenAPI document (Development): `GET /openapi/v1.json`
- Health: `GET /health`, `GET /health/detailed`; service info: `GET /api/info`
- Configuration (`appsettings.json`, section `Api`): `MaxResources` (default 500),
  `MaxConcurrentRuns` (default 2), `MaxIterations` (default 500,000)

## Concepts

For a terminal-capable Claude or Codex workflow with matplotlib exports and chat
display, use the [portable frequency-curve skill](bestfit-frequency-skill.md).

- **Stateful resource store.** Creation endpoints store resources in memory keyed by GUID; later
  calls reference the ids. Resources are immutable after creation; analyses clone their inputs at
  creation, so deleting or replacing upstream resources never corrupts an existing fit. State is
  lost on restart (by design for the proof of concept).
- **Response contract.** Every response carries `success`, `errorMessage`, `validationErrors`,
  `validationWarnings`, `computationTimeMs`, `timestamp`, `nonFiniteFindings`. JSON is camelCase
  with enums as camelCase strings; NaN serializes as the named literal `"NaN"` (missing data);
  ±Infinity is rejected server-side before serialization.
- **Status codes.** 200/201 success; 400 validation or argument errors; 404 unknown id or no USGS
  data; 409 store at capacity or analysis already running; 499 client cancelled; 502/503 USGS
  upstream failures; 500 run failures. Exception: workflow endpoints report step failures in-body
  (`success=false`, `failedStep`) with HTTP 200, preserving the ids of already-created resources.
- **Probability convention.** All probability ordinates are annual exceedance probabilities
  (AEP), strictly between 0 and 1 (0.01 = 100-year event). Client-supplied ordinates are
  de-duplicated and sorted ascending automatically.
- **Distribution specs and engineering judgment.** Wherever a request supplies a distribution as
  an input — uncertain-observation measurement errors, `parameterPriors`, `quantilePriors` — it is
  a `{ type, parameters }` spec with parameters in the type's canonical order.
  `GET api/metadata/distributions` lists the parameter names/order per type (also the names that
  priors and Bulletin 17C penalties are matched against — the trailing short-form marker like
  "(γ)" may be omitted); `GET api/metadata/enums` lists the accepted spec types
  (`priorDistributions`). Supplying any parameter prior switches that analysis off its default
  flat priors; unnamed parameters keep flat priors. Bulletin 17C uses Gaussian *penalties*
  (`{ parameterName | aep, mean, mse, useLog/useLog10 }`) instead of priors, and reports a
  warning (never silently) when its input data contains uncertain observations, which its
  Expected Moments Algorithm ignores.
- **Runs are synchronous.** `POST .../run` returns when the estimation finishes (seconds to about
  a minute for MCMC). Client disconnect cancels the run; the analysis stays rerunnable.

## Endpoints

### Time series — `api/timeseries`

| Endpoint | Purpose |
|---|---|
| `POST api/timeseries/usgs` | Download from USGS water services: `dailyDischarge`, `dailyStage`, `instantaneousDischarge`, `instantaneousStage`, `peakDischarge`, `peakStage`, `measuredDischarge`, `measuredStage` |
| `POST api/timeseries/manual` | Build from explicit `{dateTime, value}` points (NaN = missing) |
| `GET api/timeseries` / `GET {id}?includePoints&offset&limit` / `DELETE {id}` | List / detail with paged points / delete |

### Input data — `api/inputdata`

| Endpoint | Purpose |
|---|---|
| `POST api/inputdata/manual` | Explicit observations: exact (systematic record), uncertain (per-observation measurement-error distributions, e.g. paleoflood estimates), interval-censored, perception thresholds; plotting parameter, low-outlier threshold, lambda |
| `POST api/inputdata/block-max` | Block maxima from a stored time series (`timeBlock=waterYear` default; smoothing for n-day flows) |
| `POST api/inputdata/peaks-over-threshold` | Independent POT events from a stored series (`threshold`, `minStepsBetweenPeaks`, optional `lambda` override) |
| `POST api/inputdata/usgs-peaks` | USGS annual peak-flow file directly (no intermediate time series) |
| `GET api/inputdata` / `GET {id}?includeData` / `GET {id}/summary-statistics` / `DELETE {id}` | List / detail with observations and plotting positions / sample statistics / delete |

### Analyses — `api/analyses/{univariate|bulletin17c|ratingcurve|mixture|pointprocess|competingrisks|composite|distributionfitting|bivariate|coincidentfrequency|timeseries}`

All kinds share the verb set: `POST` (create), `POST {id}/run`, `GET` (list), `GET {id}`,
`GET {id}/results`, `GET {id}/validate`, `DELETE {id}`. Lookups are kind-guarded (a univariate id
404s on the Bulletin 17C routes).

`GET api/analyses/{analysisId}/plot-source?includeSamples=false` exports one completed run of
any kind for external plotting. The matching MCP tool is `get_analysis_plot_source`. The version 1
response identifies the analysis and last run, and includes the existing kind-specific `results`
payload, portable `analysisXml` settings, available `modelXml`, frequency `dataFrameXml`, dated
rating/time-series observations and ARIMAX covariates in `series`, bivariate marginal observation
frames and model XML, and stored parameter histogram/density/autocorrelation arrays. Histogram bins
include exact lower and upper edges and raw frequencies, allowing clients to reproduce the app's
density normalization. Typed `observations` and point-process `amsObservations` avoid requiring a
.NET runtime to decode frequency input. Distribution fitting snapshots carry `fittingCurves` and
`fittingHistogram`; bivariate snapshots carry the seeded scatter and desktop contour grids in
`bivariatePlot`; rating and time-series snapshots carry `residualPlot`, with an exact dated result
grid in `resultDates` for time-series forecasts. Parameter diagnostics include their configured
prior density, and `influenceDiagnostics` keeps leverage, fit, variance, and LOO measures distinct.
Nonstationary univariate snapshots also include a detached `chronology`
grid with mean and interval arrays. Composite and seasonal point-process snapshots include
`componentCurves` at each component's configured probabilities. The XML is produced by the
existing model serializers, while `results` is a detached copy of the established results DTO.
`includeSamples=true` additionally returns saved parameter draws and per-chain samples when that
analysis has them. For Bulletin 17C, these draws are the GMM-derived uncertainty ensemble under
its configured method, not Bayesian MCMC. An analysis with no completed current run returns 404;
an analysis or live component running concurrently returns 409. Export does not run an analysis or
alter its settings. The response is a same-run snapshot; clients should not combine it with a
separate results request when they need strict run identity.
The API's input-data resources do not retain the desktop project's free-text unit label;
plotting clients should supply one explicitly when needed, or mark that label unavailable.

**Live component references (composite, bivariate, coincidentfrequency).** Analyses that consume
OTHER analyses hold LIVE references to them — the one deliberate exception to the clone-at-create
invariant, because components may not be estimated yet when the consumer is created. This applies
to a composite's components, a bivariate's two marginal analyses, and a coincident frequency
analysis's bivariate (plus, transitively, its marginals). Consequences: every referenced analysis
must be RUN before the consumer runs (validate reports which still require estimation); while the
consumer runs it holds each referenced analysis's run lock, so any of them mid-run makes the
consumer run report 409 (and vice versa); re-running a referenced analysis intentionally
refreshes the consumer's next run; deleting one from the store leaves the consumer functional
(the live reference keeps the fitted object alive) — the recorded ids are provenance only.

| Kind | Create request highlights | Results |
|---|---|---|
| `univariate` | `inputDataId`, `distribution` (15 supported; default `logPearsonTypeIII`), `probabilityOrdinates?`, `bayesianOptions?` (sampler, iterations, chains, seed, CI width, point estimator), `parameterPriors?`, `quantilePriors?` + `useSingleQuantile?` | Frequency curve (mode/mean/CI), posterior parameter summaries with R-hat/ESS, AIC/BIC/DIC/WAIC/LOOIC/RMSE, convergence warnings |
| `bulletin17c` | `inputDataId`, `distribution` (6 supported), `uncertaintyMethod?` (default model `linkedMultivariateNormal`), `probabilityOrdinates?`, `parameterPenalties?` (regional skew), `quantilePenalties?` | Frequency curve with confidence intervals, sampled parameter summaries, AIC/BIC/DIC/RMSE/ERL, GMM/uncertainty timing |
| `ratingcurve` | `stageTimeSeriesId`, `dischargeTimeSeriesId` (date-aligned; ≥10 common dates), `numberOfSegments` 1-3, optional stage grid (`minStage`/`maxStage`/`stageBins`), `bayesianOptions?`, `parameterPriors?` | Stage-discharge curve over the stage grid (mode/mean/CI), fitted parameters, posterior summaries |
| `mixture` | `inputDataId`, `distributions` (1-3 components, all 15 supported), `isZeroInflated?`, `probabilityOrdinates?`, `bayesianOptions?`, `parameterPriors?`, `quantilePriors?` | Frequency curve (mode/mean/CI), posterior summaries for component parameters and weights, AIC/BIC/DIC/WAIC/LOOIC/RMSE |
| `pointprocess` | `inputDataId` (POT resource; its threshold seeds the model), `isSeasonal?` + `timeBlock?`/`startMonth?`, `threshold?`/`totalYears?` overrides, `probabilityOrdinates?`, `bayesianOptions?`, `parameterPriors?`, `quantilePriors?` — GEV-only by construction (no distribution field) | Annual-exceedance frequency curve (mode/mean/CI), posterior summaries, AIC/BIC/DIC/WAIC/LOOIC/RMSE |
| `competingrisks` | `inputDataId`, `distributions` (1-3 components, one per competing process), `probabilityOrdinates?`, `bayesianOptions?`, `parameterPriors?`, `quantilePriors?` | Frequency curve (mode/mean/CI), posterior summaries, AIC/BIC/DIC/WAIC/LOOIC/RMSE |
| `composite` | `components` (`[{analysisId, weight?}]`, live references; kinds univariate/bulletin17c/mixture/pointprocess/competingrisks), `compositeType` (`competingRisks` default \| `mixture` \| `modelAverage`), `averageMethod?` (DIC/WAIC/LOOIC need MCMC components), `dependency?`, `isMaximum?`, `probabilityOrdinates?`, `credibleIntervalWidth?`, `pointEstimator?` — no MCMC of its own | Frequency curve (mode/mean/CI), `composite` block with per-component weights (model-average weights are a first-class output), AIC/BIC/DIC/RMSE |
| `distributionfitting` | `inputDataId`, `distributions?` (candidate subset; default all 15) — parallel MLE screen, seconds, no MCMC | Ranked `fits` (AIC ascending, failures last): parameters, AIC/BIC/RMSE, `fitSucceeded`, `errorMessage` |
| `bivariate` | `marginalXAnalysisId`, `marginalYAnalysisId` (live references; kinds univariate/bulletin17c/mixture/pointprocess; data pair by shared time index, ≥10 overlapping non-outlier exacts), `copulaType` (7 families; default `normal`), `estimationMethod?` (`inferenceFromMargins` default \| `pseudoLikelihood`), `xyOrdinates` (`[{x, y}]` joint-exceedance grid), `bayesianOptions?`, `parameterPriors?` (copula params) | Joint exceedance P(X&gt;x AND Y&gt;y) per grid point (mode/mean/CI), copula posterior summaries with R-hat/ESS, AIC/BIC/DIC/WAIC/LOOIC/RMSE |
| `coincidentfrequency` | `bivariateAnalysisId` (live reference; must be run first), `xValues`/`yValues` (strictly ascending), `bivariateResponse` (2-D surface Z[x][y], strictly increasing both axes), `numberOfBins` 5-1000 (default 50), `credibleIntervalWidth?`, `pointEstimator?` — no MCMC of its own; univariate-marginal posterior chains are pulled in automatically when available (`marginalX/YChainUsed` flags) | Response frequency curve: AEP per response-magnitude bin (mode/mean/CI) over `zValues` |
| `timeseries` | `timeSeriesId`, `modelType` (`ar` \| `ma` \| `arima` \| `arimax`), family-specific orders (`order` for ar/ma; `pOrder`/`dOrder`/`qOrder` for arima/arimax; `xOrder` + `covariateTimeSeriesIds` + `trendType` + `includeSeasonality` + `covariateExtension` for arimax — fields for other families are 400s, never silently ignored), `includeIntercept?`, `transformType?` (lambdas auto-fitted), `trainingTimeSteps?`, `forecastingTimeSteps?` 0-100, `bayesianOptions?`, `parameterPriors?` | Fitted-plus-forecast curve aligned by time index (mode/mean/CI), parameter posteriors with R-hat/ESS, AIC/BIC/DIC/WAIC/LOOIC/RMSE |

### Workflows (one-shot) — `api/workflows`

| Endpoint | Steps |
|---|---|
| `POST api/workflows/usgs-peak-frequency` | USGS peaks → input data → univariate MCMC → results |
| `POST api/workflows/usgs-daily-block-max-frequency` | USGS daily → time series → block maxima → univariate MCMC → results |
| `POST api/workflows/usgs-bulletin17c` | USGS peaks → input data → Bulletin 17C → results |
| `POST api/workflows/usgs-rating-curve` | USGS measured stage + discharge → rating curve MCMC → results |

Responses include every created resource id (`timeSeriesId`, `inputDataId`, `analysisId`, ...)
plus the full results. On a step failure: `success=false`, `failedStep`, and the ids of completed
steps so the workflow can be resumed manually through the granular endpoints.

### Discovery — `api/metadata`, `api/resources`

`GET api/metadata/distributions` (which distributions each analysis kind supports),
`GET api/metadata/enums` (every accepted enum string), `GET api/metadata/defaults` (default AEP
ordinates and server limits), `GET api/resources` (cross-cutting id overview).

## MCP server

### Optional Multiple Grubbs-Beck screening

Manual input creation, USGS-peak input creation, and the USGS B17C workflow accept
`useMultipleGrubbsBeckTest` (default **false**, preserving existing requests).
For example, POST `/api/workflows/usgs-bulletin17c`:

```json
{"siteNumber":"01646500","useMultipleGrubbsBeckTest":true}
```

Or POST `/api/inputdata/manual` with all supplied observations and the same flag,
then create a B17C analysis linked to its returned id. Screening delegates to
`DataFrame.SetLowOutliersFromMGBT()` after all series are populated and before an
analysis clones the data. It requires at least ten exact observations. It flags
low outliers, sets the threshold, and refreshes plotting positions; it does not
delete the observations. The model's existing strict threshold comparison is
preserved. When true, a manual `lowOutlierThreshold` (including zero) or any
`isLowOutlier:true` observation is rejected to avoid overwriting the caller's
screening choice. Omitted/false retains existing manual behavior.
For manual screening, provide the intended `isLowOutlier` flags explicitly: the
current API stores `lowOutlierThreshold` but does not derive flags from it.

The MCP tools `create_inputdata_manual`, `create_inputdata_usgs_peaks`, and
`run_usgs_bulletin17c_workflow` expose the same optional boolean. For example:
`run_usgs_bulletin17c_workflow(siteNumber="01646500", useMultipleGrubbsBeckTest=true)`.
Input summaries return `lowOutlierThreshold` and `lowOutlierCount`; GET input data
with `includeData=true` to save flags and the model's computed plotting positions.
MGBT failures store no new input resource; workflow failures identify
`failedStep:"createInputData"` before analysis creation.

### Display coordinates

Uncertain observations now include response-only `lowerBound`/`upperBound` from
the model's display properties. Frequency results add `quantileAnnotations`, an
array of `{aep,value,lowerBound,upperBound}` for enabled univariate priors or B17C
quantile penalties, in physical units. These additions preserve existing fields.
Plotters should use these values directly, including input `plottingPosition`,
instead of recomputing them from ranks or distribution parameters. The default
matplotlib renderer is `skills/bestfit-frequency/scripts/plot_frequency.py`.

### Transport

The same host serves MCP over the streamable HTTP transport at **`/mcp`** (stateless mode — all
state lives in the app-singleton resource store, so ids remain valid across MCP sessions and the
REST/MCP boundary). Tools call the same service layer as the controllers and return the same JSON
DTOs.

Connect from an MCP client:

```bash
claude mcp add --transport http bestfit http://localhost:5210/mcp
```

Tools (27): `get_metadata`, `list_resources`, `delete_resource`, `usgs_download_timeseries`,
`create_manual_timeseries`, `get_timeseries`, `create_inputdata_usgs_peaks`,
`create_inputdata_block_max`, `create_inputdata_pot`, `create_inputdata_manual`, `get_inputdata`,
`create_univariate_analysis`, `create_bulletin17c_analysis`, `create_ratingcurve_analysis`,
`create_mixture_analysis`, `create_pointprocess_analysis`, `create_competingrisks_analysis`,
`create_composite_analysis`, `create_distributionfitting_analysis`,
`create_bivariate_analysis`, `create_coincidentfrequency_analysis`, `create_timeseries_analysis`,
`run_analysis`, `get_analysis_results`, `get_analysis_plot_source`, `validate_analysis`, plus the four
`run_usgs_*_workflow` one-shots.

Typical agent chains:

```
get_metadata → create_inputdata_usgs_peaks(site) → create_bulletin17c_analysis(inputDataId) → run_analysis(analysisId)
usgs_download_timeseries(site, dailyDischarge) → create_inputdata_block_max(timeSeriesId) → create_univariate_analysis(...) → run_analysis(...)
usgs_download_timeseries(site, measuredStage) + (site, measuredDischarge) → create_ratingcurve_analysis(...) → run_analysis(...)
run_usgs_bulletin17c_workflow(site)            # everything in one call
```

## Endpoint map status

Every analysis endpoint from the original Phase-4 scope is implemented: mixture, point process,
competing risks, composite, bivariate, coincident frequency, time series, and distribution
fitting, plus the Bayesian input features (uncertain data, parameter priors, quantile priors,
Bulletin 17C penalties).

Deferred features: nonstationary trend models + chronology results, an async job pattern
(jobId + polling) if synchronous runs outgrow client timeouts, authentication.
