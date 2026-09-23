# Information expansion: mappings and source-backed examples

Use official [Bulletin 17C](https://pubs.usgs.gov/publication/tm4B5) for collecting
and representing flood evidence and the applicable regional report for regional
estimates. Current BestFit model/API contracts determine supported entry fields.
The [v1 guide](https://usace-rmc.github.io/RMC-Software-Documentation/docs/desktop-applications/rmc-bestfit/users-guide/v1.0/working-with-rmc-bestfit/)
and [verification report](https://usace-rmc.github.io/RMC-Software-Documentation/source-documents/desktop-applications/rmc-bestfit/verification-report/RMC-BestFit-Verification-Report.pdf)
explain the examples. Word originals were unavailable during review; these
published references, current source and saved projects were used instead.
Archived outputs are not new numerical verification oracles. Inspect saved data
and configuration rather than trusting generated example prose/placeholders.

## Regional LP3 skew

For a source-supported regional mean `-0.17` and MSE `0.12`, the Bayesian Normal
prior uses SD `sqrt(0.12)`; the B17C penalty retains MSE. The Blakely verification
discussion (printed report p.45) cites Wagner, Krieger and Veilleux (2016) for this
example. It is not a default for arbitrary locations.

```json
{"parameterPriors":[{"parameterName":"Skew (of log)","distribution":{"type":"normal","parameters":[-0.17,0.34641016151377546]}}]}
```

```json
{"parameterPenalties":[{"parameterName":"Skew (of log)","mean":-0.17,"mse":0.12,"useLog":false}]}
```

Discover current parameter names through `/api/metadata/distributions`; do not
copy old serialized prefixes. LP3 log skew is not another distribution's shape
parameter or a discharge. ARR-FLIKE Example 5 uses skew `0`, MSE `0.09` (SD `0.30`);
keep that case and its published acceptance criteria distinct.

## Regional and causal quantiles

A quantile prior describes uncertainty about `Q(AEP)` in physical units. Its
distribution is separate from the parent flood distribution. Regional prediction,
hydrologic modeling or elicitation must establish location, flow duration, AEP,
magnitude, uncertainty and applicability. Rainfall return period is not automatically
flood return period; PMF is not an assigned probabilistic quantile. Do not build an
unrequested rainfall-runoff model to manufacture causal evidence.

Kamp/Viglione is **GEV**, although the generic skill default is LP3. Causal information
is `Q500 ~ Normal(480,80)` m³/s: AEP `.002`, mean `480`, SD `80`. The canonical
verification disables the Jeffreys scale rule; the saved project enables it.
State which configuration is reproduced:

```json
{
  "distribution":"generalizedExtremeValue",
  "useSingleQuantile":true,
  "useJeffreysRuleForScale":false,
  "quantilePriors":[{"alpha":0.002,"distribution":{"type":"normal","parameters":[480,80]}}]
}
```

The source describes expert elicitation and temporal/spatial/causal expansion
(printed report pp.57–59). Preserve those assumptions; `480` is not an annual
observation. Compare with a baseline and show the change in intervals.

B17C `quantilePenalties` uses `{aep,mean,mse,useLog10}`. With `useLog10=true`, both
**mean and MSE are already in log10 space**. The saved one-day Blakely project has
`aep=.0001, mean=5.4689, mse=.016, useLog10=true`; stored settings alone do not
justify that quantile at another site. Do not take `log10(physical variance)` or
substitute a physical mean here. A justified physical Normal penalty uses
`useLog10=false` and variance `SD²`.

The preparation helper maps physical Normal quantile information and B17C
physical/log10 penalties. It refuses automatic log-space-to-Bayesian conversion.
Construct a physical quantile distribution only after verifying source assumptions
and BestFit distribution parameterization.

## Multiple sources and quantiles

The univariate API accepts one quantile with `useSingleQuantile=true`, or one per
parent-distribution parameter for the multi-quantile formulation (three for LP3
and GEV). Use decreasing AEPs and increasing means and bounds. A two-quantile LP3
request is rejected. Model validation remains authoritative.

The multi-quantile construction transforms adjacent increments; it is not a
general multivariate Normal prior and accepts no covariance matrix. Kamp's three
priors use AEPs `.1,.01,.001` and Normal mean/SD pairs `(100,20),(250,40),(500,60)`.
Do not combine these with the causal single quantile as independent measurements.
Prefer separate single-quantile sensitivity candidates unless the supported joint
construction and dependencies are justified. Document overlap in regional gages,
skew, station weighting, historical floods and causal-model calibration data.

## Preserve each example's flow statistic and configuration

| Source | Context and entry |
|---|---|
| Saved Blakely **one-day** B17C | 91 exact years 1923–2018, missing 1931–1935. 1882 interval 115000/132500/150000 cfs; 1870–1922 and gap thresholds 110000 |
| Same project's paleo input | 1020 interval 181000/187000/194000; thresholds -2980–1018 at 376500 and 1019–1869 at 175900 cfs |
| Published guide's **three-day** Blakely | 1882 interval 66000/76000/86000; 1020 interval 105000/110000/115000 cfs; thresholds -2890–1018 at 220000, 1019–1869 at 104000, 1870–1922 and 1931–1935 at 65000 |
| Saved Kamp/Viglione GEV | 51 exact years 1951–2001; extended case 55 through 2005. Intervals 1655 [180,300], 1803 [240,400], 1829 [202.5,337.5] m³/s; 1600–1950 threshold 300, aggregate above 0, below 348 |

These teach entry; they are not approved inputs for a new study. Preserve original
lambda and other explicit settings when reproducing a saved analysis. Reconcile
uncertainty and period boundaries before adopting inputs.

Repository references:

- `examples/4-univariate-distribution-analysis/1-univariate-analysis/1-information-expansion/viglione-et-al-2013.bestfit`
- `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/2-information-expansion/blakely-mountain-dam-b17c.bestfit`
- `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.bestfit`
- `docs/technical-reference/data-frame/index.md` and `docs/technical-reference/models/parameters-and-priors.md`

Save analysis-created/get `configuration` before and after execution: parent
distribution, ordinates, priors/penalties, Jeffreys setting and exposed MCMC options
come from the actual model. Automatic simulation defaults may change at run time.
This records applied settings, not scientific appropriateness.
