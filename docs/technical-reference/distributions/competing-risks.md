<!-- technical-reference-status: complete -->

# Competing-Risks Extremes

[Previous: Mixture Models](mixture.md) | [Technical Reference](../index.md) | [Next: Point-Process Models](point-process.md)

**CompetingRisksModel** describes a block outcome formed by the minimum or maximum of several process variables. In flood-frequency work, the maximum formulation is typical: rainfall, snowmelt, and other mechanisms can each produce a block maximum, and the recorded annual maximum is the largest. The name “competing risks” does not imply a latent mixture; all component variables participate in each block.

## Independent Maximum and Minimum

Let $X_1,\ldots,X_K$ have CDFs $F_k$, densities $f_k$, and survival functions $S_k=1-F_k$. If the components are independent, the maximum $M=\max_k X_k$ has

$$
F_M(x)=P(X_1\le x,\ldots,X_K\le x)
=\prod_{k=1}^{K}F_k(x), \tag{1}
$$

$$
f_M(x)=\sum_{k=1}^{K}
f_k(x)\prod_{j\ne k}F_j(x). \tag{2}
$$

The minimum $m=\min_k X_k$ has

$$
S_m(x)=P(X_1>x,\ldots,X_K>x)
=\prod_{k=1}^{K}S_k(x), \tag{3}
$$

$$
F_m(x)=1-\prod_{k=1}^{K}S_k(x), \tag{4}
$$

$$
f_m(x)=\sum_{k=1}^{K}
f_k(x)\prod_{j\ne k}S_j(x). \tag{5}
$$

Numerics evaluates the independent log densities in log space. Its ordinary **PDF** also applies a floor of $10^{-300}$ to prevent a zero density from entering downstream log-likelihood calculations. This floor is a numerical convention, not physical tail probability.

## Dependence Models

The underlying Numerics distribution exposes four **Probability.DependencyType** values:

| Value | CDF construction | Interpretation |
|---|---|---|
| **Independent** | products/unions in equations (1) and (4) | independent component block values |
| **PerfectlyPositive** | comonotonic joint/union probability | all components share a probability rank |
| **PerfectlyNegative** | Gaussian-copula calculation using equicorrelation just above $-1/(K-1)$ | limiting negative association approximation |
| **CorrelationMatrix** | Gaussian copula with supplied matrix | user-specified latent-normal correlation |

For a maximum under a Gaussian copula $C_R$,

$$
F_M(x)=C_R\!\left(F_1(x),\ldots,F_K(x)\right). \tag{6}
$$

For a minimum, the implementation obtains the corresponding union probability. For non-independent settings, Numerics computes the scalar density by numerical differentiation of the composite CDF. Accuracy can therefore deteriorate in very flat tails or near support boundaries.

The correlation matrix describes dependence among latent normal scores, not Pearson correlation among flood magnitudes. It must be finite, symmetric, bounded in $[-1,1]$, have unit diagonal, have dimension $K\times K$, and be strictly positive definite for the multivariate-normal implementation. Dependence is not estimated by **CompetingRisksModel**; it is fixed configuration supplied through the underlying Numerics distribution or through a parent **CompositeAnalysis**.

## Observation Likelihood

Let $\boldsymbol\theta=(\boldsymbol\theta_1^\mathsf T,\ldots,\boldsymbol\theta_K^\mathsf T)^\mathsf T$ be the concatenated component parameter vector. There are no mixture weights. For exact independent block outcomes $y_i$,

$$
\ell_D(\boldsymbol\theta)
=\sum_{i=1}^{n}\log f_C(y_i\mid\boldsymbol\theta), \tag{7}
$$

where $f_C$ is equation (2), equation (5), or the numerically differentiated dependent composite density. Low outliers, uncertain records, intervals, and perception-threshold counts use the same CDF/survival decomposition as [Data Frame and Observation Likelihood](../data-frame/index.md), but all probabilities are evaluated on the composite distribution.

For example, an interval $(a_i,b_i]$ contributes

$$
\log\left[F_C(b_i\mid\boldsymbol\theta)
-F_C(a_i\mid\boldsymbol\theta)\right], \tag{8}
$$

and an uncertain observation with error density $g_i$ contributes the normalized numerical approximation to

$$
\log\int g_i(x)f_C(x\mid\boldsymbol\theta)\,dx. \tag{9}
$$

The pointwise method returns one contribution per exact, uncertain, interval, or grouped threshold record, preserving the data-likelihood sum used by WAIC and LOO.

## Priors and Posterior

Each component parameter receives its configured scalar prior. Optional scale terms add $-\log s_k$ for recognized positive component scales. The model forces a single quantile prior; if enabled, it is evaluated on the quantile of the composite maximum or minimum:

$$
\ell_Q(\boldsymbol\theta)
=\log p_Q\!\left[
F_C^{-1}(1-\alpha_Q\mid\boldsymbol\theta)
\right]. \tag{10}
$$

The complete posterior log target is $\ell_D+\ell_P$. Since the components are not observed separately, their parameters may be weakly identified when their distributions overlap or one component dominates the composite tail.

## Compile-Checked Maximum Configuration

The Numerics default is **MinimumOfRandomVariables = true**. A flood-maximum analysis must therefore set the property explicitly, as the compiled example does.

<!-- snippet: competing-risks-workflow -->
```csharp
private static CompetingRiskAnalysis ConfigureIndependentFloodSources(
    global::RMC.BestFit.Models.DataFrame annualMaxima)
{
    var parent = new CompetingRisks(
        new UnivariateDistributionBase[]
        {
            new GeneralizedExtremeValue(),
            new LogNormal()
        })
    {
        MinimumOfRandomVariables = false,
        Dependency = Probability.DependencyType.Independent
    };

    var model = new CompetingRisksModel(annualMaxima, parent);
    return new CompetingRiskAnalysis(model);
}
```

The family choices are illustrative. A real study should connect each component to a defensible flood-generating mechanism and determine whether the available annual record can identify the separate component distributions.

## Analysis and Uncertainty Propagation

**CompetingRiskAnalysis** validates the model, probability ordinates, and MCMC settings; runs **BayesianAnalysis**; and creates a cloned Numerics competing-risk distribution for every retained posterior parameter vector. The resulting frequency intervals propagate uncertainty in component parameters conditional on the fixed max/min and dependence configuration.

The component variables are not separately observed in this likelihood. If event classification is available and reliable, a likelihood conditioned on source labels may contain substantially more information than the aggregate competing-risk likelihood implemented here.

## Simulation

**CompetingRisksModel.GenerateRandomValues(...)** validates any required correlation matrix and delegates to Numerics **CompetingRisks.GenerateRandomValues(...)**. The established Numerics entry point now routes through its dependency-aware implementation:

- **Independent** uses a separate uniform rank for every component and observation;
- **PerfectlyPositive** uses one shared rank per observation;
- **PerfectlyNegative** samples the documented limiting equicorrelated Gaussian copula; and
- **CorrelationMatrix** samples the configured Gaussian copula.

The same seed reproduces the same sample within each mode. Invalid user matrices fail before simulation rather than producing a downstream null reference, Cholesky failure, or silent independent sample. The four guarded analytical rank/CDF methods and recovery evidence are recorded in [Competing-Risk Analysis](../../verification/report/competing-risk-analysis.md).

## Recovery Verification

The [current verification report](../../verification/report/competing-risk-analysis.md) uses three 1,000-observation designs: an independent minimum of two Weibulls, an independent maximum of a Weibull and Gumbel, and a correlated two-Weibull minimum with fixed Gaussian correlation 0.6. Three MLE cells and the two independent Bayesian cells form the retained recovery matrix.

The Bayesian maximum experiment uses a Weibull with scale 100 and shape 3, a Gumbel with location 80 and scale 20, bounded parameter priors, and `UseJeffreysRuleForScale = false`. Its DEMCzs sampler settings remain the defaults. This prior choice is material: as a subordinate Weibull scale approaches zero, the maximum likelihood can remain finite because the Gumbel explains the observations. Multiplying that likelihood by the optional reciprocal-scale term can then favor the collapsing component. The passing experiment therefore supports the stated prior configuration; it does not establish recovery for this maximum model with the optional Jeffreys multiplier enabled. Inspect both component identification and prior behavior near boundaries when interpreting aggregate maxima.

Each component must contribute appreciably before recovery is assessed: theoretical control probability at least 15%, at least 100 known synthetic winners, at least 100 fractional likelihood memberships, dominance over at least 10% of the probability range, and an interior change of dominance. These conditions make the bend in the combined frequency curve informative about both populations. Winning labels are retained only for checking the experiment; estimation receives scalar minima or maxima.

MLE parents must be within 1.96 unregularized standard errors, with additional combined-CDF checks. The independent Bayesian designs require central 95% parent inclusion, R-hat below 1.10, and ESS at least 100. These acceptance limits do not establish recovery for arbitrary overlaps, fixed correlations, or maximum models. In particular, correlated Bayesian recovery remains outside the supported claim.

## Assumptions and Limitations

- Equations (1)–(5) require independence; select another dependency mode only with evidence and a valid correlation specification.
- The block variables must refer to the same time interval and the recorded response must truly be their min or max.
- BestFit supports one to three component families, all stationary in this model. The upper
  bound is an identifiability guard shared with mixture analysis: aggregate observations do not
  reliably identify more than three latent component distributions.
- Dependence is fixed, not inferred jointly with marginal parameters.
- Numerical differentiation for dependent density values can introduce a likelihood floor or instability in extreme tails.
- A direct constructor from family types inherits Numerics' minimum default. Always verify **MinimumOfRandomVariables**.
- Component-wise priors do not by themselves resolve weak identification from aggregate maxima.
- Tail extrapolation can be dominated by one component even when that component is poorly informed in the observed range.

## Implementation and Verification Traceability

| Concern | Implementation |
|---|---|
| BestFit likelihood and priors | **Models/UnivariateDistribution/CompetingRisksModel.cs** |
| Bayesian orchestration | **Analyses/Univariate/CompetingRiskAnalysis.cs** |
| Composite CDF/PDF and dependence | pinned **Numerics/Distributions/Univariate/CompetingRisks.cs** |
| Probability bounds/copula helpers | pinned **Numerics/Data/Statistics/Probability.cs** |
| Dependency simulation evidence | **Verification/Univariate/CompetingRiskTests/CompetingRiskDependencyVerificationTests.cs** |
| MLE and DEMCzs recovery evidence, with the stated maximum-model prior | **Verification/Univariate/CompetingRiskTests/CompetingRiskRecoveryTests.cs** and helper partial |

## References

<a id="ref-1"></a>[1] N. L. Johnson, S. Kotz, and N. Balakrishnan, *Continuous Univariate Distributions*, 2nd ed. New York, NY, USA: Wiley, 1994.

<a id="ref-2"></a>[2] J. Salvadori, C. De Michele, N. T. Kottegoda, and R. Rosso, *Extremes in Nature: An Approach Using Copulas*. Dordrecht, The Netherlands: Springer, 2007.

---

[Previous: Mixture Models](mixture.md) | [Technical Reference](../index.md) | [Next: Point-Process Models](point-process.md)
