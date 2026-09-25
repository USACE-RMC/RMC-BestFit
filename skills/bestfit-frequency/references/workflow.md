# Requests and saved artifacts

The examples assume the API setup in `setup.md` and commands executed from the
installed skill directory. Use absolute script paths when working elsewhere.
`run_frequency.py` speaks ordinary local HTTP; MCP is optional.

Read the matching [worked example](examples.md) before configuring an unfamiliar
input or estimator. Its tutorial establishes the expected observation audit,
diagnostics and interpretation; its saved numerical choices are not defaults for
a new watershed.

For supplied annual peaks, write a UTF-8 input request JSON with explicit annual
indexes under the declared convention. Date-only API inputs use calendar years.
Preserve all supplied values, including zeros; confirm the data's meaning
and units with the user. For a purely illustrative test, the bundle includes
`assets/synthetic-annual-flows.json`, explicitly labeled synthetic.

```sh
python scripts/run_frequency.py --kind bulletin17c --manual assets/synthetic-annual-flows.json --prepare-only --output PREVIEW
# Inspect and show PREVIEW/chronology.png before the fitting command below.
python scripts/run_frequency.py --kind bulletin17c --manual assets/synthetic-annual-flows.json --output RUN
python scripts/plot_frequency.py --results RUN/results.json --input RUN/input.json --output RUN/frequency --title "Synthetic annual flows - Bulletin 17C" --ylabel "Flow (illustrative units)"
```

Choose a **new** `RUN` directory. The first command enables MGBT for B17C when no
explicit screening choice exists. `--mgbt off` explicitly disables automatic
screening; `--mgbt on` explicitly enables it for either method. Auto mode preserves
an explicit JSON `useMultipleGrubbsBeckTest:false`, `lowOutlierThreshold`, or
`isLowOutlier:true`. The API rejects automatic screening combined with manual
thresholds/flags and rejects fewer than ten exact observations. Screening occurs
after all input series are populated and before analysis creation/cloning.

For a USGS site supplied by the user:

```sh
python scripts/run_frequency.py --kind bulletin17c --usgs SITE_NUMBER --prepare-only --output USGS_REVIEW
# Inspect source.json (dates and qualifiers) and chronology.png before fitting.
python scripts/run_frequency.py --kind bulletin17c --usgs SITE_NUMBER --output RUN
```

The USGS shortcut supports automatic MGBT or `--mgbt off`; it does not accept
manual screening fields. For a manual threshold/flags on USGS peaks, first create
USGS input without MGBT and fetch `includeData=true` through the granular endpoints.
Copy the year/value observations into a manual request with the user's explicit
threshold and an `isLowOutlier` flag on every exact observation, then use `--manual`.
The current API stores a manual threshold but **does not derive flags from it**.
If the user supplies only a threshold, explain that limitation and obtain the
intended flags before running; do not claim the threshold screened the data or
invent a threshold comparison rule. Preserve all observations and do not run
automatic screening on that request.

For Bayesian univariate analysis, use `--kind univariate` (also the client default).
This leaves the API's screening default unchanged. To reuse the illustrative
zero-containing input above with explicit screening, use `--mgbt on`. Do not
silently remove zero flows or invent a screen for a user's Bayesian request.

Use `--analysis-options options.json` to pass supported creation options verbatim,
omitting `inputDataId` (linked automatically). For example:

```json
{"distribution":"logPearsonTypeIII","probabilityOrdinates":[0.5,0.1,0.02,0.01,0.002]}
```

Omitting options preserves the 25 model-default ordinates and the default
distribution/uncertainty/simulation settings. BestFit B17C uses GMM with default
`linkedMultivariateNormal` uncertainty. Bayesian univariate analysis uses the
existing automatic MCMC configuration. For advanced priors, historical/interval
records, measurement error, or uncertainty choices, first read
[historical-data.md](historical-data.md), [information-recipes.md](information-recipes.md)
and [study-workflow.md](study-workflow.md), then consult the checkout's
`docs/api.md`, request DTOs, and `/api/metadata/distributions`, `/api/metadata/enums`,
`/api/metadata/defaults` rather than guessing parameter names or orders.

## Equivalent REST sequence

| Step | Endpoint and important fields |
|---|---|
| Create input | POST `/api/inputdata/manual` with `exactData:[{index,value}]`, optional `uncertainData`, `intervalData`, `thresholdData`, and `useMultipleGrubbsBeckTest`; or POST `/api/inputdata/usgs-peaks` with `siteNumber` and screening option |
| Save observations | GET `/api/inputdata/{inputData.id}?includeData=true` |
| Retain evidence | GET `/api/inputdata/{inputData.id}/source` |
| Inspect chronology before fitting | GET `/api/inputdata/{inputData.id}/chronology`; render PNG/SVG using `plot_chronology.py` |
| Create analysis | POST `/api/analyses/bulletin17c` or `/api/analyses/univariate` with `inputDataId` and explicit options |
| Validate | GET `/api/analyses/{kind}/{analysis.id}/validate`; require `isValid:true` |
| Run and save | POST `/api/analyses/{kind}/{analysis.id}/run` with `{}`; require `success:true` |
| Save final state | GET `/api/analyses/{kind}/{analysis.id}` |

The one-shot POST `/api/workflows/usgs-bulletin17c` accepts
`{"siteNumber":"SITE_NUMBER","useMultipleGrubbsBeckTest":true}`. It returns a
workflow envelope with `inputDataId`, `analysisId`, and `results`; check outer
`success`/`failedStep` as well as nested success, even with HTTP 200. Fetch the
input separately with `includeData=true`. The renderer also accepts this envelope.
This one-shot endpoint cannot pause for input inspection. Use the granular preview
sequence for newly collected data; only use the shortcut for an already reviewed
input source, and compare the new download with the reviewed source before adoption.

The optional MCP endpoint is `/mcp` on the same host. Equivalent tools:
`create_inputdata_manual`, `create_inputdata_usgs_peaks`,
`run_usgs_bulletin17c_workflow`, each with `useMultipleGrubbsBeckTest:true` when
appropriate; `get_inputdata(id,includeData:true)` supplies plotting coordinates.
`get_inputdata_source` and `get_inputdata_chronology` provide the same pre-fit
evidence as REST. Prefer this granular sequence over one-shot workflows when
collecting/reviewing historical information. Use `--prepare-only` to stop before
analysis creation; the client still saves sources and renders chronology.
REST and MCP share the same services/store. A locally running server is enough
for a client capable of connecting to it; this skill does not register an MCP
server in either application's configuration.

## Output contract

Keep `input-request.json`, `analysis-request.json`, `input.json`, `results.json`,
`analysis.json`, `validation.json`, API defaults/info and version/build provenance
along with `source.json`, `chronology.json` and chronology PNG/SVG
in the same run folder. Keep diagnostic warnings with the figure. A timeout is a
client wait limit, not permission to reduce iteration counts. Do not automatically
retry a failed fitting workflow or duplicate a run without inspecting its status.

Display the PNG through the current host's image mechanism. In a host that renders
Markdown local images, use `![Frequency curve](ABSOLUTE_PATH/frequency.png)`.
In a sandbox/download host, use its generated attachment URL instead of inventing
a URL. Offer the SVG and JSON alongside the image. If rendering is unavailable,
state that and provide downloadable files.
