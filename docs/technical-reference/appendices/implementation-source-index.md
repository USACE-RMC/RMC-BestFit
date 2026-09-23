<!-- technical-reference-status: complete -->

# Implementation-Source Index

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Reviewer checklist](reviewer-checklist.md)

This index maps scientific treatments to the implementation directories audited for this release. File paths are relative to the repository root and are clickable in the canonical Markdown.

| Scientific area | BestFit implementation | Numerics dependency or convention |
|---|---|---|
| Model contracts and likelihood decomposition | [Models/Support](../../../src/RMC.BestFit/Models/Support), [ModelBase.cs](../../../src/RMC.BestFit/Models/Support/ModelBase.cs) | Probability/distribution base contracts in pinned Numerics |
| Mixed-observation data frame | [Models/DataFrame](../../../src/RMC.BestFit/Models/DataFrame) | Numerical integration and univariate distribution CDF/PDF |
| Parameters, priors, and penalties | [Models/Support](../../../src/RMC.BestFit/Models/Support) | Numerics univariate prior distributions |
| Trend functions | [Models/TrendFunctions](../../../src/RMC.BestFit/Models/TrendFunctions) | No separate estimator |
| Link functions | [Models/LinkFunctions](../../../src/RMC.BestFit/Models/LinkFunctions) | Transform conventions cross-checked against primary sources |
| Univariate model | [Models/UnivariateDistribution](../../../src/RMC.BestFit/Models/UnivariateDistribution) | Fifteen univariate distributions in RMC.Numerics 2.2.0 |
| Peaks over threshold | [PointProcessModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/PointProcessModel.cs), [Analyses/Univariate](../../../src/RMC.BestFit/Analyses/Univariate) | Numerics GEV/GPD sign convention and integration |
| Competing risks and mixtures | [CompetingRisksModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/CompetingRisksModel.cs), [MixtureModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/MixtureModel.cs) | Numerics competing-risk and mixture distributions |
| Composite/model-average analysis | [CompositeAnalysis.cs](../../../src/RMC.BestFit/Analyses/Univariate/CompositeAnalysis.cs) | Information-criterion calculations |
| Bulletin 17C | [Bulletin17CDistribution](../../../src/RMC.BestFit/Models/UnivariateDistribution/Bulletin17CDistribution.cs), [B17C analysis](../../../src/RMC.BestFit/Analyses/Univariate/Bulletin17CAnalysis.cs) | Bulletin 17C, Cohn EMA methods, Numerics distributions |
| MLE, MAP, GMM, Bayesian MCMC | [Estimation](../../../src/RMC.BestFit/Estimation) | Numerics optimizers, DEMCz, DEMCzs, ARWMH, and NUTS |
| Diagnostics and predictive checks | [Diagnostics](../../../src/RMC.BestFit/Diagnostics) | Numerics MCMC results and statistical utilities |
| Rating curves | [Models/RatingCurve](../../../src/RMC.BestFit/Models/RatingCurve), [Analyses/RatingCurve](../../../src/RMC.BestFit/Analyses/RatingCurve) | Base-10 log error convention |
| Time series | [Models/TimeSeries](../../../src/RMC.BestFit/Models/TimeSeries), [Analyses/TimeSeries](../../../src/RMC.BestFit/Analyses/TimeSeries) | Numerics distributions and transformation utilities |
| Bivariate copulas | [Models/BivariateDistribution](../../../src/RMC.BestFit/Models/BivariateDistribution), [Analyses/Bivariate](../../../src/RMC.BestFit/Analyses/Bivariate) | Numerics bivariate copula families |
| Coincident frequency | [CoincidentFrequencyAnalysis.cs](../../../src/RMC.BestFit/Analyses/Bivariate/CoincidentFrequencyAnalysis.cs) | Copula CDF and Normal probability transform |
| Spatial extremes | [Models/SpatialExtremes](../../../src/RMC.BestFit/Models/SpatialExtremes), [Analyses/SpatialExtremes](../../../src/RMC.BestFit/Analyses/SpatialExtremes) | Numerics GEV, Normal transforms, matrices, and Cartesian/geodesic distance |

## Dependency Baseline

The declared dependency is RMC.Numerics 2.2.0. This draft reviews Numerics source `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02` with BestFit source `3fa55a0f75bbd583e2fb7fce42180faa376f007c`; local validation used project-reference mode. Historical fixture pins identify their own provenance and do not supersede this report checkpoint.

## Test and Example Sources

| Evidence class | Location |
|---|---|
| Fast scientific unit tests and documentation gates | [RMC.BestFit.Tests](../../../src/RMC.BestFit.Tests) |
| Compile-checked reference examples | [Documentation/Examples](../../../src/RMC.BestFit.Tests/Documentation/Examples) |
| Independent numerical verification sources | [RMC.BestFit.Verification](../../../src/RMC.BestFit.Verification) |
| UI wrapper tests | [RMC.BestFit.UI.Tests](../../../src/RMC.BestFit.UI.Tests) |
| Application tests | [RMC.BestFit.App.Tests](../../../src/RMC.BestFit.App.Tests) |

Numerical evidence is claim-specific. The [catalog](../../verification/verification-catalog.json) identifies each retained method, oracle, acceptance rule, and run-of-record. The [verification report](../../verification/report/executive-summary.md) interprets that evidence. Fast unit tests guard programmatic contracts and are distinct from those numerical comparisons.

---

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Reviewer checklist](reviewer-checklist.md)
