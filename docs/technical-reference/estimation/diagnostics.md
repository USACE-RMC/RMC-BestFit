<!-- technical-reference-status: complete -->

# Estimation and Convergence Diagnostics

[Estimation index](index.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Influence diagnostics](influence-diagnostics.md) | [Predictive checks](predictive-checks.md) | [Technical Reference](../index.md)

Diagnostics answer three separate questions: whether the numerical algorithm terminated as intended, whether Monte Carlo output represents a stable posterior calculation, and whether the scientific model can reproduce relevant features of the data. No single statistic answers all three.

## Deterministic Estimator Diagnostics

For MLE and MAP, `Estimate()` is successful only when the Numerics optimizer returns `OptimizationStatus.Success`. Retain at least:

- optimizer method, start, bounds, status, and function-evaluation count;
- fitted objective and parameter vector;
- whether numerical Hessian construction succeeded;
- covariance regularization or zero-matrix fallback;
- results from materially different starts or global-search seeds.

A value at a bound, an ill-conditioned Hessian, or large disagreement across starts is a substantive finding. `ReportFailure = false` prevents the underlying optimizer from throwing for some terminations; it does not convert them into successful fits.

GMM uses a broader best-effort contract. `IsEstimated` can be true when a finite best vector survives a nonfailure termination, while `ConvergedWithinTolerance` remains false. Report `Status`, `GMMIterations`, `ConvergenceHistory`, final $Q$, $\mathbf S$, $\mathbf W$, covariance condition, and fallback count where available. Do not publish the current `JStat` as Hansen's overidentification statistic until the discrepancy documented in the review register is resolved.

## Trace and Chain Inspection

For parameter $\theta_j$, a trace plot of every chain after warmup should show repeated overlap across the same stationary region without persistent drift, long plateaus, isolated modes, or chain-specific levels. This visual check detects pathologies that a scalar summary can miss. Also inspect marginal density, pair plots for posterior ridges or label switching, and autocorrelation.

BestFit stores two related collections:

- `MarkovChains`: the main recorded chain, including warmup; R-hat discards the first `WarmupIterations` entries.
- `Output`: a separate post-main-run collection with `OutputLength` pooled draws; posterior summaries and ESS use this collection.

Plotting only `Output` hides the warmup trajectory. Plotting all of `MarkovChains` without marking warmup can make a stable calculation appear nonstationary. A reviewer-facing diagnostic package should show both roles explicitly.

## Implemented R-hat

Pinned Numerics computes classical unsplit Gelman–Rubin R-hat. With $m$ chains and $n$ post-warmup states per chain, define chain means $\bar\theta_c$, overall mean $\bar\theta$, within-chain variance $W$, and between-chain variance

$$
B=\frac{n}{m-1}\sum_{c=1}^{m}(\bar\theta_c-\bar\theta)^2. \tag{DGN.1}
$$

Then

$$
\widehat V=\frac{n-1}{n}W+\frac{B}{n},
\qquad
\widehat R=\sqrt{\widehat V/W}. \tag{DGN.2}
$$

BestFit's generated report uses $\widehat R<1.10$ as its readiness threshold. The implementation does not split chains, rank-normalize, fold tails, or localize diagnostics. Source comments that refer to split R-hat and bulk/tail ESS are therefore inaccurate. A value below 1.10 is not proof of convergence, especially for heavy tails, multimodality, or scale differences that do not change first two moments.

For external peer review, supplement the current output with rank-normalized split R-hat and folded/tail diagnostics calculated from the serialized chains, or correct Numerics in a separately authorized task.

## Implemented Effective Sample Size

Numerics calculates autocorrelation within each `Output` chain and, for each chain, sums lags until the first negative autocorrelation. If $\bar\rho_+$ is the average of those positive sums,

$$
\widehat N_{\mathrm{eff}}
=\min\left\{\frac{mn}{1+2\bar\rho_+},mn\right\}. \tag{DGN.3}
$$

This is one scalar ESS per parameter. It is not rank-normalized bulk ESS or tail ESS. BestFit warns below 400, below 10% retained-draw efficiency, and gives a note below 25% efficiency. These thresholds are pragmatic report rules, not a guarantee that a 0.99 flood quantile or 0.005 tail probability has adequate Monte Carlo precision.

For an estimated posterior mean with marginal standard deviation $s_j$, a rough Monte Carlo standard error is

$$
\operatorname{MCSE}(\bar\theta_j)\approx
\frac{s_j}{\sqrt{N_{\mathrm{eff},j}}}. \tag{DGN.4}
$$

Quantile MCSE needs a quantile-specific method. Report reproducibility across independent seeds and compute tail ESS before relying on rare-event credible limits.

## Acceptance and Sampler-Specific Diagnostics

For DEMCz, DEMCzs, and ARWMH, `AcceptanceRates[c]` is accepted raw transitions divided by attempted transitions for chain $c$. BestFit report bands are 0.23–0.44 for DE-MC and 0.20–0.30 for ARWMH, with wider warning buffers. Acceptance is an efficiency diagnostic, not a convergence test; an apparently good rate can coexist with mode trapping.

NUTS requires different diagnostics: average Hamiltonian acceptance statistic, divergences, maximum-tree-depth saturation, energy behavior, and step size. Pinned Numerics internally accumulates the acceptance statistic for dual averaging, but its generic counter increments on every NUTS iteration. Consequently, the exposed acceptance rate is one. BestFit currently compares that value with a 0.65–0.90 band and can issue a spurious warning. Do not use `AcceptanceRates` for NUTS tuning until this is corrected.

The public BestFit result also does not expose divergence counts or tree-depth saturation. A NUTS run without those diagnostics is incomplete for peer-review purposes. DEMCzs remains the defensible default until gradient-based diagnostics and bounded-parameter treatment are strengthened.

## Posterior Summary Checks

`MCMCResults.ParameterResults` reports mean, sample standard deviation, median, equal-tailed credible limits, classical R-hat, ESS, and an averaged autocorrelation function. Check:

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

`LeverageDiagnostics` defines an observation fit term and a curvature term, then adds them into a custom “total leverage.” Its observation curvature matrix retains only diagonal second derivatives, omitting cross-parameter curvature. Prior components use a different determinant-ratio variance measure. Adding these heterogeneous quantities does not have a demonstrated identity that sums to $p$, despite current property comments. Treat this display as exploratory and see the influence chapter and review findings before using it in a report.

## Model Adequacy

Convergence establishes only that the algorithm appears to sample its target. It does not establish that the target model is useful. After numerical checks:

1. perform prior predictive checks before interpreting the fit;
2. perform observation-process-aware posterior predictive checks;
3. compare empirical and fitted central behavior and upper tail;
4. examine residual/trend/dependence structure appropriate to the model;
5. review influential observations without deleting them automatically;
6. assess sensitivity to priors, censoring thresholds, outlier treatment, parent family, and nonstationarity.

The current PSIS-LOO implementation is defective under TR-024, so PSIS-based influence and LOO model comparison are not acceptance evidence until corrected.

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

The example shows the exact API, not an endorsement of all three outputs. `Influence` inherits TR-024, `PriorInfluence` contains scale-dependent exploratory summaries, and `Leverage` is a custom local decomposition. A defensible workflow retains these objects for investigation while basing release readiness on trace review, corrected modern convergence diagnostics, predictive checks, and model-specific verification.

## Minimum Reviewer Checklist

- Target decomposition (`DataLogLikelihood` plus every prior component) is reconciled.
- At least four chains and their warmup/post-warmup traces are shown.
- No unexplained bound contact, divergent target values, or label switching remains.
- Classical versus rank-normalized diagnostic definitions are stated accurately.
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
