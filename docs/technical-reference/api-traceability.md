<!-- technical-reference-status: complete -->

# Scientific API Traceability Matrix

[Back to Documentation Index](../index.md)

This matrix assigns every exported scientific type to a technical-reference chapter and identifies the principal implementation and verification evidence. A row marked *complete* has passed its scheduled source audit; *partial* means that the API is treated in a completed chapter but broader algorithm coverage remains scheduled; *planned* has not yet completed scientific reconciliation.

## Model Infrastructure and Data

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `IModel`, `ModelBase`, `ModelParameter`, `DataComponent`, `DataComponentType`, `PriorComponent`, `PriorComponentType` | Models overview; likelihood and prior decomposition | Model support classes | Core infrastructure and pointwise-likelihood tests | Complete |
| `IGMMModel`, `IQuantilePriors`, `IUnivariateModel`, `ISimulatable`, `ISimulatable<TData>` | Models overview; estimation; univariate models | Model support interfaces | Model-estimation and univariate tests | Complete |
| `ParameterPenalty`, `QuantilePenalty`, `QuantilePrior`, `SubscriptFormatter` | Priors and parameterization | Model support classes | Support and quantile-prior tests | Complete |
| `Data`, `ExactData`, `IntervalData`, `ThresholdData`, `UncertainData` | Data likelihood | Data-frame data types | Data-frame unit tests and pointwise verification | Complete |
| `DataSeries`, `ExactSeries`, `IntervalSeries`, `ThresholdSeries`, `UncertainSeries`, `DataFrame` | Data likelihood | Data-frame collections | Data-frame and concurrency tests | Complete |
| `ThresholdDiagnostics`, `MRLPoint`, `MeanResidualLifeResult`, `StabilityPoint`, `ParameterStabilityResult` | Peaks over threshold | Threshold diagnostics | Threshold-diagnostic tests | Complete |

## Univariate Models and Analyses

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `UnivariateDistributionModelBase`, `UnivariateDistribution` | Univariate likelihood and distribution-family chapters | Univariate distribution model | Univariate model and measurement-error tests | Complete |
| `Bulletin17CDistribution`, `Bulletin17CAnalysis`, `UncertaintyMethod`, `CohnConfidenceIntervalResult` | Bulletin 17C | B17C model and analysis | Fast midpoint/ranked-initializer/bounds-repair tests, exact-LP3 Cohn scope guards, seven published-example methods, and 14 exact reliability cells passed; 13,000 unguarded finite outputs with zero retries or substitutions; no Phase 3 oracle artifact | TR-016 through TR-021 closed in the approved scope; numerical Cohn values remain deferred |
| `PointProcessModel`, `PointProcessAnalysis` | Point-process models | Point-process model and analysis | Fast contracts plus all ten guarded cells pass; Bayesian recovery uses default DEMCzs settings and 1,000 observations, including the calendar/water-year block-origin parity fixture | Complete with TR-004/TR-005 |
| `MixtureModel`, `MixtureAnalysis` | Mixture models | Mixture model and analysis | Phase 4 fast simplex, hurdle, mixed-likelihood, impossible-row, explicit-start MAP, and posterior-population tests; three guarded Numerics/BestFit parity methods; three Bayesian recovery methods plus informative-prior initialization method ready for focused rerun | TR-006/TR-007/TR-008 complete; changed Bayesian initialization evidence pending |
| `CompetingRisksModel`, `CompetingRiskAnalysis` | Competing risks | Competing-risks model and analysis | Fast seed/matrix/MAP-initialization contracts, four analytical rank/CDF methods, ten passed MLE methods, and four passed Default-DEMCzs recovery methods | TR-012 complete; six Bayesian recovery findings keep the supplement open |
| `CompositeAnalysis`, `CompositeType`, `AverageMethod`, `WeightedUnivariateAnalysis`, `CorrelationMatrix` | Composite analysis and model averaging | Composite analysis support | Fast criterion/matrix/index/seed/immutability tests, passed two-child Cartesian oracle, and nine passed report/closed-form/three-child posterior methods | TR-013/TR-014/TR-015 complete; one extreme-tail inversion finding keeps the supplement open; no TR-014 public API addition |
| `UnivariateAnalysis`, `FittingAnalysis`, `FittedDistribution` | Univariate and distribution-fitting workflows | Analysis classes | Fitting and univariate tests | Complete |

## Analysis Infrastructure

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `IAnalysis`, `IBayesianAnalysis`, `IProbabilityOrdinates`, `IUnivariateAnalysis`, `AnalysisBase` | Analyses overview | Analysis support interfaces and base | Analysis workflow tests | Complete |
| `AnalysisProgress`, `AnalysisRunCompletedEventArgs`, `BootstrapDiagnostics` | Analyses overview | Analysis support classes | Progress and bootstrap-diagnostic tests | Complete |
| `BatchAnalysisOptions`, `BatchAnalysisResult`, `BatchAnalysisRunner` | Batch execution | Batch analysis support | Batch analysis tests | Partial |

## Estimation and Diagnostics

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `OptimizationMethod`, `CovarianceComputationStatus`, `MaximumLikelihood`, `MaximumAPosteriori` | MLE and MAP chapters | Estimation classes and explicit covariance status | R `bbmle` profile, analytical posterior-profile, covariance-status tests, and compiled workflows | Complete with TR-023 |
| `GeneralizedMethodOfMoments`, `GMMIdentificationStatus`, `GMMEstimationStrategy`, `MomentConditionFunction`, `PointwiseMomentConditionFunction`, `JacobianFunction`, `PenaltyFunction` | GMM chapter | GMM estimator | R `gmm` fit/specification/covariance sources; compiled workflow | Fixed-weight and efficient fit/covariance verified with TR-026, TR-033, and TR-034 |
| `BayesianAnalysis`, `SamplerType`, `PointEstimateType` | Bayesian MCMC and model comparison | Bayesian analysis and pinned Numerics samplers | R `loo` and `posterior` parity; Bayesian/MCMC test sources; compiled workflow | PSIS, ARWMH adaptation, NUTS acceptance/gradient routes, live-sampler diagnostics, and rank-normalized R-hat/bulk-tail ESS verified |
| `NumericalDiff` | MLE, MAP, GMM, and leverage diagnostics | Numerical differentiation helper | Numerical-difference tests | Complete |
| `InfluenceDiagnostics`, `ObservationInfluence`, `ParetoKCategory` | Influence diagnostics | Influence diagnostics | R `loo` threshold parity, serialization tests, compiled workflow | Complete; TR-024 fixed |
| `LeverageDiagnostics`, `ObservationLeverage`, `PriorComponentLeverage` | Influence diagnostics | Leverage diagnostics | Leverage unit tests; compiled workflow | Complete with TR-031 |
| `PriorInfluenceDiagnostics`, `PriorComponentSummary` | Influence diagnostics | Prior-influence diagnostics | Prior-influence tests; compiled workflow | Complete |
| `PosteriorPredictiveCheck`, `PriorPredictiveCheck`, `PredictiveCheckResults`, `PredictiveSummary` | Predictive checks | Predictive diagnostics | Predictive-check test sources; compiled workflow | Complete with TR-028 |

## Time Series and Rating Curves

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `AutoRegressive`, `ARAnalysis` | Autoregressive chapter | AR model and analysis | AR unit/verification sources; compiled workflow | Complete with TR-035, TR-036, TR-040, TR-042, TR-046 |
| `MovingAverage`, `MAAnalysis` | Moving-average chapter | MA model and analysis | MA unit/verification sources; compiled workflow | Complete with TR-035, TR-036, TR-040, TR-042, TR-046 |
| `ARIMA`, `ARIMAAnalysis`, `Transform` | ARIMA and time-series chapters | ARIMA model and analysis | ARIMA unit/verification sources; compiled workflow | Complete with TR-035 through TR-040, TR-042, TR-046 |
| `ARIMAX`, `ARIMAXAnalysis`, `Trend`, `CovariateExtensionMethod` | ARIMAX chapter | ARIMAX model and analysis | ARIMAX unit/verification sources; compiled workflow | Complete with TR-036, TR-037, TR-039 through TR-042, TR-046 |
| `RatingCurve`, `RatingCurveAnalysis` | Rating-curve chapter | Rating-curve model and analysis | Rating-curve unit/verification sources; compiled workflow | Complete with TR-042 through TR-045 |

## Bivariate and Spatial Models

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `BivariateDistribution`, `BivariateAnalysis` | Bivariate distributions and analysis | Bivariate model and analysis | Unit/integration and recovery-test sources; compiled workflow | Complete with TR-047 |
| `CoincidentFrequencyAnalysis` | Coincident frequency | Coincident-frequency analysis | Numerical-integration test sources; compiled workflow | Complete |
| `SpatialGEV`, `SpatialGEVAnalysis`, `SpatialGEVUncertaintyMethod` | Spatial GEV hierarchy, likelihood, fitting, and results | Spatial model and analysis | Spatial unit/verification sources; compiled workflow | Complete with TR-048 through TR-062 |
| `SpatialGEVSiteResults`, `SpatialGEVCrossValidationResults` | Spatial result construction and validation | Spatial result types | Result DTO and cross-validation test sources | Complete with TR-050 through TR-058 |
| `GaussianCopula`, `CachedMultivariateNormal`, `SpatialRegressionErrors` | Spatial copula, Gaussian-process errors, and kriging | Spatial dependence support | Gaussian-copula, cached-MVN, and regression-error tests | Complete with TR-048, TR-049, TR-054, TR-060, TR-061 |
| `ICorrelationModel`, `CorrelationFunctionType`, `BasicExponential`, `PoweredExponential`, `Spherical` | Spatial correlation functions and bounds | Correlation implementations | Fixed-value spatial-correlation tests | Complete with TR-060 |

## Trend and Link Functions

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `ITrendModel`, `TrendModelBase`, `TrendModelType` | Trend functions | Trend support | Trend base tests | Complete |
| `ConstantTrend`, `LinearTrend`, `QuadraticTrend`, `CubicTrend`, `ExponentialTrend`, `LogisticTrend`, `PowerTrend`, `ReciprocalTrend`, `SinusoidalTrend`, `StepFunction`, `GeneralLinearFunction` | Trend functions | Trend implementations | Per-function unit tests | Complete |
| `SESLink`, `ASinHLink`, `CenteredLink`, `LogASinHLink`, `LogSESLink`, `BestFitLinkFunctionFactory` | Link functions | Link implementations | Link-function and serialization tests | Complete |

## Coverage Rule

The automated documentation test reflects over the built `RMC.BestFit.dll` and fails when an exported scientific type is absent from this matrix. Public constructors and behavior-affecting members inherit the chapter assignment of their declaring type and are audited in that chapter's API table and compiled examples. Result-only properties may be grouped, but their units, nullability, and interpretation must be stated.

---

[Back to Documentation Index](../index.md)
