<!-- technical-reference-status: complete -->

# Data Frame and Mixed-Observation Likelihood

[<- Parameters and priors](../models/parameters-and-priors.md) | [Technical reference index](../index.md) | [Next: Distribution families ->](../distributions/index.md)

## Purpose and applicability

`DataFrame` represents the heterogeneous evidence used by univariate frequency models: measured systematic peaks, uncertain reconstructed magnitudes, interval-censored events, perception-threshold counts, and exact values deliberately treated as low outliers. The scientific purpose is not merely tabular storage. Each representation selects a different likelihood contribution, and therefore a different statement about what was observed.

Historical and paleoflood information can materially change tail inference, but only when the perception process, record duration, and magnitude uncertainty are defensible. The framework follows the likelihood principle used in historical/paleoflood frequency analysis [1](#ref-1), [2](#ref-2). Bulletin 17C has additional specialized Expected Moments Algorithm behavior documented separately; this chapter describes the general `UnivariateDistribution` likelihood.

## Notation

| Symbol | Meaning |
|---|---|
| $f(y\mid\theta)$, $F(y\mid\theta)$ | PDF and CDF of the modeled univariate distribution |
| $S(y\mid\theta)=1-F(y\mid\theta)$ | Survival function |
| $\mathcal E$ | Exact observations used as exact values |
| $\mathcal O$ | Exact observations flagged as low outliers |
| $\mathcal U$ | Uncertain observations |
| $\mathcal I$ | Interval-censored observations |
| $\mathcal T$ | Perception-threshold periods |
| $g_i(q)$, $G_i(q)$ | User-supplied density and CDF over the uncertain magnitude $q$ |
| $[l_i,u_i]$ | Interval-censoring bounds |
| $c_j$ | Perception threshold for period $j$ |
| $n_j^-$, $n_j^+$ | Effective counts below and above $c_j$ |
| $t_i$ | Integer observation index, commonly water year |
| $u_O$ | `LowOutlierThreshold` |

Magnitudes and every magnitude-distribution parameter must use consistent units. `Index` is an integer label used as time by nonstationary models; the library does not infer whether it means calendar year, water year, month, or an arbitrary sequence.

## Data-generating assumptions

For a stationary univariate model, RMC.BestFit assumes conditional independence of the logical records given $\theta$. The complete data likelihood is

$$
\begin{aligned}
L_D(\theta)=
&\prod_{i\in\mathcal E} f(y_i\mid\theta)
\prod_{i\in\mathcal O} F(u_O\mid\theta) \\
&\times\prod_{i\in\mathcal U}\widetilde L_{U,i}(\theta)
\prod_{i\in\mathcal I}\left[F(u_i\mid\theta)-F(l_i\mid\theta)\right] \\
&\times\prod_{j\in\mathcal T}
F(c_j\mid\theta)^{n_j^-}S(c_j\mid\theta)^{n_j^+}.
\end{aligned}
\tag{1}
$$

The implemented log-likelihood is the sum of the logarithms in Equation (1). A zero or invalid contribution produces negative infinity. No binomial coefficient is included for threshold counts. That coefficient would be constant in $\theta$ for a fixed aggregate record, but its omission matters if an analyst tries to interpret the returned value as the fully normalized probability of an unordered count outcome.

## Exact observations

An unflagged `ExactData(index, value)` contributes

$$
\ell_{E,i}(\theta)=\log f(y_i\mid\theta).
\tag{2}
$$

“Exact” means its magnitude uncertainty is negligible for the intended analysis. It does not imply that the gage, rating curve, or historical record is literally error-free. If magnitude uncertainty is consequential, use `UncertainData` rather than widening parameter uncertainty after the fit.

`ExactSeries(IList<double>)` creates indexes $0,1,\ldots,n-1$. Use explicit `ExactData` instances when real time indexes are required. Duplicate or inconsistent indexes can make the chronology invalid even though the values alone appear reasonable.

## Low-outlier censoring

`ExactData.IsLowOutlier` changes the observation model. A flagged value is not evaluated at its recorded magnitude; it is treated as left-censored at the common `LowOutlierThreshold`:

$$
\ell_{O,i}(\theta)=\log F(u_O\mid\theta).
\tag{3}
$$

Thus $r$ flagged low outliers contribute $r\log F(u_O\mid\theta)$. `SetLowOutliersFromMGBT()` can set flags using the Multiple Grubbs-Beck Test and requires at least ten valid exact observations. `SetLowOutliersFromThreshold()` applies a supplied threshold and rejects one that would censor more than half the exact record.

Low-outlier censoring is a modeling decision, not a data-cleaning deletion. Report the test or threshold, number flagged, and sensitivity of target quantiles. The MGBT procedure and Bulletin 17C usage are covered in the dedicated Bulletin chapter [3](#ref-3).

## Uncertain observations

`UncertainData` stores a Numerics distribution over the unknown magnitude. RMC.BestFit integrates that supplied density against the model density:

$$
L_{U,i}(\theta)=\int_{-\infty}^{\infty}g_i(q)f(q\mid\theta)\,dq.
\tag{4}
$$

Numerically, the implementation uses $\varepsilon=10^{-8}$,

$$
a_i=G_i^{-1}(\varepsilon),\quad
b_i=G_i^{-1}(1-\varepsilon),\quad
\widetilde L_{U,i}(\theta)=
\frac{1}{1-2\varepsilon}
\int_{a_i}^{b_i}g_i(q)f(q\mid\theta)\,dq,
\tag{5}
$$

and a fixed 20-point Gauss-Legendre rule. The division conditions on the retained central mass. Non-finite bounds, $a_i\ge b_i$, a non-positive integral, or a non-finite result produce negative infinity.

Equation (4) treats $g_i$ as a density over the latent true magnitude supplied for that record. It is not a generic additive-error model $z_i=q_i+e_i$ constructed automatically from a standard error. The analyst is responsible for choosing a distribution whose support, skew, units, and interpretation represent the magnitude evidence. A Normal distribution may put mass on negative discharge; a positive or bounded family may be more defensible when that mass is material.

The fixed quadrature rule is deterministic and efficient, but it is not adaptively refined. Strongly multimodal, extremely narrow, or poorly scaled uncertainty densities require numerical sensitivity checks.

## Interval-censored observations

`IntervalData(index, lowerValue, value, upperValue)` means only that the latent magnitude lies in $[l_i,u_i]$. Its contribution is

$$
\ell_{I,i}(\theta)=
\log\left[F(u_i\mid\theta)-F(l_i\mid\theta)\right].
\tag{6}
$$

The central `Value` is retained for display and descriptive operations; it does not enter Equation (6). Bounds must be ordered, finite where required by validation, and compatible with distribution support. A very narrow interval can suffer cancellation in $F(u)-F(l)$; the Numerics interval-likelihood implementation controls the final computation, and a non-positive mass is impossible under the model.

An interval observation states that an event occurred and its magnitude lies in the interval. A perception threshold instead summarizes counts of years above and below a level. They are not interchangeable.

## Perception-threshold periods

`ThresholdData(startIndex, endIndex, value)` describes an inclusive index period with threshold $c_j$. Callers supply `NumberAbove`; `NumberBelow` is computed internally. The duration is

$$
d_j=\mathrm{EndIndex}-\mathrm{StartIndex}+1.
\tag{7}
$$

For the effective counts, the stationary contribution is

$$
\ell_{T,j}(\theta)=
n_j^-\log F(c_j\mid\theta)
+n_j^+\log S(c_j\mid\theta).
\tag{8}
$$

`DataFrame.ProcessThresholdSeries()` starts from the retained user count $n_{j,\mathrm{source}}^+$, calculates $d_j-n_{j,\mathrm{source}}^+$, and subtracts one for every exact, uncertain, or interval record whose index lies in the period. It floors the resulting below count at zero. If no below years remain, the effective above count is also set to zero because explicit records account for the entire period. Reprocessing always starts from the retained source count, making it idempotent after collection changes and XML round trips.

This overlap processing prevents explicit events and aggregate threshold years from both contributing as separate years. It cannot determine whether a supplied threshold count or period is historically correct; provenance remains an analyst responsibility.

## Stationary and nonstationary chronology

The stationary likelihood evaluates the four collections directly. A threshold period is one logical pointwise component whose `Count` is $n_j^-+n_j^+$ and whose log contribution is Equation (8).

A nonstationary model instead evaluates parameters $\theta(t)$ at each item in `FullTimeSeries`. `CreateFullTimeSeries()` merges explicit records and expands aggregate threshold periods to single-index records. The disaggregation is conditional on the following chronology assumptions: explicit observations retain their recorded indexes; the processed `NumberAbove` defines a terminal portion of the inclusive threshold period; unoccupied indexes before that portion are treated as below threshold; and unoccupied indexes in that terminal portion are treated as at or above threshold. The expansion skips indexes occupied by explicit observations and does not average or marginalize over other allocations compatible with the grouped counts.

This is a deterministic input convention, not an inference that the grouped record identifies individual event years. A nonstationary result is conditional on that convention because moving threshold status among indexes changes $\theta(t)$ and the likelihood. Analysts must establish that the terminal-above ordering is defensible, represent historically dated events explicitly when their dates are known, and report sensitivity to alternative defensible chronologies when the allocation could affect conclusions.

For nonstationary likelihoods,

$$
L_D=\prod_{i=1}^{m}L_i\!\left(\theta(t_i)\right),
\tag{9}
$$

and pointwise output has one element per expanded time index. Every predicted parameter vector is validated against the underlying distribution before its observation contribution is evaluated.

## Pointwise likelihood and diagnostics

For stationary fits, `PointwiseDataLogLikelihood` returns entries in collection order: exact, uncertain, interval, then threshold. `PointwiseDataLogLikelihoodComponents` attaches index or period labels, representative values, type, and multiplicity. A low outlier retains an `Exact` component type but uses the threshold value and Equation (3); a threshold period is labeled `LeftCensored` with its combined count even if it also contains above-threshold years. Interpret the numeric contribution and `Count`, not the enum label alone.

The sum of pointwise contributions equals `DataLogLikelihood` for a finite valid state. WAIC and PSIS-LOO then treat the selected logical partition as the units being left out. Leaving out one aggregated 100-year threshold period is not the same predictive question as leaving out one year. For a nonstationary fit, expansion changes the units to indexed years. This distinction must be reported in model-comparison work.

## Compile-checked mixed-record construction

The following method uses explicit year indexes and the only public threshold count setter. `NumberBelow` is computed when the threshold series is processed.

<!-- snippet: data-frame-mixed-observations -->
```cs
private static global::RMC.BestFit.Models.DataFrame CreateMixedDataFrame()
{
    var threshold = new ThresholdData(1900, 1949, 1000.0)
    {
        NumberAbove = 3
    };

    return new global::RMC.BestFit.Models.DataFrame
    {
        ExactSeries = new ExactSeries(new[]
        {
            new ExactData(1950, 1210.0),
            new ExactData(1951, 1675.0)
        }),
        UncertainSeries = new UncertainSeries(new[]
        {
            new UncertainData(1890, new Normal(1550.0, 180.0))
        }),
        IntervalSeries = new IntervalSeries(new[]
        {
            new IntervalData(1880, 1100.0, 1300.0, 1500.0)
        }),
        ThresholdSeries = new ThresholdSeries(new[] { threshold })
    };
}
```

Required namespaces are `RMC.BestFit.Models` and `Numerics.Distributions`. The values are pedagogical and do not constitute a calibrated flood study.

## Validation and failure modes

| Check | Why it matters |
|---|---|
| Unique and correctly scaled indexes | Nonstationary trends interpret index differences numerically |
| No unintended collection overlap | Avoids double counting and unexpected threshold adjustment |
| Ordered interval bounds | Otherwise Equation (6) has no probability interpretation |
| Valid uncertainty distributions | Equation (5) requires finite central quantiles and positive retained integral |
| Inclusive threshold duration and correct source count | One off-by-one year changes the exponent in Equation (8) |
| Low-outlier flags agree with threshold | Flagged values all use Equation (3), not their recorded magnitudes |
| Common physical units | The library does not convert stage, discharge, or regional units |
| Dependence assessed | Equation (1) assumes conditional independence; serially correlated annual peaks violate it |

Do not use informal minimum-sample-size rules as proof of adequacy. Identifiability depends on family, tail information, censoring, prior strength, and target return period. Report likelihood profiles or posterior diagnostics and quantify sensitivity to influential historical evidence.

## Plotting positions and derived state

For an uncensored exact sample of size \(n\), let \(r=1\) denote the largest observation. With plotting parameter \(\alpha\), the stored exceedance plotting position is

$$
p_{E,r}=\frac{r-\alpha}{n+1-2\alpha}. \tag{10}
$$

The default \(\alpha=0\) is the Weibull convention, \(p_{E,r}=r/(n+1)\). Distribution fitting evaluates quantiles at the nonexceedance complement \(1-p_{E,r}\). Threshold, interval, uncertain, and low-outlier records use the grouped historical-data arrangement implemented by `CalculatePlottingPositions()` rather than the uncensored shortcut in Equation (10).

Replacing a complete series through `ExactSeries`, `UncertainSeries`, `IntervalSeries`, or `ThresholdSeries` attaches the new collection and item handlers before refreshing derived plotting state. A valid replacement performs one refresh; exact-series replacement also refreshes the event rate `Lambda`. Invalid transient data remain assignable so callers can assemble a frame incrementally, and the derived refresh is deferred until the frame is valid.

## Serialization and reproducibility

`ToXElement()` serializes the collections, each observation's plotting position, low-outlier state, plotting parameter, event-rate metadata, and threshold source counts. `new DataFrame(xElement)` reconstructs all four collections while suppressing intermediate derived-state refreshes, preserving the serialized plotting positions exactly. It then reprocesses effective threshold counts once. This avoids four redundant full-frame plotting-position calculations during project loading and preserves reproducibility when positions were intentionally persisted. Serialize units and provenance in the surrounding project/report because a raw `DataFrame` does not encode a formal unit system or source citation.

## Implementation and evidence

| Concern | Implementation source | Evidence |
|---|---|---|
| Collections, overlap, chronology, validation | `src/RMC.BestFit/Models/DataFrame/DataFrame.cs` | data-frame unit tests in `src/RMC.BestFit.Tests` and analytical Weibull verification |
| Record semantics | `src/RMC.BestFit/Models/DataFrame/DataTypes/` and `DataCollections/` | constructor, clone, serialization, and validation tests |
| Likelihood and quadrature | `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs` | `src/RMC.BestFit.Verification/ModelEstimation/PointwiseLogLikelihoodTests.cs` and univariate validation tests |
| Compile-checked example | `src/RMC.BestFit.Tests/Documentation/Examples/FoundationExamples.cs` | `TechnicalReferenceDocumentationTests` |

## References

<a id="ref-1"></a>[1] J. R. Stedinger and T. A. Cohn, "Flood frequency analysis with historical and paleoflood information," *Water Resources Research*, vol. 22, no. 5, pp. 785-793, 1986. doi: 10.1029/WR022i005p00785.

<a id="ref-2"></a>[2] D. R. H. O'Connell, D. A. Ostenaa, D. R. Levish, and R. E. Klinger, "Bayesian flood frequency analysis with paleohydrologic bound data," *Water Resources Research*, vol. 38, no. 5, 2002. doi: 10.1029/2000WR000028.

<a id="ref-3"></a>[3] J. F. England, Jr. et al., *Guidelines for Determining Flood Flow Frequency - Bulletin 17C*, U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 2019. doi: 10.3133/tm4B5.

[<- Parameters and priors](../models/parameters-and-priors.md) | [Technical reference index](../index.md) | [Next: Distribution families ->](../distributions/index.md)
