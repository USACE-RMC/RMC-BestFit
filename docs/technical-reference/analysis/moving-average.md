<!-- technical-reference-status: complete -->

# Moving-Average Models

[Time-series index](time-series.md) | [AR](autoregressive.md) | [ARIMA](arima.md) | [ARIMAX](arimax.md)

## Formulation and Parameterization

`MovingAverage` implements a Gaussian MA($q$) process on the transformed response $z_t=g(y_t)$:

$$
z_t=\mu+\varepsilon_t+\sum_{j=1}^{q}\theta_j\varepsilon_{t-j},
\qquad \varepsilon_t\overset{\mathrm{iid}}\sim N(0,\sigma^2). \tag{MA.1}
$$

The ordered parameter vector is $(\mu,\theta_1,\ldots,\theta_q,\sigma)$ when `IncludeIntercept=true`, otherwise the $\mu$ coordinate is absent and fixed at zero. Here $\mu$ is the process mean. The default coefficient bounds are $[-2,2]$, scale has a positive data-derived bound, and `Order` is limited to 1–10.

An MA model represents a finite response to past innovations, not a regression on past observations. It is useful when shocks have short-lived effects and the empirical ACF cuts off, but hydrologic interpretation requires care because the innovations are inferred residuals rather than observed physical inputs.

## Conditional Sum-of-Squares Likelihood

BestFit sets all presample innovations to zero and recursively calculates

$$
e_t=z_t-\mu-\sum_{j=1}^{\min(t,q)}\theta_j e_{t-j},
\qquad t=0,\ldots,T-1. \tag{MA.2}
$$

Every recursive residual, including the first $q$, contributes to

$$
\ell_D(\theta)=
-\frac{T}{2}\log(2\pi)-T\log\sigma
-\frac{1}{2\sigma^2}\sum_{t=0}^{T-1}e_t^2+J_g. \tag{MA.3}
$$

This is a conditional sum-of-squares likelihood with a fixed zero presample state, not the exact Gaussian likelihood obtained by integrating the initial innovations or using a state-space/Kalman representation. Exact-likelihood results from other packages can therefore differ at short records and near the invertibility boundary.

The transform and Jacobian conventions match the AR chapter. Box–Cox/Yeo–Johnson fitting currently uses the complete response, including held-out observations ([TR-036](../review-findings.md#tr-036)); manual transform parameters do not rebuild the model ([TR-046](../review-findings.md#tr-046)). The pointwise likelihood lacks the scalar $\sigma>0$ guard ([TR-040](../review-findings.md#tr-040)).

## Prior and Posterior

The full target is

$$
\ell(\theta)=\ell_D(\theta)+\sum_k\log\pi_k(\theta_k)
-I_J\log\sigma, \tag{MA.4}
$$

where $I_J$ is one when `UseJeffreysRuleForScale` is enabled. Default marginal priors are bounded uniforms. The pointwise metadata currently classifies the Jeffreys contribution as an ordinary parameter prior ([TR-035](../review-findings.md#tr-035)).

## Invertibility

Uniqueness of the innovation representation requires the roots of

$$
\Theta(z)=1+\theta_1z+\cdots+\theta_qz^q \tag{MA.5}
$$

to lie outside the unit circle under the sign convention in (MA.1). `IsInvertible()` checks $|\theta_1|<1$ for MA(1) and only the sufficient condition $\sum_j|\theta_j|<1$ for higher order. A violation is a warning, not a target constraint. The posterior can therefore contain observationally equivalent noninvertible representations; calculate polynomial roots per retained draw before interpreting $\theta_j$.

## Forecasting

Inside the training window, `Predict` reconstructs innovations from observations. After the window, a deterministic forecast sets future innovations to zero; a seeded predictive realization injects new Gaussian innovations and feeds them through the finite MA recursion. Consequently, the conditional mean reaches $\mu$ after at most $q$ future steps. Inverse-transformed deterministic paths are conditional medians without bias correction.

`MAAnalysis` propagates joint posterior parameter uncertainty and innovations by calling `Predict` for posterior draws. Its bands are posterior predictive. Its AIC/BIC fields use the data log likelihood at the stored MAP and exclude prior-density terms. They are comparable with MLE criteria only when all active priors are constant; with the Jeffreys scale option or another nonconstant prior, use posterior criteria instead ([TR-042](../review-findings.md#tr-042)).

## Compile-Checked Workflow

<!-- snippet: moving-average-workflow -->
```csharp
private static MAAnalysis ConfigureMovingAverageAnalysis()
{
    double[] monthlyAnomaly =
    {
        0.3, -0.2, 0.1, 0.5, -0.4, 0.2,
        0.0, 0.4, -0.1, -0.3, 0.2, 0.1,
        -0.2, 0.3, 0.0, -0.1, 0.4, -0.2
    };
    var series = new NumericsTimeSeries(
        TimeInterval.OneMonth,
        new DateTime(2020, 1, 1),
        monthlyAnomaly);

    var model = new MovingAverage(
        series,
        order: 1,
        includeIntercept: true)
    {
        UseDefaultTrainingSteps = false,
        UseJeffreysRuleForScale = true
    };
    model.TrainingTimeSteps = 15;
    model.SetDefaultParameters();

    return new MAAnalysis(model)
    {
        ForecastingTimeSteps = 3
    };
}
```

The example treats the series as an already detrended anomaly. For raw streamflow, explicitly address trend, seasonality, transformation, missing intervals, and physical nonstationarity before choosing an MA order. Validate on the final three observations and inspect residual autocorrelation; an information criterion alone is insufficient.

## Limitations and Evidence

The model assumes regular spacing, no missing times, Gaussian homoscedastic innovations on the fitted scale, fixed parameters, and an error-free response. It does not model seasonal MA factors, state-dependent variance, intervention effects, or exact initial-state uncertainty.

Implementation: `Models/TimeSeries/MovingAverage.cs`; orchestration: `Analyses/TimeSeries/MAAnalysis.cs`. Fast tests cover API behavior and deterministic calculations. Verification source compares conditional behavior and recovery under `RMC.BestFit.Verification/TimeSeriesModels/MovingAverageTests.cs`; it was not run in this program.

## References

<a id="ref-1"></a>[1] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-2"></a>[2] P. J. Brockwell and R. A. Davis, *Time Series: Theory and Methods*, 2nd ed., Springer, 1991.
