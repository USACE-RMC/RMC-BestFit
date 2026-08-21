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
  freedom are not positive.
- Bulletin 17C: bootstrap diagnostics are per requested replicate with a separate realization
  count; the report states the substituted-replicate count and fraction and the point-mass
  consequence; a converged-within-tolerance refit must improve on its start; pivot bound repairs
  and z-limit clips are counted and reported; log-scale penalty centers are perturbed on the log
  scale; `BootstrapDiagnostics` gains `AttemptedRealizations`, `BoundRepairs`,
  `BoundRepairRate`, and `IncrementBoundRepair()`; `ComputeCohnStyleConfidenceIntervals()`
  throws `NotSupportedException` outside its LP3/exact-data scope; AIC/BIC use the data
  likelihood.
- Mixture: MCMC samples the identified `K-1` weight coordinates; AIC/BIC count `K-1` weights;
  legacy full-`K` posteriors still open.

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
