<!-- technical-reference-status: complete -->

# ARIMA Models

[Time-series index](time-series.md) | [AR](autoregressive.md) | [MA](moving-average.md) | [ARIMAX](arimax.md)

## Generative Model

Let $x_t=g(y_t)$ be the optionally transformed response and

$$
w_t=\Delta^d x_t=(1-B)^d x_t. \tag{ARI.1}
$$

`ARIMA` models the differenced series as

$$
w_t=\mu+
\sum_{j=1}^{p}\phi_j(w_{t-j}-\mu)+
\varepsilon_t+
\sum_{k=1}^{q}\theta_k\varepsilon_{t-k},
\qquad \varepsilon_t\overset{\mathrm{iid}}\sim N(0,\sigma^2). \tag{ARI.2}
$$

The public constructor is `ARIMA(timeSeries, pOrder, dOrder, qOrder, includeIntercept)`. Supported bounds are $0\le p,q\le10$ and $0\le d\le2$, with at least one of $p,q$ positive. The ordered fitted vector is

$$
(\mu,\phi_1,\ldots,\phi_p,\theta_1,\ldots,\theta_q,\sigma), \tag{ARI.3}
$$

omitting $\mu$ when `IncludeIntercept=false`. For $d>0$, $\mu$ is the mean/drift of the differenced transformed process, not the mean of $y_t$.

## Transform, Difference, and Training Window

BestFit applies $g$ first and differences second. None, log/Box–Cox, and Yeo–Johnson transforms are supported. Differencing reduces the training series by $d$ observations. Transform parameters are estimated preprocessing values rather than posterior coordinates. They currently use the entire response and leak the holdout window ([TR-036](../review-findings.md#tr-036)); the manual setter leaves previously transformed/differenced data unchanged ([TR-046](../review-findings.md#tr-046)).

The mathematically correct density transformation is

$$
\ell_y(\theta)=\ell_w(\theta)+
\sum_{t\in\mathcal I}\log\left|g'(y_t)\right|, \tag{ARI.4}
$$

because finite differencing has unit determinant conditional on the $d$ initial values. The code calculates a scalar $J_g$ over raw indexes beginning at $d+\max(p,q)$ and ending at the training boundary.

## Implemented Conditional Likelihood

Let $r=\max(p,q)$. `Residuals` sets $e_t=0$ for $t<r$ and, for $t\ge r$, evaluates

$$
e_t=w_t-\mu-
\sum_{j=1}^{p}\phi_j(w_{t-j}-\mu)-
\sum_{k=1}^{q}\theta_k e_{t-k}. \tag{ARI.5}
$$

The implemented conditional log likelihood is

$$
\ell_D(\theta)=
-\frac{T_d-r}{2}\log(2\pi)-(T_d-r)\log\sigma
-\frac{1}{2\sigma^2}\sum_{t=r}^{T_d-1}e_t^2+J_g, \tag{ARI.6}
$$

where $T_d$ is the differenced training length. This is not an exact Gaussian state-space likelihood. Pointwise output contains $T_d-r$ contributions and divides $J_g$ equally among them. It lacks the scalar likelihood's nonpositive-scale guard ([TR-040](../review-findings.md#tr-040)).

The prior is the product of configured marginal priors and, by default, $1/\sigma$. AR/MA bounds do not enforce stationarity or invertibility. `IsStationary` and `IsInvertible` use exact first-order checks and conservative sums of absolute coefficients at higher order; failures are warnings. For valid ARIMA interpretation, compute roots of $1-\sum\phi_jz^j$ and $1+\sum\theta_kz^k$ for posterior draws.

## Forecasting and Current Restriction

`Predict` calculates exactly $T-d+h$ values on the transformed, $d$-difference scale. Model step
$k$ maps to raw response slot $k+d$. Inverse differencing begins with the first $d$ observed
transformed levels and successively reconstructs exactly $T+h$ transformed levels; only then is
$g^{-1}$ applied once. Process uncertainty therefore accumulates through the integration
recurrence, and posterior uncertainty requires repeating the recursion for joint parameter draws.
The component vectors retain raw length $T+h$: their first $d$ conditioning entries are zero and
component step $k$ is stored at raw slot $k+d$. This behavior closes
[TR-037](../review-findings.md#tr-037); `Transform.None` with $d=0$ retains its pre-correction
fixed-seed values bit for bit.

`GenerateRandomValues` still ignores $d$ and the configured transform
([TR-038](../review-findings.md#tr-038)). Consequently, estimation and `Predict` are available
subject to the conditional-model assumptions, but prior/posterior predictive simulation is not
scientifically usable when $d>0$ or `TransformType != None` until TR-038 closes.

## Compile-Checked Configuration

<!-- snippet: arima-workflow -->
```csharp
private static ARIMAAnalysis ConfigureArimaAnalysis()
{
    double[] annualStorage =
    {
        410, 422, 431, 447, 452, 468, 475, 489, 501, 496,
        514, 526, 531, 548, 559, 571, 580, 594, 607, 615
    };
    var series = new NumericsTimeSeries(
        TimeInterval.OneYear,
        new DateTime(2000, 1, 1),
        annualStorage);

    var model = new ARIMA(
        series,
        pOrder: 1,
        dOrder: 1,
        qOrder: 1,
        includeIntercept: true)
    {
        UseDefaultTrainingSteps = false,
        UseJeffreysRuleForScale = true,
        TransformType = RMC.BestFit.Models.Transform.None
    };
    model.TrainingTimeSteps = 16;
    model.SetDefaultParameters();

    return new ARIMAAnalysis(model)
    {
        ForecastingTimeSteps = 4
    };
}
```

This block demonstrates the verified prediction API for $d=1$. Do not use its production
generator for predictive checks until TR-038 closes.

## Assumptions, Diagnostics, and Evidence

The model assumes regular spacing, fixed coefficients, Gaussian homoscedastic innovations, a fully observed response, and a differencing order chosen without mining the validation set. Diagnose residual serial dependence, conditional variance, structural breaks, root proximity, and forecast calibration. Polynomial drift after repeated integration is an extrapolation assumption, not a physical law.

Implementation: `Models/TimeSeries/ARIMA.cs`; orchestration:
`Analyses/TimeSeries/ARIMAAnalysis.cs`. Fast tests verify `d=1` linear and `d=2` quadratic
reintegration, transformed prediction, output/component alignment, and exact $d=0$ fixed-seed
compatibility. The focused analytical hand-recurrence method passes at `1E-10`; integrated
parameter recovery remains part of the Phase 5 recovery matrix.

## References

<a id="ref-1"></a>[1] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-2"></a>[2] R. J. Hyndman and G. Athanasopoulos, *Forecasting: Principles and Practice*, 3rd ed., OTexts, 2021.
