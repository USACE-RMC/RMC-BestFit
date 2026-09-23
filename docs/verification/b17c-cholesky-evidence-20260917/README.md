# B17C Cholesky/ridge and BFGS/GMM diagnostic evidence

See the [diagnosis and validation report](../b17c-cholesky-and-bfgs-diagnosis-20260917.md). These artifacts describe Numerics `189a5973559cade7725f33cef4ded8fce4e16ccc` with BestFit `4732d5cc72a34946ccdbc6851a28f2a18831351b`, Example #1 - BCB, seed 12345, and 1,000 bootstrap realizations. The model XML snapshots and uninstrumented benchmark are in [the preceding repair evidence directory](../b17c-repair-evidence-20260917/).

## Contents

- `diagnostic-inputs.jsonl.gz`: all 1,001 fixture inputs, 1,001 final fit records, all 203 BFGS failure records with their frozen matrices and line-search trials, and the last three outer passes of every fit. The input data are log10 observations. Its uncompressed SHA-256 is recorded in `manifest.json`.
- `analyze-bfgs-cause.py`: independently evaluates analytical moment/penalty derivatives, local Hessians, and 70-digit Decimal objective differences on the captured inputs.
- `outer-map-diagnosis.py`: computes stationary points and local derivatives of the existing outer weighting iteration for every fixture. This is diagnostic mathematics, not a production estimator proposal.
- `confirm-outer-map.py`: independently checks selected iteration derivatives through perturbed inner solves and replays realization 8 with pinned SciPy BFGS and the existing outer equations and convergence criteria.
- `bfgs-diagnosis.json` and `outer-diagnosis.json`: summaries for every failure/fixture, with detailed representative calculations. The scripts regenerate full details in `generated/`.
- `outer-map-confirmation.json`: perturbation checks and the full 100-pass SciPy comparison.
- `part1-current-parity.json` and `part1-original-parity.json`: exception-only parity results before correcting the terminal ridge, including normalized trace hashes and event counts.
- `repair-results.json`: final production replay counts, pass histogram, matrix checks, and changed fit/covariance identities.
- `benchmark-release.jsonl` and `benchmark-debug.jsonl`: actual uninstrumented runs, each with one cold run and three warm runs using fresh analysis objects.
- `validation.json`: build/test commands, counts, and local evidence paths.
- `manifest.json`: versions, source revisions, input provenance, and file hashes.

The live example database was open and changed during the session. These tests use the preserved XML snapshots consistently; the recorded results do not imply that the live database stayed unchanged.

## Reproduce the independent diagnosis

The recorded environment is Python 3.12.14, NumPy 2.3.5, and SciPy 1.16.2. Use an isolated Python environment with the pinned packages; they are diagnostic tools, not application dependencies. From this directory:

```powershell
python -m pip install numpy==2.3.5 scipy==1.16.2
python analyze-bfgs-cause.py
python outer-map-diagnosis.py
python confirm-outer-map.py
```

`confirm-outer-map.py` uses the generated output of `outer-map-diagnosis.py`. The three scripts were rerun from this packaged input; all saved fields and the entire confirmation result matched exactly on the recorded environment. No production file is edited by these scripts. `generated/`, optional local `reference-packages/`, and Python caches are ignored.

The independent objective calculation holds each recorded weight fixed and uses the same binary input numbers. Decimal arithmetic evaluates the mathematical objective at 70 digits, rather than reproducing the C# summation order. The derivative checks likewise use direct formulas and independently computed Hessians. The outer-map checks use polynomial moment formulas with complex-step derivatives, then verify selected gains with separate perturbed solves. These are scoped to the active systematic-only, real-scale skew-penalty fixture.

## Reproduce the uninstrumented model timing

With the source revisions above checked out, run from the BestFit root:

```powershell
dotnet run --project docs/verification/b17c-repair-evidence-20260917/Benchmark.csproj -c Release
dotnet run --project docs/verification/b17c-repair-evidence-20260917/Benchmark.csproj -c Debug
```

The stopwatch covers the analysis run, not compilation. Every run restores the same model XML, seed, and realization count. Compare cold and warm measurements separately. Timing alone does not establish convergence: the final replay still has 51 fits at the 100-pass limit.

## Local full traces

The source-instrumented harness, complete first-chance exception traces, candidate/factor/returned-matrix traces, build logs, and full intermediate results remain under the ignored directory:

`C:\GIT\RMC-BestFit\src\RMC.BestFit\obj\b17c-regression-diagnostic-20260917`

Key files are `trace-cholesky-original-baseline.jsonl`, `trace-cholesky-original-part1.jsonl`, `trace-cholesky-baseline.jsonl`, `trace-cholesky-part1.jsonl`, `trace-cholesky-final.jsonl`, and `trace-bfgs-cause.jsonl`. Instrumentation was confined to scratch source copies. The compressed portable input here preserves every failed BFGS solve and every fixture needed to reproduce the independent root-cause calculations.
