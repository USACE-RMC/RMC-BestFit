<!-- technical-reference-status: complete -->

# Bulletin 17C Analysis

[Previous: Bayesian Univariate Analysis](univariate.md) | [Technical Reference](../index.md) | [Estimation details](bulletin-17c-estimation.md) | [Uncertainty and diagnostics](bulletin-17c-uncertainty.md)

`Bulletin17CAnalysis` is BestFit's specialized stationary flood-frequency workflow for Expected-Moments-style estimation with systematic, historical, paleoflood, censored, uncertain, and perception-threshold information. Its usual parent is Log-Pearson Type III (LP3), as prescribed by Bulletin 17C [1]. The implementation generalizes the same moment machinery to five additional parent families, but those alternatives are software capabilities—not Bulletin 17C recommendations.

The central technical fact is easy to miss: this analysis is **penalized generalized method of moments (GMM), not Bayesian inference and not maximum likelihood**. The parent distribution supplies a probabilistic data model, but `Bulletin17CDistribution` implements `IGMMModel` rather than `IModel` and defines no likelihood or posterior. The dedicated [estimation chapter](bulletin-17c-estimation.md) gives the complete estimating equations, conditional-moment construction, quadratic objective, penalties, covariance, and optimizer behavior.

## Scope and Information Model

The analysis accepts these `DataFrame` components:

| Hydrologic information | Representation in the specialized estimator |
|---|---|
| systematic annual peak | exact moment contribution unless flagged as a low outlier |
| potentially influential low flood | repeated left-censored conditional moments below `LowOutlierThreshold` |
| historical or paleoflood magnitude range | conditional moments over an `IntervalData` interval |
| uncertain magnitude | moment functions integrated over the supplied measurement-error distribution |
| period known below a perception threshold | `ThresholdData.NumberBelow` repeated left-censored contributions |
| period known at or above a perception threshold | `ThresholdData.NumberAbove` repeated right-censored contributions |

`ProcessThresholdSeries()` is called before every fit. Record length is `DataFrame.TotalRecordLength()`, not merely the number of exact annual peaks. Log-Normal and LP3 estimating equations operate on base-10 logarithms, so all retained discharge support must be positive. Low-outlier and perception-threshold decisions are part of the observation model and must be justified and preserved in the study record; they are not generic data-cleaning switches.

## Supported Parent Families

`Bulletin17CDistribution.IsSupportedDistributionType` accepts:

- `Exponential`
- `GammaDistribution`
- `LogNormal`
- `LogPearsonTypeIII`
- `Normal`
- `PearsonTypeIII`

The first four distribution names should not be conflated with generic univariate fitting. In particular, selecting `LogPearsonTypeIII` here adds expected-moment treatment, external-information penalties, specialized covariance, and four frequentist uncertainty engines. Constructing a generic `UnivariateDistribution` with an LP3 parent instead targets a Bayesian likelihood/posterior and is a different analysis.

## Implemented Workflow

`RunAsync` orchestrates the following sequence:

1. clear prior results and wait for any in-flight result-only reprocessing;
2. process perception thresholds;
3. install the location/scale/shape optimizer links;
4. derive distribution bounds and starting values, using regression-on-order-statistics initialization when censoring requires it;
5. install enabled parameter and quantile penalty terms;
6. estimate the parent parameters with iterative GMM;
7. construct a parameter-sampling ensemble using the selected `UncertaintyMethod`; and
8. convert the retained ensemble into frequency-curve confidence limits at the configured annual exceedance probabilities.

Cancellation during fitting or uncertainty clears partial state. If GMM succeeds but uncertainty construction fails, the point estimate may remain usable while `AnalysisResults` is null; `UncertaintyDiagnosticMessage` records the reason. Callers must test both `IsEstimated` and the required result object.

## Compile-Checked Configuration

This example configures the normal operational starting point: LP3, linked-MVN uncertainty, a deterministic seed, 10,000 parameter realizations, and 90 percent confidence limits. The supplied `DataFrame` must already encode the systematic record, historical intervals, perception thresholds, magnitude uncertainty, and any defensible low-outlier decisions.

<!-- snippet: bulletin17c-workflow -->
```csharp
private static Bulletin17CAnalysis ConfigureBulletin17C(
    global::RMC.BestFit.Models.DataFrame annualPeakRecord)
{
    var model = new Bulletin17CDistribution(
        annualPeakRecord,
        UnivariateDistributionType.LogPearsonTypeIII);

    var analysis = new Bulletin17CAnalysis(model)
    {
        UncertaintyMethod = UncertaintyMethod.LinkedMultivariateNormal
    };

    analysis.BayesianAnalysis.PRNGSeed = 12345;
    analysis.BayesianAnalysis.OutputLength = 10_000;
    analysis.BayesianAnalysis.CredibleIntervalWidth = 0.90;
    return analysis;
}
```

Before calling `RunAsync`, call `Validate()` and treat every error as blocking. After the awaited run, require `analysis.IsEstimated`, a nonfailure `analysis.GMM.Status`, and non-null `analysis.AnalysisResults`. Also inspect `UncertaintyDiagnosticMessage`, `BootstrapResults` when applicable, covariance conditioning, retained ensemble size, and the generated report. A technically defensible study retains the exact input record, threshold chronology, parent family, seed, ensemble size, confidence level, software/dependency versions, and all diagnostics.

## External Information: Penalties, Not Priors

`ParameterPenalties` and `QuantilePenalties` add half-quadratic terms to the GMM objective. Regional skew is naturally represented as a penalty on the LP3 skew coefficient with its reported mean squared error. A quantile penalty can encode external frequency information at a specified annual exceedance probability. These terms are scaled consistently with the sample-mean GMM objective and can be treated as random information in the sandwich covariance and bootstrap.

They are not Bayesian priors: there is no normalized prior density, marginal likelihood, posterior, or MCMC target. For the same reason, generic posterior information criteria do not apply. A `CompositeAnalysis` cannot use DIC, WAIC, or LOOIC weighting for a Bulletin 17C child.

## Pseudo-AIC and Pseudo-BIC

`Bulletin17CAnalysis` retains the shared `AnalysisResults.AIC` and `BIC` fields for compatibility, but they are pseudo-information criteria rather than classical AIC/BIC. Let $\widehat{\boldsymbol\theta}_{\mathrm{GMM}}$ be the Bulletin 17C GMM solution stored internally in the shared `MAP` slot. The analysis constructs the corresponding LP3 `UnivariateDistribution` over the Bulletin 17C data frame and evaluates its data log likelihood,

$$
\ell_D^*=\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{GMM}}). \tag{B17C.1}
$$

It then reports

$$
\mathrm{pseudo\mbox{-}AIC}=-2\ell_D^*+2k,
\qquad
\mathrm{pseudo\mbox{-}BIC}=-2\ell_D^*+k\log n_t, \tag{B17C.2}
$$

where $k$ is the number of LP3 parameters and $n_t$ is the number of exact records minus identified low outliers plus the number of interval records. Parameter-prior densities are not included.

These values are not classical information criteria because the GMM solution does not maximize $\ell_D$, the usual AIC/BIC penalty derivations do not describe the GMM estimating equations or their effective complexity, and $n_t$ is a record-count convention rather than a derivation for grouped perception-threshold information. GMM parameter and quantile penalties affect the fitted estimate but are not represented in (B17C.2). Use the values only as descriptive, same-data heuristics under an identical Bulletin 17C setup; do not mix them with MLE or Bayesian MAP AIC/BIC or interpret their weights as posterior model probabilities.

## Uncertainty Interpretation

The default `LinkedMultivariateNormal` engine samples a variance-stabilized approximation to the GMM estimator's sampling distribution. `MultivariateNormal` samples directly in natural parameter space. `Bootstrap` simulates and refits records. The enum and GUI label `BiasCorrectedBootstrap` denote the **bias-corrected pivotal bootstrap**: a joint, multivariate studentized construction in link space whose bias correction comes from standardizing with each replicate covariance and re-inflating with the parent covariance. The compact user-facing name is intentional. It is not Efron's scalar BC or BCa endpoint algorithm.

All four engines return frequentist parameter ensembles used to form confidence intervals. Bulletin 17C deliberately uses the same `BayesianAnalysis`/`MCMCResults` storage architecture as Bayesian analyses so persistence, result reprocessing, and UI consumers retain one stable result shape. This is storage reuse, not a second inferential target, and no Bulletin 17C-specific parallel result hierarchy is required. In this context:

- `MAP` stores the penalized GMM point estimate, not a posterior mode;
- `PosteriorMean` stores the arithmetic mean of the uncertainty ensemble, not a posterior expectation;
- `Output` stores frequentist parameter realizations;
- `CredibleIntervalWidth` supplies the confidence level; and
- R-hat, effective sample size, DIC, WAIC, and LOOIC are not meaningful for this analysis.

The [uncertainty chapter](bulletin-17c-uncertainty.md) derives the sandwich covariance, gives every sampling algorithm, and documents rejection, fallback, cancellation, and diagnostic behavior.

## Results and Responsible Interpretation

For annual exceedance probability $\alpha$, each retained parameter set produces

$$
q_\alpha=F_X^{-1}(1-\alpha\mid\boldsymbol\theta). \tag{1}
$$

`ProbabilityOrdinates` stores $\alpha$. A conventional return period $T=1/\alpha$ is an average recurrence descriptor under the stationary independent-annual-maximum model, not a schedule or guarantee. Confidence limits reflect modeled sampling and configured external-information uncertainty; they do not automatically include future nonstationarity, rating-curve error omitted from the input magnitude distributions, threshold misspecification, parent-family uncertainty, or hydrologic dependence.

Use the specialized output alongside, not instead of, these checks:

- sensitivity to parent family and defensible low-outlier thresholds;
- sensitivity to regional-skew/quantile penalty means and MSEs;
- plotting-position and perception-threshold review against the historical record;
- comparison among direct MVN, linked MVN, and bootstrap intervals;
- retained-draw and failed-replicate diagnostics;
- physical plausibility of tail extrapolation; and
- consistency with the current Bulletin 17C study protocol and agency review requirements.

`ComputeCohnStyleConfidenceIntervals()` supplies a separate diagnostic comparison, not the main frequency result. It is supported only for Log-Pearson Type III with exact observations and no low outliers. The method throws `NotSupportedException` for every other parent family and for uncertain, interval-censored, threshold-censored, or low-outlier data. The report-side Cohn asymptotic-variance diagnostic applies the same guard and states why it is unavailable. Numerical verification of Cohn interval values is deferred.

## Specialized GMM Versus Generic Bayesian Univariate Analysis

| Question | `Bulletin17CAnalysis` | `UnivariateAnalysis` |
|---|---|---|
| estimating target | conditional moment equations plus optional penalties | full data likelihood times priors |
| core model contract | `IGMMModel` | `IModel` |
| primary estimate | GMM solution | posterior sampled by MCMC |
| uncertain magnitude | integrates estimating functions over measurement distribution | integrates model density times measurement density |
| external information | quadratic parameter/quantile penalties | normalized parameter/quantile priors |
| uncertainty | asymptotic MVN, linked MVN, bootstrap, pivotal bootstrap | posterior draws |
| interval terminology | confidence interval | credible interval |
| information criteria | pseudo-AIC/pseudo-BIC from the LP3 data likelihood at the GMM solution; no posterior criteria | AIC/BIC at MAP only under flat priors; DIC, WAIC, and PSIS-LOO when their prerequisites hold |

This distinction should be explicit in reports. Numerical similarity between an EMA/GMM curve and a Bayesian posterior summary does not make their inferential meanings interchangeable.

## Verification Status and Known Review Items

The formal current-path worked-example source is `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CExampleTests.cs`. Its `Test_Example1` through `Test_Example7` methods fit the specialized LP3 GMM path and compare log-space mean, standard deviation, and skewness with the published Bulletin 17C examples at absolute tolerance `1E-3`. The methods span systematic records, low outliers, broken records, historical information, crest-stage censoring, combined historical/low-outlier records, and paleoflood information. All seven exact methods passed on 28 July 2026 with zero failures or skips; each focused run produced one passing TRX after a zero-warning, zero-error Verification-only build.

The same Verification class also contains uncertain-data variants and pointwise-moment checks, while other sources contain PeakFQ plotting-position comparisons, covariance checks, penalty checks, synthetic recovery, and coverage experiments. Those are separate claims. The repository's legacy “Comparison with EMA” report concerns the earlier Bayesian workflow and is not the oracle for the current specialized GMM implementation.

TR-016 documents the shared result-storage architecture without a code/API redesign. TR-020 closes the unsafe Cohn scope with unit-tested exact-LP3 guards while deferring numerical Cohn verification. TR-021 records the formal seven-example suite passing the published parameter comparisons; the [review findings register](../review-findings.md) and [Bulletin 17C verification report](../../verification/bulletin-17c.md) preserve the evidence boundary for claims not exercised by those tests.

## Implementation Traceability

- Model: `src/RMC.BestFit/Models/UnivariateDistribution/Bulletin17CDistribution.cs`
- Analysis and uncertainty: `src/RMC.BestFit/Analyses/Univariate/Bulletin17CAnalysis.cs`
- Estimator: `src/RMC.BestFit/Estimation/GeneralizedMethodOfMoments.cs`
- Fast API/behavior tests: `src/RMC.BestFit.Tests/Univariate/Bulletin17CDistributionTests.cs`, `Bulletin17CAnalysisTests.cs`, and `Bulletin17CReportDiagnosticsTests.cs`
- Formal worked examples: `src/RMC.BestFit.Verification/Univariate/Bulletin17CTests/B17CExampleTests.cs` (`Test_Example1` through `Test_Example7`)
- Long-running evidence source: `src/RMC.BestFit.Verification/Univariate/B17CTests/`

## References

<a id="ref-1"></a>[1] J. F. England, Jr. et al., *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*, U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 2019. doi: 10.3133/tm4B5.

<a id="ref-2"></a>[2] T. A. Cohn, W. L. Lane, and W. G. Baier, “An algorithm for computing moments-based flood quantile estimates when historical flood information is available,” *Water Resources Research*, vol. 33, no. 9, pp. 2089–2096, 1997.

<a id="ref-3"></a>[3] T. A. Cohn, W. L. Lane, and J. R. Stedinger, “Confidence intervals for Expected Moments Algorithm flood quantile estimates,” *Water Resources Research*, vol. 37, no. 6, pp. 1695–1706, 2001.

---

[Estimation details](bulletin-17c-estimation.md) | [Uncertainty and diagnostics](bulletin-17c-uncertainty.md) | [Technical Reference](../index.md)
