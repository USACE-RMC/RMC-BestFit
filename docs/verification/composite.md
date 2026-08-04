<!-- verification-status: draft -->

# Composite Analysis Verification

This report records the Phase 4 evidence for TR-013, TR-014, and TR-015.

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

## Independent Posterior Resampling

TR-014 is corrected in Composite and coincident-frequency analysis. For actual retained counts
\(C_n\), both analyses use \(B=\min_n C_n\), initialize one Mersenne Twister from the owning
`BayesianAnalysis.PRNGSeed`, and generate one full-range, without-replacement index row per
source before parallel work. Composite uses configured child order in both competing-risk and
mixture/model-average branches. CFA uses semantic order copula, optional X marginal, optional
Y marginal; missing marginal chains retain the point-estimate fallback.

The policy targets the product posterior of separately fitted sources. A fixed seed, source order,
and retained-output order are exactly reproducible. Source or chain reordering changes the finite
seeded sample but not the target distribution. Child posterior objects, point estimates, weights,
dependence settings, and cancellation/progress behavior remain unchanged. `BivariateAnalysis`
is intentionally unchanged because its bands remain copula-only conditional on fixed marginals.

CFA caches the transient mapping so `GetEmpiricalDistribution(index)` returns the same realization
used by aggregate mean and credible-limit construction. The cache is regenerated deterministically
when absent and invalidated with derived results when a supplied chain or seed changes. The existing
seed attribute round-trips through UI save/open, copy, and undo. No index arrays or new serialization
fields are persisted. Legacy saved Composite/CFA summaries remain readable but require reprocessing
to adopt the corrected coupling policy.

Fast tests verify unique in-range rows, full longer-chain sampling, exact repeatability, distinct
source permutations, pairwise absolute Spearman correlation below `0.05` at 5,000 draws, both
Composite branches, unequal counts, immutable child posteriors, unchanged point estimates, CFA
source separation/fallback/cache identity, and UI seed lifecycle.

The following independently-oracled methods were each run separately through the exact guarded
runner; the complete Verification project was not run:

| Exact method | Oracle and tolerance | Result |
|---|---|---|
| `PosteriorResamplingVerificationTests.CompositePosteriorResampling_MatchesIndependentCartesianOracle` | empirical Cartesian product; posterior mean `0.02`, credible limits `0.05`, raw-paired miss at least `0.10`; reversed chain and swapped children | Passed - 2.677 s |
| `PosteriorResamplingVerificationTests.CoincidentFrequencyPosteriorResampling_MatchesIndependentClosedFormOracle` | closed-form independent Normal sum; posterior mean `0.02`, credible limits `0.05`, raw-paired miss at least `0.10`; reversed chain | Passed - 2.505 s |

## Composite Recovery Supplement - Focused Results

The supplement pins RMC-TotalRisk commit
`d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7`, its
`CompositeHazardVerification` and `CompositeResponseVerification` families, and the 2024
*Verification of the RMC-TotalRisk Software* composite hazard/response report. It carries the
shared Table 44 scenario into BestFit: Normal(10, 2), Normal(20, 1), and Normal(30, 5) with
weights 0.3/0.2/0.5. The 25 Table 45 R `mistr` mixture quantiles are embedded with their
published provenance; no generated oracle artifact or manifest entry is required.

Because `CompositeAnalysis` has no likelihood or estimator, the tests construct controlled
already-estimated `UnivariateAnalysis` children with explicit retained `MCMCResults`. The tests
make no composite parameter-recovery claim. Analytical oracles use direct Normal CDF formulas,
the published table, and deterministic bisection; posterior oracles enumerate the complete
20-by-20-by-20 product of the child supports without calling the production resampler or a
production composite constructor.

| Exact method | Independent contract | Tolerance | Status |
|---|---|---|---|
| `CompositeRecoveryTests.MixtureCdf_MatchesExactWeightedNormalSum` | weighted three-Normal CDF identity | `1E-12` absolute | Passed - 0.354 s |
| `CompositeRecoveryTests.MixtureQuantiles_MatchPublishedRMistrTable45` | 25 R `mistr` Table 45 quantiles | 1% relative | Passed - 0.387 s |
| `CompositeRecoveryTests.MixtureQuantiles_InvertAnalyticWeightedNormalCdf` | analytical CDF at each production quantile | `max(1E-8, 5E-3 * min(AEP, 1-AEP))` | Failed - 0.174 s; `1.02566838E-8` residual at AEP `2E-6` exceeds `1E-8` |
| `CompositeRecoveryTests.MaximumComposite_MatchesIndependentAndComonotonicClosedForms` | product and minimum child-CDF identities | `1E-10` absolute | Passed - 0.432 s |
| `CompositeRecoveryTests.MinimumComposite_MatchesIndependentAndComonotonicClosedForms` | union and maximum child-CDF identities | `1E-10` absolute | Passed - 0.444 s |
| `CompositeRecoveryTests.CombinationRules_SatisfyTheoreticalBracketingAndRemainDistinct` | mixture child envelope, maximum/minimum bounds, and material rule separation | `1E-12` slack; separation at least `0.10` | Passed - 0.547 s |
| `CompositeRecoveryTests.MixturePosterior_MatchesCompleteCartesianOracle` | three-child mixture product posterior | mean `0.02`; limits `0.05` | Passed - 4.758 s |
| `CompositeRecoveryTests.MaximumPosterior_MatchesCompleteCartesianOracle` | three-child independent maximum product posterior | mean `0.02`; limits `0.05` | Passed - 5.580 s |
| `CompositeRecoveryTests.MinimumPosterior_MatchesCompleteCartesianOracle` | three-child independent minimum product posterior | mean `0.02`; limits `0.05` | Passed - 4.766 s |
| `CompositeRecoveryTests.CorrelationMatrix_MinimumAndMaximumMatchBivariateNormalOrthants` | two Normal(10, 1) medians at latent rho 0.6 | `1E-8` absolute | Passed - 0.347 s |

All ten methods were executed individually. Nine passed. The inversion cell fails only at the
most extreme AEP because BestFit constructs its production quantile search with a logarithmic
X transform, while the pinned TotalRisk oracle uses its default untransformed X search. Changing
that production search convention or the declared TotalRisk tolerance requires separate numerical
authority; neither was altered during this run.

The posterior fixtures retain 5,000 draws per child by repeating 20 evenly spaced mean supports:
9.8-10.2, 19.8-20.2, and 29.8-30.2, with standard deviations fixed at 2, 1, and 5. They use
composite seed 20260803, 90% central limits, and nonexceedance probabilities 0.05, 0.25, 0.50,
0.75, and 0.95. Every fixed parent quantile must lie inside its reported band. The correlation
oracle uses

$$
P[\max(X_1,X_2)\le 10]=\frac14+\frac{\arcsin(0.6)}{2\pi},
\qquad
P[\min(X_1,X_2)\le 10]=1-P[\max(X_1,X_2)\le 10]. \tag{1}
$$

Fast tests remain authoritative for correlation-matrix ownership, serialization, weight
inertness, seed lifecycle, and exact reproducibility. The supplement deliberately does not
duplicate those deterministic contracts as long-running methods.

## Disposition

TR-013 and TR-015 remain complete with fast programmatic evidence. TR-014 retains its existing
fast and independent numerical evidence, and both exact TR-014 methods passed again. The
ten-method recovery supplement completed exact focused execution, but its one unresolved inversion
finding keeps the additional Phase 4 evidence gate open and Phase 5 blocked. The complete
Verification project was not run.

---

[Verification index](README.md) | [Technical treatment](../technical-reference/distributions/composite.md) | [Scientific findings](../technical-reference/review-findings.md#tr-014)
