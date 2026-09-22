<!-- verification-status: publication-draft -->

# Evidence Boundaries and Conclusions

## Supported conclusions

The 328-method verification library provides analytical, independently implemented, external-package, published-source, and recovery evidence across the model families described in this report. The strongest conclusions concern a specified calculation under a defined parameterization: distribution functions and likelihoods, estimator objectives and covariance, diagnostic statistics, recurrence relations, and uncertainty transformations.

Recovery experiments add evidence that the tested estimators recover their generating parameters or responses under the declared sample sizes, seeds, priors, and acceptance rules. Published examples connect these calculations to established flood-frequency applications. Appendix A gives the complete correspondence between report scope and executable tests.

## Limits of the supported claims

| Area | Boundary |
|---|---|
| Bulletin 17C intervals | Numerical Cohn interval values and broad repeated-sample interval coverage are not established by the current evidence. |
| Bulletin 17C bootstrap fitting | Accepted output is not synonymous with outer GMM convergence; 53 of 1,000 fits in the inspected experiment reached the iteration cap. |
| Published GEV comparison | The systematic-only Kamp example tests a 1,000-year lower endpoint of 163 m³/s, while Skahill et al. (2016), Table 2, gives 183 m³/s. That endpoint does not establish agreement with the published value. |
| Joint-prior sampling | Independent parameter-prior draws do not sample additional coupled quantile, Jeffreys, or spatial prior factors. |
| GMM deletion influence | The one-step magnitude is not calibrated to exact deletion scale, although the tested outlier-ranking result is supported. |
| Competing risks | Five recovery methods cover three specified generating designs. General correlated Bayesian recovery is not established. |
| Bivariate estimation | Copula fitting conditions on fitted marginals; joint marginal-copula posterior estimation is not established. |
| Rating curves | Independent likelihood and recovery evidence does not establish equivalence to R `bdrc`, repeated-realization simultaneous coverage, or extrapolation calibration. |
| Spatial extremes | Large-network performance, latent-error recovery, and simultaneous predictive coverage are not established by the ten-site recovery designs. |
| Spatial weighting | Correlation weights control relative site influence; they are not pairwise composite likelihood, effective sample size, or a Godambe correction. |
| PSIS-LOO | Pareto diagnostics are supplied, but exact leave-one-out refits, moment matching, and chain-relative-efficiency adjustment are not performed. |
| Engineering application | Numerical verification does not establish the physical adequacy of a model for a particular watershed or risk decision. |
