<!-- verification-status: finalized -->

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

### Chunk 10B predictive-recovery coverage matrix

The deterministic oracle cells above remain theory, published, orthant, or independent Cartesian
evidence; their 8,000 combinations and 5,000 resampling draws are not recovery N and remain
unchanged. Four additional cells cover only the scientifically distinct fitted-child interactions:

| Recovery cell | Child fixtures and fitting | Composite rule | Parent response and acceptance |
|---|---|---|---|
| Mixture composite | Two separate N=1000 Normal child samples, generated with seeds 51001/51002 and fit serially through unchanged `UnivariateAnalysis` Bayesian defaults | Physical weights 0.35/0.65 | Analytical weighted-Normal quantiles at nonexceedance 0.10, 0.25, 0.50, 0.75, 0.90 inside central 95% composite bands |
| Competing-risk maximum | Same declared independently fit child design | Independent maximum; product CDF | Analytical maximum quantiles at the same probabilities inside central 95% composite bands |
| Competing-risk minimum | Same declared independently fit child design | Independent minimum; survival-product union CDF | Analytical minimum quantiles at the same probabilities inside central 95% composite bands |
| Equal-weight model averaging | Same declared independently fit child design | `AverageMethod.Equal`, requiring exactly 0.5/0.5 weights | Analytical equal-weight Normal-mixture quantiles at the same probabilities inside central 95% composite bands |

The parents are Normal(10, 2) and Normal(22, 3). Every child mean and standard deviation is
monitored directly: generating truth must lie in its locally calculated central 95% posterior
interval, R-hat must be below 1.10, and ESS must be at least 100. Prior support and discrimination
against a three-standard-deviation mean-shift alternative are checked before sampling. Composite
resampling retains the production seed/default and each selected rule is evaluated independently.
This four-cell set is sufficient because it crosses the two `CompositeAnalysis` construction
branches (competing risks versus mixture/model average), both extrema, unequal declared mixture
weights, and computed equal weights. Correlation-matrix dependence remains covered by the exact
orthant cell rather than duplicating the long correlated fitted-child likelihood.

| Exact method | Independent contract | Tolerance | Status |
|---|---|---|---|
| `CompositeOracleVerificationTests.MixtureCdf_MatchesExactWeightedNormalSum` | weighted three-Normal CDF identity | `1E-12` absolute | Passed - 0.324 s |
| `CompositeOracleVerificationTests.MixtureQuantiles_MatchPublishedRMistrTable45` | 25 R `mistr` Table 45 quantiles | 1% relative | Passed - 0.319 s |
| `CompositeOracleVerificationTests.MixtureQuantiles_InvertAnalyticWeightedNormalCdf` | analytical CDF at each production quantile | `max(1E-8, 5E-3 * min(AEP, 1-AEP))` | Passed - 0.320 s |
| `CompositeOracleVerificationTests.MaximumComposite_MatchesIndependentAndComonotonicClosedForms` | product and minimum child-CDF identities | `1E-10` absolute | Passed - 0.390 s |
| `CompositeOracleVerificationTests.MinimumComposite_MatchesIndependentAndComonotonicClosedForms` | union and maximum child-CDF identities | `1E-10` absolute | Passed - 0.391 s |
| `CompositeOracleVerificationTests.CombinationRules_SatisfyTheoreticalBracketingAndRemainDistinct` | mixture child envelope, maximum/minimum bounds, and material rule separation | `1E-12` slack; separation at least `0.10` | Passed - 0.474 s |
| `CompositeOracleVerificationTests.MixturePosterior_MatchesCompleteCartesianOracle` | three-child mixture product posterior | mean `0.02`; limits `0.05` | Passed - 3.555 s |
| `CompositeOracleVerificationTests.MaximumPosterior_MatchesCompleteCartesianOracle` | three-child independent maximum product posterior | mean `0.02`; limits `0.05` | Passed - 3.228 s |
| `CompositeOracleVerificationTests.MinimumPosterior_MatchesCompleteCartesianOracle` | three-child independent minimum product posterior | mean `0.02`; limits `0.05` | Passed - 3.394 s |
| `CompositeOracleVerificationTests.CorrelationMatrix_MinimumAndMaximumMatchBivariateNormalOrthants` | two Normal(10, 1) medians at latent rho 0.6 | `1E-8` absolute | Passed - 0.296 s |
| `CompositePredictiveRecoveryTests.MixtureComposite_EndToEndPredictiveRecovery` | fitted-child 0.35/0.65 mixture parent quantiles | central 95% child and composite bands; R-hat/ESS | Passed - 15.711 s |
| `CompositePredictiveRecoveryTests.MaximumComposite_EndToEndPredictiveRecovery` | fitted-child independent maximum parent quantiles | central 95% child and composite bands; R-hat/ESS | Passed - 15.380 s |
| `CompositePredictiveRecoveryTests.MinimumComposite_EndToEndPredictiveRecovery` | fitted-child independent minimum parent quantiles | central 95% child and composite bands; R-hat/ESS | Passed - 14.958 s |
| `CompositePredictiveRecoveryTests.EqualWeightModelAverage_EndToEndPredictiveRecovery` | fitted-child equal-weight parent quantiles and exact 0.5/0.5 weights | central 95% child and composite bands; R-hat/ESS | Passed - 15.210 s |

The ten current oracle identities and four predictive-recovery identities pass. `CompositeRecoveryTests`
is the historical class name and none of its results was transferred; all ten oracle methods were
executed under their current exact `CompositeOracleVerificationTests` identities on 30 August 2026.
The inversion cell originally failed only at the most extreme AEP when
the analysis used a logarithmic X search despite exposing no user-visible transform configuration.
After `CompositeAnalysis` adopted the approved `XTransform.None` contract, the same exact method
passed through `scripts/run-verification-test.ps1` without changing its fixture, formula, seed, or
probability-dependent tolerance. The 20 August 2026 invocation built with zero warnings/errors,
executed one test, produced one passing TRX under `TestResults/VerificationFocused/20260820-093600-*`,
and ran the method in 0.770 s. The earlier residual remains recorded in the table as failure history.

The first predictive-mixture attempt exposed two Verification-only construction errors in sequence:
posterior coordinate arrays were declared sorted before percentile calculation, then composite
exceedance probabilities were supplied in descending order. Both one-result failure TRXs are
discarded as scientific evidence. After sorting retained arrays and supplying the same predeclared
probability set in the required order, the exact mixture method and the other three predictive
methods passed. No child estimator, parent, prior, sampler, seed, response set, or acceptance rule
changed.

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
ten current oracle identities and all four fitted-child predictive-recovery identities pass. The
Composite supplement no longer blocks Phase 5. The complete Verification project was not run.

---

[Verification index](README.md) | [Technical treatment](../technical-reference/distributions/composite.md) | [Scientific findings](../technical-reference/review-findings.md#tr-014)
