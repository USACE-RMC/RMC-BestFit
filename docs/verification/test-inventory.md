# Verification Test Inventory

## Ownership rule

Fast behavior, validation, serialization, property, state, event, exception, and regression tests belong in `RMC.BestFit.Tests`. Numerical verification remains only when a test uses an analytical, external, published, independently implemented, recovery, or coverage oracle.

## Initial migration - 24 July 2026

| Verification source | Disposition | Fast-test destination or coverage |
|---|---|---|
| `ModelEstimation/InfluenceDiagnosticsTests.cs` | Removed as unit/DTO coverage | Existing `Diagnostics/InfluenceDiagnosticsTests.cs` |
| `ModelEstimation/PointwiseLogLikelihoodTests.cs` | Removed as decomposition/unit coverage | Existing univariate, bivariate, time-series, mixture, point-process, and spatial unit tests |
| `ModelEstimation/PredictiveChecksTests.cs` | Removed as unit/regression coverage | Existing expanded prior/posterior predictive and result DTO unit tests |
| `ModelEstimation/FitVarianceInfluenceTests.cs::Test_Serialization_RoundTrip` | Removed as unit/DTO coverage | Existing `Diagnostics/LeverageDiagnosticsTests.cs` XML round-trip coverage |
| `DistributionFitting/FittingAnalysisTests.cs` | Split | Deterministic state/event/regression cases moved to `FittingAnalysisRegressionTests`; published-data comparisons retained |


Four legacy assertions were not retained: a probability-ordinate mutation test contradicted the established no-refit contract; generic large-sample and distribution-success counts had no oracle; and the outlier smoke test reproduced TR-010 (IsEstimated true with zero successful candidates). TR-010 is fixed, and `FittingAnalysisRegressionTests` now covers all-candidate failure and partial success.

## Comprehensive cleanup - 4 August 2026

Every C# source file in `RMC.BestFit.Verification` was reviewed at method level against the fast core tests. The cleanup reduced Verification from 1,196 to 435 methods and added 35 missing deterministic contracts to `RMC.BestFit.Tests`. The 761 methods removed from Verification were duplicate unit coverage, estimator smoke checks without an independent oracle, or assertions already subsumed by stronger fast or verification evidence.

| Area | Original Verification methods | Retained Verification methods | Added fast contracts | Removed from Verification |
|---|---:|---:|---:|---:|
| Bivariate | 41 | 22 | 2 | 19 |
| Distribution fitting | 45 | 45 | 0 | 0 |
| Model estimation | 181 | 61 | 21 | 120 |
| Rating curve | 117 | 20 | 10 | 97 |
| Spatial extremes | 205 | 9 | 2 | 196 |
| Time series | 387 | 80 | 0 | 307 |
| Univariate and Bulletin 17C | 220 | 198 | 0 | 22 |
| **Total** | **1,196** | **435** | **35** | **761** |

Four files were removed in full: `Bivariate/BivariateAnalysisTests.cs`, `Bivariate/BivariateDistributionTests.cs`, `ModelEstimation/FitVarianceInfluenceTests.cs`, and `Univariate/UnivariateDistributionTests.cs`. Mixed files were replaced by recovery- or oracle-specific classes for time-series models, rating curves, spatial GEV, Bayesian analysis, GMM, MAP, Profile Q, MCMC diagnostics, PSIS-LOO, and Log10-Normal influence. The 296 exact duplicates between time-series model and analysis suites were removed together with transform, constructor, property, and covariate-extension unit checks. No mixed-file ownership backlog remains.

The fast additions use small inline fixtures and do not reference `TestData.cs` or `Datasets/`. They cover cancellation and validation, RatingCurve calculations and XML contracts, spatial large-matrix/cancellation behavior, PSIS caching, GMM state and guard behavior, and MAP reset/argument guards. Twelve candidate moves were not duplicated because stronger semantic fast tests already existed; three ineffective fresh-state assertions were deleted rather than preserved.

| Affected Verification source group | Final disposition |
|---|---|
| `BivariateAnalysisTests`, `BivariateDistributionTests` | Removed; two missing cancellation/validation contracts added fast and the remaining behavior was already covered |
| `BivariateDistributionMLETests` | Retained 12 MPL/IFM parameter-recovery methods; removed the missing-plotting-position finite-result smoke method |
| Bayesian, GMM, MAP, and Profile Q mixed files | Replaced by `*RecoveryTests`; deterministic state, validation, clone, default, and argument guards moved or consolidated fast |
| PSIS-LOO, MCMC diagnostics, and Log10-Normal influence mixed files | Replaced by `*VerificationTests`; caching/report/obsolete-API contracts moved or consolidated fast |
| Rating-curve model and analysis files | Replaced by 10 MLE and 10 Bayesian recovery methods; 10 missing model contracts added fast |
| Spatial GEV model and analysis files | Replaced by two MLE and seven Bayesian recovery methods; large-matrix and cancellation contracts added fast |
| Time-series model files | Replaced by 49 MLE/R-recovery methods; 307 unit, transform-smoke, covariate-extension, and exact analysis-suite duplicate methods removed |
| `UnivariateDistributionTests` and `FitVarianceInfluenceTests` | Removed because fast distribution/diagnostic suites and stronger source-backed verification already cover the contracts |

## External model-comparison oracles and PSIS correction - 25-26 July 2026

| Verification method | External oracle | Status |
|---|---|---|
| `ModelEstimation/InformationCriterionOracleTests.cs::DIC_MatchesRBayesianToolsOracle` | R `BayesianTools::DIC` 0.1.9 | Passed - exact focused method |
| `ModelEstimation/InformationCriterionOracleTests.cs::WAIC_MatchesRLooOracle` | R `loo::waic` 2.10.0 | Passed - exact focused method |
| `ModelEstimation/PsisLooOracleVerificationTests.cs::RlooOracle_InternalIdentitiesAreConsistent` | R `loo::psis` and `loo::loo` 2.10.0 | Passed - exact focused method |
| `ModelEstimation/PsisLooOracleVerificationTests.cs::PSISLOO_MatchesRLooOracle` | R `loo::loo` 2.10.0 | Passed - aggregate, pointwise, Pareto-k, and smoothed-weight parity |
| `ModelEstimation/PsisLooOracleVerificationTests.cs::PsisTailRegimes_MatchRLooOracle` | R `loo::psis` 2.10.0, including bounded through nonfinite-mean and degenerate tails | Passed - every weight, Pareto k, and effective sample size |
| `ModelEstimation/PsisLooOracleVerificationTests.cs::ParetoInfluence_UsesRloo210DiagnosticThreshold` | R `loo` 2.10.0 sample-size diagnostic threshold | Passed - classification and XML round-trip |
| `ModelEstimation/PsisCachingContractTests.cs::DefaultInformationCriteria_EvaluatePointwiseLikelihoodOnce` | Deterministic call-count model in the fast core project | Passed - one evaluation per retained draw |
| `ModelEstimation/PsisCachingContractTests.cs::InfluenceDiagnostics_ReuseCachedPointwiseLikelihood` | Deterministic call-count model in the fast core project | Passed - influence reuses pointwise LOO cache |

The DIC/WAIC methods consume the committed [model-comparison oracle](../../verification/data/model-estimation/model-comparison-oracle.json). The PSIS/LOO methods consume the committed [PSIS-LOO oracle](../../verification/data/model-estimation/psis-loo-oracle.json). R is used only to generate the versioned artifacts and is not required when the C# verification tests run.

## TR-023, TR-027, and TR-028 verification - 26 July 2026

| Test method | Test project | Oracle or failure contract | Status |
|---|---|---|---|
| `ProfileLikelihoodFindingTests.MLE_ProfileLikelihood_MatchesRTrueProfile` | Verification | R `bbmle` 1.0.25.1 plus closed-form nuisance reoptimization | Passed - exact focused method |
| `ProfileLikelihoodFindingTests.MAP_ProfileLikelihood_WithFlatPriors_MatchesRTrueProfile` | Verification | R `bbmle` profile plus constant flat-prior shift | Passed - exact focused method |
| `ProfileLikelihoodFindingTests.MAP_ProfileLikelihood_WithInformativePrior_ProfilesFullPosteriorKernel` | Verification | Closed-form informative-prior nuisance optimum | Passed - exact focused method |
| `CovarianceFailureStatusTests.MaximumLikelihood_SingularHessian_ReportsFailureAndThrows` | Fast unit | Singular Hessian explicit failure contract | Passed |
| `CovarianceFailureStatusTests.MaximumAPosteriori_SingularHessian_ReportsFailureAndThrows` | Fast unit | Singular Hessian explicit failure contract | Passed |
| `CovarianceFailureStatusTests.GeneralizedMethodOfMoments_MomentFailure_ReportsFailureAndThrows` | Fast unit | Forced exception through public `Try` and throwing getter | Passed |
| `CovarianceFailureStatusTests.MaximumLikelihood_WellConditionedHessian_ReportsAvailable` | Fast unit | Unmodified finite positive-definite covariance | Passed |
| `CovarianceFailureStatusTests.MaximumLikelihood_NonsymmetricCandidate_ReportsRegularized` | Fast unit | Visible positive-definite covariance repair | Passed |
| `CovarianceFailureStatusTests.CovarianceComputationStatus_ValuesAreStable` | Fast unit | Stable public enum values | Passed |
| `JointPriorSamplingFindingTests.SampleFromPriors_SoftJointPrior_CurrentlyDrawsIndependentMarginals` | Verification | Analytical independent marginals with a soft coupled prior term | Passed - exact focused method |

The profile tests consume the committed [profile-likelihood oracle](../../verification/data/model-estimation/profile-likelihood-oracle.json); neither R nor Python is required at C# test runtime. The six TR-027 methods ran within the safe fast unit project. The four verification methods in this section were run individually through the exact-method script; the full Verification project was not executed.

## TR-026, TR-032, and TR-034 verification - 26 July 2026

| Test method | Oracle or traced contract | Status |
|---|---|---|
| `GmmSpecificationFindingTests.HansenJ_MatchesRGmmSelectedWeightStatistic` | R `gmm::specTest` 1.9.1 and selected-weight objective identity | Passed - exact focused parameter/objective/J/p-value parity |
| `GmmSpecificationFindingTests.OveridentifiedOneStep_MatchesRGmmFixedWeightOracle` | R `gmm` 1.9.1 fixed positive-definite weighting matrix | Passed - exact focused parameter/objective parity and `NaN` Hansen scope |
| `GmmSpecificationFindingTests.OveridentifiedTwoStepSandwichCovariance_MatchesRGmmOracle` | R `gmm` 1.9.1 `vcov()` plus analytical centered IID sandwich | Passed - exact focused covariance parity |
| `GmmSpecificationFindingTests.OveridentifiedFixedWeightSandwichCovariance_MatchesRGmmOracle` | R `gmm` 1.9.1 arbitrary-fixed-weight IID sandwich plus analytical reconstruction | Passed - exact focused covariance parity |
| `GeneralizedMethodOfMomentsExpandedTests.LegacyInfluenceDiagnosticsOverloads_AreObsoleteCompatibilityApis` | Fast reflection contract for the obsolete compatibility overloads | Passed - fast core project |

The first four methods consume the committed [GMM specification oracle](../../verification/data/model-estimation/gmm-specification-oracle.json), generated from the locked R environment. R is not required at C# runtime. Those four methods were run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the compatibility contract runs in the fast core project. The full Verification project was not executed.

## Numerics MCMC verification - 26 July 2026

| Test method | Test project | External contract or traced behavior | Status |
|---|---|---|---|
| `Test_MCMCSamplerDiagnostics.ARWMH_RejectedWarmupTransitionsEnterCovariance` | Numerics | Haario Adaptive Metropolis realized-chain covariance | Passed - focused .NET 10 method |
| `Test_MCMCSamplerDiagnostics.ARWMH_RejectedPostWarmupTransitionsContinueEnteringCovariance` | Numerics | Original continual-adaptation schedule with complete realized states | Passed - focused .NET 10 method |
| `Test_MCMCSamplerDiagnostics.NUTS_DiagnosticArraysAreNonNullAndEmptyBeforeSampling` | Numerics | Non-null empty live-sampler NUTS diagnostic arrays before initialization | Passed - focused .NET 10 method |
| `Test_MCMCSamplerDiagnostics.NUTS_AcceptanceContractsRemainSeparatedAndResultsPersistHamiltonianRates` | Numerics | Generic transition ratio, separate Hamiltonian acceptance, existing result-field transfer, and stale-JSON compatibility | Passed - focused .NET 10 method |
| `Test_MCMCSamplerDiagnostics.MCMCResults_RetainsBaselineNullabilityAndOmitsNutsDiagnostics` | Numerics | Baseline result nullability and absence of NUTS-specific persisted properties | Passed - focused .NET 10 method |
| `Test_MCMCSamplerDiagnostics.NUTS_EnergyBayesianFractionOfMissingInformationMatchesStanFormula` | Numerics | Stan E-BFMI identity on the live NUTS sampler | Passed - focused .NET 10 method |
| `Test_MCMCInitialization.NutsInitializationUsesConfiguredGradientAndReducesLikelihoodWork` | Numerics | Configured analytic gradient used by reasonable-step-size initialization | Passed - focused .NET 10 method |
| `Test_MCMCDiagnostics.Test_ModernDiagnostics_MatchRPosteriorReference` | Numerics | R `posterior` 1.7.0 rank-normalized R-hat and conservative bulk/tail ESS | Passed - focused .NET 10 method |
| `Test_MCMCDiagnostics.Test_GelmanRubin_FoldedRanksDetectScaleMismatch` | Numerics | R `posterior` 1.7.0 folded rank-normalized R-hat | Passed - focused .NET 10 method |
| `Test_MCMCDiagnostics.Test_ModernDiagnostics_EdgeCases` | Numerics | Constants, insufficient input, and chain permutation | Passed - focused .NET 10 method |
| `McmcNumericalVerificationTests.RankNormalizedRhat_MatchesRPosteriorOracle` | BestFit Verification | Committed R `posterior` 1.7.0 oracle across nine fixtures | Passed - exact focused method |
| `McmcNumericalVerificationTests.ConservativeEss_MatchesRPosteriorBulkAndTailOracle` | BestFit Verification | Committed R bulk/lower-tail/upper-tail ESS and conservative minimum | Passed - exact focused method |
| `McmcNumericalVerificationTests.Arwmh_BestFitWiringRecordsEveryRealizedStateInAdaptiveCovariance` | BestFit Verification | Production sampler setup and complete per-chain covariance counts | Passed - exact focused method |
| `BayesianAnalysisReportTests.GenerateReport_NutsAcceptance_ReportsHamiltonianRatesOnly` | BestFit fast core tests | Hamiltonian result transfer and acceptance-only BestFit report | Passed - fast core project |
| `McmcNumericalVerificationTests.Nuts_BestFitNumericalGradientMatchesPosteriorGradient` | BestFit Verification | Bound-aware finite differences of the complete coupled-prior posterior | Passed - exact focused method |

The four BestFit Verification methods were run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the report contract runs in the fast core project, and the full Verification project was not executed. The Numerics methods passed by exact fully qualified filter on the .NET 10 target, and the normalized complete Numerics .NET 10 Release project records all 2,024 tests passing with zero failures. The TR-029 BestFit methods consume the committed [MCMC diagnostics oracle](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json), so C# tests require no R or Python runtime. Fast report tests `GenerateReport_Rhat1005_PassesModernThreshold` and `GenerateReport_Rhat102_WarnsAtModernThreshold` verify the 1.01 readiness rule without changing the concise `R-hat` label.

## TR-018/TR-019 Bulletin 17C bootstrap refit reliability - 28 July 2026

| Test method | Test project | Traced contract | Status |
|---|---|---|---|
| `NonparametricEmpiricalTests.GetNonparametricMomentsWithLowOutlierMidpoints_LogScaleMatchesExplicitPseudoSample` | Fast unit | Low outliers use bounded measurement-scale midpoints before log transformation | Passed |
| `Bulletin17CDistributionTests.GetRankedBootstrapInitialValues_CensoredSample_ReturnsObjectiveOrderedCandidates` | Fast unit | Finite, valid, bounded, distinct starts ranked by the first-pass penalized GMM objective | Passed |
| `Bulletin17CDistributionTests.GetRankedBootstrapInitialValues_WrongParentDimension_Throws` | Fast unit | Candidate API dimension contract | Passed |
| `Bulletin17CAnalysisTests.RepairPivotParametersToBounds_InvalidComponents_RepairsInPlace` | Fast unit | Non-finite and out-of-bound inverse-linked draws are repaired inside model bounds | Passed |
| `Bulletin17CAnalysisTests.RepairPivotParametersToBounds_ValidComponents_RemainsUnchanged` | Fast unit | Valid pivotal parameter vectors are not altered | Passed |
| `B17CBootstrapRefitReliabilityTests.Example1_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 1 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example1_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 1 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example2_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 2 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example2_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 2 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example3_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 3 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example3_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 3 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example4_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 4 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example4_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 4 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example5_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 5 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example5_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 5 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example6_OrdinaryBootstrap_ThousandRefits` | BestFit Verification | Example 6 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example6_PivotalBootstrap_ThousandRefits` | BestFit Verification | Example 6 bias-corrected pivotal bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example7_OrdinaryBootstrap_FiveHundredRefits` | BestFit Verification | Highly censored Example 7 ordinary bootstrap | Passed - exact focused method |
| `B17CBootstrapRefitReliabilityTests.Example7_PivotalBootstrap_FiveHundredRefits` | BestFit Verification | Highly censored Example 7 bias-corrected pivotal bootstrap | Passed - exact focused method |

| Example | Method | Attempted realizations | Outer retries | Mahalanobis candidate rejections | Optimizer fallbacks | Parent substitutions | First-chance exceptions | Finite outputs |
|---|---|---:|---:|---:|---:|---:|---:|---:|
| 1 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 1 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 2 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 2 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 3 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 3 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 4 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 4 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 5 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 5 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 6 | Ordinary | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 6 | Pivotal | 1,000 | 0 | 0 | 0 | 0 | 0 | 1,000 |
| 7 | Ordinary | 500 | 0 | 0 | 0 | 0 | 0 | 500 |
| 7 | Pivotal | 500 | 0 | 0 | 0 | 0 | 0 | 500 |

| Example | Method | Function evaluations | Max-evaluation candidate statuses | Phase-one time | Test duration |
|---|---|---:|---:|---:|---:|
| 1 | Ordinary | 10,547 | 0 | 0.364 s | 1.417 s |
| 1 | Pivotal | 10,530 | 0 | 0.244 s | 0.951 s |
| 2 | Ordinary | 100,823 | 2 | 13.556 s | 14.312 s |
| 2 | Pivotal | 75,331 | 2 | 9.849 s | 10.478 s |
| 3 | Ordinary | 223,918 | 23 | 29.698 s | 30.288 s |
| 3 | Pivotal | 229,566 | 26 | 27.904 s | 28.481 s |
| 4 | Ordinary | 39,476 | 0 | 9.182 s | 9.913 s |
| 4 | Pivotal | 44,211 | 0 | 14.059 s | 14.824 s |
| 5 | Ordinary | 78,925 | 3 | 6.996 s | 7.666 s |
| 5 | Pivotal | 85,720 | 4 | 22.596 s | 23.263 s |
| 6 | Ordinary | 173,492 | 13 | 15.941 s | 16.649 s |
| 6 | Pivotal | 152,415 | 12 | 13.827 s | 14.605 s |
| 7 | Ordinary | 226,375 | 31 | 74.301 s | 75.124 s |
| 7 | Pivotal | 205,393 | 24 | 90.221 s | 91.094 s |

Each cell was run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the full Verification project was not executed. The final unguarded seeded sweep produced 13,000 finite outputs from exactly 13,000 attempted realizations, with zero outer retries, zero Mahalanobis rejections, zero optimizer fallbacks, zero parent substitutions, zero failed or uninitialized GMM candidates, and zero first-chance exceptions from Numerics or RMC.BestFit. The harness now asserts this direct-refit contract.

The preceding guarded diagnostic sweep showed that every remaining outer retry was caused by the Mahalanobis filter rejecting a finite converged fit. One apparent Example 3 solver retry was traced to `MaximumFunctionEvaluationsReached` on the final inner optimization pass even though iterative GMM had `ConvergedWithinTolerance=true` and an objective between approximately `1.8E-11` and `2.7E-08`. Bootstrap refits now honor the GMM contract: a finite estimate is accepted when the inner status is `Success` or iterative GMM confirms convergence. In the final sweep, 140 candidates carried the maximum-evaluation status; all were confirmed converged and accepted. Removing the obsolete Mahalanobis rejection then eliminated all retries without exposing malformed fits.

Pivotal phase three keeps the Yeo-Johnson links. After inverse linking, non-finite or out-of-bound components are repaired just inside the model's existing lower and upper bounds and revalidated without exception-as-control-flow. Example 5 pivotal exercised this repair once at replicate 695. Location-link fitting used the documented identity fallback in Examples 2, 5, 6, and 7 when the Yeo-Johnson optimizer reached its boundary; Examples 2 and 6 also reported positive-definite covariance regularization. None of these diagnostics caused an exception, retry, or output substitution.

The 1-2 second expectation holds only for Example 1. Examples 2 through 7 perform genuine censored-data GMM work, with highly censored Example 7 taking approximately 74 seconds ordinary and 90 seconds pivotal for 500 outputs. The parent-fit fallback remains only to guarantee configured downstream output length and remains a hard verification failure if exercised. The tests stay `[DoNotParallelize]` because first-chance exception and Trace listeners are process-wide.

## TR-020/TR-021 Bulletin 17C scope and formal-example verification - 28 July 2026

Fast TR-020 coverage was run through the Core unit project. The formal TR-021 methods were then executed one at a time through `scripts/run-verification-test.ps1`; each invocation source-resolved one fully qualified method and produced exactly one TRX result.

| Test method | Test project | Traced contract | Status |
|---|---|---|---|
| `Bulletin17CAnalysisTests.ComputeCohnStyleConfidenceIntervals_Lp3ExactDataBeforeEstimation_ReturnsNull` | Fast unit | Exact-data LP3 retains the unestimated null contract | Passed |
| `Bulletin17CAnalysisTests.ComputeCohnStyleConfidenceIntervals_NonLp3Family_ThrowsNotSupported` | Fast unit | Exponential, Gamma, Log-Normal, Normal, and Pearson III are rejected before LP3 transformations | Passed - 5 data rows |
| `Bulletin17CAnalysisTests.ComputeCohnStyleConfidenceIntervals_Lp3CensoredOrUncertainData_ThrowsNotSupported` | Fast unit | Low outliers, uncertain observations, interval censoring, and threshold censoring are rejected | Passed - 4 conditions |
| `Bulletin17CAnalysisTests.ComputeAsymptoticQuantileVariance_UnsupportedScope_ReturnsNull` | Fast unit | Report-side asymptotic variance returns no values outside exact-data LP3 | Passed |
| `B17CExampleTests.Test_Example1` | BestFit Verification | Moose River systematic-record LP3 mean, standard deviation, and skew | Passed - 0.832 s |
| `B17CExampleTests.Test_Example2` | BestFit Verification | Orestimba Creek low-outlier/zero-flow LP3 parameters | Passed - 0.402 s |
| `B17CExampleTests.Test_Example3` | BestFit Verification | Back Creek broken-record/threshold LP3 parameters | Passed - 0.425 s |
| `B17CExampleTests.Test_Example4` | BestFit Verification | Arkansas River historical/interval/threshold LP3 parameters | Passed - 0.471 s |
| `B17CExampleTests.Test_Example5` | BestFit Verification | Bear Creek crest-stage variable-threshold LP3 parameters | Passed - 0.333 s |
| `B17CExampleTests.Test_Example6` | BestFit Verification | Santa Cruz historical/low-outlier LP3 parameters | Passed - 0.598 s |
| `B17CExampleTests.Test_Example7` | BestFit Verification | American River paleoflood/threshold LP3 parameters | Passed - 0.728 s |

Formal-example aggregate: **7 passed, 0 failed, 0 skipped**. Every method asserted `gmm.IsEstimated` and compared all three current LP3 model parameters with the published fixture values at absolute tolerance `1E-3`, for 21 parameter comparisons. Every focused Verification build completed with zero warnings and zero errors. The complete Verification project was not run.

The result directories are under `TestResults/VerificationFocused/20260728-140241-*Test_Example1` through `20260728-140402-*Test_Example7`. They are local runner output rather than committed oracle artifacts; the durable result and target table is recorded in [Bulletin 17C Verification](bulletin-17c.md#formal-worked-example-parameter-parity).

These formal methods verify worked-example point-estimate parity. They do not verify Cohn interval values, asymptotic or bootstrap covariance, uncertain-data variants, PeakFQ diagnostics, penalty sensitivity, or coverage. Cohn numerical verification remains deferred.

## Phase 4 mixture closeout - 31 July 2026

The original six methods generate the parent sample through
`MixtureModel.GenerateRandomValues(1000, 12345)`. All six have prior guarded results. The public
EM estimator is unchanged, so the three parity results remain current. The three Bayesian results
predate EM-seeded MAP initialization and require focused reruns before they support the changed
initialization path.

| Test method | Oracle or recovery contract | Status |
|---|---|---|
| `MixtureRecoveryTests.NormalMixture2D_Recovery_Parity` | BestFit production generator, Numerics/BestFit likelihood and EM parity, two-component parent recovery | Passed - 1.426 s |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_Recovery_Parity` | BestFit positive-hurdle generator, Numerics/BestFit likelihood and EM parity, atom and parent recovery | Passed - 1.852 s |
| `MixtureRecoveryTests.NormalMixture3D_Recovery_Parity` | BestFit production generator, Numerics/BestFit likelihood and EM parity, three-component parent recovery | Passed - 3.247 s |
| `MixtureRecoveryTests.NormalMixture2D_BayesianRecovery` | Seeded DEMCzs posterior-mode recovery with split R-hat and ESS acceptance | Ready - focused rerun; prior result 9.589 s |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_BayesianRecovery` | Seeded positive-hurdle DEMCzs recovery, binomial atom bound, split R-hat, and ESS | Ready - focused rerun; prior result 21.346 s |
| `MixtureRecoveryTests.NormalMixture3D_BayesianRecovery` | Seeded three-component DEMCzs posterior-mode recovery with label sorting, split R-hat, and ESS | Ready - focused rerun; prior result 12.761 s |
| `MixturePriorAwareInitializationVerificationTests.InformativePrior_EmSeededMapInitialization_UsesFullPosterior` | EM start, informative-prior displacement, nondecreasing posterior, MAP covariance, and full-posterior population fitness | Ready - focused run |

The parity methods retain pre-fit tolerance `1E-10`, cross-engine fitted tolerance `1E-8`, and
absolute parent-recovery tolerance `0.1`. The Bayesian configurations, seeds, and acceptance gates
remain unchanged. The new informative-prior method runs EM, local MAP refinement, covariance, and
population construction without MCMC. Neither it nor the three affected Bayesian methods was run
during implementation, and the full Verification project was not run.

## Phase 4 point-process backcheck - 31 July 2026

All ten scoped methods were run separately through `scripts/run-verification-test.ps1`; each invocation resolved one exact source method and produced one TRX. All ten pass. The guarded build remained warning- and error-free.

The three accepted Bayesian recovery fixtures were rerun with 1,000 observations and the untouched `BayesianAnalysis` defaults. Calendar-year uniform recovery passed in 35.690 s, nonseasonal production recovery passed in 8.450 s, and seasonal production recovery passed in 36.309 s. The default configuration eliminated the former seasonal second-Kappa miss.

The original water-year recovery cell changed the block-day changepoints from calendar `170/350` to `80/260`, so it did not isolate the effect of changing the year origin. The corrected cell holds `K1/K2` fixed, verifies identical magnitudes and block days, an exact 92-day date shift, and parent data log-likelihood parity at `1E-10`, then passes with default DEMCzs in 33.897 s.

The seasonal mixed-likelihood cell initially revealed an all-above threshold fixture inconsistent with `ProcessThresholdSeries`. Its corrected three-year fixture asserts one effective below/two above observations and derives the independent threshold oracle from those processed counts; the rerun passed at unchanged tolerance `2E-7`. No production point-process formula, sampler default, prior, or numerical acceptance setting changed. The full Verification project was not run.

## Phase 4 competing-risk and composite closeout - 3 August 2026

The four TR-012 methods generate 40,000 observations with seed 24681357 through the production `CompetingRisksModel.GenerateRandomValues` path. Each was run separately through `scripts/run-verification-test.ps1`; every invocation resolved one exact fully qualified method and produced one passing TRX.

| Test method | Analytical contract | Status |
|---|---|---|
| `CompetingRiskDependencyVerificationTests.Test_IndependentSimulation_MatchesRankDependenceAndCompositeCdf` | zero Spearman dependence and standard-Normal maximum CDF 0.25 at zero | Passed - 0.732 s |
| `CompetingRiskDependencyVerificationTests.Test_PerfectlyPositiveSimulation_MatchesRankDependenceAndCompositeCdf` | unit Spearman dependence and comonotonic maximum CDF 0.5 at zero | Passed - 0.247 s |
| `CompetingRiskDependencyVerificationTests.Test_PerfectlyNegativeSimulation_MatchesRankDependenceAndCompositeCdf` | Gaussian-copula identities at Numerics limiting negative correlation | Passed - 0.309 s |
| `CompetingRiskDependencyVerificationTests.Test_CorrelationMatrixSimulation_MatchesRankDependenceAndCompositeCdf` | Gaussian-copula identities at configured latent correlation 0.6 | Passed - 0.306 s |

TR-013 and TR-015 use fast programmatic tests because their contracts are deterministic validation,
weighting, ownership, serialization, and persistence behavior rather than estimator or
external-oracle calculations. The core tests cover mixed-invalid criteria, all-invalid failure,
exact-zero RMSE, finite AIC parity, B17C compatibility, matrix validation/ownership/XML, and
result construction. UI tests cover property ownership, independent copy, appended-column
compatibility, and SQLite save/open.

TR-014 adds fast helper, Composite, CFA, and UI contracts plus two independent numerical oracles.
The helper tests establish unique in-range rows, sampling across the full longer-chain range,
exact seed repeatability, distinct source permutations, and pairwise absolute Spearman correlation
below `0.05` for three 5,000-draw mappings. Composite fast tests cover both branches, actual
shortest-chain sizing, exact finite-map reconstruction, different seeds, immutable child outputs,
and unchanged point estimates. CFA fast tests cover copula/X/Y row order, optional-chain fallback,
cache invalidation, and exact aggregate/accessor agreement. UI tests cover seed save/open/copy/undo.

| Exact verification method | Independent contract | Tolerances and negative control | Status |
|---|---|---|---|
| `PosteriorResamplingVerificationTests.CompositePosteriorResampling_MatchesIndependentCartesianOracle` | empirical Cartesian product of deliberately raw-aligned child posteriors; reversed-chain and swapped-child variants | posterior mean `0.02`; credible limits `0.05`; raw-paired miss at least `0.10` | Passed - 2.677 s |
| `PosteriorResamplingVerificationTests.CoincidentFrequencyPosteriorResampling_MatchesIndependentClosedFormOracle` | closed-form independent Normal-sum posterior; reversed-chain variant | posterior mean `0.02`; credible limits `0.05`; raw-paired miss at least `0.10` | Passed - 2.505 s |

Each TR-014 Verification method was run separately through `scripts/run-verification-test.ps1`
and produced one passing TRX. The complete Verification project was not run. Current final fast
gates pass Core 3,134/3,134, UI 571/571, and App 428/428.

## Phase 4 recovery supplement - 3 August 2026

The following 30 exact methods were run individually. Twenty-three passed and seven exposed
unresolved findings, so they continue to gate the start of Phase 5. The competing-risk source is pinned to Numerics
`c361f2864428a98a33d6072ffa9bc11ac360839d`; the composite source is pinned to RMC-TotalRisk
`d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7` and the 2024 composite hazard/response report.

### Competing-risk MLE and default-DEMCzs recovery

All ten MLE methods use the production Differential Evolution default. All ten Bayesian methods
leave the production DEMCzs sampling configuration untouched and assert its resolved values:
3,500 iterations, 1,750 warmup, 10,000 outputs, 90% intervals, posterior mean, seed 12345,
dimension-scaled chains/thinning/initialization, and the default advanced proposal settings.
`CompetingRiskAnalysis` supplies a `UserDefined` initial population from an inflated MAP/Hessian
approximation and each method that reaches post-run assertions verifies that this initializer did
not silently fall back to randomized starts.

| Exact method | Fixture and oracle | Status |
|---|---|---|
| `CompetingRiskRecoveryTests.MLE_Minimum_TwoWeibullConstantIncreasing_RecoversParent` | Minimum Weibull(50, 1) + Weibull(80, 3); likelihood and parent CDF | Passed - 3.488 s |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_TwoWeibullConstantIncreasing_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Passed - 3:21.394 |
| `CompetingRiskRecoveryTests.MLE_Minimum_TwoWeibullContrastingShapes_RecoversParent` | Minimum Weibull(30, 0.8) + Weibull(100, 3); likelihood, CDF, sorted shapes | Passed - 2.487 s |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_TwoWeibullContrastingShapes_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, CDF, draw-ordered shapes | Passed - 2:52.558 |
| `CompetingRiskRecoveryTests.MLE_Minimum_ThreeWeibullBathtub_RecoversParent` | Minimum Weibull(20, 0.7) + Weibull(200, 1) + Weibull(150, 4); likelihood and parent CDF | Passed - 39.592 s |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_ThreeWeibullBathtub_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Passed - 5:37.609 |
| `CompetingRiskRecoveryTests.MLE_Minimum_ThreeWeibullSeparatedShapes_RecoversParent` | Minimum Weibull(15, 0.5) + Weibull(60, 1.5) + Weibull(120, 4); likelihood and parent CDF | Passed - 16.884 s |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_ThreeWeibullSeparatedShapes_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Passed - 5:49.246 |
| `CompetingRiskRecoveryTests.MLE_Maximum_TwoSeparatedNormals_RecoversParent` | Maximum Normal(50, 8) + Normal(85, 12); likelihood, CDF, sorted means | Passed - 8.474 s |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_TwoSeparatedNormals_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, CDF, draw-ordered means | Failed - 2:34.639; lower-mean miss 1,086.09% |
| `CompetingRiskRecoveryTests.MLE_Maximum_WeibullAndGumbel_RecoversParent` | Maximum Weibull(50, 2) + Gumbel(70, 15); likelihood and parent CDF | Passed - 2.276 s |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_WeibullAndGumbel_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Failed - 1:40.019; scale R-hat 1.10899 |
| `CompetingRiskRecoveryTests.MLE_Maximum_ThreeSeparatedNormals_RecoversParent` | Maximum Normal(40, 6) + Normal(70, 8) + Normal(100, 10); likelihood and parent CDF | Passed - 5.581 s |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_ThreeSeparatedNormals_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Failed - 6:02.620; standard-deviation R-hat 1.44785 |
| `CompetingRiskRecoveryTests.MLE_Maximum_ThreeDifferentFamilies_RecoversParent` | Maximum Exponential(0.05) + Gamma(3, 15) + natural LogNormal(4.2, 0.4); likelihood and parent CDF | Passed - 14.076 s |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_ThreeDifferentFamilies_RecoversParent` | Same fixture; default DEMCzs and MAP initialization | Failed - 6:16.455; Gamma inverse-CDF exception |
| `CompetingRiskRecoveryTests.MLE_Minimum_CorrelatedTwoWeibulls_RecoversParent` | Minimum two-Weibull Gaussian copula at latent rho 0.6; likelihood and parent CDF | Passed - 26.354 s |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_CorrelatedTwoWeibulls_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Failed - 28:48.056; scale ESS 77.5203 |
| `CompetingRiskRecoveryTests.MLE_Maximum_CorrelatedTwoNormals_RecoversParent` | Maximum Normal(50, 10) + Normal(65, 12), latent rho 0.6; likelihood and parent CDF | Passed - 12.083 s |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_CorrelatedTwoNormals_RecoversParent` | Same fixture; default DEMCzs, MAP initialization, diagnostics, parent CDF | Failed - 17:32.062; standard-deviation R-hat 1.17331 |

Every competing-risk case uses BestFit generation seed 12345, true-parameter data-likelihood
parity at `1E-10`, and empirical-quantile CDF locations 0.01-0.99. The CDF bound is `0.05` for
ordinary two-component cases and `0.06` for three-component/correlated cases. Bayesian diagnostics
require finite R-hat below `1.1` and ESS above `100` for every parameter.

### Composite report and product-posterior recovery

| Exact method | Oracle | Status |
|---|---|---|
| `CompositeRecoveryTests.MixtureCdf_MatchesExactWeightedNormalSum` | exact weighted three-Normal CDF | Passed - 0.354 s |
| `CompositeRecoveryTests.MixtureQuantiles_MatchPublishedRMistrTable45` | 25 published R `mistr` Table 45 quantiles | Passed - 0.387 s |
| `CompositeRecoveryTests.MixtureQuantiles_InvertAnalyticWeightedNormalCdf` | direct Normal CDF and probability-dependent inversion bound | Passed - 0.770 s exact rerun on 20 August 2026; prior logarithmic-X run failed with extreme-tail residual `1.02566838E-8` |
| `CompositeRecoveryTests.MaximumComposite_MatchesIndependentAndComonotonicClosedForms` | independent product and comonotonic minimum identities | Passed - 0.432 s |
| `CompositeRecoveryTests.MinimumComposite_MatchesIndependentAndComonotonicClosedForms` | independent union and comonotonic maximum identities | Passed - 0.444 s |
| `CompositeRecoveryTests.CombinationRules_SatisfyTheoreticalBracketingAndRemainDistinct` | mixture/maximum/minimum brackets and material separation | Passed - 0.547 s |
| `CompositeRecoveryTests.MixturePosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 mixture posterior | Passed - 4.758 s |
| `CompositeRecoveryTests.MaximumPosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 independent maximum posterior | Passed - 5.580 s |
| `CompositeRecoveryTests.MinimumPosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 independent minimum posterior | Passed - 4.766 s |
| `CompositeRecoveryTests.CorrelationMatrix_MinimumAndMaximumMatchBivariateNormalOrthants` | analytical bivariate-Normal median orthants at latent rho 0.6 | Passed - 0.347 s |

The three posterior methods use explicit 5,000-draw `MCMCResults`, 20 deterministic mean supports
per child, seed 20260803, five nonexceedance probabilities, 90% limits, mean tolerance `0.02`, and
limit tolerance `0.05`. Each also requires the fixed parent curve to remain inside its band.
Analytical formulas and the short published table are embedded; no new oracle artifact is added.

The Composite supplement now passes 10/10 methods. The six failed Default-DEMCzs competing-risk
cells remain recorded above and have an approved deferred-research disposition; they were not
rerun for the Phase 5 prerequisite and no sampler, seed, prior, fixture, formula, or tolerance was
changed. The combined supplement checkpoint is therefore 24/30 passed and six deferred findings.

## Phase 5 compatibility guardrails

| Fast method | Project | Contract | Status |
|---|---|---|---|
| `PublicApiCompatibilityTests.PublicApi_MatchesCapturedBaseline` | UI.Tests | 853-line exported public/protected UI signature baseline | Passed - UI project 576/576 |
| `PublicApiCompatibilityTests.PublicApi_MatchesCapturedBaseline` | App.Tests | 1,657-line exported public/protected App signature baseline | Passed - App project 431/431 |
| `TimeSeriesModelSerializationCompatibilityTests.*` | UI.Tests | Legacy AR, MA, ARIMA, and ARIMAX XML settings plus unknown optional attribute | Passed - 4 methods |
| `TimeSeriesAnalysisControlSourceTests.TransformSelector_RetainsEstablishedBindingContract` | App.Tests | Existing XAML item source and two-way `Element.ARIMAX.TransformType` binding | Passed |
| `TimeSeriesAnalysisControlSourceTests.TransformSelector_RetainsEstablishedItems` | App.Tests | None, Logarithmic, Box-Cox, and Yeo-Johnson labels and enum values | Passed |

These are deterministic compatibility regressions, not numerical Verification methods. Baseline
hashes and command evidence are recorded in [Time-Series Verification](time-series.md).

## TR-035 time-series Jeffreys metadata

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesPriorMetadataTests.PointwisePriorMetadata_ClassifiesExactlyOneJeffreysScaleComponentWhenEnabled` | Core Tests | One scale component when enabled, none disabled, correct identity/type/density in AR, MA, ARIMA, and ARIMAX | Exact metadata; `1E-12` density; passed in Core 3,183/3,183 |
| `TimeSeriesPriorMetadataTests.PointwisePriorMetadata_SumsToScalarPriorLikelihood` | Core Tests | Decomposed sum equals scalar prior | `1E-12`; passed |
| `TimeSeriesPriorMetadataTests.ARIMAX_JeffreysScaleMetadata_RemainsEstablishedReference` | Core Tests | Unchanged ARIMAX name, type, and value | Exact name/type; `1E-12` value; passed |
| `Phase5TimeSeriesVerificationTests.JeffreysScaleMetadataMatchesIndependentPriorOracle` | Verification | Committed analytical $-\log(\sigma)$ oracle at four fixed scales | `1E-12`; guarded pass 1/1 |

The Verification method is numerical; the three state/decomposition contracts remain in the
fast project. No optimizer, sampler, recovery fixture, or production generator is invoked.

## TR-040 invalid time-series innovation scale

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesInvalidScaleTests.InvalidInnovationScale_ReturnsNegativeInfinityAcrossAllPaths` | Core Tests | Five invalid-scale rows across AR, MA, ARIMA, ARIMAX scalar/pointwise/component/prior paths; metadata and lengths retained | Exact negative infinity and metadata; 5 passing rows |
| `TimeSeriesInvalidScaleTests.FinitePositiveInnovationScale_RetainsValidEvaluation` | Core Tests | Representative finite-positive control | Scalar/pointwise parity; passed |
| `Phase5TimeSeriesVerificationTests.InvalidScaleBehaviorMatchesScalarAndPointwiseOracle` | Verification | Independent Gaussian, uniform-normalization, and Jeffreys formulas plus invalid domain | `1E-12` valid; exact negative infinity invalid; guarded pass 1/1 |

The complete Core project passes 3,189/3,189. The Verification method is the only TR-040 method
run from the Verification project.

## TR-036/TR-046 time-series transform lifecycle

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesTransformStateTests.*` | Core Tests | Read-only/non-browsable getter; atomic rebuild; training-prefix holdout isolation; automatic/manual provenance; XML/clone; canonicalization; ignored `lambda2`; invalidation | Exact state and `1E-12`; passed in Core 3,201/3,201 |
| `TimeSeriesModelSerializationCompatibilityTests.ManualTransformLambda_NewXml_RoundTripsAllModelTypes` | UI.Tests | New optional XML state across AR, MA, ARIMA, and ARIMAX while legacy/unknown-attribute fixtures remain valid | Exact state; passed in UI 578/578 |
| `TimeSeriesAnalysisTests.ManualTransformLambda_CopyUndoAndRedoPreserveEffectiveState` | UI.Tests | Copy and undo/redo preserve effective manual state | Exact state; passed |
| `Phase5TimeSeriesVerificationTests.TransformLambdaMatchesIndependentTrainingOnlyOracle` | Verification | R Box-Cox/Yeo-Johnson profile fit on six training values with mutated three-value holdouts | `1E-8` absolute or `1E-7` relative; guarded pass 1/1 |
| `Phase5TimeSeriesVerificationTests.ManualTransformLambdaRebuildMatchesIndependentLikelihoodOracle` | Verification | R transform, Jacobian, residual, and conditional Gaussian likelihood recurrence | Same cross-language rule; deterministic identities `1E-12`; guarded pass 1/1 |

API DTO/service regressions cover omitted/manual values, JSON names, all four families, and invalid
requests. App passes 438/438 and API passes 498/498. UI/App signature baselines remain exact; the
Core baseline contains only the four approved additive getters. The R artifact and generator were
committed before C# evaluation and retain their manifest SHA-256 hashes.

## TR-041 ARIMAX date/index alignment

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `ARIMAXAlignmentTests.Differencing_PreservesLaterRawDatesAndTrainingBoundary` | Core Tests | `d=0,1,2`; model step `k` maps to raw index `k+d`; exactly `T-d` training steps | Exact dates/counts; values `1E-12`; passed |
| `ARIMAXAlignmentTests.TrainingState_IsolatedFromResponseAndCovariateHoldout` | Core Tests | Response/covariate holdout mutations cannot affect training state, defaults, residuals, or likelihood | `1E-12`; passed |
| `ARIMAXAlignmentTests.ShiftedCovariate_IsRejectedWithoutPositionalFallback` | Core Tests | Same-length one-period shift fails exact-date validation and all likelihood decompositions retain shape | Exact messages/negative infinity/length; passed |
| `ARIMAXAlignmentTests.CovariateValidation_RejectsRequiredDuplicatesAndAllowsExtraDates` | Core Tests | Required duplicate date fails; extra dates outside the required window are harmless | Exact validation contract; passed |
| `ARIMAXAlignmentTests.CovariateTimestampMutation_AtomicallyRefreshesNumericalAlignment` | Core Tests | Direct timestamp edits refresh cached alignment before numerical evaluation | Exact negative infinity then finite restoration; passed |
| `ARIMAXAlignmentTests.ConditionalOrderChanges_RebuildAlignedJacobian` | Core Tests | AR/MA order changes after data attachment rebuild the conditional Jacobian range | Exact parity with preconfigured-order controls; passed |
| `ARIMAXAlignmentTests.DifferencedLikelihood_UsesDateIndexedLevelCovariateAndAlignedJacobian` | Core Tests | Level covariate at raw date `k+d`; scalar/pointwise/component parity | `1E-12`; passed |
| `TimeSeriesAnalysisControlSourceTests.ResidualPlot_UsesDateAlignedDifferencedCount` | App Tests | Residual plot uses the differenced training count and later raw timestamps | Exact source contract; passed |
| `Phase5TimeSeriesVerificationTests.ArimaxDifferencedLikelihoodMatchesDateIndexedIndependentOracle` | Verification | Independent R transform, differencing, date join, ARMA recurrence, conditional Jacobian, and Gaussian likelihood for `d=0,1,2` | `1E-10`; guarded pass 1/1 |

The complete final package gates pass Core 3,208/3,208, UI 578/578, App 440/440, and API
498/498. The strict Debug solution build reports zero warnings/errors and UI/App signature
baselines remain exact. The R artifact and generator were committed before C# evaluation.

## TR-037 ARIMA/ARIMAX prediction reintegration

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesPredictionReintegrationTests.ArimaD1LinearPrediction_ReintegratesExactLengthAndComponents` | Core Tests | `d=1`, zero/positive horizon, raw length and component `k+d` map | `1E-12`; two passing rows |
| `TimeSeriesPredictionReintegrationTests.ArimaD2QuadraticPrediction_ReintegratesExactRecurrence` | Core Tests | Constant second differences reconstruct square-number levels and a two-slot conditioning prefix | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaxD1Prediction_UsesDateIndexedLevelCovariateAndReintegrates` | Core Tests | Exact-date level covariate drives first differences at raw slots `k+1` | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.LogTransformedD1Predictions_ReintegrateBeforeInverseTransform` | Core Tests | ARIMA and ARIMAX integrate on log scale and inverse-transform the complete path once | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.NoneD0FixedSeedPrediction_RetainsGoldenArraysBitForBit` | Core Tests | Pre-change ARIMA/ARIMAX `Transform.None`, `d=0` values and every component vector | Exact double equality; passed |
| `Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle` | Verification | Hand ARIMA(0,2,0) square recurrence and logarithmic ARIMAX(0,1,0,0) level-covariate recurrence | `1E-10`; guarded pass 1/1 |

The complete package gates pass Core 3,213/3,213, UI 578/578, App 440/440, and API 498/498.
The strict Debug solution build reports zero warnings/errors, and UI/App signature baselines remain
exact. No external artifact is required for the embedded analytical recurrence; the verification
source hash and exact command/TRX evidence are recorded in [Time-Series Verification](time-series.md).

## TR-038 AR/MA/ARIMA transformed generation

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesGenerationTransformTests.ArLogarithmicGeneration_InverseTransformsCompletedModelRecurrence` | Core Tests | Fixed-seed AR(2) model-scale recurrence followed by one exponential inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.MaBoxCoxGeneration_InverseTransformsCompletedModelRecurrence` | Core Tests | Fixed-seed MA(2) recurrence followed by manual-lambda Box-Cox inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaYeoJohnsonD1Generation_IntegratesThenInverseTransforms` | Core Tests | Fixed ARMA differences, observed transformed anchor, complete integration, then Yeo-Johnson inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaD2Generation_UsesObservedOrZeroTransformedAnchors` | Core Tests | Attached first two transformed levels versus zero-anchor polynomial | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors` | Core Tests | Requested observed/zero anchors only, exact length, no model-scale values | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.NoneD0FixedSeedGeneration_RetainsGoldenArraysBitForBit` | Core Tests | Pre-change AR, MA, and ARIMA `Transform.None`/`d=0` arrays | Exact double equality; passed |
| `Phase5TimeSeriesVerificationTests.ArAndMaTransformedGeneratorsMatchIndependentOracle` | Verification | Independent exponential/Box-Cox algebra plus 1,000-step model-scale Gaussian moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |
| `Phase5TimeSeriesVerificationTests.ArimaDifferencedTransformedGeneratorMatchesIndependentOracle` | Verification | Independent Yeo-Johnson/integration algebra plus 1,000 generated steps/999 first-difference moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |

The package gates pass Core 3,219/3,219, UI 578/578, App 440/440, and API 498/498; the strict
Debug build has zero warnings/errors. The failed 50,000-step logarithmic overflow run and the
subsequent explicit 1,000-step direction are retained in the time-series report. No full
Verification run occurred.

## TR-039 ARIMAX transformed/differenced generation

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesArimaxGenerationTests.ArimaxYeoJohnsonD1Generation_UsesDateAlignedModelScaleRecurrence` | Core Tests | ARIMAX(1,1), `d=1`, dated level covariate, observed anchor, integration, one Yeo-Johnson inverse | `1E-12`; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxLogGeneration_DeterministicComponentsRemainOnModelScale` | Core Tests | Intercept, trend, Fourier seasonality, and covariate shifts remain additive on log scale | `1E-12`; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxD1Generation_UsesObservedOrZeroAnchors` | Core Tests | Identical innovations isolate observed versus zero transformed anchors | `1E-12`; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors` | Core Tests | Requested observed/zero anchors only, exact length, no innovations | Exact; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxGeneration_ExplicitCovariatesUseExactResponseDates` | Core Tests | Reversed ordinate order produces identical exact-date covariate path | Exact; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxGeneration_MissingRequiredCovariateTimestampThrows` | Core Tests | Same-length dated covariate with one required timestamp missing | Explicit exception/message; passed |
| `TimeSeriesArimaxGenerationTests.ArimaxNoneD0FixedSeedGeneration_RetainsGoldenArrayBitForBit` | Core Tests | Pre-change seed-24682 `Transform.None`/`d=0` complete ARIMAX array | Exact double equality; passed |
| `ARIMAXTests.Test_GenerateRandomValues_CovariateExtensionBlockBootstrap_ExtendsCovariates` | Core Tests | Existing block-bootstrap extension and output length | Passed |
| `ARIMAXTests.Test_GenerateRandomValues_CovariateExtensionKNN_ExtendsCovariates` | Core Tests | Existing KNN extension and output length | Passed |
| `ARIMAXTests.Test_GenerateRandomValues_ExplicitCovariates_OverridesExtensionSetting` | Core Tests | Existing explicit generation-covariate override | Passed |
| `Phase5TimeSeriesVerificationTests.ArimaxTransformedDifferencedGeneratorMatchesIndependentOracle` | Verification | Fixed algebraic Yeo-Johnson recurrence plus 1,000 generated steps/999 innovation moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |

The package gates pass Core 3,226/3,226, UI 578/578, App 440/440, and API 498/498. The final
serial strict Debug build reports zero warnings/errors; UI/App signature baselines remain exact.
The initial Verification compile failure and parallel-build file-lock failure are retained in the
time-series report. No full Verification run occurred.

## TR-042 information-criterion regression

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `AnalysisInformationCriteriaRoutingTests.TimeSeriesCriteria_UseOneDataLikelihoodCallAtMap` | Core Tests | Injected MAP and counting AR model prove one data-likelihood call, no prior/posterior call, and hand AIC/BIC routing | Exact call counts; `1E-10`; passed |
| `Phase5TimeSeriesVerificationTests.InformationCriteriaUseDataLikelihoodAtMapAndExcludePrior` | Verification | AR, MA, ARIMA, ARIMAX, and rating-curve data-only criteria plus analytical flat-prior Gaussian MAP/MLE parity | Criteria `1E-10`; parameters `1E-6`; guarded pass 1/1 |

The exact method uses 40 or fewer observations and one injected posterior row; it runs no
optimizer, sampler, or simulation and remains below the 1,000-step cap. Package gates pass Core
3,227/3,227, UI 578/578, App 440/440, and API 498/498. UI/App signature baselines remain exact and
the strict serial Debug build has zero warnings/errors. Fixture and infrastructure failure history
is retained in the time-series report. No full Verification run occurred.

## Phase 5 integrated recovery matrix

| Method | Oracle or recovery contract | Status |
|---|---|---|
| `AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1` | Independent R AR(1), 110-step burn-in, 1,000 retained observations, seed 12345, unchanged 5% gate, finite likelihood/prior, one-step recurrence | **Passed** - 1/1 after approved burn-in correction |
| `ARAnalysisTests.Test_EstimateParameters_AR1` | Same fixture; resolved-default assertions; test-only 1,000-step cap; central 95%, MAP 25%, R-hat/ESS, one-step recurrence | **Failed** - intercept R-hat `1.1478771` exceeds `< 1.1` |
| `MovingAverageMLERecoveryTests.Test_EstimateParameters_MA1` | Independent R MA(1), 110-step burn-in, 1,000 retained observations, seed 12345, unchanged 5% gate | Not run - stopped by preceding failure |
| `MAAnalysisTests.Test_EstimateParameters_MA1` | Same fixture and capped Bayesian recovery contract | Not run |
| `Phase5TimeSeriesRecoveryTests.MleArima111LogD1RecoversGeneratingParameters` | Independent R logarithmic ARIMA(1,1,1), 1,000 observations, seed 51037, 15% coefficients/10% scale | Not run |
| `Phase5TimeSeriesRecoveryTests.BayesianArima111LogD1RecoversGeneratingParameters` | Same fixture and capped Bayesian recovery contract | Not run |
| `Phase5TimeSeriesRecoveryTests.MleArimax10D1LevelCovariateRecoversGeneratingParameters` | Independent R ARIMAX(1,1,0), dated level covariate, 1,000 observations, seed 51038, 15% coefficients/10% scale | Not run |
| `Phase5TimeSeriesRecoveryTests.BayesianArimax10D1LevelCovariateRecoversGeneratingParameters` | Same fixture and capped Bayesian recovery contract | Not run |

The artifact was committed before C# recovery evaluation. The mistakenly changed AR/MA seeds and
the corrected-seed fixture without stationary burn-in remain failure history. After the approved
110-step burn-in correction was committed, AR MLE passed its unchanged gate and AR Bayesian failed
its unchanged R-hat gate after exactly 1,000 steps per chain. Per the approved matrix rule, the
remaining six methods, alternate seeds, changed thresholds, and the full Verification project were
not run. See the time-series report for hashes, exact commands, runtimes, and failure history.
