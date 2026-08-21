<!-- technical-reference-status: complete -->

# Peaks over Threshold: Poisson Point-Process Model

[Previous: Competing Risks](competing-risks.md) | [Technical Reference](../index.md) | [Next: Composite Distributions](composite.md)

**PointProcessModel** fits threshold exceedances with the GEV-compatible Poisson point-process parameterization of extreme-value theory. It supports one stationary season or two block-day seasons under a calendar- or water-year convention. The model is not a generic arrival-process framework: every component must be a Numerics **GeneralizedExtremeValue**, and the implemented mark/rate likelihood is the limiting Poisson model associated with the GEV family.

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
| \(n_e\) | exact records treated as Poisson events | **EmpiricalEventCount** |
| \(\widehat\lambda_e=n_e/N_y\) | empirical exact-event rate | **EmpiricalEventRate**, **Lambda** compatibility alias |
| \(\Lambda_u\) | fitted threshold intensity | **FittedThresholdIntensity** |

The feasibility condition is

$$
1+\xi\frac{x-\mu}{\sigma}>0. \tag{1}
$$

The **PointProcessModel** parameter vector is \((\mu,\sigma,\kappa)\) when nonseasonal. In a seasonal model it is

$$
(k_1,k_2,\mu_1,\sigma_1,\kappa_1,
\mu_2,\sigma_2,\kappa_2), \tag{2}
$$

where \(k_1\) and \(k_2\) are effective integer change-point days constrained by \(1\le k_1<k_2\le366\). Continuous latent proposals are floored once before use. The approved broad latent supports are half-open \([1,251)\) for the first value and \([200,367)\) for the second. These supports retain the paper's full second-changepoint range while allowing the first break to occur early in the configured block year [3](#ref-3).

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

Thus the occurrence rate is not a separate fitted Poisson parameter: equations (4)–(6) determine it from the GEV-compatible parameters. **FittedThresholdIntensity** exposes that fitted rate. **EmpiricalEventRate** is the exact-event count divided by exposure, while **Lambda** remains its compatibility alias; neither is a member of \(\boldsymbol\theta\) nor an additional factor in equation (5).

## Relationship to the Generalized Pareto Distribution

Conditional on an exceedance above \(u\), the excess \(Y=X-u\) has the generalized Pareto limit

$$
P(Y\le y\mid X>u)
=1-\left(1+\xi\frac{y}{\sigma_u}\right)^{-1/\xi},
\qquad \sigma_u=\sigma+\xi(u-\mu), \tag{7}
$$

on \(y\ge0\) subject to \(1+\xi y/\sigma_u>0\). BestFit fits the equivalent point-process likelihood in equations (5)-(6), not a separate **GeneralizedPareto** model. For simulation, the configured Hosking GEV is converted to a Hosking GPA using the Madsen relationship

$$
\mu_{GPA}=u,\qquad
\sigma_{GPA}=\sigma_{GEV}\widehat\lambda_e^{\kappa},\qquad
\kappa_{GPA}=\kappa_{GEV}. \tag{7a}
$$

Both simulation APIs use the empirical **Lambda** as the annual Poisson mean of a nonseasonal process. A seasonal process uses the fitted threshold intensity \(\Lambda_j(u)\) of each season, the rate season \(j\) would produce over a full year: the Madsen conversion of component \(j\) uses \(\Lambda_j(u)\), the annual Poisson mean is \(w_1\Lambda_1(u)+w_2\Lambda_2(u)\) (**FittedThresholdIntensity**), and each event is assigned to season one with probability \(w_1\Lambda_1(u)/(w_1\Lambda_1(u)+w_2\Lambda_2(u))\), then marked from the corresponding converted GPA. The simulated season-\(j\) annual maximum therefore follows the exposure-annualized seasonal distribution \(G_j^{w_j}\) used by the likelihood.

## Seasonal Likelihood

Season 1 contains days \(d<\lfloor k_1\rfloor\) or \(d\ge\lfloor k_2\rfloor\); season 2 contains \(\lfloor k_1\rfloor\le d<\lfloor k_2\rfloor\). Their exposures are

$$
N_{y1}=N_y\frac{k_1+366-k_2}{366},
\qquad
N_{y2}=N_y\frac{k_2-k_1}{366}. \tag{8}
$$

Each exact event contributes the intensity-density term from its assigned season, and each season contributes its own \(-N_{ys}\Lambda_{u,s}\) term. **POTDays** is the one-based elapsed day from the selected calendar- or water-year block start. This elapsed-day calculation, rather than a month shift, is shared by observed and generated events and preserves leap days. Uncertain, interval, and threshold-count records do not carry a usable day assignment.

The fractions \(p_s=N_{ys}/N_y\) weight the two seasonal point-process intensities; they are not mixture probabilities applied to annual GEV distributions. If \(M_s\) is the maximum generated by process \(s\) over its seasonal exposure, then

$$
F_{M_s}(x)=\exp[-p_s\Lambda_s(x)]. \tag{8a}
$$

The annual maximum is the maximum of the two independent seasonal maxima, so

$$
F_A(x)=P\{\max(M_1,M_2)\le x\}
=F_{M_1}(x)F_{M_2}(x)
=\exp[-p_1\Lambda_1(x)-p_2\Lambda_2(x)]. \tag{8b}
$$

For frequency-output composition, BestFit represents each \(F_{M_s}\) as an exposure-adjusted GEV. In Numerics-sign notation, for \(\kappa_s\ne0\),

$$
\widehat\mu_s
=\mu_s+\frac{\sigma_s}{\kappa_s}
\left(1-p_s^{-\kappa_s}\right),
\qquad
\widehat\sigma_s=\sigma_s p_s^{-\kappa_s}, \tag{9}
$$

with Gumbel limit \(\widehat\mu_s=\mu_s-\sigma_s\log p_s\) and \(\widehat\sigma_s=\sigma_s\). The resulting **CompetingRisks** distribution is the independent maximum of the two transformed seasonal components. It is equivalent to equation (8b), not to a weighted-mixture CDF \(p_1F_1+p_2F_2\).

## Non-Exact Observation Contributions

After the exact point-process contribution, the implemented hybrid likelihood evaluates other records against a competing-risk GEV magnitude distribution:

- uncertain observations use 20-point Gauss-Legendre integration of measurement-error density times composite density over the central (1-2\times10^{-8}) error-distribution mass, divided by that retained mass;
- interval records contribute the log probability between their bounds; and
- threshold counts contribute repeated left- and/or right-censored log probabilities.

These records are block-indexed annual magnitude information. They do not become Poisson events and do not receive an event-density or additional exposure contribution. In a seasonal model they have no event day because their index identifies the annual block, not a seasonal process. Their likelihood therefore uses the annual **CompetingRisks** distribution in equation (8b): exposure weights act on the process intensities, each seasonal process produces an exposure-adjusted seasonal maximum, and the annual observation is evaluated against the maximum of those two independent maxima. The weights are never applied as annual mixture probabilities.

For WAIC and LOO, the global rate term is divided equally among exact events. If there are no exact events, it is attached to the first non-exact entry so that

$$
\sum_i \ell_{D,i}(\boldsymbol\theta)
=\ell_D(\boldsymbol\theta). \tag{10}
$$

The allocation preserves the sum but is not unique; pointwise influence attributed to the rate term depends on this convention.
## Priors

The posterior target adds each **ModelParameter** prior. With **UseJeffreysRuleForScale**, one \(-\log\sigma_s\) term is added per seasonal GEV scale. A single quantile prior is evaluated on the composite annual distribution. The multi-quantile reparameterization is accepted only for a nonseasonal single-component model with three quantile priors, in which case the quantile-density terms and the transformation Jacobian enter the prior decomposition. See [Parameters and Priors](../models/parameters-and-priors.md).

When default flat priors are enabled, seasonal changepoints use a fast empirical occurrence-histogram rule. Exact dated events are counted in the existing 12 calendar-month bins and rotated so bin one is the configured block-year start. Counts first receive an effectively-flat check against month-length exposure using the Pearson statistic and the fixed 11-degree-of-freedom 95% cutoff 19.675. Non-flat counts receive one circular \([1,2,1]/4\) smoothing pass; the uniquely strongest separated peak pair defines two arcs, and the minimum-frequency month on each arc defines an approximate break. Each prior is a five-month flat window centered on the detected valley cell and intersected with the approved broad support. The latent initial value is the valley-cell center; the effective changepoint remains its floor.

The rule is not an estimator: it evaluates no likelihood, fit, MAP objective, or MCMC state. Fewer than ten dated exact events, flat/effectively flat or unimodal structure, ambiguous tied peak pairs, or an incompatible window retain the broad defaults. Index-only manually entered POT data therefore retain the broad priors. A seasonal model is invalid unless every exact POT observation has a nondefault **DateTime**, because its process assignment requires a block day. Index-only exact records remain valid for nonseasonal fits and for their year/index-span exposure fallback. Set **UseDefaultFlatPriors** to false before supplying custom priors; serialization remains the ordinary **ModelParameter** format.

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

With defaults enabled, BestFit uses a supplied finite POT metadata threshold only if it lies below the smallest exact value; otherwise it uses the floating-point value immediately below the smallest exact value. When exact data are absent, it uses the value immediately below the smallest uncertain/interval representative.

Exposure precedence is explicit model **TotalYears**, then **DataFrame.PointProcessObservationYears**, then the exact-series year/index span (or one year when no exact record exists). POT extraction records the source time-series year count before discarding sub-threshold observations, preserving leading and trailing zero-event years. Manually entered POT data normally lack that metadata, so the year/index span remains the compatibility fallback. **IsTotalYearsInferred** identifies that fallback, and validation warns that users should override it when station-history coverage is known. Only exact records enter **EmpiricalEventCount** and **EmpiricalEventRate**; non-exact rows remain magnitude-likelihood observations.

## Simulation APIs

**GenerateRandomValues(sampleSize, seed)** draws annual counts from `Poisson(Lambda)` for a nonseasonal process, or from `Poisson(w_1 Lambda_1 + w_2 Lambda_2)` with the fitted seasonal intensities, until exactly `sampleSize` exceedances have been retained. It returns only magnitudes because its established return type is `double[]`. In seasonal mode, each event is assigned to a season in proportion to \(w_j\Lambda_j(u)\), then marked from the assigned Madsen-converted GPA.

**GeneratePOTTimeSeries(sampleSize, seed)** uses the same annual Poisson batches, seasonal assignments, and GPA marks, and adds dummy dates in leap-containing blocks beginning in 2000. The block start follows the configured calendar-, water-, or custom-year setting.

**GeneratePOTTimeSeries(startDate, durationYears, seed)**:

1. converts each raw Hosking GEV component to its Hosking GPA using equation (7a) with the empirical **Lambda** (nonseasonal) or the component's fitted threshold intensity \(\Lambda_j(u)\) (seasonal);
2. draws one total count from `Poisson(durationYears * Lambda)` (nonseasonal) or `Poisson(durationYears * (w_1 Lambda_1 + w_2 Lambda_2))` (seasonal);
3. assigns each seasonal event in proportion to the exposure-weighted intensities \(w_j\Lambda_j(u)\);
4. samples a date uniformly from the requested span subject to the same block-day season predicate used by the likelihood; and
5. samples the mark from the assigned GPA and sorts the date-magnitude pairs.

A positive seed is deterministic; nonpositive seeds use a clock-seeded generator. Equation (9)'s annualized transform is used for frequency-output composition, not for Poisson-GPA generation.

## Failure Modes and Extrapolation Cautions

- Declustering and threshold selection occur outside the point-process likelihood. Dependent clusters will overstate information.
- Event-span fallback exposure can be biased if the first or last retained event does not delimit the actual observation period; heed the validation warning or supply source exposure.
- Manually entered undated exact records are suitable for nonseasonal exposure fallback, but validation rejects them for seasonal fitting.
- Seasonal uncertain, interval, and threshold-count records are annual/block-indexed and use the annual maximum distribution in equation (8b); their indexes do not assign them to a seasonal process.
- Shape estimates near zero switch formula at \(10^{-4}\), while seasonal annualization uses a tighter \(10^{-8}\) limit; check continuity in sensitive cases.
- A negative Coles shape imposes a finite upper endpoint; proposed parameters violating support return negative infinity.
- Two seasons add two change points and two complete GEV parameter sets. Sparse seasonal events can yield weak identification and partition sensitivity.
- Floored changepoint posteriors can be genuinely multimodal. Assess integer-day posterior mass, credible sets, and posterior-predictive recovery rather than posterior means alone [3](#ref-3).
- The mixed-data extension lacks event-time uncertainty and does not add uncertain or interval records to the Poisson event product.

## Implementation and Verification Traceability

The 31 July 2026 current-source backcheck closes TR-004 and TR-005 in the approved point-process scope. All ten guarded cells pass. Recovery fixtures use 1,000 observations and the untouched `BayesianAnalysis` defaults: DEMCzs, four chains, 1,500 warmup iterations, 3,000 sampling iterations, thinning 20, and seed 12345. Calendar-year uniform recovery, October-water-year block-origin parity, and both production-generator recovery cells pass. The initial water-year failure came from changing the block-day changepoints to `80/260`; keeping `170/350` and changing only the dates and block convention produces the same modeled partition and passes. No production formula, sampler default, prior, or tolerance changed. See the [point-process verification report](../../verification/point-process.md#current-source-guarded-results) for exact outcomes.

| Concern | Implementation |
|---|---|
| Model, likelihood, priors, simulation | **Models/UnivariateDistribution/PointProcessModel.cs** |
| Bayesian orchestration and output | **Analyses/Univariate/PointProcessAnalysis.cs** |
| Threshold diagnostics | **Models/DataFrame/ThresholdDiagnostics.cs** |
| Composite annual distribution | pinned **Numerics/Distributions/Univariate/CompetingRisks.cs** |
| GEV parameterization | pinned **Numerics/Distributions/Univariate/GeneralizedExtremeValue.cs** |
| Fast contracts | **RMC.BestFit.Tests/Univariate/PointProcessModelTests.cs**, **PointProcessAnalysisTests.cs**, **PointProcessChangePointPriorTests.cs**, **DataFrame/ExactDataProcessTests.cs** |
| Scientific verification cells | **RMC.BestFit.Verification/Univariate/PointProcessTests/PointProcessSeasonalFixture.cs**, **PointProcessPriorTests.cs**, **PointProcessRecoveryTests.cs**, **PointProcessRecoveryTests.Uniform.cs**; see [point-process verification](../../verification/point-process.md) |

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

<a id="ref-2"></a>[2] A. C. Davison and R. L. Smith, “Models for exceedances over high thresholds,” *Journal of the Royal Statistical Society: Series B*, vol. 52, no. 3, pp. 393–442, 1990.

<a id="ref-3"></a>[3] S. G. Coles and L. R. Pericchi, “Anticipating catastrophes through extreme value modelling,” *Journal of the Royal Statistical Society: Series C*, vol. 52, no. 4, pp. 405–416, 2003.

---

[Previous: Competing Risks](competing-risks.md) | [Technical Reference](../index.md) | [Next: Composite Distributions](composite.md)
