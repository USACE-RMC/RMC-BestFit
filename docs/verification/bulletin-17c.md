# Bulletin 17C Verification

## Status

The approved Phase 3 scope is closed. TR-003 documents the accepted grouped-threshold disaggregation and most-recent-time prior-reference assumptions for the general nonstationary univariate workflow. TR-016 documents the intentional reuse of the Bayesian analysis result-storage architecture without changing code, public API, or serialization. TR-017 through TR-019 retain their previously recorded naming and bootstrap-refit dispositions. TR-020 restricts Cohn diagnostics to exact-data Log-Pearson Type III (LP3) and is covered by fast unit tests. TR-021 is verified by the seven formal Bulletin 17C worked-example parameter tests described below.

## Phase 3 data-handling assumptions

For nonstationary univariate models, grouped perception-threshold counts are expanded conditionally: explicit records keep their indexes, unoccupied earlier indexes are assigned below-threshold status, and unoccupied indexes in the terminal `NumberAbove` portion are assigned above-threshold status. The allocation is an explicit modeling assumption rather than an inferred event chronology. Distribution-dependent Jeffreys and quantile-prior terms are evaluated once at the last, most-recent observed index, consistent with the published quantile-prior workflow. The [data-frame chronology](../technical-reference/data-frame/index.md#stationary-and-nonstationary-chronology) and [prior reference-time](../technical-reference/models/parameters-and-priors.md#complete-univariate-prior) sections define the full contract. TR-003 is documentation-only and makes no permutation-invariance claim.

## Formal worked-example parameter parity

The executable source is [B17CExampleTests.cs](../../src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CExampleTests.cs), and the published fixtures are defined in [Bulletin17CData.cs](../../src/RMC.BestFit.Verification/Datasets/UnivariateData/Bulletin17CData.cs). Each test:

1. constructs the published example data frame;
2. fits `Bulletin17CDistribution` with the LP3 parent through `GeneralizedMethodOfMoments`;
3. requires `gmm.IsEstimated`; and
4. compares fitted log-space mean, standard deviation, and skewness with the published values at absolute tolerance `1E-3`.

The seven methods were executed separately on 28 July 2026 through `scripts/run-verification-test.ps1`, which source-resolves one fully qualified method, builds only the Debug Verification project, requires exactly one TRX result, and rejects broad filters. Runtime was .NET 10, x64. Every build reported zero warnings and zero errors.

| Example | Station and principal data condition | Published mean | Published standard deviation | Published skewness | Result | Test duration |
|---:|---|---:|---:|---:|---|---:|
| 1 | Moose River at Victory, VT; 68-year systematic record | 3.328623159 | 0.140287994 | 0.396626124 | Passed | 0.832 s |
| 2 | Orestimba Creek near Newman, CA; low outliers and zero-flow years | 3.022663041 | 0.682087092 | -0.929108050 | Passed | 0.402 s |
| 3 | Back Creek near Jones Springs, WV; broken record, thresholds, and low outliers | 3.759834285 | 0.243406211 | 0.144442997 | Passed | 0.425 s |
| 4 | Arkansas River at Pueblo, CO; historical intervals and perception thresholds | 3.885777246 | 0.245920859 | 0.817849937 | Passed | 0.471 s |
| 5 | Bear Creek at Ottumwa, IA; variable crest-stage thresholds and low outliers | 3.278686106 | 0.233135027 | -0.925407257 | Passed | 0.333 s |
| 6 | Santa Cruz River at Lochiel, AZ; historical information and MGBT low outliers | 3.069106533 | 0.489820622 | -0.462278724 | Passed | 0.598 s |
| 7 | American River at Fair Oaks, CA; paleoflood intervals and long perception thresholds | 4.653457000 | 0.376721000 | -0.101163000 | Passed | 0.728 s |

Aggregate result: **7 passed, 0 failed, 0 skipped**. The formal comparisons cover 21 fitted parameter values. Example 1's fourth fixture value is weighted regional-skew metadata and is intentionally not treated as a model parameter.

## Cohn diagnostic scope

`ComputeCohnStyleConfidenceIntervals()` and the report-side asymptotic quantile variance use LP3 base-10 transformations and are supported only when all observations are exact and no exact observation is marked as a low outlier. The production guard rejects:

- Exponential;
- Gamma;
- Log-Normal;
- Normal;
- Pearson Type III;
- LP3 with low outliers;
- LP3 with uncertain observations;
- LP3 with interval censoring; and
- LP3 with threshold censoring.

Fast tests in `Bulletin17CAnalysisTests` cover these rejection cases, retain the unestimated exact-LP3 null contract, and confirm that the private report helper returns no values outside scope. Numerical verification of Cohn interval values is deferred and is not implied by the formal worked-example results.

## Result-storage architecture

Bulletin 17C uses penalized GMM and frequentist uncertainty ensembles, while deliberately storing results through the same `BayesianAnalysis` and `MCMCResults` architecture used by Bayesian analyses. In this context, `MAP` is the penalized GMM estimate, `Output` is the frequentist uncertainty ensemble, `PosteriorMean` is its arithmetic mean, and `CredibleIntervalWidth` supplies the confidence level. This compatibility mapping supports stable persistence, UI integration, and result reprocessing; it does not define a posterior distribution. TR-016 therefore requires documentation, not a parallel result hierarchy or code/API change.

## Evidence boundary

The seven passed methods verify current specialized LP3 GMM parameter parity with the published Bulletin 17C worked examples. They do not verify:

- Cohn confidence-interval values or coverage;
- asymptotic or bootstrap covariance;
- uncertain-data variants;
- PeakFQ plotting-position parity;
- penalty sensitivity;
- bootstrap interval coverage; or
- agreement with an objective/generalized-posterior target.

Those are separate claims and require separately authorized, exactly filtered verification methods or independent artifacts. The legacy version 1 Comparison with EMA report evaluates an earlier Bayesian workflow and is not the oracle for the current specialized GMM path.

## Traceability

- Findings: [TR-003](../technical-reference/review-findings.md#tr-003) and [TR-016 through TR-021](../technical-reference/review-findings.md#tr-016)
- Technical method: [Bulletin 17C analysis](../technical-reference/analysis/bulletin-17c.md)
