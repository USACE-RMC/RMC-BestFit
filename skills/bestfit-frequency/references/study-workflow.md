# Reproducible agentic FFA studies

First select a [worked tutorial](examples.md) matching the evidence and analysis
type. Consult its source/observation tables, settings, diagnostics and limitations
while constructing the study; preserve study-specific judgments as decisions,
not values copied from a teaching project.

Start from `assets/synthetic-study.json`, a runnable synthetic demonstration, not
real Blakely, Kamp or regional evidence. Its schema has:

- `schemaVersion: 1`.
- `study`: location (outlet/gage/coordinates), flowDefinition, units, yearConvention
  (`waterYear` or `calendarYear`), waterYearStartMonth (default 10), regulation;
  optional systematicWindow `[start,end]`, siteNumber, basin and requested AEP context.
- `sources`: unique id, url, title/version, retrievedUtc, locator (page/table/section);
  optional artifact path relative to the study directory and sha256 of original bytes.
- `inputs`: named manual API requests. Each observation needs evidenceIds; thresholds
  additionally need completenessRationale. Use explicit annual indexes or unambiguous
  dates with the declared ending-year convention.
- `scenarios`: unique safe name, input name, kind (`univariate` default or `bulletin17c`),
  options (API analysis request without inputDataId), evidenceIds and rationale.
- `decisions`: accepted/rejected/unresolved interpretations and their evidence,
  including conflicting values, unit conversions, duplicate events and uncertain dates.

All numerical entries use the declared study units. Keep original values and
conversion calculations in evidence files. Source artifacts must be files inside
the study directory; the runner copies them and checks supplied hashes. Citation-only
sources are explicitly reported as lacking retained bytes. Preparation validates
structure and traceability, not scientific truth or the quality of a rationale.

## Define candidates

Keep separate inputs for baseline, historical and paleo augmentation. Preserve
systematic values across inputs. Add regional-skew, individual regional-quantile,
causal and supported combined scenarios. Specify the parent distribution when not
LP3. Instead of manually writing common priors, a scenario can contain:

```json
"information":[
  {"type":"skew", "mean":-0.17, "mse":0.12,
   "evidenceIds":["regional-report"], "rationale":"Applicable LP3 region and MSE"}
]
```

Or one explicitly justified Normal quantile uncertainty model:

```json
"information":[
  {"type":"causalQuantile", "aep":0.002, "mean":480,
   "uncertainty":80, "uncertaintyKind":"sd", "space":"physical",
   "evidenceIds":["elicitation"], "rationale":"Source, duration and outlet match"}
]
```

`regionalQuantile` and `quantile` have the same mapping. Uncertainty kinds are `sd`
and `variance`; B17C also permits `space:"log10"`. SEP percentages/prediction
intervals need source-specific interpretation first. Do not claim a Normal
uncertainty model without considering its support/tails and source assumptions.

Multiple prior/penalty terms require `dependenceAssessment` explaining shared
data/covariance and why the representation is a reasonable sensitivity candidate.
The helper checks that the explanation is present; it cannot prove independence.
Keep unresolved dependencies in separate candidates. Two automatic quantile
mappings cannot be merged; supply a justified supported multi-quantile API request
or use separate single-quantile scenarios.

For augmented-data MGBT, specify `screeningInput:"baseline"`. That input must contain
only the documented systematic exact cohort, matching candidate years/values. Mark
historical or paleo exact rows with `recordType:"historical"` or `"paleo"`; this
classification is preserved in the coverage audit and excludes them from a screening cohort. The
runner saves API screening flags/threshold and transfers them into the augmented
request with screening disabled. Alternatively supply manual flags/threshold or
explicit screening off. Historical **exact** events also need a cohort decision:
absence of interval data does not prove every exact value is systematic.

The preparation helper rejects unknown analysis-option names, including nested
sampler/prior fields, so misspellings cannot silently turn into API defaults. Use
only the documented stationary univariate/B17C options; inspect `configuration`
to confirm effective settings after model validation.

## Execute

Start the compatible loopback API per [setup.md](setup.md), then:

```sh
python scripts/prepare_study.py --study study.json --output prepared
python scripts/run_study.py --study study.json --output preview --prepare-only
python scripts/run_study.py --study study.json --output candidates
```

Use the **original** evidence-bearing study for both helpers, not stripped
`prepared.json`. Preparation checks evidence, indexes and counts without fitting.
The preview creates inputs, captures sources and renders chronology without
creating an analysis. Inspect/show those images and resolve contradictory coverage.
Each candidate also renders its chronology before analysis creation, validates,
runs, captures settings and renders the matching frequency curve.

For a direct API input request:

```sh
python scripts/run_frequency.py --manual input.json --prepare-only --output input-review --ylabel "Discharge (cfs)" --index-label "Water year" --zoom 1950 2020
python scripts/plot_chronology.py --input input-review/chronology.json --output input-review/chronology-detail --ylabel "Discharge (cfs)"
```

Use fresh output directories. Bundles retain sources/receipts, original/prepared
study, coverage, requests/responses, input chronology, frequency plots, applied
settings, diagnostics, comparison JSON/CSV and checksums. Failed candidates remain
visible; blank cells are not zero flows. No automatic tuning of seeds, samplers,
priors or tolerances is performed after failures.

## Interpret and deliver

Compare the same AEPs/units and state interval width/method. Narrower intervals do
not establish improved truth. Inspect R-hat/ESS, convergence warnings, accepted
periods and dependencies. Avoid ranking information criteria across different data.
Explain which changes result from historical, skew or quantile information.

Show chronology and frequency PNGs; link SVG, evidence, requests, effective settings
and comparison. State unresolved judgments and excluded evidence. A request for
analysis authorizes documented candidates; final engineering adoption is separate.
Complete supported independent candidates while reporting unavailable regional
information, runtime capabilities or uncertainty definitions.
