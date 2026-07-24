<!-- technical-reference-status: complete -->

# Trend Functions and Nonstationary Parameters

[<- Data likelihood](../data-frame/index.md) | [Technical reference index](../index.md) | [Next: Link functions ->](link-functions.md)

## Role in the probability model

An `ITrendModel` maps an integer index or a covariate row to one distribution parameter. In `UnivariateDistribution`, there is one trend model for each Numerics distribution parameter. If the parent family has parameter vector $\theta=(\theta_1,\ldots,\theta_K)$, a nonstationary fit uses

$$
\theta_k(t)=h_k(t;\beta_k),\qquad k=1,\ldots,K,
\tag{1}
$$

and evaluates the observation likelihood at $\theta(t_i)$. The fitted vector is the concatenation

$$
\beta=(\beta_1^\mathsf T,\ldots,\beta_K^\mathsf T)^\mathsf T
\tag{2}
$$

in distribution-parameter order and, within each trend, `Parameters` order. Priors and bounds are placed on these coefficients. The predicted distribution parameters must satisfy their family support at every evaluated index; otherwise the candidate likelihood is negative infinity.

Trend functions operate in the same space as the distribution parameter they predict. They do not automatically apply a log or logit link. A positive scale modeled with a linear trend can become non-positive during interpolation or extrapolation, causing rejection by distribution validation.

## Contract and notation

`ITrendModel` exposes `OwnerName`, `Type`, `StartIndex`, ordered `Parameters`, `NumberOfParameters`, `UseDefaultFlatPriors`, `SetParameterValues`, `SetDefaultParameters`, `Predict`, `Clone`, and `ToXElement`.

For the index-based functions, define

$$
\tau=t-t_0,
\tag{3}
$$

where $t$ is the integer argument to `Predict` and $t_0$ is `StartIndex`. Centering reduces intercept-slope dependence and keeps polynomial powers numerically smaller. Changing `StartIndex` without transforming the coefficients changes their meanings and, for nonlinear trends, may change the curve.

## Implemented index-based functions

| `TrendModelType` / class | Parameter order | Implemented prediction $h(t)$ |
|---|---|---|
| `Constant` / `ConstantTrend` | $(a)$ | $a$ |
| `Linear` / `LinearTrend` | $(a,b)$ | $a+b\tau$ |
| `Quadratic` / `QuadraticTrend` | $(a,b,c)$ | $a+b\tau+c\tau^2$ |
| `Cubic` / `CubicTrend` | $(a,b,c,d)$ | $a+b\tau+c\tau^2+d\tau^3$ |
| `Exponential` / `ExponentialTrend` | $(a,b)$ | $a\exp(b\tau)$ |
| `Logistic` / `LogisticTrend` | $(a,b)$ | $a/[1+\exp(-b\tau)]$ |
| `Power` / `PowerTrend` | $(a,b)$ | $a\tau^b$ subject to boundary rules below |
| `Reciprocal` / `ReciprocalTrend` | $(a,b)$ | $1/(a+b\tau)$ subject to pole protection |
| `Sinusoidal` / `SinusoidalTrend` | $(a,b,c,d)$ | $a+b\sin(2\pi c\tau+d)$ |
| `StepFunction` / `StepFunction` | $(\mu_1,\mu_2,t_c)$ | $\mu_1$ for $t\le t_c$; $\mu_2$ for $t>t_c$ |

In the sinusoidal form, $a$ is the center, $b$ is amplitude, $c$ is cycles per index unit, and $d$ is phase in radians. A period is $1/c$ index units when $c>0$. The default frequency bounds restrict $c$ to at most $0.5$, corresponding to a two-index Nyquist period for equally spaced integer data, but missing/irregular indexes require separate aliasing assessment.

The logistic implementation has two coefficients, not the common three- or four-parameter sigmoid. It approaches $0$ and $a$, has midpoint $a/2$ at `StartIndex`, and uses $b$ as the rate and direction. It cannot estimate a separate horizontal midpoint.

## Numerical boundary behavior

The prediction functions intentionally define deterministic behavior at arithmetic boundaries:

- `ExponentialTrend` returns signed infinity when $b\tau>700$ and zero when $b\tau<-700$. The downstream distribution validity/finite checks reject unphysical overflow rather than accepting a saturated finite value.
- `LogisticTrend` clamps $-b\tau$ to $[-700,700]$ before exponentiation. Predictions remain finite and approach the asymptotes numerically.
- `PowerTrend` replaces negative $\tau$ by zero. If $\tau=0$ and $b<0$, it evaluates at $\tau=10^{-2}$ so the boundary remains finite. This is an implementation regularization, not the mathematical limit of $a\tau^b$.
- `ReciprocalTrend` replaces a denominator with magnitude below $10^{-12}$ by $+10^{-12}$ or $-10^{-12}$, preserving its sign. The result remains finite but extremely large near a pole.
- `StepFunction` is left-continuous at the change point: the pre-change value is returned at $t=t_c$.

These protections prevent low-level floating-point failures; they do not make the corresponding extrapolations scientifically defensible. Posterior draws that approach a reciprocal pole or rely on a power boundary should be diagnosed and usually reparameterized.

## General linear covariate function

`GeneralLinearFunction` represents

$$
h_i=\beta_0+\sum_{j=1}^{p}\beta_j x_{ij}.
\tag{4}
$$

Its parameter order is intercept followed by covariate coefficients in matrix-column order. `Predict(i)` treats `i` as a zero-based row of the stored `double[,] Covariates`; unlike the time functions, it does not subtract `StartIndex`. An out-of-range row throws `ArgumentOutOfRangeException`.

`PredictWithCovariates(x)` supports prediction at an ungauged site. It returns the intercept if the model contains no covariates. For a configured covariate model, null or empty input also returns the intercept; a nonempty array with the wrong length throws `ArgumentException`. Because silent intercept-only prediction is possible, callers should validate the feature vector explicitly before regional extrapolation.

Covariates should be centered and scaled before fitting when magnitudes differ substantially. Coefficients then have interpretable units: if $h$ is a location parameter in cubic feet per second and $x_j$ is elevation in feet, $\beta_j$ has units of discharge per foot. Collinearity, extrapolation beyond the calibration cloud, and omitted spatial structure can dominate uncertainty even when the algebraic prediction is finite.

## Compile-checked linear example

The following linear trend is centered at year 2000. Its intercept is 1,200 discharge units in 2000 and its slope is 4 discharge units per year, so the 2050 prediction is 1,400.

<!-- snippet: trend-linear-prediction -->
```cs
private static double PredictLocationIn2050()
{
    var trend = new LinearTrend
    {
        StartIndex = 2000
    };
    trend.Parameters[0].Value = 1200.0;
    trend.Parameters[1].Value = 4.0;

    return trend.Predict(2050);
}
```

The example only evaluates a configured curve. Fitting it requires assigning the trend to the correct distribution parameter and running an analysis.

## Likelihood, priors, and uncertainty

For conditionally independent observations under a nonstationary univariate distribution,

$$
\ell_D(\beta;y)=\sum_{i=1}^{m}\log L_i\!\left(y_i\mid
h_1(t_i;\beta_1),\ldots,h_K(t_i;\beta_K)\right).
\tag{5}
$$

Exact, uncertain, interval, and threshold contributions retain the forms in the data-frame chapter, with $\theta$ replaced by $\theta(t_i)$. `UnivariateDistribution` clones each trend, assigns the candidate coefficient slice, predicts every parameter, validates the resulting Numerics distribution, and then evaluates the record.

Configured parameter priors apply to trend coefficients. Jeffreys-scale and quantile-prior terms, when enabled, use the distribution parameters predicted at the last `FullTimeSeries` index. A return level at future $t^*$ is a posterior transformation

$$
q_\alpha(t^*)=F^{-1}\!\left(1-\alpha\mid\theta(t^*)\right),
\tag{6}
$$

so uncertainty must propagate both coefficient uncertainty and the nonlinear distribution quantile. Evaluating Equation (6) only at posterior mean coefficients generally does not equal the posterior mean return level.

## Identifiability and extrapolation

- Constant, linear, quadratic, and cubic models are nested, but higher degree does not guarantee more credible future behavior. Polynomial tails diverge rapidly outside the observed index range.
- The exponential intercept $a$ and rate $b$ can be strongly correlated. Center `StartIndex` within or near the observed range.
- The two-parameter logistic is structurally constrained to have its midpoint at `StartIndex`; a displaced transition may be better represented by `StepFunction` or a different authorized formulation.
- Step changes are difficult to identify when $t_c$ is weakly bounded or few observations occur on either side.
- A sinusoid requires enough cycles and suitable sampling to distinguish periodicity from trend and noise.
- Trend selection performed on the same data introduces model-selection uncertainty. Compare predictive performance and inspect residual dependence; do not select solely by the largest fitted slope.
- Nonstationarity in annual maxima can reflect changing climate, regulation, land use, rating practice, or record artifacts. The mathematical trend does not identify the cause.

## Serialization

All trend implementations serialize `OwnerName`, `UseDefaultFlatPriors`, `Type`, `StartIndex`, and parameter state. `GeneralLinearFunction` additionally stores matrix dimensions and invariant-culture comma-separated covariate values. Clone operations deep-copy parameter objects; the general linear model also clones its matrix. Round-trip equality establishes configuration persistence, not statistical equivalence under changed units or covariate preprocessing.

## Implementation and evidence

| Concern | Implementation source | Evidence |
|---|---|---|
| Contract/base state | `src/RMC.BestFit/Models/TrendFunctions/Support/ITrendModel.cs`, `TrendModelBase.cs` | trend unit tests in `src/RMC.BestFit.Tests` |
| Ten index functions | `src/RMC.BestFit/Models/TrendFunctions/*.cs` | formula, boundary, clone, and XML tests |
| Covariate regression | `GeneralLinearFunction.cs` | spatial-extremes unit/verification tests |
| Nonstationary likelihood | `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs` | nonstationary univariate verification tests |
| Compile-checked example | `src/RMC.BestFit.Tests/Documentation/Examples/FoundationExamples.cs` | `TechnicalReferenceDocumentationTests` |

## References

<a id="ref-1"></a>[1] P. F. Chandler and H. S. Wheater, "Analysis of rainfall variability using generalized linear models: A case study from the west of Ireland," *Water Resources Research*, vol. 38, no. 10, 2002. doi: 10.1029/2001WR000906.

<a id="ref-2"></a>[2] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001. doi: 10.1007/978-1-4471-3675-0.

[<- Data likelihood](../data-frame/index.md) | [Technical reference index](../index.md) | [Next: Link functions ->](link-functions.md)
