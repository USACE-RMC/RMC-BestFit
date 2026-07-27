<!-- technical-reference-status: complete -->

# Estimation and Convergence Diagnostics

[Estimation index](index.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Influence diagnostics](influence-diagnostics.md) | [Predictive checks](predictive-checks.md) | [Technical Reference](../index.md)

Diagnostics answer three separate questions: whether the numerical algorithm terminated as intended, whether Monte Carlo output represents a stable posterior calculation, and whether the scientific model can reproduce relevant features of the data. No single statistic answers all three.

## Deterministic Estimator Diagnostics

For MLE and MAP, `Estimate()` is successful only when the Numerics optimizer returns `OptimizationStatus.Success`. Retain at least:

- optimizer method, start, bounds, status, and function-evaluation count;
- fitted objective and parameter vector;
- whether numerical Hessian construction succeeded;
- covariance status and any regularization or failure diagnostic;
- results from materially different starts or global-search seeds.

A value at a bound, an ill-conditioned Hessian, or large disagreement across starts is a substantive finding. `ReportFailure = false` prevents the underlying optimizer from throwing for some terminations; it does not convert them into successful fits.

GMM uses a broader best-effort contract. `IsEstimated` can be true when a finite best vector survives a nonfailure termination, while `ConvergedWithinTolerance` remains false. Report `Status`, `GMMIterations`, `ConvergenceHistory`, final $Q$, $\mathbf S$, $\mathbf W$, covariance condition, and fallback count where available. `JStat` has the Hansen chi-squared interpretation only for unpenalized overidentified `TwoStep` or `Iterative` fits, where BestFit evaluates the selected efficient-weight objective; generic fixed-weight `OneStep` and penalized fits leave `JStat` and its p-value unset.

## Trace and Chain Inspection

For parameter $\theta_j$, a trace plot of every chain after warmup should show repeated overlap across the same stationary region without persistent drift, long plateaus, isolated modes, or chain-specific levels. This visual check detects pathologies that a scalar summary can miss. Also inspect marginal density, pair plots for posterior ridges or label switching, and autocorrelation.

BestFit stores two related collections:

- `MarkovChains`: the main recorded chain, including warmup; R-hat discards the first `WarmupIterations` entries.
- `Output`: a separate post-main-run collection with `OutputLength` pooled draws; posterior summaries and ESS use this collection.

Plotting only `Output` hides the warmup trajectory. Plotting all of `MarkovChains` without marking warmup can make a stable calculation appear nonstationary. A reviewer-facing diagnostic package should show both roles explicitly.

## Rank-Normalized Split and Folded R-hat

Numerics implements the rank-normalized split and folded R-hat of Vehtari et al. Each post-warmup chain is divided into two half-chains; the middle draw is discarded when the retained length is odd. Pooled midranks $r_s$ are converted to normal scores with Blom's transform

$$
z_s=\Phi^{-1}\!\left(\frac{r_s-3/8}{S+1/4}\right), \tag{DGN.1}
$$

where $S$ is the pooled number of split-chain draws. The usual between/within-chain variance ratio is calculated from these scores. A second value is calculated after folding the original draws around their pooled median. The stored diagnostic is

$$
\widehat R=\max\!\left(\widehat R_{\mathrm{rank}},
\widehat R_{\mathrm{folded\ rank}}\right). \tag{DGN.2}
$$

Rank normalization improves behavior for heavy-tailed marginals, splitting detects within-chain drift, and folding detects scale disagreement even when chains have similar centers. BestFit's concise generated report uses $\widehat R<1.01$ as its readiness threshold.

A value near one means the split chains have comparable rank and folded-rank variation. A value at or above 1.01 identifies unresolved location, scale, or nonstationarity disagreement. It remains a screening diagnostic rather than proof of convergence; trace plots and sampler-specific diagnostics remain necessary. A single original chain, an insufficient retained length, constant draws, or nonfinite draws produce `NaN` rather than a reassuring value.

## Rank-Normalized Bulk and Tail Effective Sample Size

Numerics rank-normalizes and splits the retained chains, estimates each chain's autocovariance with zero-padded FFTs, and combines them with the multi-chain between/within-chain variance estimate. Geyer's initial-positive and initial-monotone paired autocorrelation sequence gives the integrated autocorrelation time $\widehat\tau$ and

$$
\widehat N_{\mathrm{eff}}
=\frac{mn}{\widehat\tau}. \tag{DGN.3}
$$

Bulk ESS is computed from rank-normalized draws. Lower- and upper-tail ESS use indicators at the pooled 0.05 and 0.95 quantiles. The existing scalar `ESS` field stores the conservative minimum of bulk, lower-tail, and upper-tail ESS, so downstream APIs and serialized results remain unchanged. BestFit warns below 400, below 10% retained-draw efficiency, and gives a note below 25% efficiency. These report rules do not guarantee adequate precision for more extreme probabilities.

The stored scalar is deliberately conservative across central and tail behavior. The existing 51-lag original-scale averaged autocorrelation output remains available for plots; it is not reused as the modern ESS estimator. ESS is an estimated precision equivalence, not a count of unique or accepted draws.

Interpret both the absolute ESS and the efficiency ratio `ESS / retained draws`. Negative autocorrelation can legitimately produce ESS above the retained draw count, so the implementation follows the `posterior` estimator rather than imposing a draw-count cap. Stability across longer runs and independent seeds remains the practical check for the quantity being reported.

For an estimated posterior mean with marginal standard deviation $s_j$, a rough Monte Carlo standard error is

$$
\operatorname{MCSE}(\bar\theta_j)\approx
\frac{s_j}{\sqrt{N_{\mathrm{eff},j}}}. \tag{DGN.4}
$$

Quantile MCSE needs a quantile-specific method. Report reproducibility across independent seeds and compute tail ESS before relying on rare-event credible limits.

## Acceptance and Sampler-Specific Diagnostics

For DEMCz, DEMCzs, and ARWMH, `AcceptanceRates[c]` is accepted raw transitions divided by attempted transitions for chain $c$. BestFit report bands are 0.23–0.44 for DE-MC and 0.20–0.30 for ARWMH, with wider warning buffers. Acceptance is an efficiency diagnostic, not a convergence test; an apparently good rate can coexist with mode trapping.

For NUTS, `AcceptanceRates[c]` is the mean post-warmup Hamiltonian acceptance statistic for chain $c$. `MCMCResults` also exposes post-warmup diagnostic transition count, divergences, maximum-tree-depth hits, mean tree depth, mean leapfrog steps, final step size, and E-BFMI. BestFit applies NUTS-specific report wording and uses a target Hamiltonian acceptance of 0.80 rather than interpreting the retained-state counter as a Metropolis rate.

E-BFMI is computed per chain as $\operatorname{mean}(\Delta E^2)/\operatorname{var}(E)$ from streaming post-warmup energies. BestFit warns below 0.2, matching the current [`rstan::check_energy()`](https://mc-stan.org/rstan/reference/check_hmc_diagnostics.html) diagnostic. CmdStan's [`diagnose`](https://mc-stan.org/docs/2_39/cmdstan-guide/diagnose_utility.html) command uses the more conservative nominal threshold of 0.3, so values between 0.2 and 0.3 still merit scrutiny. Any divergence, repeated maximum-depth saturation, low E-BFMI, poor R-hat/ESS, or posterior mass against parameter bounds requires investigation. These diagnostics improve visibility but do not remove the limitations of finite differences and direct bounded-parameter integration; compare trace behavior and ESS with DEMCzs when the NUTS result is sensitive or consequential.

## Posterior Summary Checks

`MCMCResults.ParameterResults` reports mean, sample standard deviation, median, equal-tailed credible limits, rank-normalized split/folded R-hat, conservative bulk/tail ESS, and an averaged original-scale autocorrelation function. Check:

- support and units for every marginal summary;
- whether posterior mass touches API bounds;
- multimodality or label switching hidden by the mean;
- sensitivity to prior choices and record-information assumptions;
- joint correlation relevant to transformed flood quantiles;
- stability under seed, chain count, and longer runs.

Do not declare readiness from rounded R-hat and ESS alone. Analysis-specific posterior outputs should be regenerated from the joint draws, not assembled from marginal endpoints.

## Curvature, Leverage, and Influence

BestFit supplies local Hessian/score diagnostics for MLE, MAP, GMM, and Bayesian MAP states. These are first-order approximations. The general observation score is

$$
\mathbf s_i(\boldsymbol\theta)=
\frac{\partial\ell_i(\boldsymbol\theta)}{\partial\boldsymbol\theta}, \tag{DGN.5}
$$

and the estimator classes return standardized displacement approximations based on $\mathbf J^{-1}\mathbf s_i$. `GetCooksDistance()` uses $\mathbf s_i^{\mathsf T}\mathbf J^{-1}\mathbf s_i/p$; it is Cook's-distance-like, not the exact deletion diagnostic from linear regression, so familiar cutoffs such as 1 or $4/n$ are heuristics only.

`LeverageDiagnostics` reports fit influence, variance influence, and their sum as combined leverage. Fit influence is the Cook score quadratic described above. Observation variance influence is a local curvature-trace approximation, while prior and penalty variance influence use the change in finite log generalized variance when that component is removed.

Combined leverage is an additive ranking index for those two effects. It is not a classical hat-matrix diagonal, is not expected to sum to the parameter count, and has no universal intervention threshold. Use it to compare components within the same fitted analysis, with fit and variance contributions shown separately; do not compare its absolute magnitude across estimators or model families without model-specific calibration. See the [influence chapter](influence-diagnostics.md) and [model-estimation verification](../../verification/model-estimation.md#fit-influence-variance-influence-and-combined-leverage) for definitions and scoped validation.

## Model Adequacy

Convergence establishes only that the algorithm appears to sample its target. It does not establish that the target model is useful. After numerical checks:

1. perform prior predictive checks before interpreting the fit;
2. perform observation-process-aware posterior predictive checks;
3. compare empirical and fitted central behavior and upper tail;
4. examine residual/trend/dependence structure appropriate to the model;
5. review influential observations without deleting them automatically;
6. assess sensitivity to priors, censoring thresholds, outlier treatment, parent family, and nonstationarity.

PSIS-LOO is externally verified against pinned R `loo` 2.10.0 for aggregate and pointwise results, six tail regimes, draw-count thresholds, and single-pass evaluation. Inspect every Pareto $k$ before using LOO model comparison or observation influence; values at or above the draw-count reliability limit require investigation, and the default path does not perform exact refits.

## Compile-Checked Diagnostic Workflow

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

The example shows the exact API. `Influence` uses verified PSIS numerics but still requires Pareto-k review; `PriorInfluence` contains scale-dependent exploratory summaries; and `Leverage` is a custom local decomposition. A defensible workflow retains these objects for investigation while basing release readiness on trace review, appropriately interpreted convergence diagnostics, predictive checks, and model-specific verification.

## Minimum Reviewer Checklist

- Target decomposition (`DataLogLikelihood` plus every prior component) is reconciled.
- At least four chains and their warmup/post-warmup traces are shown.
- No unexplained bound contact, divergent target values, or label switching remains.
- Rank-normalized split/folded R-hat and conservative bulk/tail ESS definitions are stated accurately.
- ESS is adequate for the actual posterior functional, especially extreme quantiles.
- Predictive checks reproduce the observation process and hydrologic structure.
- Criterion and influence outputs affected by open findings are excluded from decisions.
- Seeds, settings, versions, dependency commit, and valid/failed simulations are reported.

Implementation symbols: `MCMCResults`, `MCMCDiagnostics.GelmanRubin`, `EffectiveSampleSize`, `BayesianAnalysis.GenerateReport`, `MaximumLikelihood`, `MaximumAPosteriori`, `GeneralizedMethodOfMoments`, `InfluenceDiagnostics`, `PriorInfluenceDiagnostics`, and `LeverageDiagnostics`.

## References

<a id="ref-1"></a>[1] A. Gelman and D. B. Rubin, “Inference from iterative simulation using multiple sequences,” *Statistical Science*, vol. 7, no. 4, pp. 457–472, 1992.

<a id="ref-2"></a>[2] A. Vehtari et al., “Rank-normalization, folding, and localization: an improved $\widehat R$ for assessing convergence of MCMC,” *Bayesian Analysis*, vol. 16, no. 2, pp. 667–718, 2021.

<a id="ref-3"></a>[3] J. M. Flegal, M. Haran, and G. L. Jones, “Markov chain Monte Carlo: can we trust the third significant figure?” *Statistical Science*, vol. 23, no. 2, pp. 250–260, 2008.

<a id="ref-4"></a>[4] R. D. Cook, “Detection of influential observation in linear regression,” *Technometrics*, vol. 19, no. 1, pp. 15–18, 1977.

---

[Previous: predictive checks](predictive-checks.md) | [Next: influence diagnostics](influence-diagnostics.md)
