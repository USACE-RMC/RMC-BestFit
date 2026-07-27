<!-- technical-reference-status: complete -->

# Observation, Prior, and Leverage Diagnostics

[Estimation index](index.md) | [Diagnostics](diagnostics.md) | [Model comparison](model-comparison.md) | [Technical Reference](../index.md)

Influence diagnostics identify observations or prior components that materially affect fitted parameters, local uncertainty, or predictive accuracy. They are investigation tools, not automatic deletion rules. Extreme floods, paleoflood intervals, perception thresholds, and regional-skew information can be influential precisely because they contain the tail information the analysis was designed to use.

## PSIS-LOO Observation Influence

For posterior draws $\boldsymbol\theta^{(s)}$, deleting observation $i$ changes the posterior by an importance ratio proportional to

$$
r_i^{(s)}=\frac{1}{p(y_i\mid\boldsymbol\theta^{(s)})}. \tag{INF.1}
$$

Pareto-smoothed importance sampling fits a generalized Pareto distribution to the largest excess ratios. The fitted shape $k_i$ diagnoses how heavy that importance-weight tail is. A valid implementation combines smoothed normalized weights with pointwise likelihoods to estimate

$$
\widehat{\mathrm{elpd}}_{\mathrm{loo},i}
=\log\sum_s\widetilde w_i^{(s)}
p(y_i\mid\boldsymbol\theta^{(s)}). \tag{INF.2}
$$

`BayesianAnalysis.ComputeInfluenceDiagnostics()` attaches the cached `ParetoK` and pointwise ELPD values to `DataComponent` metadata. Default Bayesian completion evaluates the pointwise likelihood once per retained draw, shares the transient matrix between WAIC and PSIS, and retains only the $O(n)$ LOO summaries needed here.

For $S$ retained draws, Bayesian diagnostics use the R `loo` 2.10.0 limit $\min(1-1/\log_{10}S,0.7)$. Values below the limit are categorized as good; values from the limit to 0.7 are categorized as OK; values from 0.7 to 1.0 are bad; and values at or above 1.0 are very bad. The public fixed-threshold constructors, fixed 0.5/0.7/1.0 count properties, and legacy XML remain available for compatibility, while Bayesian reliability uses the draw-count limit and stores that limit in new XML.

The 40-draw external fixture has threshold $0.375803649418215$. Its third observation has $k=0.411627035764065$ and is correctly flagged. Aggregate and pointwise LOO, all five Pareto-$k$ values, six tail regimes, and single-pass evaluation behavior match the pinned R oracle. A high value means the PSIS approximation for that pointwise unit needs investigation; it is not an automatic outlier or deletion rule. The current implementation assumes `r_eff = 1` for tail-length selection and does not automatically perform exact or moment-matched refits.
## MLE and MAP Score-Displacement Diagnostics

At an MLE or MAP point $\widehat{\boldsymbol\theta}$, let $\mathbf J$ be the negative Hessian of the relevant full objective and let

$$
\mathbf s_i=
\left.\frac{\partial\ell_i}{\partial\boldsymbol\theta}
\right|_{\widehat{\boldsymbol\theta}}. \tag{INF.3}
$$

A first-order one-case deletion displacement is proportional to $\mathbf J^{-1}\mathbf s_i$. BestFit's `GetObservationInfluence()` returns each component standardized by its local standard error:

$$
I_{ij}=\frac{(\mathbf J^{-1}\mathbf s_i)_j}
{\sqrt{(\mathbf J^{-1})_{jj}}}. \tag{INF.4}
$$

`GetCooksDistance()` returns the score quadratic

$$
D_i^{\mathrm{API}}=
\frac{\mathbf s_i^{\mathsf T}\mathbf J^{-1}\mathbf s_i}{p}. \tag{INF.5}
$$

MLE uses the data-likelihood Hessian; MAP uses the full posterior Hessian but still uses a data score in (INF.3). These are local approximations and do not refit after deletion. The familiar linear-model suggestions $D_i>1$ or $4/n$ have no universal calibration for censored nonlinear flood-frequency models. Hessian inversion failure returns arrays of zeros, so “no influence” can also mean “diagnostic unavailable.”

Grouped threshold contributions require care. A pointwise unit with count $m$ represents a group, not one interchangeable exact observation. Its score and any deletion approximation correspond to removing that entire contribution as implemented.

## GMM Influence

For pointwise moment vector $\mathbf g_i$, Jacobian $\mathbf D$, weighting matrix $\mathbf W$, and penalized bread $\mathbf B$, define

$$
\mathbf e_i=\mathbf D^{\mathsf T}\mathbf W\mathbf g_i,
\qquad
\mathbf B=\mathbf D^{\mathsf T}\mathbf W\mathbf D+\mathbf H_P. \tag{INF.6}
$$

`GeneralizedMethodOfMoments.GetLeverageDiagnostics()` reports observation Cook fit influence and variance influence as

$$
D_i^{\mathrm{GMM}}
=\frac{\mathbf e_i^{\mathsf T}\boldsymbol\Sigma_Q\mathbf e_i}{n^2p},
\qquad
V_i^{\mathrm{GMM}}
=\frac{|\mathbf e_i^{\mathsf T}\mathbf B^{-1}\mathbf e_i|}{np}, \tag{INF.6a}
$$

where

$$
\boldsymbol\Sigma_Q=
\left\{\nabla^2\left(
\frac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n+P
\right)\right\}^{-1}. \tag{INF.6b}
$$

The diagnostic objective in (INF.6b) always uses the half-quadratic moment term, whether or not a penalty is enabled. This keeps observation scores, bread, Cook influence, and penalty curvature on one scale. It does not alter the optimizer's public `Q` convention, its estimating gradient, fitted parameters, penalty Hessian, or reported GMM covariance.

For Bulletin 17C grouped components, row-level $\mathbf e_i$ vectors are summed before either quadratic form is calculated, so removing a threshold group retains its full count effect. Penalty Cook influence uses its score quadratic; penalty variance influence uses the finite generalized-variance change described below. `Leverage` is the sum $D+V$ and supplies an estimator-specific ranking. MAP and GMM Cook magnitudes are not compared because their likelihood and estimating-equation objectives differ.

The seven-point Log10-Normal fixture is independently reproduced by R `gmm` 1.9.1 and `sandwich` 3.1.2. BestFit matches every observation and the aggregate values $\sum D_i=0.0880102040816327$, $\sum V_i=0.616071428571429$, and $\sum(D_i+V_i)=0.704081632653061$ within $10^{-5}$. A centered penalty with width $100SE_L$ leaves the observation scale unchanged within $10^{-4}$.

`GeneralizedMethodOfMoments.GetInfluenceDiagnostics()` is a legacy compatibility path that stores a different GMM Cook-like scalar in the PSIS-oriented `ObservationInfluence.ParetoK` member. Both overloads are marked `[Obsolete]` with a non-error compatibility warning. Their Pareto thresholds and reliability summaries are not GMM diagnostics. Use `GetLeverageDiagnostics()` for labeled fit/variance/combined GMM diagnostics or `GetCooksDistance()` for the raw Cook-like values; do not interpret the legacy compatibility value as Pareto $k$.

## Prior-Component Diagnostics

`PriorInfluenceDiagnostics` evaluates `PointwisePriorLogLikelihood` over every `thinEvery`-th posterior draw. For component $k$, it reports the mean, sample standard deviation, minimum, and maximum of

$$
\log\pi_k(\boldsymbol\theta^{(s)}). \tag{INF.7}
$$

It also computes the heuristic magnitude ratio

$$
R_{\pi}=
\frac{|E_s\log\pi(\boldsymbol\theta^{(s)})|}
{|E_s\log\pi(\boldsymbol\theta^{(s)})|+
|E_s\ell(\boldsymbol\theta^{(s)})|}. \tag{INF.8}
$$

`IsPriorInfluential` uses $R_\pi>0.2$. Equation (INF.8) is not invariant to data units, likelihood constants, prior normalization constants, sample size, or parameterization. A bounded flat prior can constrain inference while contributing a constant log density; an improper prior's constant is arbitrary. Treat this as a descriptive log-kernel decomposition only.

For parameter $j$, the class additionally reports

$$
S_{\pi,j}=
\frac{1/V_{\pi,j}}
{\max(1/V_{\mathrm{post},j},1/V_{\pi,j})}, \tag{INF.9}
$$

clamped to $[0,1]$, with zero assigned when analytical prior variance is unavailable. This is invariant to a separate linear rescaling of one parameter, but not to general reparameterization or posterior correlation. It uses only `ModelParameter.PriorDistribution`; quantile, Jeffreys, coupled, and spatial prior components in (INF.7) are absent from (INF.9). The name “precision share” should therefore be read as a marginal heuristic, not a formal fraction of posterior information.

Time-series `AutoRegressive`, `MovingAverage`, and `ARIMA` pointwise methods currently classify their Jeffreys scale component as `ParameterPrior` rather than `JeffreysScalePrior`; type-filtered summaries undercount Jeffreys contributions for those models.

## MAP Fit, Variance, and Combined Influence

`LeverageDiagnostics` evaluates the posterior Hessian at the supplied MCMC MAP state or MAP optimizer result. For observation $i$, fit influence is the Cook score quadratic in (INF.5). Observation variance influence is the local curvature trace

$$
V_i^{\mathrm{MAP}}=
\frac{1}{p}\left|
\operatorname{tr}(\mathbf J_{\mathrm{post}}^{-1}\mathbf J_i^{\mathrm{diag}})
\right|, \tag{INF.10}
$$

where $\mathbf J_i^{\mathrm{diag}}$ contains the diagonal second derivatives of the pointwise log likelihood. This first-order observation calculation is inexpensive because it reuses the forward and backward evaluations used for scores.

For prior component $k$, the component can supply a substantial share of posterior curvature, so variance influence uses the finite log generalized-variance change

$$
V_k^{\mathrm{prior}}
=\frac1p\left|
\log\det(\boldsymbol\Sigma_{-k})-
\log\det(\boldsymbol\Sigma)
\right|. \tag{INF.11}
$$

Here $\boldsymbol\Sigma_{-k}$ is the inverse reduced posterior curvature evaluated at the fitted mode after removing component $k$. Prior fit influence remains the Cook score quadratic. The displayed combined leverage is

$$
L_k=D_k+V_k. \tag{INF.12}
$$

This is an additive ranking index, not classical hat-matrix leverage or a conserved information decomposition. It is not expected to sum to $p$. The plot therefore labels each stacked bar as a percentage of total combined influence.

The deterministic Log10-Normal tests establish the intended interpretation. A wide centered Gaussian prior has negligible fit and variance influence; a narrow centered prior has negligible fit influence and strong variance influence; and a narrow prior shifted by $2SE_L$ has both. With the centered prior held fixed, variance influence decreases as the sample grows. Sigma is reestimated jointly in every fit.

For the displaced narrow-prior fixture, replacing $\mathbf J_i^{\mathrm{diag}}$ in (INF.10) with the analytical full Log10-Normal observation Hessian changes every reported value by less than $0.003$ and preserves the leading three observations. That result supports the local approximation for this verified case, not for every model family.

Numerical differentiation can cross bounds or discontinuous model branches. Regularization or caught exceptions can return empty or zero diagnostics. Retain the raw values, fitted model, finite-difference configuration, and covariance status when using rankings in an engineering review.

## Compile-Checked API Workflow

<!-- snippet: bayesian-diagnostics-workflow -->
```csharp
private static (
    InfluenceDiagnostics Influence,
    PriorInfluenceDiagnostics PriorInfluence,
    LeverageDiagnostics Leverage) ComputeBayesianDiagnostics(
        BayesianAnalysis analysis)
{
    if (!analysis.IsEstimated || analysis.Results is null)
    {
        throw new InvalidOperationException(
            "Run the Bayesian analysis before computing diagnostics.");
    }

    return (
        analysis.ComputeInfluenceDiagnostics(),
        analysis.ComputePriorInfluenceDiagnostics(thinEvery: 10),
        analysis.ComputeLeverageDiagnostics());
}
```

Use the returned metadata to locate observations in the source record, then investigate chronology, measurement quality, censoring definitions, and model assumptions. Do not delete an observation solely because it ranks highly. Refit-with-and-without sensitivity analysis should preserve a documented scientific reason and distinguish data correction from robustness analysis.

## Interpretation Checklist

- Confirm that pointwise components correspond to independent deletion units.
- Inspect each Pareto $k$ against the draw-count reliability limit before using PSIS results.
- Distinguish exact refitting from first-order score approximations.
- Never interpret zero arrays without checking Hessian/covariance failure.
- Treat prior log-magnitude and marginal precision ratios as heuristics.
- Use GMM Cook measures only on the GMM scale; never relabel the legacy compatibility value as Pareto $k$.
- Preserve hydrologic context: an influential extreme may be correct and indispensable.
- Report sensitivity of design quantiles, not only parameter displacement.

## Verification and Traceability

Fast tests exercise serialization, draw-count threshold persistence, legacy XML shape, sorting, percentages, component mapping, and UI labels. Focused Log10-Normal methods verify MAP prior regimes, GMM penalty regimes, sample-size attenuation, MAP curvature materiality, and pointwise/aggregate parity with R `gmm`. Six exact R `loo` 2.10.0 methods verify PSIS identities, corrected BestFit aggregate and pointwise output, every tail-regime weight and Pareto $k$, diagnostic classification, and the single-pass pointwise-likelihood contract. These results do not establish universal Cook thresholds or exact deletion parity.
Implementation symbols: `InfluenceDiagnostics`, `ObservationInfluence`, `PriorInfluenceDiagnostics`, `PriorComponentSummary`, `LeverageDiagnostics`, `MaximumLikelihood.GetObservationInfluence`, `MaximumLikelihood.GetCooksDistance`, matching MAP methods, and `GeneralizedMethodOfMoments.GetInfluenceDiagnostics`.

## References

<a id="ref-1"></a>[1] R. D. Cook, “Detection of influential observation in linear regression,” *Technometrics*, vol. 19, no. 1, pp. 15–18, 1977.

<a id="ref-2"></a>[2] S. Chatterjee and A. S. Hadi, “Influential observations, high leverage points, and outliers in linear regression,” *Statistical Science*, vol. 1, no. 3, pp. 379–393, 1986.

<a id="ref-3"></a>[3] A. Vehtari et al., “Pareto smoothed importance sampling,” *Journal of Machine Learning Research*, vol. 25, no. 72, pp. 1–58, 2024.

---

[Previous: diagnostics](diagnostics.md) | [Estimation index](index.md)
