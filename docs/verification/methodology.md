# Verification Methodology

## Purpose

The program establishes claim-specific evidence for statistical correctness, numerical stability, and external parity. Unit tests remain in `RMC.BestFit.Tests`; this report covers only numerical verification and validation.

## Evidence hierarchy

1. Closed-form analytical solution.
2. Independently implemented algorithm.
3. Trusted external package with a documented parameterization crosswalk.
4. Published worked example or table.
5. Simulation coverage with a predeclared design and Monte Carlo acceptance interval.

Regression pins and estimator smoke tests are useful engineering checks but are not independent verification.

## Reproducibility contract

Each external artifact records the source dataset, package and runtime versions, generator path, command, seed, tolerance and rationale, SHA-256 hash, generation date, and affected finding IDs. R and Python generate committed CSV or JSON files. C# tests do not invoke those runtimes.

## Tolerance policy

| Comparison | Default |
|---|---|
| Deterministic analytical arithmetic | Absolute tolerance approximately \(10^{-10}\) |
| Cross-language log likelihood and criteria | Absolute \(10^{-8}\), relative \(10^{-7}\) |
| Optimizer parameter parity | Scaled tolerance no looser than \(10^{-5}\), plus objective parity |
| PSIS aggregate values | \(10^{-6}\) |
| Pareto \(k\) | \(10^{-4}\) |
| Coverage | Nominal rate must lie in the predeclared binomial interval |

Exceptions must be documented before the C# result is observed.

## Defect off-ramp

When independent evidence confirms a defect, scientific progression stops. The finding is updated and a focused correction plan is presented. Production correction begins only after approval and must preserve public signatures and serialization whenever possible.

After a correction, the three fast test projects, XML documentation gate, public API compatibility check, focused verification test, technical-reference chapter, finding entry, and this report must all agree before the finding is marked fixed.
