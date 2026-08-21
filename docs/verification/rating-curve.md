<!-- verification-status: draft -->

# Rating-Curve Verification

This chapter records the Phase 6 (Batch 6.1) verification of the stage-discharge rating-curve model:
the discharge-space likelihood (TR-043), continuity at activation stages (TR-044), aligned-pair
validation (TR-045), and the replication of the three synthetic cases of
`examples/6-rating-curve-analysis` as Verification recovery cells. The technical treatment is in the
[rating-curve chapter](../technical-reference/analysis/rating-curve.md); the findings are in the
[review register](../technical-reference/review-findings.md#tr-043).

## Status

| Item | State | Evidence |
|---|---|---|
| TR-043 discharge-space likelihood | Confirmed defect (21 August 2026); fix plan pending approval | Three exact guarded cells fail by exactly the base-10 change-of-variables sum (below) |
| TR-044 continuity at activation stages | Confirmed defect (21 August 2026); fix plan and exponent bound pending approval | Default exponent lower bound 0 admits a discontinuous model; the analytical increment cells pass |
| TR-045 aligned-pair validation | Confirmed defect (21 August 2026); fix plan pending approval | Fast contract `Validate_UnmatchedNonPositiveDischarge_RemainsValidAndIsReported` fails on current source |
| Example replication recovery cells | Ready - acceptance rule pending approval; not yet run | `RatingCurveExampleRecoveryTests` (3 MLE + 3 Bayesian) against the committed fixtures |

No production code has changed. The complete Verification project was not run.

## Fixtures and oracles

The shipped example project `examples/6-rating-curve-analysis/synthetic-rating-curve-examples.bestfit`
and its workbook `Synthetic Data.xlsx` hold three synthetic cases on one shared stage series. The
generator `verification/python/rating-curve/generate_rating_curve_example_oracles.py` (SHA-256
`17d51387d5565e97e4fab1dfeecff755413162a691335fdec2dd22eecebe7936`) reads the workbook's stored
uniform draws with the standard library, regenerates every case from the recipe, and asserts that the
regenerated values match the workbook (maximum log10 difference `8.9e-16`, maximum relative discharge
difference `3.9e-15`, tolerance `1e-9`):

- stage `h = 1 + 19 r1`, 300 daily observations from 2000-01-01 (minimum 1.1425, maximum 19.8940);
- log10 discharge `= log10(sum over active controls of alpha_k (h - h_k)^beta_k) + 0.05 PhiInverse(r2)`;
- one segment: Manning triangular channel `h1 = 1`, `alpha1 = 1.7534628349561987`
  (`log10 alpha1 = 0.24389656534469958`), `beta1 = 8/3`;
- two segments: plus a rectangular overbank `h2 = 10`, `alpha2 = 883.6898943878617`
  (`log10 alpha2 = 2.9462998885606555`), `beta2 = 1.67`;
- three segments: plus a second overbank `h3 = 15`, `alpha3 = 4069.4267574438463`
  (`log10 alpha3 = 3.6095332363473847`), `beta3 = 1.67`;
- the example analyses use the default flat priors, Jeffreys' rule for the scale, and the untouched
  production `BayesianAnalysis` defaults (DEMCzs, seed 12345, 3,500/1,750, 90% interval, posterior mean).

Two artifacts are committed and listed in `verification/data/MANIFEST.md` before any C# result was observed:

| Artifact | Contents | SHA-256 |
|---|---|---|
| `verification/data/rating-curve/rating-curve-example-fixtures.json` | Dates, stage, the three discharge series, generating parameters in the BestFit layout, the true curve on the 1.5-19.5 stage grid, the replicated BestFit default bounds (which equal the example project's stored bounds), and an independent SciPy conditional maximum-likelihood optimum per case | `e472b8d32f307f542287dee379d39fb6cd297ab725b01b19335a5c4c9de40900` |
| `verification/data/rating-curve/rating-curve-likelihood-oracle.json` | Per-observation log-space Gaussian terms, base-10 change-of-variables terms `log(Q ln 10)`, and SciPy `lognorm(s = sigma ln 10, scale = predicted)` discharge-space terms at the generating parameters and at the independent optimum | `53b7dacbb09fffa912dd1ef7da7b747877a252955359090b29f899fd4fe82168` |

The independent optimum minimizes the log10 residual sum of squares with `scipy.optimize.least_squares`
(`trf`, 25 deterministic starts, seed 20260821) inside the replicated BestFit bounds and profiles the
scale `sigma = sqrt(RSS / n)`; it is the conditional maximum-likelihood point of the log-space Gaussian
model, and the discharge-space log likelihood follows by subtracting the change-of-variables sum.

| Case | Generating parameters | Independent optimum | RSS | Example posterior mean (project, documentation only) |
|---|---|---|---|---|
| One segment | `[1, 0.24390, 2.66667, 0.05]` | `[0.98688, 0.22099, 2.69213, 0.05065]` | `0.7697448599` (single optimum) | `[0.9866, 0.2206, 2.6925, 0.0511]` |
| Two segments | `[1, 0.24390, 2.66667, 10, 2.94630, 1.67, 0.05]` | `[0.98506, 0.21647, 2.69971, 9.85068, 2.84131, 1.78502, 0.05003]` | `0.7509746230` (single optimum) | `[0.9846, 0.2158, 2.7004, 9.8463, 2.8380, 1.7882, 0.0507]` |
| Three segments | `[1, 0.24390, 2.66667, 10, 2.94630, 1.67, 15, 3.60953, 1.67, 0.05]` | `[0.98544, 0.21738, 2.69800, 9.72863, 2.73605, 1.93627, 15.08589, 3.51082, 1.80235, 0.04975]` | `0.7425121238` (three further local optima at `0.9899`, `1.0117`, `1.0117`) | `[0.9849, 0.2166, 2.6988, 9.7050, 2.7173, 1.9533, 14.6480, 3.1560, 2.1716, 0.0507]` |

The third control of the three-segment case trades off along a ridge (`h3`, `log10 alpha3`, `beta3`),
which is why the replication acceptance rule carries wider three-segment parameter tolerances and the
same curve-level checks for every case.

## TR-043 - Discharge-space likelihood

The observation model `log10 Q_i ~ Normal(log10 q(h_i), sigma^2)` implies the discharge-space density
`log f(Q_i) = log phi((log10 Q_i - log10 q_i) / sigma) - log sigma - log(Q_i ln 10)`. The exact
methods `RatingCurveLikelihoodOracleTests.{One,Two,Three}Segment_DataLogLikelihood_IsDischargeSpaceDensity`
assert that the scalar, pointwise, and component data log likelihoods equal the SciPy terms at the
generating parameters and at the independent optimum (`1e-8` sums, `1e-10` terms), after establishing
that the Numerics base-10 `LogNormal` density reproduces the SciPy oracle to the same tolerances.

Confirmation runs (21 August 2026, one method per guarded invocation, current source):

| Case | `DataLogLikelihood` at truth | Discharge-space oracle | Difference | Change-of-variables sum | Outcome |
|---|---|---|---|---|---|
| One segment | `465.49695577450461` | `-1577.7594156516989` | `2043.2563714262035` | `2043.2563714262035` | Failed - confirms defect |
| Two segments | `465.49695577450393` | `-1850.3134085800252` | `2315.8103643545292` | `2315.8103643545292` | Failed - confirms defect |
| Three segments | `465.49695577450370` | `-1888.5687896304166` | `2354.0657454049206` | `2354.0657454049206` | Failed - confirms defect |

The current implementation returns the log-space Gaussian value (equation RC.4 of the technical
reference) exactly; the difference from the discharge-space density equals the change-of-variables
sum to all printed digits. The same three cells are the acceptance tests for the correction.

## TR-044 - Continuity at activation stages

Control `k` contributes `10^{a_k} (h - h_k)^{beta_k}` for `h > h_k` and nothing below, so the curve is
continuous at `h_k` exactly when `beta_k > 0`; with `beta_k = 0` the added control jumps by `10^{a_k}`.
`RatingCurveContinuityVerificationTests` embeds the analytical formulas:

| Exact method | Contract | Outcome (21 August 2026) |
|---|---|---|
| `AddedControl_TwoSidedIncrementAtActivation_MatchesAnalyticalPowerLaw` | Two-sided increment across `h2` equals the smooth first-control increment plus `10^{a2} epsilon^{beta2}` for `beta2` in {0.1, 1, 1.67, 2.5} and `epsilon` in {1e-3, 1e-6, 1e-9}, relative `1e-10` | Passed (after evaluating the formula on the same floating-point stage values the model receives; the first attempt compared against the exact `epsilon`, whose representation in `h2 + epsilon` differs by about `1e-6` relative) |
| `ZeroExponent_AddedControlJumpsByItsCoefficientAtActivation` | With `beta2 = 0` the added control jumps by `10^{a2}` at `epsilon = 1e-12` | Passed (documents the discontinuity the bound must exclude) |
| `DefaultExponentLowerBounds_AreStrictlyPositive_{One,Two,Three}Segment` | Every default exponent lower bound and prior support minimum exceeds zero | Failed - confirms defect: `Exponent (beta1) lower bound 0 admits a zero exponent` |
| `ExponentsAtDefaultLowerBound_AddedControlIncrementsVanishAtActivation` | With every exponent at its default lower bound, each control's added increment at offset `1e-12` is at most 0.999 times its value at offset `1e-6` | Failed - confirms defect: control 1 increment `1.7534628349561987` at both offsets (the coefficient `alpha1`) |

## TR-045 - Aligned-pair validation

The likelihood uses only the date-inner-joined stage/discharge pairs, but `Validate()` rejects the
model if any value in the entire discharge series is nonpositive. The fast contract
`RatingCurveTests.Validate_UnmatchedNonPositiveDischarge_RemainsValidAndIsReported` builds twenty valid
aligned daily pairs plus one nonpositive discharge record on a date without a stage observation and
requires a valid result with a message that reports the ignored unmatched record. On current source it
fails with `Error: All discharge values must be positive (log-space model requires Q > 0)`.

## Example replication recovery cells

`RatingCurveExampleRecoveryTests` builds each case exactly as the example project does (default flat
priors, Jeffreys' rule for the scale, no `BayesianAnalysis` setting assigned; the resolved production
DEMCzs defaults are asserted before and after sampling) and compares with the independent optimum and
the generating curve. Proposed acceptance rule, declared before the first run and awaiting approval:

- MLE cells (`Mle_{One,Two,Three}Segment_RecoversExampleCurve`): the data log likelihood at the
  independent optimum equals the oracle (`1e-8`); the production MLE (default Differential Evolution)
  reaches the optimum's log likelihood within `1e-5`; parameters agree with the optimum at `1e-3`
  relative (one and two segments) or `1e-2` (three segments) with a `1e-3` floor; the estimate lies
  inside the model bounds; the fitted curve lies within 5% of the true curve at every grid stage from 2.0.
- Bayesian cells (`Bayesian_{One,Two,Three}Segment_RecoversExampleCurve`): R-hat below 1.1 and ESS
  above 100 for every parameter; sampled MAP agrees with the independent optimum at 5% (one and two
  segments) or 10% (three segments) with a `1e-3` floor; the median posterior curve lies within 5% of the
  true curve at every grid stage from 2.0; the true curve lies inside the 90% posterior band at no fewer
  than 90% of those grid stages.

The six cells are run only after the acceptance rule is approved, and after the TR-043 correction
because their same-point likelihood check is written against the discharge-space contract. The existing
`RatingCurveMLERecoveryTests` (9 methods) and `RatingCurveBayesianRecoveryTests` (10 methods) remain
supplementary self-generated recovery evidence.

## Next steps

1. Approve the TR-043 correction (change-of-variables term in the scalar, pointwise, and component
   likelihoods), the TR-044 exponent lower bound and legacy-bound handling, the TR-045 aligned-pair
   validation with reporting, and the replication acceptance rule.
2. Implement the approved corrections with fast regressions, rerun the exact cells above and the six
   replication cells one at a time, and record the outcomes here, in the test inventory, the manifest,
   the register, and the technical reference.

[Verification index](README.md) | [Technical treatment](../technical-reference/analysis/rating-curve.md) | [Scientific findings](../technical-reference/review-findings.md#tr-043)
