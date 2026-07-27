<!-- technical-reference-status: complete -->

# Model Comparison and Predictive Criteria

[Estimation index](index.md) | [MLE](maximum-likelihood.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Influence diagnostics](influence-diagnostics.md) | [Technical Reference](../index.md)

Model comparison in BestFit spans likelihood criteria (AIC/BIC), posterior criteria (DIC/WAIC), and Pareto-smoothed leave-one-out cross-validation (LOOIC). These quantities answer different questions. They are comparable only when candidates use the same observations, observation process, pointwise partition, response transformation including Jacobian, and likelihood constants.

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

`MaximumAPosteriori` and the Bayesian analysis summaries use the same formulas with the data log likelihood evaluated at the posterior mode, $\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})$; prior log densities are excluded. When every active prior is constant over the relevant parameter region, MAP coincides with the constrained MLE and these values are comparable with (MC.1). With informative, Jeffreys, quantile, or other nonconstant priors, MAP generally differs from MLE and the classical AIC/BIC penalties do not account for the prior. In that setting, use DIC, WAIC, or verified PSIS-LOO for Bayesian comparison rather than interpreting the MAP-evaluated fields as conventional AIC/BIC. See [TR-011](../review-findings.md#tr-011) and the MAP chapter.

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

A deterministic 40-draw Normal fixture verifies the full calculation against R `BayesianTools` 0.1.9. BestFit agrees within $10^{-10}$ for $\overline D$, $D(\overline{\boldsymbol\theta})$, $p_D$, and DIC. See the [verification report](../../verification/model-estimation.md#dic-and-waic-external-package-parity) and [committed oracle](../../../verification/data/model-estimation/model-comparison-oracle.json).

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

The same deterministic fixture verifies every pointwise data-log-likelihood value and the aggregate lppd, $p_{\mathrm{WAIC}}$, expected log predictive density, and WAIC against R `loo` 2.10.0 within $10^{-10}$. See the [verification report](../../verification/model-estimation.md#dic-and-waic-external-package-parity) and [committed oracle](../../../verification/data/model-estimation/model-comparison-oracle.json).

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

BestFit's smoothing path follows R `loo` 2.10.0 for independent draws (`r_eff = 1`): it selects the reference tail length, fits positive cutoff excesses with the bounded fixed-grid `posterior::gpdfit` 1.7.0 estimator and shrinkage, replaces ordered tail ratios with monotone expected order statistics, and applies the reference truncation. The same transient pointwise matrix supplies WAIC and PSIS, so the model is evaluated once per retained draw. Pointwise ELPD and Pareto-$k$ arrays are cached for later influence reporting without retaining the $n\times S$ matrix.

The pinned R fixture verifies aggregate and pointwise LOO values, every smoothed weight, importance-sampling effective sample size, Pareto $k$, six bounded-through-degenerate tail regimes, and the sample-size reliability threshold. See [TR-024](../review-findings.md#tr-024), the [verification report](../../verification/model-estimation.md#psis-loo-and-pareto-diagnostics), and the [committed oracle](../../../verification/data/model-estimation/psis-loo-oracle.json).

For $S$ retained draws, BestFit uses the `loo` 2.10.0 reliability limit

$$
k_{\mathrm{threshold}}=\min\left(1-\frac{1}{\log_{10}S},0.7\right). \tag{MC.9}
$$

A pointwise value at or above this limit means the approximation needs investigation; $k\ge1$ lacks the usual finite-mean guarantee. A large Pareto $k$ diagnoses the importance-sampling approximation, not whether an observation is erroneous. BestFit does not run exact or moment-matched LOO refits automatically, and it does not currently estimate chain-relative efficiency for tail-length selection. If important observations exceed the limit, use an explicit sensitivity or refitting workflow.
## Comparing Models Responsibly

For criterion $C_m$ where lower is better, BestFit composite/model-average workflows can form relative weights of the familiar form

$$
\Delta_m=C_m-\min_r C_r,
\qquad
w_m=\frac{\exp(-\Delta_m/2)}{\sum_r\exp(-\Delta_r/2)}. \tag{MC.10}
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

The example reflects the API. Interpret DIC and WAIC with their documented posterior and pointwise limitations, and inspect Pareto $k$ before using LOOIC. Always pair a relative criterion with posterior predictive checks, parameter and tail plausibility, convergence diagnostics, and the engineering consequences of extrapolation.
## Verification and Traceability

The documentation test compiles the API example and checks its exact source region. Unit tests exercise arithmetic, threshold interpretation, legacy serialization compatibility, and state behavior. Focused verification establishes deterministic external-package parity for DIC, WAIC, and PSIS-LOO. The PSIS artifact independently verifies R `loo` 2.10.0 weights, effective sample size, pointwise and aggregate LOO values, standard errors, Pareto $k$, six tail regimes, and sample-size diagnostic thresholds. Call-count tests establish one pointwise evaluation per retained draw for default WAIC plus PSIS and no repeated evaluation when influence diagnostics are requested.
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
