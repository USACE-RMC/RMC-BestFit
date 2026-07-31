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

## Remaining audit

The following mixed files remain a method-level ownership backlog:

| Area | Mixed files |
|---|---|
| Model estimation | `GeneralizedMethodOfMomentsTests.cs`, `MaximumAPosterioriTests.cs` |
| Rating curve | `RatingCurveTests.cs`, `RatingCurveAnalysisTests.cs` |
| Time series | Model and analysis test files containing both constructors/properties and estimator recovery |
| Bivariate and spatial | Files containing both DTO/state checks and fitted-model recovery |

The audit is intentionally marked **in progress**. No remaining mixed file is represented as verification-grade until its methods have been classified and moved. This repository-hygiene backlog does not reopen the claim-specific scientific evidence that closed Phases 1 and 2.

## External model-comparison oracles and PSIS correction - 25-26 July 2026

| Verification method | External oracle | Status |
|---|---|---|
| `ModelEstimation/InformationCriterionOracleTests.cs::DIC_MatchesRBayesianToolsOracle` | R `BayesianTools::DIC` 0.1.9 | Passed - exact focused method |
| `ModelEstimation/InformationCriterionOracleTests.cs::WAIC_MatchesRLooOracle` | R `loo::waic` 2.10.0 | Passed - exact focused method |
| `ModelEstimation/PsisLooOracleTests.cs::RlooOracle_InternalIdentitiesAreConsistent` | R `loo::psis` and `loo::loo` 2.10.0 | Passed - exact focused method |
| `ModelEstimation/PsisLooOracleTests.cs::PSISLOO_MatchesRLooOracle` | R `loo::loo` 2.10.0 | Passed - aggregate, pointwise, Pareto-k, and smoothed-weight parity |
| `ModelEstimation/PsisLooOracleTests.cs::PsisTailRegimes_MatchRLooOracle` | R `loo::psis` 2.10.0, including bounded through nonfinite-mean and degenerate tails | Passed - every weight, Pareto k, and effective sample size |
| `ModelEstimation/PsisLooOracleTests.cs::ParetoInfluence_UsesRloo210DiagnosticThreshold` | R `loo` 2.10.0 sample-size diagnostic threshold | Passed - classification and XML round-trip |
| `ModelEstimation/PsisLooOracleTests.cs::DefaultInformationCriteria_EvaluatePointwiseLikelihoodOnce` | Deterministic call-count model | Passed - one evaluation per retained draw |
| `ModelEstimation/PsisLooOracleTests.cs::InfluenceDiagnostics_ReuseCachedPointwiseLikelihood` | Deterministic call-count model | Passed - influence reuses pointwise LOO cache |

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
| `Log10NormalInfluenceLeverageFindingTests.GmmCookInfluence_LegacyPsisAdapterIsObsoleteCompatibilityOnly` | Obsolete attribute plus preserved Cook-value-to-legacy-DTO mapping | Passed - exact focused compatibility method |

The first four methods consume the committed [GMM specification oracle](../../verification/data/model-estimation/gmm-specification-oracle.json), generated from the locked R environment. R is not required at C# runtime. Each method was run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the full Verification project was not executed.

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
| `NumericsMcmcFindingTests.RankNormalizedRhat_MatchesRPosteriorOracle` | BestFit Verification | Committed R `posterior` 1.7.0 oracle across nine fixtures | Passed - exact focused method |
| `NumericsMcmcFindingTests.ConservativeEss_MatchesRPosteriorBulkAndTailOracle` | BestFit Verification | Committed R bulk/lower-tail/upper-tail ESS and conservative minimum | Passed - exact focused method |
| `NumericsMcmcFindingTests.Arwmh_BestFitWiringRecordsEveryRealizedStateInAdaptiveCovariance` | BestFit Verification | Production sampler setup and complete per-chain covariance counts | Passed - exact focused method |
| `NumericsMcmcFindingTests.Nuts_BestFitResultsUseHamiltonianAcceptanceWithoutDetailedDiagnostics` | BestFit Verification | Generic sampler acceptance, Hamiltonian result transfer, and acceptance-only BestFit report | Passed - exact focused method |
| `NumericsMcmcFindingTests.Nuts_BestFitNumericalGradientMatchesPosteriorGradient` | BestFit Verification | Bound-aware finite differences of the complete coupled-prior posterior | Passed - exact focused method |

The BestFit methods were run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the full Verification project was not executed. The Numerics methods passed by exact fully qualified filter on the .NET 10 target, and the reconciled complete Numerics .NET 10 Release project records all 1,986 tests passing with zero failures. The TR-029 BestFit methods consume the committed [MCMC diagnostics oracle](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json), so C# tests require no R or Python runtime. Fast report tests `GenerateReport_Rhat1005_PassesModernThreshold` and `GenerateReport_Rhat102_WarnsAtModernThreshold` verify the 1.01 readiness rule without changing the concise `R-hat` label.

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

All six methods generate the parent sample through `MixtureModel.GenerateRandomValues(1000, 12345)`. Each method was run separately through `scripts/run-verification-test.ps1`; every invocation source-resolved one exact method and produced one passing TRX under `TestResults/VerificationFocused`.

| Test method | Oracle or recovery contract | Status |
|---|---|---|
| `MixtureRecoveryTests.NormalMixture2D_Recovery_Parity` | BestFit production generator, Numerics/BestFit likelihood and EM parity, two-component parent recovery | Passed - 1.426 s |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_Recovery_Parity` | BestFit positive-hurdle generator, Numerics/BestFit likelihood and EM parity, atom and parent recovery | Passed - 1.852 s |
| `MixtureRecoveryTests.NormalMixture3D_Recovery_Parity` | BestFit production generator, Numerics/BestFit likelihood and EM parity, three-component parent recovery | Passed - 3.247 s |
| `MixtureRecoveryTests.NormalMixture2D_BayesianRecovery` | Seeded DEMCzs posterior-mode recovery with split R-hat and ESS acceptance | Passed - 9.589 s |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_BayesianRecovery` | Seeded positive-hurdle DEMCzs recovery, binomial atom bound, split R-hat, and ESS | Passed - 21.346 s |
| `MixtureRecoveryTests.NormalMixture3D_BayesianRecovery` | Seeded three-component DEMCzs posterior-mode recovery with label sorting, split R-hat, and ESS | Passed - 12.761 s |

The parity methods retain pre-fit tolerance `1E-10`, cross-engine fitted tolerance `1E-8`, and absolute parent-recovery tolerance `0.1`. The Bayesian methods use four chains, 1,500 warmup iterations, 3,000 sampling iterations, thinning 5, 5,000 output draws, deterministic seeds, weight tolerance `0.1`, component tolerance `max(0.15, 0.15 * abs(parent))`, split R-hat below `1.1`, and conservative ESS above `100`. The full Verification project was not run.

## Phase 4 point-process backcheck - 31 July 2026

All ten scoped methods were run separately through `scripts/run-verification-test.ps1`; each invocation resolved one exact source method and produced one TRX. All ten pass. The guarded build remained warning- and error-free.

The three accepted Bayesian recovery fixtures were rerun with 1,000 observations and the untouched `BayesianAnalysis` defaults. Calendar-year uniform recovery passed in 35.690 s, nonseasonal production recovery passed in 8.450 s, and seasonal production recovery passed in 36.309 s. The default configuration eliminated the former seasonal second-Kappa miss.

The original water-year recovery cell changed the block-day changepoints from calendar `170/350` to `80/260`, so it did not isolate the effect of changing the year origin. The corrected cell holds `K1/K2` fixed, verifies identical magnitudes and block days, an exact 92-day date shift, and parent data log-likelihood parity at `1E-10`, then passes with default DEMCzs in 33.897 s.

The seasonal mixed-likelihood cell initially revealed an all-above threshold fixture inconsistent with `ProcessThresholdSeries`. Its corrected three-year fixture asserts one effective below/two above observations and derives the independent threshold oracle from those processed counts; the rerun passed at unchanged tolerance `2E-7`. No production point-process formula, sampler default, prior, or numerical acceptance setting changed. The full Verification project was not run.
