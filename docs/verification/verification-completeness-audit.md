<!-- verification-status: active-remediation -->

# Verification Completeness and Ownership Audit

## Purpose and authority

This document is the static audit baseline for the RMC.BestFit verification-completeness remediation program. It supplements, but does not rewrite, the historical phase ledgers and focused-run results. The implementation plan is [Verification Completeness Remediation](../superpowers/plans/2026-08-28-verification-completeness-remediation.md).

Haden Smith remains the final technical and numerical authority. This program does not authorize changes to production algorithms, priors, samplers, seeds, likelihoods, convergence policies, numerical defaults, serialization, or public contracts. A verification failure initiates diagnosis and an approval-gated finding; it does not authorize tuning either production behavior or a test tolerance.

## Audit checkpoint

The current static checkpoint is commit `df8ccf4` on branch `documentation-verification-updates`, reviewed 28 August 2026.

| Measure | Current value |
|---|---:|
| Verification C# source files | 99 |
| Files containing `[TestMethod]` or `[DataTestMethod]` | 79 |
| Ordinary `[TestMethod]` declarations | 505 |
| Data-driven method declarations | 1 |
| `[DataRow]` execution units | 30 |
| Total current MSTest execution units | 535 |
| Scientific analysis types in scope | 15 |

No Verification test was executed for this audit. The lightweight namespace/private-XML scan passed. The worktree was clean when the checkpoint was recorded.

The data-driven method is `B17CCohnEtAlCoverageTests.CohnEtAl_LP3_Coverage` with 30 named rows. The current guarded runner resolves only `[TestMethod]` and requires one TRX result, so it cannot execute this method under the exact-method rule. The catalog will record this as an open focused-execution gap. The Bulletin 17C ownership work will convert the historical coverage rows into separately named exact methods that share a private scientific helper solely so the source inventory is unambiguous; those coverage methods remain execution-excluded. The runner will not be broadened to accept multi-result filters.

The 4 August ownership cleanup remains the historical baseline: 1,196 methods were reduced to 435 and 35 deterministic contracts were added to the fast tests. The current count is higher because approved verification and regression work continued after that checkpoint. Consequently, the earlier statement that no mixed-file ownership backlog remained must be re-audited against the present 506 declared methods and 535 execution units.

The Bulletin 17C confidence-interval coverage methods in `B17CCoverageTests`, `B17CCensoredCoverageTests`, and `B17CCohnEtAlCoverageTests` are execution-excluded for this program. Their sources and historical results may be inventoried, ownership-reviewed, and reported as reruns-on-request, but Codex will not execute them. This restriction does not prohibit ordinary fast-test validation or explicitly authorized non-B17C Verification methods.

## Binding verification boundary

A Verification method must have at least one declared analytical, independently implemented, external-package, published/real-source, recovery, or coverage oracle. Estimator invocation, convergence, finiteness, retry counts, result shape, property state, validation, serialization, dispatch, cache behavior, exception behavior, and agreement with another production path are not sufficient by themselves.

Deterministic engineering contracts belong in `RMC.BestFit.Tests`. Fast tests may use inline fixtures or restore estimated state, but must not run an optimizer or MCMC sampler and must not reference Verification `TestData.cs` or `Datasets/`.

## Scientific analysis coverage matrix

The following 15 user-facing analyses and their accompanying models are in scope. Support infrastructure such as `WeightedUnivariateAnalysis`, `BatchAnalysisRunner`, base classes, interfaces, event arguments, and DTOs is fast-test-only and will be recorded as not applicable in the final traceability matrix.

| Analysis | Accompanying model or model family | Existing evidence | Open verification work |
|---|---|---|---|
| `FittingAnalysis` | All 15 univariate distribution families | Published and external fixtures | Add N=1000 end-to-end family recovery and an explicit report section |
| `UnivariateAnalysis` | `UnivariateDistribution` and trend functions | Fifteen stationary N=1000 families; analytical reciprocal identity; 380-cell deterministic parent/trend default matrix; five-cell nonstationary Bayesian family/parameter-role covering array | Migrate and individually rerun the sixteen legacy Normal trend cells before counting them under the shared central-95% rule; a multi-seed calibration study remains separate from single-seed recovery |
| `Bulletin17CAnalysis` | `Bulletin17CDistribution` and six supported families | Worked examples, bootstrap, and coverage infrastructure | Add six N=1000 family recoveries; replace completion-only claims with Cohn or PeakFQ value oracles; retain coverage studies as execution-excluded history |
| `PointProcessAnalysis` | `PointProcessModel` | Analytical likelihoods and N=1000 recovery | Add a compatible external POT oracle; separate likelihood invariance from recovery; review the N=4000 prior characterization |
| `CompetingRiskAnalysis` | `CompetingRisksModel` | Four dependency-aware analytical simulations plus four N=1000 recovery cells over three cause-balanced dog-leg fixtures | Preserve full-likelihood effective-information acceptance; retain the former maximum fixture as a local-mode example and the Bayesian maximum collapsed-prior mode as a documented limitation; do not add correlated Bayesian MCMC |
| `MixtureAnalysis` | `MixtureModel` | N=1000 recovery and same-ecosystem Numerics parity | Add an independent mixture-package oracle and consistent label-identified recovery |
| `CompositeAnalysis` | Composite mixture, competing-risk, and model-average outputs | Analytical composition and posterior resampling | Rename non-recovery cells and add N=1000 end-to-end predictive recovery through fitted component analyses |
| `BivariateAnalysis` | `BivariateDistribution` and seven copula families | Six-family external oracles | Raise recovery from N=100 to N=1000; add Student-t external parity and common acceptance |
| `CoincidentFrequencyAnalysis` | Coincident response surface | Closed-form Normal-sum oracle | Add N=1000 nonlinear response recovery against independent numerical integration or simulation |
| `RatingCurveAnalysis` | `RatingCurve` | N=1000 recovery and SciPy artifacts | Replace 5-50% recovery bands; move default-bound contracts fast; retain only parameterization-compatible external comparisons |
| `ARAnalysis` | `AutoRegressive` | R parity and N=1000 recovery | Apply the common Bayesian/frequentist rule and add a dedicated report section |
| `MAAnalysis` | `MovingAverage` | R parity and N=1000 recovery | Consolidate legacy acceptance and add a dedicated report section |
| `ARIMAAnalysis` | `ARIMA` | R parity and N=1000 recovery | Consolidate acceptance and document the differencing/parameter-order crosswalk |
| `ARIMAXAnalysis` | `ARIMAX` | R/xreg parity and N=1000 recovery | Consolidate acceptance and document covariate alignment |
| `SpatialGEVAnalysis` | `SpatialGEV`, `BasicExponential`, `PoweredExponential`, and `Spherical` | Likelihood, GP, simulation, recovery, and external artifacts | Use N=1000 row/year vectors; complete all correlation families; replace same-path cross-validation, prediction, and uncertainty contracts with independent evidence |

## Recovery design rule

Every genuine synthetic recovery method will use exactly 1,000 generated observational units:

- 1,000 scalar observations for univariate, mixture, competing-risk, and Bulletin 17C models;
- 1,000 paired observations for bivariate models;
- 1,000 stage-discharge pairs for rating curves;
- 1,000 post-burn-in observations for time-series models;
- 1,000 response/covariate rows for nonstationary models; and
- 1,000 row/year vectors across the full site network for spatial models.

Coverage-study record lengths, bootstrap replicate counts, quadrature grids, and high-volume simulation-calibration draws are not recovery sample sizes. They retain their separately justified designs and must not be labeled recovery.

Frequentist recovery requires the parent value inside a predeclared 95% confidence/profile interval, or absolute standardized error no greater than 1.96 when a defensible standard error is the available uncertainty measure. Bayesian recovery requires the parent inside a central 95% posterior interval computed from retained draws, R-hat below 1.10, and effective sample size of at least 100 for every monitored coordinate. This is a test-only acceptance policy and does not change production interval or sampler defaults.

A secondary 5% point or curve criterion applies only where the corresponding 95% uncertainty band is already narrower than 5% of a nonzero parent value. Near-zero, boundary, weakly identified, label-switching, and non-identifiable cases use standardized error or identified predictive quantities such as CDFs, quantiles, and response curves. External-package parity keeps source-specific numerical tolerances.

## Confirmed ownership candidates

These are the current method groups requiring move, removal, split, or replacement. Final disposition is method-level; a mixed source file is not moved wholesale when it also contains a scientific oracle.

| Verification source | Required disposition |
|---|---|
| `ModelEstimation/MLEIntegrationTests.cs` | Retain genuine family recovery; move consistency/small-sample engineering contracts fast; remove the non-general assertion that a log-likelihood must be negative |
| `ModelEstimation/GeneralizedMethodOfMomentsRecoveryTests.cs` | Move optimizer selection, strategy state, and success-only contracts fast; replace remaining cells with analytical N=1000 GMM recovery |
| `ModelEstimation/ProfileLikelihoodGridPointFailureTests.cs` | Move NaN/throw/failure policy fast; retain only independently derived profile-likelihood evidence |
| `ModelEstimation/JointPriorSamplingVerificationTests.cs` | Retain the analytical independent-marginal characterization as evidence for the accepted joint-prior limitation; split or remove the same-production-path fitness equality because it is an engineering contract, not part of that oracle |
| `ModelEstimation/BayesianAnalysisRecoveryTests.cs` | Replace the qualitative prior-shift check with the exact Normal-Normal conjugate result or move it fast; normalize the remaining N=100 recovery |
| `Univariate/Bulletin17CTests/B17CLowOutlierSetterFitTests.cs` | Extract deterministic setter/recalculation equivalence fast without carrying its optimizer run into the fast suite |
| `Univariate/Bulletin17CTests/B17CBootstrapRefitReliabilityTests.cs` | Move retry/substitution/accounting behavior fast; retain only independently supported bootstrap accuracy evidence |
| `Univariate/Bulletin17CTests/B17CCohnEtAlCoverageTests.cs` | Move accounting behavior fast; preserve the coverage design as execution-excluded history and add separate published value verification |
| `RatingCurve/RatingCurveContinuityVerificationTests.cs` | Retain analytical continuity; move default-bound and setter-specific behavior fast |
| `SpatialExtremes/SpatialGEVCrossValidationVerificationTests.cs` | Move fold status/accounting fast; replace same-production-path comparisons with independent fold predictions |
| `SpatialExtremes/SpatialGEVUncertaintyMethodVerificationTests.cs` | Move dispatch and selected-method state fast; add independent Godambe, block-bootstrap, and VIF evidence |
| `SpatialExtremes/SpatialGEVPredictionVerificationTests.cs` | Move same-path aggregation fast; retain or add independent kriging/prediction evidence |
| `Univariate/CompositeTests/CompositeOracleVerificationTests*.cs` | Retain analytical and resampling cells; add genuine predictive recovery from N=1000 component samples |
| `Univariate/PointProcessTests/PointProcessRecoveryTests.OrderInvariance.cs` | Retain its independent annual-maximum likelihood oracle but relocate it to a likelihood-oracle class because its N=300 permutation is not recovery |
| `extract_tests.py` | Remove or replace with the maintained catalog validator under `scripts/` |

## Documentation and report gaps

- The Verification report checkpoint and method totals are stale relative to this audit.
- `test-inventory.md` contains stale counts and still labels some Verification groups as regression sets.
- `book-order.txt` omits the existing `report/estimation-diagnostics.md` chapter.
- Fitting, Univariate, Bulletin 17C, AR, MA, ARIMA, and ARIMAX do not each have a consistently structured analysis section.
- Several report statements equate numerical completion with verification.
- The codebase contains 185 generic XML/comment patterns identified by `rg -n "Verifies <c>|This helper keeps fixture setup local|Tests Bayesian|Tests MLE" src/RMC.BestFit.Verification -g "*.cs"`, including tautological method summaries and repeated fixture boilerplate. Separate Arrange/Act/Assert comments also require a qualitative why-versus-what review.

The final report section for each analysis will state the scientific claim, model and parameterization, theoretical oracle, external or published oracle, N=1000 recovery design, seed, acceptance rule, result, limitations, and artifact provenance.

## Completion conditions

The remediation is complete only when the machine-readable catalog accounts for every Verification method and data-driven execution unit, all open catalog gaps are closed or explicitly approved as limitations, every recovery entry records N=1000, every scientific analysis has a report section, the fast ownership gates pass, and every newly claimed executable Verification result has been run as one explicitly authorized fully qualified method through `scripts/run-verification-test.ps1`. Bulletin 17C confidence-interval coverage results remain historical or reruns-on-request and are not executed by Codex.

The full Verification project is never executed automatically.
