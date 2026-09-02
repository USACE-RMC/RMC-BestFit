<!-- verification-status: finalized -->
# Verification Test Inventory

## Chunk 13 time-series reconciliation - 31 August 2026

Chunk 13 retains the eight Phase 5 estimator identities and the twelve independent Phase 5 oracle
identities. All eight retained estimator cells are verified. The retained MA(1)
MLE passed after its arbitrary five-percent gate was replaced by the common production observed-
information standardized-error rule. The AR(1) MLE's former boundary solution remains failure
history: it had non-finite/non-positive covariance and an objective independently inferior to both
parent and conditional optimum. After Haden Smith approved the DE reliability correction, the final
BestFit configuration uses a minimum population of 100 and midpoint repair between the target and a
violated bound while retaining Numerics convergence tolerances. The exact AR(1) rerun under
`20260831-192951-...` passed 1/1. Chunk 13 adds four
artifact-backed identities in `TimeSeriesChunk13OracleTests`:

- `FirstOrderConditionalObjectivesMatchIndependentPythonOracle` - passed, one-result TRX;
- `HigherOrderArAndMaResponsesMatchIndependentPythonOracle` - passed, one-result TRX;
- `PureArAndMaThroughArimaMatchIndependentPythonOracle` - passed, one-result TRX;
- `ArimaxTrendSeasonalityAndCovariatesMatchIndependentPythonOracle` - passed, one-result TRX.

Seventy-six historical identities were consolidated and removed from discovery/catalog: every
open method in `ARIMAAnalysisTests`, `ARIMAMLERecoveryTests`, `ARIMAXAnalysisTests`, and
`ARIMAXMLERecoveryTests`; and every open method in `ARAnalysisTests`,
`AutoRegressiveMLERecoveryTests`, `MAAnalysisTests`, and `MovingAverageMLERecoveryTests` except
`AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1`. The method bodies remain for audit
provenance, but no old pass was transferred. The removal covers redundant order/trend/covariate
Cartesian variants, N=10,000 fixtures, arbitrary percentage/central-90% bands, weak raw
higher-order coordinates, and R comparisons whose exact/conditional likelihood, initialization,
or intercept convention was not aligned.

The single authored-red TRX for the new first-order identity executed exactly one method and failed
only on the intentionally absent artifact. There were no zero-result runs. The current matrix and
hashes are recorded in [time-series.md](time-series.md#chunk-13-completeness-reconciliation---31-august-2026).

On 1 September 2026 the retained ARIMA and ARIMAX MLE acceptance rules were normalized without
changing their fixtures, estimators, defaults, or scientific models. ARIMA now compares production
and R optima in a joint 95% likelihood-ratio region and retains its independently generated
one-coordinate 95% profiles for truth recovery. ARIMAX uses the four-coordinate joint 95%
likelihood-ratio region for both optimizer parity and generating-truth recovery. Both MLE cells and
their source-shared Bayesian cells passed in serial guarded reruns with exactly one result each; the
authored-red ARIMAX metadata run also executed exactly one failed result and is not pass evidence.

## Chunk 11A bivariate reconciliation

Chunk 11A raises every generated-parent copula recovery to exactly 1,000 paired observations. The
six one-coordinate families retain the production DEMCzs defaults and now require parent-versus-
independence discrimination, Normal marginal MLE standardized errors no greater than 1.96, and
central-95% copula parent inclusion with R-hat below 1.10 and ESS at least 100. The Student-t MCMC
identity was removed at Haden Smith's direction because repeated beta-function evaluation made the
realization impractical. Its renamed MLE replacement uses an unregularized observed-information
covariance for `rho` and the full-covariance delta-method uncertainty of symmetric tail dependence;
weak raw `nu` is not claimed recovered.

| Exact current identity | Latest isolated result directory | Result |
|---|---|---|
| `BivariateAnalysisParameterRecoveryTests.RecoverAMHCopulaParameters` | `20260831-072731-..._RecoverAMHCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverClaytonCopulaParameters` | `20260831-072836-..._RecoverClaytonCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverFrankCopulaParameters` | `20260831-072951-..._RecoverFrankCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverGumbelCopulaParameters` | `20260831-073105-..._RecoverGumbelCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverJoeCopulaParameters` | `20260831-073231-..._RecoverJoeCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverNormalCopulaParameters` | `20260831-073409-..._RecoverNormalCopulaParameters` | Passed |
| `BivariateAnalysisParameterRecoveryTests.RecoverStudentTCopulaParametersWithMaximumLikelihood` | `20260831-073524-..._RecoverStudentTCopulaParametersWithMaximumLikelihood` | Passed |
| `CopulaEstimationOracleTests.StudentT_PseudoLikelihood_MatchesIndependentOptimum` | `20260901-143725-..._StudentT_PseudoLikelihood_MatchesIndependentOptimum` | Passed |
| `CopulaEstimationOracleTests.StudentT_InferenceFromMargins_MatchesIndependentOptimum` | `20260901-143950-..._StudentT_InferenceFromMargins_MatchesIndependentOptimum` | Passed |

The removed historical identity is
`BivariateAnalysisParameterRecoveryTests.RecoverStudentTCopulaParameters`; its interrupted
`20260830-172221-..._RecoverStudentTCopulaParameters` directory contains no TRX and is discarded.
The initial sandboxed AMH invocation (`20260830-171612-..._RecoverAMHCopulaParameters`) stopped at
the guarded runner's build step because the sandbox could not read the existing NuGet profile; its
empty result directory also contains no TRX and is discarded.
The first MLE replacement run produced one failed result because the test inspected covariance status
before invoking covariance computation; the corrected lifecycle passed without a production change.
The first Student-t MPL oracle run produced one expected TDD failure because its fixture was not yet
present; both failures are retained as development history, not accepted scientific evidence.

The extended artifact is `verification/data/bivariate/copula-estimation-oracle.json` (SHA-256
`28edfbd28e392df1ac540766f3ba5c3f1ce57d8a442facbcd6541866f683956e`), generated by
`verification/python/bivariate/generate_copula_estimation_oracle.py` (SHA-256
`358dca1910f1091e1f9f07978f38662444575f9a0373e39cd31189c37918307b`) with Python 3.12.13,
NumPy 2.3.5, and SciPy 1.18.1. Its Student-t sample uses seed 20260830, parent `[rho=0.8, nu=4]`,
Normal marginal parameters `[100,15]` then `[80,25]`, and physical fit order `[rho, nu]`.

## Chunk 11B coincident-frequency reconciliation

The three analytical `X+Y` cells retain their analytical classification and cover independence,
positive dependence, and negative dependence. One new N=1000 cell supplies the missing nonlinear
interaction without expanding to a redundant correlation-by-response Cartesian matrix. It uses
`Z=exp(0.01X+0.01Y)` with generator seed 13055, positive `rho=0.5`, and the exact Lognormal law.
Numerical response-table error is bounded separately at 0.015 AEP. Normal marginal MLE coordinates
use observed-information standardized errors; copula `rho` uses central-95% inclusion, R-hat below
1.10, and ESS at least 100. Two thousand independent asymptotic Normal-MLE uncertainty draws at seeds
24680/24681 are propagated as MLE uncertainty, not posterior uncertainty. The generating response is
inside the central 95% bands at nonexceedance 0.10, 0.25, 0.50, 0.75, and 0.90.

| Exact current identity | Latest isolated result directory | Result |
|---|---|---|
| `CoincidentFrequencyAnalysisTests.ExponentialLinearCombination_ParentResponseInsidePredictiveBands` | `20260831-073912-..._ExponentialLinearCombination_ParentResponseInsidePredictiveBands` | Passed |
| `CoincidentFrequencyAnalysisTests.SumOfNormals_RhoZero_MatchesClosedForm` | `20260831-073650-..._SumOfNormals_RhoZero_MatchesClosedForm` | Passed |
| `CoincidentFrequencyAnalysisTests.SumOfNormals_RhoPositive_MatchesClosedForm` | `20260831-073728-..._SumOfNormals_RhoPositive_MatchesClosedForm` | Passed |
| `CoincidentFrequencyAnalysisTests.SumOfNormals_RhoNegative_MatchesClosedForm` | `20260831-073809-..._SumOfNormals_RhoNegative_MatchesClosedForm` | Passed |

Every latest TRX contains exactly one result. The authored nonlinear test first exposed a shared
constant ownership compile error (`MinimumEffectiveSampleSize` belongs to `RecoveryAcceptance`);
the corrected test then passed without any production change. No frozen nonlinear artifact is needed
because the oracle is fully analytical and its grid, seeds, uncertainty sources, and numerical bound
are executable in the current identity.

## Chunk 12 rating-curve reconciliation

Chunk 12 replaced the arbitrary 5-50% coordinate bands with unregularized observed-information
standardized errors for MLE, central-95% posterior inclusion plus R-hat/ESS for Bayesian coordinates,
and predeclared predictive-response ordinates for weak multi-control coordinates. Exactly 1,000 aligned
stage-discharge pairs are generated per retained experiment. Parent physics, generator seeds, prior flags,
production sampler defaults, and convergence rules are unchanged. Every MLE uses Differential Evolution
with untouched default tolerances. Bayesian recovery declares posterior MAP as its point estimator.
MLE covariance or retained posterior draws propagate parameter uncertainty; independent draw-specific
Normal residuals on log10 discharge propagate observation uncertainty. A max-|t| rule across the entire
predeclared grid yields one simultaneous 95% predictive band.

The coverage matrix retains standard single-control, low-noise error, wide-range, bankfull-transition,
and three-control activation fixtures. `SingleSegment_LargeSample` was removed because all recovery
fixtures are now N=1000. `SingleSegment_SteepChannel` and `SingleSegment_WideChannel` were consolidated
because differently named coefficient/exponent variants add no identification mechanism beyond the
standard, range, and multiple-control cells. `TwoSegment_Default` was consolidated into the sharper
bankfull transition, and `ThreeSegment_Default` into the multiple-control activation fixture. These
five removals apply to both estimator classes; none of their historical outcomes was transferred.

| Exact current identity | Latest isolated result directory | Result |
|---|---|---|
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_Default` | `20260831-101709-..._SingleSegment_Default` | Passed; exactly 1 result |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_LowNoise` | `20260831-101715-..._SingleSegment_LowNoise` | Passed; exactly 1 result |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_SingleSegment_WideRange` | `20260831-101722-..._SingleSegment_WideRange` | Passed; exactly 1 result |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_TwoSegment_BankfullTransition` | `20260831-101728-..._TwoSegment_BankfullTransition` | Passed; exactly 1 result; exact allocation 495/505; stage-6.5 simultaneous predictive band contains truth |
| `RatingCurveMLERecoveryTests.Test_EstimateParameters_ThreeSegment_MultipleControl` | `20260831-101737-..._ThreeSegment_MultipleControl` | Passed; exactly 1 result; exact allocation 270/406/324; stage-8.5 simultaneous predictive band contains truth |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_Default` | `20260831-101837-..._SingleSegment_Default` | Passed; exactly 1 result |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_LowNoise` | `20260831-101905-..._SingleSegment_LowNoise` | Passed; exactly 1 result |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_SingleSegment_WideRange` | `20260831-101935-..._SingleSegment_WideRange` | Passed; exactly 1 result |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_TwoSegment_BankfullTransition` | `20260831-102004-..._TwoSegment_BankfullTransition` | Passed; exactly 1 result; exact allocation 495/505 |
| `RatingCurveBayesianRecoveryTests.Test_EstimateParameters_ThreeSegment_MultipleControl` | `20260831-102127-..._ThreeSegment_MultipleControl` | Passed; exactly 1 result; exact allocation 270/406/324; stage-8.5 simultaneous posterior-predictive band contains truth |

The initial sandboxed invocation of the single-segment default MLE stopped at the guarded runner's build
step because the sandbox could not read the existing NuGet profile; no test executed and no TRX was
produced, so it is discarded. A test-only compile attempt also failed on a missing
`Numerics.Sampling.MCMC` import before any method ran. Overlapping LowNoise and WideRange runs were
discarded despite one result each and rerun serially; an interrupted TwoSegment directory contains no
TRX. The earlier `062717`-through-`063614` pointwise results and the intermediate no-residual `100700`-
through-`101102` results are superseded, not transferred: they omitted observation residual uncertainty
and did not provide a grid-wide 95% statement. No current rating-curve method was renamed. Current
rating-curve accounting is 22 verified declarations and no open rating-curve gaps.

## Verification-wide MLE/MAP optimizer reconciliation - 31 August 2026

The direct-construction audit found 149 `MaximumLikelihood` or `MaximumAPosteriori` estimators. Five
intentional profile-likelihood constructions retain BFGS because their nuisance optimization is paired
with Brent root finding and is itself part of the independent profile oracle. All other 144 constructions
now use `OptimizationMethod.DifferentialEvolution`, and none mutates the optimizer's default absolute or
relative tolerance. GMM-only optimizers and tolerances were not changed.

All source-affected Chunk 11/12 and optimizer-oracle identities were run exactly and serially. The
initial sweep retained three failures rather than tuning them: the MAP prior-regime score exceeded
an arbitrary `0.0002` gate; the old cross-estimator Log10-Normal identity missed an arbitrary
`0.01 SE` coordinate gate; and AR(1) MLE converged to an inferior bound-clamped point. The final
scientific reconciliation replaces estimator-coordinate gates with known-covariance or
likelihood-ratio regions. The cross-estimator identity was renamed because joint scale reestimation
makes fixed-variance inverse weighting a statistical reference rather than an exact coordinate
identity; no old result was transferred.

With Haden Smith's approval, Numerics DE now repairs infeasible trials halfway between the target
and violated bound without consuming an extra random draw, and BestFit MLE/MAP uses a minimum
population of 100 while retaining Numerics convergence tolerances. The final exact AR(1) run passed
1/1. All five source-shared Log10-Normal identities also passed in fresh one-result guarded runs,
including the renamed
`MapAndGmmMuPosterior_InverseVarianceReferenceInsideCentral95Intervals`. The normalized MAP method
first exposed one leftover `1e-5` centered-coordinate assertion in a one-result failed TRX under
`20260901-115353-...`; that redundant nonstatistical assertion was removed before the final pass.

## Chunk 7 Bulletin 17C reconciliation

Chunk 7 replaced the six same-sample product-moment identities with genuine generated-parent
recovery at exactly 1,000 complete scalar observations and seed `12345`. The exact family,
parameter-order, log-space, skew, and uncertainty crosswalk is recorded in
[bulletin-17c.md](bulletin-17c.md#chunk-7-six-family-parameterization-crosswalk). Numerics
method-of-moments parameter and quantile variance is used only for the approved statistical
recovery acceptance; it is not used as the covariance oracle.

| Exact recovery identity | Latest isolated result directory | Result |
|---|---|---|
| `B17CSyntheticDataTests.Exponential_GmmRecoversGeneratingParent` | `20260830-094803-..._Exponential_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.Gamma_GmmRecoversGeneratingParent` | `20260830-094809-..._Gamma_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.Normal_GmmRecoversGeneratingParent` | `20260830-094815-..._Normal_GmmRecoversGeneratingParent` | Passed |
| `B17CSyntheticDataTests.PearsonTypeIII_GmmRecoversGeneratingParentAndQ99` | `20260830-094821-..._PearsonTypeIII_GmmRecoversGeneratingParentAndQ99` | Passed |
| `B17CSyntheticDataTests.LogNormal_GmmRecoversGeneratingLog10Parent` | `20260830-094827-..._LogNormal_GmmRecoversGeneratingLog10Parent` | Passed |
| `B17CSyntheticDataTests.LogPearsonTypeIII_GmmRecoversGeneratingLog10ParentAndQ99` | `20260830-094833-..._LogPearsonTypeIII_GmmRecoversGeneratingLog10ParentAndQ99` | Passed |

The 13 covariance methods now load a frozen Python-standard-library artifact and compare current
fits with a separate C# implementation of the complete-data just-identified sandwich. The oracle
uses central moments through order six, the B17C `c2`/`c3` centered-moment factors, and an analytical
Jacobian; it calls no RMC.BestFit or Numerics covariance, moment-conversion, matrix, or numerical-
differentiation routine.

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

The N=25 and N=100 Pearson III cells initially exposed a scale-sensitive trace ridge applied before
the shared helper tested whether the symmetric covariance was already positive definite. Haden Smith
approved correcting `MatrixRegularization.MakeSymmetricPositiveDefinite` so Cholesky tests the
un-ridged symmetric candidate first. The existing ridge schedule is unchanged for rejected candidates.
Both Pearson cells now pass the original independent oracle and tolerance; no covariance formula,
B17C estimator rule, seed, or reference result changed.

Artifact: `verification/data/bulletin17c/b17c-gmm-covariance-oracle.json`, generated by
`verification/python/bulletin17c/generate_b17c_covariance_oracle.py` with Python 3.12.13 standard
library only. The manifest records both SHA-256 values. Aggregate recovery/covariance outcome:
**19 passed, 0 failed, 0 skipped**. Fresh regression runs also passed all 12 penalty identities, all
12 example/PeakFQ plotting-position identities, and all seven selected general-GMM identities. Three
wrong-class PeakFQ attempts produced zero-test TRXs and were discarded; the corrected exact class
identities passed. No method in `B17CCoverageTests`, `B17CCensoredCoverageTests`, or
`B17CCohnEtAlCoverageTests` was executed; all 56 coverage entries remain execution-excluded.

## Chunk 8 point-process reconciliation

Chunk 8 retains the two independently coded mixed-likelihood cells at `2E-7`, reclassifies the two
N=4000 PERT cells as independent occurrence-histogram/prior-placement evidence, adds one frozen
stationary SciPy compatibility cell, and normalizes all five total-N=1000 Bayesian recoveries.
Nonseasonal coordinates use empirical central 95% posterior intervals. Seasonal continuous recovery
uses likelihood-native threshold intensity, GPA scale, and Hosking Kappa with effective component
count `N_s = 1000 p_s`, where `p_s = w_s Lambda_s / sum(w_j Lambda_j)` is the fixed-size event-mixture
weight. Intensity uses its Poisson standard error; GPA scale and Kappa use Numerics analytical MLE
covariance scaled to `N_s`; absolute standardized error must not exceed 1.96. R-hat remains below
1.10 and ESS at least 100 for every monitored fitted GEV coordinate, with floored central-95%
inclusion for effective changepoints and identified threshold-intensity/conditional-tail posterior bands.
Every recovery first verifies prior support, a parent-versus-collapsed likelihood ordering, exposure,
threshold, block origin, `Kappa = -xi`, and seasonal annualization. The effective-N oracle correction
changed no seed, prior, sampler default, production algorithm, formula, or parameterization.

| Exact identity | Latest isolated result directory | Result |
|---|---|---|
| `PointProcessLikelihoodOracleTests.NonseasonalMixedObservations_MatchIndependentLikelihood` | `20260830-075825-..._NonseasonalMixedObservations_MatchIndependentLikelihood` | Passed |
| `PointProcessLikelihoodOracleTests.SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood` | `20260830-075831-..._SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood` | Passed |
| `PointProcessPriorTests.Test_CalendarYearPertHistogram_MatchesIndependentPriorPlacementOracle` | `20260830-081024-..._CalendarYearPertHistogram_MatchesIndependentPriorPlacementOracle` | Passed |
| `PointProcessPriorTests.Test_WaterYearPertHistogram_MatchesIndependentPriorPlacementOracle` | `20260830-081031-..._WaterYearPertHistogram_MatchesIndependentPriorPlacementOracle` | Passed |
| `PointProcessExternalPackageOracleTests.StationaryPoissonGpa_MatchesSciPyArtifact` | `20260830-081037-..._StationaryPoissonGpa_MatchesSciPyArtifact` | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalProductionGenerator_RecoversParent` | `20260830-081738-..._NonSeasonalProductionGenerator_RecoversParent` | Passed |
| `PointProcessRecoveryTests.Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints` | `20260830-090132-..._SeasonalProductionGenerator_RecoversParentAndBothChangePoints` | Passed |
| `PointProcessRecoveryTests.Test_SeasonalProductionGenerator_WithUnequalIntensities_RecoversParentAndBothChangePoints` | `20260830-090050-..._UnequalIntensities_RecoversParentAndBothChangePoints` | Passed |
| `PointProcessRecoveryTests.Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | `20260830-090216-..._CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | Passed |
| `PointProcessRecoveryTests.Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | `20260830-090303-..._WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail` | `20260830-082100-..._NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail` | Passed |
| `PointProcessRecoveryTests.Test_SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails` | `20260830-082119-..._SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails` | Passed |
| `PointProcessRecoveryTests.Test_SeasonalSimulation_WithUnequalIntensities_MatchesSeasonRatesAssignmentsAndConditionalTails` | `20260830-082125-..._UnequalIntensities_MatchesSeasonRatesAssignmentsAndConditionalTails` | Passed |

Aggregate exact outcome: **13 passed, 0 failed, 0 skipped**. Equal-intensity effective counts are
`508.1967213114754` and `491.8032786885246`. For the 12-versus-4 unequal-intensity fixture, combining
exposure and intensity gives event-mixture effective counts `756.09756097561` and
`243.90243902439`. All pre-MCMC diagnostics, effective-N Poisson-GPA standardized errors, raw-coordinate
R-hat/ESS checks, changepoint credible sets, and response bands pass.

Artifact: `verification/data/point-process/stationary-poisson-gpa-scipy-oracle.json`, generated by
`verification/python/point-process/generate_point_process_scipy_oracle.py` with Python 3.12.13,
SciPy 1.17.1, and NumPy 2.5.2. SHA-256 values are
`54a57ea459ba1a71dc9e828672dda83386ed5e137348f9c2740567ba242d7d5b` and
`0d085434034e484a94dc5db3e212127f9043372e376dcbc2904ae7e650a9af56`, respectively. The
artifact is stationary-only; seasonal and block-origin claims remain independently analytical.

## Ownership rule

Fast behavior, validation, serialization, property, state, event, exception, and regression tests belong in `RMC.BestFit.Tests`. Numerical verification remains only when a test uses an analytical, external, published, independently implemented, recovery, or coverage oracle.

## Verification completeness Chunk 6A distribution-fitting recovery preparation - 29 August 2026

`DistributionFitting/FittingAnalysisRecoveryTests.cs` now contains 15 separately named generated-parent
cells, one per supported default `FittingAnalysis` candidate. Every cell has scalar sample size 1,000,
seed 12345, an explicit parent vector, a recovered generating-family fit-success assertion, and no
information-criterion or RMSE-rank assertion. The regular families use fitted Numerics
`ParameterCovariance(1000, MaximumLikelihood)` standardized-error acceptance. LnNormal compares
the parent and actual distributions in covariance coordinates `(Mu, Sigma^2)`, while Pearson Type
III and Log-Pearson Type III compare them in MLE covariance coordinates `(Mu, 1/Beta, Alpha)`;
none of those covariance matrices is transformed. Generalized Pareto
uses the predeclared Q(0.99) response band for its zero-location design; and Generalized Logistic,
Generalized Normal, and Kappa Four use same-data auxiliary profile intervals because their variance
APIs are unavailable. The secondary five-percent check remains conditional on a sufficiently narrow
nonzero-parent band.

Authorized one-method-at-a-time execution on 29 August 2026 initially produced 12 reviewed passes:
Normal, LogNormal, LnNormal, Exponential, Gamma, Generalized Extreme Value, Generalized Pareto,
Gumbel, Logistic, Log-Pearson Type III, Pearson Type III, and Weibull. Generalized Logistic,
Generalized Normal, and Kappa Four first exposed unsupported or nonconvergent parameter-0 profile
evaluations after their generating-family `FittingAnalysis` and auxiliary MLE completed.

Haden Smith authorized an RMC.BestFit production profile-method correction. MLE and MAP interval
construction now searches for finite model-constrained brackets, calls Numerics `Brent.Bracket` and
`Brent.Solve`, and retries strict nuisance optimization from cached successful profile starts in
deterministic nearest-coordinate order. Final exact reruns of Generalized Logistic, Generalized
Normal, and Kappa Four passed in the isolated `20260829-165506`, `20260829-165528`, and
`20260829-165445` result directories, respectively. All 15 Chunk 6A generated-parent identities are
now **verified**; the solver correction did not change the recovery seed, parent, acceptance rule,
optimizer defaults, convergence requirement, chi-squared threshold, or Brent defaults. On
1 September 2026 the eight fixed-data `FittingAnalysisTests` identities were declassified because
their 1%-10% coordinate bands lacked statistical justification and were redundant with the complete
generated-parent and external-package family matrices. Their bodies and final one-result passing
TRXs remain historical provenance; no result was transferred.
The scientifically distinct 15-cell `UnivariateDistributionMLETests` real-data matrix was retained,
but its 1%-10% coordinate bands were replaced by joint 95% likelihood-ratio regions. All 15 current
identities passed exact one-result runs under `20260901-142634-...` through
`20260901-142721-...`. The one-result `20260901-140711-...Test_LnNormal_MLE` failure was discarded
after it exposed a missing natural-log to physical-moment parameter crosswalk.
`FittingAnalysisRecoveryTests` is class-level `[DoNotParallelize]` because each default-list fitting
run internally parallelizes 15 candidates, avoiding 15 simultaneous nested fitting runs under the
assembly's method-level MSTest parallelization.

## Verification completeness Chunk 3 model-estimation ownership - 28 August 2026

Chunk 3 reapplied the ownership rule to the five cataloged ModelEstimation sources without changing production code, algorithms, priors, samplers, seeds, optimizer behavior, or tolerances.

| Source | Chunk 3 disposition | Remaining work or evidence boundary |
|---|---|---|
| `MLEIntegrationTests.cs` | Retained 12 N=1000 generating-family recovery methods; renamed the LnNormal cell as a same-sample closed-form MLE comparison; removed small-sample completion, likelihood-sign, and repeated-run smoke cells without fast replacements | Chunk 5 must normalize the 12 recovery acceptance rules and add distinct LnNormal generating-parent recovery |
| `GeneralizedMethodOfMomentsRecoveryTests.cs` | Removed the empty source after deleting four optimizer-success and same-production-path cells; the four R-backed specification methods and two independent objective-gradient methods remain | Genuine N=1000 GMM recovery remains open for Chunk 5 |
| `ProfileLikelihoodGridPointFailureTests.cs` | Retained MLE and flat-prior MAP supported-grid comparisons against the independently derived correlated-quadratic profiles; removed NaN-placement and confidence-interval throw assertions | Both renamed identities passed fresh one-result guarded runs under `20260831-200518-...` and `20260831-200524-...`; no old pass was transferred. No fast failure-policy test was added because the behavior has no public seam that avoids an estimator and nuisance optimizer |
| `JointPriorSamplingVerificationTests.cs` | Retained only the analytical independent-marginal moment characterization as an accepted limitation; removed the same-production-path fitness equality | `PriorPredictiveSamplingContractTests.SampleFromPriors_StoresNegativeFullModelPriorLogLikelihood` now protects the deterministic full-prior fitness sign and inclusion contract without running an estimator |
| `BayesianAnalysisRecoveryTests.cs` | Replaced both N=100 fixtures with N=1000; made the interval method assert actual generating-parameter inclusion; replaced the qualitative prior-shift ordering with an independently calculated known-scale Normal-Normal posterior mean, scale, and interval oracle | Chunk 5 still owns central-95% recovery, R-hat, and ESS normalization; the new conjugate method requires an approval-gated focused run before verified status |

No Verification method was executed for this ownership cleanup. Historical focused results later in this inventory remain historical and were not rewritten as current executions.

## Initial migration - 24 July 2026

| Verification source | Disposition | Fast-test destination or coverage |
|---|---|---|
| `ModelEstimation/InfluenceDiagnosticsTests.cs` | Removed as unit/DTO coverage | Existing `Diagnostics/InfluenceDiagnosticsTests.cs` |
| `ModelEstimation/PointwiseLogLikelihoodTests.cs` | Removed as decomposition/unit coverage | Existing univariate, bivariate, time-series, mixture, point-process, and spatial unit tests |
| `ModelEstimation/PredictiveChecksTests.cs` | Removed as unit/regression coverage | Existing expanded prior/posterior predictive and result DTO unit tests |
| `ModelEstimation/FitVarianceInfluenceTests.cs::Test_Serialization_RoundTrip` | Removed as unit/DTO coverage | Existing `Diagnostics/LeverageDiagnosticsTests.cs` XML round-trip coverage |
| `DistributionFitting/FittingAnalysisTests.cs` | Split | Deterministic state/event/regression cases moved to `FittingAnalysisRegressionTests`; published-data comparisons were initially retained, then declassified on 1 September 2026 when their arbitrary coordinate bands were superseded by statistical generated-parent and likelihood-region evidence |


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
| `BivariateDistributionMLETests` | Historical fixture bodies retained as provenance only; all 12 fixed-`1e-3` identities consolidated into the independent `CopulaEstimationOracleTests` likelihood-region matrix |
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
| `ProfileLikelihoodVerificationTests.MLE_ProfileLikelihood_MatchesRTrueProfile` | Verification | R `bbmle` 1.0.25.1 plus closed-form nuisance reoptimization | Passed - exact focused method |
| `ProfileLikelihoodVerificationTests.MAP_ProfileLikelihood_WithFlatPriors_MatchesRTrueProfile` | Verification | R `bbmle` profile plus constant flat-prior shift | Passed - exact focused method |
| `ProfileLikelihoodVerificationTests.MAP_ProfileLikelihood_WithInformativePrior_ProfilesFullPosteriorKernel` | Verification | Closed-form informative-prior nuisance optimum | Passed - exact focused method |
| `CovarianceFailureStatusTests.MaximumLikelihood_SingularHessian_ReportsFailureAndThrows` | Fast unit | Singular Hessian explicit failure contract | Passed |
| `CovarianceFailureStatusTests.MaximumAPosteriori_SingularHessian_ReportsFailureAndThrows` | Fast unit | Singular Hessian explicit failure contract | Passed |
| `CovarianceFailureStatusTests.GeneralizedMethodOfMoments_MomentFailure_ReportsFailureAndThrows` | Fast unit | Forced exception through public `Try` and throwing getter | Passed |
| `CovarianceFailureStatusTests.MaximumLikelihood_WellConditionedHessian_ReportsAvailable` | Fast unit | Unmodified finite positive-definite covariance | Passed |
| `CovarianceFailureStatusTests.MaximumLikelihood_NonsymmetricCandidate_ReportsRegularized` | Fast unit | Visible positive-definite covariance repair | Passed |
| `CovarianceFailureStatusTests.CovarianceComputationStatus_ValuesAreStable` | Fast unit | Stable public enum values | Passed |
| `JointPriorSamplingVerificationTests.SampleFromPriors_SoftJointPrior_CurrentlyDrawsIndependentMarginals` | Verification | Analytical independent marginals with a soft coupled prior term | Passed - exact focused method |

The profile tests consume the committed [profile-likelihood oracle](../../verification/data/model-estimation/profile-likelihood-oracle.json); neither R nor Python is required at C# test runtime. The six TR-027 methods ran within the safe fast unit project. The four verification methods in this section were run individually through the exact-method script; the full Verification project was not executed.

## TR-026, TR-032, and TR-034 verification - 26 July 2026

| Test method | Oracle or traced contract | Status |
|---|---|---|
| `GmmSpecificationVerificationTests.HansenJ_MatchesRGmmSelectedWeightStatistic` | R `gmm::specTest` 1.9.1 and selected-weight objective identity | Passed - exact focused parameter/objective/J/p-value parity |
| `GmmSpecificationVerificationTests.OveridentifiedOneStep_MatchesRGmmFixedWeightOracle` | R `gmm` 1.9.1 fixed positive-definite weighting matrix | Passed - exact focused parameter/objective parity and `NaN` Hansen scope |
| `GmmSpecificationVerificationTests.OveridentifiedTwoStepSandwichCovariance_MatchesRGmmOracle` | R `gmm` 1.9.1 `vcov()` plus analytical centered IID sandwich | Passed - exact focused covariance parity |
| `GmmSpecificationVerificationTests.OveridentifiedFixedWeightSandwichCovariance_MatchesRGmmOracle` | R `gmm` 1.9.1 arbitrary-fixed-weight IID sandwich plus analytical reconstruction | Passed - exact focused covariance parity |
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

## Chunk 10A mixture reconciliation - 30 August 2026

The six recovery identities generate exactly 1,000 observations through
`MixtureModel.GenerateRandomValues(1000, 12345)`. The `_Parity` names are retained for identity
continuity, but same-ecosystem parity is no longer their scientific oracle. EM recovery uses
ascending-mean labels and the responsibility-count/observed-likelihood covariance. Bayesian
recovery reconstructs the final physical weight in every draw and applies central-95% parent
inclusion plus R-hat below 1.10 and ESS at least 100.

| Test method | Oracle or recovery contract | Status |
|---|---|---|
| `MixtureRecoveryTests.NormalMixture2D_Recovery_Parity` | Two-component generated-parent EM recovery | Passed - 0.373 s |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_Recovery_Parity` | Separate atom plus positive-hurdle component EM recovery | Passed - 0.744 s |
| `MixtureRecoveryTests.NormalMixture3D_Recovery_Parity` | Three-component generated-parent EM recovery | Passed - 1.371 s |
| `MixtureRecoveryTests.NormalMixture2D_BayesianRecovery` | Full-K posterior reconstruction, central-95% parent inclusion, diagnostics | Passed - 1:47.770 |
| `MixtureRecoveryTests.ZeroInflatedNormalMixture2D_BayesianRecovery` | Atom plus positive-mass full-K posterior reconstruction and diagnostics | Passed - 4:04.938 |
| `MixtureRecoveryTests.NormalMixture3D_BayesianRecovery` | Full-K posterior reconstruction, central-95% parent inclusion, diagnostics | Passed - 4:05.235 |
| `MixtureExternalPackageOracleTests.OrdinaryNormalMixture2D_MatchesScikitLearnArtifact` | Frozen scikit-learn fit plus independent likelihood/CDF values | Passed - 0.242 s |

The external artifact uses Python 3.12.13, NumPy 2.5.2, SciPy 1.18.1, and scikit-learn 1.9.0;
the package overlap excludes the unsupported zero-hurdle law. Every method above produced exactly
one inspected TRX result. Three earlier post-edit Bayesian TRXs used unsorted arrays while declaring
them sorted to the percentile routine; those false failures were discarded and replaced by the
passing exact reruns above. No estimator, likelihood, prior, sampler, seed, or tolerance changed,
and the full Verification project was not run.

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

### Competing-risk identifiable recovery - 30 August 2026

The historical 20-cell matrix was replaced by four BestFit methods over three N=1000,
seed-12345 dog-leg fixtures. Verification-only diagnostics require every component to have at
least 15 percent theoretical cause share, 100 hard wins, 100 likelihood-responsibility soft
events, 10 percent dominance mass, and an interior responsibility crossover before fitting.
Same-family Weibull coordinates are ordered by increasing shape. MLE uses full-likelihood
observed information without a ridge and requires absolute standardized error at most 1.96;
Bayesian recovery requires central-95-percent parent inclusion, R-hat below 1.10, and ESS at
least 100.

| Exact method | Fixture | Status |
|---|---|---|
| `CompetingRiskRecoveryTests.MLE_Minimum_TwoWeibullDogLeg_RecoversParent` | Independent minimum Weibull(50,1) + Weibull(80,3); hard wins 720/280 | Passed; one-result TRX `20260830-150518-...` |
| `CompetingRiskRecoveryTests.Bayesian_Minimum_TwoWeibullDogLeg_RecoversParent` | Same identified fixture | Passed; one-result TRX `20260830-150532-...` |
| `CompetingRiskRecoveryTests.MLE_Maximum_WeibullGumbelDogLeg_RecoversParent` | Independent maximum Weibull(100,3) + Gumbel(80,20); hard wins 495/505; crossovers 0.474/0.987 | Passed; one-result TRX `20260830-150439-...` |
| `CompetingRiskRecoveryTests.Bayesian_Maximum_WeibullGumbelDogLeg_RecoversParent` | Same identified maximum with bounded parameter priors and optional Jeffreys scale multiplier disabled | Passed; one-result TRX `20260830-154621-...` |
| `CompetingRiskRecoveryTests.MLE_Minimum_CorrelatedTwoWeibullDogLeg_RecoversParent` | Fixed-rho=0.6 minimum Weibull(50,1) + Weibull(80,3); hard wins 776/224 | Passed; one-result TRX `20260830-151259-...` |

No correlated Bayesian method exists or ran. The Bayesian maximum identity passes when its
optional Jeffreys scale multiplier is disabled; its earlier one-result default-prior diagnostic
remains a failure finding because the production MAP selected a disappeared-Weibull boundary
mode with rank-deficient information and a 95 percent Weibull-scale interval excluding the parent.
The production default remains unchanged. A balanced
three-Weibull candidate passed the
pre-fit cause-share gates but failed coordinate identification in both MLE implementations and
BestFit Bayesian recovery, so its three proposed cells were removed rather than tuned. Numerics
retains the three matching MLE tests: both minima and the redesigned maximum pass individually on
net481/net8/net9/net10. This
cross-implementation agreement is a diagnostic finding, not an independent scientific oracle.

The older identities and results below this checkpoint are historical only; no pass was
transferred across a rename.
### Composite report and product-posterior recovery

| Exact method | Oracle | Status |
|---|---|---|
| `CompositeOracleVerificationTests.MixtureCdf_MatchesExactWeightedNormalSum` | exact weighted three-Normal CDF | Passed - 0.324 s |
| `CompositeOracleVerificationTests.MixtureQuantiles_MatchPublishedRMistrTable45` | 25 published R `mistr` Table 45 quantiles | Passed - 0.319 s |
| `CompositeOracleVerificationTests.MixtureQuantiles_InvertAnalyticWeightedNormalCdf` | direct Normal CDF and probability-dependent inversion bound | Passed - 0.320 s |
| `CompositeOracleVerificationTests.MaximumComposite_MatchesIndependentAndComonotonicClosedForms` | independent product and comonotonic minimum identities | Passed - 0.390 s |
| `CompositeOracleVerificationTests.MinimumComposite_MatchesIndependentAndComonotonicClosedForms` | independent union and comonotonic maximum identities | Passed - 0.391 s |
| `CompositeOracleVerificationTests.CombinationRules_SatisfyTheoreticalBracketingAndRemainDistinct` | mixture/maximum/minimum brackets and material separation | Passed - 0.474 s |
| `CompositeOracleVerificationTests.MixturePosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 mixture posterior | Passed - 3.555 s |
| `CompositeOracleVerificationTests.MaximumPosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 independent maximum posterior | Passed - 3.228 s |
| `CompositeOracleVerificationTests.MinimumPosterior_MatchesCompleteCartesianOracle` | complete 20-by-20-by-20 independent minimum posterior | Passed - 3.394 s |
| `CompositeOracleVerificationTests.CorrelationMatrix_MinimumAndMaximumMatchBivariateNormalOrthants` | analytical bivariate-Normal median orthants at latent rho 0.6 | Passed - 0.296 s |
| `CompositePredictiveRecoveryTests.MixtureComposite_EndToEndPredictiveRecovery` | two fitted N=1000 Normal children; 0.35/0.65 analytical parent quantiles | Passed - 15.711 s |
| `CompositePredictiveRecoveryTests.MaximumComposite_EndToEndPredictiveRecovery` | two fitted N=1000 Normal children; independent maximum parent quantiles | Passed - 15.380 s |
| `CompositePredictiveRecoveryTests.MinimumComposite_EndToEndPredictiveRecovery` | two fitted N=1000 Normal children; independent minimum parent quantiles | Passed - 14.958 s |
| `CompositePredictiveRecoveryTests.EqualWeightModelAverage_EndToEndPredictiveRecovery` | two fitted N=1000 Normal children; exact equal weights and analytical parent quantiles | Passed - 15.210 s |

The three posterior methods use explicit 5,000-draw `MCMCResults`, 20 deterministic mean supports
per child, seed 20260803, five nonexceedance probabilities, 90% limits, mean tolerance `0.02`, and
limit tolerance `0.05`. Each also requires the fixed parent curve to remain inside its band.
Analytical formulas and the short published table are embedded; no new oracle artifact is added.

The ten oracle identities pass under their current `CompositeOracleVerificationTests` names;
historical `CompositeRecoveryTests` results were not transferred. All four predictive-recovery
identities also pass, for 14/14 Chunk 10B cells. The first predictive-mixture attempts exposed an
unsorted-percentile helper error and then a descending probability grid; both one-result failure
TRXs were discarded before the final passing exact rerun. No sampler, seed, prior, parent, formula,
or acceptance rule changed.

## Phase 5 compatibility guardrails

| Fast method | Project | Contract | Status |
|---|---|---|---|
| `PublicApiCompatibilityTests.PublicApi_MatchesCapturedBaseline` | UI.Tests | 853-line exported public/protected UI signature baseline | Passed - UI project 576/576 |
| `PublicApiCompatibilityTests.PublicApi_MatchesCapturedBaseline` | App.Tests | 1,657-line exported public/protected App signature baseline | Passed - App project 431/431 |
| `TimeSeriesModelSerializationCompatibilityTests.*` | UI.Tests | Legacy AR, MA, ARIMA, and ARIMAX XML settings plus unknown optional attribute; the Yeo-Johnson exponent refit from the training window is pinned | Passed - 4 methods |

These are deterministic compatibility regressions, not numerical Verification methods. Baseline
hashes and command evidence are recorded in [Time-Series Verification](time-series.md). The App
transform-selector binding and item list are documented in the technical reference; source-text
regressions that only matched App source files were removed because they did not exercise behaviour.

## TR-035 time-series Jeffreys metadata

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesPriorMetadataTests.PointwisePriorMetadata_ClassifiesExactlyOneJeffreysScaleComponentWhenEnabled` | Core Tests | One scale component when enabled, none disabled, correct identity/type/density in AR, MA, ARIMA, and ARIMAX | Exact metadata; `1E-12` density; passed in Core 3,183/3,183 |
| `TimeSeriesPriorMetadataTests.PointwisePriorMetadata_SumsToScalarPriorLikelihood` | Core Tests | Decomposed sum equals scalar prior | `1E-12`; passed |
| `TimeSeriesPriorMetadataTests.ARIMAX_JeffreysScaleMetadata_IsTheReferenceForOtherModels` | Core Tests | Unchanged ARIMAX name, type, and value | Exact name/type; `1E-12` value; passed |
| `TimeSeriesIndependentOracleTests.JeffreysScaleMetadataMatchesIndependentPriorOracle` | Verification | Committed analytical $-\log(\sigma)$ oracle at four fixed scales | `1E-12`; guarded pass 1/1 |

The Verification method is numerical; the three state/decomposition contracts remain in the
fast project. No optimizer, sampler, recovery fixture, or production generator is invoked.

## TR-040 invalid time-series innovation scale

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesInvalidScaleTests.InvalidInnovationScale_ReturnsNegativeInfinityAcrossAllPaths` | Core Tests | Five invalid-scale rows across AR, MA, ARIMA, ARIMAX scalar/pointwise/component/prior paths; metadata and lengths retained | Exact negative infinity and metadata; 5 passing rows |
| `TimeSeriesInvalidScaleTests.FinitePositiveInnovationScale_RetainsValidEvaluation` | Core Tests | Representative finite-positive control | Scalar/pointwise parity; passed |
| `TimeSeriesIndependentOracleTests.InvalidScaleBehaviorMatchesScalarAndPointwiseOracle` | Verification | Independent Gaussian, uniform-normalization, and Jeffreys formulas plus invalid domain | `1E-12` valid; exact negative infinity invalid; guarded pass 1/1 |

The complete Core project passes 3,189/3,189. The Verification method is the only TR-040 method
run from the Verification project.

## TR-036/TR-046 time-series transform lifecycle

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesTransformStateTests.*` | Core Tests | Read-only/non-browsable getter; atomic rebuild; training-prefix holdout isolation; automatic/manual provenance; XML/clone; canonicalization; ignored `lambda2`; invalidation | Exact state and `1E-12`; passed in Core 3,201/3,201 |
| `TimeSeriesModelSerializationCompatibilityTests.ManualTransformLambda_NewXml_RoundTripsAllModelTypes` | UI.Tests | New optional XML state across AR, MA, ARIMA, and ARIMAX while legacy/unknown-attribute fixtures remain valid | Exact state; passed in UI 578/578 |
| `TimeSeriesAnalysisTests.ManualTransformLambda_CopyUndoAndRedoPreserveEffectiveState` | UI.Tests | Copy and undo/redo preserve effective manual state | Exact state; passed |
| `TimeSeriesIndependentOracleTests.TransformLambdaMatchesIndependentTrainingOnlyOracle` | Verification | R Box-Cox/Yeo-Johnson profile fit on six training values with mutated three-value holdouts | `1E-8` absolute or `1E-7` relative; guarded pass 1/1 |
| `TimeSeriesIndependentOracleTests.ManualTransformLambdaRebuildMatchesIndependentLikelihoodOracle` | Verification | R transform, Jacobian, residual, and conditional Gaussian likelihood recurrence | Same cross-language rule; deterministic identities `1E-12`; guarded pass 1/1 |

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
| `TimeSeriesIndependentOracleTests.ArimaxDifferencedLikelihoodMatchesDateIndexedIndependentOracle` | Verification | Independent R transform, differencing, date join, ARMA recurrence, conditional Jacobian, and Gaussian likelihood for `d=0,1,2` | `1E-10`; guarded pass 1/1 |

The complete final package gates pass Core 3,208/3,208, UI 578/578, App 440/440, and API
498/498. The strict Debug solution build reports zero warnings/errors and UI/App signature
baselines remain exact. The R artifact and generator were committed before C# evaluation.

## TR-037 ARIMA/ARIMAX prediction reintegration

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesPredictionReintegrationTests.AutoRegressivePrediction_ConditionsOnTrainingAndRecursesAfterBoundary` | Core Tests | AR lags use observed training values through the first forecast, then generated values | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.MovingAveragePrediction_ConditionsOnTrainingAndRecursesAfterBoundary` | Core Tests | MA residuals remain observation-conditioned through training and expire after the boundary | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaD1Prediction_ConditionsOnTrainingAndForecastBoundary` | Core Tests | Irregular `d=1` path uses preceding observed levels inside training and at horizon one | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaD2Prediction_ConditionsOnObservedDifferenceStatesAtBoundary` | Core Tests | Irregular `d=2` path uses observed level/first-difference states, then advances generated states | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaxD1Prediction_ConditionsOnTrainingAndForecastBoundary` | Core Tests | Conditional boundary reconstruction with an exact-date level covariate and holdout sentinel | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.LogArimaD1Prediction_ConditionsOnTransformedTrainingBoundary` | Core Tests | Conditional reconstruction on log scale before one inverse transform | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaD1LinearPrediction_ReintegratesExactLengthAndComponents` | Core Tests | `d=1`, zero/positive horizon, raw length and component `k+d` map | `1E-12`; two passing rows |
| `TimeSeriesPredictionReintegrationTests.ArimaD2QuadraticPrediction_ReintegratesExactRecurrence` | Core Tests | Constant second differences reconstruct square-number levels and a two-slot conditioning prefix | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.ArimaxD1Prediction_UsesDateIndexedLevelCovariateAndReintegrates` | Core Tests | Exact-date level covariate drives first differences at raw slots `k+1` | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.LogTransformedD1Predictions_ReintegrateBeforeInverseTransform` | Core Tests | Exact recurrence on log scale followed by one inverse transform | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.TransformedPredictions_UseOnlyModelScaleLagAndResidualStates` | Core Tests | AR, MA, ARIMA(1,1,1), and ARIMAX(1,1,1) use only log-scale lag/residual/difference states before one inverse transform; raw/transformed scale separation is deliberately large | `1E-12`; passed |
| `TimeSeriesPredictionReintegrationTests.NoneD0FixedSeedPrediction_RetainsGoldenArraysBitForBit` | Core Tests | Pre-change ARIMA/ARIMAX `Transform.None`, `d=0` values and every component vector | Exact double equality; passed |
| `TimeSeriesIndependentOracleTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle` | Verification | Irregular ARIMA `d=2` and logarithmic ARIMAX `d=1` hand recurrences distinguish observed training states, the first forecast anchor, later recursion, and holdout exclusion | `1E-10`; guarded pass 1/1 |
| `TimeSeriesIndependentOracleTests.ArimaAndArimaxPredictionUncertaintyBeginsAtForecastBoundary` | Verification | Exactly 1,000 fixed seeds; conditional training and horizon-one variance `1`, then random-walk forecast variances `2` and `3` | Four Monte Carlo standard errors with 3% variance floor; guarded pass 1/1 |
| `TimeSeriesIndependentOracleTests.TransformedArimaAndArimaxForecastsMatchModelScaleOracle` | Verification | Independent Yeo-Johnson plus conditional ARMA(1,1) recurrence; exactly 1,000 fixed seeds verify accumulated transformed-level horizon variance before inverse transformation | `1E-10` recurrence; four Monte Carlo standard errors with 3% variance floor; guarded pass 1/1 |

The transformed-scale audit gates pass Core 3,238/3,238, UI 578/578, App 444/444, and API 498/498.
The strict Debug solution build reports zero warnings/errors, and UI/App signature baselines remain
exact. No external artifact is required for the embedded analytical recurrences; source hashes,
the complete-path failure history, and exact command/TRX evidence are recorded in
[Time-Series Verification](time-series.md).

## TR-038 AR/MA/ARIMA transformed generation

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `TimeSeriesGenerationTransformTests.ArLogarithmicGeneration_InverseTransformsCompletedModelRecurrence` | Core Tests | Fixed-seed AR(2) model-scale recurrence followed by one exponential inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.MaBoxCoxGeneration_InverseTransformsCompletedModelRecurrence` | Core Tests | Fixed-seed MA(2) recurrence followed by manual-lambda Box-Cox inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaYeoJohnsonD1Generation_IntegratesThenInverseTransforms` | Core Tests | Fixed ARMA differences, observed transformed anchor, complete integration, then Yeo-Johnson inverse | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaD2Generation_UsesObservedOrZeroTransformedAnchors` | Core Tests | Attached first two transformed levels versus zero-anchor polynomial | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.ArimaGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors` | Core Tests | Requested observed/zero anchors only, exact length, no model-scale values | `1E-12`; passed |
| `TimeSeriesGenerationTransformTests.NoneD0FixedSeedGeneration_RetainsGoldenArraysBitForBit` | Core Tests | Pre-change AR, MA, and ARIMA `Transform.None`/`d=0` arrays | Exact double equality; passed |
| `TimeSeriesIndependentOracleTests.ArAndMaTransformedGeneratorsMatchIndependentOracle` | Verification | Independent exponential/Box-Cox algebra plus 1,000-step model-scale Gaussian moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |
| `TimeSeriesIndependentOracleTests.ArimaDifferencedTransformedGeneratorMatchesIndependentOracle` | Verification | Independent Yeo-Johnson/integration algebra plus 1,000 generated steps/999 first-difference moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |

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
| `TimeSeriesIndependentOracleTests.ArimaxTransformedDifferencedGeneratorMatchesIndependentOracle` | Verification | Fixed algebraic Yeo-Johnson recurrence plus 1,000 generated steps/999 innovation moments | `1E-10` algebra; four-SE/3% moments; guarded pass 1/1 |

The package gates pass Core 3,226/3,226, UI 578/578, App 440/440, and API 498/498. The final
serial strict Debug build reports zero warnings/errors; UI/App signature baselines remain exact.
The initial Verification compile failure and parallel-build file-lock failure are retained in the
time-series report. No full Verification run occurred.

## TR-042 information-criterion regression

| Method | Project | Oracle or contract | Tolerance/status |
|---|---|---|---|
| `AnalysisInformationCriteriaRoutingTests.TimeSeriesCriteria_UseOneDataLikelihoodCallAtMap` | Core Tests | Injected MAP and counting AR model prove one data-likelihood call, no prior/posterior call, and hand AIC/BIC routing | Exact call counts; `1E-10`; passed |
| `TimeSeriesIndependentOracleTests.InformationCriteriaUseDataLikelihoodAtMapAndExcludePrior` | Verification | AR, MA, ARIMA, ARIMAX, and rating-curve data-only criteria (time-series likelihoods checked against an independent iid Gaussian evaluation) plus production MLE/MAP recovery of the analytical flat-prior Gaussian optimum | Criteria `1E-10`; MLE/MAP coordinates within known Normal central 95% information intervals and joint 95% likelihood-ratio region |

The exact criteria cells use 40 or fewer observations and one injected posterior row; the same
method also runs the declared flat-prior Gaussian MLE and MAP fits used by its analytical optimum
cross-check. It runs no sampler or simulation and remains below the 1,000-step cap. Package gates pass Core
3,227/3,227, UI 578/578, App 440/440, and API 498/498. UI/App signature baselines remain exact and
the strict serial Debug build has zero warnings/errors. Fixture and infrastructure failure history
is retained in the time-series report. No full Verification run occurred.

## Phase 5 integrated recovery matrix

| Method | Oracle or recovery contract | Status |
|---|---|---|
| `AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1` | Independent R AR(1), 110-step burn-in, 1,000 retained observations, seed 12345, observed-information standardized errors, finite likelihood/prior, one-step recurrence | **Passed** - final approved DE configuration, 1/1 under `20260831-192951-...`; earlier boundary failures retained |
| `ARAnalysisTests.Test_EstimateParameters_AR1` | Same fixture; unchanged production DEMCzs defaults asserted before/after; independently calculated central 95%, MAP 25%, R-hat/ESS, one-step recurrence | **Passed** - 1/1 in 24.611 s |
| `MovingAverageMLERecoveryTests.Test_EstimateParameters_MA1` | Independent R MA(1), 110-step burn-in, 1,000 retained observations, seed 12345, unchanged 5% gate | **Passed** - 1/1 in 0.198 s |
| `MAAnalysisTests.Test_EstimateParameters_MA1` | Same fixture and unchanged production-default Bayesian recovery contract | **Passed** - 1/1 in 24.097 s |
| `TimeSeriesIndependentRecoveryTests.MleArima111LogD1RecoversGeneratingParameters` | Independent R conditional ARIMA(1,1,1) optimum, profile intervals, logarithmic Jacobian, 1,000 observations, seed 51037; same-point likelihood and forecast level conditioned on the final observed training state | **Passed** - 1/1 against the direct conditional oracle |
| `TimeSeriesIndependentRecoveryTests.BayesianArima111LogD1RecoversGeneratingParameters` | Same fixture; unchanged DEMCzs defaults; truth in central 95%; sampled MAP versus independent default-prior posterior MAP; R-hat/ESS and prediction | **Passed** - 1/1 in 30.642 s |
| `TimeSeriesIndependentRecoveryTests.MleArimax10D1LevelCovariateRecoversGeneratingParameters` | Independent R conditional ARIMAX(1,1,0), dated level covariate, 1,000 observations, seed 51038, same-point likelihood; unchanged production Differential Evolution default | **Passed** - 1/1 in 0.516 s |
| `TimeSeriesIndependentRecoveryTests.BayesianArimax10D1LevelCovariateRecoversGeneratingParameters` | Same fixture; unchanged DEMCzs defaults; truth in central 95%; sampled MAP versus independent default-prior posterior MAP; R-hat/ESS and prediction | **Passed** - 1/1 in 37.241 s |

The artifact was committed before C# recovery evaluation. The mistakenly changed AR/MA seeds,
corrected-seed fixture without stationary burn-in, and artificially capped AR Bayesian run remain
failure history. Commit `ccd5842` removed the cap and all time-series Verification assignments to
`BayesianAnalysis` settings. The independent ARIMA/ARIMAX conditional MLE and posterior-MAP oracles
were committed before their final C# evaluations. Formula comparisons use common parameter vectors;
sampled Bayesian point recovery uses `Results.MAP`, while generating truth remains a central-95%
coverage criterion. The forced bounded-Nelder-Mead ARIMAX failure is retained; the passing cell uses
the unchanged production Differential Evolution default and changes no optimizer implementation or
default. All eight cells pass. No alternate seed, tolerance, sampler setting, or full Verification
run was attempted. See the time-series report for hashes, exact commands, runtimes, and history.

## Estimation and diagnostics corrections - 21 August 2026

| Test | Project | Contract | Status |
|---|---|---|---|
| `Diagnostics/InfluenceDiagnosticsParetoKLimitTests.cs` | Fast core | An unestimated (NaN) Pareto k counts above every reliability limit and is never reliable; `GetProblematicObservations()` defaults to the instance limit and includes unestimated values | Passed |
| `ModelEstimation/PsisDegenerateTailDiagnosticTests.cs` | Fast core | Degenerate PSIS tails (five-ratio tail, tied lower-quartile excesses) report `k = +inf` and unreliable diagnostics; fewer than eleven retained draws fall back to the fixed 0.7 limit without a negative serialized threshold; forty draws serialize the draw-count limit | Passed |
| `DataFrame/DataFrameLambdaTests.cs` | Fast core | Replacing the exact series of a peaks-over-threshold frame keeps the rate per observed year | Passed |
| `ModelEstimation/GeneralizedMethodOfMomentsRestoredStateTests.cs` | Fast core | Restored out-of-scope J statistics read as NaN; `PostProcess()` on a restored estimator computes covariance and keeps the restored statistic; covariance queries leave `S`, `W`, and `Q` unchanged; `PostProcess()` refreshes `S`/`W` at the estimate | Passed |
| `DistributionFitting/FittingAnalysisProgressTests.cs` | Fast core | A fitting run in which no candidate fits reports completion to the progress reporter | Passed |
| `ModelEstimation/ProfileLikelihoodGridPointFailureTests.cs` | Verification | Profile grid points without a finite nuisance optimum are NaN while the remaining points equal the unrestricted profile; `ParameterConfidenceIntervals()` still requires converged solves (MLE and flat-prior MAP) | Passed 2/2 |
| `ModelEstimation/MaximumLikelihoodCovarianceVerificationTests.cs` | Verification | One-parameter MLE covariance equals the closed-form `sigma^2 / n`; MLE and flat-prior MAP report the same interior covariance within the 1e-4 numerical-Hessian tolerance | Passed 2/2 |

## Bulletin 17C bootstrap diagnostics and reporting - 21 August 2026

| Test | Project | Contract | Status |
|---|---|---|---|
| `Support/BootstrapDiagnosticsTests.cs` (merged; `Diagnostics/BootstrapDiagnosticsTests.cs` removed) | Fast core | Per-replicate substitution rate, retries, and evaluations; realization count; bound-repair and z-limit clip counters; XML round trip including legacy files that stored realizations under `AttemptedReplicates` | Passed |
| `Univariate/Bulletin17CReportDiagnosticsTests.cs` | Fast core | Report lists realizations attempted, substituted replicates with the point-mass note, per-replicate discard warnings, bound repairs, and z-limit clips | Passed |
| `Univariate/Bulletin17CDistributionTests.cs::GetRankedBootstrapInitialValues_CensoredSample_ReturnsObjectiveOrderedCandidates` | Fast core | Ranking objective equals the penalized identity-weight moment objective with the regional-skew penalty enabled; candidates ordered | Passed |
| `Univariate/Bulletin17CTests/B17CCoverageTests.cs` | Verification | Coverage assertions re-enabled (completion >= 90%, mean coverage in [0.82, 0.97], per-ordinate coverage >= 0.70; binomial 95% band at B = 1,000 stated for reference) | Assertions re-enabled; not rerun in this round |
| `Univariate/Bulletin17CTests/B17CBootstrapRefitReliabilityTests.cs` | Verification | Zero retries asserted through `AttemptedRealizations`; optimizer status counts cover every realization; no substituted replicates | Passed 14/14 |
| `Univariate/Bulletin17CTests/B17CSyntheticDataTests.cs` | Verification | Methods renamed `*_MatchesProductMomentParameters` (GMM versus sample product-moment parameters) | Passed 6/6 |
| `Univariate/Bulletin17CTests/B17CCovarianceTests.cs` | Verification | Absolute tolerance floor applies to off-diagonal entries only | Passed except the pre-existing `PearsonTypeIII_Covariance_N25/N100` diagonal mismatches, which fail identically at the pre-review commit |
| `Univariate/Bulletin17CTests/B17CExampleTests.cs` | Verification | Example 4/7 uncertain-data messages and tolerance rationale corrected | Passed 13/13; the three `HirschStedingerPlottingPositionVerificationTests` peakFQ cells pass |
| `Univariate/Bulletin17CTests/B17CCohnEtAlCoverageTests.cs` | Verification | Documented as completion-rate checks; Table 3 coverage values are not asserted | Unchanged contract |

## Default MCMC settings in Verification recovery tests - 21 August 2026

All Verification recovery tests use the default `BayesianAnalysis` simulation settings; the
overrides in `CoincidentFrequencyAnalysisTests`, `BivariateAnalysisParameterRecoveryTests`,
`MixtureRecoveryTests`, `NonstationaryValidationTests`, and `BayesianAnalysisRecoveryTests` were
removed (3-chain configurations were rejected by `BayesianAnalysis.Validate()`). Each method was
run in its own `dotnet test` invocation.

| Test | Result with default settings |
|---|---|
| `CoincidentFrequencyAnalysisTests` (3 closed-form sum-of-Normals cells) | Passed 3/3 (46-57 s each) |
| `BivariateAnalysisParameterRecoveryTests` (7 copula families) | Passed 7/7 (4-10 s; Student t 470 s) |
| `MixtureRecoveryTests` (3 parity, 3 Bayesian cells) | Passed 6/6 (Bayesian cells 173-456 s) |
| `NonstationaryValidationTests` (16 trend cells) | Passed 1/16: `ConstantTrend` passes; the other cells miss their 1% relative tolerances (calibrated for 10,000/5,000 iterations) by 1-4%, and `LinearTrend` (slope -0.001 versus 0.5) and `PowerTrend` (3.4 versus 100) miss outright. Left for a decision; no tolerance was re-pinned. |

## Focused reruns after the corrections - 21 August 2026

Each method ran in its own `dotnet test` invocation after the estimation, point-process, composite,
and Bulletin 17C corrections. 133 of the rerun methods passed. Results that were not passes:

| Test | Outcome |
|---|---|
| `B17CCovarianceTests.PearsonTypeIII_Covariance_N25`, `_N100` | Pre-existing diagonal mismatches (36.2 vs 22.4; 3.04 vs 3.92); identical failures at the pre-review commit `7a0a797` |
| `UncertainDataBootstrapVerificationTests.LogPearsonBootstrap_Move3StyleUncertaintyRemainsStable` | Pre-existing: optimizer fallback rate 88.5% against a 1% limit (68.3% at the pre-review commit with the realization denominator) |
| `CompetingRiskRecoveryTests` Bayesian maximum cells (4) | Pre-existing documented failures; not rerun |
| `B17CCensoredCoverageTests.LP3_LowOutliers_N50_Bootstrap`, `LP3_HistoricalThreshold_N50_Bootstrap` | Pre-existing: 996/1000 and 1000/1000 coverage replicates fail to estimate within seconds; identical at the pre-review commit `7a0a797` |

Passing groups: `PsisLooOracleVerificationTests` 4/4, `McmcNumericalVerificationTests` 4/4,
`BayesianAnalysisRecoveryTests` 3/3 (default settings), `PointProcessPriorTests` 2/2,
`FittingAnalysisCriteriaVerificationTests` 3/3, `GeneralizedMethodOfMomentsRecoveryTests` 4/4,
`GmmSpecificationVerificationTests` 4/4, `GmmObjectiveGradientVerificationTests` 2/2,
`GmmInfluenceDiagnosticsVerificationTests` 2/2, `ProfileLikelihoodVerificationTests` 3/3,
`ProfileQRecoveryTests` 1/1, `MLEIntegrationTests` 16/16, `MaximumAPosterioriRecoveryTests` 3/3,
`Log10NormalInfluenceVerificationTests` 2/2, `Log10NormalEstimationEquivalenceTests` 5/5,
`UnivariateDistributionMLETests` 15/15, `GoodnessOfFitRmseVerificationTests` 1/1,
`B17CSyntheticDataTests` 6/6, `B17CCovarianceTests` (all other cells), `B17CPenalityTests`,
`B17CExampleTests` 13/13, `UncertainDataBootstrapVerificationTests` (other cells),
`B17CBootstrapRefitReliabilityTests` 14/14, `PointProcessRecoveryTests` 8/8,
`ProfileLikelihoodGridPointFailureTests` 2/2, `MaximumLikelihoodCovarianceVerificationTests` 2/2.

## Phase 6 prelude - 21 August 2026

Batch 6.0 of the finalization plan completed the register (TR-084 through TR-090 record the 21 August
results that still lack dispositions), closed TR-078 with the 14/14 reliability-grid evidence above and
TR-081 with the reruns below, and restated the TR-052 failure mode. No production code changed, so no
unit-test gate was required. The twelve `B17CPenalityTests` methods were rerun one at a time through
`scripts/run-verification-test.ps1` (TRX files under `TestResults/VerificationFocused/20260821-16*`);
the recorded durations are wall-clock per guarded invocation and include the incremental build step.

| Exact method | Contract | Status |
|---|---|---|
| `B17CPenalityTests.LogNormal_PenalityOnMu_N25` | Log-Normal, mean penalty, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 12.7 s |
| `B17CPenalityTests.LogNormal_PenalityOnMu_N100` | Log-Normal, mean penalty, $n=100$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 4 s |
| `B17CPenalityTests.LogNormal_PenalityOnSigma_N25` | Log-Normal, standard-deviation penalty, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 4.3 s |
| `B17CPenalityTests.LogNormal_PenalityOnSigma_N100` | Log-Normal, standard-deviation penalty, $n=100$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.9 s |
| `B17CPenalityTests.LogNormal_PenalityOnMuAndSigma_N25` | Log-Normal, mean and standard-deviation penalties, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.7 s |
| `B17CPenalityTests.LogNormal_PenalityOnMuAndSigma_N100` | Log-Normal, mean and standard-deviation penalties, $n=100$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.6 s |
| `B17CPenalityTests.LogNormal_PenalityOnQ99_N25` | Log-Normal, 0.99-quantile penalty, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.6 s |
| `B17CPenalityTests.LogNormal_PenalityOnQ99_N100` | Log-Normal, 0.99-quantile penalty, $n=100$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.6 s |
| `B17CPenalityTests.LogNormal_PenalityOnMuSigmaAndQ99_N25` | Log-Normal, mean, standard-deviation, and 0.99-quantile penalties, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.6 s |
| `B17CPenalityTests.LogPearsonTypeIII_PenalityOnMuAndGamma_N25` | Log-Pearson Type III, mean and skew penalties, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.7 s |
| `B17CPenalityTests.LogPearsonTypeIII_PenalityOnAllParams_N25` | Log-Pearson Type III, mean, standard-deviation, and skew penalties, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.7 s |
| `B17CPenalityTests.LogPearsonTypeIII_PenalityOnAllParamsAndQ99_N25` | Log-Pearson Type III, all-parameter and 0.99-quantile penalties, $n=25$: penalized GMM equals the closed-form multivariate inverse-variance weighted combination | Passed - 3.8 s |

Register completion: TR-084 (`NonstationaryValidationTests`, 1/16 under defaults), TR-085
(`B17CCovarianceTests` Pearson III diagonals), TR-086 (Move3-style uncertain-data bootstrap fallback
rate), TR-087 (`B17CCensoredCoverageTests` bootstrap cells), TR-088 (`B17CCoverageTests` re-enabled, not
rerun), TR-089 (`ARIMAAnalysisTests.Test_EstimateParameters_ARIMA22`,
`ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX22`, and
`ARIMAXAnalysisTests.Test_EstimateParameters_ARIMA111`, pre-existing failures), and TR-090 (the
intermittent `UnivariateAnalysisPositivePathReprocessTests` race) point back to the 21 August sections
of this inventory and to `docs/PROGRESS.md`.

## Phase 6 Batch 6.1 rating-curve confirmation - 21 August 2026

Test-first confirmation of the rating-curve findings before any production change. Each Verification
method ran once through `scripts/run-verification-test.ps1`; the fast contract ran through
`dotnet test` with a method filter. The committed artifacts `rating-curve-example-fixtures.json` and
`rating-curve-likelihood-oracle.json` (manifest rows added before these runs) replicate the three
synthetic cases of `examples/6-rating-curve-analysis`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `RatingCurveLikelihoodOracleTests.OneSegment_DataLogLikelihood_IsDischargeSpaceDensity` | Verification | Scalar, pointwise, and component data log likelihood equal the SciPy discharge-space density (`1e-8` sums, `1e-10` terms) | Failed - confirms TR-043; difference `2043.2563714262035` equals the change-of-variables sum |
| `RatingCurveLikelihoodOracleTests.TwoSegment_DataLogLikelihood_IsDischargeSpaceDensity` | Verification | Same | Failed - confirms TR-043; difference `2315.8103643545292` |
| `RatingCurveLikelihoodOracleTests.ThreeSegment_DataLogLikelihood_IsDischargeSpaceDensity` | Verification | Same | Failed - confirms TR-043; difference `2354.0657454049206` |
| `RatingCurveContinuityVerificationTests.AddedControl_TwoSidedIncrementAtActivation_MatchesAnalyticalPowerLaw` | Verification | Analytical two-sided increment at the second activation stage, relative `1e-10` | Passed (second run, after evaluating the formula on the model's floating-point stage values) |
| `RatingCurveContinuityVerificationTests.ZeroExponent_AddedControlJumpsByItsCoefficientAtActivation` | Verification | Zero exponent jumps by `10^a2` | Passed |
| `RatingCurveContinuityVerificationTests.DefaultExponentLowerBounds_AreStrictlyPositive_{One,Two,Three}Segment` | Verification | Default exponent bounds and prior supports exclude zero | Failed - confirms TR-044 (lower bound 0) |
| `RatingCurveContinuityVerificationTests.ExponentsAtDefaultLowerBound_AddedControlIncrementsVanishAtActivation` | Verification | Added increments vanish as the offset shrinks when exponents sit at their lower bound | Failed - confirms TR-044 (constant increment `1.7534628349561987`) |
| `RatingCurveTests.Validate_UnmatchedNonPositiveDischarge_RemainsValidAndIsReported` | Fast core | An unmatched nonpositive discharge record does not invalidate a model with enough valid aligned pairs and is reported | Failed - confirms TR-045 (`Error: All discharge values must be positive`); kept in the working tree until the approved fix lands |
| `RatingCurveExampleRecoveryTests` (6 methods) | Verification | Example replication recovery against the independent SciPy optimum and the true curve | Ready - acceptance rule pending approval; not run |

## Phase 6 Batch 6.2 bivariate evidence consolidation - 21 August 2026

`copula-estimation-oracle.json` (manifest row recorded before the runs) transcribes the twelve
`BivariateDistributionMLETests` fixtures and records an independent SciPy optimum per fixture from
closed-form copula densities that are self-checked against the numerical mixed partial of each
distribution function. Each method below ran once through `scripts/run-verification-test.ps1`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `CopulaEstimationOracleTests.{AliMikhailHaq,Clayton,Frank,Gumbel,Joe,Normal}_PseudoLikelihood_MatchesIndependentOptimum` | Verification | Production DE MPL fit: deterministic same-point likelihood within `1e-8`; production optimum inside the one-coordinate joint 95% LR region; historical R coordinate is provenance only | Passed 6/6 fresh on 1 September 2026 |
| `CopulaEstimationOracleTests.{AliMikhailHaq,Clayton,Frank,Gumbel,Joe,Normal}_InferenceFromMargins_MatchesIndependentOptimum` | Verification | Same contract with Normal marginals set to closed-form MLEs | Passed 6/6 fresh on 1 September 2026 |
| `CopulaEstimationOracleTests.StudentT_PseudoLikelihood_MatchesIndependentOptimum` | Verification | Production DE MPL fit and independent two-coordinate Student-t copula optimum in the joint 95% LR region | Passed fresh on 1 September 2026 |
| `CopulaEstimationOracleTests.StudentT_InferenceFromMargins_MatchesIndependentOptimum` | Verification | Production DE IFM fit and independent two-coordinate Student-t copula optimum in the joint 95% LR region | Passed fresh on 1 September 2026 |
| `AnalysisInformationCriteriaRoutingTests.BivariateCriteria_UseOneDataLikelihoodCallAtMap` | Fast core | Bivariate point-estimate results evaluate the copula data likelihood once at the stored MAP and route it to AIC/BIC (TR-047) | Passed |
| `BivariateAnalysisParameterRecoveryTests` (7 copula families) | Verification | Default-setting Bayesian recovery, recorded in the 21 August default-settings table above | Passed 7/7 |
| `CoincidentFrequencyAnalysisTests` (3 cells) | Verification | Closed-form sum-of-Normals response surface, recorded in the 21 August default-settings table above | Passed 3/3 |

After the final review made the two-point likelihood distance sign-safe, the first twelve identities
passed with one result per TRX under `20260901-143648-...` through `20260901-143722-...`; the two
Student-t identities passed under `20260901-143725-...` and `20260901-143950-...`.

## Phase 6 Batch 6.3 spatial likelihood confirmation - 21 August 2026

`spatial-copula-likelihood-oracle.json` (manifest row recorded before the runs) defines with R
`mvtnorm` the observed-site marginalized Gaussian-copula likelihood of a five-site model with
patterned missing sites, the complete-row likelihood, and a location-error model whose observation
terms and Gaussian-process density are recorded separately. Each method ran once through
`scripts/run-verification-test.ps1` on current source.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `SpatialGEVLikelihoodOracleTests.MissingSites_DataLogLikelihood_UsesObservedSiteCopulaSubmatrix` | Verification | Scalar likelihood equals the observed-subset oracle (`1e-8`) | Failed - confirms TR-048 (returns the zero-placeholder value) |
| `SpatialGEVLikelihoodOracleTests.MissingSites_PointwiseRows_MatchObservedSubsetOracle` | Verification | Pointwise rows equal the observed-subset rows (`1e-10`) | Failed - confirms TR-048 |
| `SpatialGEVLikelihoodOracleTests.CompleteRows_CopulaLikelihood_MatchesIndependentOracle` | Verification | Complete-row copula likelihood equals the oracle | Passed |
| `SpatialGEVLikelihoodOracleTests.MarginalOnly_WithoutCopula_MatchesIndependentOracle` | Verification | Marginal-only likelihood equals the Hosking GEV oracle | Passed |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_PosteriorKernel_IsInvariantToTheDecomposition` | Verification | Posterior kernel equals observation terms + process density + parameter priors | Passed |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_DataLogLikelihood_ExcludesProcessDensity` | Verification | Data likelihood holds observation terms only | Failed - confirms TR-049 |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_ScalarAndPointwiseDecompositionsAgree` | Verification | Data equals pointwise sum; prior equals pointwise prior sum | Failed - confirms TR-049 |
| `SpatialGEVLikelihoodOracleTests.LocationErrorModel_ScalarAndPointwiseGradientsAgree` | Verification | Scalar and pointwise gradients agree (`1e-4`) | Failed - confirms TR-057 |

## Phase 6 Batch 6.1 rating-curve corrections and replication - 21 August 2026

After the approved TR-043/TR-044/TR-045 corrections (discharge-space density; exponent lower bound 0.1
with a legacy warning; aligned-pair validation with an unmatched-record warning) the fast core project
passes 3,281/3,281 with zero build warnings and every method below ran once through
`scripts/run-verification-test.ps1`. The replication fixtures moved from the shipped 300 observations to
the seeded 1,000-observation block under the recovery sample-size policy; the acceptance-rule
amendments and the first-run failures are recorded in the chapter.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `RatingCurveLikelihoodOracleTests.{One,Two,Three}Segment_DataLogLikelihood_IsDischargeSpaceDensity` | Verification | Scalar, pointwise, and component likelihoods equal the SciPy/Numerics discharge-space density | Passed 3/3 (failed by the change-of-variables sums before the correction) |
| `RatingCurveContinuityVerificationTests` (7 methods) | Verification | Analytical two-sided continuity, positive default bounds, vanishing increment at the lower bound | Passed 7/7 |
| `RatingCurveTests.DataLogLikelihood_IsDischargeSpaceDensity_AtGeneratingParameters`, `..._WithResiduals`, `DataLogLikelihood_ParameterDifferences_AreFreeOfTheChangeOfVariablesTerm`, `DataLogLikelihood_NonPositiveAlignedDischarge_IsNegativeInfinity` | Fast core | Hand-computed discharge-space density, parameter-free Jacobian, identities, nonpositive discharge | Passed |
| `RatingCurveTests.DefaultFlatPriors_BetaBounds_ArePositiveForAllSegments`, `Validate_LegacyZeroExponentBound_WarnsButRemainsValid` | Fast core | Default exponent bound 0.1; legacy bound verbatim with warning | Passed |
| `RatingCurveTests.Validate_UnmatchedNonPositiveDischarge_RemainsValidAndIsReported`, `Validate_ReportsUnmatchedRecordCounts`, `Validate_NonPositiveDischarge_IsInvalid` | Fast core | Aligned-pair error; unmatched-record warning with counts | Passed |
| `RatingCurveExampleRecoveryTests.Mle_{One,Two,Three}Segment_RecoversExampleCurve` | Verification | Production MLE versus independent SciPy optimum and generating parent: deterministic same-point likelihood plus joint 95% LR regions with 4, 7, and 10 fitted coordinates, N=1000 | Passed 3/3 fresh on 1 September 2026 |
| `RatingCurveExampleRecoveryTests.Bayesian_{One,Two,Three}Segment_RecoversExampleCurve` | Verification | Production defaults; R-hat < 1.1, ESS > 100; sampled MAP versus the optimum (5%/10%); MAP-curve parity 2%; 10% truth band; in-band fraction reported (36/36, 36/36, 34/36) | Passed 3/3 (37.4 s, 71.5 s, 122.9 s) |
| `RatingCurveMLERecoveryTests` (10 methods, now 1,000 observations) | Verification | Self-generated truth recovery, unchanged tolerances | Passed 10/10 (3.4-4.0 s) |
| `RatingCurveBayesianRecoveryTests` (10 methods, now 1,000 observations) | Verification | Self-generated truth recovery under production defaults, unchanged tolerances | Passed 10/10 (34-148 s) |

## Phase 6 Batch 6.3 spatial likelihood corrections - 21 August 2026

After the approved TR-048/TR-049/TR-057 corrections (observed-subset copula marginalization; Gaussian-process
densities in `PriorLogLikelihood`; consistent Godambe estimating equations with explicit failure) and the
TR-055 closure, the fast core project passes 3,299/3,299 with zero build warnings, the other three unit
projects pass (UI 579, App 438, API 498), and every method below ran once through
`scripts/run-verification-test.ps1`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `GaussianCopulaTests.LogPDF_ObservedSubset_AllSitesObserved_EqualsFullEvaluation`, `..._FewerThanTwoSites_IsZero`, `..._EqualsCopulaBuiltOnObservedSites`, `..._TracksParameterChanges`, `..._InvalidArguments_Throw` | Fast core | Complete-row parity, no dependence term below two sites, equality with the copula built on the observed sites, cache invalidation, argument validation | Passed |
| `SpatialGEVTests.DataLogLikelihood_WithCopulaAndMissingSites_UsesObservedSiteCopulaSubmatrix`, `..._SingleObservedSiteRow_HasNoDependenceTerm`, `..._FullyMissingRow_ContributesNothing` | Fast core | Hand-computed observed-subset rows in both likelihood paths; placeholder value rejected; empty row contributes zero | Passed |
| `SpatialGEVTests.PriorLogLikelihood_WithSpatialErrors_HoldsGaussianProcessDensities`, `..._DoesNotMutateModelState` | Fast core | Prior equals parameter priors plus process densities; data equals the marginal sum; the three identities; pure evaluation | Passed |
| `SpatialGEVAnalysisTests.ComputeInformationCriteria_UsesNonEmptyRowYearBlocks`, `PredictiveCriteria_FromInjectedDraws_UseRowYearPointwiseTerms` | Fast core | BIC sample unit is the nonempty row/year count; WAIC from injected draws equals the row/year recomputation | Passed |
| `SpatialGEVAnalysisTests.ComputeGodambeCovariance_SingularHessian_ReportsFailureWithoutSubstitute`, `..._WellConditioned_ReportsAvailableCovariance`, `..._WrongParameterCount_Throws` | Fast core | Null plus `Failed` status on a singular sensitivity matrix; finite symmetric positive-variance covariance with `Available`; reset by `ClearResults`; parameter validation | Passed |
| `SpatialGEVTests.Clone_WithCopula_PreservesParameterStructure`, `Clone_WithSpatialErrors_PreservesParameterStructure`, `Clone_PreservesParameterValuesBoundsAndPriors` | Fast core | TR-091: the clone carries the copula and error parameter blocks and the source values, bounds, and priors (failed before the Clone correction: 3 parameters instead of 4 and 17) | Passed |
| `SpatialGEVLikelihoodOracleTests` (8 methods) | Verification | Observed-subset marginalization, conventions, kernel invariance, data/prior decomposition, and gradient agreement against the R `mvtnorm` oracle | Passed 8/8 (up to 3.6 s) |
| `SpatialGEVInformationCriteriaTests.MissingSiteModel_InformationCriteria_UseRowYearBlocks` | Verification | MCMC with production defaults on the oracle's missing-site model; AIC/BIC at the sampled MAP with eleven nonempty row/year blocks; WAIC/PSIS-LOO from the row/year terms | Passed (18.2 s) |
| `SpatialGEVMLERecoveryTests` (2 methods) | Verification | Complete-data MLE recovery, unchanged tolerances | Passed 2/2 (4.1-4.4 s) |
| `SpatialGEVBayesianRecoveryTests` (7 methods) | Verification | Complete-data Bayesian recovery under production defaults, unchanged tolerances | Passed 7/7 (42-115 s) |

## Phase 6 Batch 6.4 spatial cross-validation corrections - 22 August 2026

After the approved TR-050 through TR-053 corrections (reduced training model per fold, fold analyses with
the main settings and seed, held-out covariate rows, explicit fold accounting) the fast core project passes
3,309/3,309 with zero build warnings, the other three unit projects pass (UI 579, App 438, API 498),
and every method below ran once through `scripts/run-verification-test.ps1`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `SpatialGEVTests.CreateReducedModel_WithCopula_RemovesTheHeldOutSite`, `..._WithCopula_IsIndependentOfTheHeldOutSite`, `..._WithCovariateTrend_RemovesTheHeldOutRow`, `..._WithLatentErrors_RemovesTheHeldOutLatentError`, `..._InvalidSite_Throws` | Fast core | Reduced training model: site removed from data, coordinates, weights, covariate rows, copula dimension, and error blocks; settings copied; independent of the held-out column; equals a network built without the site | Passed |
| `SpatialGEVAnalysisTests.SiteWeightZero_WithCopula_DoesNotExcludeTheHeldOutSite`, `SiteWeightZero_WithLatentErrors_KeepsTheHeldOutLatentError` | Fast core | The leakage mechanism of the weight-based exclusion (TR-051 evidence) | Passed |
| `SpatialGEVAnalysisTests.PredictWithCovariates_NullForCovariateTrend_Throws`, `GeneralLinearFunctionTests.Test_PredictWithCovariates_NullOrEmpty_Throws` | Fast core | A covariate trend without covariates throws; intercept-only trends accept null (TR-052) | Passed |
| `SpatialGEVAnalysisTests.RunCrossValidationAsync_WhenNoFoldSucceeds_ThrowsAndReportsNothing`, `SpatialGEVResultsTests.CrossValidation_FoldAccounting_RoundTrips` | Fast core | No-fold policy (two-site network, no sampler run) and DTO fold fields (TR-053) | Passed |
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_WithCopula_RetainsResultsAndMatchesReducedModel` | Verification | Results retained; fold 1 equals the independently reduced copula model fitted through the production path with the same defaults and seed (`1e-6` relative) | Passed (112.0 s) |
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_WithLocationRegression_UsesHeldOutCovariates` | Verification | Fold 1 equals the reduced regression model evaluated at the held-out covariate row (`1e-6` relative) | Passed (113.0 s) |
| `SpatialGEVCrossValidationVerificationTests.LeaveOneSiteOut_SiteWithoutObservations_IsReportedNotScored` | Verification | Unscored fold with NaN metrics; aggregates over the three successful folds | Passed (54.3 s) |
| Batch 6.3 spot checks (two oracle cells, the criteria cell, two recovery cells) | Verification | Unchanged after the cross-validation change | Passed 5/5 |

## Phase 6 Batch 6.5 spatial prediction, uncertainty, simulation, and dispatch - 22 August 2026

After the approved TR-054, TR-056, TR-058, TR-061, TR-062, TR-092, and TR-093 corrections the fast core project
passes 3,323/3,323 with zero build warnings, the other three unit projects pass (UI 579, App 438, API 498),
and every method below ran once through `scripts/run-verification-test.ps1`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `SpatialGEVTests.SetDefaultParameters_LatentErrorBounds_FollowTheLinkSpace`, `DataLogLikelihood_NonFiniteSiteParameters_IsNegativeInfinity` | Fast core | TR-093 bound rule in both link spaces; TR-092 negative-infinite likelihood for an overflowing latent error | Passed |
| `SpatialGEVTests.GenerateRandomValues_WithCopula_ReproducesTheFittedDependence`, `..._WithoutCopula_SimulatesIndependentSites`, `..._WithoutCopula_MatchesTheHistoricalSiteMajorAlgorithm`, `..._WithCopula_IsReproducibleAndKeepsTheMarginals`; `GaussianCopulaTests.GetCorrelationMatrix_ReturnsCopyOfTheFittedMatrix` | Fast core | TR-061 dependence (±0.05 at 4,000 rows), unchanged independent algorithm, reproducibility, marginal support, correlation accessor | Passed |
| `SpatialGEVTests.CreateResampledModel_ReplacesRowsAndKeepsTheNetwork`; `SpatialGEVAnalysisTests.BuildBlockBootstrapRows_DrawsContiguousWrappingBlocks` | Fast core | TR-056 replicate model and block draw | Passed |
| `SpatialGEVAnalysisTests.PredictAtUngaugedLocation_UsesConditionalGaussianProcessPerDraw`, `RegionalCurve_FromInjectedDraws_IsPosteriorOfTheRegionalMean`, `ApplyUncertaintyMethod_RecordsTheAppliedMethod`, `UncertaintySettings_ValidateAndRoundTrip`; `SpatialGEVResultsTests.SiteResultsAndBootstrapResults_DefaultsAndRoundTrip` | Fast core | TR-054 conditional mean per draw and seeded residual; TR-058 regional arithmetic; TR-062 inflated/Godambe dispatch and serialization; DTO defaults | Passed |
| `SpatialGEVKrigingOracleTests.KrigingPrediction_MatchesConditionalGaussianProcessOracle` | Verification | R conditional-GP oracle, 15 cases (`1e-10`) | Passed (3.9 s) |
| `SpatialGEVPredictionVerificationTests.UngaugedPrediction_UsesConditionalGaussianProcessPerDraw` | Verification | Deterministic option equals the posterior mean of the model-level kriging prediction (`1e-9`); residual option reproducible and at least as wide | Passed (111.9 s) |
| `SpatialGEVPredictionVerificationTests.RegionalCurve_IsPosteriorOfTheRegionalMeanQuantile` | Verification | Regional mean curve and bounds equal the per-draw regional posterior (`1e-9`) | Passed (41.5 s) |
| `SpatialGEVSimulationVerificationTests.GenerateRandomValues_WithCopula_ReproducesTheFittedIntersiteDependence` | Verification | Seeded 20,000 rows: correlations within ±0.02, quantiles within 3% | Passed (3.2 s) |
| `SpatialGEVUncertaintyMethodVerificationTests.RunAsync_BayesianInflated_WidensThePosteriorIntervals`, `RunAsync_GodambeSandwich_BuildsResultsFromGaussianDraws`, `RunAsync_SpatialBootstrap_FitsResampledReplicatesAndReportsAccounting` | Verification | Dispatch, applied method, sqrt-VIF widening, MAP-centred Gaussian-draw intervals, bootstrap accounting | Passed 3/3 (63.0 s, 49.0 s, 74.4 s) |
| Regression set (15 cells: oracle, criteria, cross-validation, recovery) | Verification | Batch 6.3/6.4 contracts unchanged | Passed 15/15 |

## Verification completeness Chunk 14 - 31 August 2026

| Current identity or owner | Layer | Scientific/contract role | Outcome |
|---|---|---|---|
| `SpatialGEVChunk14OracleTests.BasicExponentialCorrelation_MatchesAnalyticalGrid` | Verification | Analytical exponential grid including zero and the range | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.PoweredExponentialCorrelation_MatchesAnalyticalGrid` | Verification | Analytical powered-exponential grid with smoothness 1.6 | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.SphericalCorrelation_MatchesAnalyticalGridAndCompactSupport` | Verification | Analytical compact-support boundary and beyond-range zeros | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.HeldOutCopulaFold_MatchesIndependentFittedOracle` | Verification | Independent complete Gaussian-copula reduced-fold optimum and held-out quantile uncertainty | Passed; one-result TRX `20260901-144228-...` after the final sign-safe LR review |
| `SpatialGEVChunk14OracleTests.HeldOutCovariateFold_MatchesIndependentRegressionOracle` | Verification | Independent held-out covariate row plus executable normal-equation uncertainty split | Passed; one-result TRX |
| Historical three `SpatialGEVCrossValidationVerificationTests` methods | Design history | Same-ecosystem production parity or result accounting; no longer executable Verification declarations | Consolidated; fast owners retained |
| `SpatialGEVChunk14OracleTests.UngaugedDrawSpecificPrediction_MatchesIndependentGeodesicGaussianOracle` | Verification | Four fixed geodesic GP draws, conditional mean/variance, physical location | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.RegionalFixedDrawAggregation_MatchesIndependentPosteriorOracle` | Verification | Nine fixed draws, three nonexchangeable sites, three central-95% regional ordinates | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.GodambeSensitivityVariabilityAndSandwich_MatchIndependentOracle` | Verification | Independent H, J, and unregularized sandwich covariance | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.TemporalBlockBootstrap_MatchesIndependentWholeRowOracle` | Verification | Whole-row wrapping blocks, five independent SciPy flat-prior MAP fits, and production fitted physical-parameter/site/regional interval parity | Passed; one-result TRX |
| `SpatialGEVChunk14OracleTests.VarianceInflation_UsesExactIndependentAnalyticalTransformation` | Verification | Independent VIF plus exact site and regional endpoint transformation | Passed; one-result TRX |
| Historical two `SpatialGEVPredictionVerificationTests` and three `SpatialGEVUncertaintyMethodVerificationTests` methods | Design history | Same-production posterior recomputation or estimator dispatch/accounting; no longer executable Verification declarations | Consolidated; fast owners retained |

## Phase 6 Batch 6.6 spatial site-weight naming and distance metric - 22 August 2026

After the approved TR-059 rename (obsolete alias retained) and the TR-060 distance metric the fast core
project passes 3,328/3,328 with zero build warnings, the other three unit projects pass (UI 579,
App 438, API 498), and every method below ran once through `scripts/run-verification-test.ps1`.

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `SpatialGEVTests.ComputeCorrelationHeuristicSiteWeights_PinsTheFormulaAndTheObsoleteAlias` (+ renamed update/custom-matrix/mismatch contracts) | Fast core | Heuristic formula pinned; obsolete alias forwards bitwise (TR-059) | Passed |
| `SpatialGEVTests.DistanceMetric_DefaultsToCartesianAndPropagatesToComponents`, `DistanceMetric_RoundTripsThroughSerializationAndFactories`; `GaussianCopulaTests.GeodesicMetric_BuildsCorrelationFromGreatCircleKilometres`; `SpatialRegressionErrorsTests.GeodesicMetric_UsesGreatCircleKilometresForCovarianceAndKriging` | Fast core | Cartesian default, latitude/longitude validation, propagation, serialization, cloning, factories, hand-haversine correlation and kriging (TR-060) | Passed |
| `SpatialGEVDistanceOracleTests.GeodesicMetric_MatchesHaversineOracle` | Verification | R haversine oracle: distances `1e-9` km, correlation and kriging `1e-10` | Passed (3.9 s) |
| `SpatialGEVDistanceOracleTests.CartesianMetric_IsPlanarEuclidean` | Verification | Default metric equals the planar Euclidean distances | Passed (3.5 s) |
| Regression set (9 cells) | Verification | Batches 6.3-6.5 contracts unchanged | Passed 9/9 |

## Phase 7 closeout - 22 August 2026

TR-084 through TR-090 were diagnosed with a scratch console against the built assemblies, disposed one
decision at a time (Haden Smith, 22 August 2026), implemented, and rerun one method per guarded invocation.
Production changes: GMM covariance conditioning without the eigenvalue cap (TR-085), relative re-centring of
bootstrap measurement-error distributions for log-space fits and positive supports (TR-086), and the Bulletin 17C
initial-parameter fallback with validation messages (TR-087). Fixture and test changes: trend-carrying
nonstationary generators with in-bounds rates and the gross-error acceptance gate (TR-084), plotting positions in
the censored coverage frames (TR-087), 1,000-observation legacy time-series fixtures with credible-interval
acceptance for the Bayesian cells (TR-089), and a deterministic reprocess wait (TR-090). The per-cell table keeps
the last run of each cell; the earlier runs under the superseded rules are summarized in the register. Fast gates after the changes:
Core 3,337, UI 579, App 438, API 498, 0 failures; strict Debug XML builds 0 warnings/errors; the restored
`scripts/validate-code-xml-docs.ps1` gate passes. Every Verification method below ran through
`scripts/run-verification-test.ps1` (wall-clock per guarded invocation).

| Test | Project | Contract | Outcome |
|---|---|---|---|
| `GeneralizedMethodOfMomentsCovarianceScaleTests` (2) | Fast core | Exactly identified sandwich equals the closed-form mean and scale variances on the spread-eigenvalue Pearson III fixture (TR-085) | Passed |
| `DataFrameBootstrapShiftTests` (3) | Fast core | Positive support and preserved relative spread under an LP3 sampling distribution; bitwise additive shift for unbounded real-space errors; relative shift for positive-support errors (TR-086) | Passed |
| `Bulletin17CInitialParameterFallbackTests` (4) | Fast core | Constraint-based initial values plus validation warning without plotting positions; censored initial estimate without warning once plotting positions exist (TR-087) | Passed |
| `UnivariateAnalysisPositivePathReprocessTests.CredibleIntervalWidthChange_EstimatedAnalysis_PreservesResultsReference` | Fast core | Waits for the published reprocess instead of racing a second direct call (TR-090) | Passed |
| `B17CCovarianceTests` (13 cells) | Verification | GMM sandwich versus the Numerics asymptotic covariance oracle at 15%/5% with the off-diagonal floor (TR-085) | Passed 13/13 |
| GMM regression set: `GmmSpecificationVerificationTests` (4), `GmmObjectiveGradientVerificationTests` (2), `GmmInfluenceDiagnosticsVerificationTests` (2), `GeneralizedMethodOfMomentsRecoveryTests` (4), `B17CPenalityTests` (12), `B17CExampleTests` (10) | Verification | Unchanged contracts after the cap removal (TR-085) | Passed 34/34 |
| `UncertainDataBootstrapVerificationTests` (2 cells) | Verification | Finite delivery, failure rate < 1%, optimizer fallback rate < 1%, centred means (TR-086) | Passed 2/2 |
| `B17CBootstrapRefitReliabilityTests` (14 cells) | Verification | Ordinary/pivotal refits: zero retries, substitutions, and exceptions (TR-086 regression) | Passed 14/14 |
| `NonstationaryValidationTests` (16 cells) | Verification | Gross-error gate on trend-carrying fixtures under the production defaults: posterior mode within four posterior SD of the truth, R-hat < 1.1, ESS > 100 (TR-084; under the earlier 1% rule 2/16 and under 90% interval coverage 9/16 passed, see the register) | Passed 16/16 |
| Legacy time-series recovery cells (29) | Verification | 1,000 observations; the 22 Bayesian cells assert central 90% credible-interval coverage and R-hat < 1.1, the 7 MLE cells keep their tolerances (TR-089; 27/29 under the former bands) | Passed 29/29 |
| `B17CCensoredCoverageTests`, `B17CCoverageTests` | Verification | Coverage cells | Not rerun by decision (TR-087, TR-088) |

Per-cell outcomes (wall-clock per guarded invocation):

| Method | Outcome | Time |
|---|---|---|
| `NonstationaryValidationTests.Nonstationary_ConstantTrend_RecoversTrueParameters` | Passed | 25 s |
| `NonstationaryValidationTests.Nonstationary_LinearTrend_RecoversTrueParameters` | Passed | 33 s |
| `NonstationaryValidationTests.Nonstationary_QuadraticTrend_RecoversTrueParameters` | Passed | 47 s |
| `NonstationaryValidationTests.Nonstationary_CubicTrend_RecoversTrueParameters` | Passed | 67 s |
| `NonstationaryValidationTests.Nonstationary_ExponentialTrend_RecoversTrueParameters` | Passed | 39 s |
| `NonstationaryValidationTests.Nonstationary_LogisticTrend_RecoversTrueParameters` | Passed | 41 s |
| `NonstationaryValidationTests.Nonstationary_PowerTrend_RecoversTrueParameters` | Passed | 41 s |
| `NonstationaryValidationTests.Nonstationary_SinusoidalTrend_RecoversTrueParameters` | Passed | 73 s |
| `NonstationaryValidationTests.Nonstationary_StepFunctionTrend_RecoversTrueParameters` | Passed | 48 s |
| `NonstationaryValidationTests.Nonstationary_SigmaLinearTrend_RecoversTrueParameters` | Passed | 34 s |
| `NonstationaryValidationTests.Nonstationary_SigmaQuadraticTrend_RecoversTrueParameters` | Passed | 57 s |
| `NonstationaryValidationTests.Nonstationary_SigmaExponentialTrend_RecoversTrueParameters` | Passed | 45 s |
| `NonstationaryValidationTests.Nonstationary_BothLinearTrend_RecoversTrueParameters` | Passed | 52 s |
| `NonstationaryValidationTests.Nonstationary_MuQuadraticSigmaLinear_RecoversTrueParameters` | Passed | 62 s |
| `NonstationaryValidationTests.Nonstationary_MuLinearSigmaExponential_RecoversTrueParameters` | Passed | 53 s |
| `NonstationaryValidationTests.Nonstationary_BothStepFunction_RecoversTrueParameters` | Passed | 71 s |
| `ARAnalysisTests.Test_EstimateParameters_AR2` | Passed | 36 s |
| `ARAnalysisTests.Test_EstimateParameters_AR3` | Passed | 49 s |
| `MAAnalysisTests.Test_EstimateParameters_MA2` | Passed | 33 s |
| `MAAnalysisTests.Test_EstimateParameters_MA3` | Passed | 39 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA11` | Passed | 40 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA21` | Passed | 50 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA12` | Passed | 45 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA22` | Passed | 58 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA_FitsAR1` | Passed | 27 s |
| `ARIMAAnalysisTests.Test_EstimateParameters_ARIMA_FitsMA1` | Passed | 29 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX11` | Passed | 29 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX21` | Passed | 42 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX12` | Passed | 37 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX22` | Passed | 55 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX_FitsAR1` | Passed | 22 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMAX_FitsMA1` | Passed | 20 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMA110` | Passed | 21 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMA011` | Passed | 20 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_ARIMA111` | Passed | 27 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_LinearTrend_Only` | Passed | 20 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_AR1_LinearTrend` | Passed | 25 s |
| `ARIMAXAnalysisTests.Test_EstimateParameters_AR1_Seasonal` | Passed | 39 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_LinearTrend_Only` | Passed | 3.3 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_QuadraticTrend_Only` | Passed | 3.3 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_CubicTrend_Only` | Passed | 3.2 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_AR1_LinearTrend` | Passed | 3.2 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_AR1_QuadraticTrend` | Passed | 3.4 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_AR1_CubicTrend` | Passed | 3.4 s |
| `ARIMAXMLERecoveryTests.Test_EstimateParameters_MA1_LinearTrend` | Passed | 3.3 s |
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

The three `HirschStedingerPlottingPositionVerificationTests` PeakFQ cells listed under `B17CExampleTests` in the
first run script resolved to zero tests because they belong to the second class of that file; they are
plotting-position parity cells unrelated to TR-085 and were not rerun.

## Verification completeness Chunk 4A ownership disposition - 29 August 2026

This section records the current ownership ruling and supersedes the ownership labels in the historical run
registers above without rewriting their dated outcomes. No Verification method, including the three governed
Bulletin 17C coverage classes, was executed for this disposition.

The cumulative default catalog now resolves 509 declarations and 509 execution units: 224 verified, 227 open,
56 execution-excluded, and two accepted limitations. The two renamed point-process likelihood oracles remain
open until exact approval-gated focused runs provide current execution evidence.

### Removed completion-only or engineering-contract Verification methods

| Former Verification method | Current disposition | Fast or retained protection |
|---|---|---|
| `B17CBootstrapRefitReliabilityTests.Example1_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Deterministic retry, exception, substitution, and failure accounting in `Bulletin17CAnalysisTests.ResolveBootstrapReplicate_*` |
| `B17CBootstrapRefitReliabilityTests.Example1_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example2_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example2_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example3_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example3_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example4_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example4_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example5_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example5_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example6_OrdinaryBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example6_PivotalBootstrap_ThousandRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `B17CBootstrapRefitReliabilityTests.Example7_OrdinaryBootstrap_FiveHundredRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection |
| `B17CBootstrapRefitReliabilityTests.Example7_PivotalBootstrap_FiveHundredRefits` | Removed: completion/accounting only | Same deterministic retry-policy protection; pivotal production path uses the same seam |
| `UncertainDataBootstrapVerificationTests.LogPearsonBootstrap_Move3StyleUncertaintyRemainsStable` | Removed: same-parent centering and delivery, no independent accuracy oracle | Delivery/accounting policy protected deterministically; uncertain-data bootstrap accuracy remains open for Chunk 7 |
| `UncertainDataBootstrapVerificationTests.NormalBootstrap_UncertainObservationsRemainStable` | Removed: same-parent centering and delivery, no independent accuracy oracle | Delivery/accounting policy protected deterministically; uncertain-data bootstrap accuracy remains open for Chunk 7 |
| `B17CLowOutlierSetterFitTests.Gmm_SetterOnly_MatchesSetterPlusManualRecompute` | Removed from Verification; estimator invocation was unnecessary | Existing fast `PlottingPositionTests.Test_SetLowOutliersFromMGBT_RecomputesPlottingPositions` protects immediate recalculation |
| `B17CExampleTests.Test_PointwiseMomentConditions_MeanEqualsG` | Removed from Verification; same-production identity | Existing fast `Bulletin17CDistributionTests.PointwiseMomentConditions_ColumnMeans_MatchMomentConditionsG_*` methods use valid fixed parameter vectors and no estimator |
| `PointProcessRecoveryTests.Test_SeasonalMixedObservationLikelihood_IsOrderInvariantAndMatchesIndependentCalculation` | Removed after separating the duplicated oracle and engineering contract | Seasonal oracle retained below; existing fast `PointProcessModelTests.Test_Seasonal_DataLogLikelihood_IsInvariantToInputOrder` protects ordering |

The obsolete source scraper `src/RMC.BestFit.Verification/extract_tests.py` was also removed; no build,
validation, or catalog workflow referenced it.

### Retained point-process numerical oracles

| Current Verification method | Disposition | Oracle and acceptance |
|---|---|---|
| `PointProcessLikelihoodOracleTests.NonseasonalMixedObservations_MatchIndependentLikelihood` | Retained and explicitly named; open pending an exact approval-gated rerun | Independent Poisson, intensity-density, Normal-convolution, interval, and threshold contribution sum; absolute tolerance `2e-7`; exact event count |
| `PointProcessLikelihoodOracleTests.SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood` | Retained and explicitly named; open pending an exact approval-gated rerun | Independent exposure-weighted seasonal annual-maximum distribution; absolute tolerance `2e-7`; exact censoring and event counts |

### Execution-excluded Cohn coverage methods

The former 30-row `DataTestMethod` is now 30 ordinary methods so every governed scenario has an exact catalog
identity. Each method preserves gamma, `Ns`, `Nh`, the 1,000-replicate design, master seed `12345`, nominal
coverage `0.90`, and the historical at-least-800-completions rule. These methods remain historical evidence,
rerun only on explicit request, and were not executed during Chunk 4A.

| Exact method | Gamma | Ns | Nh | Status |
|---|---:|---:|---:|---|
| `CohnEtAl_LP3_GammaMinus1p0_Ns25_Nh0_Coverage` | -1.0 | 25 | 0 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus1p0_Ns25_Nh50_Coverage` | -1.0 | 25 | 50 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus1p0_Ns25_Nh150_Coverage` | -1.0 | 25 | 150 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus1p0_Ns100_Nh0_Coverage` | -1.0 | 100 | 0 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus1p0_Ns100_Nh50_Coverage` | -1.0 | 100 | 50 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus1p0_Ns100_Nh150_Coverage` | -1.0 | 100 | 150 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns25_Nh0_Coverage` | -0.5 | 25 | 0 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns25_Nh50_Coverage` | -0.5 | 25 | 50 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns25_Nh150_Coverage` | -0.5 | 25 | 150 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns100_Nh0_Coverage` | -0.5 | 100 | 0 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns100_Nh50_Coverage` | -0.5 | 100 | 50 | Execution-excluded |
| `CohnEtAl_LP3_GammaMinus0p5_Ns100_Nh150_Coverage` | -0.5 | 100 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns25_Nh0_Coverage` | 0.0 | 25 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns25_Nh50_Coverage` | 0.0 | 25 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns25_Nh150_Coverage` | 0.0 | 25 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns100_Nh0_Coverage` | 0.0 | 100 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns100_Nh50_Coverage` | 0.0 | 100 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p0_Ns100_Nh150_Coverage` | 0.0 | 100 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns25_Nh0_Coverage` | 0.5 | 25 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns25_Nh50_Coverage` | 0.5 | 25 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns25_Nh150_Coverage` | 0.5 | 25 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns100_Nh0_Coverage` | 0.5 | 100 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns100_Nh50_Coverage` | 0.5 | 100 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma0p5_Ns100_Nh150_Coverage` | 0.5 | 100 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns25_Nh0_Coverage` | 1.0 | 25 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns25_Nh50_Coverage` | 1.0 | 25 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns25_Nh150_Coverage` | 1.0 | 25 | 150 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns100_Nh0_Coverage` | 1.0 | 100 | 0 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns100_Nh50_Coverage` | 1.0 | 100 | 50 | Execution-excluded |
| `CohnEtAl_LP3_Gamma1p0_Ns100_Nh150_Coverage` | 1.0 | 100 | 150 | Execution-excluded |

## Verification completeness Chunk 5 estimator recovery - 29 August 2026

The common test-only recovery policy fixes generated-parent estimator experiments at 1,000 declared
observational units. Regular MLE cells use Numerics distribution-level parameter variance at recovered
parameters; the true-profile-supported LP3 Q(0.99) identified-response cell and the Generalized-Pareto
zero-location MLE cell use the corresponding Numerics Q(0.99) quantile-variance response bands. Although
their Numerics parameter- and quantile-variance methods throw `NotImplementedException`, the
GeneralizedNormal and GeneralizedLogistic MLE cells use Haden's approved production true-profile-likelihood
path at alpha=0.05: every generating coordinate must lie inside a finite, ordered true 95% profile interval.
LnNormal uses native covariance coordinates `(Mu, SigmaSquared)`; Pearson III and Log-Pearson III use
`(Mu, OneOverBeta, Alpha)`, with no covariance transformation. Bayesian Normal, MAP Normal, and the
two-step GMM response/instrument cell retain their common acceptance rules. The Normal-Normal conjugate
and MAP AIC/BIC formula claims remain explicitly analytical.

Chunk 6B exact guarded execution now passes 22 of 22 current identities. The original fifteen passes are
Gamma (`20260829-173507-*`), Weibull (`173529-*`), Normal (`173557-*`), Generalized Normal (`173611-*`),
Logistic (`173638-*`), Generalized Logistic (`173706-*`), Gumbel (`173740-*`), GEV (`173753-*`),
Generalized Pareto (`173827-*`), Kappa Four (`173849-*`), Pearson III (`173957-*`), Log-Pearson III
(`174057-*`), LogNormal (`174200-*`), LnNormal (`174221-*`), and the ReciprocalTrend analytical identity
(`174245-*`).

The Exponential failure was a Numerics constraint regression: a negative location initializer was passed
to `Log10` for its upper bound. Restoring the distribution-support upper bound to the sample minimum made
the fixed-seed priors finite; the exact Exponential rerun passed in `20260829-182917-*`. Because Bulletin 17C
fits Exponential parameters by GMM moments rather than likelihood support, `Bulletin17CDistribution` now
restores the former order-of-magnitude location upper bound only for that B17C/GMM path. Fast regressions
distinguish the Exponential override from unchanged Numerics constraints for other B17C families; no B17C
Verification method was rerun for this default-bound correction. Reciprocal defaults had copied a
response-scale initializer into coefficient `a` of `1/(a+b t)`, mis-centering the response and creating
orders-of-magnitude prior mismatch. Response-anchored reciprocal defaults passed the unchanged Normal
reciprocal-mean cell in `20260829-182932-*`. Sinusoidal amplitude defaults were also constrained by the nearer
parent-parameter bound so the complete default response remains valid.

The fast deterministic configuration matrix covers all 38 supported parent-distribution parameters crossed
with all ten temporal trend types, for 380 finite-prior and valid-default-trajectory assignments. The targeted
Bayesian covering array adds Normal reciprocal scale (`20260829-183018-*`), Normal sinusoidal scale
(`183058-*`), GEV linear shape (`183143-*`), GPD linear location plus exponential scale (`183323-*`), and
Log-Pearson III linear log-mean plus exponential log-scale (`183534-*`); all five exact methods passed the
shared central-95% response/coordinate inclusion rule and per-coordinate R-hat/ESS gates. The sixteen older
Normal trend cells retain their documented four-posterior-standard-deviation rule and are not represented as
shared-policy cells until a separately authorized migration and exact rerun.

Twenty-two current identities were run separately through the guarded exact-method runner. All final
runs passed: two Bayesian recovery cells, one conjugate oracle, MAP recovery and two information-criteria
oracles, thirteen MLE family recoveries, the retained LnNormal closed-form comparison, two-step GMM,
and LP3 Q(0.99) Profile-Q. TRXs are retained in the isolated `20260829-165839` through
`20260829-170359` result directories. Initial LnNormal and Pearson III failures exposed the covariance-
coordinate mismatch; the same correction was applied to Log-Pearson III, and all three final exact reruns
passed without changing N, seed, generating parent, covariance method, or the 1.96 rule. The 22 catalog
identities are now verified.
