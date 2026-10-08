# BFGS repair and B17C regression evidence

The approved BFGS and systematic-data Jacobian repairs are implemented. The headless 1,000-realization Release benchmark completes in approximately 0.77 seconds after warm-up. **The complete B17C numerical/performance defect is not fixed:** 50 realizations still reach 100 outer GMM passes, and indefinite weighting matrices remain possible. No GMM equations, weighting updates, stopping rules, penalties, seeds, limits, or fallback policy were changed.

## Implemented scope

`Numerics/Mathematics/Optimization/Local/BFGS.cs` now checks the infinity norm of the projected gradient at initialization and after accepted steps, using the existing absolute tolerance. A small objective change or an exhausted parameter step no longer establishes successful convergence. The returned successful point is the point tested for convergence, and accepted iterations are counted accurately.

The existing strong-Wolfe search uses the scaled direction's slope, a feasible ray and step limit, both bracket endpoint values and available slopes, and safeguarded cubic/quadratic interpolation. It checks gradients at rounded-equal objective values before rejecting a potentially valid Wolfe point, detects unchanging trials/brackets, and reuses the accepted gradient. Invalid curvature is skipped or resets the metric; an exhausted quasi-Newton search gets one retry with the identity metric and the same Wolfe conditions and budget. Genuine exhaustion remains `LineSearchFailed`. Supplied gradients receive the maximization sign and are copied before use; invalid gradients and exhausted evaluation budgets retain failure statuses. The unused private Armijo routine was removed.

`Bulletin17CDistribution` supplies the analytical Pearson III/Log-Pearson III systematic-data Jacobian through its existing interface. It includes the existing finite-sample corrections and inverse-link derivatives. Mixed/censored data and other families retain numerical differentiation. Exact systematic moment evaluations skip unused integration-bound inverse CDFs. Nelder–Mead, GMM, both B17C analysis classes, matrix regularization, and Cholesky were not changed.

The source review used [SciPy BFGS 1.16.2](https://github.com/scipy/scipy/blob/v1.16.2/scipy/optimize/_optimize.py), [SciPy's Wolfe search](https://github.com/scipy/scipy/blob/v1.16.2/scipy/optimize/_linesearch.py), [Math.NET BFGS 5.0.0](https://github.com/mathnet/mathnet-numerics/blob/v5.0.0/src/Numerics/Optimization/BfgsMinimizer.cs), and [R's vmmin](https://github.com/wch/r-source/blob/R-4-4-branch/src/appl/optim.c). The implementation retains Numerics' BFGS update and public surface; it does not import another solver. Bounded external comparisons used SciPy L-BFGS-B because SciPy's ordinary BFGS does not support bounds.

## Reproduction

Original repository baselines: Numerics `e485640`, BestFit `1e8f26c`. The supplied project SHA-256 was checked again after the implementation:

`B25A2659F862C768F7B519A01F7631B64B2C44A66324D77A8AC654D953F1C8B7`

Input: `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.bestfit`, Example #1 - BCB, 68 exact systematic observations, regional skew 0.44, MSE 0.078, 1,000 realizations, seed 12345. The model's enabled penalty was preserved.

The [benchmark project and frozen XML inputs](b17c-repair-evidence-20260917/Benchmark.csproj) invoke the actual BestFit and local Numerics assemblies. From the BestFit repository root:

```powershell
dotnet run --project docs/verification/b17c-repair-evidence-20260917/Benchmark.csproj -c Release
dotnet run --project docs/verification/b17c-repair-evidence-20260917/Benchmark.csproj -c Debug
```

Each command creates a fresh model for one cold run and three measured runs. There is no exception listener, optimizer instrumentation, debugger, or UI work in this benchmark. JSON output includes all production bootstrap counters. The XML snapshots were extracted from the supplied model before diagnosis; the original project was not rewritten.

Separate trace builds instrumented copies under `src/RMC.BestFit/obj/b17c-regression-diagnostic-20260917`, preserving production source. `trace-repaired-final.jsonl` records the full analysis; `trace-repaired-serial.jsonl` records every realization's first-chance exceptions, inner solver statuses/fallbacks, outer passes, final parameters, and `ConvergedWithinTolerance`. The serial trace and complete analysis agree on objective evaluations and fallback counts. Serial realization indices are zero based; parallel fit IDs must not be interpreted as realization indices.

## Results

| Measurement | Before | After |
|---|---:|---:|
| Parent outer passes | 100 | 6 |
| Parent convergence flag | false | true |
| Parent objective evaluations | 826 | 55 |
| Bootstrap objective evaluations | 960,828 | 683,957 |
| Bootstrap fits reaching 100 passes | 675/1,000 | 50/1,000 |
| BFGS fallbacks | 165 | 202 |
| Weighting-matrix Cholesky exceptions | 420 | 411 |
| Covariance-path Cholesky exceptions | 9 | 9 |

The bootstrap median is 12 outer passes; only 50 realizations finish within five. The remaining BFGS line-search failures occur near the requested gradient tolerance: the traced infinity norms range from 1.0020289543e-8 to 5.0538246477e-8. They remain reported failures, and the unchanged GMM policy uses Nelder–Mead for the rest of those fits. The increase in fallback count is visible and is not treated as a successful resolution of all optimizer failures.

Warm timings against the actual assemblies:

| Configuration | Three runs, milliseconds | Median | Cold run |
|---|---|---:|---:|
| Release | 772.2006, 766.4873, 818.8463 | 0.7722 seconds | 2.1002 seconds |
| Debug | 1976.6563, 2017.1049, 1976.4312 | 1.9767 seconds | 2.3341 seconds |

The saved [Release](b17c-repair-evidence-20260917/benchmark-release.jsonl) and [Debug](b17c-repair-evidence-20260917/benchmark-debug.jsonl) runs each reproduce 683,957 bootstrap objective evaluations and 202 fallbacks. These headless timings do not establish UI or debugger performance. Earlier diagnostic timings included trace-related overhead and should not be used for a direct speedup ratio.

## Remaining numerical failures

[The frozen residual reproductions](b17c-repair-evidence-20260917/residual-reproductions.json) include logged observations, randomized penalty centers, MSE, initial parameters, realization seeds, all outer solver results, convergence flags, and rejected regularization matrices.

Realization 8, seed 1219180210, still alternates between two fits. Pass 99 returns approximately `(3.2929216571, 0.1323194360, -0.1607021283)`; pass 100 returns `(3.3181854994, 0.1732220284, 0.5576639277)`. The existing acceptance logic retains this fit despite a false outer convergence flag. The independent SciPy 1.16.2 run on the corresponding original diagnostic fixture also reproduces the 100-pass two-cycle with the existing GMM loop and analytical derivatives. This is evidence of a remaining outer-iteration problem, not a reason to weaken BFGS's convergence checks.

Realizations 213 and 402 produce indefinite moment matrices. The current terminal ridge fallback is smaller than a ridge that already failed, and returns its matrix without checking positive definiteness. In realization 213, this permits a negative GMM quadratic and a fitted location/scale at the lower bounds, even though the existing flags report convergence. Therefore, the 950 true outer convergence flags are **not** a certificate that every retained fit is statistically valid.

The [separate regularization plan](b17c-regularization-exception-plan-20260917.md) describes how to remove expected Cholesky exceptions without changing numerical results, and isolates the terminal-ridge correction as a numerical decision requiring separate approval. That plan has not been implemented.

The historical penalty-preservation change `e5c9222` should not be reverted: its older systematic-data clone path dropped enabled penalties. Likewise, `5b56a01` and `d29f87a` exposed/routed line-search failures that must remain visible. Restoring false success or dropping penalties would conceal this regression.

## Validation

- Numerics Release build: net481, net8.0, net9.0, net10.0; zero warnings/errors.
- Focused BFGS and Augmented Lagrange regression tests: 36 per framework, all passing. New analytical tests cover scaled/warm quadratics, stationary starts, Rosenbrock, valid bound optima, maximization gradients, non-finite inputs, true Wolfe failure, budgets, rounded objectives, and very short Wolfe steps. The original false-success and bound-failure cases were observed failing before repair.
- Complete Numerics Release suite: 2,857 cases on net481 and 2,872 on each modern framework, 11,473 distinct framework/test cases. Fifteen initial live USGS/BOM failures were HTTP 500/503 or timeout failures; all 15 passed when only those exact failed cases were rerun, serially by framework. There are no unresolved numerical test failures.
- BestFit Debug solution build: zero warnings/errors. All four mandatory fast suites pass: core 3,434; UI 645; App 444; API 498; total 5,021.
- Fourteen new B17C derivative/fallback cases pass, including hand-calculated Jacobians, independent five-point link-space derivatives, and small-sample corrections.
- Scoped B17C verification: all seven published examples and 13 independent covariance-oracle tests pass. Example #1 also passed the one-method/one-TRX `scripts/run-verification-test.ps1` workflow. No coverage study or complete Verification suite was run.
- XML/private-method documentation scan: 945 source files, passed. Git whitespace checks pass.

Raw Numerics TRXs are under `C:\GIT\Numerics\Test_Numerics\TestResults` with prefixes `bfgs-repair-final` and `bfgs-network-retry`. BestFit scoped TRXs are under `TestResults/B17CRepairExamples`, `TestResults/B17CRepairCovariance`, and `TestResults/VerificationFocused/20260917-115809-RMC_BestFit_Verification_Univariate_Bulletin17CTests_B17CExampleTests_Test_Example1`.
