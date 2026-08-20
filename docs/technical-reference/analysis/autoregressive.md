<!-- technical-reference-status: complete -->

# Autoregressive Models

[Time-series index](time-series.md) | [MA](moving-average.md) | [ARIMA](arima.md) | [ARIMAX](arimax.md)

## Purpose and Data Model

`AutoRegressive` represents a regularly spaced Gaussian AR($p$) process for a transformed response $z_t=g(y_t)$:

$$
z_t=\mu+\sum_{j=1}^{p}\phi_j(z_{t-j}-\mu)+\varepsilon_t,
\qquad \varepsilon_t\overset{\mathrm{iid}}\sim N(0,\sigma^2). \tag{AR.1}
$$

When `IncludeIntercept=false`, the implementation sets $\mu=0$. Despite the API label “Intercept,” $\mu$ in (AR.1) is the stationary mean, not the regression constant $c=\mu(1-\sum_j\phi_j)$. The ordered parameter vector is

$$
(\mu,\phi_1,\ldots,\phi_p,\sigma) \quad\text{or}\quad
(\phi_1,\ldots,\phi_p,\sigma). \tag{AR.2}
$$

$z_t$, $\mu$, and $\sigma$ have transformed-response units; $\phi_j$ is dimensionless. `Order` is restricted to 1–10, the input interval must be regular, and at least ten response values are required.

## Conditional Likelihood

The first $p$ transformed observations condition the recursion and do not contribute density terms. For a training prefix of length $T$,

$$
e_t=z_t-\mu-\sum_{j=1}^{p}\phi_j(z_{t-j}-\mu),\qquad t=p,\ldots,T-1, \tag{AR.3}
$$

$$
\ell_D(\theta)=
-\frac{T-p}{2}\log(2\pi)- (T-p)\log\sigma
-\frac{1}{2\sigma^2}\sum_{t=p}^{T-1}e_t^2+J_g. \tag{AR.4}
$$

`DataLogLikelihood` implements (AR.4), rejects nonpositive $\sigma$, and returns negative infinity when the training series is unavailable. `PointwiseDataLogLikelihood` returns $T-p$ terms and distributes the scalar transformation Jacobian $J_g$ equally among them. The pointwise path currently lacks the scalar path's scale guard; see [TR-040](../review-findings.md#tr-040).

The available transforms are none, Box–Cox logarithmic ($\lambda=0$), fitted Box–Cox, and fitted Yeo–Johnson. The change-of-variables term is evaluated over the same raw observations represented by (AR.4). Transformation parameters are plug-in preprocessing estimates, not coordinates in $\theta$ and not propagated through posterior uncertainty. They are currently fit using the full response rather than the training prefix ([TR-036](../review-findings.md#tr-036)); `SetTransformParameters` also does not rebuild dependent state ([TR-046](../review-findings.md#tr-046)). Holdout scores are therefore not publishable when an estimated transform is selected.

## Priors and Posterior

Each `ModelParameter` contributes its configured marginal prior. Defaults are bounded uniforms: data-scaled bounds for $\mu$ and $\sigma$, and $[-2,2]$ for each $\phi_j$. With `UseJeffreysRuleForScale=true`, the implementation adds

$$
\log\pi_J(\sigma)=-\log\sigma. \tag{AR.5}
$$

Thus `LogLikelihood` is $\ell_D+\sum_j\log\pi_j+\log\pi_J$. The Jeffreys term is currently mislabeled as `ParameterPrior` in the pointwise-prior metadata ([TR-035](../review-findings.md#tr-035)). Bayesian estimation uses the bounded natural coordinates and sampler configuration described in [Bayesian MCMC](../estimation/bayesian-mcmc.md).

## Stationarity and Identifiability

Weak stationarity requires every zero of

$$
\Phi(z)=1-\phi_1z-\cdots-\phi_pz^p \tag{AR.6}
$$

to lie outside the unit circle. The implementation checks the exact AR(1) and AR(2) inequalities; for $p\ge3$ it uses the sufficient, not necessary, condition $\sum_j|\phi_j|<1$. Failure produces a warning only. Bounds therefore admit nonstationary states, and neither the likelihood nor MCMC target enforces (AR.6). Near-unit roots confound $\mu$, initial conditions, and long-horizon forecasts; reviewers should calculate characteristic roots for every retained draw used in a stationary interpretation.

## Prediction and Uncertainty

`Predict(parameters, forecastSteps, seed)` uses observed transformed lags inside the training window and recursively predicted lags afterward. With `seed=-1`, it returns the conditional-median path on the original scale after inverse transformation. A nonnegative seed adds independent Gaussian innovations after the $p$ seed observations. `ARAnalysis.CreateUncertaintyAnalysisResultsAsync()` combines posterior parameter draws with seeded innovation draws, so its bands are posterior-predictive bands, not parameter-only credible bands.

`GenerateRandomValues(sampleSize, seed)` likewise completes the full AR recursion on transformed
model scale and inverse-transforms the completed vector exactly once. The returned length is
`sampleSize`; `Transform.None` retains the established seeded sequence bit for bit.

For nonlinear inverse transforms, $g^{-1}\{E(Z)\}\ne E\{g^{-1}(Z)\}$; the deterministic back-transform is not a mean forecast and no lognormal/Box–Cox bias correction is applied. Forecasts also assume fixed parameters, a regular interval, no missing times, and no observation error.

## Compile-Checked Workflow

The 16-point training prefix leaves four annual observations for honest validation when no fitted transform is used.

<!-- snippet: autoregressive-workflow -->
```csharp
private static ARAnalysis ConfigureAutoregressiveAnalysis()
{
    double[] annualFlow =
    {
        112, 128, 121, 139, 151, 147, 160, 158, 171, 166,
        179, 185, 181, 194, 202, 198, 211, 219, 214, 228
    };
    var series = new NumericsTimeSeries(
        TimeInterval.OneYear,
        new DateTime(2000, 1, 1),
        annualFlow);

    var model = new AutoRegressive(
        series,
        order: 1,
        includeIntercept: true)
    {
        UseDefaultTrainingSteps = false,
        UseJeffreysRuleForScale = true,
        TransformType = RMC.BestFit.Models.Transform.None
    };
    model.TrainingTimeSteps = 16;
    model.SetDefaultParameters();

    return new ARAnalysis(model)
    {
        ForecastingTimeSteps = 4
    };
}
```

Run the analysis asynchronously, require satisfactory chain diagnostics, inspect residual ACF/PACF and time plots, and score the four untouched observations. Do not call the result evidence of hydrologic causality: AR coefficients describe serial prediction, not physical storage mechanisms.

## Validation and Traceability

Implementation: `Models/TimeSeries/AutoRegressive.cs` and `Analyses/TimeSeries/ARAnalysis.cs`.
Fast tests cover construction, likelihood decomposition, transforms, prediction state, generation
algebra, and analysis lifecycle. The focused generator oracle verifies inverse-transform algebra
and 1,000 model-scale moment values; recovery remains in the Phase 5 matrix.

## References

<a id="ref-1"></a>[1] P. J. Brockwell and R. A. Davis, *Introduction to Time Series and Forecasting*, 3rd ed., Springer, 2016.

<a id="ref-2"></a>[2] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-3"></a>[3] G. E. P. Box and D. R. Cox, “An analysis of transformations,” *J. R. Stat. Soc. B*, vol. 26, no. 2, pp. 211–252, 1964.
