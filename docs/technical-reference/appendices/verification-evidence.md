<!-- technical-reference-status: complete -->

# Verification and Evidence Matrix

[Technical reference](../index.md) | [Distribution verification](../distributions/verification-matrix.md) | [Public verification report](../../verification/report/executive-summary.md)

This matrix distinguishes deterministic software contracts from independent numerical verification and summarizes the current evidence supporting each capability.

| Area | Fast deterministic evidence | Independent numerical evidence | Supported conclusion |
|---|---|---|---|
| Model, parameter, trend, and link contracts | Constructor, mapping, likelihood-decomposition, validation, and compiled-example tests | Primary statistical sources cited by chapter | Public contracts and documented mappings are source-audited |
| Mixed exact/censored/threshold/uncertain likelihood | Pointwise and data-frame identities | Published historical/paleoflood formulations and specialized examples | Likelihood routing and pointwise decomposition are supported in declared cases |
| Fifteen univariate distributions | Fixed PDF/CDF/quantile and parameter tests | Analytical, SciPy, and R `lmomco` comparisons | All fifteen family cells passed |
| Point process | Exposure, prior, likelihood, simulation, and lifecycle contracts | Poisson/GPA oracles and generation-recovery | Ten of ten cells passed |
| Finite mixture | Simplex, hurdle, likelihood, covariance, persistence, and diagnostic contracts | Numerics parity and Bayesian recovery | Six of six cells passed |
| Competing risks | Composition, dependency, seed, matrix, and identifiability guards | Four analytical, ten MLE, and four supported Bayesian cells | All reported cells passed; six difficult Bayesian designs are outside the claim set |
| Composite/model averaging | Criterion, matrix, independent-index, seed, cache, and persistence contracts | R `mistr`, closed forms, Gaussian orthants, and Cartesian posteriors | Twelve of twelve reported cells passed |
| Bulletin 17C | Configuration, moments, covariance, scope guards, and reporting | Seven published parameter examples, three PeakFQ comparisons, and fourteen refit-reliability cells | Published parameter parity and numerical reliability are supported; broad coverage is not claimed |
| MLE, MAP, and GMM | Objective, bounds, state, profile, covariance, and gradient contracts | R `bbmle`, R `gmm`, and analytical covariance | Profile, fit, specification, and covariance comparisons passed |
| Bayesian MCMC and diagnostics | Configuration, output mapping, acceptance terminology, and thresholds | R `loo` PSIS and R `posterior` R-hat/ESS | DIC, WAIC, PSIS, R-hat, and ESS comparisons passed |
| Rating curves | Prediction, validation, likelihood, bound, alignment, and persistence contracts | SciPy likelihood/optimum, analytical continuity, and recovery | Thirty-six of thirty-six reported cells passed |
| AR, MA, ARIMA, and ARIMAX | Likelihood, transform, recursion, alignment, and serialization contracts | Twelve independent oracle groups and 37 recovery cells | All reported oracle and recovery cells passed |
| Bivariate and coincident frequency | Pair matching, likelihood, simulation, cache, and seed contracts | Independent copula optima, recovery, and closed-form Normal sums | Twenty-three of twenty-three reported cells passed |
| Spatial GEV | Correlation, likelihood, clone, criteria, prediction, bootstrap, result, and dispatch contracts | R `mvtnorm`, conditional-GP, haversine, cross-validation, simulation, and recovery | Thirty of thirty reported cells passed |
| Documentation | Namespace, link, citation, manifest, exported-API, and exact-snippet gates | Reproducible PDF render and visual inspection | Required publication gate |

## Evidence labels

- **Fast contract** means a deterministic test protects a software behavior but does not by itself establish a scientific numerical claim.
- **Analytical oracle** means the expected value is derived independently from a closed form or identity.
- **External-package oracle** means a committed artifact records a result from a named package and version.
- **Published result** means an official standard, table, or worked example supplies the comparison.
- **Recovery** means data generated from known parameters are refitted under a predeclared design and acceptance rule.
- **Coverage** means repeated simulations test a nominal interval rate against a Monte Carlo acceptance interval.

## Controlled execution

Long-running verification methods are executed one fully qualified method at a time. Every publication claim requires its oracle provenance, tolerance, observed result, and software checkpoint to be recorded. The complete verification project is not a publication command.

---

[Technical reference](../index.md) | [Public verification report](../../verification/report/executive-summary.md)
