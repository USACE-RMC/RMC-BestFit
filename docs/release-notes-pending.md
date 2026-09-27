# Pending release notes

Behavior changes made since the public releases RMC.BestFit 2.0.0 and RMC.Numerics 2.1.4 that
must appear in the next release notes. Package versions are not incremented by this list. The
BestFit solution builds against the published RMC.Numerics 2.2.0 package by default; a sibling
Numerics checkout is used only when a build opts in with `-p:UseLocalRmcNumerics=true`.

## RMC.BestFit (since 2.0.0)

- HS plotting positions (issue #19): restores magnitude ordering for explicitly recorded exact,
  uncertain, and interval values below their covering perception thresholds, fixing a regression
  introduced after v2-beta.5. Threshold-only years retain their censored HS weighting, and the
  current recurrence, tie handling, and strict probability bounds are preserved. Valid affected
  input frames are repaired automatically when a project opens and marked dirty only if positions
  change, so normal saving persists the correction. Source data and plot settings are preserved;
  direct `DataFrame(XElement)` construction continues to retain supplied positions exactly.
  Corrected positions can change empirical moments, regression-on-order-statistics initialization,
  and fit diagnostics. Saved analyses are not automatically re-estimated; reprocess affected
  analyses to refresh results that depend on those positions.
- Point process: seasonal Gumbel-limit annualization uses `xi + alpha ln p`; the seasonal
  simulator uses the fitted per-season threshold intensities; clones recompute the event rate;
  seasonal quantile priors are evaluated on the annualized distribution; the seasonal block-day
  list pairs positionally with the exact series in caller order, with block days computed by
  elapsed-day arithmetic on the unshifted dates (the former date-sorted `ShiftDatesByMonth` path
  mis-paired out-of-order records and `DayOfYear` broke across leap-year boundaries);
  `CustomYear` blocks shift like `WaterYear`; seasonal fitting requires dated exact observations
  (the fabricated January-1 index fallback is removed and validation reports the missing dates).
  Seasonal changepoint defaults (TR-005): the broad default supports are `K1` in `[1, 251)` and
  `K2` in `[200, 367)` (formerly `[10, 170]` and `[171, 330]`); with at least ten dated exact
  events, the monthly occurrence histogram, rotated to the block-year start, sets each default to
  a flat five-month window centered on a seasonal valley (within the broad support) unless the
  histogram is effectively flat (Pearson statistic at or below the fixed cutoff 19.675, the 95th
  percentile of a chi-square distribution with 11 degrees of freedom), lacks two separated peaks,
  or is otherwise ambiguous; the likelihood, exposure weights, annual distribution conversion,
  and simulation use the floored changepoint days. Seasonal fits that use the default changepoint
  priors therefore change.
- Composite and coincident frequency: zero inflation is inferred only when the weights sum to
  less than one by more than `1e-10`; the correlation matrix edit is undoable; the posterior
  index cache is thread safe; opening a coincident frequency analysis now syncs the upstream
  marginal posterior chains immediately after linking the upstream bivariate analysis and
  before restoring the saved results, so the first upstream validation notification delivered
  after a project opens no longer clears the just-restored coincident frequency curves.
  Composite and coincident-frequency uncertainty (TR-014) now samples the retained posterior draws
  of each source analysis independently: for each source, a seeded draw without replacement from
  its whole retained chain, using the composite or coincident-frequency analysis's own `PRNGSeed`,
  with as many realizations as the shortest source has retained draws. Separately fitted chains
  are no longer paired by draw index, so the bands follow the product of the source posteriors.
  Composite and coincident-frequency results saved by earlier versions stay readable but must be
  re-run to adopt this sampling.
- Information criteria (TR-011, TR-042, TR-047): the AIC and BIC of the Bayesian univariate,
  mixture, competing-risk, point-process, time-series, rating-curve, and bivariate copula analyses
  and of the `MaximumAPosteriori` estimator (`GetAIC`, `GetBIC`) are evaluated with the data
  log-likelihood at the MAP instead of the full posterior kernel, so prior densities, their
  normalization constants, and Jeffreys or quantile-prior terms no longer shift them. With flat
  priors the values are comparable with maximum-likelihood criteria; with informative priors they
  are not conventional AIC/BIC (DIC, WAIC, or PSIS-LOO apply). Criteria stored with results saved
  by earlier versions keep their values until the analysis is re-run or reprocessed.
- Profiles, PSIS-LOO, and covariance status (TR-023, TR-024, TR-027): the profile likelihoods and
  profile intervals of `MaximumLikelihood` and `MaximumAPosteriori` (`ProfileLikelihood`,
  `ParameterConfidenceIntervals`) re-optimize every free nuisance parameter at each point (against
  the data likelihood for MLE and the posterior kernel for MAP) instead of holding the other
  parameters at the optimum, so intervals of correlated parameters are no longer too narrow (MAP
  profile intervals remain chi-squared cutoffs on the profiled posterior, not credible
  intervals); `ParameterConfidenceIntervals` throws when a bound has no converged nuisance solve.
  PSIS-LOO follows R `loo` 2.10.0 and `posterior` 1.7.0 (a tail fitted to the cutoff excesses with
  the bounded generalized-Pareto fit and shrinkage of `posterior::gpdfit`, monotone expected order
  statistics, reference truncation, and `r_eff = 1`), so LOOIC, `p_loo`, the LOOIC standard error,
  Pareto k, and PSIS influence values change. Covariance failures are reported instead of
  appearing as zero uncertainty: MLE, MAP, and GMM expose `CovarianceStatus`
  (`CovarianceComputationStatus`: `NotComputed`, `Available`, `Regularized`, `Failed`) and
  `CovarianceDiagnostic`; the new `TryGetCovarianceMatrix` (MLE, MAP),
  `TryGetSandwichCovarianceMatrix` (MLE), and `TryGetCovariance` (GMM) return `false` when no
  usable covariance exists; and the existing covariance getters throw `InvalidOperationException`
  instead of returning zero standard errors.
- Estimation and diagnostics: a degenerate PSIS tail reports `k = +inf` and unestimated Pareto
  k counts as unreliable; fewer than eleven retained draws use the fixed 0.7 limit; the data
  frame keeps the recorded POT observation span when the exact series is replaced; GMM
  `PostProcess` keeps a restored J statistic, restored out-of-scope J statistics read as NaN,
  and covariance queries no longer overwrite the weighting state; profile grids report NaN for
  grid points without a finite nuisance optimum; the MLE Hessian uses bounded steps;
  single-parameter covariance is available; `InfluenceDiagnostics.GetProblematicObservations()`
  gains a parameterless overload that uses the instance limit; all-failed distribution fitting
  reports completion; Mixture, CompetingRisk, B17C, Univariate, and PointProcess report NaN
  RMSE when residual degrees of freedom are not positive; the GMM moment covariance that feeds
  the sandwich covariance, the post-estimate weighting matrix, Hansen J, and the influence
  diagnostics is conditioned only by the symmetric positive-definite floor (the former
  50-times-median eigenvalue cap rewrote the moment covariance of real-space three-parameter
  families and distorted every covariance entry; point estimates were never affected).
- Bulletin 17C: bootstrap diagnostics are per requested replicate with a separate realization
  count; the report states the substituted-replicate count and fraction and the point-mass
  consequence; a converged-within-tolerance refit must improve on its start; pivot bound repairs
  and z-limit clips are counted and reported; log-scale penalty centers are perturbed on the log
  scale; `BootstrapDiagnostics` gains `AttemptedRealizations`, `BoundRepairs`,
  `BoundRepairRate`, and `IncrementBoundRepair()`; `ComputeCohnStyleConfidenceIntervals()`
  throws `NotSupportedException` outside its LP3/exact-data scope; AIC/BIC use the data
  likelihood; bootstrap realizations re-centre additive measurement-error distributions by the
  ratio of the simulated flood to the original mean for log-space fitted families
  (Log-Pearson Type III, Log-Normal, Ln-Normal) and for error distributions with strictly
  positive support, so a MOVE.3-style relative error keeps its relative spread and never crosses
  zero (real-space fits with unbounded errors keep the additive shift); when the censored-data
  (ROS) initial-moment estimate is unavailable, `Bulletin17CDistribution` keeps the
  constraint-based initial values and records a validation warning instead of reporting zero
  parameters, and an outright initialization failure is reported as a validation error; a GMM
  covariance failure during MVN/LinkedMVN uncertainty sampling, the effective-record-length
  calculation, or `ComputeCohnStyleConfidenceIntervals()` now degrades gracefully (the existing
  "point estimate is still valid" diagnostic, or a `NaN` effective record length, or a `null`
  Cohn result) instead of throwing and clearing the whole analysis; `IsEstimated` and the point
  estimate are unaffected by a covariance-only failure; estimation reports now label the interval
  width "Confidence Interval" (previously "Credible Interval"); reports saved by earlier versions
  keep their text until the analysis is re-run.
- Rating curve: the data log likelihood is the discharge-space density (the log10-space Gaussian
  term plus the base-10 change-of-variables term per aligned pair) in the scalar, pointwise, and
  component paths, so AIC/BIC/DIC/WAIC/LOOIC shift by the data constant `-sum log(Q ln 10)` and
  persisted rating-curve criteria differ on reprocess (parameter estimates are unchanged); the
  default exponent lower bound and prior minimum are 0.1 instead of 0 (legacy projects keep their
  stored bounds and receive a validation warning); validation rejects only nonpositive date-aligned
  discharge and reports unmatched stage/discharge records as a warning with counts.
- Mixture: MCMC samples the identified `K-1` weight coordinates; AIC/BIC count `K-1` weights;
  legacy full-`K` posteriors still open.
- Spatial GEV: rows with missing sites are marginalized through the observed-site Gaussian-copula
  submatrix (`GaussianCopula.LogPDF(z, observedSites)`; a zero placeholder score is no longer
  substituted), so posteriors of copula models fitted to networks with missing data change; the
  Gaussian-process densities of the latent location/scale/shape errors moved from
  `DataLogLikelihood` to a `PriorLogLikelihood` override (posterior kernel, sampler, MAP, and
  posterior unchanged; AIC/BIC/DIC/WAIC/LOOIC of latent-error models now exclude the process
  densities; the scalar/pointwise identities hold); `ComputeGodambeCovariance` derives both sandwich
  factors from the row/year estimating equations, returns `null` with
  `GodambeCovarianceStatus = Failed` and a `GodambeCovarianceDiagnostic` instead of the variability
  matrix when the sensitivity matrix is singular or a value is not finite, validates the parameter
  count, and is reset by `ClearResults`; spatial AIC/BIC keep the nonempty row/year unit
  (`SpatialGEVAnalysis.ComputeInformationCriteria`); `SpatialGEV.Clone()` now carries the copula and
  latent-error parameter blocks and the source values, bounds, and priors (previously the clone held
  only the trend blocks with reset intercepts, so copula and latent-error Bayesian analyses failed
  while building site results); leave-one-site-out cross-validation fits a reduced training model
  per fold (the held-out site's data, coordinates, covariate rows, copula coordinate, and latent
  error removed) with a fold analysis carrying the main settings and seed, predicts the held-out
  site with its own covariate rows, never refits or mutates the main analysis (results are
  retained), and reports `FoldStatus`, `FoldMessages`, `SuccessfulFolds`, and `TotalFolds` with NaN
  metrics for unscored folds, aggregates over successful folds, and an `InvalidOperationException`
  when no fold succeeds; `GeneralLinearFunction.PredictWithCovariates(null or empty)` throws for a
  trend that has covariates (intercept-only trends still accept null), so
  `PredictAtUngaugedLocation` and `SpatialGEV.PredictAtUngauged` require covariate values for
  covariate models; ungauged-site predictions apply the conditional Gaussian process of every
  posterior draw with a seeded conditional residual (`SampleConditionalResidual`, default true;
  false gives the conditional mean) instead of inverse-distance interpolation; regional credible
  bounds are posterior quantiles of the per-draw regional mean quantile instead of averages of site
  interval endpoints; `GenerateRandomValues` simulates spatially dependent rows through the fitted
  copula (independent sites without it); `RunSpatialBootstrapAsync` runs a temporal block bootstrap
  (rows resampled in blocks, all sites kept, MAP refit per replicate, NaN failures, at least half of
  the replicates required, `BootstrapResults` accounting; `blockSize` now counts rows); `RunAsync`
  applies the selected `UncertaintyMethod` (posterior, sqrt-VIF inflation, Gaussian parameter draws
  from the Godambe covariance at the MAP, or the bootstrap with
  `BootstrapReplicates`/`BootstrapBlockSize`) and records `AppliedUncertaintyMethod` and
  `SpatialGEVSiteResults.UncertaintyMethod`; `UncertaintyMethod`, `SampleConditionalResidual`,
  `BootstrapReplicates`, and `BootstrapBlockSize` are serialized as optional attributes; a
  non-finite site GEV parameter gives negative-infinite likelihood instead of throwing inside the
  sampler; default latent-error bounds under a log link use the log-space spread (floor 1.0 log
  unit), so `ConfigureForProperCoverage` models sample under the defaults;
  `SpatialGEV.DistanceMetric` (`SpatialDistanceMetric.Cartesian` default, bitwise the former planar
  distances; `Geodesic` for latitude/longitude in decimal degrees with great-circle kilometres) with
  new `GaussianCopula` and `SpatialRegressionErrors` constructor overloads, coordinate validation,
  and an optional serialized attribute; `ComputeEffectiveSampleSizeWeights` is obsolete in favor of
  `ComputeCorrelationHeuristicSiteWeights` (same numbers; the weights are a correlation heuristic on
  the marginal terms, not a composite likelihood).
- Data frame: the low-outlier setters (`SetLowOutliersFromMGBT`, `SetLowOutliersFromThreshold`)
  recompute the Hirsch-Stedinger plotting positions before raising `LowOutliers`, so headless and
  GUI callers alike fit from current positions instead of the TR-087 constraint-initials fallback;
  the setters restore the notification-suppression flag in a finally block (a throwing test can no
  longer strand the frame silently un-refreshing); `ClearLowOutliers` saves and restores the
  caller's suppression state and, when unsuppressed, refreshes positions and raises a single
  `LowOutliers` change instead of per-item notifications; `LinearTrendTest` delegates to the
  Numerics `HypothesisTests.LinearTrendTest` (identical math, dead local removed);
  `SetStandardizedValues` computes its standardization moments with `CentralMoments(1000)`,
  matching the summary statistics (formerly 200 steps, so the Q-Q reference normal used a coarser
  quadrature than the reported moments).
- Estimation: GMM's iterative method gains an OR-ed scale-relative parameter-change convergence
  test (largest |Δθ|/max(1, |θ|) below the relative tolerance), so the pass count for well-fitted
  models — where the near-zero objective disables the relative-objective test — no longer depends
  on last-bit optimizer noise in a non-scale-aware absolute distance; convergence can only trigger
  earlier. DIC and WAIC accumulate per-index terms and sum sequentially (matching PSIS-LOO), so
  the reported criteria are bit-reproducible run to run (last-bits-only change).
- Sub-unity datasets (issue #15): LogNormal and Log-Pearson Type III accept records whose values
  are mostly below 1 (negative log10 mean) — the Numerics constraint fix below plus a defensive
  midpoint clamp of out-of-bounds default initials in `UnivariateDistribution.SetDefaultParameters`
  (matching the Bulletin 17C initializer). LnNormal, parameterized by the real-space mean, was
  never affected.
- Mixture analysis (issues #16, #17): deleting a distribution row no longer crashes the
  application (the grid delete is cancelled and performed from a clean dispatcher stack, and the
  re-bind suppresses combo-box write-backs); changing the point estimator updates the summary —
  the K-1 weight expansion clamps a ULP-scale negative residual at the simplex boundary instead of
  throwing, and a failed reprocess now raises the conventional `AnalysisResults` notification
  (alongside clearing `IsEstimated`) so every analysis view rebuilds and resets its wait cursor
  instead of freezing on stale output.
- Low outlier test (issue #13): a threshold rejected by the 50-percent-censoring guard shows the
  validation message instead of terminating the application, and a legacy project whose stored
  low-outlier settings the current guards reject opens with the outliers cleared instead of
  crashing on load.
- Version 1.0 project open: opening a version 1.0 project whose saved low-outlier
  settings the current guards reject (for example a threshold that censors more than half the
  record) now shows a warning that its low outliers were cleared, instead of clearing them
  silently.
- Input data POT diagnostics (issue #14): the mean-residual-life and parameter-stability plots
  operate on the same smoothed series the peaks-over-threshold extraction thresholds (via the new
  `TimeSeries.SmoothedSeries`), and editing the smoothing function, period, minimum steps between
  peaks, or the source time-series element marks the diagnostics dirty.
- Input data POT diagnostics: a smoothing period outside `1 <= period < series length` for any
  smoothing function other than `None` (new `InputData.IsSmoothingPeriodValid`) is now a validation
  message instead of an unhandled exception from `TimeSeries.MovingAverage`/`MovingSum`/
  `Difference` — opening the Threshold Diagnostics tab with such a period no longer closes the
  application, and the three diagnostic plots clear instead of showing a stale curve. With the
  smoothing function `None`, any smoothing period is accepted (2.0.0 rejected a period below 1 or
  longer than the series whatever the smoothing function).
- Input data POT exposure: `DataFrame.CreateBlockSeries` now clears any
  `PointProcessObservationYears` retained from an earlier peaks-over-threshold extraction, and
  `InputData.ExactDataMethod` clears it as soon as the method changes away from
  `PeaksOverThresholdSeries` (Manual, Block Series, and USGS entry never populate it). Previously
  the recorded POT source-observation span survived a source change and could be silently reused
  as the exposure for an unrelated point-process fit. Replacing or editing the POT-derived exact
  series while the method stays peaks-over-threshold is unchanged and still keeps the recorded
  span. Returning to peaks-over-threshold with the extracted series unchanged — by re-selecting the
  method with no intervening edit, or by an Undo/Redo of the method change — restores the exposure
  that was cleared on the way out; if the series changed (edited in place, or re-derived by another
  method such as Block Series) while a different method was selected, the exposure stays cleared
  until the next POT extraction.
- HEC-DSS import: a regular time series whose storage blocks are partly missing now imports
  completely. The reader resolves every catalog record that matches the path's A, B, C, E, and F
  parts, reads each calendar block separately, keeps gaps as missing values, and rejects
  malformed or conflicting partial results; weekly records keep their stored seven-day phase. The
  former single whole-record read did not handle such sparse records and could import an
  incomplete series. The DSS path selector shows the date range of the same complete read.
- Nonstationary trend models: a failed default-parameter build in
  `UnivariateDistribution.SetTrendModel` (for example, too few observations, a constant sample, or
  non-finite values reaching the parent distribution's automatic constraint estimator) still
  throws `InvalidOperationException`, but now leaves the previous trend model in place and
  re-attaches the parameter change handlers removed at entry instead of leaving the distribution
  permanently unresponsive to later parameter edits. The App's trend-model combo box catches the
  failure, reverts the row to the distribution's actual trend model, and shows a warning dialog
  instead of crashing the application.
- API/MCP input data (approved 25 September 2026): manual input creation
  now applies a supplied `lowOutlierThreshold` with `DataFrame.SetLowOutliersFromThreshold()`
  after the exact series is populated, instead of only storing it. Every exact observation
  strictly below the threshold is now flagged a low outlier and counted in the response's
  `lowOutlierCount`, regardless of any `isLowOutlier` sent on it. **Callers that previously sent
  `lowOutlierThreshold` on a manual request relied on it being stored but not applied; those
  requests now censor observations below the threshold and may 400 if the threshold would censor
  more than half the record or fewer than ten exact observations are present.** Sending
  `isLowOutlier:true` on an observation whose value is at or above `lowOutlierThreshold` is a new
  400 (the threshold would unflag it); a preflagged observation already below the threshold is
  unaffected. Omitting `lowOutlierThreshold` when no observation is preflagged is unchanged.
  `useMultipleGrubbsBeckTest` (new, below) rejects a request that also supplies
  `lowOutlierThreshold` or a preflagged observation. The bundled `bestfit-frequency` skill follows
  suit: its runner no longer stops a manual request that sends a threshold without per-observation
  flags, and its workflow notes describe the applied threshold.
- API/MCP input data (approved 26 September 2026): manual input creation now rejects a request
  that preflags any exact observation `isLowOutlier:true` without also supplying
  `lowOutlierThreshold` (and without `useMultipleGrubbsBeckTest`, already rejected together with a
  preflag by the rule above) with a 400: "Exact observations flagged isLowOutlier=true require
  lowOutlierThreshold, which defines their censoring threshold; supply lowOutlierThreshold or
  remove the flags (useMultipleGrubbsBeckTest derives the flags itself)." Previously such a
  request stored the flags with no censoring threshold and `NumberOfLowOutliers` left at zero;
  Bulletin 17C's `MomentConditions` then dropped the flagged values from its likelihood while its
  counter-gated analytical-Jacobian guard and univariate filtering both still treated them as
  ordinary exact data, so the same flags produced disagreeing fits across analysis kinds. Supply
  `lowOutlierThreshold`, or use `useMultipleGrubbsBeckTest` to derive the flags instead, whenever
  any observation is preflagged; a request that already supplies one of those, or that preflags
  nothing, is unaffected.
- `bestfit-frequency` skill (26 September 2026): the study runner never lets a low-outlier
  threshold transferred from a screened systematic cohort censor exact observations outside that
  cohort. The API applies a manual threshold to every exact row, so a candidate whose historical
  or paleo exact rows lie below the transferred threshold stops with a message naming them,
  unless the study input flags them `isLowOutlier:true` (the analyst's decision to censor them).
  The runner also stops, before creating any input, the requests the API rejects:
  `isLowOutlier:true` flags without `lowOutlierThreshold`, and automatic screening (`--mgbt on`
  or `useMultipleGrubbsBeckTest:true`) combined with a manual threshold or flags.
- API/MCP additions (see `docs/api.md`): `GET api/analyses/{analysisId}/plot-source` (MCP
  `get_analysis_plot_source`) exports one completed run of any analysis kind for external
  plotting without running or changing the analysis (settings and model XML, the results payload,
  observations, stored parameter diagnostics, and, with `includeSamples=true`, the saved draws);
  `GET api/inputdata/{id}/chronology` (MCP `get_inputdata_chronology`) returns an input resource's
  observations and inclusive threshold windows before fitting; and `GET api/inputdata/{id}/source`
  (MCP `get_inputdata_source`) returns the original creation request, its capture time, any raw
  USGS download text, and a SHA-256 of that text. Univariate requests accept
  `useJeffreysRuleForScale` and reject a quantile-prior count the model cannot apply (one with
  `useSingleQuantile=true`, otherwise one per distribution parameter); univariate and Bulletin 17C
  resources report their effective `configuration`; manual input, USGS-peak input, and the USGS
  Bulletin 17C workflow accept `useMultipleGrubbsBeckTest` (Multiple Grubbs-Beck low-outlier
  screening, default off); frequency results add `quantileAnnotations`, and uncertain
  observations report `lowerBound`/`upperBound`.
- Distribution and mixture-EM robustness (TR-095, approved 8 September 2026): quantile priors in
  `UnivariateDistribution` and `PointProcessModel` use an additive log-quantile-Jacobian so a
  finite logarithmic determinant survives raw-determinant overflow/underflow instead of failing
  (exact singularity still returns negative infinity); Mixture EM evaluates exact, censored,
  interval, positive-conditional, and measurement-error observations logarithmically through
  responsibility normalization, so an extremely large common log density no longer distorts
  component weights, and zero-weight components are skipped before singular-density or
  effective-support checks; automatic parameter initialization now reports an unusable sample
  through model validation instead of failing silently, and a later valid sample clears the
  diagnostic.
- Bulletin 17C BFGS convergence (TR-096; implemented 17 September 2026, commit `4732d5c`): with
  the RMC.Numerics BFGS repair below, BFGS checks the infinity norm of the projected gradient at
  initialization and after every accepted step, so a small objective change or an exhausted
  parameter step no longer registers as successful convergence, and genuine line-search
  exhaustion is reported as `LineSearchFailed` rather than concealed; the GMM estimator treats a
  BFGS pass that ends in `LineSearchFailed` as failed and finishes it with its Nelder-Mead
  fallback, as for any failed BFGS pass (commit `d29f87a`); `Bulletin17CDistribution` now supplies
  an analytical Pearson III/Log-Pearson III systematic-data Jacobian instead of numerical
  differentiation (mixed/censored data and other families are unaffected). This reduces
  outer-GMM-pass counts and false-convergence reports in Bulletin 17C bootstrap fitting but does
  not eliminate every optimizer failure: some bootstrap realizations can still reach the 100-pass
  ceiling or produce an indefinite weighting matrix.
- Competing-risk Bayesian MCMC initialization (TR-097; implemented 4 August 2026, commit
  `c28228d`): competing-risk analyses that start Bayesian MCMC from the MAP now use a MAP-centered
  initialization covariance, falling back to a regularized Moore-Penrose pseudo-inverse when the
  posterior information matrix is singular so null-space directions are anchored at the MAP instead
  of given unbounded variance; this does not change the sampled posterior, priors, or convergence
  criteria.
- Nonstationary trend defaults (TR-098; implemented 30 August 2026, commit `0a2703a`): reciprocal
  temporal trends now initialize their coefficient in response space (`a = 1 / responseInitial`)
  instead of copying the stationary response initializer directly into `a`, and their default
  priors follow from it: a finite same-sign one-decade response interval, transformed to `a`, sets
  the `a` prior, and the endpoint response changes over the record set the `b` prior. Sinusoidal
  amplitude now uses the smaller distance from the stationary initializer to either
  parent-parameter bound, keeping the whole default trajectory valid. This fixes reciprocal
  default starting points and priors that were previously orders of magnitude away from a
  workable scale for some parent distributions.
- Mixture analysis parameter-sets table: the App's parameter-sets table now
  uses the core's K-1/full-K expansion (`MixtureModel.TryGetPhysicalParameters`, newly public)
  instead of re-deriving the arithmetic; the API's results mapper shares only the new public
  `MixtureModel.IsSampledWeightVectorLength(int)` shape test it already used to decide which
  parameter name to omit. The table no longer crashes the application when a stored draw's
  derived final weight is a rounding-level negative — it now shows zero, as the fit itself does —
  and shows an empty table with a message when a stored draw cannot be displayed for any reason
  (an infeasible derived weight, invalid component parameters, or a length that matches neither
  the K-1 nor the full-K shape).
- Spatial GEV validation: Spatial GEV analysis validation now rejects
  the Godambe sandwich uncertainty method combined with spatial regression errors, a combination
  that always failed after the MCMC run.
- NuGet package: `RMC.BestFit` now ships its XML documentation file (`lib/net10.0/RMC.BestFit.xml`),
  so IntelliSense shows the library's summaries, parameter descriptions, and remarks (the 2.0.0
  package shipped without it).

### Time series (AR, MA, ARIMA, ARIMAX)

**Action required: re-run affected time-series analyses.** When a project opens, RMC-BestFit warns
about each time-series analysis whose saved results predate v2.0.1 and use a configuration this
release handles differently (a covariate, a fitted Box-Cox or Yeo-Johnson exponent, differencing,
or, without covariates, a covariate lag order above `max(p, q)`; see the entry on results saved
before v2.0.1), and about each one whose saved parameters no longer fit its covariates; re-run the
Bayesian analysis of every analysis that shows either warning. Before re-running an ARIMAX analysis
with covariates that was reopened, edited, and saved in 2.0.0, check its priors: they may already
be the defaults (see the covariate entry below). Other time-series analyses need a re-run only to
refresh their saved information criteria; a reprocess refreshes just their AIC and BIC (see the
information-criteria and PSIS-LOO entries above).

- Time series: ARIMAX conditions the likelihood, residuals, transform Jacobian, generation, and
  prediction on one conditioning order (the rule is the ARIMAX conditioning entry below); an empty
  conditional sum is an invalid fit (negative-infinite likelihood) rather than a zero
  log-likelihood; ARIMA/AR order setters rebuild the training state; the pointwise transform
  Jacobian is per observation; the transform reset of custom priors is gated on
  `UseDefaultFlatPriors`; transform failures are validation messages. `ARIMAX.Validate` requires
  exact-date covariate matching in the training window (legacy projects with positionally aligned
  but differently dated covariates fail validation). Covariate dates needed only in the holdout or
  forecast window are not validated (with the covariate extension `None`, validation checks only
  that each covariate has at least as many values as the response plus the forecast steps): a
  covariate that is missing or duplicates such a date passes validation, and the run then fails
  after sampling, when the predictions are built, with a "missing required timestamp" or
  "duplicate required timestamp" error. `SetTransformParameters` throws on a non-finite first
  parameter and ignores the second; `GenerateRandomValues` returns raw-scale values of length
  `sampleSize`; `ARIMAX.TrainingTimeSeries` is the differenced series.
- Transform exponent, differencing, and simulation (TR-036, TR-037, TR-038, TR-039, TR-041): the
  Box-Cox and Yeo-Johnson exponent is fitted on the raw training window only (formerly on the
  whole series, holdout included) and then applied to the full response; the new read-only
  `TransformLambda` reports the effective exponent, which is saved with the model (a fitted
  exponent is refitted after a data or training-window change; a manually set exponent stays
  fixed when the training window changes); the REST time-series request accepts an optional
  manual `transformLambda`, and time-series results report the effective exponent. Differenced
  ARIMAX models train on the `T - d` differences inside a `T`-observation training window
  (formerly the first `T` differences, which reached into the holdout), keep each difference at
  its later raw time stamp, and select level covariates by that time stamp without differencing
  them. ARIMA and ARIMAX fitted values and forecasts are reintegrated from the observed state at
  the preceding step (formerly off by one step), so fitted values no longer accumulate
  innovations from the start of the record and forecasts start from the final observed training
  state. AR, MA, ARIMA, and ARIMAX simulation completes its recursion on the transformed,
  differenced model scale and inverse-transforms once (formerly ARIMA ignored differencing and
  transforms, and ARIMAX mixed transformed and raw scales).
- ARIMAX structural edits and undo: the structural setters (the AR, differencing, MA, and
  covariate-lag orders, intercept, seasonality, trend, training window, and default-window rule)
  and turning `UseDefaultFlatPriors` on now rebuild the default parameters before they notify, so
  undoing or redoing a structural edit in a time-series analysis restores the priors and bounds
  recorded with that step, custom or default, which match the restored structure; turning
  default flat priors on or off is an undoable step (undoing it restores the priors it replaced).
- ARIMAX covariate validation: when a covariate is missing a date the response needs, `Validate()`
  now also adds a hint that covariates are paired by date (RMC-BestFit 2.0.0 paired them by
  position).
- Time-series analyses with covariates (ARIMAX; present since 2.0.0): opening a project, copying
  the analysis, and undoing or redoing a model-property edit keep the saved coefficient values,
  bounds, and custom priors. The covariates were reattached through a path that rebuilt the
  default parameters, so a reopened analysis showed residual diagnostics at default coefficients
  (residual RMS 0.660229 instead of the fitted 0.341147 in the time-series regression example),
  and a later save of an edited analysis wrote those defaults to the project. An ARIMAX analysis
  with covariates that was reopened, edited, and saved in 2.0.0 may therefore already store the
  default priors (see the action above). Metadata edits on a covariate series (its name,
  description, or unit label, or saving it) no longer rebuild the parameters or clear the
  results, and reselecting the same covariate series is not a change. Adding or removing a
  covariate, pointing a covariate row at another series, or replacing a covariate's series (for
  example by downloading it again) still rebuilds the defaults; editing covariate values in place
  rebuilds them only when default flat priors are on and clears the results either way. Undo
  and redo no longer apply a replaced covariate's coefficient, bounds, or prior to the covariate
  that replaced it, and redoing a structural edit that changes the number of parameters (for
  example the AR order) restores a vector that fits the model. Copying an analysis also keeps its
  manual training window and covariate-extension method. API: the new
  overload `ARIMAX.SetCovariates(List<TimeSeries>, bool resetParameters)` keeps a parameter list
  whose layout still fits the covariates (the one-argument overload still rebuilds the
  defaults), and `ARIMAX.Clone()` keeps the source's parameter values, bounds, and priors, so the
  REST plot-source leverage and leave-one-out diagnostics of ARIMAX models with covariates and
  non-uniform priors now use the fitted priors, as the desktop does.
- ARIMAX conditioning (TR-066; with-covariates rule approved 25 September 2026, review decision D6;
  without-covariates rule confirmed 26 September 2026, ruling R2): a model with covariates now
  conditions on `max(q, p + b)` leading model steps instead of `max(p, q, b)`, so every evaluated
  step's own mean and every autoregressive-lag mean include all `b` lagged covariate values (the
  first evaluated steps of a model with `p > 0` and `b > 0` formerly used AR-lag means that
  omitted the covariate lags before the first observation). A model without covariates conditions
  on `max(p, q)`: the covariate lag order has no role without covariates. ARIMAX analyses with
  covariates and `p > 0`, `b > 0` (where `p + b > q`) now condition on more leading steps, and
  analyses without covariates whose lag order exceeds `max(p, q)` on fewer; their likelihood,
  pointwise terms, information criteria, residual diagnostics, in-sample predictions, and fits
  change. This rule leaves the conditioning order of models with `b = 0`, including every shipped
  example, unchanged; the other changes in this section can still affect their saved results,
  which the warning on results saved before v2.0.1 reports. A training window must provide more
  differenced steps than this order, and the validation message names it. Changing the covariate
  lag order, or attaching the first covariate or removing the last one, now also moves the
  Box-Cox, Yeo-Johnson, or logarithmic transform Jacobian window to the new order, keeping the
  transform exponent; previously a lag-order edit left the window at its old start until the
  training data was next rebuilt, which offset the reported log-likelihood and criteria of the
  edited analysis (the fitted parameters were unaffected because the offset is constant).
- Results saved before v2.0.1 (approved 25 September 2026): opening a time-series analysis
  whose saved results were computed before v2.0.1 now adds a validation warning when the restored
  model has a covariate, uses a fitted Box-Cox or Yeo-Johnson transform, is differenced
  (`DiffOrderD > 0`), or (covariate-free) has `XOrderB > max(AROrderP, MAOrderQ)` — the
  configurations changed by v2.0.1's training-window transform fit (TR-036), date-based covariate
  alignment, corrected training and reintegration windows for differenced models (TR-041,
  TR-037), and revised conditioning window (see the ARIMAX conditioning entry above). Detection
  reads the saved model's `TransformLambda` attribute, which is present only in saves made by
  v2.0.1 or later (earlier saves never wrote it). Because every save now writes that attribute, a
  save made while the warning stands also stores a `PreV201Results` marker with the analysis, so
  saving without re-running keeps the warning on the next open; projects without the marker are
  checked by the attribute alone. The warning asks the user to re-run the Bayesian analysis, and it
  clears, together with the marker, as soon as the results are cleared (including by an undo or
  redo that rebuilds the model without them) or the analysis is re-run; it does not change any
  algorithm, default, or numerical result.
- Saved parameters that no longer fit: opening a time-series analysis whose saved parameters no
  longer match its covariates (for example, a missing covariate series) now warns and opens without
  the saved results, instead of silently restoring default parameters next to results of the wrong
  size (which could make a later reprocess fail); the other saved settings are still restored, and
  the warning clears after a successful re-run. Saving that analysis after the warning writes the
  default parameters and no results and drops the missing covariate's name; to recover, restore
  the covariate series under its original name and reopen the project before editing, re-running,
  or saving the analysis (a plain project save leaves the unchanged row untouched).
- Forecast steps and random series: every public `Predict` overload of `AutoRegressive`,
  `MovingAverage`, `ARIMA`, and `ARIMAX` now rejects a negative `forecastSteps` with
  `ArgumentOutOfRangeException` (previously a differenced model, d > 0, could throw an internal
  reconstruction `ArgumentOutOfRangeException` naming the wrong parameter, or an undifferenced
  model would silently accept it). `GenerateRandomSeries` on the same four classes again accepts a
  requested length shorter than the training window for every differencing order (previously this
  threw for d > 0), and now also rejects a `timeSteps` less than one with
  `ArgumentOutOfRangeException` (previously a negative `timeSteps` threw `OverflowException` from
  a negative array size for every class, and `timeSteps = 0` did too for a differenced model).
  `ARAnalysis`/`MAAnalysis`/`ARIMAAnalysis`/`ARIMAXAnalysis` internally derive their own
  forecast-step count and are updated to tolerate the same negative value, but that value is only
  negative when the model already fails validation (`TrainingTimeSteps` greater than the response
  length); `RunAsync` refuses to run such a model, so this path is not reachable from a normally
  validated project.
- Default training window (approved 26 September 2026): with Use Default Training Steps
  (`UseDefaultTrainingSteps`) on, AR, MA, ARIMA, and ARIMAX models now train on 80% of the series
  but on at least `d + K + k + 10` steps, where `d` is the differencing order, `K` the
  conditioning order (AR `p`; MA 0; ARIMA `max(p, q)`; ARIMAX `max(p, q)`, or `max(q, p + b)` with
  covariates), and `k` the number of estimated parameters, so every default fit keeps at least ten
  residual degrees of freedom. The former window, the largest of 30 steps, the number of
  parameters, and 80% of the series, made every series of 10 to 29 values fail validation under
  the default settings; now, for example, AR(1) with an intercept validates from 14 values, MA(1)
  from 13, and ARIMA(1,1,1) with an intercept from 16. A series shorter than the minimum is too
  short for the model, and validation names the minimum. New analyses of a 30- to 37-value series
  now train on 80% of it (24 to 29 steps) instead of 30 when the model's minimum is no larger, as
  for low-order models. The default window is also recomputed when the orders, intercept, trend,
  seasonality, or covariates change (formerly only when the series changed). Opening a project,
  copying an analysis, and undoing or redoing a model-property edit keep the saved window;
  undoing a covariate or response-series change recomputes it, as the change itself did. A
  default window saved by 2.0.0 that is longer than its series (any series under 30 values)
  still fails validation until Use Default Training Steps is turned off and on again. In the
  desktop application, the Training Steps box no longer caps a default window at the series
  length: in 2.0.0, showing such an analysis's properties silently switched it to a manual
  window of the whole series. Manual windows keep their existing checks. Library callers: assigning `TrainingTimeSteps` does not turn `UseDefaultTrainingSteps`
  off, so a window assigned while the default rule is on is now replaced at the next structural
  change as well as at the next series change; turn the default rule off after attaching the
  series (attaching a series turns it back on) to keep a manual window, as the desktop
  application and the REST API do.

## RMC.Numerics (since 2.1.4)

- `CompetingRisks.CreateEmpiricalCDF` stratifies on log-spaced bins of the offset axis for any
  `XTransform`, so heavy-tailed components keep quantile resolution.
- `MultivariateNormal` seeds its default generator (`MersenneTwister(12345)`); `CompetingRisks`
  exposes and serializes `PRNGSeed`.
- `FrankCopula.InverseConditionalCDF` uses the reflected draw for positive dependence.
- `BootstrapAnalysis.BCaQuantileCI` uses the `count / (B + 1)` bias-correction proportion, and
  the ensemble methods throw when every replicate fails; `Quantiles`/`Probabilities` gain an
  optional `IUnivariateDistribution[]` parameter (binary-breaking for assemblies compiled
  against 2.1.4).
- `UnivariateDistributionFactory.CreateDistribution(XElement)`, `CompetingRisks.FromXElement`,
  `Mixture.FromXElement`, and the empirical/kernel-density readers reject missing or invalid
  attributes instead of loading degraded distributions.
- Rank-normalized split R-hat and bulk/tail ESS with the 1.01 threshold; ARWMH realized-state
  covariance; NUTS acceptance diagnostics; `Probability.NegativeJointProbability` uses the
  Frechet-Hoeffding bound; `KappaFour` zero-shape density and quantile; `GoodnessOfFit.RMSE`
  uses every residual and rejects non-positive residual degrees of freedom; positive-hurdle
  mixture law; dependent competing-risk simulation; `LogNormal.Clone` base.
- `Statistics.RanksInPlace(data, out ties)` records a tie run that reaches the final sorted
  element (formerly its length was silently dropped); `Statistics.ParallelMean` uses a
  fixed-chunk parallel reduction merged serially in chunk order (matching the bootstrap's
  jackknife accumulation), so it is bit-reproducible across machines and core counts — the PLINQ
  partition order was not — with small samples falling through to the sequential mean.
- `UncertainOrdinate.operator==` compares X with the same machine-epsilon tolerance (and NaN
  convention) as `Ordinate`; the mean-vs-median central-probe asymmetry between `OrdinateValid`
  and `OrdinateErrors` is documented as deliberate (the median is always bracketed by the
  percentile probes; the mean of a skewed distribution need not be).
- RWMH factorizes its fixed proposal covariance once per chain and translates only the mean each
  transition via the new `MultivariateNormal.SetMean` (bit-identical draws, removes an O(D³)
  Cholesky per iteration; an invalid proposal covariance now throws at initialization rather than
  from the first chain iteration); SNIS resamples through a stable sort, so tied fitness draws
  (common -Infinity values under wide priors) keep their draw order and seeded output is
  reproducible across runs and platforms.
- `GaussianMixtureModel` applies the M-step's symmetric positive-definite repair to the stored
  covariances (the pure helper's return value was formerly discarded, leaving only the diagonal
  floor; fitted covariances gain the trace-scaled base ridge of about 1E-10); `DecisionTree`
  regression stops at pure nodes (distinct-response count for both modes, scikit-learn's rule —
  formerly a default regression tree split zero-gain pure nodes down to one observation per
  leaf).
- BFGS reports success only when the infinity norm of the projected gradient is at or below the
  absolute tolerance, tested at the start point and after every accepted step (a start that
  already satisfies it returns immediately), so a small objective change or an exhausted
  parameter step no longer counts as convergence. The strong-Wolfe line search is safeguarded
  (feasible step limit, safeguarded cubic/quadratic interpolation, gradient checks at
  rounded-equal objective values); after an exhausted search BFGS retries once with the identity
  metric, and a search that still finds no acceptable step ends with the new
  `OptimizationStatus.LineSearchFailed` instead of continuing. `Optimizer` computes its
  end-of-run Hessian for a `LineSearchFailed` result as for `Success`. BestFit uses BFGS for
  Bulletin 17C GMM fits and for the nuisance re-optimization of MLE and MAP profile likelihoods,
  so those results can change.
- Differential Evolution repairs an infeasible trial coordinate halfway between the target
  member's coordinate and the violated bound instead of clamping it to the bound (approved
  change, commit `2b57771`; convergence tolerances are unchanged), so seeded Differential
  Evolution runs follow different trajectories, and the MLE and MAP estimates (including
  distribution fitting) that BestFit computes with its default Differential Evolution optimizer
  can differ from those computed with 2.1.4.
- The interpolation correlated-search windows scale as Count^0.25 (`Interpolater.deltaStart` was
  pinned to 1 by a Math.Min typo; `OrderedPairedData`'s X/Y windows were never assigned), so the
  hunt search path is reachable; brackets are unchanged.
- `LogNormal` and `LogPearsonTypeIII` parameter constraints allow a negative log-space mean (the
  location bounds are symmetric about zero like Normal's; the former machine-epsilon floor
  rejected any sub-unity sample), and their `MinimumOfParameters` report negative infinity for
  the location. `TimeSeries.SmoothedSeries` exposes the exact smoothing preprocessing
  `PeaksOverThresholdSeries` applies, for threshold-selection diagnostics.
- `Statistics.ProductMoments` accumulates its power sums about a shifted origin (first
  observation), so moment-seeded parameter estimates and the data-frame summary statistics shift
  in the last bits (better conditioned, same algebra); `HypothesisTests` Mann-Kendall and
  Mann-Whitney variance corrections sum tie groups by their full size, so the displayed
  homogeneity/stationarity p-values change on any record with ties; `Gamma.Incomplete` runs its
  continued fraction to the convergence test (the former single-convergent break returned values
  wrong by up to ~0.4 in probability near `X = alpha` at large shape; no BestFit path calls it).
- `TimeSeries.CumulativeSum` preserves the source series' time interval (formerly the result
  claimed a daily interval regardless of the source); the indexed `LogTransform`, `Inverse`, and
  `InterpolateMissingData` overloads skip out-of-range indexes like their seven siblings instead
  of throwing from the indexer, and the indexed interpolation carries its twin's series-start
  extrapolation guard.

## Known issues and limitations

- Desktop updater: when an update is available, RMC-BestFit 2.0.0 and 2.0.1 show the "Update
  Available" prompt twice at startup. Answering Yes to the first prompt starts the Tools-menu
  update check, which asks again; answer Yes to the second prompt as well to download and install
  the update. The single-prompt fix needs RMC.Wpf.Framework 1.0.5, which is not yet published;
  this release stays on Framework 1.0.4.
- Sharing projects with RMC-BestFit 2.0.0: version 2.0.0 opens projects saved by 2.0.1, but teams
  that share projects should upgrade together. A mixture analysis estimated in 2.0.1 stores its
  weight posterior with one fewer free weight than components (the shipped mixture example
  already does); 2.0.0 shows its saved results but cannot recompute them, so changing that
  analysis's credible-interval width, point estimator, or probability ordinates in 2.0.0 clears
  its results. Version 2.0.0 also reopens time-series analyses with covariates at default
  coefficients, the defect 2.0.1 fixes (see the time-series section).
- Spatial GEV (library): after `RunAsync` with an uncertainty method other than the Bayesian
  posterior, a reprocess (for example after the probability ordinates change) rebuilds the
  results from the posterior without re-applying the selected method; re-run the analysis after
  such a change. `RunSpatialBootstrapAsync` and `RunCrossValidationAsync` cannot be cancelled, and
  cross-validation is not serialized with `RunAsync`; do not start either while `RunAsync` is
  running. The `SpatialGEV` XML constructor restores the saved parameter values by position into
  a parameter list built before the copula setting is applied, so the saved values of a model
  with copula dependence are misaligned. (The Godambe sandwich method with spatial regression
  errors is now rejected by validation; see the Spatial GEV validation entry above.)
- GHCN snowfall downloads (RMC.Numerics downloader): daily snowfall values are ten times too
  small. NOAA reports SNOW in whole millimetres, but the downloader converts it as it converts
  PRCP, which NOAA reports in tenths of a millimetre (inches are the NOAA value divided by 254
  instead of 25.4). Multiply downloaded snowfall values by ten; precipitation downloads are
  unaffected. The shipped Paradise snowfall example keeps its original values (38 nonzero values
  are affected) pending a decision on correcting them.
- Verification reference value: the systematic-only Viglione et al. (Kamp) example tests the
  lower endpoint of its 1,000-year quantile interval against 163 m³/s, while Skahill et al.
  (2016), Table 2, gives 183 m³/s. Passing that test does not establish agreement with the
  published endpoint; the disposition of the reference value is open.
- Legacy Bulletin 17C rows: Bulletin 17C analyses saved by pre-release 2.0 builds older than
  2.0 Beta-4 store their fit in an earlier format that this release does not convert. They open
  without their fitted curves and without a warning. Opening leaves such an analysis unchanged and
  a project save rewrites only changed analyses, so its original result cells are overwritten when
  the project is saved after that analysis is changed or re-run. The Back Creek analysis in the
  shipped Bulletin 17C Bayesian examples project is one. Keep a copy of such a project before
  saving it if the original results are needed.
- Time-series covariate rows: undoing the removal of a covariate row restores the row without its
  time series. Select the series again in the restored row.
- Coincident-frequency links: a coincident-frequency analysis stored in a project ahead of its
  upstream bivariate analysis (for example, one created before that bivariate analysis) opens
  without its link to it. Select the bivariate analysis again in the coincident-frequency
  analysis's properties and re-run it.
- Peaks-over-threshold exposure after save and reopen: the exposure (observation span) recorded
  by a peaks-over-threshold extraction is restored on returning to the peaks-over-threshold
  method only within the same session. If the project is saved and reopened in between, the
  exposure falls back to the exact series' year or index span (the 2.0.0 behavior). Run the
  extraction again after returning to peaks-over-threshold.
- Peaks-over-threshold undo after a source change: after extracting peaks over threshold from one
  time series, switching the source to another series, and processing again, undoing twice can
  restore the first series' peaks with the second series' exposure. Run the extraction again
  after such an undo.
- Save prompt after an automatic repair: opening a project in which RMC-BestFit repairs or
  migrates an element (for example the plotting-position repair described above) does not mark
  the project as changed, so there is no prompt to save the repair. Save the project after
  opening it to keep the repair.
- API plot-source export: the `results` payload of `GET api/analyses/{analysisId}/plot-source` is
  not checked for ±Infinity as other responses are, so a non-finite value there is returned as
  the JSON string "Infinity" or "-Infinity" instead of failing the request.
