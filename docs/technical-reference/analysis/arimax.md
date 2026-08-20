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

Let $r=\max(p,q)$. Presample residuals and fitted residuals before $r$ are conditioned out. For $t\ge r$,

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

This is a conditional Gaussian likelihood, not an exact state-space likelihood. Marginal parameter priors are bounded uniforms by default and `UseJeffreysRuleForScale` adds $-\log\sigma$. All four time-series models type that term as `JeffreysScalePrior` in pointwise metadata and consistently reject non-finite or non-positive innovation scales with negative infinity ([TR-035](../review-findings.md#tr-035), [TR-040](../review-findings.md#tr-040)).

Box–Cox/Yeo–Johnson parameters are plug-in values fitted on the raw training prefix and frozen
before transformation of the complete response; they are not jointly estimated and their
uncertainty is not propagated. Manual transform assignment rebuilds transformed/differenced
state and preserves fitted/manual provenance through persistence ([TR-036](../review-findings.md#tr-036),
[TR-046](../review-findings.md#tr-046)).

## Covariate Alignment and Lags

For a raw training prefix of $T$ observations and differencing order $d$, `ARIMAX` forms exactly
$T_d=T-d$ model steps. Model index $k$ represents raw response index $k+d$ and receives that
later raw timestamp. Level covariate $x_{i,k}$ is selected by exact equality with this timestamp;
covariates are not differenced. Lag $j$ uses the timestamp of model step $k-j$. Pre-sample lags
are omitted under the conditional convention.

Missing or duplicate covariate timestamps required by the training window invalidate the model;
numerical evaluation returns negative infinity rather than falling back to positional matching.
Extra dates outside the required window are harmless. The conditional transform Jacobian is

$$
J_g=\sum_{u=d+r}^{T-1}\log|g'(y_u)|,\qquad r=\max(p,q),
\tag{AX.6}
$$

so response, level covariates, conditional residuals, and change-of-variable terms share one raw
index set ([TR-041](../review-findings.md#tr-041)).

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

For $d>0$, likelihood and residual diagnostics now use the verified exact-date map, but forecast
reintegration remains shifted ([TR-037](../review-findings.md#tr-037)). `GenerateRandomValues`
additionally mixes transformed and original scales and inverse-transforms before integration
([TR-039](../review-findings.md#tr-039)). Therefore transformed/differenced posterior predictive
checks and forecasts remain unavailable until those separate findings close. Analysis AIC/BIC use
the data log likelihood at the stored MAP and exclude prior-density terms; they are comparable with
MLE criteria only when every active prior is constant ([TR-042](../review-findings.md#tr-042)).

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

Implementation: `Models/TimeSeries/ARIMAX.cs`; orchestration:
`Analyses/TimeSeries/ARIMAXAnalysis.cs`. Fast tests cover configuration, serialization, prediction
shapes, covariate extension, transformations, exact-date alignment, holdout isolation, and
likelihood decomposition. The independent R alignment oracle verifies `d=0,1,2` at `1E-10`.
Prediction reintegration and generation scale identities remain assigned to TR-037 and TR-039.

## References

<a id="ref-1"></a>[1] G. E. P. Box, G. M. Jenkins, G. C. Reinsel, and G. M. Ljung, *Time Series Analysis: Forecasting and Control*, 5th ed., Wiley, 2015.

<a id="ref-2"></a>[2] R. H. Shumway and D. S. Stoffer, *Time Series Analysis and Its Applications*, 4th ed., Springer, 2017.

<a id="ref-3"></a>[3] R. J. Hyndman and G. Athanasopoulos, *Forecasting: Principles and Practice*, 3rd ed., OTexts, 2021.
