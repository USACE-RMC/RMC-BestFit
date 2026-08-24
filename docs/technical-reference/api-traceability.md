<!-- technical-reference-status: complete -->

# Scientific API Traceability Matrix

[Back to Documentation Index](../index.md)

This matrix assigns every exported scientific type to a technical-reference chapter and identifies the principal implementation and verification evidence. A row marked *complete* has passed its source audit. A *partial* row has a documented evidence boundary that prevents a broader claim.

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
| `Bulletin17CDistribution`, `Bulletin17CAnalysis`, `UncertaintyMethod`, `CohnConfidenceIntervalResult` | Bulletin 17C | B17C model and analysis | Fast moment/covariance/scope tests, seven published examples, three PeakFQ cells, and fourteen reliability cells with 13,000 finite outputs | Complete for published parameter parity and reliability; numerical Cohn values and broad coverage are outside the current claim set |
| `PointProcessModel`, `PointProcessAnalysis` | Point-process models | Point-process model and analysis | Fast contracts plus ten Poisson/GPA, likelihood, calendar/water-year, and recovery cells under production defaults | Complete |
| `MixtureModel`, `MixtureAnalysis` | Mixture models | Full-$K$ public/EM boundary; identified $K-1$ posterior storage | Fast residual-weight, prior, covariance, diagnostic, persistence, and frequency-curve contracts; three Numerics parity and three Bayesian recovery cells | Complete; six of six numerical cells passed |
| `CompetingRisksModel`, `CompetingRiskAnalysis` | Competing risks | Competing-risks model and analysis | Fast seed/matrix/initialization contracts, four analytical rank/CDF cells, ten MLE cells, and four supported Bayesian cells | Complete for the reported matrix; six difficult Bayesian designs are outside the supported recovery claim |
| `CompositeAnalysis`, `CompositeType`, `AverageMethod`, `WeightedUnivariateAnalysis`, `CorrelationMatrix` | Composite analysis and model averaging | Composite analysis support | Fast criterion/matrix/index/seed/immutability tests, two posterior-resampling oracles, and ten closed-form/published/Cartesian cells | Complete; twelve of twelve numerical cells passed |
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
| `OptimizationMethod`, `CovarianceComputationStatus`, `MaximumLikelihood`, `MaximumAPosteriori` | MLE and MAP chapters | Estimation classes and explicit covariance status | R `bbmle` profile, analytical posterior profile, covariance-status tests, and compiled workflows | Complete |
| `GeneralizedMethodOfMoments`, `GMMIdentificationStatus`, `GMMEstimationStrategy`, `MomentConditionFunction`, `PointwiseMomentConditionFunction`, `JacobianFunction`, `PenaltyFunction` | GMM chapter | GMM estimator | R `gmm` fit/specification/covariance and analytical sandwich reconstruction; compiled workflow | Complete for fixed-weight and efficient fit/covariance |
| `BayesianAnalysis`, `SamplerType`, `PointEstimateType` | Bayesian MCMC and model comparison | Bayesian analysis and pinned Numerics samplers | R `loo` and `posterior` parity; Bayesian/MCMC test sources; compiled workflow | PSIS, ARWMH adaptation, NUTS acceptance/gradient routes, live-sampler diagnostics, and rank-normalized R-hat/bulk-tail ESS verified |
| `NumericalDiff` | MLE, MAP, GMM, and leverage diagnostics | Numerical differentiation helper | Numerical-difference tests | Complete |
| `InfluenceDiagnostics`, `ObservationInfluence`, `ParetoKCategory` | Influence diagnostics | Influence diagnostics | R `loo` threshold parity, serialization tests, compiled workflow | Complete |
| `LeverageDiagnostics`, `ObservationLeverage`, `PriorComponentLeverage` | Influence diagnostics | Leverage diagnostics | Leverage unit tests; compiled workflow | Complete |
| `PriorInfluenceDiagnostics`, `PriorComponentSummary` | Influence diagnostics | Prior-influence diagnostics | Prior-influence tests; compiled workflow | Complete |
| `PosteriorPredictiveCheck`, `PriorPredictiveCheck`, `PredictiveCheckResults`, `PredictiveSummary` | Predictive checks | Predictive diagnostics | Predictive-check test sources; compiled workflow | Complete with the documented joint-prior limitation |

## Time Series and Rating Curves

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `AutoRegressive`, `ARAnalysis` | Autoregressive chapter | AR model and analysis | Transform, likelihood, generation, recovery, and compiled workflow evidence | Complete |
| `MovingAverage`, `MAAnalysis` | Moving-average chapter | MA model and analysis | Transform, likelihood, generation, recovery, and compiled workflow evidence | Complete |
| `ARIMA`, `ARIMAAnalysis`, `Transform` | ARIMA and time-series chapters | ARIMA model and analysis | Independent recurrence, forecast, transform, MLE/Bayesian recovery, and compiled workflow evidence | Complete |
| `ARIMAX`, `ARIMAXAnalysis`, `Trend`, `CovariateExtensionMethod` | ARIMAX chapter | ARIMAX model and analysis | Date-indexed likelihood, forecast, generation, recovery, and compiled workflow evidence | Complete |
| `RatingCurve`, `RatingCurveAnalysis` | Rating-curve chapter | Rating-curve model and analysis | SciPy likelihood/optimum, analytical continuity, MLE/Bayesian recovery, and compiled workflow evidence | Complete |

## Bivariate and Spatial Models

| Public API | Technical treatment | Primary implementation | Verification evidence | Status |
|---|---|---|---|---|
| `BivariateDistribution`, `BivariateAnalysis` | Bivariate distributions and analysis | Bivariate model and analysis | Twelve independent optima, seven recovery cells, unit/integration sources, and compiled workflow | Complete; marginals are conditioned on rather than jointly estimated |
| `CoincidentFrequencyAnalysis` | Coincident frequency | Coincident-frequency analysis | Numerical-integration test sources; compiled workflow | Complete |
| `SpatialGEV`, `SpatialGEVAnalysis`, `SpatialGEVUncertaintyMethod`, `SpatialDistanceMetric` | Spatial GEV hierarchy, likelihood, fitting, distance metric, uncertainty methods, and results | Spatial model and analysis | Likelihood, kriging, and geodesic oracles; criteria, cross-validation, prediction, simulation, dispatch, and nine recovery cells | Complete for the tested network sizes and dependence structures |
| `SpatialGEVSiteResults`, `SpatialGEVCrossValidationResults`, `SpatialGEVCrossValidationFoldStatus`, `SpatialGEVBootstrapResults` | Spatial result construction and leave-one-site-out validation | Spatial result types | Result DTO, fold-accounting, reduced-model, cross-validation, prediction, regional-posterior, and bootstrap sources | Complete |
| `GaussianCopula`, `CachedMultivariateNormal`, `SpatialRegressionErrors` | Spatial copula, Gaussian-process errors, and kriging | Spatial dependence support | Gaussian-copula, cached-MVN, R `mvtnorm`, and conditional-GP tests | Complete |
| `ICorrelationModel`, `CorrelationFunctionType`, `BasicExponential`, `PoweredExponential`, `Spherical` | Spatial correlation functions and bounds | Correlation implementations | Fixed-value correlation, haversine, and Cartesian distance tests | Complete |

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
