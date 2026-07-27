<!-- technical-reference-status: in-progress -->

# Scientific Review Findings

[Technical Reference](index.md)

The active continuation and batching plan is maintained in the [Verification Finalization Plan](../verification/verification-finalization-plan.md).

This is the canonical register for disagreements among statistical theory, the pinned RMC.Numerics 2.1.4 source, RMC.BestFit behavior, tests, and earlier documentation. Corrections require explicit authorization, focused tests, and—where scientific parity is claimed—approved verification evidence.

## Summary

| ID | Finding | Severity | Review disposition | Implementation | Verification | Evidence | Updated |
|---|---|---|---|---|---|---|---|
| [TR-001](#tr-001) | Kappa Four zero-shape PDF and quantile | High | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-001---kappa-four-zero-primary-shape) · [Artifact](../../verification/data/distribution-fitting/kappa-four-zero-shape.json) | 2026-07-24 |
| [TR-002](#tr-002) | Kappa Four shape validation | Closed | Rejected non-defect | N/A | Passed - regression | [Report](../verification/distribution-fitting.md#tr-002---finite-kappa-shape-pairs) / [Artifact](../../verification/data/distribution-fitting/kappa-four-finite-shapes.json) | 2026-07-24 |
| [TR-003](#tr-003) | Nonstationary threshold chronology | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-004](#tr-004) | Point-process rate definitions | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-005](#tr-005) | Point-process fitted-model simulation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-006](#tr-006) | Mixture weights and proposal mutation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-007](#tr-007) | Zero-inflated mixed distribution | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-008](#tr-008) | Mixture EM impossible rows | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-009](#tr-009) | RMSE residual omission | High | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-009---parameter-adjusted-rmse) / [Artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json) | 2026-07-24 |
| [TR-010](#tr-010) | FittingAnalysis all-failed status | Medium | Confirmed defect | Fixed | Passed - regression | [Report](../verification/distribution-fitting.md#tr-010---all-candidate-failure-reports-overall-success) · [Artifact](../../verification/data/distribution-fitting/fitting-analysis-success-state.json) | 2026-07-24 |
| [TR-011](#tr-011) | Bayesian AIC/BIC prior-density inclusion | High | Confirmed defect - resolved | Fixed | Passed - focused regression/source audit | [Report](../verification/model-estimation.md#aic-and-bic-evaluated-at-map) | 2026-07-25 |
| [TR-012](#tr-012) | Competing-risk dependent simulation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-013](#tr-013) | Non-finite composite criteria | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-014](#tr-014) | Composite posterior draw coupling | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-015](#tr-015) | Composite correlation matrix configuration | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-016](#tr-016) | Bulletin 17C frequentist terminology | Medium | Confirmed terminology limitation | Pseudo-AIC/BIC documented; broader work pending | Pseudo-criteria source-audited | [Bulletin 17C](analysis/bulletin-17c.md#pseudo-aic-and-pseudo-bic) | 2026-07-25 |
| [TR-017](#tr-017) | Bulletin 17C bootstrap naming | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-018](#tr-018) | Bulletin 17C failed bootstrap fits | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-019](#tr-019) | Bulletin 17C bootstrap truncation | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-020](#tr-020) | Bulletin 17C Cohn diagnostics scope | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-021](#tr-021) | Bulletin 17C release evidence | Evidence | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-022](#tr-022) | NUTS versus HMC inventory | Documentation/API | Confirmed defect | Fixed | Passed - source/API inventory | [Bayesian MCMC](estimation/bayesian-mcmc.md) | 2026-07-25 |
| [TR-023](#tr-023) | MLE and MAP nuisance profiling | High | Confirmed defect - resolved | Fixed without public API changes | Passed - R `bbmle`, closed-form, and informative-prior MAP parity | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) / [Artifact](../../verification/data/model-estimation/profile-likelihood-oracle.json) | 2026-07-27 |
| [TR-024](#tr-024) | PSIS tail smoothing | High | Confirmed defect - resolved | Fixed | Passed - R `loo` aggregate, pointwise, tail, threshold, and performance parity | [Report](../verification/model-estimation.md#psis-loo-and-pareto-diagnostics) / [Artifact](../../verification/data/model-estimation/psis-loo-oracle.json) | 2026-07-26 |
| [TR-025](#tr-025) | ARWMH covariance adaptation | High | Confirmed scoped defect - resolved | Fixed | Passed - focused Numerics and BestFit regression | [Report](../verification/model-estimation.md#numerics-mcmc-verification) | 2026-07-26 |
| [TR-026](#tr-026) | GMM Hansen J statistic | High | Confirmed defect - resolved | Fixed | Passed - R `gmm::specTest` J/p-value parity | [Report](../verification/model-estimation.md#gmm-specification-covariance-and-legacy-influence-verification) / [Artifact](../../verification/data/model-estimation/gmm-specification-oracle.json) | 2026-07-26 |
| [TR-027](#tr-027) | Explicit covariance failure status | High | Confirmed defect - resolved | Fixed | Passed - deterministic failure, success, and regularization paths | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) | 2026-07-26 |
| [TR-028](#tr-028) | Joint prior-predictive sampling | High | Confirmed limitation | Documented | Passed - source/contract and coupled-prior characterization | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) / [Predictive checks](estimation/predictive-checks.md) | 2026-07-26 |
| [TR-029](#tr-029) | MCMC diagnostic claims | High | Confirmed defect - resolved | Modernized without API or serialization changes | Passed - R `posterior` 1.7.0 rank-normalized R-hat and ESS parity | [Report](../verification/model-estimation.md#rank-normalized-convergence-diagnostics-tr-029) / [Artifact](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json) | 2026-07-27 |
| [TR-030](#tr-030) | NUTS acceptance reporting | High | Confirmed defect - resolved | Fixed | Passed - diagnostics, gradient, serialization, and report regression | [Report](../verification/model-estimation.md#numerics-mcmc-verification) | 2026-07-26 |
| [TR-031](#tr-031) | Combined influence interpretation | Methodological | Confirmed defect | Fixed | Passed - analytical and R parity | [Report](../verification/model-estimation.md#fit-influence-variance-influence-and-combined-leverage) | 2026-07-25 |
| [TR-032](#tr-032) | GMM influence labeled Pareto k | High | Confirmed defect - resolved | Legacy overloads obsolete; supported GMM paths correctly labeled | Passed - compatibility, attribute, and mapping regressions | [Report](../verification/model-estimation.md#gmm-specification-covariance-and-legacy-influence-verification) | 2026-07-27 |
| [TR-033](#tr-033) | GMM objective/gradient scale | High | Rejected non-defect | No change required | Passed | [Model-estimation verification](../verification/model-estimation.md#gmm-objective-gradient-and-covariance-scaling) | 2026-07-25 |
| [TR-034](#tr-034) | Overidentified one-step GMM | Medium | Confirmed defect - resolved | Fit and covariance fixed | Passed - R `gmm` parameter/objective and fixed-weight/two-step covariance parity | [Report](../verification/model-estimation.md#gmm-specification-covariance-and-legacy-influence-verification) / [Artifact](../../verification/data/model-estimation/gmm-specification-oracle.json) | 2026-07-27 |
| [TR-035](#tr-035) | Time-series Jeffreys component type | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-036](#tr-036) | Transform fitting holdout leakage | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-037](#tr-037) | ARIMA/ARIMAX reintegration index | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-038](#tr-038) | ARIMA simulation transform/differencing | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-039](#tr-039) | ARIMAX simulation scale mixing | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-040](#tr-040) | Pointwise time-series invalid scale | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-041](#tr-041) | Differenced ARIMAX alignment | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-042](#tr-042) | Time-series/rating AIC/BIC kernel | High | Confirmed defect - resolved | Fixed | Passed - focused regression/source audit | [Report](../verification/model-estimation.md#aic-and-bic-evaluated-at-map) | 2026-07-25 |
| [TR-043](#tr-043) | Rating-curve log10 Jacobian | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-044](#tr-044) | Rating-curve zero-exponent continuity | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-045](#tr-045) | Rating-curve unused-record validation | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-046](#tr-046) | Manual transform state rebuild | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-047](#tr-047) | Bivariate AIC/BIC posterior kernel | High | Confirmed defect - resolved | Fixed | Passed - focused regression/source audit | [Report](../verification/model-estimation.md#aic-and-bic-evaluated-at-map) | 2026-07-25 |
| [TR-048](#tr-048) | Spatial missing-site marginalization | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-049](#tr-049) | Spatial likelihood decomposition | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-050](#tr-050) | Spatial cross-validation result retention | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-051](#tr-051) | Spatial held-out-site leakage | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-052](#tr-052) | Spatial held-out covariates | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-053](#tr-053) | Failed spatial folds counted as zero | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-054](#tr-054) | Ungauged conditional spatial variance | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-055](#tr-055) | Spatial AIC/BIC definition | Methodological | Confirmed defect - scoped correction complete | Corrected with caveats | Passed - source audit | [Spatial reference](spatial/spatial-extremes.md#estimation-and-output-construction) | 2026-07-25 |
| [TR-056](#tr-056) | Spatial bootstrap data wiring | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-057](#tr-057) | Spatial Godambe decomposition | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-058](#tr-058) | Regional posterior interval construction | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-059](#tr-059) | Spatial site-weight interpretation | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-060](#tr-060) | Spatial distance units | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-061](#tr-061) | Spatial dependent simulation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-062](#tr-062) | Spatial uncertainty-method dispatch | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-063](#tr-063) | Whole-series replacement leaves plotting positions stale | Medium | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-063---whole-series-replacement-refresh) / [Artifact](../../verification/data/distribution-fitting/dataframe-series-replacement.json) | 2026-07-24 |
| [TR-064](#tr-064) | DE/BFGS optimizer tolerance parity | Medium | Rejected non-defect | N/A | Passed - SciPy parity | [Report](../verification/distribution-fitting.md#tr-064---distribution-fitting-optimizer-tolerance) / [Artifact](../../verification/data/distribution-fitting/fitting-analysis-optimizer-precision.json) | 2026-07-25 |
| [TR-065](#tr-065) | GMM influence Hessian scale depends on penalty presence | High | Confirmed defect | Fixed | Passed - R `gmm` parity | [Report](../verification/model-estimation.md#gmm-calibration-against-r) / [Artifact](../../verification/data/model-estimation/gmm-influence-oracle.json) | 2026-07-25 |
<a id="tr-001"></a>
## TR-001 — Kappa Four \(\kappa=0\) Density and Quantile

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API changes in RMC.Numerics commit `3e058ebe5917817f3dde5e3b2ed6574d6bab083e`.

**Verification status.** Passed. The two exact analytical Verification methods pass independently against the corrected Numerics source.

**Report evidence.** [Distribution fitting verification](../verification/distribution-fitting.md#tr-001---kappa-four-zero-primary-shape) and [committed result artifact](../../verification/data/distribution-fitting/kappa-four-zero-shape.json).

**Evidence.** At pinned Numerics commit `828664650c9327b309ee8332e707ccca73588e93`, `KappaFour.CDF` uses

$$
F(x)=[1-h\exp(-z)]^{1/h},\qquad z=(x-\xi)/\alpha,
$$

when \(\kappa=0\) and \(h\ne0\). Algebraic inversion gives

$$
x=\xi-\alpha\log\left(\frac{1-p^h}{h}\right),
$$

but `InverseCDF` evaluates `Xi - Alpha * Log(1 - p^h / h)`. `PDF` also evaluates the generic factor `(1-kappa*z)^(1/kappa-1)` at exactly zero shape. In .NET that base-one/infinite-exponent expression evaluates as one, omitting the required \(\exp(-z)\) limit.

A compiled local probe with \((\xi,\alpha,\kappa)=(0,1,0)\) found, at \(x=1\): for \(h=0.2\), CDF `0.6824160756`, PDF `0.7366130339`, central numerical CDF derivative `0.2709847914`, and inverse-CDF at that CDF `NaN`. The existing Numerics Kappa test exercises construction at zero but not zero-shape density or CDF/quantile inversion.

The corrected source adds the exact zero-kappa density factor and fixes the inverse-CDF grouping. Four upstream regressions cover the analytical derivative, inverse/CDF round trip, support and normalization, and two-sided continuity. The complete Numerics .NET 10 gate passed 1,905 tests with no failures or skips. Both exact BestFit verification methods pass independently at absolute tolerance `1e-10`.

**Impact.** The defect affected zero-primary-shape likelihoods and quantiles. The corrected implementation restores the analytical density and inverse CDF for this branch.

**Follow-up.** Retain the two exact BestFit methods and four upstream regressions as permanent release gates.

<a id="tr-002"></a>
## TR-002 — Kappa Shape Validation Re-audit (Closed)

**Review disposition.** Rejected non-defect.

**Implementation status.** No production change required.

**Verification status.** Passed by the Numerics finite-shape/support regression on .NET 10. The complete Kappa Four class passed 12 tests with zero failures or skips. See the [distribution-fitting verification chapter](../verification/distribution-fitting.md#tr-002---finite-kappa-shape-pairs) and [result artifact](../../verification/data/distribution-fitting/kappa-four-finite-shapes.json).

The initial audit suspected that finite \((\kappa,h)\) pairs needed additional rejection. Re-reading `Minimum`, `Maximum`, CDF branches, and the Hosking formulation showed that the support changes with the two shapes and that all finite shape pairs are admissible distribution parameters when \(\alpha>0\). Existence of particular moments is a separate question. No general shape-combination validation defect was established, so this item is closed as a non-defect.

**Evidence.** The regression spans positive and negative values of both shape parameters. It verifies admissibility, monotone finite quantiles, CDF/quantile round trips at (10^{-10}), positive interior density, and the reported finite support endpoints. No production change was required; the regression is committed in RMC.Numerics commit `bc11849c762d7b87d06aa64a3f3706ecf118fc13`.

<a id="tr-003"></a>
## TR-003 — Nonstationary Threshold Chronology

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `DataFrame.FullTimeSeries` expands a perception period after removing explicit observations, places below-threshold records from the beginning of the remaining period, and places above-threshold records from its end.

**Impact.** Group totals identify counts and bounds, not event chronology. In a nonstationary likelihood, assigning those counts to different time indices changes parameter values and hence the likelihood. The deterministic ordering assumption supplies information that was not observed.

**Follow-up.** Establish an intended grouped nonstationary likelihood—such as time-index-specific threshold contributions or marginalization over compatible allocations—and add tests showing invariance to arbitrary record ordering.

<a id="tr-004"></a>
## TR-004 — Point-Process Rate Definitions

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `PointProcessModel.CalculateLambda()` divides the count of exact, uncertain, and interval records by `TotalYears`. `GeneratePOTTimeSeries()` divides exact count only by `TotalYears`. The fitted point-process likelihood does not use the public `Lambda`; its expected exceedance rate is the GEV-compatible tail intensity determined by \((\mu,\sigma,\xi,u)\).

**Impact.** Three values can be described as “rate”: the public summary, the simulator rate, and the fitted intensity. They need not agree, particularly with uncertain/interval observations.

**Follow-up.** Give the empirical summary an unambiguous name, define which records constitute Poisson events, and expose the fitted threshold intensity separately. Add mixed-data rate tests.

<a id="tr-005"></a>
## TR-005 — Point-Process Simulation Is Not Fitted-Model Predictive Simulation

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `GeneratePOTTimeSeries()` samples event count from `ExactSeries.Count / TotalYears`, not from the fitted intensity measure at the threshold. Seasonal mark distributions receive the raw six GEV parameters, while annual frequency output uses seasonal-fraction location/scale transforms. Dates are uniform over the requested span.

**Impact.** The method can reproduce the empirical event count but is not a posterior predictive realization of the likelihood used for estimation. Seasonal simulated magnitude behavior can differ from the output composition.

**Follow-up.** Specify the intended generative model; derive per-season fitted rates and conditional mark laws from the same parameterization; test simulated count means, threshold adherence, seasonal proportions, and recovery of configured tail probabilities.

<a id="tr-006"></a>
## TR-006 — Mixture Weights Are Redundant and Mutate Candidate Arrays

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** For \(K>1\), `MixtureModel` exposes all \(K\) weights with independent Uniform(0,1) priors. Numerics `Mixture.SetParameters(ref double[])` normalizes those weights and writes the normalized values back into the caller's array. `DataLogLikelihood` and `PriorLogLikelihood` pass the supplied array by reference.

**Impact.** Multiplying all raw weights by a common positive constant leaves the likelihood unchanged after normalization, creating a nonidentified radial direction. Objective evaluation also mutates optimizer/MCMC proposals, violating the normal pure-function contract and making the stated raw-weight priors hard to interpret.

**Follow-up.** Use \(K-1\) simplex coordinates or an unconstrained softmax/stick-breaking parameterization, apply a coherent Dirichlet/logistic-normal prior, never mutate input proposals, and add identification/Jacobian/round-trip tests.

<a id="tr-007"></a>
## TR-007 — Zero-Inflated Mixture Probability Functions

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** With zero inflation enabled, pinned Numerics `Mixture.PDF(x)` returns `ZeroWeight` for every \(x\le0\); `CDF(x)` starts at `ZeroWeight` even for \(x<0\); and `InverseCDF(p)` returns zero for \(p\le\)`ZeroWeight`. Simulation uses a deterministic mass at exactly zero.

**Impact.** Probability mass is treated as a Lebesgue density, the CDF is positive below the alleged point mass, and the PDF/CDF/simulator do not define the same distribution. Negative exact values are also collapsed into the zero category.

**Follow-up.** Decide whether the model is a point mass at exactly zero or a censored/nonpositive category. Implement the corresponding mixed-measure likelihood separately from continuous PDF calls and test CDF limits, jumps, quantiles, simulation frequencies, and log likelihood at/around zero.

<a id="tr-008"></a>
## TR-008 — Mixture EM Skips Impossible Rows

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** In `MixtureModel.ExpectationMaximization`, if every component log contribution for a row is non-finite, or its log-sum-exp is nonpositive, the E-step executes `continue`. The row adds nothing to the objective and retains no valid responsibilities.

**Impact.** A data row outside every component support can disappear from the EM objective instead of making the candidate likelihood impossible. The returned parameters/covariance can therefore appear finite for an invalid fit.

**Follow-up.** Return negative infinity or a failed status when any required row has zero total probability; clear its responsibilities deterministically; add support-boundary tests for every observation type.

<a id="tr-009"></a>
## TR-009 — RMSE Omits the Last \(k\) Residuals

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API changes in RMC.Numerics commit `24bf9f98139b23400bf008df413b0d97330ccfd3`. The numerator now includes every residual, and only the denominator uses the residual degrees of freedom \(n-k\). Invalid parameter counts that do not leave positive residual degrees of freedom are rejected.

**Verification status.** Passed by an analytical hand calculation and paired-permutation test at absolute tolerance \(10^{-12}\). The complete Numerics .NET 10 gate passed 1,907 tests with zero failures or skips. See the [distribution-fitting verification chapter](../verification/distribution-fitting.md#tr-009---parameter-adjusted-rmse) and [result artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json).

**Evidence.** For observed values \([0,0,0,0]\), modeled values \([1,2,3,4]\), and \(k=1\), the analytical value is \(\sqrt{30/3}=3.1622776601683795\). The baseline implementation returned \(2.160246899469287\) because it summed only the first three squared residuals. The same exact verification method passes after the correction and proves invariance to paired row permutation.

**Impact.** `FittingAnalysis` and Bayesian univariate point-result RMSE are now independent of paired input ordering. Rankings and inverse-MSE model weights use the complete residual vector.

**Follow-up.** Retain the hand-calculated, paired-permutation, and invalid-parameter-count regressions. Family-level fitting verification must continue to compare the resulting RMSE and rankings with independent distribution oracles.

<a id="tr-010"></a>
## TR-010 — FittingAnalysis Overall Success State

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API changes. Overall success now requires at least one candidate with `FitSucceeded == true`.

**Verification status.** Passed by two deterministic fast regressions and the complete .NET 10 core gate. See the [distribution-fitting verification chapter](../verification/distribution-fitting.md#tr-010---all-candidate-failure-reports-overall-success) and [result artifact](../../verification/data/distribution-fitting/fitting-analysis-success-state.json).

**Evidence.** Candidate exceptions are caught inside the parallel loop and recorded per `FittedDistribution`. If the outer loop is not canceled and raises no outer exception, `FittingAnalysis` sets `IsEstimated = true` without requiring any `FitSucceeded` result. On 24 July 2026, the isolated outlier smoke fixture reproduced exactly that state: `IsEstimated` was true and all 15 candidates had `FitSucceeded == false`.

**Impact.** An all-failed screening run now reports failure, while a partial-success run remains successful and preserves every candidate result.

**Follow-up.** Retain the zero-success and partial-success regressions as permanent state-semantic gates. Candidate counts remain directly available from `FittedDistributions` without adding API.

<a id="tr-011"></a>
## TR-011 — Bayesian AIC/BIC Included Prior Density at MAP

**Review disposition.** Confirmed defect; the scoped issue is resolved.

**Implementation status.** Fixed without public API or serialization changes. `MaximumAPosteriori` and every Bayesian analysis result builder now pass `DataLogLikelihood(MAP)` to the AIC/BIC helpers.

**Verification status.** Passed by the focused MAP AIC and BIC regressions plus a complete source-call-site audit; see [model-estimation verification](../verification/model-estimation.md#aic-and-bic-evaluated-at-map).

**Evidence.** The prior implementation passed `LogLikelihood(MAP)`, adding parameter-prior normalization constants and any Jeffreys or quantile-prior terms. The current implementation excludes all prior-density values while retaining the stored posterior mode as the evaluation point. The focused fixture has bounded uniform priors with nonzero normalization constants and proves that both criteria equal the data-likelihood formulas and differ from posterior-kernel formulas.

**Impact.** Under priors that are constant throughout the relevant bounded region, MAP coincides with the constrained MLE and the Bayesian-analysis criteria are comparable with fitting-analysis MLE criteria. Under informative, Jeffreys, quantile, or other nonconstant priors, the MAP remains prior-influenced and the fields are not conventional AIC/BIC.

**Follow-up.** Retain the focused regressions and call-site audit. With nonconstant priors, direct users to DIC, WAIC, or verified PSIS-LOO rather than AIC/BIC.
<a id="tr-012"></a>
## TR-012 — Competing-Risk Simulation Ignores Dependency

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `CompetingRisksModel.GenerateRandomValues()` calls Numerics `CompetingRisks.GenerateRandomValues()`, which samples each marginal with an independent uniform draw. Numerics has a separate `GenerateRandomValuesWithDependency()` implementation, but BestFit does not call it.

**Impact.** Simulations from perfectly dependent or correlation-matrix models do not follow the fitted/configured composite CDF.

**Follow-up.** Delegate to the dependency-aware method, validate correlation matrices, and test simulated rank dependence and composite CDFs for every dependency mode.

<a id="tr-013"></a>
## TR-013 — Non-Finite Composite Criteria

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `CompositeAnalysis.EstimateModelWeights()` filters on estimated/non-null child results but does not filter or reject non-finite criterion values before calling `AICWeights` or `RMSEWeights`.

**Impact.** One `NaN`, infinity, or zero RMSE can produce non-finite or degenerate weights for all models.

**Follow-up.** Validate every selected metric, define whether invalid children fail the analysis or receive zero weight, handle zero RMSE explicitly, and add mixed-validity tests.

<a id="tr-014"></a>
## TR-014 — Composite Posterior Draw Coupling

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** For separately fitted children, `CompositeAnalysis` takes the minimum output length and combines child distribution at raw index \(b\) with every other child's raw index \(b\). It does not establish a joint posterior, permute draws, or independently resample indices.

**Impact.** Nonlinear composite uncertainty depends on arbitrary chain ordering and possibly common pseudo-random seeds. The resulting interval encodes an undocumented cross-child coupling.

**Follow-up.** Define independence or another joint dependence assumption among child posteriors. For independent fits, use deterministic seeded independent index permutations/resampling and test invariance to child draw order.

<a id="tr-015"></a>
## TR-015 — Composite Correlation Matrix Cannot Be Supplied

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `CompositeAnalysis.Dependency` can be set to `CorrelationMatrix`, but the analysis exposes no matrix property and constructs fresh Numerics `CompetingRisks` objects without assigning `CorrelationMatrix`. Numerics dereferences that matrix when creating its multivariate normal.

**Impact.** A public enum value represents an unconfigurable path that can fail during CDF/empirical-CDF construction.

**Follow-up.** Add a validated, serialized matrix configuration or reject `CorrelationMatrix` at validation until supported. Test dimension, symmetry, unit diagonal, positive definiteness, persistence, and result construction.

<a id="tr-016"></a>
## TR-016 — Bulletin 17C Frequentist Results Use Bayesian/MCMC Terminology

**Review disposition.** Confirmed terminology limitation.

**Implementation status.** Pseudo-AIC and pseudo-BIC are now documented. The broader public API and report terminology work remains pending.

**Verification status.** The pseudo-criterion calculation passed source audit. The broader terminology finding remains planned.

**Evidence.** `Bulletin17CDistribution` implements `IGMMModel`, not `IModel`, and defines no likelihood, prior, posterior, or MCMC target. `Bulletin17CAnalysis` nevertheless exposes a `BayesianAnalysis` property and stores GMM uncertainty draws in `MCMCResults`; the GMM estimate is placed in `MAP`, ensemble averages are exposed as `PosteriorMean`, and `CredibleIntervalWidth` controls frequentist confidence limits.

**Impact.** API consumers and generated reports can incorrectly describe a sampling distribution as a posterior, a GMM estimate as a posterior mode, and confidence intervals as credible intervals. DIC, WAIC, and LOOIC are not defined for these draws. The shared AIC/BIC fields are now documented as pseudo-criteria formed from the LP3 data likelihood at the GMM solution, not as Bayesian or likelihood-maximized criteria.

**Follow-up.** Introduce estimator-neutral uncertainty/result abstractions or explicit aliases, preserve serialization compatibility, suppress inapplicable Bayesian diagnostics, retain explicit pseudo-AIC/pseudo-BIC labeling, and test terminology in public reports and UI labels.

<a id="tr-017"></a>
## TR-017 — `BiasCorrectedBootstrap` Does Not Implement BC or BCa

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** The enum XML describes bias-corrected intervals, but `GetParameterSetsFromPivotalBootstrap()` collects bootstrap estimates and covariances, fits Yeo-Johnson/log links, forms a studentized multivariate pivot with replicate Cholesky factors, adds smoothing, clips pivot components to `[-6,6]`, and maps them through the parent covariance. It does not compute the BC bias constant, BC percentile mapping, jackknife acceleration, or BCa endpoints.

**Impact.** Selecting and reporting “bias corrected bootstrap” implies a recognized algorithm that is not the one executed. Coverage expectations and literature citations can therefore be wrong.

**Follow-up.** Rename the option to a precise pivotal/studentized name or implement the documented BC/BCa method. Add algorithm-identity tests and coverage comparisons against a trusted implementation.

<a id="tr-018"></a>
## TR-018 — Failed Bulletin 17C Bootstrap Refits Become Parent-Estimate Mass

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Both bootstrap branches retry each replicate at most ten times. When ordinary bootstrap attempts are exhausted, the parent parameter vector is inserted. In the pivotal branch, the parent parameters and parent covariance replace an exhausted phase-one refit. These entries are finite and satisfy the exact-output-count publishing check.

**Impact.** Refit failures create artificial point mass at the fitted estimate and can narrow confidence limits. A method may publish the requested ensemble size even when some nominal replicates contain no successful resampled fit.

**Follow-up.** Define an explicit failure policy: resample until a separately bounded accepted count is obtained, fail the uncertainty run, or publish partial results with calibrated missingness rules. Never silently substitute the parent fit. Add tests showing interval behavior as the refit-failure rate rises.

<a id="tr-019"></a>
## TR-019 — Bulletin 17C Bootstrap Uses Asymptotic Mahalanobis Truncation

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Both bootstrap collectors reject a successful refit when its squared distance from the parent estimate, measured with the parent GMM covariance, exceeds the `1 - 1/(5B)` quantile of a chi-squared distribution with `p` degrees of freedom. Rejected candidates are retried; exhausted candidates then receive the fallback described in TR-018.

**Impact.** The empirical bootstrap distribution is deliberately truncated using an asymptotic reference distribution. This may remove genuine tail behavior precisely where flood-quantile confidence limits are most sensitive, and it couples results to the requested ensemble size.

**Follow-up.** Establish through simulation whether this rule removes only numerical degeneracy or materially changes coverage. If retained, document it as a calibrated robustification rule, report every rejection, and test sensitivity to the threshold.

<a id="tr-020"></a>
## TR-020 — Cohn-Style Bulletin 17C Diagnostics Are Unguarded LP3 Calculations

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ComputeCohnStyleConfidenceIntervals()` is public for every supported Bulletin 17C parent, but `EvaluateQuantileSafe()` always constructs `PearsonTypeIII`, interprets the supplied parameter vector in LP3 log space, and the caller always applies `Math.Pow(10, ...)` to interval endpoints. `ComputeAsymptoticQuantileVariance()` reuses the same helper. No distribution-type guard restricts these paths to `LogPearsonTypeIII`.

**Impact.** Cohn-style intervals and reported asymptotic quantile variances are nonsensical or fail for Exponential, Gamma, Log-Normal, Normal, and Pearson III analyses while appearing to be generally available.

**Follow-up.** Either reject non-LP3 use explicitly and label the diagnostics LP3-only, or generalize every quantile-space transform and covariance calculation by parent family. Add one focused test per supported parent, including unit checks that natural-space families are never exponentiated by 10.

<a id="tr-021"></a>
## TR-021 — Legacy EMA Verification Report Does Not Validate the Current GMM Path

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** The repository PDF “Comparison with EMA - Verification Report” compares EMA with the version 1 Bayesian likelihood/posterior workflow and expressly states that the comparisons do not validate either method. The current specialized `Bulletin17CDistribution`/`Bulletin17CAnalysis` GMM path is different. Current Verification source contains seven-example parity and coverage tests, but those long-running tests were not executed during this documentation program.

**Impact.** Citing the legacy report as validation of version 2 specialized Bulletin 17C behavior would be an unsupported scientific claim.

**Follow-up.** Produce a versioned verification artifact for the current GMM implementation from approved, narrowly filtered runs: official worked-example parameter parity, PeakFQ/EMA diagnostic parity, covariance, penalty behavior, censoring designs, and uncertainty coverage. Record seeds, tolerances, dependency commit, and test hashes.

<a id="tr-022"></a>
## TR-022 — BestFit Selects NUTS, Not Plain HMC

**Review disposition.** Confirmed documentation and inventory defect.

**Implementation status.** Fixed without production-code, public-API, or serialization changes.

**Verification status.** Passed by source and public-API inventory.

**Evidence.** `BayesianAnalysis.SamplerType` exposes `DEMCz`, `DEMCzs`, `ARWMH`, and `NUTS`, and `SetUpSampler()` constructs those four Numerics samplers. Numerics also contains a separate plain `HMC` class, but BestFit exposes no `SamplerType.HMC` member or configuration branch.

**Impact.** The technical reference now distinguishes BestFit-selectable samplers from the broader Numerics capability and no longer directs users to a nonexistent BestFit HMC option.

**Follow-up.** Treat the current BestFit API as authoritative. Adding plain HMC remains a separate feature decision.

<a id="tr-023"></a>
## TR-023 — MLE and MAP Reoptimize Profile Nuisance Parameters

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** `MaximumLikelihood.ProfileLikelihood()` and `ParameterConfidenceIntervals()` reoptimize all free nuisance parameters against the data likelihood. `MaximumAPosteriori.ProfileLikelihood()` and `ParameterConfidenceIntervals()` now perform the same nuisance reoptimization against the complete posterior kernel. Public signatures remain unchanged; bounded BFGS uses a deterministic bounded Nelder-Mead fallback.

**Verification status.** Passed by three exact focused methods against a committed R `bbmle` oracle, its closed-form correlated-quadratic solution, and an analytical informative-prior posterior profile.

**Evidence.** For correlation $\rho=0.8$, nuisance reoptimization gives the 90% profile interval $[-1.6448536,1.6448536]$, whereas the former fixed-nuisance coordinate slice gives $[-0.9869122,0.9869122]$. MLE and flat-prior MAP match every R and analytical profile ordinate plus the nuisance-optimized interval. The informative-prior fixture confirms that MAP reoptimizes the nuisance parameter using the full posterior target rather than either a coordinate slice or a data-only profile.

**Impact.** MLE and MAP profile curves now retain parameter correlation through nuisance reoptimization. MAP `ParameterConfidenceIntervals()` still are not Bayesian credible intervals: they apply a chi-squared cutoff to a profiled posterior kernel rather than integrating posterior mass. MCMC marginal quantiles remain the Bayesian interval product.

**Follow-up.** Preserve all three exact regressions and the committed oracle. Keep the MAP interval interpretation explicit in technical documentation and user guidance.

<a id="tr-024"></a>
## TR-024 — PSIS Tail Smoothing Did Not Preserve the Required Tail Model or Ordering

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** Fixed without public API changes and without a Numerics dependency change. The PSIS tail now uses cutoff excesses, the bounded fixed-grid generalized-Pareto fit and shrinkage used by `posterior::gpdfit` 1.7.0, monotone expected order statistics, and reference-compatible truncation. WAIC and PSIS share one transient pointwise likelihood matrix; only pointwise ELPD and Pareto-k summaries are retained for later influence reporting.

**Verification status.** Passed by six exact focused methods against R `loo` 2.10.0 and `posterior` 1.7.0.

**Evidence.** On the deterministic 40-draw fixture, BestFit matches R for LOOIC $13.4018968330430$, $p_{\mathrm{loo}}=0.342431868035246$, LOOIC standard error $1.95756902272540$, all five pointwise contributions, and Pareto $k=(0.0682,-0.0302,0.4116,0.3062,0.3209)$. Six bounded-through-degenerate tail fixtures match every smoothed log weight, Pareto $k$, and importance-sampling effective sample size; the degenerate tail returns $k=+\infty$. The 40-draw reliability limit is $0.375803649418215$, so the third observation is correctly flagged. Default WAIC plus PSIS performs exactly $S$ pointwise model evaluations, and a later influence request remains at $S$ by reusing cached $O(n)$ summaries.

**Impact.** LOOIC, $p_{\mathrm{loo}}$, LOOIC standard error, Pareto $k$, and PSIS observation influence now follow the pinned external implementation for independent retained draws. They remain approximate leave-one-out results: no exact refits or moment matching are performed, and the current tail-length calculation uses `r_eff = 1` rather than estimating MCMC relative efficiency.

**Follow-up.** Retain the pinned artifact, exact methods, single-pass call-count checks, and legacy serialization regression. Users must inspect Pareto $k$ and use an explicit sensitivity/refit strategy when the draw-count reliability limit is exceeded.<a id="tr-025"></a>
## TR-025 — ARWMH Covariance Uses the Complete Realized-State History

**Review disposition.** Confirmed scoped defect; resolved.

**Implementation status.** Fixed in Numerics without changing the continual Adaptive Metropolis schedule, public API, or serialization.

**Verification status.** Passed by two deterministic Numerics methods and one exact BestFit integration method; see [model-estimation verification](../verification/model-estimation.md#numerics-mcmc-verification).

**Evidence.** `ARWMH.ChainIteration()` now determines the retained state and performs exactly one covariance update after every transition. Accepted, rejected, and infeasible proposals therefore contribute the realized chain state. Both 12-transition rejection fixtures record 12 covariance states, regardless of whether the nominal warmup is 50 or five. Through BestFit's production setup path, all four chains record all 125 repeated retained states.

**Current behavior.** Covariance adaptation is based on the complete realized-chain history. Continued updating after the nominal warmup remains intentional and consistent with the original Haario-Saksman-Tamminen Adaptive Metropolis construction.

**Follow-up.** Retain the deterministic rejection/count tests. Distributional recovery on correlated Gaussian targets remains useful additional sampler validation, but is not required to establish the corrected state-history contract.

<a id="tr-026"></a>
## TR-026 — Hansen J Uses the Selected Efficient Weight

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** Fixed. Successful estimation preserves the unpenalized moment objective evaluated with the strategy-selected weight before covariance post-processing can replace `W`.

**Verification status.** Passed by exact focused parameter, objective, Hansen J, and p-value parity against R 4.4.3 and `gmm` 1.9.1.

**Evidence.** The committed one-parameter, two-moment R oracle gives the two-step fit $\widehat\theta=1.93548454750500$, selected-weight objective $Q=1.00755078518454$, $J=nQ=10.0755078518454$, and $p=0.00150253202968641$. BestFit matches all four quantities within the declared tolerances.

**Current behavior.** `PostProcess(computeJstat: true)` populates `JStat` and `JStatPval` for unpenalized overidentified `TwoStep` and `Iterative` fits. Generic fixed-weight `OneStep` and penalized fits leave both fields as `NaN`, because the efficient-weight Hansen chi-squared interpretation is not automatic in those cases.

**Correction.** The rank-deficient projected-residual-covariance calculation was removed. The statistic is now $n\mathbf g(\widehat{\boldsymbol\theta})^\mathsf T\mathbf W\mathbf g(\widehat{\boldsymbol\theta})$ with $\chi^2_{q-p}$ reference degrees of freedom in its verified scope. Public API and XML serialization member names are unchanged.

<a id="tr-027"></a>
## TR-027 — Covariance Failure Is Explicitly Reported

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** Fixed with additive status and `Try` APIs while retaining existing covariance method signatures. `CovarianceComputationStatus` exposes `NotComputed`, `Available`, `Regularized`, and `Failed`; MLE, MAP, and GMM expose the latest status and diagnostic text.

**Verification status.** Passed by deterministic fast tests covering singular MLE/MAP Hessians, a forced GMM covariance exception, a well-conditioned covariance, a positive-definite repair, and stable enum values.

**Evidence.** `TryGetCovarianceMatrix` for MLE/MAP and public `TryGetCovariance` for GMM return `false` with `Failed` status when covariance is unavailable. Their zero-valued out parameters are documented placeholders only. Existing throwing getters now raise `InvalidOperationException` instead of returning false zero uncertainty. MLE sandwich covariance has the same `Try` contract, and MLE/MAP influence paths obtain covariance through the validated throwing getter. A usable unmodified covariance reports `Available`; a repaired result reports `Regularized` and provides an adjustment diagnostic.

**Impact.** Numerical failure can no longer be silently presented as zero standard errors or zero influence. Callers can choose an explicit non-throwing branch or let covariance-dependent reporting fail fast.

**Follow-up.** Preserve the singular, available, and regularized regressions. Scientific interpretation must still review `Regularized` results because successful numerical repair does not resolve weak identification.

<a id="tr-028"></a>
## TR-028 — Prior-Predictive Sampling Is Marginal, Not a General Joint-Prior Sampler

**Review disposition.** Confirmed limitation.

**Implementation status.** The current behavior is documented without a production algorithm change.

**Verification status.** Passed by source/model-contract audit and one exact focused coupled-prior characterization.

**Evidence.** `PriorPredictiveCheck.SampleFromPriors()` independently samples each `ModelParameter.PriorDistribution`, clamps values to bounds, and rejects sets whose full prior log likelihood is non-finite. It does not sample or reweight coupled quantile priors, Jeffreys factors, transformation Jacobians, spatial terms, or other non-marginal contributions implemented only in `IModel.PriorLogLikelihood`. `ParameterSet.Fitness` stores the negative joint prior log likelihood for each accepted draw; it is not a likelihood or posterior score. A fixed-seed 20,000-draw fixture with independent Uniform$(-1,1)$ marginals and a narrow $y\mid x$ coupling confirms that the draws remain marginally independent even though fitness records the coupled density.

**Impact.** The resulting ensemble is a valid direct prior sample only when the model prior factorizes into the sampled parameter marginals subject to the rejection rule. It is not a general joint-prior sampler for models with additional coupled prior structure.

**Follow-up.** Retain the documented scope. A general solution requires a model-level joint-prior sampler or a separately approved validated weighting/MCMC method, genuine truncation, failure reporting, and independent/coupled-prior verification.

<a id="tr-029"></a>
## TR-029 — Rank-Normalized R-hat and Conservative Bulk/Tail ESS

**Review disposition.** Confirmed defect - resolved.

**Implementation status.** Fixed without new public methods, result fields, or serialized fields. `GelmanRubin(...)` stores the maximum of rank-normalized split and folded rank-normalized split R-hat in the existing `Rhat` field. The existing scalar `ESS` stores the minimum of rank-normalized bulk ESS and pooled 0.05/0.95 quantile ESS. The concise `R-hat` and `ESS` report labels remain unchanged, while the readiness threshold is now 1.01.

**Verification status.** Passed against R `posterior` 1.7.0 and by focused Numerics/BestFit tests. Public signatures and serialization are unchanged.

**Evidence.** The committed deterministic oracle covers IID chains, autocorrelation, shifted means, scale disagreement, sticky tails, ties, constants, warmup removal, and chain permutation. C# uses pooled midranks, Blom inverse-normal scores, split chains, and Geyer's multi-chain initial-positive and initial-monotone paired autocorrelation sequence with zero-padded FFT autocovariances. Invalid, constant, or insufficient input returns `NaN`; single-chain R-hat remains `NaN`; ESS trims unequal chains to their common usable length. A five-run .NET 10 Release benchmark of the same deterministic four-chain, ten-parameter completion fixture retained exactly 439,645 target evaluations and changed median completion time from 1,232.328 ms to 1,243.330 ms, a 0.893% increase.

**Impact.** The existing compact report now detects within-chain drift, between-chain location or scale disagreement, and weak 5%/95% tail mixing more robustly. The 51-lag original-scale averaged ACF remains unchanged for plots, and diagnostics add no model-target evaluations.

**Follow-up.** Retain the R-oracle fixtures, public-API and serialization contract, focused report-threshold tests, and FFT-based $O(PMN\log N)$ implementation. R-hat and ESS remain screening diagnostics rather than proof of convergence or model adequacy.

<a id="tr-030"></a>
## TR-030 — NUTS Reports Sampler-Specific Hamiltonian Diagnostics

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** Fixed with additive Numerics and `MCMCResults` properties plus sampler-specific BestFit reporting. Older serialized results remain readable; absent fields are treated as legacy diagnostics-unavailable state.

**Verification status.** Passed by focused Numerics acceptance/diagnostic, E-BFMI, analytic-gradient-routing, and serialization tests plus exact BestFit report and posterior-gradient methods; see [model-estimation verification](../verification/model-estimation.md#numerics-mcmc-verification).

**Evidence.** NUTS now overrides the generic acceptance calculation with the mean post-warmup Hamiltonian acceptance statistic and streams per-chain divergence counts, maximum-depth hits, mean tree depth, mean leapfrog steps, final step size, and E-BFMI without additional target evaluations or retained-draw storage. `MCMCResults` carries those arrays through JSON serialization. BestFit reports them in a compact NUTS section and suppresses the former generic Metropolis interpretation for legacy results.

**Gradient regression.** A prior Numerics audit had already corrected the step-size initialization heuristic so it honors a caller-supplied `GradientFunction` instead of unconditionally recomputing finite differences. The permanent Numerics test proves that route. BestFit does not currently supply an analytic gradient; its coupled-prior verification proves that the default bounded finite-difference function differentiates the complete `Model.LogLikelihood` posterior target.

**Follow-up.** Retain the focused diagnostic, gradient, serialization, and report regressions. Sampling remains in the bounded API parameterization, so posterior-boundary behavior and model-specific recovery still require review when NUTS is selected.

<a id="tr-031"></a>
## TR-031 — Combined Influence Was Presented as Hat-Matrix Information

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API or serialization changes.

**Verification status.** Passed by focused analytical Log10-Normal tests and the R `gmm` oracle associated with TR-065.

**Report evidence.** [Model-estimation verification](../verification/model-estimation.md#fit-influence-variance-influence-and-combined-leverage).

**Evidence.** `LeverageDiagnostics` intentionally combines a Cook score quadratic with variance influence. Observation variance uses a local curvature trace, while prior and penalty variance uses a finite log generalized-variance change. Their sum is a useful ranking index but is not a hat-matrix diagonal or conserved information decomposition. The former sum-to-$p$ warning and “% of Total Information” plot labels asserted an identity that these definitions do not possess.

The displaced-prior Log10-Normal calculation also tested the observation trace approximation directly. Replacing its diagonal observation curvature with the full analytical Hessian changed every variance-influence value by less than `0.003` and preserved the three leading observations. This does not justify a universal hat interpretation; it confirms that the current local approximation is materially adequate for the scoped fixture.

**Correction.** The unsupported sum-to-$p$ warning was removed. MAP and GMM plots now say “Combined Leverage (% of Total Influence),” and summaries say “total combined influence.” XML documentation identifies the observation trace and prior/penalty generalized-variance definitions separately. The DTO and public members remain source- and serialization-compatible.

**Impact.** The combined plot can be used to rank fit and variance effects without implying a classical leverage identity. No universal threshold or cross-estimator magnitude comparison is claimed.

**Follow-up.** Retain the regime, sample-size, and full-curvature tests. Extend model-family-specific calibration before adopting numerical intervention thresholds.
<a id="tr-032"></a>
## TR-032 — Legacy GMM PSIS-Shaped Influence Overloads Are Obsolete

**Review disposition.** Confirmed defect; resolved for supported API use.

**Implementation status.** Both legacy `GeneralizedMethodOfMoments.GetInfluenceDiagnostics()` overloads are marked `[Obsolete]` with a non-error compatibility warning. Their signatures and behavior remain intact for source, binary, and serialization compatibility. The warning directs callers to `GetLeverageDiagnostics()` for labeled GMM diagnostics or `GetCooksDistance()` for raw Cook-like values.

**Verification status.** Passed by a fast reflection regression and an exact focused compatibility/mapping method.

**Evidence.** The compatibility test confirms both overloads are obsolete without being compile errors. Reflection invocation preserves the legacy value-for-value mapping from `GetCooksDistance()` into `ObservationInfluence.ParetoK`, while demonstrating why its PSIS categories and summary are not supported GMM interpretations. The main GMM UI already consumes `GetLeverageDiagnostics()` and remains on the correctly labeled path.

**Impact.** New and maintained callers receive an explicit compiler warning before entering the semantically invalid PSIS-shaped path, while existing compiled clients remain functional. Supported GMM diagnostics no longer direct users through Pareto-k labels or thresholds.

**Follow-up.** Retain the compatibility regressions. Remove the legacy overloads only in a future major version with an explicit migration notice.

<a id="tr-033"></a>
## TR-033 — The Unpenalized GMM Objective and Gradient Have Different Scale

**Review disposition.** Rejected non-defect.

**Implementation status.** No production change required.

**Verification status.** Passed by focused analytical verification.

**Evidence.** Independent central differences confirm that the unpenalized estimating-equation gradient is one half of the derivative of the conventional reported objective $\mathbf g^\mathsf T\mathbf W\mathbf g$, while the penalized objective $\tfrac12\mathbf g^\mathsf T\mathbf W\mathbf g+P$ and supplied gradient agree exactly. Multiplication by the positive constant preserves the unpenalized stationary point. The reported GMM covariance is constructed from $\mathbf D^\mathsf T\mathbf W\mathbf D$ and the sandwich meat, not from the scalar objective Hessian. A deterministic Log10-Normal experiment further verifies that the current half-quadratic parameter penalty, gradient, penalty Hessian, and default Bulletin 17C covariance path reproduce both the inverse-variance posterior mean and posterior variance for wide-centered, narrow-centered, and narrow-shifted Gaussian information. The B17C GMM scale remains the intended unbiased $n-1$ moment estimate, while MAP retains its $n$-denominator MLE scale.

**Impact.** No point-estimate or covariance defect is present in the verified Log10-Normal path. The unpenalized constant factor is an optimizer-direction convention and is not a covariance multiplier. The penalized path, where relative data/prior scaling matters, is internally consistent and produces the required Gaussian inverse-precision inference.

**Follow-up.** Retain the two independent gradient-scale tests and the MAP/GMM inverse-variance test as permanent verification evidence. Any future change to the objective, gradient, penalty definition, or covariance bread/meat must preserve their joint equations.

<a id="tr-034"></a>
## TR-034 — Overidentified One-Step GMM Uses Fixed Weighting for Fit and Covariance

**Review disposition.** Confirmed defect; resolved.

**Implementation status.** The overidentified/`OneStep` guards were removed from `Estimate()` and `IsValid(out List<string>)`. Covariance now retains the configured fixed weight for `OneStep` bread and meat while recomputing $\mathbf S$ at the solution. `TwoStep` and `Iterative` covariance use final fitted-parameter $\mathbf S^{-1}$. Public API and serialization are unchanged.

**Verification status.** Four exact focused tests pass R 4.4.3 and `gmm` 1.9.1 parameter/objective, selected-weight Hansen J, fixed-weight IID sandwich covariance, and efficient two-step covariance parity.

**Evidence.** For the one-parameter, two-moment fixture with identity weight, R obtains $\widehat\theta=2.28992700729929$, $Q=0.317956204379562$, and fixed-weight sandwich variance $0.145652392138090$; BestFit matches all three. For two-step GMM, R and BestFit agree on variance $0.132600447299456$. The generator independently reconstructs both centered IID sandwiches and distinguishes the second-step objective weight from the final efficient covariance weight.

**Current behavior.** `OneStep` fitting and covariance use the requested fixed weight for just-identified and overidentified systems. Generic fixed-weight fits do not populate Hansen J. `TwoStep` and `Iterative` refresh the efficient covariance weight at the final parameters.

**Correction.** Identification guards and covariance weight selection were corrected surgically. Enum values, constructor signatures, default identity weighting, underidentification handling, public API, and XML serialization signatures remain unchanged.

<a id="tr-035"></a>
## TR-035 — Time-Series Jeffreys Terms Are Misclassified in Pointwise Prior Output

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** AR, MA, and ARIMA pointwise-prior methods type their Jeffreys scale contribution as `ParameterPrior` rather than `JeffreysScalePrior`. The scalar sum can remain correct, but downstream grouping relies on the component type.

**Impact.** Prior-influence summaries can attribute scale-invariant prior information to an ordinary marginal prior.

**Follow-up.** Emit the correct component type, test scalar-versus-pointwise decomposition, and audit transformed and ARIMAX variants.

<a id="tr-036"></a>
## TR-036 — Time-Series Transform Fitting Leaks Holdout Data

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** AR, MA, ARIMA, and ARIMAX call `BoxCox.FitLambda(TimeSeries.ValuesToList(), ...)` or the Yeo-Johnson equivalent on the entire response series. `TrainingTimeSteps` is applied only afterward. ARIMAX also transforms the entire response before selecting the training prefix.

**Impact.** Transformation choice uses validation/holdout observations, so reported out-of-sample performance is not genuinely out of sample. Forecast-era additions can change calibration without changing the training window.

**Follow-up.** Fit every preprocessing parameter on the training subset only, freeze it for validation/forecasting, serialize it, and test invariance to changes beyond `TrainingTimeSteps`.

<a id="tr-037"></a>
## TR-037 — ARIMA and ARIMAX Reintegration Is Off by One

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** For first differences, `Difference` stores `d[0]=x[1]-x[0]`. `Predict()` allocates `TrainingTimeSteps + forecastSteps` differenced entries, then overwrites `integrated[0]` with `x[0]` and evaluates `integrated[i]=anchor[i-1]+integrated[i]`. Thus `d[0]` is discarded and output index 1 uses `d[1]`; the differenced vector is also `d` entries too long for an output of the requested undifferenced length. ARIMA and ARIMAX share this integration pattern.

**Impact.** Fitted values and forecasts for `d>0` are time-shifted and can be numerically biased even though array lengths and uncertainty-band widths look plausible.

**Follow-up.** Define explicit raw/differenced index maps, predict `T-d+h` differences, reconstruct from the required `d` initial conditions, and add hand-computable linear/quadratic sequence tests for `d=1,2`.

<a id="tr-038"></a>
## TR-038 — ARIMA Simulation Ignores Differencing and Transformations

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ARIMA.GenerateRandomValues()` simulates a stationary ARMA recursion from intercept, AR, MA, and scale, then returns it directly. It does not apply `DOrder`, inverse Box-Cox/Yeo-Johnson transformation, or the configured initial conditions.

**Impact.** Prior/posterior predictive checks and any `ISimulatable<double[]>` consumer generate from a different model whenever `d>0` or `TransformType != None`.

**Follow-up.** Simulate innovations on the fitted transformed/differenced scale, integrate with an explicit initial-condition policy, inverse-transform last, and verify distributional properties for every transform and `d`.

<a id="tr-039"></a>
## TR-039 — ARIMAX Simulation Mixes Original and Transformed Scales

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ARIMAX.GenerateRandomValues()` constructs its deterministic mean and ARMA recursion on the fitted transformed/differenced parameter scale. For Box-Cox or Yeo-Johnson it then calls `Transform(deterministic)` again, adds noise, and immediately inverse-transforms each value. Subsequent AR/MA residual recursion combines those original-scale values with transformed-scale means, and differencing is reversed only after the inverse transform.

**Impact.** Generated data do not follow the fitted ARIMAX model under a transform and/or differencing, invalidating predictive checks and synthetic uncertainty studies.

**Follow-up.** Keep the complete regression/ARMA recursion on one model scale, integrate differences on that scale, inverse-transform once at the end, and add deterministic algebra plus Monte Carlo moment tests.

<a id="tr-040"></a>
## TR-040 — Pointwise Time-Series Likelihoods Can Throw at Invalid Scale

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Scalar AR, MA, ARIMA, and ARIMAX data likelihoods reject `sigma<=0` before constructing a Numerics `Normal`. Their pointwise likelihood and component methods construct `Normal(0,sigma)` without the same guard.

**Impact.** A parameter set that correctly returns negative infinity from the scalar likelihood can throw from WAIC/LOO or influence diagnostics, violating scalar/pointwise decomposition behavior.

**Follow-up.** Apply identical validation to scalar and pointwise paths and test zero, negative, NaN, and positive scales.

<a id="tr-041"></a>
## TR-041 — Differenced ARIMAX Raw-Time Alignment Is Inconsistent

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** With `DiffOrderD=d`, differenced response index `t` corresponds to raw response index `t+d`. Residual regression nevertheless uses covariate index `t`. The transform Jacobian ends at raw index `TrainingTimeSteps-1`, although a training prefix of that many differenced values extends through raw index `TrainingTimeSteps+d-1`.

**Impact.** Exogenous effects are shifted relative to the response and the transformed likelihood omits/misassigns raw observations when `d>0`.

**Follow-up.** Specify whether covariates enter levels or differences, align by `DateTime` rather than positional index, and derive/test the exact Jacobian observation set.

<a id="tr-042"></a>
## TR-042 — Time-Series and Rating-Curve AIC/BIC Included Prior Density

**Review disposition.** Confirmed defect; the scoped issue is resolved.

**Implementation status.** Fixed without public API or serialization changes. AR, MA, ARIMA, ARIMAX, and rating-curve analyses now evaluate their data likelihoods at `Results.MAP.Values`.

**Verification status.** Passed by the shared focused MAP criterion regression and the source-call-site audit; see [model-estimation verification](../verification/model-estimation.md#aic-and-bic-evaluated-at-map).

**Evidence.** Prior-density terms are no longer passed to `GoodnessOfFit.AIC/BIC`. Each analysis retains its existing parameter count and observation/training sample-size convention.

**Impact.** Prior normalization constants no longer shift the reported criteria. Flat-prior fits can be compared with their constrained-MLE counterparts; nonconstant priors can move MAP away from MLE, so those values require the documented Bayesian caveat.

**Follow-up.** Resolve the separate time-series indexing and rating-curve likelihood-measure findings before making cross-model criterion claims. Use DIC, WAIC, or verified PSIS-LOO with informative priors.

<a id="tr-043"></a>
## TR-043 — Rating-Curve Likelihood Omits the Log10 Change-of-Variables Term

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** The rating curve assumes `Z=log10(Q)` is Normal and sums `Normal.LogPDF(log10(q)-log10(qhat))`. As a density for observed discharge `Q`, the likelihood also requires `-log(q ln 10)` per observation. The code omits this Jacobian while transformed time-series likelihoods include their corresponding Jacobians.

**Impact.** Parameter estimates are unchanged because the omitted term is data-only, but absolute log likelihood, predictive density, AIC/BIC/WAIC/LOO values, and cross-model comparisons are on the wrong measure.

**Follow-up.** Decide and label the observation measure explicitly. If outputs claim a discharge-space density, include the Jacobian in scalar and pointwise paths and verify against a base-10 lognormal density.

<a id="tr-044"></a>
## TR-044 — Rating-Curve Continuity Claim Fails at the Allowed Zero Exponent

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Each added control contributes zero at `h=h_k` because activation requires `h>h_k`. Its exponent prior and bound allow `beta_k=0`; immediately above the breakpoint, `(h-h_k)^0=1`, so discharge jumps by `alpha_k` rather than approaching zero.

**Impact.** The implementation and documentation claim automatic continuity over a parameter space that includes discontinuous boundary models.

**Follow-up.** Require strictly positive exponents with a defensible lower bound or define the boundary limit explicitly, then test continuity from both sides.

<a id="tr-045"></a>
## TR-045 — Rating-Curve Validation Rejects Unused Discharge Records

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** The likelihood uses only the date-inner-joined stage/discharge pairs, but `Validate()` rejects the model if any value in the entire discharge series is nonpositive, including dates with no matching stage that never enter the likelihood.

**Impact.** An irrelevant unmatched record can prevent an otherwise valid fit, contradicting the stated alignment contract.

**Follow-up.** Apply likelihood-domain validation to aligned pairs, report dropped invalid/unmatched records separately, and test both matched and unmatched cases.

<a id="tr-046"></a>
## TR-046 — Manual Transform Parameters Do Not Rebuild Model Data

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SetTransformParameters(lambda1,lambda2)` in AR, MA, ARIMA, and ARIMAX only assigns backing fields. It does not re-transform the training series, recompute differences or Jacobians, reset parameters, or clear analysis results. The stored `lambda2` offset is not used by the shown transform calls.

**Impact.** Calling the public method can leave the reported transform parameters inconsistent with the data and likelihood actually evaluated; `lambda2` suggests an unsupported offset capability.

**Follow-up.** Make transform configuration atomic and observable, either remove the unused offset or implement it consistently, rebuild dependent state, and test serialization and likelihood changes.

<a id="tr-047"></a>
## TR-047 — Bivariate AIC and BIC Included the Copula Prior

**Review disposition.** Confirmed defect; the scoped issue is resolved.

**Implementation status.** Fixed without public API or serialization changes. `BivariateAnalysis.UpdatePointEstimateResultsAsync` now evaluates `BivariateDistribution.DataLogLikelihood` at the stored MAP.

**Verification status.** Passed by the shared focused MAP criterion regression and the source-call-site audit; see [model-estimation verification](../verification/model-estimation.md#aic-and-bic-evaluated-at-map).

**Evidence.** Copula-prior density is excluded from AIC/BIC. The parameter penalty remains the number of fitted copula parameters, and BIC continues to use the matched-pair count.

**Impact.** With a flat copula prior, MAP coincides with the constrained copula MLE and the criteria have their usual likelihood interpretation conditional on the fixed marginal fits. Informative copula priors invalidate that interpretation, and comparisons remain conditional on identical marginals and paired events.

**Follow-up.** With informative priors, use DIC, WAIC, or verified PSIS-LOO. Do not compare bivariate criteria across different marginal fits, event pairings, or likelihood conventions.

<a id="tr-048"></a>
## TR-048 — Spatial Copula Does Not Marginalize Missing Sites

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** In both scalar and pointwise `SpatialGEV` likelihoods, a missing site is assigned latent Gaussian score `z[j] = 0.0`, after which the full-dimensional Gaussian-copula density is evaluated. The correct observed-data likelihood uses the correlation submatrix for the sites observed in that row.

**Impact.** Missing observations are treated as if their latent normal score were exactly zero, altering the likelihood for every observed site correlated with them and potentially biasing dependence and GEV regression estimates.

**Follow-up.** Evaluate each missingness pattern with its observed-site correlation submatrix, cache factorizations by pattern, and add complete-data and patterned-missingness parity tests.

<a id="tr-049"></a>
## TR-049 — Spatial Likelihood Decomposition Is Internally Inconsistent

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SpatialGEV.DataLogLikelihood` includes Gaussian-process spatial-error densities. `PointwiseDataLogLikelihoodComponents` omits them, while `PointwisePriorLogLikelihood` emits them even though the inherited scalar `PriorLogLikelihood` does not. Source remarks acknowledge that the scalar/pointwise sum identities are broken and that WAIC/LOO omit the spatial-error process.

**Impact.** Pointwise diagnostics do not describe the same posterior kernel used for fitting; consumers can also double-count spatial errors by combining scalar data likelihood with pointwise prior components.

**Follow-up.** Choose and enforce one coherent hierarchical decomposition, define the predictive unit for spatial model comparison, and test all scalar/pointwise sum identities.

<a id="tr-050"></a>
## TR-050 — Spatial Leave-One-Site-Out Results Are Cleared Before Return

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `RunCrossValidationAsync` populates `CrossValidationResults`, restores site weights, and then calls `RunAsync` to refit the full model. `RunAsync` begins with `ClearResults`, which sets `CrossValidationResults = null`; the method therefore raises its final property-change notification after discarding the result it just computed.

**Impact.** A successful cross-validation run does not leave the documented result available to callers.

**Follow-up.** Preserve the completed cross-validation DTO across the restoration refit, or refit through a path that clears only fitting outputs; add an end-to-end unit test for result retention.

<a id="tr-051"></a>
## TR-051 — Spatial Leave-One-Site-Out Does Not Fully Exclude the Site

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Cross-validation sets only the held-out site's marginal `SiteWeight` to zero. With copula dependence enabled, the held-out observations remain in the full Gaussian-copula vector and its copula log density is unweighted. Enabled spatial-error vectors also retain the held-out site's latent error and Gaussian-process contribution.

**Impact.** The purported leave-one-site-out fit leaks held-out information and can materially overstate ungauged-site predictive performance.

**Follow-up.** Construct an actual training submodel without the held-out column, coordinates, covariates, copula dimension, or latent spatial error; verify against a manually reduced model.

<a id="tr-052"></a>
## TR-052 — Spatial Cross-Validation Omits Held-Out Covariates

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `RunCrossValidationAsync` calls `PredictAtUngaugedLocation(coords, null, probs)`. `GeneralLinearFunction.PredictWithCovariates` requires a covariate vector whenever the fitted trend has covariates.

**Impact.** Cross-validation fails for the principal regional-regression use case or cannot evaluate the trend model actually fitted.

**Follow-up.** Extract the held-out row from each trend model's covariate matrix, verify consistent covariate definitions across location, scale, and shape, and pass it to prediction.

<a id="tr-053"></a>
## TR-053 — Failed Spatial Cross-Validation Folds Are Counted as Zero Error

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Site error arrays are initialized to zero. A failed Bayesian fit, missing Bayesian analysis, or site with no finite observations executes `continue` without marking the fold invalid. Overall MAE, RMSE, and bias then average all array entries.

**Impact.** Failed or unevaluable folds appear to be perfect predictions and bias aggregate validation metrics downward.

**Follow-up.** Store fold status and `NaN` metrics for failed folds, aggregate only successful folds, report the success count, and fail the analysis when too few folds are valid.

<a id="tr-054"></a>
## TR-054 — Analysis-Level Ungauged Prediction Uses IDW and Omits Conditional Spatial Variance

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SpatialGEV.PredictAtUngauged` uses simple kriging and returns kriging variances. `SpatialGEVAnalysis.PredictAtUngaugedLocation`, which provides posterior summaries, instead interpolates each latent error with inverse-distance weights proportional to `1/d` and never samples or propagates the conditional spatial-error variance.

**Impact.** The main posterior prediction API disagrees with the model-level predictor and produces intervals that omit an important source of ungauged-site uncertainty.

**Follow-up.** Use the model's conditional Gaussian-process predictor for each posterior draw and sample the conditional residual, with an explicit option if deterministic conditional means are desired.

<a id="tr-055"></a>
## TR-055 — Spatial AIC and BIC Required a Defensible Likelihood and Sample Unit

**Review disposition.** Confirmed defect; the scoped correction is complete with remaining methodological limitations.

**Implementation status.** Corrected as far as the current spatial likelihood contract permits, without public API or serialization changes. The analysis now evaluates `SpatialGEV.DataLogLikelihood` at MAP and uses the number of nonempty row/year blocks for BIC instead of `Sites * Observations`.

**Verification status.** Passed by source audit for the likelihood call and sample-count implementation. The remaining scientific limitations are explicitly documented in the [spatial reference](spatial/spatial-extremes.md#estimation-and-output-construction).

**Evidence.** Independent parameter-prior densities and fully missing rows no longer affect the criterion calculation, and contemporaneously dependent site cells are no longer counted as independent BIC replicates. However, `SpatialGEV.DataLogLikelihood` currently contains Gaussian-process spatial-error densities; the copula does not correctly marginalize missing sites; and weighted/dependent spatial likelihoods do not automatically meet ordinary AIC/BIC regularity conditions.

**Impact.** The corrected fields are materially better defined but remain qualified diagnostics, especially with nonconstant priors or latent spatial errors. They must not be presented as generally conventional AIC/BIC for hierarchical spatial comparison.

**Follow-up.** Resolve TR-048 and TR-049, then verify posterior predictive comparison at the row/year unit. Prefer WAIC or verified PSIS-LOO once the scalar and pointwise spatial likelihoods describe the same fitted target.

<a id="tr-056"></a>
## TR-056 — Spatial Bootstrap Does Not Fit the Resampled Data

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `RunSpatialBootstrapAsync` constructs a `bootData` matrix from resampled site blocks, but then creates `bootModel` by cloning `SpatialGEV`; the clone retains the original `AtSiteData`, and `bootData` is never passed to any model. Consequently each replicate refits the original data with a short stochastic MCMC run. If a run returns `IsEstimated == false` without throwing, its zero-initialized result entries are also treated as valid bootstrap values.

**Impact.** The reported "spatial bootstrap" intervals are not bootstrap intervals and can be dominated by MCMC variability or artificial zeros.

**Follow-up.** Construct each replicate model from the resampled data, coordinates, and matching covariate rows; represent failed replicates as `NaN`, enforce a minimum success rate, and verify on a deterministic small network.

<a id="tr-057"></a>
## TR-057 — Godambe Covariance Mixes Incompatible Likelihood Decompositions

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ComputeGodambeCovariance` forms its Hessian from scalar `SpatialGEV.DataLogLikelihood`, which includes spatial-error Gaussian-process densities, but forms its score outer products from `PointwiseDataLogLikelihood`, which omits those densities. If the Hessian is singular, the method returns the variability matrix `J` itself as though it were a covariance matrix.

**Impact.** The sandwich factors do not derive from the same estimating equations, and the singular fallback has no Godambe-covariance interpretation.

**Follow-up.** Define clusterwise estimating equations whose sum equals the scalar objective, use the matching sensitivity and variability matrices, and report numerical failure instead of substituting `J`.

<a id="tr-058"></a>
## TR-058 — Regional Spatial Bounds Average Sitewise Endpoints

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `CreateUncertaintyAnalysisResultsAsync` computes the regional curve by averaging each site's posterior mean, lower endpoint, and upper endpoint separately. It does not compute the regional mean quantile for each joint posterior draw and then take quantiles of that derived sample.

**Impact.** The displayed lower and upper regional curves are descriptive averages of marginal interval endpoints, not a credible interval for the regional-average quantile; cross-site posterior dependence is discarded.

**Follow-up.** Compute the regional statistic within each posterior draw and summarize its empirical posterior distribution. If endpoint averages remain useful, label them explicitly as descriptive envelopes.

<a id="tr-059"></a>
## TR-059 — Spatial Site Weights Are Not an Effective-Sample-Size or Pairwise Composite Likelihood

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ComputeEffectiveSampleSizeWeights` first computes \(w_j^\star=[1+(S-1)\bar\rho_j]^{-1}\), but then rescales the weights so that \(\sum_j w_j=S\). The likelihood uses those weights only on marginal GEV log densities; it retains one unweighted full-dimensional Gaussian-copula density per row. `ConfigureForProperCoverage` nevertheless describes this option as composite-likelihood weighting.

**Impact.** The option changes relative site influence but does not reduce the total marginal log-likelihood contribution to the separately reported effective sample size. It is not a pairwise composite likelihood, and standard composite-likelihood uncertainty corrections do not follow from it.

**Follow-up.** Decide whether the intended method is effective-information scaling, a formally specified weighted likelihood, or a pairwise composite likelihood. Implement and name that method explicitly, derive its posterior or sandwich adjustment, and validate interval coverage by simulation.

<a id="tr-060"></a>
## TR-060 — Spatial Distance Is Euclidean Despite Latitude/Longitude Being Advertised

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Spatial constructors and prediction methods accept coordinates described as `(X,Y) or (Lat,Lon)`. Both `GaussianCopula` and `SpatialRegressionErrors` call pinned Numerics `Tools.Distance`, which is \(\sqrt{(x_2-x_1)^2+(y_2-y_1)^2}\). Correlation-range priors are hard-coded to \((\epsilon,500)\), with source comments interpreting 500 as kilometres.

**Impact.** Supplying unprojected longitude/latitude produces distances in degrees, ignores Earth geometry and longitude scaling, and makes the range prior's units inconsistent. Fitted spatial dependence and ungauged interpolation can therefore be materially wrong.

**Follow-up.** Require projected coordinates in a documented linear unit or add an explicit geodesic distance option. Carry the coordinate unit into range-parameter metadata and choose bounds from the observed network extent.

<a id="tr-061"></a>
## TR-061 — Spatial Simulation Ignores Enabled Dependence

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SpatialGEV.GenerateRandomValues` samples each site's GEV independently and groups the result by site. It does not consult `UseCopulaDependence` or `SpatialDependence`. Its remarks refer users to “copula-based simulation methods,” but no correlated simulation method exists in the spatial model namespace.

**Impact.** Simulations from a fitted dependent model do not reproduce intersite dependence and are unsuitable for regional risk aggregation, simultaneous-event probabilities, or posterior predictive checks of spatial structure.

**Follow-up.** Generate correlated standard-normal vectors from the fitted correlation matrix, transform them through \(\Phi\), and apply each site's inverse GEV CDF. Define output ordering clearly and add seeded tests for marginal and cross-site behavior.

<a id="tr-062"></a>
## TR-062 — Spatial Uncertainty-Method Selection Does Not Control Result Construction

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SpatialGEVAnalysis.UncertaintyMethod` exposes `BayesianPosterior`, `BayesianInflated`, `GodambeSandwich`, and `SpatialBootstrap`, but no production branch reads the property after assignment. `RunAsync` always constructs Bayesian posterior site summaries. Variance inflation, Godambe covariance, and bootstrap require independent method calls and do not replace the normal result-construction path automatically.

**Impact.** Selecting an advertised uncertainty method can leave outputs unchanged, so callers may report a method that was not applied.

**Follow-up.** Either dispatch the selected method through `RunAsync` with method-specific validation and result metadata, or replace the enum property with explicit operations whose outputs cannot be confused with the Bayesian results.

<a id="tr-063"></a>
## TR-063 - Whole-Series Replacement Leaves Plotting Positions Stale

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API or serialization-schema changes.

**Verification status.** Passed by exact analytical verification at absolute tolerance `1e-12` and by focused serialization and invalid-input regressions.

**Report evidence.** [Distribution fitting verification](../verification/distribution-fitting.md#tr-063---whole-series-replacement-refresh) and [evidence artifact](../../verification/data/distribution-fitting/dataframe-series-replacement.json).

**Evidence.** Assigning a complete `ExactSeries` detached and attached collection handlers and incremented `PlottingPositionVersion`, but did not calculate plotting positions. Newly constructed observations therefore retained `PlottingPosition == 0`. `FittingAnalysis` complements the stored value and evaluated candidate quantiles at probability one, so finite MLE fits acquired infinite RMSE values and were classified as failed. The deterministic three-family fixture reproduced zero successful candidates before the correction.

**Correction.** A valid programmatic whole-series replacement now refreshes plotting positions after the new collection and item handlers are attached. Exact-series replacement also refreshes `Lambda`. Invalid transient frames retain the previous non-throwing setter behavior and defer derived-state calculation. XML construction suppresses all four intermediate replacement refreshes, preserves serialized plotting positions exactly, and reprocesses effective threshold counts once after loading.

**Regression control.** A custom-position XML round trip proves deserialization does not recalculate the serialized plotting positions. A special-value fixture proves that replacement with `NaN` or infinity still does not throw. The analytical verification independently reproduces all 39 Weibull nonexceedance probabilities as \(i/(n+1)\). The complete Debug regression gate passed Core 3,032, UI 564, and App 428 tests with zero failures or skips; the public API baseline and enforced XML-documentation build also passed.

**Impact.** Programmatic replacement now leaves a valid data frame immediately ready for distribution fitting without an extra manual `CalculatePlottingPositions()` call. Persisted projects retain their stored plotting positions and avoid redundant deserialization work.

**Follow-up.** Retain the analytical and serialization regressions as permanent release gates.

<a id="tr-064"></a>
## TR-064 - Distribution Fitting Optimizer Tolerance

**Review disposition.** Rejected non-defect.

**Implementation status.** No production change required.

**Verification status.** Passed both exact SciPy comparison methods. Parameters use `1e-4` scaled tolerance for the cross-optimizer comparison; likelihood and information criteria retain the tighter cross-language tolerances.

**Report evidence.** [Distribution fitting verification](../verification/distribution-fitting.md#tr-064---distribution-fitting-optimizer-tolerance) and [evidence artifact](../../verification/data/distribution-fitting/fitting-analysis-optimizer-precision.json).

**Evidence.** The deterministic common-data `FittingAnalysis` run uses differential evolution, whose stopping rule measures convergence of objective values across the population. The SciPy oracle uses a local configuration that converges in parameter or gradient space. For Gumbel, SciPy returned location `93.11234799935337` and scale `13.157628567998076`; BestFit returned location `93.1129321294911` and scale `13.157823249599968`. Their relative coordinate differences are approximately `6.27e-6` and `1.48e-5`, while the maximized likelihoods and information criteria agree at their tighter declared tolerances. A scaled parameter tolerance of `1e-4` is therefore appropriate for this global-versus-local optimizer comparison.

RMSE magnitudes are evaluated at each optimizer's returned parameter vector, so they are not required to be identical across the two configurations. The verification instead checks the RMSE equation to `1e-10` at the BestFit vector, checks inverse-RMSE weights to `1e-12` from those actual values, and requires the SciPy and BestFit RMSE rankings to agree exactly.

**Impact.** No production defect was established. Both optimizers identify effectively identical likelihood solutions, and candidate ordering and model weights remain verified under comparisons appropriate to the quantities being tested.

**Follow-up.** Retain `1e-4` scaled parameter tolerance for comparisons between objective-converged global optimizers and parameter-converged local optimizers. Continue to enforce tight likelihood parity, exact criterion formulas, and exact candidate rankings.

<a id="tr-065"></a>
## TR-065 - GMM Influence Hessian Scale Depended on Penalty Presence

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API, optimizer, objective, gradient, penalty, covariance, or serialization changes.

**Verification status.** Passed every pointwise and aggregate comparison against R `gmm` 1.9.1 at the predeclared `1e-5` absolute tolerance. A second focused method passed the `1e-4` finite-wide-penalty invariance tolerance.

**Report evidence.** [Model-estimation verification](../verification/model-estimation.md#gmm-calibration-against-r), [R generator](../../verification/r/model-estimation/generate_gmm_influence_oracle.R), and [oracle artifact](../../verification/data/model-estimation/gmm-influence-oracle.json).

**Evidence.** GMM observation scores and bread use the half-quadratic estimating-equation convention. `GetLeverageDiagnostics()` formerly obtained Cook curvature from the public `Q` method, which returns $\mathbf g^\mathsf T\mathbf W\mathbf g$ when no penalty exists but $\tfrac12\mathbf g^\mathsf T\mathbf W\mathbf g+P$ when a penalty exists. The unpenalized numerical Hessian was therefore twice the score-consistent Hessian, and its inverse halved every Cook value. On the seven-point Log10-Normal fixture, observation zero was `0.0138256180` rather than R's `0.0276512391`; total Cook influence was `0.0440051` rather than `0.0880102`. Merely enabling an effectively flat centered penalty restored the R scale, proving that the diagnostic changed units based only on penalty presence. Variance influence already matched R at `0.6160714`.

**Correction.** `GetLeverageDiagnostics()` now differentiates a private local diagnostic objective $\tfrac12\mathbf g^\mathsf T\mathbf W\mathbf g+P$ in both penalized and unpenalized cases. Penalty-deletion generalized variance uses that same diagnostic objective. The estimator's public `Q`, `GetGradient`, parameter estimates, penalty Hessian, and covariance bread/meat are unchanged.

**Regression control.** `Log10NormalObservationInfluence_MatchesRGmmOracle` verifies all seven observation Cook, variance, and combined values plus all totals. `VanishingCenteredPenalty_PreservesObservationInfluenceScale` proves that a centered penalty at $100SE_L$ does not change observation diagnostic scale. MAP and GMM regime tests separately verify wide-centered, narrow-centered, and narrow-shifted behavior, and the fixed-centered sample-size test verifies declining variance influence.

**Impact.** GMM fit influence now has one estimator-consistent scale regardless of penalty configuration. The combined leverage plot and percentages no longer jump by a factor of two when a negligible penalty is toggled.

**Follow-up.** Retain the pinned R artifact and focused methods. Do not compare GMM Cook magnitudes directly with likelihood-based MAP Cook magnitudes.
## Resolution Rule

A documentation-only clarification may close a finding when the implementation is intentional and mathematically coherent. A defect in production behavior is never silently corrected by documentation. It moves to a separately authorized code-change task, receives focused unit tests, and uses the repository's mandated build/test gates. The complete `RMC.BestFit.Verification` suite is never run as one command; verification executes one exact fully qualified method at a time through the guarded runner.

---

[Technical Reference](index.md)
