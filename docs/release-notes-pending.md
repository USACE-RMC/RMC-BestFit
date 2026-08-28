# Pending release notes

Behavior changes made since the public releases RMC.BestFit 2.0.0 and RMC.Numerics 2.1.4 that
must appear in the next release notes. Package versions are not incremented by this list; the
BestFit solution builds against the local Numerics checkout while these changes are in
development.

## RMC.BestFit (since 2.0.0)

- Time series: ARIMAX conditions the likelihood, residuals, generation, and prediction on
  `max(p, q, b)`; an empty conditional sum is an invalid fit (negative-infinite likelihood)
  rather than a zero log-likelihood; ARIMA/AR order setters rebuild the training state; the
  pointwise transform Jacobian is per observation; the transform reset of custom priors is
  gated on `UseDefaultFlatPriors`; prediction-window covariate gaps and transform failures are
  validation messages. `ARIMAX.Validate` requires exact-date covariate matching (legacy
  projects with positionally aligned but differently dated covariates fail validation);
  `SetTransformParameters` throws on a non-finite first parameter and ignores the second;
  `GenerateRandomValues` returns raw-scale values of length `sampleSize`;
  `ARIMAX.TrainingTimeSeries` is the differenced series.
- Point process: seasonal Gumbel-limit annualization uses `xi + alpha ln p`; the seasonal
  simulator uses the fitted per-season threshold intensities; clones recompute the event rate;
  seasonal quantile priors are evaluated on the annualized distribution.
- Composite and coincident frequency: zero inflation is inferred only when the weights sum to
  less than one by more than `1e-10`; the correlation matrix edit is undoable; the posterior
  index cache is thread safe.
- Estimation and diagnostics: a degenerate PSIS tail reports `k = +inf` and unestimated Pareto
  k counts as unreliable; fewer than eleven retained draws use the fixed 0.7 limit; the data
  frame keeps the recorded POT observation span when the exact series is replaced; GMM
  `PostProcess` keeps a restored J statistic, restored out-of-scope J statistics read as NaN,
  and covariance queries no longer overwrite the weighting state; profile grids report NaN for
  grid points without a finite nuisance optimum; the MLE Hessian uses bounded steps;
  single-parameter covariance is available; `InfluenceDiagnostics.GetProblematicObservations()`
  gains a parameterless overload that uses the instance limit; all-failed distribution fitting
  reports completion; Mixture, CompetingRisk, and B17C report NaN RMSE when residual degrees of
  freedom are not positive; the GMM moment covariance that feeds the sandwich covariance, the
  post-estimate weighting matrix, Hansen J, and the influence diagnostics is conditioned only by
  the symmetric positive-definite floor (the former 50-times-median eigenvalue cap rewrote the
  moment covariance of real-space three-parameter families and distorted every covariance entry;
  point estimates were never affected).
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
  parameters, and an outright initialization failure is reported as a validation error.
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
  Gaussian-process densities of the latent location/scale/shape errors moved from `DataLogLikelihood`
  to a `PriorLogLikelihood` override (posterior kernel, sampler, MAP, and posterior unchanged;
  AIC/BIC/DIC/WAIC/LOOIC of latent-error models now exclude the process densities; the
  scalar/pointwise identities hold); `ComputeGodambeCovariance` derives both sandwich factors from
  the row/year estimating equations, returns `null` with `GodambeCovarianceStatus = Failed` and a
  `GodambeCovarianceDiagnostic` instead of the variability matrix when the sensitivity matrix is
  singular or a value is not finite, validates the parameter count, and is reset by `ClearResults`;
  spatial AIC/BIC keep the nonempty row/year unit (`SpatialGEVAnalysis.ComputeInformationCriteria`);
  `SpatialGEV.Clone()` now carries the copula and latent-error parameter blocks and the source
  values, bounds, and priors (previously the clone held only the trend blocks with reset
  intercepts, so copula and latent-error Bayesian analyses failed while building site results);
  leave-one-site-out cross-validation fits a reduced training model per fold (the held-out site's
  data, coordinates, covariate rows, copula coordinate, and latent error removed) with a fold
  analysis carrying the main settings and seed, predicts the held-out site with its own covariate
  rows, never refits or mutates the main analysis (results are retained), and reports
  `FoldStatus`, `FoldMessages`, `SuccessfulFolds`, and `TotalFolds` with NaN metrics for unscored
  folds, aggregates over successful folds, and an `InvalidOperationException` when no fold
  succeeds; `GeneralLinearFunction.PredictWithCovariates(null or empty)` throws for a trend that
  has covariates (intercept-only trends still accept null), so `PredictAtUngaugedLocation` and
  `SpatialGEV.PredictAtUngauged` require covariate values for covariate models; ungauged-site
  predictions apply the conditional Gaussian process of every posterior draw with a seeded conditional
  residual (`SampleConditionalResidual`, default true; false gives the conditional mean) instead of
  inverse-distance interpolation; regional credible bounds are posterior quantiles of the per-draw
  regional mean quantile instead of averages of site interval endpoints; `GenerateRandomValues`
  simulates spatially dependent rows through the fitted copula (independent sites without it);
  `RunSpatialBootstrapAsync` runs a temporal block bootstrap (rows resampled in blocks, all sites kept,
  MAP refit per replicate, NaN failures, at least half of the replicates required, `BootstrapResults`
  accounting; `blockSize` now counts rows); `RunAsync` applies the selected `UncertaintyMethod`
  (posterior, sqrt-VIF inflation, Gaussian parameter draws from the Godambe covariance at the MAP, or
  the bootstrap with `BootstrapReplicates`/`BootstrapBlockSize`) and records `AppliedUncertaintyMethod`
  and `SpatialGEVSiteResults.UncertaintyMethod`; `UncertaintyMethod`, `SampleConditionalResidual`,
  `BootstrapReplicates`, and `BootstrapBlockSize` are serialized as optional attributes; a non-finite
  site GEV parameter gives negative-infinite likelihood instead of throwing inside the sampler; default
  latent-error bounds under a log link use the log-space spread (floor 1.0 log unit), so
  `ConfigureForProperCoverage` models sample under the defaults; `SpatialGEV.DistanceMetric`
  (`SpatialDistanceMetric.Cartesian` default, bitwise the former planar distances; `Geodesic` for
  latitude/longitude in decimal degrees with great-circle kilometres) with new `GaussianCopula` and
  `SpatialRegressionErrors` constructor overloads, coordinate validation, and an optional serialized
  attribute; `ComputeEffectiveSampleSizeWeights` is obsolete in favor of
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
- Input data POT diagnostics (issue #14): the mean-residual-life and parameter-stability plots
  operate on the same smoothed series the peaks-over-threshold extraction thresholds (via the new
  `TimeSeries.SmoothedSeries`), and editing the smoothing function, period, minimum steps between
  peaks, or the source time-series element marks the diagnostics dirty.

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
  element (formerly its length was silently dropped); `Statistics.ParallelMean` delegates to the
  sequential mean, so it is bit-reproducible across machines (the PLINQ partition order was not).
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
  leaf); BFGS implements dfpmin's parameter-change exit (TOLX), so a stagnated warm start returns
  immediately instead of repeating the identical non-progressing iteration to the budget.
- The interpolation correlated-search windows scale as Count^0.25 (`Interpolater.deltaStart` was
  pinned to 1 by a Math.Min typo; `OrderedPairedData`'s X/Y windows were never assigned), so the
  hunt search path is reachable; brackets are unchanged.
- `LogNormal` and `LogPearsonTypeIII` parameter constraints allow a negative log-space mean (the
  location bounds are symmetric about zero like Normal's; the former machine-epsilon floor
  rejected any sub-unity sample), and their `MinimumOfParameters` report negative infinity for
  the location. `TimeSeries.SmoothedSeries` exposes the exact smoothing preprocessing
  `PeaksOverThresholdSeries` applies, for threshold-selection diagnostics.
