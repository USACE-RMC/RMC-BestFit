<!-- technical-reference-status: complete -->

# ARIMAX Models

[Time-series index](time-series.md) | [AR](autoregressive.md) | [MA](moving-average.md) | [ARIMA](arima.md)

## Implemented Regression–ARMA Hierarchy

For transformed and differenced response $w_t=\Delta^d g(y_t)$, `ARIMAX` constructs the deterministic mean

$$
m_t=\mu+
\sum_{r=1}^{R}\gamma_rt^r+
\psi_s\sin(2\pi t/S)+\psi_c\cos(2\pi t/S)+
\sum_{k=1}^{K}\sum_{j=0}^{b}\beta_{kj}x_{k,t-j}, \tag{AX.1}
$$

and residual dynamics

$$
w_t=m_t+
\sum_{i=1}^{p}\phi_i(w_{t-i}-m_{t-i})+
\varepsilon_t+
\sum_{j=1}^{q}\theta_j\varepsilon_{t-j},
\qquad \varepsilon_t\overset{\mathrm{iid}}\sim N(0,\sigma^2). \tag{AX.2}
$$

$R$ is 0–3 from `TrendType`; $S$ is inferred from `TimeInterval`; `IncludeSeasonality` supplies one sine/cosine harmonic; $K$ is the number of covariate series; and `XOrderB=b` includes current through $b$-lagged values for every covariate. The fitted vector order is

$$
[\mu],\ [\gamma_1,\ldots,\gamma_R],\ [\psi_s,\psi_c],\
[\beta_{10},\ldots,\beta_{1b},\ldots,\beta_{K0},\ldots,\beta_{Kb}],\
[\phi_1,\ldots,\phi_p],\ [\theta_1,\ldots,\theta_q],\ \sigma, \tag{AX.3}
$$

where bracketed blocks appear only when configured. Coefficients inherit the units of $w_t$ divided by their predictor units; $t$ is a zero-based index, so polynomial coefficients depend on time origin and interval.

## Likelihood and Prior

Let $r=\max(p,q,b)$. Presample residuals and fitted residuals before $r$ are conditioned out. For $t\ge r$,

$$
e_t=w_t-m_t-
\sum_{i=1}^{p}\phi_i(w_{t-i}-m_{t-i})-
\sum_{j=1}^{q}\theta_je_{t-j}. \tag{AX.4}
$$

The implemented log likelihood is

$$
\ell_D(\theta)=
-\frac{T_d-r}{2}\log(2\pi)-(T_d-r)\log\sigma
-\frac{1}{2\sigma^2}\sum_{t=r}^{T_d-1}e_t^2+J_g. \tag{AX.5}
$$

This is a conditional Gaussian likelihood, not an exact state-space likelihood. Marginal parameter priors are bounded uniforms by default and `UseJeffreysRuleForScale` adds $-\log\sigma$. Unlike AR/MA/ARIMA, ARIMAX correctly types that term as `JeffreysScalePrior` in pointwise metadata. Pointwise data likelihood still lacks the scalar nonpositive-scale guard ([TR-040](../review-findings.md#tr-040)).

Box–Cox/Yeo–Johnson parameters are plug-in values fitted on the entire response ([TR-036](../review-findings.md#tr-036)); they are not jointly estimated and their uncertainty is not propagated. Manual transform configuration leaves derived state stale ([TR-046](../review-findings.md#tr-046)).

## Covariate Alignment and Lags

`SetCovariates` requires every covariate to have the same count as the response. Likelihood code indexes covariates positionally; it does not inner-join or verify `DateTime` equality. Users must supply identical interval, start time, timestamps, missing-value treatment, units, and provenance. Lag $j$ means $j$ array positions, not necessarily a hydrologically meaningful elapsed duration if the metadata are wrong.

For $d>0$, differenced response position $t$ corresponds to raw position $t+d$, but the code uses $x_{k,t}$ and computes a Jacobian ending $d$ observations too early. This is [TR-041](../review-findings.md#tr-041). Covariate regression with differencing is therefore not scientifically usable until its level/difference convention and alignment are corrected.

Trend and Fourier seasonality are explicitly rejected when `DiffOrderD>0`, avoiding an additional deterministic-term ambiguity. With $d=0$, the single Fourier harmonic is useful for a stable sinusoidal cycle but cannot represent changing phase, multiple seasonal frequencies, or event-timed hydrology.

## Forecast Covariates

`Predict` accepts optional future covariate series. Without them:

- `None` requires observed covariates long enough for the complete horizon and otherwise throws;
- `BlockBootstrap` appends blocks of length $\min(10,\lfloor N/4\rfloor)$, bounded below by one;
- `KNN` appends values using $k=\max(3,\lfloor N/10\rfloor)$;
- deterministic prediction (`seed=-1`) appends the empirical covariate mean, independent of the configured stochastic extension method.

These mechanisms represent empirical continuation scenarios, not a probabilistic model fitted jointly with the response. They do not propagate parameter uncertainty in a covariate forecast model, preserve cross-covariate dependence by construction, or condition on climate/operations scenarios. For defensible engineering forecasts, provide explicit aligned future covariates or model their joint uncertainty outside BestFit and pass scenario paths realization by realization.

## Forecast and Simulation Restrictions

For $d=0$ and no transform, `Predict` uses observed response/residual history inside training and recursive response/noise afterward. `ARIMAXAnalysis` combines posterior parameter and innovation draws. Its bands also include whichever covariate extension is invoked, so clearly state that scenario.

For $d>0$, forecast reintegration is shifted ([TR-037](../review-findings.md#tr-037)). `GenerateRandomValues` additionally mixes transformed and original scales and inverse-transforms before integration ([TR-039](../review-findings.md#tr-039)). Therefore transformed/differenced posterior predictive checks and forecasts are unavailable. Analysis AIC/BIC are posterior-kernel quantities rather than conventional criteria ([TR-042](../review-findings.md#tr-042)).

AR stationarity and MA invertibility are warned using sums of absolute coefficients, not enforced by roots or reparameterization. Polynomial trends extrapolate without bound, empirical covariate extension can leave the historical support, and collinear lag blocks can make $\beta$, trend, seasonality, and AR persistence weakly identifiable.

## Compile-Checked Workflow

This $d=0$ example uses matched monthly indexes and explicit future covariate coverage; `None` prevents silent empirical extension.

<!-- snippet: arimax-workflow -->
```csharp
private static ARIMAXAnalysis ConfigureArimaxAnalysis()
{
    double[] monthlyFlow =
    {
        92, 105, 121, 138, 151, 144, 132, 119, 108, 101, 96, 90,
        95, 109, 125, 141, 155, 148, 136, 122, 111, 103, 98, 93
    };
    double[] monthlyPrecipitation =
    {
        28, 35, 48, 61, 72, 65, 54, 43, 36, 31, 29, 26,
        30, 38, 51, 64, 75, 68, 57, 46, 39, 34, 31, 28
    };
    var start = new DateTime(2022, 1, 1);
    var response = new NumericsTimeSeries(
        TimeInterval.OneMonth,
        start,
        monthlyFlow);
    var precipitation = new NumericsTimeSeries(
        TimeInterval.OneMonth,
        start,
        monthlyPrecipitation);

    var model = new ARIMAX(response)
    {
        AROrderP = 1,
        DiffOrderD = 0,
        MAOrderQ = 1,
        XOrderB = 1,
        IncludeIntercept = true,
        IncludeSeasonality = true,
        TrendType = ARIMAX.Trend.None,
        CovariateExtension =
            ARIMAX.CovariateExtensionMethod.None,
        UseDefaultTrainingSteps = false
    };
    model.TrainingTimeSteps = 20;
    model.SetCovariates(
        new List<NumericsTimeSeries> { precipitation });
    model.SetDefaultParameters();

    return new ARIMAXAnalysis(model)
    {
        ForecastingTimeSteps = 4
    };
}
```

The last four response/covariate observations are holdout values, not future extensions. After fitting, report predictor units, lag rationale, coefficient correlations, characteristic roots, residual ACF/variance diagnostics, holdout scores, and sensitivity to trend/seasonality/covariate choices.

## Traceability and Evidence

Implementation: `Models/TimeSeries/ARIMAX.cs`; orchestration: `Analyses/TimeSeries/ARIMAXAnalysis.cs`. Fast tests cover configuration, serialization, prediction shapes, covariate extension, and broad transformations. Recovery and forecast verification sources were inspected under `RMC.BestFit.Verification/TimeSeriesModels/ARIMAXTests.cs` but not executed. Existing tests do not establish the raw-time alignment or scale identities in TR-037, TR-039, and TR-041.

## References

<a id="ref-1"></a>[1] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-2"></a>[2] R. H. Shumway and D. S. Stoffer, *Time Series Analysis and Its Applications*, 4th ed., Springer, 2017.

<a id="ref-3"></a>[3] R. J. Hyndman and G. Athanasopoulos, *Forecasting: Principles and Practice*, 3rd ed., OTexts, 2021.
