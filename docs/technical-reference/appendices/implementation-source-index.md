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
| Univariate model | [Models/UnivariateDistribution](../../../src/RMC.BestFit/Models/UnivariateDistribution) | Fifteen univariate distributions in RMC.Numerics 2.1.4 |
| Peaks over threshold | [PointProcessModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/PointProcessModel.cs), [Analyses/Univariate](../../../src/RMC.BestFit/Analyses/Univariate) | Numerics GEV/GPD sign convention and integration |
| Mixtures and competing risks | [MixtureModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/MixtureModel.cs), [CompetingRisksModel](../../../src/RMC.BestFit/Models/UnivariateDistribution/CompetingRisksModel.cs) | Numerics mixture and competing-risk distributions |
| Composite/model-average analysis | [CompositeAnalysis.cs](../../../src/RMC.BestFit/Analyses/Univariate/CompositeAnalysis.cs) | Information-criterion calculations |
| Bulletin 17C | [Bulletin17CDistribution](../../../src/RMC.BestFit/Models/UnivariateDistribution/Bulletin17CDistribution.cs), [B17C analysis](../../../src/RMC.BestFit.UI/Elements/UnivariateAnalysis) | Bulletin 17C, Cohn EMA methods, Numerics distributions |
| MLE, MAP, GMM, Bayesian MCMC | [Estimation](../../../src/RMC.BestFit/Estimation) | Numerics optimizers, DEMCz, DEMCzs, ARWMH, and NUTS |
| Diagnostics and predictive checks | [Diagnostics](../../../src/RMC.BestFit/Diagnostics) | Numerics MCMC results and statistical utilities |
| Rating curves | [Models/RatingCurve](../../../src/RMC.BestFit/Models/RatingCurve), [Analyses/RatingCurve](../../../src/RMC.BestFit/Analyses/RatingCurve) | Base-10 log error convention |
| Time series | [Models/TimeSeries](../../../src/RMC.BestFit/Models/TimeSeries), [Analyses/TimeSeries](../../../src/RMC.BestFit/Analyses/TimeSeries) | Numerics distributions and transformation utilities |
| Bivariate copulas | [Models/BivariateDistribution](../../../src/RMC.BestFit/Models/BivariateDistribution), [Analyses/Bivariate](../../../src/RMC.BestFit/Analyses/Bivariate) | Numerics bivariate copula families |
| Coincident frequency | [CoincidentFrequencyAnalysis.cs](../../../src/RMC.BestFit/Analyses/Bivariate/CoincidentFrequencyAnalysis.cs) | Copula CDF and Normal probability transform |
| Spatial extremes | [Models/SpatialExtremes](../../../src/RMC.BestFit/Models/SpatialExtremes), [Analyses/SpatialExtremes](../../../src/RMC.BestFit/Analyses/SpatialExtremes) | Numerics GEV, Normal transforms, matrices, and Euclidean distance |

## Dependency Baseline

The published package baseline remains RMC.Numerics 2.1.4 at commit `828664650c9327b309ee8332e707ccca73588e93`. Phase 0-4 verification used the local `C:\GIT\Numerics` correction history through validated head `34b51862f0429b263efbed0c22f61d57b8771e1a`; reachable scientific correction anchors include `3e69a9388bbecaad89960679301b3bd8d685b88d` for mixtures and `cafe6cf3837988341912a5aa8bfda444ea55ff77` for competing-risk simulation. Chapter-specific pins identify the source actually audited rather than implying that later unrelated chapters were revalidated at this head.

## Test and Example Sources

| Evidence class | Location |
|---|---|
| Fast scientific unit tests and documentation gates | [RMC.BestFit.Tests](../../../src/RMC.BestFit.Tests) |
| Compile-checked reference examples | [Documentation/Examples](../../../src/RMC.BestFit.Tests/Documentation/Examples) |
| Long-running scientific verification sources | [RMC.BestFit.Verification](../../../src/RMC.BestFit.Verification) |
| UI wrapper tests | [RMC.BestFit.UI.Tests](../../../src/RMC.BestFit.UI.Tests) |
| Application tests | [RMC.BestFit.App.Tests](../../../src/RMC.BestFit.App.Tests) |

Verification execution is claim-specific. Phases 1-4 include explicitly authorized methods run one at a time through `scripts/run-verification-test.ps1`; the full Verification project was never executed. Rows that describe source inspection only remain labeled as such in their subject chapters.

---

[Technical reference](../index.md) | [API traceability](../api-traceability.md) | [Reviewer checklist](reviewer-checklist.md)
