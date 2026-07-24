<!-- technical-reference-status: complete -->

# Distribution Fitting Analysis

[Previous: Analyses Overview](overview.md) | [Technical Reference](../index.md) | [Next: Univariate Analysis](univariate.md)

`FittingAnalysis` is a screening analysis: it fits the fifteen configured distribution families independently by maximum likelihood and reports AIC, BIC, and a plotting-position RMSE for each successful fit. It does not perform Bayesian updating, quantify model-form uncertainty, or establish that the lowest-criterion family is physically correct. Its defensible use is to identify plausible candidates for the fuller analyses described in [Univariate Analysis](univariate.md).

## Scope and Candidate Set

The default `DistributionList` contains Exponential, Gamma, Generalized Extreme Value, Generalized Logistic, Generalized Normal, Generalized Pareto, Gumbel, Kappa Four, Ln-Normal, Logistic, Log-Normal, Log-Pearson Type III, Normal, Pearson Type III, and Weibull, in that order. The list contents are mutable, so a caller may screen a scientifically justified subset. The analysis creates one `FittedDistribution` result per configured candidate.

The input is a complete `DataFrame`. Exact, uncertain, interval-censored, and perception-threshold records therefore enter through the likelihood in [Data Frame and Observation Likelihood](../data-frame/index.md); this is not an exact-data-only fit.

## Likelihood and Estimator

Let candidate family (m) have parameter vector (oldsymbol\theta_m\in\Theta_m), and let (ell_{D,m}) denote its full data log-likelihood, including every supported observation type. The fitted parameter vector is

$$
\widehat{\boldsymbol\theta}_m
=\arg\max_{\boldsymbol\theta_m\in\Theta_m}
\ell_{D,m}(\boldsymbol\theta_m). \tag{1}
$$

`FittingAnalysis` constructs a `UnivariateDistribution`, applies its data-derived defaults and bounds, disables the optional Jeffreys scale term, and invokes `MaximumLikelihood` with `OptimizationMethod.DifferentialEvolution`. The optimizer receives `DataLogLikelihood`, not `LogLikelihood`, so parameter and quantile priors are excluded. For this workflow, Hessian computation is disabled, the iteration limit is 10,000, and the function-evaluation limit is 100,000. A successful optimizer status is necessary but not sufficient for a published result: AIC, BIC, and RMSE must all also be finite.

Each candidate is fitted in a separate `Parallel.For` iteration. A failure in one family is caught and stored in that candidate's `ErrorMessage`; it does not abort the remaining fits. Consequently, reviewers must inspect `FitSucceeded` for every candidate instead of treating the outer `IsEstimated` flag as evidence that all—or any—families fitted successfully. This run-state issue is tracked in [TR-010](../review-findings.md#tr-010).

## Comparison Statistics

For (k_m) fitted parameters and maximized data log-likelihood (widehat\ell_{D,m}), the implementation reports

$$
\operatorname{AIC}_m=-2\widehat\ell_{D,m}+2k_m, \tag{2}
$$

and

$$
\operatorname{BIC}_m=-2\widehat\ell_{D,m}+k_m\log n_{\mathrm{eff}}, \tag{3}
$$

where `DataFrame.TotalRecordLength()` supplies (n_{\mathrm{eff}}). The meaning of that effective length for grouped perception-threshold records is defined in the data-frame chapter. Criterion values are comparable only across fits to the same observational information and likelihood convention.

For exact, uncertain, and interval records, `FittingAnalysis` pairs each stored representative value (y_i) with its plotting-position complement (p_i), evaluates (q_i=F_m^{-1}(p_i\mid\widehat{\boldsymbol\theta}_m)), and calls the Numerics RMSE helper. The intended degrees-of-freedom form is

$$
\operatorname{RMSE}_m
=\left[\frac{1}{n-k_m}\sum_{i=1}^{n}
\left(y_i-q_i\right)^2\right]^{1/2}. \tag{4}
$$

The pinned helper currently sums only indices (0,\ldots,n-k_m-1), thereby dropping the last (k_m) residuals instead of merely changing the denominator. That implementation discrepancy is [TR-009](../review-findings.md#tr-009). Until resolved, use the reported RMSE only as an implementation-specific screening statistic, and do not reproduce equation (4) from the current value without an independent calculation.

Neither AIC nor BIC measures tail plausibility, structural adequacy, or compliance with a regulatory method. RMSE is in the units of the modeled variable and therefore cannot be compared across differently scaled datasets.

## Compile-Checked Workflow

The following code is compiled as part of `RMC.BestFit.Tests`. It validates the configuration, runs all configured candidates, removes failures, and orders successful fits by AIC.

<!-- snippet: fitting-analysis-workflow -->
```csharp
private static async Task<IReadOnlyList<FittedDistribution>> FitCandidateFamilies(
    global::RMC.BestFit.Models.DataFrame dataFrame)
{
    var analysis = new FittingAnalysis(dataFrame);
    var validation = analysis.Validate();
    if (!validation.IsValid)
    {
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, validation.ValidationMessages));
    }

    await analysis.RunAsync();

    return analysis.FittedDistributions
        .Where(result => result.FitSucceeded)
        .OrderBy(result => result.AIC)
        .ToArray();
}
```

For flood-frequency practice, screen only families whose support and tail behavior are compatible with the phenomenon. Compare high-return-period quantiles and parameter-bound proximity, not merely the first row of an AIC sort. A narrow AIC difference is not evidence that extrapolated quantiles are practically interchangeable.

## Lifecycle, Cancellation, and Persistence

`RunAsync` validates the data frame and probability ordinates, raises the preview event, creates a cancellation token, clears prior results, and starts the parallel fits. Cancellation prevents queued iterations from starting and prevents completed iterations from being stored after the token is observed. The completion event distinguishes cancellation, success, and an outer exception.

Changing the data frame clears fits. Changing `ProbabilityOrdinates` does not refit because those ordinates are consumed by presentation rather than the MLE. `ToXElement()` stores the ordinates, outer estimated flag, and each `FittedDistribution`; the raw data frame remains external and must be supplied to the deserializing constructor.

## Assumptions, Limitations, and Review Checks

- All candidates are optimized inside their configured finite parameter bounds. A solution at a bound warrants scrutiny.
- Differential evolution is stochastic. A single successful run does not rule out another optimum; repeat difficult fits or use an independently configured estimator.
- Representative values for uncertain and interval observations are used only in the RMSE display statistic; the likelihood itself integrates or intervals over those observations.
- Perception-threshold counts affect the likelihood and effective record length but do not enter the plotting-position RMSE vector.
- The family list includes highly flexible models. Information criteria penalize parameter count but do not protect against scientifically implausible tail extrapolation.
- No numerical table in this chapter is hand-authored. Verification claims must point to a deterministic assertion or an approved long-running verification result.

## Implementation and Verification Traceability

| Concern | Implementation |
|---|---|
| Candidate orchestration | `Analyses/DistributionFitting/FittingAnalysis.cs` |
| Result DTO and XML | `Models/DistributionFitting/FittedDistribution.cs` |
| MLE objective and status | `Estimation/MaximumLikelihood.cs` |
| Family likelihoods | `Models/UnivariateDistribution/UnivariateDistribution.cs` |
| AIC, BIC, RMSE helpers | pinned `Numerics/Data/Statistics/GoodnessOfFit.cs` |
| Documentation compile gate | `RMC.BestFit.Tests/Documentation/TechnicalReferenceDocumentationTests.cs` |

The existing computational verification project contains family-specific estimator comparisons, but it is intentionally not executed by the documentation gate. See the [distribution verification matrix](../distributions/verification-matrix.md) for the evidence map.

## References

<a id="ref-1"></a>[1] H. Akaike, “A new look at the statistical model identification,” *IEEE Transactions on Automatic Control*, vol. 19, no. 6, pp. 716–723, 1974.

<a id="ref-2"></a>[2] G. Schwarz, “Estimating the dimension of a model,” *Annals of Statistics*, vol. 6, no. 2, pp. 461–464, 1978.

<a id="ref-3"></a>[3] K. P. Burnham and D. R. Anderson, *Model Selection and Multimodel Inference*, 2nd ed. New York, NY, USA: Springer, 2002.

---

[Previous: Analyses Overview](overview.md) | [Technical Reference](../index.md) | [Next: Univariate Analysis](univariate.md)
