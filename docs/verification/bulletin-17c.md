<!-- verification-status: finalized -->
# Bulletin 17C Verification

## Status

TR-003 documents the accepted grouped-threshold disaggregation and most-recent-time prior-reference assumptions for the general nonstationary univariate workflow. TR-016 documents the intentional reuse of the Bayesian analysis result-storage architecture without changing code, public API, or serialization. TR-017 through TR-019 retain their previously recorded naming and bootstrap-refit dispositions. TR-020 restricts Cohn diagnostics to exact-data Log-Pearson Type III (LP3) and is covered by fast unit tests. TR-021 is verified by the seven formal Bulletin 17C worked-example parameter tests described below.

<a id="phase-3-data-handling-assumptions"></a>

## Data-handling assumptions
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

`ComputeCohnStyleConfidenceIntervals()` uses LP3 base-10 transformations and is supported only when all observations are exact and no exact observation is marked as a low outlier. Its production guard rejects:

- Exponential;
- Gamma;
- Log-Normal;
- Normal;
- Pearson Type III;
- LP3 with low outliers;
- LP3 with uncertain observations;
- LP3 with interval censoring; and
- LP3 with threshold censoring.

Fast tests in `Bulletin17CAnalysisTests` cover these rejection cases and retain the unestimated exact-LP3 null contract. Numerical verification of Cohn interval values is deferred and is not implied by the formal worked-example results.

## GMM quantile-variance report contracts

The report's asymptotic delta-method table uses the existing distribution-aware `Bulletin17CDistribution.QuantileVariance` with fitted GMM parameters and covariance. It supports all six parent families and does not apply the Cohn interval scope guard. LP3 and Log-Normal quantiles are in log10 space with squared log10 variance units; other families use native quantiles and squared native units. Unusable covariance is explained without suppressing point estimates, and failed quantile/variance rows display `N/A` without parent-value substitution or clipping negative variance to zero.

`Bulletin17CQuantileVarianceReportTests` contains eighteen deterministic fitted-state cases. Supplied correlated covariance matrices and known-point derivatives test all six families, AEP conversion, parameter-coordinate cross terms, units, preservation of fitted/model state, and report failure handling. LP3 cases with low outliers, uncertain observations, intervals, and thresholds still report available GMM variance while the separate Cohn API rejects them. No optimizer or sampler runs in these tests. These contracts establish report dispatch and arithmetic only, not censored/uncertain covariance accuracy, Cohn/PeakFQ parity, or interval coverage.

## Result-storage architecture

Bulletin 17C uses penalized GMM and frequentist uncertainty ensembles, while deliberately storing results through the same `BayesianAnalysis` and `MCMCResults` architecture used by Bayesian analyses. In this context, `MAP` is the penalized GMM estimate, `Output` is the frequentist uncertainty ensemble, `PosteriorMean` is its arithmetic mean, and `CredibleIntervalWidth` supplies the confidence level. This compatibility mapping supports stable persistence, UI integration, and result reprocessing; it does not define a posterior distribution. TR-016 therefore requires documentation, not a parallel result hierarchy or code/API change.

## Evidence boundary

The seven passed worked-example methods verify current specialized LP3 GMM parameter parity with the published Bulletin 17C examples. Three additional PeakFQ cells verify Hirsch-Stedinger plotting-position parity, and twelve analytical penalty cells verify regional parameter/quantile weighting behavior. complete-data Bulletin 17C verification separately verifies six generated-parent recovery cells and all thirteen independently derived complete-data covariance cells. The evidence still does not verify:

- Cohn confidence-interval values or coverage;
- bootstrap covariance;
- uncertain-data variants;
- bootstrap interval coverage; or
- agreement with an objective/generalized-posterior target.

Those are separate claims and require separately authorized, exactly filtered verification methods or independent artifacts. The legacy version 1 Comparison with EMA report evaluates an earlier Bayesian workflow and is not the oracle for the current specialized GMM path.

<a id="chunk-7-six-family-parameterization-crosswalk"></a>

## Six-family parameterization crosswalk
The generated-parent recovery design uses exactly 1,000 complete scalar observations and the fixed
generator seed `12345` for every supported family. The generator constructs the listed Numerics
distribution directly; `Bulletin17CDistribution` exposes the same parameter names and order through
the wrapped distribution. Complete raw-space families enter the GMM moments without transformation.
Log-Normal and Log-Pearson Type III observations are transformed with `log10` before the GMM moment
conditions are evaluated.

| Family | Generator and Numerics order | B17C/GMM order | Coordinate space and convention | Predeclared recovery uncertainty |
|---|---|---|---|---|
| Exponential | `(Xi location, Alpha scale) = (0, 50)` | `(Xi, Alpha)` | Natural response space; positive scale | Numerics method-of-moments `ParameterCovariance(1000, MethodOfMoments)` at the fitted `(Xi, Alpha)` |
| Gamma | `(Theta scale, Kappa shape) = (5, 2)` | `(Theta, Kappa)` | Natural response space; `Theta` is scale and the derived rate is `1/Theta` | Numerics method-of-moments parameter covariance at fitted `(Theta, Kappa)` |
| Normal | `(Mu mean, Sigma standard deviation) = (100, 15)` | `(Mu, Sigma)` | Natural response space; positive standard deviation | Numerics method-of-moments parameter covariance at fitted `(Mu, Sigma)` |
| Pearson Type III | `(Mu mean, Sigma standard deviation, Gamma skew) = (100, 20, 0.5)` | `(Mu, Sigma, Gamma)` | Natural response space; positive `Gamma` is positive/right skew | Parameter covariance for fitted `Mu` and `Sigma`; Numerics method-of-moments Q(0.99) variance for the weak skew direction |
| Log-Normal | `(Mu, Sigma) = (3, 0.5)` | `(Mu, Sigma)` | Base-10 log space; both coordinates describe `log10(X)`, not natural-log or real-space moments | Numerics method-of-moments parameter covariance in fitted base-10 `(Mu, Sigma)` coordinates |
| Log-Pearson Type III | `(Mu, Sigma, Gamma) = (3, 0.5, 0.2)` | `(Mu, Sigma, Gamma)` | Base-10 log space; skew sign is unchanged by the monotone transform and positive `Gamma` is positive/right log-space skew | Parameter covariance for fitted log-space `Mu` and `Sigma`; Numerics method-of-moments Q(0.99) variance in real response space for the weak skew direction |

For each parameter-coordinate check, the generating coordinate must have absolute standardized error
no greater than `1.96` using the fitted method-of-moments covariance at `N=1000`. For the two
predeclared Q(0.99) response checks, the generating quantile must lie inside the fitted response's
central Normal-approximation 95-percent band formed from Numerics `QuantileVariance`. These
acceptance rules are fixed before observing the GMM results; estimator success, finiteness,
or agreement with sample product moments is not the scientific oracle.

The covariance cells use a separate evidence boundary. Approval to use Numerics uncertainty for the
recovery acceptance above does not make Numerics an independent covariance oracle. The independent oracle derives the complete-data just-identified GMM sandwich independently from the first six
central moments and the finite-sample centered-moment Jacobian. In particular, the three-coordinate
Jacobian uses `D[2,0] = -3 * (N / (N - 2)) * Sigma^2`, preserving the B17C second- and third-moment
Bessel factors. The derivation is checked against frozen values generated without RMC.BestFit or
Numerics production code and cites Bulletin 17C version 1.1 and Cohn, Lane, and Stedinger (2001) for
the method-of-moments/EMA uncertainty framework.

<a id="chunk-7-current-results"></a>

## Complete-data covariance and recovery evidence
All six generated-parent recovery identities and all thirteen covariance identities passed current
exact guarded runs on 30 August 2026. The covariance artifact was generated with Python 3.12.13
standard library only and independently cross-checked by the C# analytical evaluator.

The two natural-space Pearson Type III cells initially failed because their fourth-to-sixth central
moments are scale-separated. The shared Numerics positive-definite helper added a trace-scaled ridge
before testing an already-positive-definite matrix, so the largest moment coordinate materially changed
covariance[0,0]. Haden Smith approved correcting the shared contract: the symmetric candidate is now
tested first and returned unchanged when Cholesky accepts it; the existing ridge magnitudes, escalation,
and fallback remain unchanged for rejected candidates. No covariance formula, oracle, tolerance, seed,
or B17C estimator behavior changed. Fresh isolated TRXs at `20260830-094558` and `20260830-094618`
record the two Pearson cells passing; the other eleven covariance cells and all six recoveries passed in
the `20260830-094658` through `20260830-094833` series. The exact method/TRX ledger is in the
[Recorded covariance and recovery runs](bulletin-17c.md#recorded-covariance-and-recovery-runs).

The twelve penalty identities, twelve example/plotting-position identities, and seven selected general
GMM identities also passed fresh exact guarded runs after the shared change. Three discarded wrong-class
attempts produced zero-result TRXs and are not evidence; the corrected
`HirschStedingerPlottingPositionVerificationTests` identities each passed exactly once.

No confidence-interval coverage method was executed. The 56 retired catalog entries in the three coverage
classes remain historical evidence; their source declarations were later removed, as described in
[Retired interval-coverage studies](#retired-interval-coverage-studies).

<a id="phase-7-dispositions---22-august-2026"></a>

## Covariance and bootstrap findings - 22 August 2026
Three Bulletin 17C items from the 21 August 2026 reruns have the following recorded scientific dispositions:

- **TR-085 (GMM covariance, independently closed).** The eigenvalue cap was removed and only the positive-definite floor remained. The historical 13/13 result compared with the same Numerics covariance ecosystem. The 30 August independent covariance study replaced that oracle with an independent analytical sandwich. After the approved shared Numerics correction stopped adding a ridge to already-positive-definite matrices, all 13 current exact methods passed without changing the independent oracle or tolerance.
- **TR-086 (bootstrap re-centring, fixed).** `DataFrame.BootstrapDataFrame` re-centres additive measurement-error distributions by the simulated/original ratio for log-space fits and strictly positive error supports, so MOVE.3-style relative errors keep their relative spread and never cross zero. `UncertainDataBootstrapVerificationTests` 2/2 and the reliability grid 14/14 after the fix.
- **TR-087 (censored coverage frames and initial parameters, fixed).** The coverage fixture now computes plotting positions; `Bulletin17CDistribution` keeps the constraint-based initial values and records a validation warning when the censored-data (ROS) initial estimate is unavailable instead of reporting zero parameters. The censored coverage cells and the `B17CCoverageTests` cells (TR-088) were not rerun, by decision; their later retirement is documented in [Retired interval-coverage studies](#retired-interval-coverage-studies).

The [recorded historical runs](#historical-numerical-results---22-august-2026) retain the outcomes and timings for the two uncertain-data bootstrap checks and thirteen covariance checks.

## Traceability

- Findings: [TR-003](bulletin-17c.md#data-handling-assumptions) and [TR-016 through TR-021](bulletin-17c.md#result-storage-architecture)
- Technical method: [Bulletin 17C analysis](../technical-reference/analysis/bulletin-17c.md)


## Recorded covariance and recovery runs

These nineteen isolated results were recorded on 30 August 2026. Each identifies one method, one result directory, and its recorded outcome; they are historical execution evidence, not a claim of a new run at the current documentation checkpoint. The recovery design and independent covariance oracle are described above.

| Exact recovery identity | Latest isolated result directory | Result |
|---|---|---|
| `B17CSyntheticDataTests.Exponential_GmmRecoversGeneratingParent` | `20260830-094803-..._Exponential_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.Gamma_GmmRecoversGeneratingParent` | `20260830-094809-..._Gamma_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.Normal_GmmRecoversGeneratingParent` | `20260830-094815-..._Normal_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.PearsonTypeIII_GmmRecoversGeneratingParentAndQ99` | `20260830-094821-..._PearsonTypeIII_GmmRecoversGeneratingParentAndQ99` | Passed |
| `B17CSyntheticDataTests.LogNormal_GmmRecoversGeneratingLog10Parent` | `20260830-094827-..._LogNormal_GmmRecoversGeneratingLog10Parent` | Passed |
| `B17CSyntheticDataTests.LogPearsonTypeIII_GmmRecoversGeneratingLog10ParentAndQ99` | `20260830-094833-..._LogPearsonTypeIII_GmmRecoversGeneratingLog10ParentAndQ99` | Passed |

| Exact covariance identity | Latest isolated result directory | Result |
|---|---|---|
| `B17CCovarianceTests.Exponential_Covariance_N25` | `20260830-094658-..._Exponential_Covariance_N25` | Passed |
| `B17CCovarianceTests.Exponential_Covariance_N100` | `20260830-094703-..._Exponential_Covariance_N100` | Passed |
| `B17CCovarianceTests.Gamma_Covariance_N25` | `20260830-094709-..._Gamma_Covariance_N25` | Passed |
| `B17CCovarianceTests.Gamma_Covariance_N100` | `20260830-094714-..._Gamma_Covariance_N100` | Passed |
| `B17CCovarianceTests.Normal_Covariance_N25` | `20260830-094720-..._Normal_Covariance_N25` | Passed |
| `B17CCovarianceTests.Normal_Covariance_N100` | `20260830-094726-..._Normal_Covariance_N100` | Passed |
| `B17CCovarianceTests.PearsonTypeIII_Covariance_N25` | `20260830-094558-..._PearsonTypeIII_Covariance_N25` | Passed |
| `B17CCovarianceTests.PearsonTypeIII_Covariance_N100` | `20260830-094618-..._PearsonTypeIII_Covariance_N100` | Passed |
| `B17CCovarianceTests.LogNormal_Covariance_N25` | `20260830-094733-..._LogNormal_Covariance_N25` | Passed |
| `B17CCovarianceTests.LogNormal_Covariance_N100` | `20260830-094740-..._LogNormal_Covariance_N100` | Passed |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_N25` | `20260830-094746-..._LogPearsonTypeIII_Covariance_N25` | Passed |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_N100` | `20260830-094751-..._LogPearsonTypeIII_Covariance_N100` | Passed |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_Example1` | `20260830-094757-..._LogPearsonTypeIII_Covariance_Example1` | Passed |

The aggregate outcome was **19 passed, 0 failed, 0 skipped**. The frozen covariance artifact is [b17c-gmm-covariance-oracle.json](../../verification/data/bulletin17c/b17c-gmm-covariance-oracle.json); its Python-standard-library generator and hashes are listed in the [verification data manifest](../../verification/data/MANIFEST.md).

## Retired interval-coverage studies

The [retired catalog](verification-catalog-retired.json) preserves 56 coverage-study records from source removed in commit `ae4aaa1` on 9 September 2026: 18 `B17CCoverageTests` records, eight `B17CCensoredCoverageTests` records, and 30 `B17CCohnEtAlCoverageTests` records. They are excluded from the 329-method active inventory. Their model definitions, sample sizes, seeds, oracle descriptions, acceptance rules, execution exclusions, and limitations remain historical records; updated links do not reinstate the deleted methods or promote their results to current coverage claims.

These studies address repeated-sample confidence-interval coverage, a different claim from single-dataset parameter recovery, complete-data covariance identities, or successful bootstrap delivery. The Cohn method is restricted to exact LP3 data without low-outlier flags. Censored, uncertain-data, bootstrap covariance, and broad interval-coverage claims are not established by the seven worked examples or nineteen runs above. Historical coverage results require a separately scoped reproduction; none was rerun during documentation consolidation.

The 30 historical Cohn scenarios cross skew values `-1, -0.5, 0, 0.5, 1`, systematic lengths `Ns=25` or `100`, and historical lengths `Nh=0`, `50`, or `150`. Each used 1,000 replicates, master seed `12345`, nominal coverage `0.90`, and a historical minimum of 800 completed replicates. Completion accounting is not a quantitative coverage oracle.

## Bootstrap repair evidence

Three frozen packages record successive experiments on Example #1 - BCB, with 68 systematic observations, 1,000 bootstrap realizations, seed 12345, and the enabled regional-skew penalty preserved:

- [BFGS and analytical-Jacobian repair](b17c-repair-evidence-20260917/README.md): frozen XML inputs, benchmark, initial solver repair, and residual failures.
- [Cholesky/ridge and outer-iteration diagnosis](b17c-cholesky-evidence-20260917/README.md): exception-removal parity, checked ridge escalation, independent derivative/rounding calculations, and unstable outer fixed points.
- [Objective-rounding repair](bfgs-roundoff-evidence-20260917/README.md): the later bounded gradient check and its recorded effects.

The final recorded replay retained **1,000 accepted bootstrap outputs, 947 outer-converged fits, 53 capped fits, and 77 BFGS fallbacks**. These counts are distinct. Positive-definite returned matrices and accepted outputs do not establish interval coverage or solve the remaining outer iteration problem. The general three-to-five-pass target remains unmet; no tolerance, penalty, seed, fallback policy, or outer-GMM equation was changed to conceal it.


<a id="historical-recovery-results---22-august-2026"></a>

## Historical numerical results - 22 August 2026

These isolated results were recorded at the 22 August 2026 checkpoint. The covariance checks then used the same Numerics covariance ecosystem; the independent sandwich oracle and nineteen runs described above supersede that covariance claim. The two uncertain-data bootstrap results retain their stated stability scope. Timings are recorded wall-clock observations, not current performance guarantees.

| Method | Outcome | Time |
|---|---|---|
| `UncertainDataBootstrapVerificationTests.NormalBootstrap_UncertainObservationsRemainStable` | Passed | 3.6 s |
| `UncertainDataBootstrapVerificationTests.LogPearsonBootstrap_Move3StyleUncertaintyRemainsStable` | Passed | 3.8 s |
| `B17CCovarianceTests.Exponential_Covariance_N25` | Passed | 3.4 s |
| `B17CCovarianceTests.Exponential_Covariance_N100` | Passed | 3.2 s |
| `B17CCovarianceTests.Gamma_Covariance_N25` | Passed | 3.2 s |
| `B17CCovarianceTests.Gamma_Covariance_N100` | Passed | 3.1 s |
| `B17CCovarianceTests.Normal_Covariance_N25` | Passed | 3.3 s |
| `B17CCovarianceTests.Normal_Covariance_N100` | Passed | 3.3 s |
| `B17CCovarianceTests.PearsonTypeIII_Covariance_N25` | Passed | 3.1 s |
| `B17CCovarianceTests.PearsonTypeIII_Covariance_N100` | Passed | 3.2 s |
| `B17CCovarianceTests.LogNormal_Covariance_N25` | Passed | 3.4 s |
| `B17CCovarianceTests.LogNormal_Covariance_N100` | Passed | 3.4 s |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_N25` | Passed | 3.3 s |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_N100` | Passed | 3.2 s |
| `B17CCovarianceTests.LogPearsonTypeIII_Covariance_Example1` | Passed | 3.2 s |
