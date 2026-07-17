# Rating Curve Analysis

[<- Previous: Coincident Frequency](coincident-frequency.md) | [Back to Index](../../index.md) | [Next: Time Series ->](time-series.md)

A rating curve describes the relationship between river stage (water level) and discharge (flow rate) at a gauging station. This relationship is fundamental to hydrology — while continuous water level measurements are relatively easy to obtain using pressure transducers or float gauges, direct discharge measurements are expensive, time-consuming, and can only be made periodically. The rating curve bridges this gap, allowing hydrologists to convert years of continuous stage records into flow records for flood frequency analysis, water resources planning, and real-time flood forecasting.

This chapter introduces the theory behind stage-discharge relationships, describes the mathematical models implemented in ***RMC-BestFit***, and demonstrates how to fit rating curves using both Maximum Likelihood and Bayesian methods with full uncertainty quantification.

## Why Rating Curves Matter

Consider a typical USGS stream gauging station. A technician might visit the site 10-20 times per year to make direct discharge measurements using an acoustic Doppler current profiler (ADCP) or traditional current meter. Meanwhile, a pressure transducer records water level every 15 minutes — that's 35,000 stage readings per year, but only 10-20 paired with measured discharge.

The rating curve transforms those sparse discharge measurements into a continuous flow record. The quality of flood frequency analysis, water supply forecasting, and dam safety assessments depends critically on the accuracy of this transformation — and on honest uncertainty quantification about what we don't know.

## Physical Background

### The Hydraulics of Open-Channel Flow

At a stable river cross-section, the relationship between water depth and flow rate is governed by fundamental hydraulic principles. For uniform, steady flow in an open channel, the Manning equation describes the relationship:

```math
Q = \frac{1}{n} \cdot A \cdot R^{2/3} \cdot S^{1/2}
```

where:
- $Q$ is discharge (volume per unit time, e.g., cubic feet per second or cubic meters per second)
- $n$ is Manning's roughness coefficient (dimensionless, typically 0.02-0.15 for natural channels)
- $A$ is the cross-sectional area of flow (perpendicular to flow direction)
- $R$ is the hydraulic radius (area divided by wetted perimeter)
- $S$ is the energy gradient (approximately equal to bed slope for uniform flow)

For a channel with a fixed cross-sectional shape, both the flow area $A$ and hydraulic radius $R$ are functions of the water depth $d$. If we measure water level relative to some datum — the stage $h$ — then we can write:

```math
A = f_1(h - \xi)
```
```math
R = f_2(h - \xi)
```

where $\xi$ is the stage at which discharge equals zero (the "zero-flow stage" or "point of zero flow"). The functions $f_1$ and $f_2$ depend on channel geometry.

For many natural channels, these geometric relationships can be approximated by power functions, leading to the classic power-law rating curve:

```math
Q = \alpha \cdot (h - \xi)^{\beta}
```

This deceptively simple equation captures the essential physics: discharge increases as a power of the effective depth $(h - \xi)$, with the coefficient $\alpha$ encoding channel properties (roughness, slope, width) and the exponent $\beta$ reflecting how the flow area and hydraulic radius change with depth.

### Understanding the Parameters

Each parameter in the rating curve equation has physical meaning that helps guide model fitting and interpretation:

**Main-channel Zero-Flow Stage ($h_1$)**: The stage reading at which the main-channel power law evaluates to zero — essentially the elevation of the lowest point of the main hydraulic control relative to the gauge datum. ISO 1100-2:2010 §5.2.5 refers to this as the **effective gauge height of zero flow**. For a gauge with its zero set at the low-flow control (riffle or weir) thalweg, $h_1 \approx 0$; for gauges referenced to an arbitrary datum (common with pressure transducers) $h_1$ might be several feet.

**Activation Stages ($h_2, h_3$)**: Under BaRatin addition mode, each additional control (overbank floodplain, secondary terrace, etc.) activates at a threshold stage $h_k$ and contributes $\alpha_k (h - h_k)^{\beta_k}$ on top of the already-active controls. The activation stage plays the dual role of *threshold* and *offset*. The same naming convention is used for all controls ($h_1, h_2, h_3$) — the main-channel zero-flow stage is simply the "b" offset of control 1.

**Coefficient ($\alpha$)**: This parameter combines the effects of channel roughness, bed slope, and cross-sectional geometry. Larger values indicate a more hydraulically efficient channel — smooth banks, steep gradient, or wide cross-section. The units of $\alpha$ depend on the units of $h$ and $Q$, making physical interpretation of its magnitude difficult without knowing the unit system.

**Exponent ($\beta$)**: The exponent reflects how rapidly the conveyance capacity increases with stage. For a wide rectangular channel where width dominates, $\beta \approx 5/3 \approx 1.67$. For a channel where increasing stage primarily increases depth (narrow, steep banks), $\beta$ can exceed 2.5. Typical values for natural channels:

| Channel Type | Typical $\beta$ | Physical Interpretation |
|--------------|-----------------|------------------------|
| Wide, shallow floodplain | 1.3 - 1.5 | Small depth increase → large area increase |
| Typical natural channel | 1.5 - 2.0 | Balanced depth and width response |
| Deep, narrow channel | 2.0 - 2.5 | Depth dominates width |
| Steep mountain stream | 2.5 - 3.0 | Confined geometry with high gradient |

### Why Channels Change Behavior

Real rivers don't maintain the same geometry across all flow conditions. A typical river has distinct zones:

1. **Low flow**: Water is confined to the main channel thalweg. The wetted perimeter is relatively large compared to flow area, creating high friction losses.

2. **Bankfull flow**: The main channel is full but flow hasn't spilled onto the floodplain. This is often the most hydraulically efficient condition.

3. **Overbank flow**: Water has spilled onto the floodplain. The flow area increases dramatically, but so does friction from vegetation and irregular terrain. The effective slope may decrease as water takes longer paths across the floodplain.

These transitions create distinct "segments" in the rating curve, each with different parameters. The ***RMC-BestFit*** library supports up to three segments to capture these physical changes.

## Mathematical Formulation

### Single-Segment Model

The basic rating curve model relates stage $h$ to discharge $Q$ through a power law:

```math
Q = \alpha \cdot (h - \xi)^{\beta}
```

However, discharge measurements are never perfect. Measurement uncertainty, turbulence, unsteady flow, and channel changes all introduce variability. Following standard practice in hydrology, we model this uncertainty in logarithmic space, which is equivalent to assuming multiplicative errors:

```math
\log_{10}(Q_i) = \log_{10}(\alpha) + \beta \cdot \log_{10}(h_i - \xi) + \varepsilon_i
```

where $\varepsilon_i \sim N(0, \sigma^2)$ represents measurement error in log-space.

The log-space error model has important advantages:
- It ensures predicted discharge remains positive
- It accounts for heteroscedasticity — larger flows have larger absolute uncertainty, but similar relative uncertainty
- It produces residuals that are typically more normally distributed than linear-space residuals

The single-segment model has **four parameters**: $[h_1, \log_{10}(\alpha_1), \beta_1, \sigma]$.

Note that we parameterize the coefficient as $\log_{10}(\alpha_1)$ rather than $\alpha_1$ directly. This improves numerical stability during optimization (since $\alpha$ varies over many orders of magnitude) and produces more symmetric posterior distributions.

### Multi-Segment Model — BaRatin Addition Mode

For compound channels, the main channel continues to carry flow above bankfull while additional hydraulic controls (overbank floodplain, secondary terraces, etc.) activate and contribute on top of the main-channel flow. ***RMC-BestFit*** models this with the BaRatin matrix-of-controls framework (Le Coz et al. 2014 [[5]](#5)) in **addition mode**: each additional control activates at its own threshold stage $h_k$ and adds its own power-law contribution.

The authoritative BaRatin master equation, as implemented in the [reference Fortran engine](https://github.com/BaRatin-tools/BaRatin/blob/main/src/RatingCurve_tools.f90), is:

```math
Q(h) = \sum_{r=1}^{N_{\text{segment}}} \mathbb{1}_{[\kappa_r;\,\kappa_{r+1}]}(h) \,\cdot\, \sum_{j=1}^{N_{\text{control}}} M(r,j)\, a_j\, (h - b_j)^{c_j}
```

where $M(r, j)$ is a lower-triangular binary matrix specifying which controls are active in each stage segment, and $b_j$ is derived from BaRatin's continuity condition. Different choices of $M$ encode different physical configurations:

- **Addition mode** (each row has the 1s of the previous row plus a new 1 — e.g. 2-segment $M = \begin{pmatrix}1&0\\1&1\end{pmatrix}$): controls *accumulate*. A new control activates on top of already-active ones. Continuity collapses to $b_j = \kappa_j$. Appropriate for main-channel + overbank-floodplain (the flood-frequency use case).
- **Succession mode** (each row has exactly one 1 — e.g. 2-segment $M = I_2$): one control *replaces* another. Continuity is enforced by a root-find on $b_j$. Appropriate for drowning weirs / stage-fall-discharge sites.

***RMC-BestFit*** defaults to the addition-mode matrix $M = \begin{pmatrix}1&0&0\\1&1&0\\1&1&1\end{pmatrix}$ (for up to three controls). Succession-mode and mixed matrices are deferred to a future release.

> **Physics of compound channels.** For a main channel + wide-rectangular overbank floodplain, Manning's equation gives $Q_{main}(h) = \alpha_1 (h - h_1)^{\beta_1}$ as a continuing contribution above bankfull, and $Q_{ob}(h) = (1.49/n_{ob})\,W_{ob}\,S^{1/2}\,(h - h_{bf})^{5/3}$ as a separate Manning-derived power law in overbank depth. Total discharge is their sum. Wickert et al. (2024/2025) [[11]](#11) describe this "double-Manning" approach explicitly for rating-curve development.

#### Two-Segment Addition-Mode Model

```math
Q(h) = \alpha_1(h - h_1)^{\beta_1} + \alpha_2(h - h_2)^{\beta_2}\,\mathbb{1}\{h > h_2\}
```

Below $h_2$ only the main-channel power law is active. At $h = h_2$ the overbank term is zero, so the rating curve is continuous by construction. Above $h_2$, both controls contribute.

The two-segment model has **seven free parameters**: $[h_1, \log_{10}(\alpha_1), \beta_1, h_2, \log_{10}(\alpha_2), \beta_2, \sigma]$. Note that $h_2$ serves a dual role: it is both the *activation stage* of the second control and the *offset* $b_2$ of its power-law term (an automatic consequence of the BaRatin addition-mode continuity derivation).

#### Three-Segment Addition-Mode Model

```math
Q(h) = \alpha_1(h - h_1)^{\beta_1} + \alpha_2(h - h_2)^{\beta_2}\,\mathbb{1}\{h > h_2\} + \alpha_3(h - h_3)^{\beta_3}\,\mathbb{1}\{h > h_3\}
```

Three hydraulic controls successively activate as stage rises (e.g., main channel, then inner floodplain, then outer floodplain). Each contribution is zero at its own activation stage, so the rating curve is C⁰ continuous at both $h_2$ and $h_3$.

The three-segment model has **ten free parameters**: $[h_1, \log_{10}(\alpha_1), \beta_1, h_2, \log_{10}(\alpha_2), \beta_2, h_3, \log_{10}(\alpha_3), \beta_3, \sigma]$.

With ten parameters, the three-segment model requires substantial data — ideally 100+ observations distributed across all three stage regimes — and careful attention to convergence diagnostics.

#### Why Addition Mode Is Well-Identified Under Flat Priors

A key virtue of the addition-mode form is that **parameters are statistically separable** even under flat uniform priors:

1. **Below $h_2$** only control 1 is active, so the data in that stage range pin $(h_1, \alpha_1, \beta_1)$ via a standard single-segment log-linear regression.
2. **Above $h_2$** the residual discharge $Q_{\text{obs}}(h) - \alpha_1(h - h_1)^{\beta_1}$ (with the first-control parameters already determined) is itself a simple power law in $(h - h_2)$, which pins $(\alpha_2, \beta_2, h_2)$ without interaction with the first-control parameters.

This decoupling avoids the identifiability ridge that arises when a single replacement power law is forced to represent the physical sum of two simultaneous controls. In that case — the succession-mode form applied to compound-channel data — no set of $(\alpha, \xi, \beta)$ can exactly match the true $\alpha_1(h-h_1)^{\beta_1} + \alpha_2(h-h_2)^{\beta_2}$, and the likelihood surface becomes a multi-parameter ridge of approximately-equal-error fits.

#### Hand-Computable Coefficients from Manning's Equation

For a well-documented cross-section, the overbank coefficient $\alpha_2$ is **hand-computable** from geometry and Manning's roughness. Example: a 20-ft-wide rectangular overbank at $n_{ob} = 0.035$, $S = 0.05$:

```math
\alpha_{ob} = \frac{1.49}{n_{ob}}\,W_{ob}\,S^{1/2} = \frac{1.49}{0.035}\cdot 20 \cdot \sqrt{0.05} \approx 190
```
```math
\beta_{ob} = \tfrac{5}{3} \approx 1.667
```

Fitted $\alpha_2, \beta_2$ that drift substantially from these physical values on synthetic data are a direct signal of model misspecification — not estimator failure.

### Standards and Reference Implementations

- **BaRatin reference Fortran engine** ([github.com/BaRatin-tools/BaRatin](https://github.com/BaRatin-tools/BaRatin)). The master-equation implementation and continuity derivation used by ***RMC-BestFit*** match `ApplyRC_General` (lines 464–478), `RC_General_Continuity` (lines 308–326), and `RC_General_CheckControlMatrix` (lines 191–259) of [`src/RatingCurve_tools.f90`](https://github.com/BaRatin-tools/BaRatin/blob/main/src/RatingCurve_tools.f90). The addition-mode default is the lower-triangular-all-ones matrix valid under those rules.
- **Le Coz et al. (2014) — BaRatin** [[5]](#5) publishes the matrix-of-controls framework and documents both succession and addition configurations. ***RMC-BestFit*** currently exposes only the addition-mode default; a user-facing matrix editor is a planned enhancement.
- **Wickert et al. (2024/2025)** [[11]](#11) ("A double-Manning approach to compute robust rating curves and hydraulic geometries") explicitly adds floodplain flow to main-channel flow using two Manning-derived power laws.
- **ISO 1100-2:2010 §5.2.5** [[3]](#3) instructs that multi-control rating curves be continuous across control transitions and that each control be fit against its own effective gauge height of zero flow. The addition-mode form satisfies both requirements: each control has its own offset ($h_1$ for control 1, $h_k$ for $k \geq 2$), and the curve is $C^0$ at every transition by construction.
- **USGS Kennedy (1984)** [[4]](#4); **Rantz et al. (1982)** [[2]](#2): foundational USGS references for stage-discharge rating methodology.
- **Reitan & Petersen-Øverleir (2009)** [[6]](#6) and **Kiang et al. (2018)** [[9]](#9) present piecewise-succession Bayesian formulations and comparisons; the succession mode is appropriate for sites where a low-flow control is drowned out by a rising channel control, not for overbank transitions.

### The Likelihood Function

Given $n$ paired observations $(h_i, Q_i)$ and parameters $\theta$ (see §Single-Segment / Two-Segment / Three-Segment Model above for the free-parameter layout), the log-likelihood is:

```math
\ell(\theta) = -\frac{n}{2}\log(2\pi) - n \cdot \log(\sigma) - \frac{1}{2\sigma^2} \sum_{i=1}^{n} r_i^2
```

where the residuals are computed in log-space:

```math
r_i = \log_{10}(Q_i) - \log_{10}[\hat{Q}(h_i; \theta)]
```

and $\hat{Q}(h_i; \theta)$ is the predicted discharge. Under the addition-mode model, $\hat{Q}(h; \theta) = \alpha_1(h - h_1)^{\beta_1} + \sum_{k \geq 2} \alpha_k(h - h_k)^{\beta_k}\,\mathbb{1}\{h > h_k\}$.

For the model to be valid we require $h_i > h_1$ (every observed stage above the main-channel zero-flow stage) so that the first term is well-defined, and $h_1 < h_2 < h_3$ so that each successive control activates strictly above its predecessor. Activation-guarded indicator functions ensure each subsequent term contributes zero when $h \leq h_k$; ordering violations cause the likelihood to return $-\infty$:

```math
\ell(\theta) = -\infty \quad \text{if any}\ (h_i - h_1) \leq 0\ \text{or}\ h_1 \geq h_2 \ \text{or}\ h_2 \geq h_3
```

These constraints are enforced automatically during optimization and MCMC sampling.

### Interpreting the Error Parameter $\sigma$

The parameter $\sigma$ represents the standard deviation of residuals in log₁₀-space. To convert this to approximate percentage error in discharge:

```math
\text{CV} \approx \ln(10) \cdot \sigma \approx 2.303 \cdot \sigma
```

| Log-space $\sigma$ | Approximate CV | Interpretation |
|-------------------|----------------|----------------|
| 0.02 | 5% | Excellent measurements, stable channel |
| 0.05 | 12% | Good field conditions |
| 0.10 | 23% | Moderate uncertainty |
| 0.15 | 35% | High uncertainty, variable channel |
| 0.20 | 46% | Very high uncertainty |

Values of $\sigma > 0.15$ often indicate either measurement problems or non-stationarity (the channel is changing over time).

## Stage / Discharge Alignment

The two input time series supplied to the rating curve — stage $h_i$ and discharge $Q_i$ — are **not required to match index-for-index or to have the same length**. Real-world gauge records (USGS, Water Data for the Nation) routinely return stage and discharge series whose date ranges overlap only partially. ***RMC-BestFit*** inner-joins the two series by `DateTime` index and fits the rating curve to the **common-date pairs only**:

```math
\mathcal{D} = \bigl\{ (h_i, Q_i) \;\big|\; \text{date}_i \in \text{Stage.Dates} \cap \text{Discharge.Dates} \bigr\}
```

Observations whose date appears in only one series are silently dropped from the fit and from every derived diagnostic (residuals, fitted values, RMSE, AIC / BIC / DIC, posterior-predictive plots).

Two soft safeguards protect the user from unintentionally fitting a tiny or ill-matched subset:

- **Hard error** (`RCA-ERR-010`): calibration requires at least **10 common dates**. Below this threshold the analysis is marked invalid and the fit refuses to run.
- **Soft warning** (`RCA-WRN-011`): when the two series differ in length by more than **10 %**, a warning is raised advising the user to verify the selected series are correct. The fit still proceeds on the inner-join — this is purely a sanity check for cases where, for example, an entire discharge record is accidentally paired with an unrelated long stage record.

## Parameter Estimation

### Maximum Likelihood Estimation

Maximum Likelihood Estimation (MLE) finds the parameter values that maximize the log-likelihood — equivalently, the parameters under which the observed data are most probable:

```math
\hat{\theta}_{\text{MLE}} = \arg\max_{\theta} \ell(\theta)
```

For rating curves, the likelihood surface can have multiple local optima and ridges, especially for multi-segment models. The `MultilevelSingleLinkage` global optimizer is recommended:

```cs
using RMC.BestFit.Models;
using RMC.BestFit.Estimation;
using Numerics.Mathematics.Optimization;

// Create the rating curve model
var model = new RatingCurve(stageTimeSeries, dischargeTimeSeries, numberOfSegments: 1);

// Fit using global optimization
var mle = new MaximumLikelihood(model, OptimizationMethod.MultilevelSingleLinkage);
mle.Estimate();

// Check convergence and extract parameters
if (mle.IsEstimated)
{
    model.SetParameterValues(mle.BestParameterSet.Values);

    Console.WriteLine($"Zero-flow stage (ξ):   {model.Parameters[0].Value:F3}");
    Console.WriteLine($"Log₁₀(α):              {model.Parameters[1].Value:F3}");
    Console.WriteLine($"Exponent (β):          {model.Parameters[2].Value:F3}");
    Console.WriteLine($"Log-space error (σ):   {model.Parameters[3].Value:F4}");
    Console.WriteLine($"Log-likelihood:        {mle.BestParameterSet.Fitness:F2}");
}
```

**Advantages of MLE:**
- Fast computation (typically seconds)
- Produces point estimates suitable for operational use
- Good starting point for MCMC

**Limitations of MLE:**
- No uncertainty quantification on parameters
- Can find local optima instead of global optimum
- Standard errors (when available) assume normality that may not hold

### Bayesian MCMC Estimation

Bayesian estimation provides full uncertainty quantification by characterizing the posterior distribution of parameters given the data:

```math
\pi(\theta | \text{data}) \propto \mathcal{L}(\text{data} | \theta) \cdot \pi(\theta)
```

The ***RMC-BestFit*** library uses the **DEMCzs** sampler (Differential Evolution MCMC with snooker update) [[1]](#1), which is self-tuning and robust to multimodal posteriors.

```cs
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;

// Create the rating curve model
var model = new RatingCurve(stageTimeSeries, dischargeTimeSeries, numberOfSegments: 1);

// Configure and run Bayesian analysis
var analysis = new RatingCurveAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;

await analysis.RunAsync();

// Access posterior summaries
var results = analysis.BayesianAnalysis.Results;
Console.WriteLine("Parameter posterior summaries:");
Console.WriteLine($"{"Parameter",-25} {"Mean",10} {"Std Dev",10} {"95% CI Lower",12} {"95% CI Upper",12} {"R-hat",8}");
Console.WriteLine(new string('-', 80));

for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    Console.WriteLine($"{model.Parameters[i].Name,-25} {stats.Mean,10:F4} {stats.StandardDeviation,10:F4} " +
                      $"{stats.LowerCI,12:F4} {stats.UpperCI,12:F4} {stats.Rhat,8:F3}");
}
```

**Advantages of Bayesian MCMC:**
- Full posterior distribution, not just point estimates
- Proper uncertainty propagation to derived quantities (e.g., the 100-year discharge)
- Incorporates prior information when available
- Provides convergence diagnostics (R-hat, ESS)

**Considerations:**
- Slower than MLE (minutes instead of seconds)
- Requires checking convergence diagnostics
- Prior specification can influence results (though default flat priors are usually appropriate)

### Prior Distributions

The ***RMC-BestFit*** library uses weakly informative default priors that place minimal constraints while ensuring computational stability:

| Parameter | Default Prior | Rationale |
|-----------|--------------|-----------|
| $h_1$ (main-channel zero-flow stage) | $\text{Uniform}(\min(h) - \text{range}(h),\ \min(h) + 0.1\cdot\text{range}(h))$ | Must be below minimum observed stage |
| $\log_{10}(\alpha_k)$ (per-control coefficients, all $k$) | $\text{Uniform}(-10, 10)$ | Broad enough for very large rivers while remaining finite |
| $\beta_k$ (per-control exponents) | $\text{Uniform}(0, 5.0)$ | Allows near-flat log-space responses and typical channel exponents |
| $h_2$ (second-control activation stage) | $\text{Uniform}(\min(h)+0.2\cdot\text{range}(h),\ \min(h)+0.7\cdot\text{range}(h))$ | Keeps the activation stage inside the data range, well away from the extremes |
| $h_3$ (third-control activation stage) | $\text{Uniform}(\min(h)+0.5\cdot\text{range}(h),\ \min(h)+\text{range}(h))$ | Sits above $h_2$ and within the data range |
| $\sigma$ (error scale) | $\text{Uniform}(\epsilon,\ 3\cdot\text{std}(\log_{10}Q))$ + optional Jeffreys | Small positive to reasonable upper bound |

Default prior calibration uses date-aligned stage-discharge pairs when possible, because unpaired observations do not enter the likelihood. Non-finite stages and non-positive or non-finite discharges are ignored. If the observed stage range or log-discharge standard deviation collapses, ***RMC-BestFit*** applies finite positive fallback bounds so the default uniform priors remain valid during model setup.

Under BaRatin addition mode $h_k$ serves a dual role — it is both the *activation stage* and the *offset* of control $k$'s power-law term — because the continuity derivation collapses to $b_k = \kappa_k$ when a control persists across all upper segments. No separate $\xi_2, \xi_3$ priors are needed.

When `UseJeffreysRuleForScale = true` (the default), the error scale receives Jeffreys' prior:

```math
\pi(\sigma) \propto \frac{1}{\sigma}
```

This non-informative prior is appropriate for scale parameters and produces posteriors that are invariant to rescaling of the data.

## Practical Examples

### Example 1: Simple Mountain Stream

A mountain stream has a relatively uniform channel geometry — steep, rocky bed with little overbank area. A single-segment model is appropriate.

```cs
using RMC.BestFit.Models;
using RMC.BestFit.Analyses;
using Numerics.Data;

// Load stage and discharge time series (typically from CSV or database)
var stageTS = new TimeSeries(TimeInterval.Irregular, stageData);
var dischargeTS = new TimeSeries(TimeInterval.Irregular, dischargeData);

// Create single-segment rating curve
var model = new RatingCurve(stageTS, dischargeTS, numberOfSegments: 1);

// Run Bayesian analysis
var analysis = new RatingCurveAnalysis(model);
analysis.BayesianAnalysis.Iterations = 10000;
analysis.BayesianAnalysis.WarmupIterations = 5000;
await analysis.RunAsync();

// Generate rating table with uncertainty bands
var mapParams = analysis.BayesianAnalysis.Results.MAP.Values;
var table = model.GenerateRatingTable(mapParams, minStage: 0.5, maxStage: 8.0, numPoints: 50);

Console.WriteLine("Stage (ft)    Discharge (cfs)");
Console.WriteLine("----------    ---------------");
for (int i = 0; i < table.GetLength(0); i++)
{
    Console.WriteLine($"{table[i, 0],10:F2}    {table[i, 1],15:F1}");
}
```

For a steep mountain stream, expect $\beta \approx 2.0-2.8$.

### Example 2: River with Floodplain (Bankfull Transition)

A lowland river has a distinct main channel and floodplain. Below bankfull stage, flow is confined to the main channel. Above bankfull, the main channel continues carrying flow *and* the floodplain activates — both controls contribute. Under BaRatin addition mode the two contributions add.

```cs
// Create two-segment rating curve for a compound channel
// Q(h) = α₁(h − h₁)^β₁  + α₂(h − h₂)^β₂·𝟙{h > h₂}
var model = new RatingCurve(stageTS, dischargeTS, numberOfSegments: 2);

var analysis = new RatingCurveAnalysis(model);
analysis.BayesianAnalysis.Iterations = 15000;  // More iterations for 7 parameters
analysis.BayesianAnalysis.WarmupIterations = 7500;
await analysis.RunAsync();

// The activation stage h₂ corresponds to bankfull
var results = analysis.BayesianAnalysis.Results;
var h2Stats = results.ParameterResults[3].SummaryStatistics;  // h₂ is parameter index 3

Console.WriteLine($"Estimated bankfull stage: {h2Stats.Mean:F2} ± {h2Stats.StandardDeviation:F2} ft");
Console.WriteLine($"95% CI: [{h2Stats.LowerCI:F2}, {h2Stats.UpperCI:F2}] ft");

// Main-channel exponent β₁ vs. overbank exponent β₂
// Parameter layout: [h₁, log₁₀α₁, β₁, h₂, log₁₀α₂, β₂, σ]  (indices 0..6)
var beta1Stats = results.ParameterResults[2].SummaryStatistics;
var beta2Stats = results.ParameterResults[5].SummaryStatistics;

Console.WriteLine($"Main-channel exponent (β₁): {beta1Stats.Mean:F2}");
Console.WriteLine($"Overbank exponent (β₂):    {beta2Stats.Mean:F2}");
```

Typical physical values:
- $\beta_1 \approx 1.8-2.5$ (main channel — depth-dominated conveyance)
- $\beta_2 \approx 5/3 \approx 1.667$ (wide rectangular overbank — Manning exact)
- $\alpha_2$ is hand-computable from the floodplain width $W_{ob}$, roughness $n_{ob}$, and slope $S$ via $\alpha_2 = (1.49/n_{ob}) \cdot W_{ob} \cdot S^{1/2}$; MAP values far from the hand-computed floodplain coefficient indicate model-specification problems

### Example 3: Coastal Plain River (Wide, Shallow)

Coastal plain rivers are often very wide relative to their depth, with extensive low-gradient floodplains. The exponent is typically low because small increases in stage produce large increases in flow area.

```cs
// Single-segment model for wide, shallow channel
var model = new RatingCurve(stageTS, dischargeTS, numberOfSegments: 1);

// For very flat channels, the zero-flow stage may be poorly identified
// Consider providing an informative prior if gauge datum is known
model.Parameters[0].PriorDistribution = new Numerics.Distributions.Uniform(-0.5, 0.5);

var analysis = new RatingCurveAnalysis(model);
await analysis.RunAsync();

var beta = analysis.BayesianAnalysis.Results.ParameterResults[2].SummaryStatistics.Mean;
Console.WriteLine($"Exponent: {beta:F2}");  // Expect 1.3-1.6
```

### Example 4: Complex Cross-Section (Three Segments)

Some sites have multiple hydraulic controls: a low-flow control (weir, riffle, or rock outcrop), main channel control, and floodplain control. This requires a three-segment model.

```cs
// Three-segment model requires substantial data (recommend n > 100)
var model = new RatingCurve(stageTS, dischargeTS, numberOfSegments: 3);

// Increase iterations for 10-parameter model
var analysis = new RatingCurveAnalysis(model);
analysis.BayesianAnalysis.Iterations = 25000;
analysis.BayesianAnalysis.WarmupIterations = 12500;
await analysis.RunAsync();

// Check convergence carefully
var results = analysis.BayesianAnalysis.Results;
bool allConverged = true;
for (int i = 0; i < model.Parameters.Count; i++)
{
    var rhat = results.ParameterResults[i].SummaryStatistics.Rhat;
    var ess = results.ParameterResults[i].SummaryStatistics.ESS;

    if (rhat > 1.1 || ess < 100)
    {
        Console.WriteLine($"Warning: {model.Parameters[i].Name} may not have converged (R-hat={rhat:F3}, ESS={ess:F0})");
        allConverged = false;
    }
}

if (allConverged)
    Console.WriteLine("All parameters converged successfully.");
```

**Caution:** Three-segment models require data distributed across all three stage regimes to identify all ten parameters. If discharge measurements are clustered below the overbank activation or in a narrow range, the third control's parameters will be weakly identified and posteriors will be wide regardless of the BaRatin addition-mode parameterization. Consider whether the physical situation truly requires three controls, or whether a two-segment model with wider uncertainty might be more appropriate for the available data.

## Parameter Identifiability

### The ξ-α-β Trade-off

Within a single segment, the power-law rating curve parameters $\xi$, $\alpha$, and $\beta$ are not fully independent — the relation

```math
Q = \alpha \cdot (h - \xi)^{\beta}
```

can be approximately reproduced over a limited stage range by different combinations of the three parameters, creating a ridge in the likelihood surface. Shifting $\xi$ upward and compensating with changes in $\alpha$ and $\beta$ gives nearly identical discharge predictions when the dynamic range of $(h - \xi)$ is narrow.

This ridge is most pronounced when:

1. **Data doesn't extend to low flows**: Without observations near the true zero-flow stage, the data cannot constrain $\xi$.
2. **Limited stage range**: A narrow range of observed stages provides little leverage to distinguish between parameter combinations.
3. **High measurement error**: Noise masks the subtle differences between competing parameter sets.

> **Why the BaRatin addition mode helps.** The $(\xi, \beta)$ ridge is a classical weak-identification signature of power-law regression when both the location and the exponent must be fit jointly. Under the addition-mode formulation used by ***RMC-BestFit***, only the main-channel location $h_1$ is a free parameter — the activation stages $h_k$ for $k \geq 2$ serve as both activation thresholds and "offsets" of each control's power-law term, which decouples them from the main-channel regression. Below $h_2$ the data pin $(h_1, \alpha_1, \beta_1)$ via ordinary log-log regression; above $h_2$ the residual discharge is a clean power law in $(h - h_2)$ that pins $(\alpha_2, \beta_2)$ independently. The segment-1 location $h_1$ is still fit and can still suffer from weak identification when low-flow data are sparse or narrow — the guidance in this section primarily applies to $h_1$.

### Diagnosing Identifiability Problems

Signs of identifiability issues include:

- **High posterior correlation**: Check correlation between $\xi$, $\alpha$, and $\beta$ in MCMC output
- **Wide credible intervals**: Especially on $\xi$ and $\beta$
- **Slow MCMC mixing**: Low effective sample size despite many iterations
- **Sensitivity to priors**: Results change substantially with different prior specifications

```cs
// Check parameter correlations after MCMC
var output = analysis.BayesianAnalysis.Results.Output;

// Extract posterior samples for correlation analysis
var xiSamples = output.Select(p => p.Values[0]).ToArray();
var logAlphaSamples = output.Select(p => p.Values[1]).ToArray();
var betaSamples = output.Select(p => p.Values[2]).ToArray();

// Compute correlation (using Numerics.Data.Statistics)
double corr_xi_beta = Statistics.Correlation(xiSamples, betaSamples);
Console.WriteLine($"Correlation(ξ, β): {corr_xi_beta:F3}");

if (Math.Abs(corr_xi_beta) > 0.9)
    Console.WriteLine("Warning: High correlation between ξ and β suggests identifiability issues.");
```

### Improving Identifiability

Several strategies can improve parameter identifiability:

**1. Include low-flow measurements**: The most effective solution is to have discharge measurements at stages close to the true zero-flow stage.

```cs
// Data with good low-flow coverage
var minObservedStage = stageData.Min();
var trueXi = 0.5;  // Known or estimated zero-flow stage

if (minObservedStage - trueXi > 0.5)
    Console.WriteLine("Warning: Minimum observed stage is far from zero-flow stage. ξ may be poorly identified.");
```

**2. Use informative priors**: If the gauge datum is known from surveys, specify an informative prior on $\xi$.

```cs
// If gauge zero is at the channel thalweg, ξ should be near 0
model.Parameters[0].PriorDistribution = new Numerics.Distributions.Normal(0.0, 0.2);
```

**3. Fix $\xi$ if known**: If the zero-flow stage has been surveyed or is known from channel geometry, fix it rather than estimating.

```cs
// Fix zero-flow stage at surveyed value
model.Parameters[0].Value = 0.3;
model.Parameters[0].IsFixed = true;
```

**4. Use physical constraints**: Ensure the prior on $\beta$ reflects physical reality (typically 1.0-3.5 for natural channels).

## Working with Results

### Generating Rating Tables

A rating table provides stage-discharge pairs for operational use:

```cs
var analysis = new RatingCurveAnalysis(model);
await analysis.RunAsync();

// Use MAP (Maximum A Posteriori) parameters for best point estimate
var mapParams = analysis.BayesianAnalysis.Results.MAP.Values;

// Generate rating table
double[,] table = model.GenerateRatingTable(
    parameters: mapParams,
    minStage: 0.0,
    maxStage: 15.0,
    numPoints: 100);

// Export to CSV
using (var writer = new StreamWriter("rating_table.csv"))
{
    writer.WriteLine("Stage,Discharge");
    for (int i = 0; i < table.GetLength(0); i++)
    {
        writer.WriteLine($"{table[i, 0]:F3},{table[i, 1]:F2}");
    }
}
```

### Uncertainty Bands on the Rating Curve

To visualize uncertainty, generate predictions from multiple posterior samples:

```cs
var posteriorSamples = analysis.BayesianAnalysis.Results.Output;
var stages = Enumerable.Range(0, 100).Select(i => 0.5 + i * 0.15).ToArray();

// Store predictions for each stage and posterior sample
var predictions = new double[stages.Length, posteriorSamples.Count];

for (int j = 0; j < posteriorSamples.Count; j++)
{
    var params = posteriorSamples[j].Values;
    for (int i = 0; i < stages.Length; i++)
    {
        predictions[i, j] = model.Predict(params, stages[i]);
    }
}

// Compute percentiles at each stage
Console.WriteLine("Stage    Q_median    Q_2.5%    Q_97.5%");
for (int i = 0; i < stages.Length; i++)
{
    var row = Enumerable.Range(0, posteriorSamples.Count).Select(j => predictions[i, j]).OrderBy(x => x).ToArray();
    double median = row[row.Length / 2];
    double lower = row[(int)(row.Length * 0.025)];
    double upper = row[(int)(row.Length * 0.975)];

    Console.WriteLine($"{stages[i]:F2}     {median:F1}       {lower:F1}      {upper:F1}");
}
```

### Checking Model Fit

Examine residuals to assess model adequacy:

```cs
var mapParams = analysis.BayesianAnalysis.Results.MAP.Values;
var residuals = model.Residuals(mapParams);

// Basic statistics
double meanResid = residuals.Average();
double stdResid = Math.Sqrt(residuals.Select(r => r * r).Average() - meanResid * meanResid);

Console.WriteLine($"Mean residual: {meanResid:F4} (should be near 0)");
Console.WriteLine($"Std dev of residuals: {stdResid:F4} (should be near σ = {mapParams.Last():F4})");

// Check for patterns
// - Residuals vs. fitted values: should show no trend
// - Residuals vs. time: should show no autocorrelation (if temporal order known)
// - Q-Q plot: should be approximately linear
```

### Synthetic Data Generation

For simulation studies or testing, generate synthetic data from a specified rating curve:

```cs
// Define true parameters
double[] trueParams = new double[]
{
    0.5,                // ξ: zero-flow stage
    Math.Log10(10),     // log₁₀(α): coefficient
    2.0,                // β: exponent
    0.05                // σ: measurement error
};

// Create model and set parameters
var model = new RatingCurve();
model.SetParameterValues(trueParams);

// Generate synthetic data
var (stageTS, dischargeTS) = model.GenerateSyntheticData(
    sampleSize: 200,
    minStage: 1.0,
    maxStage: 10.0,
    seed: 12345);

Console.WriteLine($"Generated {stageTS.Count} stage-discharge pairs");
Console.WriteLine($"Stage range: {stageTS.Min(s => s.Value):F2} to {stageTS.Max(s => s.Value):F2}");
Console.WriteLine($"Discharge range: {dischargeTS.Min(s => s.Value):F1} to {dischargeTS.Max(s => s.Value):F1}");
```

## Model Selection

### Choosing the Number of Segments

The number of segments should be guided by physical knowledge of the channel:

| Situation | Recommended Segments |
|-----------|---------------------|
| Uniform channel geometry across observed range | 1 |
| Clear bankfull transition (channel to floodplain) | 2 |
| Multiple distinct controls (weir + channel + floodplain) | 3 |
| Uncertain | Start with 1, add segments if residuals show patterns |

### Comparing Models with Information Criteria

When physical reasoning is ambiguous, compare models using information criteria:

```cs
// Fit models with 1, 2, and 3 segments
var modelResults = new List<(int Segments, double DIC, double WAIC)>();

foreach (int nSegments in new[] { 1, 2, 3 })
{
    var m = new RatingCurve(stageTS, dischargeTS, numberOfSegments: nSegments);
    var a = new RatingCurveAnalysis(m);
    a.BayesianAnalysis.Iterations = 15000;
    a.BayesianAnalysis.WarmupIterations = 7500;
    await a.RunAsync();

    var dic = a.BayesianAnalysis.DIC;
    var waic = a.BayesianAnalysis.WAIC;

    modelResults.Add((nSegments, dic, waic));
    Console.WriteLine($"{nSegments} segment(s): DIC = {dic:F1}, WAIC = {waic:F1}");
}

// Lower values indicate better fit (penalized for complexity)
var best = modelResults.OrderBy(r => r.DIC).First();
Console.WriteLine($"\nBest model by DIC: {best.Segments} segment(s)");
```

**Interpretation:**
- **DIC (Deviance Information Criterion)**: Balances fit and complexity. Differences > 10 are considered strong evidence.
- **WAIC (Widely Applicable Information Criterion)**: More robust than DIC, especially for small samples.

## Assumptions and Limitations

### Model Assumptions

The ***RMC-BestFit*** rating curve model assumes:

1. **Power-law relationship**: The stage-discharge relationship follows $Q = \alpha(h-\xi)^\beta$ (possibly segmented)

2. **Log-normal errors**: Measurement uncertainty is multiplicative and approximately log-normal

3. **Independence**: Observations are statistically independent (no unmodeled temporal correlation)

4. **Stationarity**: The rating curve is stable over the observation period (no channel scour, vegetation growth, or datum shifts)

5. **Steady flow**: Each observation represents steady, uniform flow conditions

### Known Limitations

1. **Maximum 3 segments**: More complex geometries require custom models

2. **No loop rating**: Rising and falling limb hysteresis is not modeled

3. **No unsteady flow correction**: Rapid stage changes violate steady-flow assumptions

4. **No shift analysis**: Rating shifts from channel changes must be handled externally

5. **Minimum 10 observations**: Fewer observations provide insufficient information for reliable estimation

### When the Model May Be Inappropriate

Consider alternative approaches when:

- **Channel is highly unstable**: Sand-bed rivers with frequent morphological changes
- **Strong unsteady effects**: Tidal influence, backwater from downstream, or very rapid stage changes
- **Loop rating present**: Consistent hysteresis between rising and falling limbs
- **Ice effects**: Ice jams or frazil ice affect the stage-discharge relationship

## Class Reference

### RatingCurve Class

**Namespace:** `RMC.BestFit.Models`

**Constructors:**

```cs
// Default constructor (single segment)
RatingCurve()

// Constructor with data
RatingCurve(TimeSeries stageData, TimeSeries dischargeData, int numberOfSegments = 1)

// Constructor from XML
RatingCurve(TimeSeries stageData, TimeSeries dischargeData, XElement xElement)
```

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `StageData` | `TimeSeries` | Stage (water level) observations |
| `DischargeData` | `TimeSeries` | Discharge (flow) observations |
| `NumberOfSegments` | `int` | Number of power law segments (1-3) |
| `UseJeffreysRuleForScale` | `bool` | Use Jeffreys prior on σ (default: true) |
| `UseDefaultFlatPriors` | `bool` | Use default flat priors (default: true) |
| `Parameters` | `List<ModelParameter>` | Model parameters with priors |
| `NumberOfParameters` | `int` | Total parameter count |

**Key Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `Predict(double[] params, double stage)` | `double` | Predict discharge at given stage |
| `Residuals(double[] params)` | `double[]` | Log-space residuals |
| `FittedValues(double[] params)` | `double[]` | Predicted discharge values |
| `GenerateRatingTable(...)` | `double[,]` | Stage-discharge lookup table |
| `GenerateSyntheticData(...)` | `(TimeSeries, TimeSeries)` | Synthetic stage-discharge data |
| `GenerateRandomValues(int sampleSize, int seed = -1)` | `double[]` | Random discharge samples |
| `DataLogLikelihood(double[] params)` | `double` | Log-likelihood of data |
| `Validate()` | `(bool, List<string>)` | Validation check |
| `Clone()` | `object` | Deep copy of model |

### RatingCurveAnalysis Class

**Namespace:** `RMC.BestFit.Analyses`

**Constructor:**

```cs
RatingCurveAnalysis(RatingCurve ratingCurve)
```

**Key Properties:**

| Property | Type | Description |
|----------|------|-------------|
| `RatingCurve` | `RatingCurve` | The rating curve model |
| `BayesianAnalysis` | `BayesianAnalysis` | MCMC configuration and results |
| `IsEstimated` | `bool` | Whether estimation completed |

**Key Methods:**

| Method | Returns | Description |
|--------|---------|-------------|
| `RunAsync()` | `Task` | Run Bayesian MCMC estimation |
| `CancelAnalysis()` | `void` | Cancel running analysis |

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/RatingCurve/RatingCurve.cs`, `src/RMC.BestFit/Analyses/RatingCurve/RatingCurveAnalysis.cs`, `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`, and `src/RMC.BestFit/Estimation/MaximumLikelihood.cs`.

---

## References

<a id="1">[1]</a> C. J. F. ter Braak and J. A. Vrugt, "Differential Evolution Markov Chain with snooker updater and fewer chains," *Statistics and Computing*, vol. 18, no. 4, pp. 435-446, 2008.

<a id="2">[2]</a> S. E. Rantz et al., *Measurement and computation of streamflow: Volume 2. Computation of discharge*, USGS Water-Supply Paper 2175, 1982.

<a id="3">[3]</a> ISO 1100-2:2010, *Hydrometry — Measurement of liquid flow in open channels — Part 2: Determination of the stage-discharge relation*, International Organization for Standardization, 2010.

<a id="4">[4]</a> E. J. Kennedy, *Discharge ratings at gaging stations*, USGS Techniques of Water-Resources Investigations, Book 3, Chapter A10, 1984.

<a id="5">[5]</a> J. Le Coz, B. Renard, L. Bonnifait, F. Branger, and R. Le Boursicaud, "Combining hydraulic knowledge and uncertain gaugings in the estimation of hydrometric rating curves: A Bayesian approach," *Journal of Hydrology*, vol. 509, pp. 573-587, 2014.

<a id="6">[6]</a> T. Reitan and A. Petersen-Øverleir, "Bayesian methods for estimating multi-segment discharge rating curves," *Stochastic Environmental Research and Risk Assessment*, vol. 23, no. 5, pp. 627-642, 2009.

<a id="7">[7]</a> World Meteorological Organization, *Manual on Stream Gauging*, WMO-No. 1044, 2010.

<a id="8">[8]</a> V. T. Chow, *Open-Channel Hydraulics*, McGraw-Hill, 1959.

<a id="9">[9]</a> J. E. Kiang, C. Gazoorian, H. McMillan, G. Coxon, J. Le Coz, I. K. Westerberg, A. Belleville, D. Sevrez, A. E. Sikorska, A. Petersen-Øverleir, T. Reitan, J. Freer, B. Renard, V. Mansanarez, and R. Mason, "A Comparison of Methods for Streamflow Uncertainty Estimation," *Water Resources Research*, vol. 54, no. 10, pp. 7149-7176, 2018.

<a id="10">[10]</a> V. Mansanarez, J. Le Coz, B. Renard, M. Lang, G. Pierrefeu, and P. Vauchel, "Rapid stage-discharge rating curve assessment using hydraulic modeling in an uncertainty framework," *Water Resources Research*, vol. 55, no. 4, 2019.

<a id="11">[11]</a> A. D. Wickert, G. H. C. Ng, and C. P. Bolduc, "A double-Manning approach to compute robust rating curves and hydraulic geometries," *EGUsphere preprint*, 2024/2025.

<a id="12">[12]</a> BaRatin-tools, *BaRatin reference Fortran computational engine*, GitHub repository [github.com/BaRatin-tools/BaRatin](https://github.com/BaRatin-tools/BaRatin). Master-equation implementation and continuity derivation in `src/RatingCurve_tools.f90`.

---

[<- Previous: Coincident Frequency](coincident-frequency.md) | [Back to Index](../../index.md) | [Next: Time Series ->](time-series.md)

*Last updated: 2026-04-21 — switched default multi-segment form to BaRatin matrix-of-controls addition mode (Le Coz et al. 2014) following line-by-line verification against the BaRatin reference Fortran source.*
