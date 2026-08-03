<!-- verification-status: verified-partial -->

# Composite Analysis Verification

This report records the Phase 4 evidence for TR-013 and TR-015. TR-014 remains open and is intentionally excluded from the implementation and closeout claim.

## Criterion Weighting

Fast core tests verify the following contracts through `CompositeAnalysis.EstimateModelWeights()` and `Validate()`:

- non-finite AIC, BIC, DIC, WAIC, LOOIC, or RMSE values and negative RMSE values are unusable;
- an unusable child receives exactly zero weight and a named warning when at least one usable child remains;
- a model average with no usable selected criterion is invalid and leaves every weight at exactly zero;
- exact-zero RMSE children divide unit weight equally while every positive or invalid RMSE child receives zero, avoiding division by zero and NaN;
- ordinary finite criteria retain the established Numerics weighting formula; and
- all computed weights are finite and normalized.

Bulletin 17C is not rejected as a composite child. It participates normally in Equal, AIC, BIC, and RMSE averaging. Its `BayesianAnalysis` object is a compatibility container for GMM/frequentist uncertainty rather than a likelihood-based posterior, so DIC, WAIC, and LOOIC are unavailable: for those methods B17C receives zero weight with a named warning when another usable child remains. Only the absence of any usable criterion invalidates the model average; the child type itself does not.

## Correlation-Matrix Configuration

The authorized `CorrelationMatrix` property is available on both core and UI composite analyses without changing any existing constructor, method, parameter vector, seed, default, or result signature. Tests verify:

- defensive-copy assignment, retrieval, and UI copy behavior;
- rejection of nonsquare, non-finite, out-of-range, non-unit-diagonal, asymmetric, dimensionally incompatible, and non-positive-definite matrices;
- invariant-culture XML rows named `CorrelationMatrix` and `Correlation_Row`;
- optional legacy deserialization with a null matrix;
- SQLite UI save/open round-trip through an appended optional `CorrelationMatrix` column;
- propagation into point-estimate and uncertainty-result `CompetingRisks` objects; and
- deterministic seeded simulation after result construction.

The matrix is required only when the composite type is `CompetingRisks` and dependency is `CorrelationMatrix`. Its dimension must equal the child count. The strict positive-definiteness rule matches the pinned Numerics multivariate-Normal requirement.

## Deferred Posterior Coupling

TR-014 remains open by direction. Composite realization construction still pairs separately fitted child outputs by raw index. No resampling, permutation, seed, output-length, point-estimate, or bivariate-posterior behavior was changed. The final policy must be reviewed together with bivariate posterior consumers before implementation.

## Disposition

TR-013 and TR-015 are complete with fast programmatic evidence. TR-014 remains open, so the overall Phase 4 ledger remains in progress. The complete Verification project was not run.

---

[Verification index](README.md) | [Technical treatment](../technical-reference/distributions/composite.md) | [Scientific findings](../technical-reference/review-findings.md#tr-013)
