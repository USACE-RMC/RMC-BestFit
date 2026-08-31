<!-- verification-status: finalized -->

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
| TR-043 discharge-space likelihood | Corrected (approved 21 August 2026) | Three exact oracle cells pass after failing by exactly the change-of-variables sums on the uncorrected source; fast hand-calculation and identity contracts |
| TR-044 continuity at activation stages | Corrected (approved 21 August 2026; default exponent lower bound 0.1, legacy bounds kept with a warning) | Seven exact continuity/bound cells pass; fast bound, legacy-warning, and XML round-trip contracts |
| TR-045 aligned-pair validation | Corrected (approved 21 August 2026) | Fast aligned-pair, unmatched-record, and count-reporting contracts pass |
| Example replication recovery cells (1,000 observations) | Passed 6/6 (21 August 2026) | `RatingCurveExampleRecoveryTests`, production defaults, independent SciPy optima |
| Reconciled rating-curve recovery cells (1,000 observations) | Passed 10/10 (31 August 2026) | Five scientifically distinct fixtures retained for both MLE and Bayesian recovery; ten redundant declarations consolidated before execution; log10 residual and parameter uncertainty propagated separately into simultaneous predictive bands |

No sampler, generator or estimator seed, prior other than the approved exponent bound, production
optimizer default, convergence rule, or tolerance of a closed phase changed. The new Verification-only
predictive-draw seeds are recorded below. The complete Verification project was not run.

## Fixtures and oracles

The shipped example project `examples/6-rating-curve-analysis/synthetic-rating-curve-examples.bestfit`
and its workbook `Synthetic Data.xlsx` hold three synthetic cases on one shared stage series. The
generator `verification/python/rating-curve/generate_rating_curve_example_oracles.py` (SHA-256
`949a303dd79c4e4d08f82d65c3ff1a8d869bcdbfbdf1fefafd2f0ccf73cb1bbb`; Python 3.12.13, NumPy 2.4.2,
SciPy 1.17.1) reads the workbook's stored uniform draws with the standard library, regenerates every
case from the recipe, and asserts that the regenerated values match the workbook (maximum log10
difference `8.9e-16`, maximum relative discharge difference `3.9e-15`, tolerance `1e-9`):

- stage `h = 1 + 19 r1` (uniform on (1, 20)), daily observations from 2000-01-01;
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
| `verification/data/rating-curve/rating-curve-example-fixtures.json` | The shipped 300-observation data (`cases`) and a seeded 1,000-observation replication block with the identical recipe (`replication_n1000`, numpy `default_rng(20260822)`); per case the generating parameters in the BestFit layout, the true curve on the 1.5-19.5 stage grid, the replicated BestFit default bounds (identical to the example project's stored bounds), the independent SciPy conditional maximum-likelihood optimum, and its asymptotic standard errors | `a2cc2e85d61788be5b87dd5e0c8ea0e71086664c68d285165f7f93658809d780` |
| `verification/data/rating-curve/rating-curve-likelihood-oracle.json` | Per-observation log-space Gaussian terms, base-10 change-of-variables terms `log(Q ln 10)`, and SciPy `lognorm(s = sigma ln 10, scale = predicted)` discharge-space terms at the generating parameters and at the independent optimum (300-observation data) | `7ea23979e7e30745e201b74377c69a825053a73f69080aee7744e6efbee4dae2` |

The independent optimum minimizes the log10 residual sum of squares with `scipy.optimize.least_squares`
(`trf`, 25 deterministic starts, seed 20260821) inside the replicated BestFit bounds and profiles the
scale `sigma = sqrt(RSS / n)`; it is the conditional maximum-likelihood point of the log-space Gaussian
model, and the discharge-space log likelihood follows by subtracting the change-of-variables sum.

### Identifiability of the shipped 300-observation design

| Case (300 observations) | Independent optimum | Standard errors | Comment |
|---|---|---|---|
| One segment | `[0.98688, 0.22099, 2.69213, 0.05065]` | `[0.006, 0.012, 0.012, 0.002]` | Well identified |
| Two segments | `[0.98506, 0.21647, 2.69971, 9.85068, 2.84131, 1.78502, 0.05003]` | `[0.007, 0.016, 0.021, 0.071, 0.045, 0.045, 0.002]` | Second control 2.3 SE from truth in this realization |
| Three segments | `[0.98544, 0.21738, 2.69800, 9.72863, 2.73605, 1.93627, 15.08589, 3.51082, 1.80235, 0.04975]` | `[0.007, 0.016, 0.021, 0.117, 0.090, 0.113, 0.501, 0.297, 0.371, 0.002]` | Third control weakly identified: `SE(beta3) = 0.37` (22% relative); optimum within 0.4 SE of truth |

Across eight fresh seeds of the same recipe the estimator is unbiased, but `beta3` ranges 1.36-2.17 at
300 observations (standard deviation 0.25) and 1.53-1.96 at 1,000 (standard deviation 0.12). A 10%
parameter gate on the third control is therefore below the sampling variability of the shipped design,
which is why the recovery cells use the 1,000-observation block (program policy: recovery fixtures use
300 to 1,000 observations and prefer 1,000) and compare the production estimates primarily with the
independent optimum. No seed was searched: in the seeded 1,000-observation realization the third
control's optimum (`h3 = 15.33`, `log10 alpha3 = 3.83`, `beta3 = 1.34`; standard errors 0.14, 0.08,
0.11) sits about three standard errors along its ridge from the truth while the first two controls and
the scale are at truth; a Nelder-Mead start at the truth converges to the same optimum, the profile over
`beta3` places the truth 4.8 log-units below the optimum, and the overall fit at truth is unremarkable
(6.4 log-units below the optimum for nine shape parameters). The replication claim - that BestFit
reproduces the independent optimum and the example's curve - does not depend on that realization.

## TR-043 - Discharge-space likelihood

The observation model `log10 Q_i ~ Normal(log10 q(h_i), sigma^2)` implies the discharge-space density
`log f(Q_i) = log phi((log10 Q_i - log10 q_i) / sigma) - log sigma - log(Q_i ln 10)`. The exact
methods `RatingCurveLikelihoodOracleTests.{One,Two,Three}Segment_DataLogLikelihood_IsDischargeSpaceDensity`
assert that the scalar, pointwise, and component data log likelihoods equal the SciPy terms at the
generating parameters and at the independent optimum (`1e-8` sums, `1e-10` terms), after establishing
that the Numerics base-10 `LogNormal` density reproduces the SciPy oracle to the same tolerances.

Confirmation runs on the uncorrected source (21 August 2026):

| Case | `DataLogLikelihood` at truth | Discharge-space oracle | Difference | Change-of-variables sum | Outcome |
|---|---|---|---|---|---|
| One segment | `465.49695577450461` | `-1577.7594156516989` | `2043.2563714262035` | `2043.2563714262035` | Failed - confirmed the defect |
| Two segments | `465.49695577450393` | `-1850.3134085800252` | `2315.8103643545292` | `2315.8103643545292` | Failed - confirmed the defect |
| Three segments | `465.49695577450370` | `-1888.5687896304166` | `2354.0657454049206` | `2354.0657454049206` | Failed - confirmed the defect |

The uncorrected implementation returned the log-space Gaussian value (equation RC.4 of the technical
reference) exactly. The approved correction adds the per-pair term `-log(Q_i ln 10)` in
`DataLogLikelihood`, `PointwiseDataLogLikelihood`, and `PointwiseDataLogLikelihoodComponents`
(cached with the aligned-observation cache; a nonpositive aligned discharge makes every path
negative-infinite); the three cells then pass. Fast contracts pin a hand-computed discharge-space
density at the generating parameters and with residuals, the parameter-free difference identity, the
pointwise and component sum identities, and the nonpositive-discharge behavior. Maximum-likelihood,
MAP, and posterior parameter estimates are unchanged; AIC, BIC, DIC, WAIC, and LOOIC shift by the data
constant `-sum log(Q_i ln 10)` (release note).

## TR-044 - Continuity at activation stages

Control `k` contributes `10^{a_k} (h - h_k)^{beta_k}` for `h > h_k` and nothing below, so the curve is
continuous at `h_k` exactly when `beta_k > 0`; with `beta_k = 0` the added control jumps by `10^{a_k}`.
`RatingCurveContinuityVerificationTests` embeds the analytical formulas:

| Exact method | Contract | Uncorrected source | After the correction |
|---|---|---|---|
| `AddedControl_TwoSidedIncrementAtActivation_MatchesAnalyticalPowerLaw` | Two-sided increment across `h2` equals the smooth first-control increment plus `10^{a2} epsilon^{beta2}` for `beta2` in {0.1, 1, 1.67, 2.5} and `epsilon` in {1e-3, 1e-6, 1e-9}, relative `1e-10` (evaluated on the model's floating-point stage values) | Passed | Passed |
| `ZeroExponent_AddedControlJumpsByItsCoefficientAtActivation` | With `beta2 = 0` the added control jumps by `10^{a2}` at `epsilon = 1e-12` | Passed (documents the discontinuity) | Passed |
| `DefaultExponentLowerBounds_AreStrictlyPositive_{One,Two,Three}Segment` | Every default exponent lower bound and prior support minimum exceeds zero | Failed - lower bound 0 | Passed - lower bound 0.1 |
| `ExponentsAtDefaultLowerBound_AddedControlIncrementsVanishAtActivation` | With every exponent at its default lower bound, each control's added increment at offset `1e-12` is at most 0.999 times its value at offset `1e-6` | Failed - constant increment `1.7534628349561987` | Passed - ratio `(1e-6)^0.1 = 0.251` |

The approved correction sets the default exponent lower bound and prior minimum to 0.1
(`Uniform(0.1, 5)`); legacy projects restore their stored bounds verbatim and receive the non-blocking
validation warning `Warning: Exponent (beta_k) lower bound ... admits a zero exponent ...`. Fast
contracts cover the new bounds (`DefaultFlatPriors_BetaBounds_ArePositiveForAllSegments`) and the legacy
warning with its XML round trip (`Validate_LegacyZeroExponentBound_WarnsButRemainsValid`).

## TR-045 - Aligned-pair validation

The likelihood uses only the date-inner-joined stage/discharge pairs, but `Validate()` rejected the
model if any value in the entire discharge series was nonpositive. The fast contract
`RatingCurveTests.Validate_UnmatchedNonPositiveDischarge_RemainsValidAndIsReported` (twenty valid
aligned pairs plus one nonpositive discharge record on a date without a stage) failed on the
uncorrected source with `Error: All discharge values must be positive (log-space model requires Q > 0)`.
The approved correction rebuilds the date alignment inside `Validate()`, reports an error only for
nonpositive date-aligned discharge, and adds the non-blocking warning
`Warning: {s} stage and {d} discharge record(s) are unmatched by date and are ignored by the
rating-curve likelihood; {m} of the ignored discharge record(s) are nonpositive.`; the UI validation
adapter renders the `Warning:` prefix as a warning. `Validate_ReportsUnmatchedRecordCounts` checks the
counts and `Validate_NonPositiveDischarge_IsInvalid` keeps the aligned-pair error.

## Example replication recovery cells

`RatingCurveExampleRecoveryTests` builds each case exactly as the example project does (default flat
priors, Jeffreys' rule for the scale, no `BayesianAnalysis` setting assigned; the resolved production
DEMCzs defaults are asserted before and after sampling) on the 1,000-observation block and compares
with the independent optimum and the generating curve. Acceptance rule as approved and amended on
21 August 2026:

- MLE cells: data log likelihood at the independent optimum within `1e-8`; the production MLE
  (default Differential Evolution) reaches the optimum's log likelihood within `1e-4` (documented
  convergence margin); parameters within `1e-3` relative (one and two segments) or `1e-2` (three
  segments) with a `1e-3` floor; the estimate lies inside the model bounds; the fitted curve lies within
  0.5% of the independent optimum's curve and within 10% of the true curve at every grid stage from 2.0.
- Bayesian cells: R-hat below 1.1 and ESS above 100 for every parameter; the sampled MAP from
  `MCMCResults` within 5% (one and two segments) or 10% (three segments) of the independent optimum
  with a `1e-3` floor; the sampled-MAP curve within 2% of the optimum's curve and within 10% of the
  true curve; the fraction of grid stages at which the true curve lies inside the 90% posterior band is
  reported, not asserted.

| Exact method | Outcome (21 August 2026) | Wall-clock per guarded invocation |
|---|---|---|
| `Mle_OneSegment_RecoversExampleCurve` | Passed | 6.4 s |
| `Mle_TwoSegment_RecoversExampleCurve` | Passed | 6.5 s |
| `Mle_ThreeSegment_RecoversExampleCurve` | Passed | 15.9 s |
| `Bayesian_OneSegment_RecoversExampleCurve` | Passed; true curve inside the 90% band at 36 of 36 grid stages | 37.4 s |
| `Bayesian_TwoSegment_RecoversExampleCurve` | Passed; 36 of 36 | 71.5 s |
| `Bayesian_ThreeSegment_RecoversExampleCurve` | Passed; 34 of 36 | 122.9 s |

Failure history against the rule as first declared (shipped 300-observation data): the two-segment MLE
curve exceeded a 5% true-curve band at stage 10 (+5.2%), the three-segment production optimum fell
`1.6e-5` short of a `1e-5` optimality tolerance, the one- and two-segment Bayesian cells placed the true
curve inside the 90% band at only 21/36 and 28/36 grid stages (the realization's curve sits 2-4% from the
truth at the upper stages, an offset the independent optimum shares), and the three-segment sampled MAP's
`beta3` differed from the optimum by 10.9% along the ridge (posterior kernel 0.65 log-units below the
optimum's). Each amendment above was approved before the cells were rerun, and the fixtures moved to
1,000 observations under the recovery sample-size policy.

## Reconciled recovery coverage and identification matrix

The legacy ten-fixture Cartesian set mixed distinct hydraulic interactions with arbitrary names for
width, slope, sample size, and range. Chunk 12 reconciles the design before executing it. Every retained
fixture generates exactly 1,000 stage-discharge observations, preserves its existing seed, generating
physics, Bayesian priors and sampler configuration, and uses production Differential Evolution with
untouched default tolerances for MLE. The Bayesian recovery cells publish posterior MAP as their point
estimator while retaining the same posterior draws, intervals, diagnostics, and curve bands, and use
the model coordinate order shown below. The observational law is Normal error with standard deviation
`sigma` in base-10 log discharge; the physical curve is the additive BaRatin response
`sum alpha_k (h-h_k)^beta_k I(h>h_k)`.

| Retained fixture (seed) | Controls and parameter order | Stage range | Scientific interaction | Identification and predeclared response stages |
|---|---|---|---|---|
| `SingleSegment_Default` (12345) | 1; `[h1, log10(alpha1), beta1, sigma]` | 1-10 | Standard single-control geometry and moderate error | All four coordinates identified; curve stages 1.25, 3, 6, 9.5 |
| `SingleSegment_LowNoise` (54321) | 1; same order | 1-12 | Observation/error-model behavior at `sigma=0.02` | All four coordinates identified; curve stages 1.25, 3, 7, 11.5 |
| `SingleSegment_WideRange` (99999) | 1; same order | 1-25 | Range leverage over a wide calibration domain | All four coordinates identified; curve stages 1.25, 5, 15, 24.5 |
| `TwoSegment_BankfullTransition` (44444) | 2; `[h1, log10(alpha1), beta1, h2, log10(alpha2), beta2, sigma]` | 0.5-12 | In-bank/overbank activation at `h2=6` | Exact exclusive allocation 495 below and 505 at/above `h2`; controls 1/2 are active for 1,000/505 observations. Individual control coordinates may trade along a likelihood ridge; response stages 0.75, 2, 5.5, 6.5, 9, 11.5 and raw `sigma` are evaluated |
| `ThreeSegment_MultipleControl` (66666) | 3; `[h1, log10(alpha1), beta1, h2, log10(alpha2), beta2, h3, log10(alpha3), beta3, sigma]` | 0.5-10 | Low-flow, channel, and floodplain controls activating at 3 and 7 | Exact exclusive allocation 270/406/324; controls 1/2/3 are active for 1,000/730/324 observations. Individual control coordinates may trade along ridges; response stages 0.75, 2, 2.75, 3.25, 5.5, 6.75, 7.25, 8.5, 9.75 and raw `sigma` are evaluated |

The MLE cells require an unregularized observed-information covariance. Identifiable single-control
coordinates and the multi-control scale use absolute standardized error no greater than 1.96. Response
recovery is predictive rather than a latent mean-curve Wald check. Twenty thousand bounded
multivariate-Normal parameter draws propagate the full covariance through the nonlinear curve on the
log10-discharge scale (seed 20260831); an independent Normal residual using each draw's `sigma` is then
added at every ordinate (seed 20260901). The empirical 95th percentile of the maximum absolute
standardized deviation across the complete predeclared grid defines one simultaneous 95% predictive band.

The Bayesian cells use central 95% posterior coordinate intervals, require R-hat below 1.10 and ESS at
least 100 for every coordinate, and require all single-control truths and the multi-control `sigma` truth
inside their coordinate intervals. Each retained posterior draw separately propagates parameter
uncertainty; an independent draw-specific log10 residual is added at every ordinate (seed 20260902), and
the same max-|t| construction yields one MAP-centered simultaneous 95% posterior-predictive band over the
complete grid. Multi-control hydraulic coordinates are deliberately not claimed individually recovered;
their generating responses are evaluated in that predictive space. MAP is the declared Bayesian point
estimator but does not replace any coordinate interval, R-hat, ESS, or predictive-band requirement. Prior
support is checked for every Bayesian coordinate. The exact stage allocations are reported as
identification diagnostics, not substituted as interchangeable effective sample sizes: the likelihood
covariance and posterior already reflect which observations activate each control. No empirical fitted
curve is used as an oracle, and no conditional 5% tail rule substitutes for the stated 95% rules.

`SingleSegment_LargeSample` is consolidated because every recovery fixture now has the same required
N=1000. `SingleSegment_SteepChannel` and `SingleSegment_WideChannel` are consolidated into the standard,
wide-range, and multiple-control cells because their differently named exponent/coefficient variants do
not add another identification mechanism. `TwoSegment_Default` is consolidated into the sharper bankfull
transition, and `ThreeSegment_Default` into the multiple-control activation case. Their historical results
are not transferred to another identity. The retained likelihood-oracle and example-replication cells
remain separate evidence. No `bdrc` parity is claimed because exact equivalence of its additive-control
law, base-10 error density, segmentation, and parameterization has not been established; the independent
SciPy likelihood and optimum artifacts above cover the compatible overlap.

### Current exact outcomes

| Retained fixture | MLE | Bayesian |
|---|---|---|
| `SingleSegment_Default` | Passed | Passed |
| `SingleSegment_LowNoise` | Passed | Passed |
| `SingleSegment_WideRange` | Passed | Passed |
| `TwoSegment_BankfullTransition` | Passed; simultaneous critical 2.62536; stage-6.5 band `[578.013, 950.262]` contains parent 724.391 | Passed; simultaneous critical 2.63612; stage-6.5 band `[576.299, 953.454]` contains parent 724.391 |
| `ThreeSegment_MultipleControl` | Passed; simultaneous critical 2.76477; stage-8.5 band `[1695.479, 2851.016]` contains parent 2144.093 | Passed; simultaneous critical 2.75410; stage-8.5 band `[1701.739, 2838.807]` contains parent 2144.093 |

Every latest guarded run produced exactly one passing TRX result. Earlier pointwise mean-curve results are
superseded because they omitted the residual term required by the declared log10 observation model and
treated multiple pointwise intervals as one 95% statement. Parents, generator seeds, stage grids, priors,
samplers, chains, convergence rules, optimizer defaults, and fitted realizations were unchanged. Current
accounting is 22 rating-curve declarations, all verified.

[Verification index](README.md) | [Technical treatment](../technical-reference/analysis/rating-curve.md) | [Scientific findings](../technical-reference/review-findings.md#tr-043)
