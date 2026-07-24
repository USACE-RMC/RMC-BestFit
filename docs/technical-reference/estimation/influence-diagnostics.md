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

`BayesianAnalysis.ComputeInfluenceDiagnostics()` attaches the stored `ParetoK` and recomputed pointwise ELPD to `DataComponent` metadata. `InfluenceDiagnostics` summarizes counts above 0.5, 0.7, and 1.0 and calls the result reliable only when fewer than 1% exceed 0.7 and none exceed 1.0.

Those thresholds are meaningful only for correctly calculated PSIS with its sample-size/version rules. BestFit's current tail smoother fits cutoff ratios rather than excesses, ignores the fitted location, and reverses tail order-statistic replacement. Under [TR-024](../review-findings.md#tr-024), all BestFit `ParetoK`, ELPD-LOO, reliability flags, and PSIS observation rankings are scientifically unavailable until corrected.

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

For pointwise moment vector $\mathbf g_i$, Jacobian $\mathbf D$, weighting matrix $\mathbf W$, and bread $\mathbf B$, BestFit forms an influence-function-like vector

$$
\boldsymbol\psi_i=
\mathbf B^{-1}\mathbf D^{\mathsf T}\mathbf W\mathbf g_i. \tag{INF.6}
$$

For Bulletin 17C grouped components, row-level vectors are summed before a covariance quadratic is calculated. The returned scalar is a Cook-distance-like GMM measure. However, `GeneralizedMethodOfMoments.GetInfluenceDiagnostics()` stores that scalar in `ObservationInfluence.ParetoK`, because it reuses the PSIS-oriented DTO. Consequently, `GetProblematicObservations(0.7)`, Pareto threshold counts, and `GetReliabilitySummary()` apply PSIS semantics to a non-PSIS Cook measure. This is a production/API defect recorded in the review findings. Inspect the raw scalar only with a GMM-specific calibration; do not call it Pareto $k$.

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
{\max(1/V_{\mathrm{post},j},,1/V_{\pi,j})}, \tag{INF.9}
$$

clamped to $[0,1]$, with zero assigned when analytical prior variance is unavailable. This is invariant to a separate linear rescaling of one parameter, but not to general reparameterization or posterior correlation. It uses only `ModelParameter.PriorDistribution`; quantile, Jeffreys, coupled, and spatial prior components in (INF.7) are absent from (INF.9). The name “precision share” should therefore be read as a marginal heuristic, not a formal fraction of posterior information.

Time-series `AutoRegressive`, `MovingAverage`, and `ARIMA` pointwise methods currently classify their Jeffreys scale component as `ParameterPrior` rather than `JeffreysScalePrior`; type-filtered summaries undercount Jeffreys contributions for those models.

## Custom Leverage Decomposition

`LeverageDiagnostics` evaluates the full posterior Hessian at the supplied MCMC MAP state or MAP optimizer result. For observation $i$, it defines fit influence using (INF.5) and an observation curvature term

$$
V_i^{\mathrm{API}}=
\frac{1}{p}\left|
\operatorname{tr}(\mathbf J_{\mathrm{post}}^{-1}\mathbf J_i^{\mathrm{diag}})
\right|, \tag{INF.10}
$$

where the code constructs $\mathbf J_i^{\mathrm{diag}}$ from only the diagonal second derivatives of the pointwise log likelihood. Cross-parameter curvature is omitted. It then defines `Leverage = FitInfluence + VarianceInfluence`.

For a prior component, variance influence is instead the absolute log determinant ratio between a covariance with that prior removed and the full posterior covariance, divided by $p$. Adding this determinant metric to the score quadratic produces a useful exploratory ranking only if empirically validated; the two terms do not share a standard additive information identity. The documented expectation that total leverage approximately equals $p$ is not established for this implementation.

Numerical differentiation can also cross bounds or discontinuous model branches. Regularization or caught exceptions can return empty/zero diagnostics. Do not report percentages without retaining the raw Hessian condition, finite-difference scale, and failure state.

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
- Exclude PSIS results while TR-024 is open.
- Distinguish exact refitting from first-order score approximations.
- Never interpret zero arrays without checking Hessian/covariance failure.
- Treat prior log-magnitude and marginal precision ratios as heuristics.
- Treat GMM Cook measures as GMM quantities, not Pareto $k$.
- Preserve hydrologic context: an influential extreme may be correct and indispensable.
- Report sensitivity of design quantiles, not only parameter displacement.

## Verification and Traceability

Fast tests exercise serialization, sorting, thresholds, component mapping, and deterministic numerical helpers. They do not validate nominal influence thresholds or deletion approximations across model families. Release-grade verification requires exact leave-one-out/refit comparisons and reference PSIS parity; those long-running checks were not executed here.

Implementation symbols: `InfluenceDiagnostics`, `ObservationInfluence`, `PriorInfluenceDiagnostics`, `PriorComponentSummary`, `LeverageDiagnostics`, `MaximumLikelihood.GetObservationInfluence`, `MaximumLikelihood.GetCooksDistance`, matching MAP methods, and `GeneralizedMethodOfMoments.GetInfluenceDiagnostics`.

## References

<a id="ref-1"></a>[1] R. D. Cook, “Detection of influential observation in linear regression,” *Technometrics*, vol. 19, no. 1, pp. 15–18, 1977.

<a id="ref-2"></a>[2] S. Chatterjee and A. S. Hadi, “Influential observations, high leverage points, and outliers in linear regression,” *Statistical Science*, vol. 1, no. 3, pp. 379–393, 1986.

<a id="ref-3"></a>[3] A. Vehtari et al., “Pareto smoothed importance sampling,” *Journal of Machine Learning Research*, vol. 25, no. 72, pp. 1–58, 2024.

---

[Previous: diagnostics](diagnostics.md) | [Estimation index](index.md)
