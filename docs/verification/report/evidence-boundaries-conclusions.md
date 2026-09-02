<!-- verification-status: publication-draft -->

# Evidence Boundaries and Conclusions

## Supported conclusions

The report supports the following conclusions for the documented software checkpoint:

- The fifteen univariate distributions reproduce their declared analytical, SciPy, or R package references over the tested parameter regions.
- Fitting-analysis criteria, ranking, and model weights reproduce independent calculations for the declared common-data fixture.
- MLE, MAP, GMM, DIC, WAIC, PSIS-LOO, profile, covariance, and rank-normalized MCMC diagnostic calculations reproduce analytical or R package references in the tested cases.
- The specialized Bulletin 17C GMM path reproduces the seven published example parameter vectors. Bootstrap completion and accounting remain engineering evidence rather than accuracy verification.
- Point-process, supported competing-risk, mixture, and composite calculations reproduce analytical identities or recover their declared generating designs.
- Bivariate copula estimation and coincident-frequency response surfaces reproduce the declared independent references.
- Rating curves reproduce independent discharge-space likelihoods, analytical continuity, and generating curves.
- Time-series models reproduce independent transform, likelihood, recurrence, prediction, uncertainty, generation, and recovery results.
- Spatial likelihoods, prediction, distance, uncertainty, and recovery reproduce the declared independent references.

## Claims not established by this report

The following boundaries prevent readers from extending the conclusions beyond the evidence:

- **Bulletin 17C Cohn values.** Exact-data LP3 scope guards are tested, but numerical Cohn interval values are not verified at this checkpoint.
- **Bulletin 17C coverage.** Previously defined coverage cells were not executed for this checkpoint. Bootstrap reliability is not a substitute for interval coverage.
- **Competing-risk Bayesian generality.** Only the two identified independent Bayesian fixtures are claimed. Removed boundary/local-mode candidates and correlated Bayesian recovery are not supported claims.
- **Joint bivariate estimation.** Copula estimation and recovery condition on fitted marginals. A joint marginal-copula posterior is not implemented or verified.
- **Generic prior-predictive sampling.** Independently sampled parameter priors do not reproduce coupled quantile, Jeffreys, spatial-error, or other joint prior factors.
- **Spatial scale.** The tested networks are modest. Computational performance, approximation quality, and calibration for large networks are not established.
- **Spatial weighting.** Correlation heuristic weights change relative site influence. They are not a pairwise composite likelihood, an effective sample size, or a Godambe correction.
- **PSIS scope.** BestFit reports Pareto diagnostics but does not perform exact leave-one-out refits, moment matching, or chain-relative-efficiency adjustment.
- **Application validation.** Passing synthetic and published-example cells does not establish that a selected model is physically adequate for a specific watershed or decision problem.

## Software-quality controls

Fast regression projects protect constructor validation, state transitions, exceptions, deterministic calculations, caching, public API snapshots, and serialization. At the publication checkpoint the recorded regression gate contained 3,342 core, 579 UI, 438 App, and 498 API tests with zero failures, and the strict XML documentation gate passed. These results support implementation stability but are not counted as independent numerical verification.

## Reproducibility

Each independent oracle records its generator, package versions, source inputs, parameter conversions, seed, and hash in the data manifest. Long-running verification methods are invoked one at a time by fully qualified name, and each invocation must produce one TRX result. The public report is generated from an explicit book manifest; report metadata identifies the exact BestFit and Numerics source commits.

## Overall conclusion

RMC.BestFit 2.0 has broad, multi-source numerical evidence across its principal statistical and hydrologic capabilities. Exact analytical and external-package comparisons establish the calculation-level core; published examples establish practical parameter parity; and recovery tests exercise estimators and posterior propagation. The Bulletin 17C confidence-interval coverage studies are preserved as execution-excluded history and make no current refreshed claim. All results presented as passing in this report satisfied predeclared acceptance rules. The evidence boundaries above are intentionally excluded from the verified claim set and should guide external review, future verification priorities, and application-specific model validation.
