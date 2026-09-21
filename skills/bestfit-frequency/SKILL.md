---
name: bestfit-frequency
description: Use when asked to run RMC-BestFit flood or flow frequency analysis, Bulletin 17C with optional MGBT low-outlier screening, or Bayesian univariate analysis from USGS annual peaks or supplied observations, and display a matplotlib frequency curve matching BestFit's default desktop plots.
---

# BestFit frequency curves

Use the headless BestFit API for numerical work and the bundled Python scripts for
requests, saved artifacts, and plotting. In a web session, clone the
[RMC-BestFit repository](https://github.com/USACE-RMC/RMC-BestFit) into that session's
execution workspace, then build and run the API there. The Python client connects
to the API through loopback inside the same environment. The repository supplies
the source; no separately hosted BestFit service or MCP connector is needed.

This workflow needs a terminal, a writable workspace, .NET 10 SDK, Python 3.10+,
dependency download access, and a session that can keep the API process alive while
making local HTTP requests. Check those capabilities and source compatibility in
[setup.md](references/setup.md). A repository link or uploaded skill does not itself
provide an execution environment.

## Choose the analysis and input

- Follow the requested analysis. For an unspecified frequency analysis, use BestFit's
  Bayesian univariate workflow. Bulletin 17C is a separate GMM/EMA workflow.
- Obtain the USGS site number or actual year/value observations. Establish units
  before adding a unit label. Do not fabricate data or silently alter observations.
- For B17C, explicitly enable `useMultipleGrubbsBeckTest` unless the user opts out,
  supplies a manual low-outlier threshold, or supplies preflagged observations.
  The API itself defaults to **false**. MGBT requires at least ten exact observations;
  failures are reported, not bypassed. Preserve zeros in the input.
- A manual threshold alone does not flag observations in the current API. Obtain
  the user's intended `isLowOutlier` flags before running manual screening; do not
  invent a threshold comparison rule or claim a stored threshold was applied.
- Preserve all explicit distribution, uncertainty, seed, prior, probability, and
  sampler settings. Omitted settings retain BestFit's defaults. Do not shorten an
  analysis to make it finish faster. Report B17C's warning if uncertain observations
  are present: that analysis does not use their measurement-error distributions.

## Run and display

1. Read [setup.md](references/setup.md) for checkout/runtime checks, API-only build,
   loopback startup, and owned-process cleanup. Locate the directory containing this
   `SKILL.md`; bundled paths below are relative to it, not the current repository.
2. Read [workflow.md](references/workflow.md) for request formats and a complete
   example. Use `python scripts/run_frequency.py` with the selected method and input.
   Use a new output directory for every run. Save the server build provenance there.
3. Inspect saved `validation.json`, `analysis.json`, and `results.json`. Check HTTP
   failures, `success`, `isValid`, `errorMessage`, `validationErrors`,
   `validationWarnings`, `nonFiniteFindings`, and `diagnostics.convergenceWarnings`.
   Validation responses additionally use `errors` and `warnings`.
   For Bayesian results, also report R-hat/ESS concerns. A plot is not evidence of
   convergence or scientific validation. If the run fails, retain and explain the
   failure; do not label partial output as a completed curve.
4. Render the **same run's** `results.json` and `input.json`:
   ```sh
   python scripts/plot_frequency.py --results RUN/results.json --input RUN/input.json --output RUN/frequency
   ```
   Add `--ylabel` only for known units. Add `--title` if useful. The renderer writes
   PNG and SVG and reports display omissions. Read [plot-contract.md](references/plot-contract.md)
   when interpreting markers, uncertainty, or unavailable display fields.
5. Open/inspect the PNG with the host's image tool. Then embed it using that host's
   supported image attachment or Markdown image mechanism, with a downloadable SVG
   and results JSON. If the chat cannot render local images, supply the files and
   state that limitation; creating a PNG alone is not displaying it in chat.
6. Summarize the input source, method, screening count/threshold, units, and relevant
   warnings beside the image. Stop only the API process started for this run.

## Common problems

| Symptom | Action |
|---|---|
| No terminal or runtimes | Explain the missing capability; use a terminal-capable session or a user-supplied API/results file. |
| Missing MGBT option/display bounds | Use a checkout containing this skill's API changes; do not approximate them in Python. |
| Build dependency error | Follow setup checks and report exact versions; do not silently swap Numerics versions. |
| USGS failure | Keep the error and site number; ask for supplied observations if download access is unavailable. |
| Timeout | Check server state before rerunning; the API cancels when the client disconnects. |
| Missing/invalid plot coordinates | Fetch `includeData=true`; retain gaps and report omissions rather than refitting or clipping data. |

Installation and ZIP distribution are described in [install.md](references/install.md).
