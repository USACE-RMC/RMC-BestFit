<!-- verification-status: finalized -->
# Verification Methodology

## Purpose

The program establishes claim-specific evidence for statistical correctness, numerical stability, and external parity. Unit tests remain in `RMC.BestFit.Tests`; this report covers only numerical verification and validation.

## Evidence hierarchy

1. Closed-form analytical solution.
2. Independently implemented algorithm.
3. Trusted external package with a documented parameterization crosswalk.
4. Published worked example or table.
5. Simulation coverage with a predeclared design and Monte Carlo acceptance interval.

Regression pins and estimator smoke tests are useful engineering checks but are not independent verification. Merely invoking MLE, MAP, GMM, or MCMC does not make a test Verification-owned; convergence, finiteness, and result-shape assertions without an independent target are insufficient.

Recovery tests predeclare the generating model, sample size, seed, fitted parameters or curves, diagnostic requirements, and acceptance bounds. Every generated-parent recovery uses exactly 1,000 declared observational units. Probability-distribution MLE cells use a predeclared 95% interval or absolute standardized error no greater than 1.96 from the Numerics distribution-level parameter-variance API evaluated at recovered parameters and N=1000 where implemented; when that API is unavailable, a technical-authority-approved production true-profile-likelihood interval is the alternative. MAP cells retain their documented observed-information intervals; GMM cells retain their documented sandwich or true-profile uncertainty source. A weakly identified, correlated, boundary, label-switching, or zero-parent probability-distribution MLE coordinate is accepted through a predeclared identified response ordinate rather than a relative coordinate band: where implemented, its documented Numerics quantile-variance 95% response band must contain the generating response. Bayesian cells use central-95% parent inclusion, R-hat below 1.10, and ESS at least 100 for every monitored coordinate. A secondary 5% point/curve criterion applies only after an existing finite, ordered 95% band is narrower than 5% of a nonzero parent. Coverage tests additionally predeclare the repetition count, nominal target, and Monte Carlo acceptance interval. Deterministic validation, state, exception, serialization, cache, and calculation contracts remain in the fast projects and may use injected or restored fitted state so long as they do not run an estimator.

## Catalog contract

`verification-catalog.json` is the method-level ownership ledger for every current `[TestMethod]` and `[DataTestMethod]` declaration in `RMC.BestFit.Verification`. It is validated against `verification-catalog.schema.json` and the source tree, so a catalog identity is not inferred from a filename or test name.

| Field group | Required meaning |
|---|---|
| `source`, `namespace`, `class`, `method` | Exact source declaration identity discovered from the C# source. |
| `analysis`, `model` | Scientific analysis and parameterization, including transform, differencing, dependence, or trend context when applicable. |
| `primaryEvidenceKind`, `evidenceTags` | One primary claim and every applicable evidence type. |
| `disposition`, `status`, `gap` | Current Verification ownership, present evidence state, and the exact planned remediation for an open entry. |
| `executesEstimator` | Whether the method invokes an optimizer or sampler; estimator execution is not evidence by itself. |
| `sampleUnit`, `sampleSize`, `seed` | Observational unit, exact design size, and reproducibility seed. Recovery uses exactly 1,000 observational units; for spatial analyses one unit is a complete row/year vector across the declared site network. |
| `oracle`, `acceptanceRule` | Independent target or rationale and the executable quantitative decision rule. |
| `artifact`, `reportAnchor` | Committed external evidence, when present, and an existing report heading that states the method's evidence context. |
| `methodOverrides` | Named `[DataRow]` execution-unit differences for a `[DataTestMethod]`; ordinary methods cannot use overrides. |

One method can support more than one evidence type. `primaryEvidenceKind` identifies the claim that controls ownership, while `evidenceTags` records additional genuine evidence without promoting a same-path or engineering assertion. For example, an independently generated R recovery fixture can carry `external-package`, `independent`, and `recovery`; its primary claim remains the independently supported comparison chosen in the catalog. Each named data row inherits the declaration fields unless its `methodOverrides` entry supplies a row-specific sample design, oracle, rule, status, or disposition.

`verified` means the current method has qualifying analytical, independently implemented, external-package, published/real-source, recovery, or coverage evidence and a quantitative rule. `open` means the current declaration is incomplete, same-production-path, smoke/shape/finiteness-only, qualitative, engineering-owned, or otherwise requires the recorded future disposition. `accepted-limitation` is reserved for a justified scientific boundary with qualifying evidence and an explicit residual limitation; it is not a substitute for an unresolved gap. `execution-excluded` preserves governed historical evidence that this remediation program must not run.

Default validation is an audit mode: it checks schema, source completeness, unique identities, named rows, recovery sample sizes, artifacts, and report anchors, reports each `GAP`, and succeeds when those checks pass. `-RequireComplete` is the strict closeout mode: the same checks run, but every `open` entry is an error. Accepted limitations and explicitly governed execution exclusions remain visible and do not fail strict mode.

The confidence-interval coverage methods in `B17CCoverageTests`, `B17CCensoredCoverageTests`, and `B17CCohnEtAlCoverageTests` are execution-excluded historical evidence. Their declarations and the 30 named Cohn rows remain cataloged, but this program does not execute those classes, any of their methods, or the full Verification suite. Historical coverage results are reruns-on-request; catalog inspection does not imply a new run.

## Reproducibility contract

Each external artifact records the source dataset, package and runtime versions, generator path, command, seed, tolerance and rationale, SHA-256 hash, generation date, and affected finding IDs. R and Python generate committed CSV or JSON files. C# tests do not invoke those runtimes.

## Tolerance policy

| Comparison | Default |
|---|---|
| Deterministic analytical arithmetic | Absolute tolerance approximately \(10^{-10}\) |
| Cross-language log likelihood and criteria | Absolute \(10^{-8}\), relative \(10^{-7}\) |
| Optimizer parameter parity | Scaled \(10^{-5}\) for comparable parameter-converged configurations; scaled \(10^{-4}\) for objective-converged global versus parameter-converged local optimizers; tight objective parity is required in both cases |
| PSIS aggregate and pointwise values | \(10^{-10}\) |
| Smoothed weights, Pareto \(k\), and importance-sampling effective sample size | \(10^{-8}\) |
| Coverage | Nominal rate must lie in the predeclared binomial interval |

Exceptions must be documented before the C# result is observed.

## Defect off-ramp

When independent evidence confirms a defect, scientific progression stops. The finding is updated and a focused correction plan is presented. Production correction begins only after approval and must preserve public signatures and serialization whenever possible.

After a correction, the three fast test projects, XML documentation gate, public API compatibility check, focused verification test, technical-reference chapter, finding entry, and this report must all agree before the finding is marked fixed.
