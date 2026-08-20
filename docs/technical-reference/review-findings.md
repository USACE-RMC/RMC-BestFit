<!-- technical-reference-status: in-progress -->

# Scientific Review Findings

[Technical Reference](index.md)

The active continuation and batching plan is maintained in the [Verification Finalization Plan](../verification/verification-finalization-plan.md).

This is the canonical register for disagreements among statistical theory, the pinned RMC.Numerics 2.1.4 source, RMC.BestFit behavior, tests, and earlier documentation. Corrections require explicit authorization, focused tests, and—where scientific parity is claimed—approved verification evidence.

Closeout reconciliation (20 August 2026): Phase 1 and Phase 2 dispositions are closed for their approved scopes. All 16 associated oracle files match the manifest. Phase 3 is closed in its approved scope. The original Phase 4 findings are complete: TR-014 independently resamples the actual retained outputs used by Composite and coincident-frequency propagation, while `BivariateAnalysis` remains intentionally conditional on fixed marginals. The unchanged extreme-tail Composite method passed after the approved `XTransform.None` correction, bringing the 30-method recovery supplement to 24 passes. The six remaining Bayesian competing-risk findings are explicitly deferred for separate research and no longer gate Phase 5; none of the original Phase 4 finding dispositions is reopened.

## Summary

| ID | Finding | Severity | Review disposition | Implementation | Verification | Evidence | Updated |
|---|---|---|---|---|---|---|---|
| [TR-001](#tr-001) | Kappa Four zero-shape PDF and quantile | High | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-001---kappa-four-zero-primary-shape) · [Artifact](../../verification/data/distribution-fitting/kappa-four-zero-shape.json) | 2026-07-24 |
| [TR-002](#tr-002) | Kappa Four shape validation | Closed | Rejected non-defect | N/A | Passed - regression | [Report](../verification/distribution-fitting.md#tr-002---finite-kappa-shape-pairs) / [Artifact](../../verification/data/distribution-fitting/kappa-four-finite-shapes.json) | 2026-07-24 |
| [TR-003](#tr-003) | Nonstationary threshold chronology and prior reference time | Closed | Accepted documented conventions | Documentation complete; no code change required | Passed - source/documentation/literature audit | [Data frame](data-frame/index.md#stationary-and-nonstationary-chronology) · [Priors](models/parameters-and-priors.md#complete-univariate-prior) | 2026-07-28 |
| [TR-004](#tr-004) | Point-process rate definitions | High | Confirmed defect with accepted inference fallback | Complete | Passed - fast contracts and two independent mixed-likelihood cells | [Report](../verification/point-process.md#tr-004---exposure-and-rate-definitions) | 2026-07-31 |
| [TR-005](#tr-005) | Point-process Poisson-GPA simulation | High | Confirmed defect | Complete | Passed in approved scope - all ten guarded cells | [Report](../verification/point-process.md#tr-005---poisson-gpa-simulation-and-seasonal-priors) | 2026-07-31 |
| [TR-006](#tr-006) | Mixture weights and proposal mutation | High | Confirmed defect; corrected | Complete | Passed - fast, exact parity, and Bayesian recovery | [Report](../verification/mixture.md#tr-006--weights-and-proposal-purity) | 2026-07-31 |
| [TR-007](#tr-007) | Zero-inflated mixed distribution | High | Confirmed defect; corrected | Complete | Passed - analytical, simulation, parity, and Bayesian recovery | [Report](../verification/mixture.md#tr-007--positive-hurdle) | 2026-07-31 |
| [TR-008](#tr-008) | Mixture EM impossible rows | High | Confirmed defect; corrected | Complete | Passed - explicit-failure regressions and focused recovery | [Report](../verification/mixture.md#tr-008--impossible-rows) | 2026-07-31 |
| [TR-009](#tr-009) | RMSE residual omission | High | Confirmed defect | Fixed | Passed - analytical | [Report](../verification/distribution-fitting.md#tr-009---parameter-adjusted-rmse) / [Artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json) | 2026-07-24 |
| [TR-010](#tr-010) | FittingAnalysis all-failed status | Medium | Confirmed defect | Fixed | Passed - regression | [Report](../verification/distribution-fitting.md#tr-010---all-candidate-failure-reports-overall-success) · [Artifact](../../verification/data/distribution-fitting/fitting-analysis-success-state.json) | 2026-07-24 |
| [TR-011](#tr-011) | Bayesian AIC/BIC prior-density inclusion | High | Confirmed defect - resolved | Fixed | Passed - focused regression/source audit | [Report](../verification/model-estimation.md#aic-and-bic-evaluated-at-map) | 2026-07-25 |
| [TR-012](#tr-012) | Competing-risk dependent simulation | High | Confirmed defect; corrected | Complete | Passed - fast seed/validation contracts and four analytical rank/CDF methods | [Report](../verification/competing-risks.md) | 2026-08-03 |
| [TR-013](#tr-013) | Non-finite composite criteria | Medium | Confirmed defect; corrected | Complete | Passed - fast mixed-validity, zero-RMSE, finite-weight, and B17C compatibility contracts | [Report](../verification/composite.md#criterion-weighting) | 2026-08-03 |
| [TR-014](#tr-014) | Cross-analysis posterior draw coupling | Methodological | Confirmed concern; corrected | Complete | Passed - fast contracts and two independent numerical oracles | [Report](../verification/composite.md#independent-posterior-resampling) | 2026-08-03 |
| [TR-015](#tr-015) | Composite correlation matrix configuration | High | Confirmed defect; corrected | Complete | Passed - fast validation, ownership, persistence, and construction contracts | [Report](../verification/composite.md#correlation-matrix-configuration) | 2026-08-03 |
| [TR-016](#tr-016) | Bulletin 17C frequentist terminology | Closed | Accepted shared result-storage architecture | Documentation complete; no code/API change required | Passed - source/XML/report terminology audit | [Bulletin 17C](analysis/bulletin-17c.md#uncertainty-interpretation) | 2026-07-28 |
| [TR-017](#tr-017) | Bulletin 17C bootstrap naming | Closed | Rejected naming defect; documentation clarified | No production change required | Passed - paper/implementation concordance audit | [Uncertainty method](analysis/bulletin-17c-uncertainty.md) | 2026-07-28 |
| [TR-018](#tr-018) | Bulletin 17C failed bootstrap fits | Closed | Robust initialization and convergence-aware acceptance implemented; parent fallback retained only for output length | Complete | Passed - 14 exact cells; 13,000 outputs with zero retries, exceptions, or substitutions | [Test inventory](../verification/test-inventory.md#tr-018tr-019-bulletin-17c-bootstrap-refit-reliability---28-july-2026) | 2026-07-28 |
| [TR-019](#tr-019) | Bulletin 17C bootstrap truncation | Closed | Obsolete Mahalanobis rejection removed | Complete | Passed - unguarded 13,000-output sweep; zero malformed or failed refits | [Test inventory](../verification/test-inventory.md#tr-018tr-019-bulletin-17c-bootstrap-refit-reliability---28-july-2026) | 2026-07-28 |
| [TR-020](#tr-020) | Bulletin 17C Cohn diagnostics scope | Closed | Confirmed scope defect - resolved | LP3 exact-data guards implemented | Passed - fast scope regressions; numerical Cohn verification deferred | [Bulletin 17C report](../verification/bulletin-17c.md#cohn-diagnostic-scope) | 2026-07-28 |
| [TR-021](#tr-021) | Bulletin 17C formal-example parity | Closed | Current GMM formal-example source verified | Seven exact methods executed | Passed - 7/7 published worked examples at 1E-3 | [Bulletin 17C report](../verification/bulletin-17c.md#formal-worked-example-parameter-parity) | 2026-07-28 |
| [TR-022](#tr-022) | NUTS versus HMC inventory | Documentation/API | Confirmed defect | Fixed | Passed - source/API inventory | [Bayesian MCMC](estimation/bayesian-mcmc.md) | 2026-07-25 |
| [TR-023](#tr-023) | MLE and MAP nuisance profiling | High | Confirmed defect - resolved | Fixed without public API changes | Passed - R `bbmle`, closed-form, and informative-prior MAP parity | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) / [Artifact](../../verification/data/model-estimation/profile-likelihood-oracle.json) | 2026-07-27 |
| [TR-024](#tr-024) | PSIS tail smoothing | High | Confirmed defect - resolved | Fixed | Passed - R `loo` aggregate, pointwise, tail, threshold, and performance parity | [Report](../verification/model-estimation.md#psis-loo-and-pareto-diagnostics) / [Artifact](../../verification/data/model-estimation/psis-loo-oracle.json) | 2026-07-26 |
| [TR-025](#tr-025) | ARWMH covariance adaptation | High | Confirmed scoped defect - resolved | Fixed | Passed - focused Numerics and BestFit regression | [Report](../verification/model-estimation.md#numerics-mcmc-verification) | 2026-07-26 |
| [TR-026](#tr-026) | GMM Hansen J statistic | High | Confirmed defect - resolved | Fixed | Passed - R `gmm::specTest` J/p-value parity | [Report](../verification/model-estimation.md#gmm-specification-covariance-and-legacy-influence-verification) / [Artifact](../../verification/data/model-estimation/gmm-specification-oracle.json) | 2026-07-26 |
| [TR-027](#tr-027) | Explicit covariance failure status | High | Confirmed defect - resolved | Fixed | Passed - deterministic failure, success, and regularization paths | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) | 2026-07-26 |
| [TR-028](#tr-028) | Joint prior-predictive sampling | High | Confirmed limitation | Documented | Passed - source/contract and coupled-prior characterization | [Report](../verification/model-estimation.md#profile-likelihood-covariance-failure-and-joint-prior-characterization) / [Predictive checks](estimation/predictive-checks.md) | 2026-07-26 |
| [TR-029](#tr-029) | MCMC diagnostic claims | High | Confirmed defect - resolved | Modernized without API or serialization changes | Passed - R `posterior` 1.7.0 rank-normalized R-hat and ESS parity | [Report](../verification/model-estimation.md#rank-normalized-convergence-diagnostics-tr-029) / [Artifact](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json) | 2026-07-27 |
| [TR-030](#tr-030) | NUTS acceptance reporting | High | Confirmed defect - resolved | Fixed surgically | Passed - acceptance contract, gradient, JSON compatibility, and report regression | [Report](../verification/model-estimation.md#numerics-mcmc-verification) | 2026-07-26 |
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

The corrected source adds the exact zero-kappa density factor and fixes the inverse-CDF grouping. Four upstream regressions cover the analytical derivative, inverse/CDF round trip, support and normalization, and two-sided continuity. The normalized Numerics .NET 10 Release gate records 2,024 passing tests with no failures. Both exact BestFit verification methods pass independently at absolute tolerance `1e-10`.

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
## TR-003 — Nonstationary Threshold Chronology and Prior Reference Time

**Review disposition.** Closed as accepted, explicitly documented modeling conventions.

**Implementation status.** Documentation complete; no production-code, API, or serialization change is required.

**Verification status.** Passed by source, technical-documentation, and published-literature audit. No numerical oracle is required for this documentation-only disposition, and no permutation-invariance claim is made.

**Evidence.** `DataFrame.CreateFullTimeSeries()` retains explicit observation indexes and disaggregates a grouped perception period around a terminal split: unoccupied earlier indexes receive below-threshold records and unoccupied indexes in the final `NumberAbove` portion receive above-threshold records. It does not marginalize over other compatible allocations. For a nonstationary `UnivariateDistribution`, coefficient priors are evaluated on the trend coefficients, while distribution-dependent Jeffreys and quantile-prior terms use the distribution predicted at `DataFrame.FullTimeSeries.Last().Index`. The final-time quantile-prior reference is consistent with the published workflow of Viglione et al. (2013).

**Impact.** Group totals identify counts and bounds, not event chronology, so a nonstationary fit is conditional on the terminal-above disaggregation. Likewise, a quantile prior for a changing distribution expresses present-condition information at the most-recent observed time; it is not repeated across the historical record and does not automatically describe earlier conditions.

**Follow-up.** Retain these assumptions in practitioner-facing documentation. Analysts must use explicitly dated observations when event chronology is known, document the final-time reference for distribution-dependent priors, and assess alternative defensible allocations when grouped-threshold chronology could materially affect a result. A chronology-marginalized likelihood would be a separately approved enhancement, not unfinished TR-003 work.

<a id="tr-004"></a>
## TR-004 - Point-Process Rate Definitions

**Review disposition.** Confirmed defect with an accepted manual-data fallback.

**Implementation status.** The public empirical count/rate and fitted threshold intensity are distinct. `Lambda` remains the empirical exact-event-rate alias. Source-record exposure is serialized on `DataFrame`; explicit `TotalYears` takes precedence, followed by stored source exposure and then the exact-record year/index span.

**Verification status.** Passed. Fast mixed-count, precedence, warning, seasonal-date, annualized mixed-likelihood, state-refresh, and serialization regressions pass. Both guarded independent mixed-likelihood cells pass, including uncertain, interval, and threshold records under the seasonal annual-maximum distribution.

**Evidence.** `CalculateLambda()` counts every exact POT record and excludes uncertain, interval, and threshold-count rows. Simulation uses that empirical rate, while the likelihood separately exposes the GEV-compatible fitted threshold intensity. POT extraction retains inclusive source-record years, including leading and trailing years with no selected peak. Manual POT data fall back to exact year/index span and receive an inference warning.

**Resolved authority decisions.** Seasonal fitting now requires a valid date on every exact POT observation; nonseasonal manual records retain year/index-span exposure fallback. Seasonal uncertain, interval, and threshold records are annual/block-indexed. Their likelihood uses the annual maximum of the two independent exposure-adjusted seasonal processes through **CompetingRisks**. Exposure fractions weight process intensities and are not annual mixture probabilities.

**Closeout.** The seasonal fixture correction retained one below-threshold and two above-threshold annual observations and derived its oracle from the processed counts. No production likelihood or tolerance changed. TR-004 is complete.

<a id="tr-005"></a>
## TR-005 - Point-Process Poisson-GPA Simulation

**Review disposition.** Confirmed defect.

**Implementation status.** The approved production process is implemented. Both simulation surfaces use empirical `Lambda` for Poisson counts, Madsen conversion from Hosking GEV to Hosking GPA for marks, floored changepoints, analytical exposure weights, and the shared elapsed block-day convention. GEV priors, exact-event point-process likelihood equations, sampler settings, tolerances, and seed behavior remain unchanged.

**Verification status.** Complete in the approved scope. All ten guarded current-source cells pass. Recovery fixtures use 1,000 observations and the untouched `BayesianAnalysis` defaults. Calendar-year uniform recovery, October-water-year block-origin parity, nonseasonal production recovery, and seasonal production recovery pass; the robust defaults eliminate the former seasonal second-Kappa miss. The initial water-year failure was a verification-coordinate error: the fixture changed `K1/K2` from `170/350` to `80/260` rather than keeping the parent block-day parameters fixed while changing only the block origin.

**Evidence.** Seasonal exposure is \(w_1=(k_1+366-k_2)/366\) and \(w_2=(k_2-k_1)/366\). Default supports are \([1,251)\) and \([200,367)\); the linear-time rotated monthly histogram may supply five-month flat windows, while ambiguous, insufficient, flat, or undated timing retains broad defaults. Independent exponential-clock Poisson and analytical Hosking-GPA fixtures support the scientific checks.

**Impact.** Generated counts, component assignments, dates, and marks now follow the approved Poisson-GPA parent process. Automatic changepoint priors enter a broad seasonal neighborhood without profile likelihood, MAP preprocessing, or changes to GEV defaults.

**Closeout.** TR-005 is complete without changing any production formula, DEMCzs default, prior, or recovery tolerance. PERT timing remains placement evidence only because its interior timing law is absent from the fitted likelihood.

<a id="tr-006"></a>
## TR-006 — Mixture Weights Are Redundant and Mutate Candidate Arrays

**Review disposition.** Confirmed defect; the direct physical $K-1$ correction was approved.

**Implementation status.** Complete in reachable Numerics commit `3e69a93` and the BestFit Phase 4 mixture batch without changing any public weight-related method signature.

**Verification status.** Passed. Fast regressions, three exact Numerics/BestFit recovery-parity methods, and three Bayesian `MixtureAnalysis` generation-and-recovery methods pass. Every focused method was run separately through the guarded runner and produced one passing TRX.

**Evidence.** Numerics retains all $K$ physical weights in its public arrays and copies caller input before normalization or assignment. BestFit now tracks only $w_1,\ldots,w_{K-1}$, derives $w_K=m-\sum_{j=1}^{K-1}w_j$, rejects infeasible proposals, and uses one nonmutating reconstruction path. Its proper flat physical-simplex prior includes $\log\Gamma(K)-(K-1)\log m$. EM outputs, covariance, parameter names, and information-criterion dimension use $K-1$ weights.

**Impact.** The redundant radial direction and objective-side proposal mutation are removed. Existing saved mixture posterior results from the former normalized $K$-weight workflow require re-estimation; no migration or parameterization version is provided.

**Closeout.** The production BestFit generator supplies all six seeded samples. Exact parity retains the declared likelihood and recovery tolerances; Bayesian recovery retains deterministic DEMCzs settings and requires finite split R-hat below 1.1 and conservative ESS above 100 for every fitted coordinate. Saved results from the former parameterization still require re-estimation.

<a id="tr-007"></a>
## TR-007 — Zero-Inflated Mixture Probability Functions

**Review disposition.** Confirmed defect; the exact-zero positive-hurdle interpretation was approved.

**Implementation status.** Complete in reachable Numerics commit `3e69a93` and the BestFit Phase 4 mixture batch.

**Verification status.** Passed. Analytical identities, simulation, support, invalid-positive-mass, and mixed-observation fast tests pass. The guarded zero-inflated parity and Bayesian recovery methods pass; Bayesian recovery also checks the generated atom against its five-standard-error binomial bound.

**Evidence.** The corrected law has $F(x)=0$ for $x<0$, an atom $\pi_0$ at zero, and component distributions conditioned on $X>0$ for every positive continuous contribution. `PDF(0)=pi0` is documented under the Lebesgue-plus-Dirac reference measure. CDF, log density, quantiles, simulation, and BestFit exact, uncertain, interval, and threshold likelihoods use the same law. BestFit derives the fixed atom only from exact zeros divided by all exact annual records and rejects negative exact observations.

**Impact.** The probability functions, likelihood, and simulator now define one coherent distribution; negative values are no longer collapsed into the zero atom.

**Closeout.** The positive-hurdle generator, likelihood, EM fit, and Bayesian fit recover the same seeded parent without tolerance changes.

<a id="tr-008"></a>
## TR-008 — Mixture EM Skips Impossible Rows

**Review disposition.** Confirmed defect; explicit failure was approved.

**Implementation status.** Complete in reachable Numerics commit `3e69a93` and the BestFit Phase 4 mixture batch.

**Verification status.** Passed. Fast impossible-row tests pass for Numerics exact data and BestFit exact, uncertain, interval, and threshold records. All six guarded recovery methods pass with the explicit-failure contract retained.

**Evidence.** Numerics and BestFit now throw `InvalidOperationException` containing row index and value whenever the required total row probability is zero or nonfinite. The exact-data BestFit path delegates to the corrected Numerics EM, while the mixed path applies the same explicit failure contract.

**Impact.** An impossible observation can no longer disappear from the objective or leave stale responsibilities behind.

**Closeout.** Explicit impossible-row failures remain part of the verified contract; no row skipping or convergence-tolerance change was introduced.

<a id="tr-009"></a>
## TR-009 — RMSE Omits the Last \(k\) Residuals

**Review disposition.** Confirmed defect.

**Implementation status.** Fixed without public API changes in RMC.Numerics commit `24bf9f98139b23400bf008df413b0d97330ccfd3`. The numerator now includes every residual, and only the denominator uses the residual degrees of freedom \(n-k\). Invalid parameter counts that do not leave positive residual degrees of freedom are rejected.

**Verification status.** Passed by an analytical hand calculation and paired-permutation test at absolute tolerance \(10^{-12}\). The normalized Numerics .NET 10 Release gate records 2,024 passing tests with no failures. See the [distribution-fitting verification chapter](../verification/distribution-fitting.md#tr-009---parameter-adjusted-rmse) and [result artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json).

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

**Review disposition.** Confirmed defect; corrected.

**Implementation status.** Complete. Pinned Numerics commit `cafe6cf3837988341912a5aa8bfda444ea55ff77` preserves the established simulation signature and independent seeded sequence while routing all dependency modes through the existing dependency-aware implementation. BestFit preflights any required matrix before calling Numerics.

**Verification status.** Passed. Fast Numerics and BestFit contracts cover seed determinism, independent golden-sequence preservation, entry-point equivalence, mode selection, and invalid matrices. Four exact guarded methods independently verify every dependency mode against Gaussian-copula Spearman-rank and analytical maximum-CDF targets; see [competing-risks verification](../verification/competing-risks.md).

**Evidence.** The prior Numerics public entry point contained a separate independent-only loop even though a dependency-aware method already existed. The corrected entry point delegates to that method. Independent mode retains its original PRNG loop, perfectly positive mode shares ranks, and the perfectly negative/correlation-matrix modes use the configured Gaussian copula.

**Impact.** Simulation now follows the same configured dependency semantics as CDF evaluation for all supported modes. Invalid custom matrices fail explicitly before sampling.

**Follow-up.** Retain the independent golden sequence and all four exact rank/CDF cells. Do not change the limiting perfectly-negative construction or seed defaults without separate approval.

<a id="tr-013"></a>
## TR-013 — Non-Finite Composite Criteria

**Review disposition.** Confirmed defect; corrected.

**Implementation status.** Complete without changing public weighting signatures. Every estimated child criterion is classified before weighting. Invalid values are exactly zero-weighted when a usable child remains and cause an explicit invalid result only when no usable criterion remains. Exact-zero RMSE is handled as a separate limiting case.

**Verification status.** Passed by fast programmatic tests for mixed finite/non-finite criteria, all-invalid criteria, negative and exact-zero RMSE, ordinary finite-weight parity, normalization, and Bulletin 17C participation.

**Evidence.** Criterion classification now excludes NaN and infinities for every method and negative RMSE. Exact-zero RMSE children split unit weight. Bulletin 17C remains eligible for Equal/AIC/BIC/RMSE; its non-posterior compatibility container does not supply DIC/WAIC/LOOIC, so those values are treated as unavailable rather than rejecting the child type.

**Impact.** One invalid child can no longer contaminate otherwise usable models, and no zero-RMSE case divides by zero or publishes NaN. A model average still cannot be defined when every selected criterion is unavailable or invalid.

**Follow-up.** Retain named diagnostics and exact-zero tests. B17C must not be type-rejected: it participates in every criterion it computes and receives zero weight only when the selected posterior criterion is unavailable.

<a id="tr-014"></a>
## TR-014 — Cross-Analysis Posterior Draw Coupling

**Review disposition.** Confirmed methodological concern; corrected under the approved independent product-posterior policy.

**Implementation status.** Complete. One internal helper validates positive actual retained-output counts, sets the realization count to their minimum, and uses Numerics `NextIntegers(..., replacement: false)` to generate one full-range index row per source from the owning analysis's existing nonnegative `PRNGSeed`. Composite applies those rows in both construction branches. CFA uses fixed semantic order (copula, optional X, optional Y), caches the mapping for indexed access, and invalidates cache and results when sources or seed change. No index arrays are serialized, no Numerics code or public API changed, and `BivariateAnalysis` remains unchanged.

**Verification status.** Passed. Fast helper tests establish unique in-range rows, complete longer-chain range, exact seed repeatability, distinct source permutations, and pairwise absolute Spearman correlation below `0.05` for 5,000 draws. Composite and CFA fast tests cover both composition branches, unequal counts, source separation, fallback, cache identity/invalidation, child immutability, point-estimate stability, and UI seed save/open/copy/undo. The two exact guarded methods pass independent Cartesian-product and closed-form Normal-sum oracles at maximum mean tolerance `0.02` and credible-limit tolerance `0.05`; each raw-paired negative control misses by at least `0.10`.

**Evidence.** The generated sample targets the product posterior of separately fitted sources. Longer chains contribute across their complete retained range without replacement, while every source contributes exactly the shortest actual output count. A fixed seed, source order, and retained order reproduce the exact matrix. Reversing a chain or swapping commutative Composite children changes the finite seeded sample but passes the same independent target, establishing distributional rather than bitwise order invariance. Child `MCMCResults` and point estimates remain unchanged.

**Impact.** Composite and CFA uncertainty summaries no longer encode raw-index alignment among separately fitted chains. Saved derived summaries created before TR-014 remain readable but must be reprocessed to adopt the corrected coupling policy.

**Follow-up.** Preserve the existing seed/semantic source-order contract and transient-cache policy. Do not reinterpret `BivariateAnalysis` intervals as marginal-uncertainty propagation; optional marginal propagation remains downstream in CFA.

<a id="tr-015"></a>
## TR-015 — Composite Correlation Matrix Cannot Be Supplied

**Review disposition.** Confirmed defect; corrected.

**Implementation status.** Complete. The authorized `CorrelationMatrix` property was added to core and UI composite analyses. Existing constructors, methods, parameter vectors, seed behavior, defaults, and result signatures remain unchanged. XML and UI persistence use optional appended fields so legacy projects remain readable.

**Verification status.** Passed by fast core/UI tests for defensive ownership, square/dimension checks, finite bounds, unit diagonal, symmetry, strict positive definiteness, invariant XML, legacy-null loading, SQLite save/open, copy independence, and result construction.

**Evidence.** Core validation now requires a matrix exactly when correlation-matrix competing-risk dependence is active and requires its dimension to match the child count. Point-estimate, modal, and retained-realization constructions all copy the matrix into the new Numerics `CompetingRisks` object.

**Impact.** The existing public enum value is now fully configurable and persistable; malformed configurations fail during assignment, validation, or simulation preflight with explicit messages.

**Follow-up.** Retain the property in the public API baseline and preserve the established `CorrelationMatrix`/`Correlation_Row` serialization names.

<a id="tr-016"></a>
## TR-016 — Bulletin 17C Frequentist Results Use Bayesian/MCMC Terminology

**Review disposition.** Accepted shared result-storage architecture; the terminology limitation is documented.

**Implementation status.** Complete as documentation only. Bulletin 17C intentionally uses the same `BayesianAnalysis`/`MCMCResults` storage architecture as Bayesian analyses so persistence, result reprocessing, and UI consumers share one stable shape. No production behavior, public API, serialization member, alias, or parallel result hierarchy was added.

**Verification status.** Passed by source, XML-documentation, and report-terminology audit. No numerical verification was required for this architectural clarification.

**Evidence.** `Bulletin17CDistribution` implements `IGMMModel`, not `IModel`, and defines no likelihood, prior, posterior, or MCMC target. In the shared container, `MAP` stores the penalized GMM point estimate, `Output` stores the frequentist uncertainty ensemble, `PosteriorMean` is the ensemble arithmetic mean, and `CredibleIntervalWidth` supplies the confidence level. Inapplicable DIC, WAIC, LOOIC, R-hat, and effective-sample-size interpretations remain unset or out of scope.

**Impact.** The stable shared architecture avoids a second serialization and result-processing system, but consumers must interpret its legacy Bayesian-shaped member names in the Bulletin 17C context. The technical reference and XML comments now state that mapping explicitly. Shared AIC/BIC fields remain labeled pseudo-criteria formed from the LP3 data likelihood at the GMM solution, not likelihood-maximized or Bayesian criteria.

**Follow-up.** Preserve the documented mapping and serialization compatibility. A future breaking API redesign may introduce estimator-neutral names, but it is not required for TR-016.

<a id="tr-017"></a>
## TR-017 — `BiasCorrectedBootstrap` Implements a Bias-Corrected Pivotal Bootstrap (Closed)

**Review disposition.** Rejected naming defect; the implementation and pending methods paper support the existing name. A documentation ambiguity was confirmed and corrected.

**Implementation status.** No production, enum, public-API, GUI-label, or serialization change is required. `BiasCorrectedBootstrap = 3` remains the stable compatibility contract.

**Verification status.** Passed by concordance audit against Smith and Stedinger, *A Bias-Corrected Pivotal Bootstrap for Objective-Bayes Parameter Ensembles* (manuscript in preparation, local paper checkpoint `aaec860e875e`) and its reference engine. No numerical verification was rerun.

**Evidence.** BestFit implements the paper's three-part construction: refit each parametric sample and retain its covariance; form the multivariate studentized pivot $\mathbf z_b=(\mathbf L_b^*)^{-1}(\widehat{\boldsymbol\eta}-\widehat{\boldsymbol\eta}^{*(b)})$ in variance-stabilizing link space; then re-inflate with the parent factor $\mathbf L$ and invert the link. The replicate covariance removes local bias and heteroskedasticity, while the parent covariance expresses the corrected draw in the parent's uncertainty. This is the method the paper names the *bias-corrected pivotal bootstrap* and describes as second-order under its stated regularity and estimator-target conditions.

**Impact.** The compact enum and GUI label are scientifically defensible and easier for practitioners than the full technical name. Confusion remains possible because Efron's scalar BC and BCa intervals also use “bias-corrected”; documentation must therefore identify this as a multivariate pivotal/studentized ensemble and explicitly state that it is not the BC or BCa endpoint algorithm.

**Documentation convention.** Retain “Bias-Corrected Bootstrap” in the enum and GUI. On first technical mention, use “bias-corrected pivotal bootstrap,” explain that the correction is intrinsic to the two-covariance pivot, and distinguish it from BC/BCa. Link stabilization, bounds repair, and failed-refit policy remain documented separately from the paper's bare construction.

<a id="tr-018"></a>
## TR-018 — Failed Bulletin 17C Bootstrap Refits Become Parent-Estimate Mass

**Review disposition.** Confirmed numerical-reliability risk. The parent-fit fallback remains an explicit operational policy because downstream processing requires the configured output length; the primary correction is to make refit exhaustion exceptional.

**Implementation status.** Complete. Every bootstrap realization preserves the parent model's bounds, links, and penalties; draws one randomized penalty target; constructs midpoint-moment, ROS-moment, distribution-default, and parent-fit starts; ranks valid distinct starts by the first-pass penalized GMM objective; and tries them against the same realization before generating another sample. A finite fit is accepted when its inner optimizer reports `Success` or iterative GMM confirms `ConvergedWithinTolerance`. The ten-realization retry limit and final parent-fit substitution remain only to preserve configured output length.

**Verification status.** Passed. Fast tests cover bounded midpoint construction, candidate validity, distinctness, bounds, ordering, invalid dimensions, and pivotal parameter repair. Fourteen seeded ordinary/pivotal Examples 1-7 cells were executed separately: 1,000 outputs per method for Examples 1-6 and 500 per method for highly censored Example 7. The final unguarded run produced 13,000 finite outputs from exactly 13,000 attempted realizations with zero retries, optimizer fallbacks, parent substitutions, failed GMM candidates, and first-chance exceptions from Numerics or RMC.BestFit.

**Evidence.** Alternate initializations are optimizer restarts for one fixed bootstrap data/penalty target; they are not additional bootstrap draws. During diagnosis, an apparent Example 3 failure was a false rejection: every candidate had reached outer iterative-GMM tolerance with objectives near zero, although the final inner pass reported `MaximumFunctionEvaluationsReached`. Honoring the estimator's convergence contract eliminated that retry. The final grid accepted 140 such confirmed-converged results and required no alternate realization. Exhausted ordinary refits still receive the parent vector, and exhausted pivotal phase-one refits receive the parent vector and covariance, preserving the exact-output-count contract while incrementing `FailedReplicates`. Exact per-cell evidence is recorded in the [test inventory](../verification/test-inventory.md#tr-018tr-019-bulletin-17c-bootstrap-refit-reliability---28-july-2026).

**Impact.** Ranked same-realization starts and convergence-aware acceptance eliminated fallback mass in the 13,000-output worked-example grid without changing requested ensemble size. Any future nonzero `FailedReplicates` still creates artificial point mass at the fitted estimate and can narrow confidence limits, so it remains a verification failure for this grid and a required report diagnostic in production.

**Follow-up.** Retain the exact reliability cells as zero-retry regressions and continue reporting retries, optimizer fallbacks, and parent substitutions. If parent substitution is exercised, reopen the initialization/solver defect rather than accepting fallback mass as normal behavior.

<a id="tr-019"></a>
## TR-019 — Bulletin 17C Bootstrap Uses Asymptotic Mahalanobis Truncation (Closed)

**Review disposition.** Confirmed truncation defect; the guard was no longer needed after refit reliability was corrected and has been removed.

**Implementation status.** Complete. Ordinary and pivotal bootstrap refits no longer compute parent-covariance Mahalanobis distances or reject converged fits against a chi-squared threshold. Multi-start fitting, finite-parameter validation, pivotal covariance checks, parameter-bound repair, fresh-realization retries, and the fixed-length parent fallback remain.

**Verification status.** Passed. In the complete guarded Examples 1-7 diagnostic sweep, every outer retry was caused by Mahalanobis rejection of an otherwise finite converged refit. After removal, the same 14 exact cells produced 13,000 finite outputs from 13,000 realizations with zero retries, zero rejections, zero parent substitutions, zero optimizer fallbacks, and zero BestFit/Numerics first-chance exceptions.

**Evidence.** Guarded runs forced resampling without exposing any independent solver, covariance, or transform failure. The unguarded runs accepted those tail refits directly and still satisfied all parameter, covariance, output-length, and exception assertions, including highly censored Example 7. Example 5 pivotal separately exercised the model-bound repair for one inverse-linked skew draw.

**Impact.** Removing the guard eliminates an undocumented asymptotic truncation of the empirical bootstrap distribution, avoids unnecessary resampling, and preserves genuine tail behavior. The safeguards that directly test numerical usability remain in place.

**Follow-up.** Retain the no-guard reliability grid as a regression. Pivotal smoothing and $z$ clipping remain separate operational choices whose calibration and coverage evidence are not resolved by this closeout.

<a id="tr-020"></a>
## TR-020 — Cohn-Style Bulletin 17C Diagnostics Are Unguarded LP3 Calculations

**Review disposition.** Confirmed scope defect; resolved by explicit validation.

**Implementation status.** Complete. `ComputeCohnStyleConfidenceIntervals()` now throws `NotSupportedException` unless the parent is Log-Pearson Type III and the data are exact, with no low outliers, uncertain observations, interval censoring, or threshold censoring. The report-side asymptotic-variance path uses the same guard and reports why the diagnostic is unavailable instead of applying LP3 calculations outside scope.

**Verification status.** Fast unit tests cover all five non-LP3 parent families, all four unsupported LP3 data conditions, the unestimated exact-LP3 null contract, and the report-side helper's unsupported-scope null result. Numerical verification of Cohn interval values is explicitly deferred.

**Evidence.** `EvaluateQuantileSafe()`, nested quadrature, delta-method variance, and base-10 endpoint conversion remain LP3-specific. A single scope helper now protects both Cohn entry points before those assumptions are used. Exact LP3 remains the sole supported domain.

**Impact.** Unsupported parents and censored/uncertain data now fail explicitly instead of producing misleading intervals or variances.

**Follow-up.** Defer Cohn value/parity verification to a separately approved verification task. Do not extend the method to censoring without a derived and independently verified covariance treatment.

<a id="tr-021"></a>
## TR-021 — Formal Bulletin 17C Examples Are the Current GMM Traceability Source

**Review disposition.** Documentation and source-traceability scope complete.

**Implementation status.** Complete as documentation only. No production or test code was changed for TR-021.

**Verification status.** Passed. `B17CExampleTests.Test_Example1` through `Test_Example7` were executed separately through the exact-method verification runner. All seven passed with zero failures or skips. Each method compares the current specialized LP3 GMM estimates for mean, standard deviation, and skewness with the published Bulletin 17C worked-example values at absolute tolerance `1E-3`.

**Evidence.** The canonical source is `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CExampleTests.cs`; fixtures and published targets are in `Datasets/UnivariateData/Bulletin17CData.cs`. The suite spans systematic records, low outliers, broken records, historical information, crest-stage censoring, combined historical/low-outlier records, and paleoflood information. Each focused run produced exactly one passing TRX after a zero-warning, zero-error Verification-only build. The repository's legacy “Comparison with EMA - Verification Report” concerns the earlier Bayesian workflow and is not the oracle for this current GMM path.

**Impact.** The current specialized GMM point estimates now have executed traceability to all seven formal published worked examples.

**Follow-up.** Retain the seven exact methods as the formal parameter-parity regression. Broader PeakFQ diagnostics, covariance, penalty, coverage, and Cohn-interval verification are separate claims and are not part of TR-021's scope.

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

**Implementation status.** Fixed without adding sampler-specific properties to `MCMCResults`. `MCMCSampler.AcceptanceRates` retains accepted-transition/sample-count semantics. `NUTS.HamiltonianAcceptanceRates` holds the mean post-warmup Hamiltonian statistic, and the existing `MCMCResults.AcceptanceRates` field receives that statistic for NUTS so BestFit can persist and report it. Other NUTS diagnostics remain on the live sampler only.

**Verification status.** Passed by focused Numerics acceptance-contract, live-diagnostic, E-BFMI, nullability, analytic-gradient-routing, and stale-JSON compatibility tests plus exact BestFit acceptance-only report and posterior-gradient methods; see [model-estimation verification](../verification/model-estimation.md#numerics-mcmc-verification).

**Evidence.** Generic sampler acceptance is again calculated as accepted transitions divided by samples. NUTS separately streams post-warmup Hamiltonian acceptance, divergence counts, maximum-depth hits, mean tree depth, mean leapfrog steps, final step size, and E-BFMI without additional target evaluations or retained-draw storage. Only Hamiltonian acceptance crosses the `MCMCResults` boundary through its existing acceptance field. BestFit reports that statistic with NUTS-specific wording and does not persist or display the other live-sampler diagnostics.

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

**Regression control.** A custom-position XML round trip proves deserialization does not recalculate the serialized plotting positions. A special-value fixture proves that replacement with `NaN` or infinity still does not throw. The analytical verification independently reproduces all 39 Weibull nonexceedance probabilities as \(i/(n+1)\). The normalized Debug regression gate records Core 3,116, UI 568, and App 428 passing tests with zero failures; the public API baseline and enforced XML-documentation build also passed.

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
