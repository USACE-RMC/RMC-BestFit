<!-- technical-reference-status: complete -->

# Model Comparison and Predictive Criteria

[Estimation index](index.md) | [MLE](maximum-likelihood.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Influence diagnostics](influence-diagnostics.md) | [Technical Reference](../index.md)

Model comparison in BestFit spans likelihood criteria (AIC/BIC), posterior criteria (DIC/WAIC), and an intended Pareto-smoothed leave-one-out criterion (LOOIC). These quantities answer different questions. They are comparable only when candidates use the same observations, observation process, pointwise partition, response transformation including Jacobian, and likelihood constants.

## Pointwise Predictive Basis

Let $\ell_i^{(s)}=\log p(y_i\mid\boldsymbol\theta^{(s)})$ for posterior draw $s=1,\ldots,S$. BestFit obtains this matrix from `IModel.PointwiseDataLogLikelihood`; priors are deliberately excluded. For exact observations, an element ordinarily corresponds to one value. Censored intervals, uncertain observations, and threshold-count groups can be different information units. The data-frame chapter documents their decomposition.

Criterion values are not meaningful if two models partition the same record differently. In particular, expanding a threshold count into repeated contributions versus retaining one grouped binomial-like contribution changes pointwise variance and cross-validation interpretation even when aggregate likelihoods agree.

## AIC and BIC

For an MLE with $p$ fitted parameters and maximized data log likelihood $\widehat\ell$,

$$
\mathrm{AIC}=-2\widehat\ell+2p,
\qquad
\mathrm{BIC}=-2\widehat\ell+p\log n. \tag{MC.1}
$$

`MaximumLikelihood.GetAIC()` and `GetBIC(n)` implement (MC.1). AIC estimates relative expected out-of-sample deviance under regularity conditions; BIC is a large-sample approximation related to a particular marginal-likelihood regime. Lower is preferred only relative to the candidate set. Neither is a goodness-of-fit test.

The similarly named methods on `MaximumAPosteriori` and some analysis summaries substitute the posterior kernel at a MAP. Those values are nonstandard and must not be mixed with (MC.1); see [TR-011](../review-findings.md#tr-011) and the MAP chapter.

## Deviance Information Criterion

Define deviance from the data likelihood only:

$$
D(\boldsymbol\theta)=-2\sum_i\log p(y_i\mid\boldsymbol\theta). \tag{MC.2}
$$

BestFit computes posterior mean deviance $\overline D=S^{-1}\sum_sD(\boldsymbol\theta^{(s)})$, deviance at the componentwise posterior-mean vector $D(\overline{\boldsymbol\theta})$, and

$$
\mathrm{DIC}=2\overline D-D(\overline{\boldsymbol\theta})
=D(\overline{\boldsymbol\theta})+2p_D,
\quad
p_D=\overline D-D(\overline{\boldsymbol\theta}). \tag{MC.3}
$$

The public API exposes `DIC` but not its $p_D$ separately. DIC can behave poorly for skewed or multimodal posteriors, mixture label switching, constrained parameters, and cases where the posterior mean is a low-density or invalid representative point. WAIC or valid LOO is generally preferable for prediction-focused comparison.

## WAIC

BestFit uses the variance form of the effective parameter count. The log pointwise predictive density is

$$
\mathrm{lppd}
=\sum_{i=1}^{n}\log\left[
\frac1S\sum_{s=1}^{S}\exp(\ell_i^{(s)})
\right], \tag{MC.4}
$$

evaluated with a log-sum-exp shift. With unbiased sample variance across draws,

$$
p_{\mathrm{WAIC}}
=\sum_{i=1}^{n}\operatorname{Var}_{s}(\ell_i^{(s)}),
\qquad
\mathrm{WAIC}=-2\mathrm{lppd}+2p_{\mathrm{WAIC}}. \tag{MC.5}
$$

The properties are `WAIC_pD` and `WAIC`. Large pointwise variance signals weak WAIC reliability. Serial correlation among retained MCMC draws reduces Monte Carlo precision even though (MC.5) uses all output draws as supplied.

## Leave-One-Out Cross-Validation and PSIS

Exact leave-one-out predictive density for observation $i$ is

$$
p(y_i\mid\mathbf y_{-i})
=\int p(y_i\mid\boldsymbol\theta)
p(\boldsymbol\theta\mid\mathbf y_{-i})\,d\boldsymbol\theta. \tag{MC.6}
$$

Using draws from the full posterior gives raw importance ratios

$$
r_i^{(s)}\propto\frac{1}{p(y_i\mid\boldsymbol\theta^{(s)})}
=\exp(-\ell_i^{(s)}). \tag{MC.7}
$$

PSIS fits a generalized Pareto law to excesses over a high cutoff, replaces the ordered tail ratios with stabilized expected order statistics, and uses the fitted shape $k_i$ as a reliability diagnostic. With normalized smoothed weights $\widetilde w_i^{(s)}$,

$$
\widehat{\mathrm{elpd}}_{\mathrm{loo},i}
=\log\sum_s\widetilde w_i^{(s)}
\exp(\ell_i^{(s)}),
\qquad
\mathrm{LOOIC}=-2\sum_i\widehat{\mathrm{elpd}}_{\mathrm{loo},i}. \tag{MC.8}
$$

BestFit exposes `LOOIC`, `LOO_pD = lppd - elpd_loo`, `LOOIC_SE`, and one `ParetoK` per pointwise unit. Its standard error is $2\sqrt{n\widehat{\operatorname{Var}}_i(\widehat{\mathrm{elpd}}_{\mathrm{loo},i})}$.

The current `ParetoSmoothWeights()` is not a valid implementation of the PSIS tail replacement: it fits raw cutoff ratios rather than positive excesses, discards the fitted nonzero GPD location, and assigns ascending quantiles to descending tail indices. This is [TR-024](../review-findings.md#tr-024). Until corrected and verified against a reference implementation, treat `LOOIC`, `LOO_pD`, `LOOIC_SE`, `ParetoK`, and PSIS-based influence outputs as unavailable for scientific decisions.

After correction, $k$ thresholds must be tied to the implemented PSIS version and finite $S$, not treated as universal constants. Problematic observations should receive exact or moment-matched LOO refits rather than defaulting to WAIC without investigation.

## Comparing Models Responsibly

For criterion $C_m$ where lower is better, BestFit composite/model-average workflows can form relative weights of the familiar form

$$
\Delta_m=C_m-\min_r C_r,
\qquad
w_m=\frac{\exp(-\Delta_m/2)}{\sum_r\exp(-\Delta_r/2)}. \tag{MC.9}
$$

These are normalized relative scores, not posterior model probabilities unless the assumptions of a specific derivation are satisfied. AIC, BIC, DIC, WAIC, LOOIC, RMSE, and equal weights must not be mixed in one weighting calculation. Nonfinite child criteria and separately indexed posterior draws have additional composite-model caveats documented in the composite chapter.

For WAIC or LOO differences, uncertainty should be calculated from paired pointwise differences because candidate predictions for the same observations are correlated. Comparing two standalone `LOOIC_SE` values does not give the standard error of their difference.

## Compile-Checked API Workflow

<!-- snippet: model-comparison-workflow -->
```csharp
private static (
    double Dic,
    double Waic,
    double EffectiveParametersWaic,
    double Looic,
    double LooicStandardError) ReadBayesianCriteria(
        BayesianAnalysis analysis)
{
    if (!analysis.IsEstimated || analysis.Results is null)
    {
        throw new InvalidOperationException(
            "Run the Bayesian analysis before reading model criteria.");
    }

    return (
        analysis.DIC,
        analysis.WAIC,
        analysis.WAIC_pD,
        analysis.LOOIC,
        analysis.LOOIC_SE);
}
```

The example reflects the API but does not endorse every returned value. Under TR-024, use DIC/WAIC only with their documented limitations and do not use the LOO fields for peer-reviewed decisions. Always pair a relative criterion with posterior predictive checks, parameter/tail plausibility, convergence diagnostics, and engineering consequences of extrapolation.

## Verification and Traceability

The documentation test compiles the API example and checks its exact source region. Unit tests exercise arithmetic and state behavior. A release-grade acceptance test for PSIS must compare pointwise ELPD and $k$ against a named primary implementation over light-tailed, heavy-tailed, degenerate, and high-$k$ cases. That production correction requires separate authorization; no such validation claim is made here.

Implementation symbols: `MaximumLikelihood.GetAIC`, `GetBIC`, `BayesianAnalysis.ComputeDIC`, `ComputeWAIC`, `ComputePSISLOO`, `ParetoSmoothWeights`, `DIC`, `WAIC`, `WAIC_pD`, `LOOIC`, `LOO_pD`, `LOOIC_SE`, and `ParetoK`.

## References

<a id="ref-1"></a>[1] H. Akaike, “A new look at the statistical model identification,” *IEEE Transactions on Automatic Control*, vol. 19, no. 6, pp. 716–723, 1974.

<a id="ref-2"></a>[2] G. Schwarz, “Estimating the dimension of a model,” *The Annals of Statistics*, vol. 6, no. 2, pp. 461–464, 1978.

<a id="ref-3"></a>[3] D. J. Spiegelhalter et al., “Bayesian measures of model complexity and fit,” *Journal of the Royal Statistical Society: Series B*, vol. 64, no. 4, pp. 583–639, 2002.

<a id="ref-4"></a>[4] S. Watanabe, “Asymptotic equivalence of Bayes cross validation and widely applicable information criterion in singular learning theory,” *Journal of Machine Learning Research*, vol. 11, pp. 3571–3594, 2010.

<a id="ref-5"></a>[5] A. Vehtari, A. Gelman, and J. Gabry, “Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC,” *Statistics and Computing*, vol. 27, pp. 1413–1432, 2017.

<a id="ref-6"></a>[6] A. Vehtari et al., “Pareto smoothed importance sampling,” *Journal of Machine Learning Research*, vol. 25, no. 72, pp. 1–58, 2024.

---

[Previous: Bayesian MCMC](bayesian-mcmc.md) | [Next: predictive checks](predictive-checks.md)
