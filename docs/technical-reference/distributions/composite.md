<!-- technical-reference-status: complete -->

# Composite Analysis and Model Averaging

[Previous: Point-Process Models](point-process.md) | [Technical Reference](../index.md) | [Next: Estimation](../estimation/index.md)

**CompositeAnalysis** combines already estimated univariate analyses. It has no likelihood and no MCMC fit of its own. Instead, it constructs a competing-risk or mixture distribution from each set of child posterior realizations and summarizes the resulting quantiles. **ModelAverage** uses the same mixture construction as **Mixture**, but computes weights from child comparison statistics.

## Three Composition Semantics

| **CompositeType** | Mathematical object | Weight meaning |
|---|---|---|
| **CompetingRisks** | distribution of the maximum or minimum of child variables | no weights |
| **Mixture** | fixed weighted mixture of child distributions | user-supplied population/scenario probabilities |
| **ModelAverage** | weighted mixture of candidate predictive distributions | criterion-derived relative support |

These objects are not interchangeable. A maximum asks which process produces the largest block value. A mixture asks which one latent source generated the observation. Model averaging asks how to combine predictions from alternative fitted models; it does not assert that future events switch among physical populations with the criterion weights.

## Competing-Risk Composition

For independent child CDFs \(F_1,\ldots,F_M\), the maximum and minimum CDFs are

$$
F_{\max}(x)=\prod_{m=1}^{M}F_m(x), \tag{1}
$$

$$
F_{\min}(x)=1-\prod_{m=1}^{M}[1-F_m(x)]. \tag{2}
$$

**IsMaximum** selects equation (1); otherwise Numerics evaluates the minimum. **Dependency** selects independent, perfectly positive, perfectly negative, or correlation-matrix probability rules as described in [Competing Risks](competing-risks.md).

**CompositeAnalysis.CorrelationMatrix** supplies the latent-Normal matrix used by correlation-matrix dependence. The property owns defensive copies, is serialized with invariant-culture `CorrelationMatrix`/`Correlation_Row` elements, and is propagated to every point-estimate and realization **CompetingRisks** object. The UI wrapper persists the same matrix through an appended optional project column, so legacy projects remain readable. Validation requires the matrix for this dependency mode, requires its dimension to equal the child count, and applies the finite, bounds, unit-diagonal, symmetry, and strict positive-definiteness checks described in [Competing Risks](competing-risks.md). TR-015 is complete.

## Fixed Mixture Composition

For child predictive distributions \(F_m\) and configured weights \(w_m\),

$$
F_C(x)=\sum_{m=1}^{M}w_mF_m(x). \tag{3}
$$

For **CompositeType.Mixture**, every weight must satisfy $0<w_m<1$, and the sum may not exceed one. If

$$
s=\sum_{m=1}^{M}w_m<1, \tag{4}
$$

the implementation sets Numerics **IsZeroInflated = true** and **ZeroWeight = 1-s**. The corrected law treats $1-s$ as an exact atom at zero and conditions every child distribution on $X>0$ for its continuous contribution. This is a scientifically meaningful positive-hurdle model, not merely an "unallocated model probability." Unless that atom and conditioning are intended, fixed mixture weights should sum to one.

## Criterion Weights

For AIC, BIC, DIC, WAIC, and LOOIC, let \(C_m\) be the selected smaller-is-better criterion and

$$
\Delta_m=C_m-\min_j C_j. \tag{5}
$$

The implementation calls Numerics **GoodnessOfFit.AICWeights** for all five criteria:

$$
w_m=\frac{\exp(-\Delta_m/2)}
{\sum_j\exp(-\Delta_j/2)}. \tag{6}
$$

For RMSE \(r_m\), it uses inverse-MSE weights,

$$
w_m=\frac{r_m^{-2}}{\sum_jr_j^{-2}}, \tag{7}
$$

and **Equal** uses \(w_m=1/M\). A final proportional normalization corrects floating-point sums that differ from one.

Equation (6) gives conventional Akaike weights when \(C_m=\mathrm{AIC}_m\). Applying the same exponential transform to BIC approximates normalized evidence under additional assumptions; applying it to DIC, WAIC, or LOOIC is a pseudo-BMA-style heuristic, not Bayesian posterior model probability and not predictive stacking [1](#ref-1). The implementation does not optimize stacking weights.

Every estimated child is classified by the selected criterion before Numerics weighting. Non-finite information criteria, non-finite RMSE, and negative RMSE are unusable. If at least one usable child remains, each unusable child receives exactly zero weight and a named warning; if none remains, validation fails with named errors and all weights remain zero. If one or more RMSE values are exactly zero, those children divide unit weight equally and all positive or invalid RMSE children receive zero. Ordinary finite values retain equations (6) and (7). These contracts close TR-013.

**Bulletin17CAnalysis** is a supported composite child. Equal, AIC, BIC, and RMSE weighting include it normally. Its **BayesianAnalysis** member is a compatibility container for GMM/frequentist uncertainty and does not represent a likelihood-based posterior, so DIC, WAIC, and LOOIC are unavailable. Under one of those posterior criteria B17C receives zero weight with a named warning when another child has a usable value. The composite is invalid only when no child has a usable selected criterion; there is no type-based B17C rejection.

## Compile-Checked Model Average

Both supplied child analyses must already have completed successfully.

<!-- snippet: composite-model-average -->
```csharp
private static CompositeAnalysis ConfigureModelAverage(
    UnivariateAnalysis gevAnalysis,
    UnivariateAnalysis logPearsonAnalysis)
{
    var composite = new CompositeAnalysis(
        new[]
        {
            new WeightedUnivariateAnalysis(gevAnalysis, 0.5),
            new WeightedUnivariateAnalysis(logPearsonAnalysis, 0.5)
        })
    {
        CompositeDistributionType = CompositeType.ModelAverage,
        ModelAverageMethod = AverageMethod.WAIC
    };

    composite.EstimateModelWeights();
    return composite;
}
```

The initial 0.5 values satisfy construction but are replaced by **EstimateModelWeights()**. The example does not imply that WAIC weighting is automatically preferable to predictive stacking or a scientifically specified ensemble.

## Posterior Realization Composition

Suppose child \(m\) exposes retained distributions

$$
F_m^{(1)},\ldots,F_m^{(B_m)}. \tag{8}
$$

The composite uses the actual retained output counts, not the configured output-length settings:

$$
B=\min_m B_m \tag{9}
$$

Before parallel result construction, one Mersenne Twister initialized from the Composite
`BayesianAnalysis.PRNGSeed` generates a without-replacement index vector for every child:

$$
k_{m,1},\ldots,k_{m,B}
\subset \{1,\ldots,B_m\},
\qquad k_{m,b}\ne k_{m,b'}\text{ for }b\ne b'. \tag{10}
$$

Rows are generated independently in configured child order. For each realization
\(b=1,\ldots,B\), the mixture/model-average branch constructs

$$
F_C^{(b)}(x)=
\sum_m w_mF_m^{(k_{m,b})}(x), \tag{11}
$$

or the configured min/max composition of the \(F_m^{(k_{m,b})}\) for competing risks.
Longer chains are sampled across their complete retained range rather than truncated to
the shortest-chain prefix. The child `MCMCResults` objects and their output order are not
modified. The point-estimate composite still uses each child's selected posterior-mean or
MAP distribution, and Numerics **BootstrapAnalysis.Estimate(...)** summarizes the supplied
realization distributions at requested nonexceedance probabilities.

This construction targets the product posterior of separately fitted children. A fixed
seed, source order, and retained output order reproduce the exact finite mapping. Reordering
sources or retained chains changes the finite seeded sample, but not the product-posterior
target; summaries are therefore distributionally rather than bitwise order invariant.
[TR-014](../review-findings.md#tr-014) records the correction and verification.

The **BayesianAnalysis** property on **CompositeAnalysis** stores the point estimator,
credible width, output length, and posterior-resampling seed. The composite does not call
its sampler. `PRNGSeed` must be nonnegative and is a result-generation setting; changing it
invalidates derived results. **GetDistribution(index)** intentionally returns null because
realization distributions are constructed internally during result creation. Index arrays
are transient and are not serialized. Saved Composite uncertainty summaries created before
TR-014 must be reprocessed to adopt the independent product-posterior policy.

## Recovery and Report Verification

`CompositeAnalysis` has no likelihood and does not sample a composite parameter posterior.
Verification therefore supplies already-estimated children with explicit retained
`MCMCResults`, then evaluates deterministic combination and posterior-propagation behavior.
This distinction prevents the report from describing composite result construction as an
estimator recovery.

The Phase 4 supplement pins RMC-TotalRisk commit
`d4d43e6407ddb4219e5cd7f613e80f749a3a0ab7` and its 2024 composite hazard/response report.
For Normal(10, 2), Normal(20, 1), and Normal(30, 5) with weights 0.3/0.2/0.5, it checks the
mixture identity, 25 published R `mistr` Table 45 quantiles, inversion against the analytical
weighted CDF, independent/comonotonic min/max identities, and rule bracketing. A bivariate
Normal median fixture checks correlation-matrix minimum and maximum probabilities at latent
correlation 0.6.

Three posterior cells use 5,000 retained draws, 20 deterministic mean-support values per child,
seed 20260803, five central/tail probabilities, and complete 20-by-20-by-20 Cartesian oracles.
The independent oracle uses direct Normal CDFs and bisection rather than the production
resampler or composite constructors. Mean ordinates have tolerance 0.02, credible limits have
tolerance 0.05, and the fixed parent must lie inside every 90% band. Nine of the ten exact methods
pass focused execution. The extreme-tail mixture inversion cell remains open because its residual
exceeds the fixed TotalRisk bound by `2.566838E-10`. Details are recorded
in [Composite Verification](../../verification/composite.md).

## Validation, Run, and Cancellation

Validation requires:

- at least one child;
- every **WeightedUnivariateAnalysis** to reference an estimated, valid child;
- no nested **CompositeAnalysis**;
- valid ascending probability ordinates in \([0,1]\);
- valid fixed mixture weights and a sum not greater than one; and
- at least one usable selected criterion for non-equal model averaging; and
- a valid, dimensionally compatible matrix when correlation-matrix competing-risk dependence is selected; and
- a nonnegative posterior-resampling seed.

**RunAsync** repeats the fitted-child check, raises a cancellable preview event, waits for any in-flight reprocessing, clears results, estimates model weights when needed, and constructs frequency results. Parallel realization construction observes the cancellation token both in scheduling and at each iteration. The analysis is marked estimated only after result construction succeeds.

Changes to weights, child results, composition type, averaging method, dependence, max/min
selection, credible width, posterior-resampling seed, point estimator, or probability ordinates
clear or reprocess derived output according to their effect. **WeightedUnivariateAnalysis**
forwards child property changes and rejects a composite child in its setter.

## Practical Interpretation

For annual flood studies:

- use **CompetingRisks** if distinct processes can occur in the same year and the annual observation is their maximum;
- use **Mixture** if exactly one latent population produces each modeled observation and its probability is externally specified; and
- use **ModelAverage** when several alternative models address the same observation process and weights express a chosen predictive-comparison rule.

Before averaging, compare supports, upper endpoints, tail indices, prior assumptions, data treatments, and target quantities. Averaging incompatible estimands produces a smooth curve but not a coherent scientific model.

## Limitations

- Child fits must refer to compatible data, units, block definitions, AEP semantics, and time index.
- Criterion weights ignore uncertainty in the criteria themselves.
- AIC/BIC values from **UnivariateAnalysis** use the data log likelihood at MAP. They are comparable with conventional MLE criteria only when all active priors are constant; with nonconstant priors, select DIC, WAIC, or verified PSIS-LOO weighting instead. See [TR-011](../review-findings.md#tr-011).
- DIC, WAIC, and LOOIC require comparable pointwise likelihood definitions and priors.
- Bulletin 17C has no likelihood-based posterior criterion and is therefore zero-weighted for DIC, WAIC, and LOOIC; it remains eligible for Equal, AIC, BIC, and RMSE.
- LOOIC exponential weights are not PSIS stacking and do not use Pareto-\(k\) diagnostics in weight optimization.
- Correlated competing sources require a valid joint model; merely choosing a dependency enum is insufficient.
- A model average can hide severe disagreement in the decision tail. Report component curves and weights alongside the composite.

## Implementation and Verification Traceability

| Concern | Implementation |
|---|---|
| Composition lifecycle and weighting | **Analyses/Univariate/CompositeAnalysis.cs** |
| Child/weight wrapper | **Analyses/Support/WeightedUnivariateAnalysis.cs** |
| Correlation-matrix validation and XML | **Models/Support/CorrelationMatrixUtilities.cs** |
| Criterion formulas | pinned **Numerics/Data/Statistics/GoodnessOfFit.cs** |
| Mixture construction | pinned **Numerics/Distributions/Univariate/Mixture.cs** |
| Min/max construction | pinned **Numerics/Distributions/Univariate/CompetingRisks.cs** |
| Uncertainty aggregation | pinned **Numerics/Distributions/Univariate/Uncertainty Analysis/BootstrapAnalysis.cs** |
| Posterior index generation | **Analyses/Support/PosteriorIndexResampler.cs** |
| Fast contract evidence | **RMC.BestFit.Tests/Analyses/PosteriorIndexResamplerTests.cs**, **RMC.BestFit.Tests/Univariate/CompositePhase4Tests.cs**, and Composite UI tests |
| Independent numerical oracle | **RMC.BestFit.Verification/ModelEstimation/PosteriorResamplingVerificationTests.cs** |
| Report and three-child recovery supplement | **RMC.BestFit.Verification/Univariate/CompositeTests/CompositeRecoveryTests.cs** and helper partial |

## References

U.S. Army Corps of Engineers, Risk Management Center, *Verification of the RMC-TotalRisk
Software*, 2024, “Composite Hazard and Response Functions,” Equation 49 and Tables 44-46.

R `mistr` package, `normdist` and `mixdist` mixture-distribution functions,
<https://cran.r-project.org/package=mistr>.

<a id="ref-1"></a>[1] Y. Yao, A. Vehtari, D. Simpson, and A. Gelman, “Using stacking to average Bayesian predictive distributions,” *Bayesian Analysis*, vol. 13, no. 3, pp. 917–1007, 2018.

<a id="ref-2"></a>[2] K. P. Burnham and D. R. Anderson, *Model Selection and Multimodel Inference*, 2nd ed. New York, NY, USA: Springer, 2002.

<a id="ref-3"></a>[3] A. Vehtari, A. Gelman, and J. Gabry, “Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC,” *Statistics and Computing*, vol. 27, no. 5, pp. 1413–1432, 2017.

---

[Previous: Point-Process Models](point-process.md) | [Technical Reference](../index.md) | [Next: Estimation](../estimation/index.md)
