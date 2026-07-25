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
