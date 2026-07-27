<!-- technical-reference-status: complete -->

# Bayesian Univariate Analysis

[Previous: Distribution Fitting](distribution-fitting.md) | [Technical Reference](../index.md) | [Next: Composite Analysis](composite.md)

`UnivariateAnalysis` is the analysis-layer workflow for one `UnivariateDistribution`. It validates the model and sampler, prepares observation and prior structures, runs Bayesian MCMC, and converts retained parameter draws into frequency and—when applicable—nonstationary chronology results. The analysis is Bayesian-first: a frequency curve is a posterior-derived quantity, not an MLE curve with an attached generic confidence band.

## Statistical Target

Let (D) denote the complete `DataFrame`, (oldsymbol\theta) the model parameter vector in the order exposed by `Parameters`, and (M) the selected family and trend specification. The sampler target is

$$
p(\boldsymbol\theta\mid D,M)
=\frac{L(D\mid\boldsymbol\theta,M)\,p(\boldsymbol\theta\mid M)}
{\int_{\Theta}L(D\mid\boldsymbol\vartheta,M)
p(\boldsymbol\vartheta\mid M)\,d\boldsymbol\vartheta}. \tag{1}
$$

Equivalently, the implemented log target is

$$
\ell(\boldsymbol\theta)
=\ell_D(\boldsymbol\theta)+\ell_P(\boldsymbol\theta), \tag{2}
$$

where (ell_D) contains exact, uncertain, interval-censored, and threshold-count contributions, and (ell_P) contains parameter-prior densities, optional scale terms, quantile-prior densities, and any required transformation Jacobian. The exact decomposition is specified in [Data Frame and Observation Likelihood](../data-frame/index.md) and [Parameters and Priors](../models/parameters-and-priors.md).

For a stationary distribution with CDF (F(\cdot\mid\boldsymbol\theta)), the return level at annual exceedance probability (alpha) is

$$
q_{\alpha}(\boldsymbol\theta)
=F^{-1}(1-\alpha\mid\boldsymbol\theta). \tag{3}
$$

`ProbabilityOrdinates` stores (alpha), not the nonexceedance probability. The implementation therefore passes (1-\alpha) to the quantile function. If annual observations are independent and identically distributed, the conventional return period is (T=1/\alpha) years; that interpretation is not generally a waiting-time guarantee and requires additional care for nonstationary models.

## Run Lifecycle

`RunAsync` performs the following implemented sequence:

1. Validate the `UnivariateDistribution`, `ProbabilityOrdinates`, and `BayesianAnalysis` settings.
2. Raise the cancellable preview event and establish a new cancellation source.
3. Wait for any in-flight result-only reprocessing, then clear prior MCMC and derived results.
4. Reprocess perception-threshold records; for a nonstationary model, construct the full indexed time series.
5. Transform configured quantile priors into the representation consumed by the likelihood.
6. Run `BayesianAnalysis` against the complete posterior target.
7. If MCMC succeeds, construct frequency results and, for a nonstationary model, chronology results.
8. Mirror the inner Bayesian estimator's success state and raise a completion event carrying success, cancellation, or exception information.

`CancelAnalysis()` cancels both the outer analysis and the sampler. Exceptions raised inside the workflow are reported through the completion event; callers should inspect `IsEstimated`, `BayesianAnalysis.IsEstimated`, and non-null results rather than assuming that an awaited task necessarily produced a fit.

## Compile-Checked Workflow

The code below is compiled against the current API. Production studies should set the sampler configuration deliberately and assess convergence as described in [Bayesian MCMC](../estimation/index.md).

<!-- snippet: analysis-validated-run -->
```csharp
private static async Task<UnivariateAnalysis> RunValidatedAnalysis(
    UnivariateDistribution model)
{
    var analysis = new UnivariateAnalysis(model);
    var validation = analysis.Validate();
    if (!validation.IsValid)
    {
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, validation.ValidationMessages));
    }

    await analysis.RunAsync();
    if (!analysis.IsEstimated || analysis.AnalysisResults is null)
    {
        throw new InvalidOperationException(
            "The analysis did not produce uncertainty results.");
    }

    return analysis;
}
```

This method intentionally does not prescribe iteration counts. The defensible count depends on posterior geometry, tail functionals, effective sample size, and convergence diagnostics—not a universal constant.

## Posterior Frequency Results

Let retained MCMC draws be (oldsymbol\theta^{(1)},\ldots,\boldsymbol\theta^{(B)}). For each requested AEP (alpha_j), the analysis evaluates

$$
q_{j}^{(b)}=F^{-1}(1-\alpha_j\mid\boldsymbol\theta^{(b)}),
\qquad b=1,\ldots,B. \tag{4}
$$

For a stationary model, each draw is applied to a clone of the parent Numerics distribution. For a nonstationary model, each full trend-parameter draw is applied to a cloned `UnivariateDistribution`, and the exposed distribution at `ParameterTimeIndex` is retained. `AnalysisResults` then summarizes the empirical distribution of (q_j^{(b)}) with its mean and equal-tail credible limits. `CredibleIntervalWidth` is the retained posterior probability between those limits; changing it recomputes summaries without rerunning MCMC.

The displayed point-estimate curve is selected by `BayesianAnalysis.PointEstimator`:

- `PosteriorMean` applies the componentwise mean of retained parameter draws.
- `MAP` applies the stored maximum-a-posteriori parameter set.

Neither curve is generally equal to the posterior mean of the quantile because (F^{-1}(p\mid\boldsymbol\theta)) is nonlinear in (oldsymbol\theta):

$$
F^{-1}(p\mid E[\boldsymbol\theta\mid D])
\ne E[F^{-1}(p\mid\boldsymbol\theta)\mid D]. \tag{5}
$$

Use the posterior quantile distribution, not only the point curve, for risk calculations.

## Nonstationary Chronology Results

For an indexed covariate or time value (t), trend models map global coefficients to distribution parameters (oldsymbol\theta(t)). The chronology curve is

$$
q_{\alpha}(t)=F^{-1}\!\left(1-\alpha
\mid\boldsymbol\theta(t)\right), \tag{6}
$$

where `UnivariateDistribution.Alpha` selects (alpha). The analysis evaluates equation (6) across the full chronology for each posterior draw, then computes a posterior mean and equal-tail limits at each index. These are marginal credible bands by index; they are not a simultaneous confidence envelope and do not imply causal attribution of the trend.

Changing `ParameterTimeIndex` reprocesses frequency results from the existing chain. Crossing the observed-record boundary also refreshes the chronology extent. Changing the chronology AEP reprocesses chronology output. Structural changes—data, family, parameters, trend models, and prior settings—clear the fit and require new MCMC.

## Point-Estimate Criteria and an Important Convention

`UpdatePointEstimateResultsAsync()` computes the displayed AIC and BIC from `UnivariateDistribution.DataLogLikelihood(...)` evaluated at the stored MAP parameter vector; parameter, Jeffreys, and quantile-prior densities are excluded. When every active prior is constant over the relevant parameter region, MAP coincides with the constrained MLE and these values are comparable with the fitting-analysis criteria. With any informative or otherwise nonconstant prior—including the optional Jeffreys scale term—the MAP is prior-influenced and these fields should not be interpreted as conventional AIC/BIC. Use DIC, WAIC, or verified PSIS-LOO for Bayesian comparison in that setting. See [TR-011](../review-findings.md#tr-011).

The displayed RMSE uses the same pinned Numerics helper discussed in [TR-009](../review-findings.md#tr-009).

## Persistence and Restoration

`ToXElement()` stores the outer estimated flag, probability ordinates, and `BayesianAnalysis` configuration/result metadata. It does not serialize the model, raw data, `AnalysisResults`, or `ChronologyAnalysisResults` in the same element. The restoring constructor therefore accepts the `UnivariateDistribution` and optionally accepts `MCMCResults` and the two uncertainty-result objects. If complete Bayesian and frequency artifacts are restored from an older save, the constructor normalizes a stale outer estimated flag.

## Assumptions and Limitations

- Posterior statements are conditional on the selected family, trend structure, observation model, prior distributions, and data preprocessing.
- Independent observation contributions are assumed by the pointwise WAIC/LOO representation. Serial dependence, duplicated events, and unmodeled spatial dependence invalidate the nominal decomposition.
- A low tail probability can be much farther beyond the information in the record than its compact AEP notation suggests. Report posterior uncertainty and sensitivity to family/prior choices.
- Parameterwise posterior means can violate nonlinear structural interpretations even when they remain inside scalar bounds; inspect the resulting distribution.
- Credible intervals quantify posterior uncertainty under the model. They are not frequentist coverage guarantees and do not include unmodeled measurement, process, or model-form uncertainty.
- MCMC completion is not convergence. Review rank-normalized (widehat R), effective sample size, chain behavior, boundary contact, and tail-functional stability.

## Implementation Traceability

| Concern | Implementation |
|---|---|
| Analysis lifecycle | `Analyses/Univariate/UnivariateAnalysis.cs` |
| Statistical model | `Models/UnivariateDistribution/UnivariateDistribution.cs` |
| Sampler orchestration | `Estimation/BayesianAnalysis.cs` |
| MCMC results | pinned `Numerics/Sampling/MCMC/MCMCResults.cs` |
| Frequency uncertainty aggregation | pinned `Numerics/Distributions/Univariate/Uncertainty Analysis/UncertaintyAnalysisResults.cs` |
| Compiled example | `RMC.BestFit.Tests/Documentation/Examples/AnalysisExamples.cs` |

## References

<a id="ref-1"></a>[1] A. Gelman et al., *Bayesian Data Analysis*, 3rd ed. Boca Raton, FL, USA: CRC Press, 2013.

<a id="ref-2"></a>[2] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

<a id="ref-3"></a>[3] A. Vehtari et al., “Rank-normalization, folding, and localization: An improved \(\widehat R\) for assessing convergence of MCMC,” *Bayesian Analysis*, vol. 16, no. 2, pp. 667–718, 2021.

---

[Previous: Distribution Fitting](distribution-fitting.md) | [Technical Reference](../index.md) | [Next: Composite Analysis](composite.md)
