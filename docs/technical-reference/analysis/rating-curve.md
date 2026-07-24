<!-- technical-reference-status: complete -->

# Stage–Discharge Rating Curves

[Technical reference](../index.md) | [Time-series models](time-series.md)

## Purpose and Scope

`RatingCurve` relates gauge stage $h$ to positive discharge $Q$ using one to three accumulating hydraulic controls. It is a statistical calibration model for paired gaugings, not a hydraulic solver. Stage is treated as exact, the curve is time invariant, residuals are independent and homoscedastic in base-10 log discharge, and only BaRatin-style addition mode is exposed. Hysteresis, backwater, unsteady flow, shifting controls, uncertain stage/discharge measurements, succession/drowning controls, and a user-specified control matrix are outside the implemented likelihood.

## Addition-Mode Formulation

For $K\in\{1,2,3\}$ controls,

$$
q(h;\theta)=
\sum_{k=1}^{K}10^{a_k}(h-h_k)^{\beta_k}
\mathbf 1(h>h_k), \tag{RC.1}
$$

where $h_1$ is the main-channel zero-flow stage and $h_k$ for $k\ge2$ is both an activation stage and power-law offset. In code, the first indicator is implemented by returning zero when $h\le h_1$; higher-control terms are simply omitted below their thresholds. The parameter vector is

$$
(h_1,a_1,\beta_1,\ldots,h_K,a_K,\beta_K,\sigma),
\qquad a_k=\log_{10}\alpha_k, \tag{RC.2}
$$

of length $3K+1$. Stage parameters have stage units; discharge has the supplied discharge units; $10^{a_k}$ has discharge units divided by stage units raised to $\beta_k$; $\beta_k$ is dimensionless; and $\sigma$ is a standard deviation in log10-discharge units.

The ordering constraint is $h_1<h_2<h_3$ for configured controls. Each added contribution approaches zero at activation only for $\beta_k>0$. Default bounds allow $\beta_k=0$, which creates a jump immediately above the breakpoint and contradicts the unconditional continuity claim ([TR-044](../review-findings.md#tr-044)).

Equation (RC.1) is the lower-triangular-all-ones control-matrix case of the BaRatin matrix-of-controls framework: existing controls continue conveying flow as new controls activate. It can represent main-channel plus overbank contributions. It should not be applied where one control drowns out and is replaced by another without first extending the model.

## Observation Model and Full Likelihood

BestFit inner-joins `StageData` and `DischargeData` by `DateTime`; unmatched records do not contribute. For aligned pair $i$,

$$
Z_i=\log_{10}Q_i
\mid h_i,\theta\sim
N\!\left(\log_{10}q(h_i;\theta),\sigma^2\right). \tag{RC.3}
$$

The implemented transformed-response log likelihood is

$$
\ell_Z(\theta)=
-\frac n2\log(2\pi)-n\log\sigma
-\frac{1}{2\sigma^2}\sum_{i=1}^{n}
\left[\log_{10}Q_i-\log_{10}q(h_i;\theta)\right]^2. \tag{RC.4}
$$

If interpreted as a density for the observed discharge $Q_i$, change of variables gives

$$
\ell_Q(\theta)=\ell_Z(\theta)-
\sum_{i=1}^{n}\log(Q_i\ln 10). \tag{RC.5}
$$

The code implements (RC.4), not (RC.5), while exposing general log-likelihood and information-criterion fields. The omitted data-only term leaves parameter estimates unchanged but shifts absolute predictive density and criteria; see [TR-043](../review-findings.md#tr-043).

At least ten aligned pairs are required. Nonfinite parameters, invalid threshold ordering, nonpositive predicted flow, and invalid marginal-prior support make a fit impossible. `Validate()` currently checks nonpositive discharge across the entire discharge series rather than aligned pairs only ([TR-045](../review-findings.md#tr-045)). Stage/discharge duplicates at the same timestamp are not modeled as replicate measurements; the date dictionary determines the aligned value.

## Priors and Posterior

Default priors are bounded uniforms built from observed stage span:

- $h_1$ ranges below/near the minimum calibration stage;
- $h_2$ and $h_3$ occupy overlapping interior stage-span ranges;
- $a_k\in[-10,10]$;
- $\beta_k\in[0,5]$;
- $\sigma$ has a positive data-scaled upper bound.

With `UseJeffreysRuleForScale=true`, the full log prior is

$$
\ell_P(\theta)=\sum_j\log\pi_j(\theta_j)-\log\sigma. \tag{RC.6}
$$

The posterior is proportional to $\exp\{\ell_Z+\ell_P\}$. Uniform density constants matter to the posterior kernel reported by the API even though they do not change a within-model posterior. `RatingCurveAnalysis` currently passes the MAP full target to AIC/BIC ([TR-042](../review-findings.md#tr-042)); those fields are not conventional criteria.

## Identifiability and Extrapolation

Addition does not guarantee separable parameters. Identification requires observations below and above every activation stage, enough depth range to distinguish $a_k$ from $\beta_k$, and informative data near breakpoints. Strong posterior correlations commonly occur among $h_k$, $a_k$, and $\beta_k$; an activation stage outside dense data leaves the new control weakly identified. Compare one-, two-, and three-control structures only with valid data-likelihood criteria or held-out prediction, not the affected analysis AIC/BIC fields.

High-stage extrapolation is dominated by the largest active exponent and coefficient. A narrow posterior over calibration stages can still produce enormous uncertainty beyond the highest gauging. Report the maximum gauged stage, the requested extrapolation ratio/depth, hydraulic plausibility of each control, and sensitivity to priors. Do not interpret statistical smoothness as evidence that channel geometry, roughness, backwater, or control regime remains fixed during an extreme flood.

## Results and Uncertainty

`Predict(parameters, stage)` returns the median conditional discharge because the normal error has median zero in log10 space. For a single realization, `Predict(parameters, stage, seed)` adds $N(0,\sigma^2)$ log error. Conditional mean discharge is

$$
E(Q\mid h,\theta)=q(h;\theta)
\exp\left\{\tfrac12(\sigma\ln10)^2\right\}. \tag{RC.7}
$$

`RatingCurveAnalysis` builds its displayed mode curve deterministically. Uncertainty curves use posterior parameter draws plus seeded log errors, so they are posterior predictive, not uncertainty bands for the latent median rating relation. If the engineering question is uncertainty in the rating curve itself, exclude residual draws or present both epistemic and predictive bands after a production/API enhancement.

Stage–discharge pairs are cached after the date inner join. Model RMSE uses aligned observations. Cancellation is propagated through the common Bayesian analysis lifecycle. Simulation through `ISimulatable<double[]>` resamples stages from all `StageData`, not necessarily the aligned calibration stages; this should be stated when predictive checks are used.

## Compile-Checked Workflow

<!-- snippet: rating-curve-workflow -->
```csharp
private static (
    RatingCurveAnalysis Analysis,
    MaximumLikelihood InitialFit) ConfigureRatingCurveAnalysis()
{
    double[] stage =
    {
        1.2, 1.4, 1.7, 2.0, 2.3, 2.7,
        3.1, 3.6, 4.1, 4.7, 5.3, 6.0
    };
    double[] discharge =
    {
        18, 29, 51, 79, 113, 169,
        238, 345, 476, 651, 861, 1_140
    };
    var start = new DateTime(2024, 1, 1);
    var stageSeries = new NumericsTimeSeries(
        TimeInterval.OneDay,
        start,
        stage);
    var dischargeSeries = new NumericsTimeSeries(
        TimeInterval.OneDay,
        start,
        discharge);

    var model = new RMC.BestFit.Models.RatingCurve(
        stageSeries,
        dischargeSeries,
        numberOfSegments: 1);
    var analysis = new RatingCurveAnalysis(model);
    var initialFit = new MaximumLikelihood(
        model,
        OptimizationMethod.DifferentialEvolution);

    return (analysis, initialFit);
}
```

Call `initialFit.Estimate()` for a data-likelihood starting solution; then run `analysis.RunAsync()` for Bayesian uncertainty. A one-control example is appropriate because twelve observations do not support a defensible ten-parameter three-control model. Before fitting multiple controls, plot gaugings by stage, predefine plausible activation ranges from cross-section/control evidence, and verify that each regime contains adequate data.

## Validation and Traceability

Implementation: `Models/RatingCurve/RatingCurve.cs`; orchestration: `Analyses/RatingCurve/RatingCurveAnalysis.cs`. Fast tests cover parameter order, addition behavior, date alignment, likelihood decomposition, serialization, validation, and deterministic simulation. Computational recovery and analysis sources under `RMC.BestFit.Verification/RatingCurve/` were inspected but not executed. Existing evidence supports API behavior; it does not validate BaRatin parity for arbitrary control matrices or uncertainty coverage in extrapolation.

## References

<a id="ref-1"></a>[1] J. Le Coz, B. Renard, L. Bonnifait, F. Branger, and R. Le Boursicaud, “Combining hydraulic knowledge and uncertain gaugings in the estimation of hydrometric rating curves: A Bayesian approach,” *J. Hydrol.*, vol. 509, pp. 573–587, 2014.

<a id="ref-2"></a>[2] E. J. Kennedy, *Discharge Ratings at Gaging Stations*, USGS Techniques of Water-Resources Investigations, book 3, chap. A10, 1984.

<a id="ref-3"></a>[3] S. E. Rantz et al., *Measurement and Computation of Streamflow*, USGS Water-Supply Paper 2175, 1982.

<a id="ref-4"></a>[4] ISO 1100-2:2010, *Hydrometry—Measurement of liquid flow in open channels—Part 2: Determination of the stage–discharge relationship*.
