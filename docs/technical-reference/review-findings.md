<!-- technical-reference-status: in-progress -->

# Scientific Review Findings

[Technical Reference](index.md)

This is the canonical register for disagreements among statistical theory, the pinned RMC.Numerics 2.1.4 source, RMC.BestFit behavior, tests, and earlier documentation. Production behavior is not changed during the documentation program. Each open item requires a separately authorized correction session, focused unit tests, and—where scientific parity is claimed—approved verification evidence.

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
| [TR-011](#tr-011) | Univariate AIC/BIC posterior kernel | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-012](#tr-012) | Competing-risk dependent simulation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-013](#tr-013) | Non-finite composite criteria | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-014](#tr-014) | Composite posterior draw coupling | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-015](#tr-015) | Composite correlation matrix configuration | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-016](#tr-016) | Bulletin 17C frequentist terminology | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-017](#tr-017) | Bulletin 17C bootstrap naming | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-018](#tr-018) | Bulletin 17C failed bootstrap fits | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-019](#tr-019) | Bulletin 17C bootstrap truncation | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-020](#tr-020) | Bulletin 17C Cohn diagnostics scope | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-021](#tr-021) | Bulletin 17C release evidence | Evidence | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-022](#tr-022) | NUTS versus HMC inventory | Documentation/API | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-023](#tr-023) | MLE/MAP coordinate slices | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-024](#tr-024) | PSIS tail smoothing | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-025](#tr-025) | ARWMH covariance adaptation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-026](#tr-026) | GMM Hansen J statistic | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-027](#tr-027) | Zero covariance on numerical failure | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-028](#tr-028) | Joint prior-predictive sampling | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-029](#tr-029) | MCMC diagnostic claims | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-030](#tr-030) | NUTS acceptance reporting | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-031](#tr-031) | Leverage interpretation | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-032](#tr-032) | GMM influence labeled Pareto k | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-033](#tr-033) | GMM objective/gradient scale | High | Rejected non-defect | No change required | Passed | [Model-estimation verification](../verification/model-estimation.md#gmm-objective-gradient-and-covariance-scaling) | 2026-07-25 |
| [TR-034](#tr-034) | Overidentified one-step GMM | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-035](#tr-035) | Time-series Jeffreys component type | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-036](#tr-036) | Transform fitting holdout leakage | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-037](#tr-037) | ARIMA/ARIMAX reintegration index | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-038](#tr-038) | ARIMA simulation transform/differencing | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-039](#tr-039) | ARIMAX simulation scale mixing | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-040](#tr-040) | Pointwise time-series invalid scale | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-041](#tr-041) | Differenced ARIMAX alignment | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-042](#tr-042) | Time-series/rating AIC/BIC kernel | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-043](#tr-043) | Rating-curve log10 Jacobian | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-044](#tr-044) | Rating-curve zero-exponent continuity | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-045](#tr-045) | Rating-curve unused-record validation | Medium | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-046](#tr-046) | Manual transform state rebuild | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-047](#tr-047) | Bivariate AIC/BIC posterior kernel | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-048](#tr-048) | Spatial missing-site marginalization | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-049](#tr-049) | Spatial likelihood decomposition | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-050](#tr-050) | Spatial cross-validation result retention | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-051](#tr-051) | Spatial held-out-site leakage | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-052](#tr-052) | Spatial held-out covariates | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-053](#tr-053) | Failed spatial folds counted as zero | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-054](#tr-054) | Ungauged conditional spatial variance | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-055](#tr-055) | Spatial AIC/BIC definition | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-056](#tr-056) | Spatial bootstrap data wiring | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-057](#tr-057) | Spatial Godambe decomposition | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-058](#tr-058) | Regional posterior interval construction | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-059](#tr-059) | Spatial site-weight interpretation | Methodological | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-060](#tr-060) | Spatial distance units | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-061](#tr-061) | Spatial dependent simulation | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-062](#tr-062) | Spatial uncertainty-method dispatch | High | Unreviewed | Not started | Planned | This register | 2026-07-24 |
| [TR-063](#tr-063) | Whole-series replacement leaves plotting positions stale | Medium | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-063---whole-series-replacement-refresh) / [Artifact](../../verification/data/distribution-fitting/dataframe-series-replacement.json) | 2026-07-24 |
| [TR-064](#tr-064) | DE/BFGS optimizer tolerance parity | Medium | Rejected non-defect | N/A | Passed - SciPy parity | [Report](../verification/distribution-fitting.md#tr-064---distribution-fitting-optimizer-tolerance) / [Artifact](../../verification/data/distribution-fitting/fitting-analysis-optimizer-precision.json) | 2026-07-25 |
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
## TR-011 — Univariate AIC/BIC Use Posterior at MAP

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `UnivariateAnalysis.UpdatePointEstimateResultsAsync()` evaluates `UnivariateDistribution.LogLikelihood(MAP)`, which includes priors, then passes that value to AIC and BIC helpers. Conventional AIC/BIC use maximized data log likelihood; priors make the reported value sensitive to prior density and parameterization.

**Impact.** Fields labeled AIC/BIC are not comparable with `FittingAnalysis` MLE AIC/BIC or standard definitions, and composite weights can mix incompatible criteria.

**Follow-up.** Compute conventional AIC/BIC from an MLE data likelihood, or rename the current quantities and prohibit Akaike/BIC interpretations. Add tests with informative versus flat priors showing the selected convention.

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

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `Bulletin17CDistribution` implements `IGMMModel`, not `IModel`, and defines no likelihood, prior, posterior, or MCMC target. `Bulletin17CAnalysis` nevertheless exposes a `BayesianAnalysis` property and stores GMM uncertainty draws in `MCMCResults`; the GMM estimate is placed in `MAP`, ensemble averages are exposed as `PosteriorMean`, and `CredibleIntervalWidth` controls frequentist confidence limits.

**Impact.** API consumers and generated reports can incorrectly describe a sampling distribution as a posterior, a GMM estimate as a posterior mode, and confidence intervals as credible intervals. Bayesian diagnostics and information criteria are not defined for these draws.

**Follow-up.** Introduce estimator-neutral uncertainty/result abstractions or explicit aliases, preserve serialization compatibility, suppress inapplicable Bayesian diagnostics, and test terminology in public reports and UI labels.

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
## TR-022 — BestFit Exposes NUTS, Not Plain HMC

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `BayesianAnalysis.SamplerType` contains `DEMCz`, `DEMCzs`, `ARWMH`, and `NUTS`. `SetUpSampler()` constructs the matching Numerics sampler. The pinned Numerics source also contains a separate `HMC` class, but BestFit has no `HMC` enum member or configuration branch. Planning material and older documentation name HMC as a selectable BestFit sampler.

**Impact.** A reviewer or API consumer could look for a nonexistent `SamplerType.HMC`, while the actual gradient-based option—adaptive NUTS—would be undocumented or described under the wrong algorithm.

**Follow-up.** Treat the current public API as authoritative in the technical reference. Decide separately whether plain HMC should become a supported BestFit option or whether project-level capability lists should be corrected to NUTS.

<a id="tr-023"></a>
## TR-023 — MLE and MAP “Profile Likelihoods” Are Coordinate Slices

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `MaximumLikelihood.ProfileLikelihood()` and `MaximumAPosteriori.ProfileLikelihood()` vary one parameter over a grid while holding all other parameters at the fitted values. Their interval methods solve cutoffs on the same fixed-coordinate slices. A statistical profile likelihood instead reoptimizes all nuisance parameters at every fixed value of the parameter of interest. The MAP version additionally slices the full log posterior and applies a chi-squared likelihood-ratio cutoff.

**Impact.** The returned curves can be narrower, asymmetric in the wrong way, or otherwise materially different from true profile likelihoods when parameters are correlated. `MaximumAPosteriori.ParameterConfidenceIntervals()` are neither frequentist profile-likelihood confidence intervals nor posterior credible intervals.

**Follow-up.** Either implement nuisance-parameter reoptimization and reserve “profile likelihood” for the MLE data likelihood, or rename the current methods as conditional coordinate slices. Define a statistically defensible MAP interval product separately and add correlated-parameter test cases.

<a id="tr-024"></a>
## TR-024 — PSIS Tail Smoothing Does Not Preserve the Required Tail Model or Ordering

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `ParetoSmoothWeights()` selects the largest raw importance weights, divides them by the cutoff in log space, and fits Numerics GPD MLE to ratios whose minimum is one. Numerics fixes the GPD location at that minimum, but BestFit discards the fitted location and evaluates a zero-location quantile. It also sorts indices from largest to smallest while assigning quantiles from smallest to largest, reversing the tail ranks. The routine does not form positive excesses above the cutoff as required by the generalized-Pareto tail approximation.

**Impact.** Smoothed importance weights, Pareto \(k\), pointwise ELPD, LOOIC, effective parameter count, standard error, and influence rankings may all be wrong. This directly affects model comparison and diagnostic conclusions.

**Follow-up.** Replace the helper with a tested PSIS implementation matched to a named reference version, including cutoff excesses, stabilized GPD fitting, monotone expected order statistics, truncation rules, and deterministic parity tests against a primary implementation such as the `loo` reference algorithms.

<a id="tr-025"></a>
## TR-025 — ARWMH Warmup Covariance Omits Repeated States

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** In pinned Numerics `ARWMH.ChainIteration()`, accepted proposals are always pushed into the running covariance. Rejected or out-of-bounds proposals push the retained state only when `SampleCount > ThinningInterval * WarmupIterations`. Thus the warmup covariance is calculated from accepted states only, whereas repeated states are added only after warmup. The proposal begins using that covariance after `100 * d` transitions.

**Impact.** Accepted-state-only covariance is not the empirical covariance of the Markov chain and can bias adaptation toward large or mobile moves. Continuing the running update after warmup also means the proposal is not frozen at the stated warmup boundary.

**Follow-up.** Define the intended adaptive-Metropolis schedule, update the covariance with every realized chain state during adaptation, and freeze or otherwise prove diminishing adaptation after warmup. Add tests for long rejection runs, out-of-bounds proposals, covariance sample counts, and stationary Gaussian targets.

<a id="tr-026"></a>
## TR-026 — The Reported GMM J-Statistic Is Not Hansen's J-Test

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `GeneralizedMethodOfMoments.PostProcess(computeJstat: true)` does not evaluate $J=n\mathbf g(\widehat{\boldsymbol\theta})^\mathsf T\widehat{\mathbf S}^{-1}\mathbf g(\widehat{\boldsymbol\theta})$. It instead projects the moment covariance, divides by $n$, attempts to invert the projected matrix, and forms a different quadratic. The projection is rank deficient under the usual overidentified geometry.

**Impact.** `JStatistic` and `JStatisticPValue` do not have the documented Hansen-test interpretation or its chi-squared reference distribution.

**Follow-up.** Implement the named statistic for the selected weighting convention and verify it against deterministic overidentified examples from a trusted econometrics implementation.

<a id="tr-027"></a>
## TR-027 — Covariance and Influence Failures Are Represented as Zeros

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** MLE, MAP, and GMM covariance routines catch inversion or factorization failures and return zero matrices. Related influence paths can consequently return zero-valued diagnostics. The result carries no status distinguishing a genuine zero from numerical failure.

**Impact.** Downstream code may report zero standard errors or apparently exact estimates when uncertainty evaluation actually failed.

**Follow-up.** Return an explicit failure status or exception, preserve diagnostics, prohibit uncertainty reporting when covariance is unavailable, and test singular, nearly singular, and well-conditioned cases.

<a id="tr-028"></a>
## TR-028 — Prior-Predictive Sampling Does Not Draw from the Full Model Prior

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `PriorPredictiveCheck.SampleFromPriors()` independently samples each `ModelParameter.PriorDistribution`, clamps values to bounds, and filters sets whose full prior is non-finite. It does not sample or reweight coupled quantile priors, Jeffreys factors, transformation Jacobians, spatial terms, or other contributions implemented only in `IModel.PriorLogLikelihood`. Clamping also creates boundary point masses rather than a truncated distribution.

**Impact.** The ensemble generally is not the prior predictive distribution for models with non-marginal prior structure, so apparent prior-data conflict can be created or hidden.

**Follow-up.** Add a model-level joint-prior sampler or validated weighting/MCMC method, use genuine truncation, report sampling failures, and verify independent and coupled-prior examples.

<a id="tr-029"></a>
## TR-029 — MCMC Documentation Claims Diagnostics That Are Not Implemented

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Numerics `MCMCResults` computes an unsplit between/within-chain Gelman–Rubin statistic and one autocorrelation ESS truncated at the first negative lag. It does not split or rank-normalize chains, fold draws, or calculate separate bulk and tail ESS. Comments and report wording nevertheless refer to split R-hat and bulk/tail ESS.

**Impact.** Reviewers may infer that modern heavy-tail and scale-sensitive convergence checks passed when only older summaries were evaluated.

**Follow-up.** Correct the labels and separately implement rank-normalized split/folded R-hat plus bulk and tail ESS with parity tests against a primary reference.

<a id="tr-030"></a>
## TR-030 — NUTS Acceptance Rate Is Always Reported as One

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Pinned Numerics NUTS increments the generic accepted-transition counter on every completed iteration because the tree always returns a retained state. `MCMCResults.AcceptanceRates` therefore reports 1.0 rather than the Hamiltonian acceptance statistic used for dual averaging. BestFit applies generic 0.65–0.90 guidance to that value.

**Impact.** Healthy NUTS runs are labeled as excessive-acceptance, while poor Hamiltonian behavior is not diagnosed by the reported rate.

**Follow-up.** Expose sampler-specific acceptance probability, divergences, maximum-tree-depth hits, and energy diagnostics; use sampler-specific report rules.

<a id="tr-031"></a>
## TR-031 — Leverage Components Lack the Claimed Hat-Matrix Interpretation

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `LeverageDiagnostics` adds a score-displacement quadratic to an absolute trace curvature term. Observation curvature uses diagonal second derivatives only, while prior components include a log-determinant ratio. These heterogeneous quantities have not been derived as one hat-matrix decomposition, yet comments and warnings compare their sum with the parameter count $p$.

**Impact.** Values can be mistaken for classical hat values or a proven Bayesian effective-parameter decomposition; rankings and thresholds have no established calibration.

**Follow-up.** Derive a coherent quantity, compute the full required Hessians, state invariance/additivity properties, and validate analytical Gaussian cases. Until then, label outputs experimental and remove the sum-to-$p$ assertion.

<a id="tr-032"></a>
## TR-032 — GMM Cook-Like Influence Is Stored and Classified as Pareto k

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `GeneralizedMethodOfMoments.GetInfluenceDiagnostics()` calculates a moment-based Cook-distance-like quadratic but stores it in `ObservationInfluence.ParetoK`. The shared DTO then applies PSIS thresholds 0.5, 0.7, and 1.0 and a PSIS reliability summary to that unrelated scalar.

**Impact.** GMM observations can be declared PSIS-problematic or reliable using thresholds that have no meaning for the computed diagnostic.

**Follow-up.** Introduce a GMM-specific influence result, reserve `ParetoK` for valid PSIS, and add serialization/UI tests preventing cross-diagnostic labels.

<a id="tr-033"></a>
## TR-033 — The Unpenalized GMM Objective and Gradient Have Different Scale

**Review disposition.** Rejected non-defect.

**Implementation status.** No production change required.

**Verification status.** Passed by focused analytical verification.

**Evidence.** Independent central differences confirm that the unpenalized estimating-equation gradient is one half of the derivative of the conventional reported objective $\mathbf g^\mathsf T\mathbf W\mathbf g$, while the penalized objective $\tfrac12\mathbf g^\mathsf T\mathbf W\mathbf g+P$ and supplied gradient agree exactly. Multiplication by the positive constant preserves the unpenalized stationary point. The reported GMM covariance is constructed from $\mathbf D^\mathsf T\mathbf W\mathbf D$ and the sandwich meat, not from the scalar objective Hessian. A deterministic Log10-Normal experiment further verifies that the current half-quadratic parameter penalty, gradient, penalty Hessian, and default Bulletin 17C covariance path reproduce both the inverse-variance posterior mean and posterior variance for wide-centered, narrow-centered, and narrow-shifted Gaussian information. The B17C GMM scale remains the intended unbiased $n-1$ moment estimate, while MAP retains its $n$-denominator MLE scale.

**Impact.** No point-estimate or covariance defect is present in the verified Log10-Normal path. The unpenalized constant factor is an optimizer-direction convention and is not a covariance multiplier. The penalized path, where relative data/prior scaling matters, is internally consistent and produces the required Gaussian inverse-precision inference.

**Follow-up.** Retain the two independent gradient-scale tests and the MAP/GMM inverse-variance test as permanent verification evidence. Any future change to the objective, gradient, penalty definition, or covariance bread/meat must preserve their joint equations.

<a id="tr-034"></a>
## TR-034 — Overidentified One-Step GMM Is Artificially Prohibited

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** Constructor validation rejects `GMMEstimationMethod.OneStep` whenever moments outnumber parameters. Standard one-step GMM is defined for overidentified systems given a fixed positive-definite initial weighting matrix.

**Impact.** The API excludes a standard estimator in the setting where weighting choices matter most, and the validation message teaches an incorrect identification rule.

**Follow-up.** Permit overidentified one-step estimation with a validated weighting matrix, or rename the narrower mode; test exact, over-, and underidentification.

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
## TR-042 — Phase 6 Analyses Compute AIC/BIC from a Posterior Kernel

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** AR, MA, ARIMA, ARIMAX, and rating-curve analyses evaluate `Model.LogLikelihood(Results.MAP.Values)`—data plus priors—and pass it to conventional `GoodnessOfFit.AIC/BIC`. This generalizes the issue recorded for univariate analysis in TR-011.

**Impact.** Values labeled AIC/BIC depend on prior density and parameterization and are not conventional likelihood criteria; comparisons across models or prior choices are invalid.

**Follow-up.** Compute AIC/BIC from a data-likelihood MLE or expose a clearly named alternative. Add tests showing invariance of conventional criteria to prior changes.

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
## TR-047 — Bivariate AIC and BIC Use the Posterior Kernel

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `BivariateAnalysis.UpdatePointEstimateResultsAsync` evaluates `BivariateDistribution.LogLikelihood` at the MAP estimate. That method includes parameter priors, whereas conventional AIC and BIC require the maximized data log-likelihood. The code comment says this prior-sensitive definition is intentional.

**Impact.** Reported AIC and BIC vary with the copula prior and are not comparable to their standard likelihood-based definitions or to external software. The BIC sample size is the matched-pair count, which is appropriate only after the likelihood issue is resolved.

**Follow-up.** Use `DataLogLikelihood` at an MLE (preferred) or clearly expose a separately named posterior-score diagnostic; retain DIC, WAIC, and LOOIC for Bayesian comparison.

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
## TR-055 — Spatial AIC and BIC Use the Posterior Kernel and Nominal Cell Count

**Review disposition.** Unreviewed.

**Implementation status.** Not started.

**Verification status.** Planned; no verification claim has been accepted.

**Evidence.** `SpatialGEVAnalysis.CreateUncertaintyAnalysisResultsAsync` evaluates `SpatialGEV.LogLikelihood` at the MAP estimate and uses `Sites * Observations` as the BIC sample size. The score includes priors and spatial-error densities, while the cell count includes missing values and does not represent the independent predictive unit under copula dependence.

**Impact.** The reported criteria do not have their conventional information-criterion interpretation and can be distorted by missingness, priors, and spatial dependence.

**Follow-up.** Define the comparison target and independent observation unit, use a maximized data likelihood for AIC/BIC, and prefer explicitly defined posterior predictive criteria for hierarchical spatial models.

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

**Regression control.** A custom-position XML round trip proves deserialization does not recalculate the serialized plotting positions. A special-value fixture proves that replacement with `NaN` or infinity still does not throw. The analytical verification independently reproduces all 39 Weibull nonexceedance probabilities as \(i/(n+1)\). The complete Debug regression gate passed Core 3,032, UI 564, and App 427 tests with zero failures or skips; the public API baseline and enforced XML-documentation build also passed.

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

## Resolution Rule

A documentation-only clarification may close a finding when the implementation is intentional and mathematically coherent. A defect in production behavior is never silently corrected by documentation. It moves to a separately authorized code-change task, receives focused unit tests, and uses the repository's mandated build/test gates. The complete `RMC.BestFit.Verification` suite is never run as one command; verification executes one exact fully qualified method at a time through the guarded runner.

---

[Technical Reference](index.md)
