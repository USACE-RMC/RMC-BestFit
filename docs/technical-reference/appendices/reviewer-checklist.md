<!-- technical-reference-status: complete -->

# Scientific Reviewer Checklist

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Verification evidence](verification-evidence.md)

This checklist gives external reviewers a consistent way to assess each scientific capability without requiring knowledge of the development history.

## Shared foundations

| Review area | Technical treatment | Questions for the reviewer | Independent evidence |
|---|---|---|---|
| Data-frame types | [Mixed-observation likelihood](../data-frame/index.md) | Are exact, censored, threshold, uncertain, and interval contributions defined on the correct measure and pointwise unit? | Pointwise identities and published censoring formulations |
| Parameters and priors | [Parameters and priors](../models/parameters-and-priors.md) | Are bounds, marginal priors, Jeffreys terms, penalties, quantile priors, and Jacobians distinguishable? | Analytical mappings and prior-component tests |
| MLE and MAP | [MLE](../estimation/maximum-likelihood.md), [MAP](../estimation/maximum-a-posteriori.md) | Are objective signs, profile targets, covariance status, and MAP-based criteria interpreted correctly? | R `bbmle` and analytical covariance |
| GMM | [GMM chapter](../estimation/generalized-method-of-moments.md) | Are moment dimensions, weights, gradients, specification tests, penalties, and covariance bread/meat consistent? | R `gmm` and analytical reconstruction |
| Bayesian MCMC | [MCMC chapter](../estimation/bayesian-mcmc.md) | Are sampler targets, defaults, retained-chain mapping, and acceptance statistics explicit? | R `posterior` and sampler-level contracts |
| Comparison and diagnostics | [Model comparison](../estimation/model-comparison.md), [diagnostics](../estimation/diagnostics.md), [influence](../estimation/influence-diagnostics.md), [predictive checks](../estimation/predictive-checks.md) | Are pointwise units, PSIS reliability, R-hat, ESS, influence, and predictive limitations clear? | R `loo`, R `posterior`, and analytical fixtures |

## Application collection order

| Review area | Technical treatment | Questions for the reviewer | Independent evidence |
|---|---|---|---|
| Time-series data | [Time-series data](../data/time-series-data.md) | Are chronology, interval, provenance, units, and serialization boundaries explicit? | Fast persistence and validation contracts |
| Input data | [Input data](../data/input-data.md) | Are source selection, processing, and conversion into analysis-ready observations separated? | Fast processing and persistence contracts |
| Distribution fitting | [Distribution fitting](../analysis/distribution-fitting.md) | Are candidate ordering, fitting methods, plotting positions, criteria, and result selection explicit? | Analytical, SciPy, R `lmomco`, and ranking oracles |
| Univariate families | [Distribution index](../distributions/index.md) | Are parameter order, units, support, tail sign, limiting branches, and moments unambiguous? | Analytical, SciPy, and R `lmomco` comparisons |
| Bulletin 17C | [Overview](../analysis/bulletin-17c.md), [estimation](../analysis/bulletin-17c-estimation.md), [uncertainty](../analysis/bulletin-17c-uncertainty.md) | Are moment equations, penalties, covariance, result terminology, and evidence boundaries explicit? | Seven examples, three PeakFQ cells, fourteen reliability cells |
| Point process | [Point-process chapter](../distributions/point-process.md) | Are exposure, empirical rate, fitted intensity, seasonal blocks, and annual maxima separated? | Ten Poisson/GPA/recovery cells |
| Competing risks | [Competing-risks chapter](../distributions/competing-risks.md) | Are minimum/maximum composition, dependence, simulation, and identifiability limits clear? | Analytical rank/CDF and supported recovery cells |
| Mixture model | [Mixture chapter](../distributions/mixture.md) | Is the full-$K$ physical boundary distinct from identified $K-1$ posterior storage and the positive hurdle? | Six parity/recovery cells |
| Composite/model averaging | [Composite chapter](../distributions/composite.md) | Are model weights, dependence, independent source indexing, and result-only uncertainty propagation clear? | R `mistr`, closed forms, Gaussian orthants, Cartesian posteriors |
| Bivariate copulas | [Bivariate chapter](../analysis/bivariate.md) | Are pseudo-likelihood, IFM, fixed marginals, copula parameterization, and posterior limits explicit? | Twelve optimum and seven recovery cells |
| Coincident frequency | [Coincident-frequency chapter](../analysis/coincident-frequency.md) | Are source posteriors independently indexed and the response surface correctly propagated? | Three Normal-sum cells and one posterior oracle |
| Rating curves | [Rating-curve chapter](../analysis/rating-curve.md) | Is the piecewise power law continuous under defaults, and is the likelihood on the discharge measure? | SciPy likelihood/optimum, continuity, and recovery |
| Time series | [Overview](../analysis/time-series.md) and the four model chapters | Are transforms, differencing, lags, dates, recurrences, forecast boundaries, and conditional likelihoods aligned? | Twelve oracle groups and 37 recovery cells |
| Spatial extremes | [Spatial GEV chapter](../spatial/spatial-extremes.md) | Are missing-site marginalization, latent-error priors, row/year units, distance, prediction, and uncertainty methods explicit? | R `mvtnorm`, conditional-GP, haversine, cross-validation, simulation, and recovery |

## Cross-document checks

- Confirm that parameter names and order agree among equations, code examples, API tables, result tables, and the parameterization crosswalk.
- Confirm that every evidence statement names its oracle type and does not infer verification from a passing unit test alone.
- Confirm that limitations describe the supported claim boundary without presenting internal development history.
- Confirm that the software and dependency checkpoints match the report front matter.
- Confirm that references resolve and that duplicate works do not appear under variant titles or DOI formats.
- Confirm that PDF equations, tables, figures, bookmarks, page numbers, and reading order remain legible after generation.

---

[Technical reference](../index.md) | [Verification evidence](verification-evidence.md)
