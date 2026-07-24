<!-- technical-reference-status: complete -->

# Peaks over Threshold: Poisson Point-Process Model

[Previous: Competing Risks](competing-risks.md) | [Technical Reference](../index.md) | [Next: Composite Distributions](composite.md)

**PointProcessModel** fits threshold exceedances with the GEV-compatible Poisson point-process parameterization of extreme-value theory. It supports one stationary season or two day-of-year seasons. The model is not a generic arrival-process framework: every component must be a Numerics **GeneralizedExtremeValue**, and the implemented mark/rate likelihood is the limiting Poisson model associated with the GEV family.

## Notation and Parameterization

| Symbol | Meaning | API |
|---|---|---|
| \(u\) | fixed threshold, in data units | **Threshold** |
| \(N_y\) | exposure duration, years | **TotalYears** |
| \(x_i\) | exact exceedance magnitude | **DataFrame.ExactSeries[i].Value** |
| \(\mu\) | point-process/GEV location, data units | Numerics **Xi** |
| \(\sigma>0\) | scale, data units | Numerics **Alpha** |
| \(\xi\) | Coles extreme-value shape | negative Numerics **Kappa** |
| \(\kappa\) | Numerics Hosking shape | \(\kappa=-\xi\) |
| \(\widehat\lambda\) | displayed empirical events per year | **Lambda** |

The feasibility condition is

$$
1+\xi\frac{x-\mu}{\sigma}>0. \tag{1}
$$

The **PointProcessModel** parameter vector is \((\mu,\sigma,\kappa)\) when nonseasonal. In a seasonal model it is

$$
(k_1,k_2,\mu_1,\sigma_1,\kappa_1,
\mu_2,\sigma_2,\kappa_2), \tag{2}
$$

where \(k_1\) and \(k_2\) are day-of-year change points constrained by \(1\le k_1<k_2\le366\). Default parameter bounds further place the first change point in days 10–170 and the second in days 171–330.

## Point-Process Intensity and Likelihood

For \(\xi\ne0\), the intensity density in magnitude is

$$
\nu(x;\mu,\sigma,\xi)
=\frac{1}{\sigma}
\left[1+\xi\frac{x-\mu}{\sigma}\right]^{-1/\xi-1}, \tag{3}
$$

and the expected annual number of points above \(u\) is the tail intensity measure

$$
\Lambda_u(\mu,\sigma,\xi)
=\left[1+\xi\frac{u-\mu}{\sigma}\right]^{-1/\xi}. \tag{4}
$$

For independent points observed over \(N_y\) years, constants that do not depend on the parameters may be omitted, giving

$$
\ell_D(\mu,\sigma,\xi)
=\sum_{i=1}^{n}
\left\{-\log\sigma-\left(1+\frac{1}{\xi}\right)
\log\left[1+\xi\frac{x_i-\mu}{\sigma}\right]\right\}
-N_y\Lambda_u. \tag{5}
$$

When \(|\xi|<10^{-4}\), the implementation uses the Gumbel limit

$$
\ell_D(\mu,\sigma,0)
=\sum_{i=1}^{n}\left[-\log\sigma-
\frac{x_i-\mu}{\sigma}\right]
-N_y\exp\left[-\frac{u-\mu}{\sigma}\right]. \tag{6}
$$

Thus the occurrence rate is not a separate fitted Poisson parameter: equations (4)–(6) determine it from the GEV-compatible parameters. The public **Lambda** property is a data summary, not a member of \(\boldsymbol\theta\) and not a factor explicitly inserted into equation (5).

## Relationship to the Generalized Pareto Distribution

Conditional on an exceedance above \(u\), the excess \(Y=X-u\) has the generalized Pareto limit

$$
P(Y\le y\mid X>u)
=1-\left(1+\xi\frac{y}{\sigma_u}\right)^{-1/\xi},
\qquad \sigma_u=\sigma+\xi(u-\mu), \tag{7}
$$

on \(y\ge0\) subject to \(1+\xi y/\sigma_u>0\). BestFit does not fit a separate **GeneralizedPareto** object in this model; it uses the equivalent point-process likelihood in equations (5)–(6). This distinction matters because **GenerateRandomValues(...)** samples the configured competing-risk GEV marginals, whereas **GeneratePOTTimeSeries(...)** conditions generated marks above \(u\).

## Seasonal Likelihood

Season 1 contains days \(d<k_1\) or \(d\ge k_2\); season 2 contains \(k_1\le d<k_2\). Their exposures are

$$
N_{y1}=N_y\frac{k_1+366-k_2}{366},
\qquad
N_{y2}=N_y\frac{k_2-k_1}{366}. \tag{8}
$$

Each exact event contributes the intensity-density term from its assigned season, and each season contributes its own \(-N_{ys}\Lambda_{u,s}\) term. **POTDays** is derived from the exact series using the selected **TimeBlock** and **StartMonth**; uncertain, interval, and threshold-count records do not carry a usable day-of-year assignment.

For frequency-output composition, BestFit transforms each seasonal GEV so its annual component represents the corresponding seasonal fraction \(p_s=N_{ys}/N_y\). In Numerics-sign notation, for \(\kappa_s\ne0\),

$$
\widehat\mu_s
=\mu_s+\frac{\sigma_s}{\kappa_s}
\left(1-p_s^{-\kappa_s}\right),
\qquad
\widehat\sigma_s=\sigma_s p_s^{-\kappa_s}, \tag{9}
$$

with Gumbel limit \(\widehat\mu_s=\mu_s-\sigma_s\log p_s\) and \(\widehat\sigma_s=\sigma_s\). The resulting annual distribution is the independent maximum of the two transformed components.

## Non-Exact Observation Contributions

After the exact point-process contribution, the implementation evaluates other records against the composite GEV distribution:

- uncertain observations use 20-point Gauss–Legendre integration of measurement-error density times composite density over the central \(1-2\times10^{-8}\) error-distribution mass, divided by that retained mass;
- interval records contribute the log probability between their bounds; and
- threshold counts contribute repeated left- and/or right-censored log probabilities.

These records are treated as block-indexed magnitude information and do not receive separate Poisson event-density or exposure contributions. This is an implemented hybrid likelihood, not the standard marked-point-process treatment of uncertain event times and counts. Studies using substantial non-exact POT information should justify this observation model explicitly.

For WAIC and LOO, the global rate term is divided equally among exact events. If there are no exact events, it is attached to the first non-exact entry so that

$$
\sum_i \ell_{D,i}(\boldsymbol\theta)
=\ell_D(\boldsymbol\theta). \tag{10}
$$

The allocation preserves the sum but is not unique; pointwise influence attributed to the rate term depends on this convention.

## Priors

The posterior target adds each **ModelParameter** prior. With **UseJeffreysRuleForScale**, one \(-\log\sigma_s\) term is added per seasonal GEV scale. A single quantile prior is evaluated on the composite annual distribution. The multi-quantile reparameterization is accepted only for a nonseasonal single-component model with three quantile priors, in which case the quantile-density terms and the transformation Jacobian enter the prior decomposition. See [Parameters and Priors](../models/parameters-and-priors.md).

## Compile-Checked Configuration

<!-- snippet: point-process-workflow -->
```csharp
private static PointProcessAnalysis ConfigurePointProcess(
    global::RMC.BestFit.Models.DataFrame peaks)
{
    var parent = new CompetingRisks(
        new UnivariateDistributionBase[]
        {
            new GeneralizedExtremeValue()
        })
    {
        MinimumOfRandomVariables = false,
        Dependency = Probability.DependencyType.Independent
    };

    var model = new PointProcessModel(peaks, parent)
    {
        UseDefaults = false,
        Threshold = 1200.0,
        TotalYears = 25.0,
        IsSeasonal = false
    };

    return new PointProcessAnalysis(model);
}
```

The numerical threshold and exposure are illustrative configuration values, not a recommended choice. Use consistent flow units, count only independent events, and derive exposure from the actual interval during which events above the selected threshold would have been observed.

## Threshold Selection and Exposure

A useful threshold is high enough for the tail approximation to be credible but low enough to retain adequate information. **ThresholdDiagnostics**, **MeanResidualLifeResult**, and **ParameterStabilityResult** support threshold sensitivity work. A defensible analysis examines mean residual life, shape/modified-scale stability, event independence, seasonal changes, and the stability of decision-relevant quantiles—not one diagnostic in isolation [1](#ref-1).

With defaults enabled, BestFit uses a supplied finite POT metadata threshold only if it lies below the smallest exact value; otherwise it uses the floating-point value immediately below the smallest exact value. When exact data are absent, it uses the value immediately below the smallest uncertain/interval representative. Default exposure is the exact-series index span or one year if no exact record exists. These are initialization heuristics, not substitutes for station-history metadata.

**CalculateLambda()** currently counts exact, uncertain, and interval records, whereas **GeneratePOTTimeSeries()** uses exact records only and the fitted likelihood derives rate from equation (4). This discrepancy is [TR-004](../review-findings.md#tr-004). The simulator also draws event count from the empirical exact-event rate rather than the fitted point-process intensity; it should not be described as a posterior predictive realization of equations (3)–(6) until [TR-005](../review-findings.md#tr-005) is resolved.

## Simulation APIs

**GenerateRandomValues(sampleSize, seed)** draws one value from every configured GEV component and takes their minimum or maximum according to the **CompetingRisks** setting. It does not condition values above the threshold and does not simulate arrival times.

**GeneratePOTTimeSeries(startDate, durationYears, seed)**:

1. draws an event count from a Poisson distribution with mean **ExactSeries.Count / TotalYears * durationYears**;
2. assigns event dates uniformly over **durationYears * 365.25** days;
3. selects a seasonal marginal from the generated calendar day, when enabled;
4. samples that marginal conditional on exceeding **Threshold**; and
5. sorts the date–magnitude pairs.

A positive seed is deterministic in the pinned implementation; nonpositive seeds use a clock-seeded generator. The seasonal simulator applies raw component parameters rather than the annualized transform in equation (9), another aspect included in [TR-005](../review-findings.md#tr-005).

## Failure Modes and Extrapolation Cautions

- Declustering and threshold selection occur outside the point-process likelihood. Dependent clusters will overstate information.
- The default exposure can be biased if the first or last observed exceedance does not delimit the actual observation period.
- Shape estimates near zero switch formula at \(10^{-4}\), while seasonal annualization uses a tighter \(10^{-8}\) limit; check continuity in sensitive cases.
- A negative Coles shape imposes a finite upper endpoint; proposed parameters violating support return negative infinity.
- Two seasons add two change points and two complete GEV parameter sets. Sparse seasonal events can yield weak identification and partition sensitivity.
- The mixed-data extension lacks event-time uncertainty and does not add uncertain/interval records to the Poisson event product.

## Implementation and Verification Traceability

| Concern | Implementation |
|---|---|
| Model, likelihood, priors, simulation | **Models/UnivariateDistribution/PointProcessModel.cs** |
| Bayesian orchestration and output | **Analyses/Univariate/PointProcessAnalysis.cs** |
| Threshold diagnostics | **Models/DataFrame/ThresholdDiagnostics.cs** |
| Composite annual distribution | pinned **Numerics/Distributions/Univariate/CompetingRisks.cs** |
| GEV parameterization | pinned **Numerics/Distributions/Univariate/GeneralizedExtremeValue.cs** |

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

<a id="ref-2"></a>[2] A. C. Davison and R. L. Smith, “Models for exceedances over high thresholds,” *Journal of the Royal Statistical Society: Series B*, vol. 52, no. 3, pp. 393–442, 1990.

---

[Previous: Competing Risks](competing-risks.md) | [Technical Reference](../index.md) | [Next: Composite Distributions](composite.md)
