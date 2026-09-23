# BFGS objective-rounding repair: evidence and limits

The current-release repair is confined to Numerics `BFGS.cs`. It adds a terminal gradient check when a rejected trial differs from the initial objective by at most `8 * machine epsilon * abs(initial objective)`. A finite supplied gradient must satisfy the existing projected-gradient absolute tolerance. A small objective change alone cannot establish success. Normal continuing steps retain their Wolfe conditions; internally computed finite-difference gradients are excluded from the new recovery path. There is no unit-scale floor in the objective comparison.

The check trusts the supplied derivative, as the existing convergence test does. It establishes first-order stationarity, not a global minimum or a second-order condition for arbitrary nonconvex objectives. It can perform additional supplied-gradient calls on ambiguous trials. Invalid gradients and exhausted objective budgets retain explicit failure statuses.

No GMM, Nelder–Mead, B17C, penalty, tolerance, seed, fallback-policy, or serialization code was changed in this repair. The future outer-iteration design is saved locally at `docs/superpowers/specs/2026-09-17-gmm-outer-iteration-damping-design.md`; that directory is intentionally ignored by this repository. Its implementation is deferred beyond this release.

## Actual 1,000-realization replay

The frozen Example #1 - BCB inputs and uninstrumented benchmark are in [the preceding evidence directory](../b17c-repair-evidence-20260917/). Seed 12345 and all 1,001 parent/bootstrap fixture records are identical to the baseline. The baseline is Numerics `189a5973559cade7725f33cef4ded8fce4e16ccc`; the BestFit baseline for this work is `cc5931cb9f069a0a879def64a4d248f742d1f3fb`. Source hashes and the final repair revision are recorded with the evidence.

| Measurement | Before | After |
|---|---:|---:|
| Bootstrap objective evaluations | 696,401 | 326,134 |
| BFGS fallbacks | 203 | 77 |
| Parent passes / evaluations | 6 / 55 | 6 / 55 |
| Bootstrap fits confirmed converged | 949 | 947 |
| Bootstrap fits reaching 100 passes | 51 | 53 |
| Median bootstrap passes | 12 | 12 |
| Bootstrap fits using at most five passes | 50 | 50 |
| First-chance exceptions | 0 | 0 |

The parent parameters are unchanged. All 1,000 bootstrap realizations remain accepted by the existing policy; none were retried or replaced. This is a 62% reduction in BFGS fallbacks and a 53% reduction in objective evaluations, **not a completed outer-GMM convergence fix**.

Uninstrumented elapsed times for the repaired production build:

| Configuration | Cold run | Median of three warm runs |
|---|---:|---:|
| Release | 2.033 s | 0.482 s |
| Debug | 1.562 s | 1.193 s |

The preceding baseline recorded warm medians of 0.713 s and 1.862 s, respectively. Those are earlier same-machine measurements, not interleaved timing experiments. Current timings were taken after the test processes finished.

The new path accepted 707 trials. Independent direct moment/penalty formulas, evaluated with each captured weight held fixed, verify every one against the existing `1e-8` gradient tolerance. The largest independent gradient norm is `9.995209503554094e-9`; the largest discrepancy from the supplied gradient is `1.2803976174960763e-13`. All 22,731 regularized matrices in the full trace are positive definite; the minimum eigenvalue is `7.366136339294137e-7`. The 77 remaining BFGS failures have gradient norms from approximately `1.00e-8` to `6.88e-8` and remain failures rather than being reclassified by an objective-only criterion.

## Observable effects and remaining outer-loop limitation

This change does affect downstream optimizer paths; it is not bitwise neutral. Of the 947 bootstrap fits converged in both runs, 725 are unchanged. Among the 222 changed fits, 159 are closer to the independently calculated stationary fixed point. The largest parameter change in that group is `5.188060095762115e-5`, predominantly an improvement in skew accuracy. The maximum distance to the independent fixed point across that group falls from `5.1977767711597345e-5` to `3.0565563481799174e-5`. These are diagnostics, not new acceptance tolerances.

The two additional capped fits are zero-based realizations **199** and **534**:

| Realization | Previous stop | Previous final inner gradient norm | New final inner gradient norm | Existing outer-map spectral radius |
|---|---:|---:|---:|---:|
| 199 | 99 passes | about `7.33e-5` | about `1.77e-11` | 0.932339 |
| 534 | 81 passes | about `5.08e-5` | about `7.53e-10` | 0.915263 |

Previously, Nelder–Mead returned an identical point on consecutive passes, satisfying the current outer parameter-change rule despite the unresolved inner gradient. The repaired BFGS solves those inner problems accurately and continues the slowly contracting alternating outer updates. At the unchanged 100-pass limit, their parameter errors relative to the independent fixed points are `5.92e-5` and `1.03e-5`, versus the previous `3.12e-6` and `2.00e-6`. Thus their finite-budget outputs are less accurate even though the inner solves are more accurate. Their `ConvergedWithinTolerance=false` results are recorded explicitly; no tolerance or fallback policy was changed to conceal this interaction.

The other 51 capped fits remain capped. The requested general 3–5-pass performance is not established. The deferred outer-loop design addresses a different problem and must not be silently included in this release repair.

## Reproduce and inspect

- `trace-extract.jsonl.gz` retains every new recovered trial with its weight and gradient, every remaining BFGS failure with its line-search history, every final fit/covariance record, the last three inner-pass records per fit, and all inner-pass records for realizations 199 and 534.
- The identical fixture inputs and independently computed fixed points come from [the prior diagnostic package](../b17c-cholesky-evidence-20260917/README.md).
- `analyze.py` recomputes the comparison, checks every recovered gradient, and diagnoses the two additional capped fits. With Python 3.12.14 and NumPy 2.3.5, `python analyze.py --check` exactly reproduces `results.json` without editing production files or saved results.
- `full-trace-audit.json` records event counts, the full-trace hash, source hashes, and the eigenvalue audit of every returned regularized matrix. The complete trace remains at the local path recorded there; the portable extract does not contain every intermediate matrix.
- `benchmark-release.jsonl` and `benchmark-debug.jsonl` contain one cold and three warm uninstrumented runs apiece. Each run creates a fresh analysis and restores the frozen inputs. Stopwatch timing excludes compilation.
- `validation.json` records the test/build results, exact method TRX, the transient live-data test failure and its retry, and repair revision. `manifest.json` records hashes for this evidence package.

The analytical regression tests cover an objective whose exact quadratic minimum appears rounded uphill, negative objective values, maximization, nonstationary ambiguous trials, a genuinely uphill stationary point, invalid gradients, and evaluation-budget exhaustion. Existing optimizer tests additionally cover finite-difference gradients, active bounds, stationary starts, warm starts, scaling, Rosenbrock, and iteration limits. The source review found no blocking correctness defect. Passing these checks does not imply arbitrary downstream fits are bitwise unchanged or that every rounding-limited line search is resolved.
