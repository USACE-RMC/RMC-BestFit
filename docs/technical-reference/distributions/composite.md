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

**CompositeAnalysis** exposes no correlation-matrix property and does not copy a matrix when it creates its new Numerics **CompetingRisks** objects. Selecting **CorrelationMatrix** therefore leaves Numerics without the required matrix and is not a usable composite configuration. This is [TR-015](../review-findings.md#tr-015).

## Fixed Mixture Composition

For child predictive distributions \(F_m\) and configured weights \(w_m\),

$$
F_C(x)=\sum_{m=1}^{M}w_mF_m(x). \tag{3}
$$

For **CompositeType.Mixture**, every weight must satisfy \(0<w_m<1\), and the sum may not exceed one. If

$$
s=\sum_{m=1}^{M}w_m<1, \tag{4}
$$

the implementation sets Numerics **IsZeroInflated = true** and **ZeroWeight = 1-s**. This reuses the zero-inflated behavior discussed in [TR-007](../review-findings.md#tr-007); it is not merely an “unallocated model probability.” Unless an exact point mass at zero is scientifically intended, fixed mixture weights should sum to one.

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

Only estimated children with non-null **AnalysisResults** enter **EstimateModelWeights()**. However, finite criterion values are not checked before the vector is passed to Numerics. A fitted child with a non-finite DIC, WAIC, LOOIC, or RMSE can contaminate all weights. This is [TR-013](../review-findings.md#tr-013). Validation separately rejects posterior-criterion weighting when any child is **Bulletin17CAnalysis**, because that analysis does not produce a Bayesian likelihood chain.

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

The composite uses

$$
B=\min_m B_m \tag{9}
$$

and, for each index \(b=1,\ldots,B\), constructs

$$
F_C^{(b)}(x)=
\sum_m w_mF_m^{(b)}(x) \tag{10}
$$

for mixture/model-average output, or the configured min/max composition of the \(F_m^{(b)}\) for competing risks. It creates a point-estimate composite from each child's selected posterior-mean or MAP distribution and sends the supplied realization distributions to Numerics **BootstrapAnalysis.Estimate(...)** to summarize requested nonexceedance quantiles and equal-tail intervals.

Raw draw index \(b\) has no joint posterior meaning across analyses fitted separately. The implementation neither randomly permutes nor independently resamples child draw indices before pairing them. Identical sampler seeds or chain ordering can therefore impose an arbitrary coupling on a nonlinear composite. This uncertainty-propagation concern is [TR-014](../review-findings.md#tr-014). Marginal child intervals remain informative, but the composite interval should not be presented as invariant to cross-model draw pairing until the coupling policy is established.

The **BayesianAnalysis** property on **CompositeAnalysis** stores display choices such as point estimator, credible width, and output length. The composite does not call its sampler. **GetDistribution(index)** intentionally returns null; realization distributions are constructed internally during result creation.

## Validation, Run, and Cancellation

Validation requires:

- at least one child;
- every **WeightedUnivariateAnalysis** to reference an estimated, valid child;
- no nested **CompositeAnalysis**;
- valid ascending probability ordinates in \([0,1]\);
- valid fixed mixture weights and a sum not greater than one; and
- no DIC/WAIC/LOOIC weighting of a Bulletin 17C child.

**RunAsync** repeats the fitted-child check, raises a cancellable preview event, waits for any in-flight reprocessing, clears results, estimates model weights when needed, and constructs frequency results. Parallel realization construction observes the cancellation token both in scheduling and at each iteration. The analysis is marked estimated only after result construction succeeds.

Changes to weights, child results, composition type, averaging method, dependence, max/min selection, credible width, point estimator, or probability ordinates clear or reprocess derived output according to their effect. **WeightedUnivariateAnalysis** forwards child property changes and rejects a composite child in its setter.

## Practical Interpretation

For annual flood studies:

- use **CompetingRisks** if distinct processes can occur in the same year and the annual observation is their maximum;
- use **Mixture** if exactly one latent population produces each modeled observation and its probability is externally specified; and
- use **ModelAverage** when several alternative models address the same observation process and weights express a chosen predictive-comparison rule.

Before averaging, compare supports, upper endpoints, tail indices, prior assumptions, data treatments, and target quantities. Averaging incompatible estimands produces a smooth curve but not a coherent scientific model.

## Limitations

- Child fits must refer to compatible data, units, block definitions, AEP semantics, and time index.
- Criterion weights ignore uncertainty in the criteria themselves.
- AIC/BIC values from **UnivariateAnalysis** currently use a posterior-at-MAP convention tracked in [TR-011](../review-findings.md#tr-011); do not mix those uncritically with conventional MLE criteria.
- DIC, WAIC, and LOOIC require comparable pointwise likelihood definitions and priors.
- LOOIC exponential weights are not PSIS stacking and do not use Pareto-\(k\) diagnostics in weight optimization.
- Correlated competing sources require a valid joint model; merely choosing a dependency enum is insufficient.
- A model average can hide severe disagreement in the decision tail. Report component curves and weights alongside the composite.

## Implementation and Verification Traceability

| Concern | Implementation |
|---|---|
| Composition lifecycle and weighting | **Analyses/Univariate/CompositeAnalysis.cs** |
| Child/weight wrapper | **Analyses/Support/WeightedUnivariateAnalysis.cs** |
| Criterion formulas | pinned **Numerics/Data/Statistics/GoodnessOfFit.cs** |
| Mixture construction | pinned **Numerics/Distributions/Univariate/Mixture.cs** |
| Min/max construction | pinned **Numerics/Distributions/Univariate/CompetingRisks.cs** |
| Uncertainty aggregation | pinned **Numerics/Distributions/Univariate/Uncertainty Analysis/BootstrapAnalysis.cs** |

## References

<a id="ref-1"></a>[1] Y. Yao, A. Vehtari, D. Simpson, and A. Gelman, “Using stacking to average Bayesian predictive distributions,” *Bayesian Analysis*, vol. 13, no. 3, pp. 917–1007, 2018.

<a id="ref-2"></a>[2] K. P. Burnham and D. R. Anderson, *Model Selection and Multimodel Inference*, 2nd ed. New York, NY, USA: Springer, 2002.

<a id="ref-3"></a>[3] A. Vehtari, A. Gelman, and J. Gabry, “Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC,” *Statistics and Computing*, vol. 27, no. 5, pp. 1413–1432, 2017.

---

[Previous: Point-Process Models](point-process.md) | [Technical Reference](../index.md) | [Next: Estimation](../estimation/index.md)
