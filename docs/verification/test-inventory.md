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


Four legacy assertions were not retained: a probability-ordinate mutation test contradicted the established no-refit contract; generic large-sample and distribution-success counts had no oracle; and the outlier smoke test reproduced TR-010 (IsEstimated true with zero successful candidates). TR-010 is recorded as a confirmed defect, and its post-fix regression will be added with the approved correction.

## Remaining audit

The following mixed files require method-level splitting before their domain phase begins:

| Area | Mixed files |
|---|---|
| Model estimation | `GeneralizedMethodOfMomentsTests.cs`, `MaximumAPosterioriTests.cs` |
| Rating curve | `RatingCurveTests.cs`, `RatingCurveAnalysisTests.cs` |
| Time series | Model and analysis test files containing both constructors/properties and estimator recovery |
| Bivariate and spatial | Files containing both DTO/state checks and fitted-model recovery |

The audit is intentionally marked **in progress**. No remaining mixed file is represented as verification-grade until its methods have been classified and moved.

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

The BestFit methods were run separately through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`; the full Verification project was not executed. The Numerics methods passed by exact fully qualified filter on the .NET 10 target, and the isolated complete Numerics .NET 10 Release project passed all 1,960 tests with zero failures or skips. The TR-029 BestFit methods consume the committed [MCMC diagnostics oracle](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json), so C# tests require no R or Python runtime. Fast report tests `GenerateReport_Rhat1005_PassesModernThreshold` and `GenerateReport_Rhat102_WarnsAtModernThreshold` verify the 1.01 readiness rule without changing the concise `R-hat` label.
