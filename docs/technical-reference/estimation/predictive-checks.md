<!-- technical-reference-status: complete -->

# Prior and Posterior Predictive Checks

[Estimation index](index.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Diagnostics](diagnostics.md) | [Technical Reference](../index.md)

Predictive checks ask whether data generated under a model resemble the observed hydrologic record in scientifically relevant ways. They do not select a model automatically, prove adequacy, or replace convergence checks. BestFit provides generic checks only for models implementing both `IModel` and `ISimulatable<double[]>`.

## Prior Predictive Distribution

For prior $\pi(\boldsymbol\theta)$ and sampling model $p(\mathbf y\mid\boldsymbol\theta)$, the prior predictive distribution is

$$
p(\mathbf y^{\mathrm{rep}})
=\int p(\mathbf y^{\mathrm{rep}}\mid\boldsymbol\theta)
\pi(\boldsymbol\theta)\,d\boldsymbol\theta. \tag{PPC.1}
$$

Prior predictive simulation should precede fitting. It reveals whether prior assumptions permit negative discharge, implausibly large floods, impossible rating curves, unstable time-series paths, or parameter combinations that make the likelihood undefined.

`PriorPredictiveCheck.SampleFromPriors()` draws each nonfixed `ModelParameter` independently through its marginal `PriorDistribution.InverseCDF`, then clamps the value to the parameter bounds. This is exact for a product of marginal priors only when each prior support already lies within its bound. Clamping an out-of-bound draw creates boundary point mass rather than sampling a properly truncated prior.

More importantly, the sampler does not draw from additional coupled prior factors in `Model.PriorLogLikelihood`, such as quantile priors, Jeffreys scale terms, quantile Jacobians, or spatial error priors. It evaluates the full prior log kernel only to reject nonfinite draws; `ParameterSet.Fitness` stores the negative of that evaluation, so positive infinity or `NaN` marks an unusable draw. Fitness is not used as an importance weight. Therefore current generic prior-predictive output is not distributed as (PPC.1) whenever the model has such extra prior components. This limitation is recorded in the review findings and must be disclosed.

For every accepted parameter draw, BestFit clones the model and calls `GenerateRandomValues(sampleSize, seed)`. Exceptions and empty simulations are filtered from the returned list. The effective prior distribution is thus also conditional on simulation success, but the result does not report a failure rate.

## Posterior Predictive Distribution

Given fitted data $\mathbf y$, the posterior predictive distribution is

$$
p(\mathbf y^{\mathrm{rep}}\mid\mathbf y)
=\int p(\mathbf y^{\mathrm{rep}}\mid\boldsymbol\theta)
p(\boldsymbol\theta\mid\mathbf y)\,d\boldsymbol\theta. \tag{PPC.2}
$$

`PosteriorPredictiveCheck` selects stored posterior parameter sets with replacement, clones the model, and generates a dataset with length equal to the supplied `observedData`. It pre-generates indices and seeds, so parallel execution remains reproducible for a fixed seed and posterior list. Failed simulations are filtered rather than replaced or counted in the result.

The generic simulator reproduces the model's `GenerateRandomValues` contract, not necessarily the original observation process. A check based only on exact `double[]` values does not automatically recreate perception thresholds, interval censoring, measurement error, low-outlier classification, missing covariates, nonstationary time indices, spatial site geometry, or point-process exposure. A scientifically valid check must simulate those mechanisms and then apply the same data-reduction rules used for fitting. When the model simulator does not do that, label the result as a conditional latent-value check.

## Discrepancy Statistics and Bayesian Predictive p-Values

For scalar statistic $T$, BestFit computes the one-sided posterior predictive p-value

$$
p_B=P\{T(\mathbf y^{\mathrm{rep}})\ge T(\mathbf y)\mid\mathbf y\}
\approx\frac1R\sum_{r=1}^{R}
\mathbb I\{T(\mathbf y_r^{\mathrm{rep}})\ge T(\mathbf y)\}. \tag{PPC.3}
$$

`ComputeCommonPValues()` applies (PPC.3) to sample mean, standard deviation, skewness, minimum, and maximum. `HasPotentialMisfit(threshold)` treats values below the threshold or above its complement as potentially problematic. Posterior predictive p-values are generally conservative because the data inform both the posterior and the discrepancy comparison; they are not uniformly distributed classical p-values.

Hydrologic discrepancy functions should target the decision-relevant structure. Useful additions include:

- exceedance counts above operational or paleoflood thresholds;
- upper-tail spacings and the largest few order statistics;
- duration, seasonality, and clustering of threshold exceedances;
- residual pattern versus stage or covariates for rating curves;
- lag autocorrelation and forecast errors for time series;
- sitewise and cross-site extremes for spatial models.

A model can pass mean and variance checks while failing the upper tail that drives life-safety risk.

## Predictive Summaries

Both check classes can summarize generated datasets. For each valid replicate, BestFit computes mean, sample standard deviation, minimum, and maximum, then reports the 0.025, 0.25, 0.50, 0.75, and 0.975 quantiles of each statistic across replicates. `NumberOfValidDraws` is the number remaining after failure filtering. These summaries describe replicate statistics, not pointwise predictive intervals for a future observation or return level.

## Compile-Checked API Workflow

<!-- snippet: predictive-checks-workflow -->
```csharp
private static (
    PredictiveCheckResults Posterior,
    PredictiveSummary Prior) RunPredictiveChecks(
        IModel model,
        MCMCResults results,
        double[] observedData)
{
    var posteriorCheck = new PosteriorPredictiveCheck(
        model,
        results,
        observedData)
    {
        Seed = 12345
    };

    var priorCheck = new PriorPredictiveCheck(model)
    {
        Seed = 12345,
        NumberOfDraws = 1_000
    };

    return (
        posteriorCheck.ComputeCommonPValues(numberOfReplicates: 1_000),
        priorCheck.ComputeSummary(sampleSize: observedData.Length));
}
```

The code is appropriate only when the model's simulator generates the same scientific quantity represented by `observedData`. For censored or uncertain flood records, build an observation-process-specific check instead of passing reconstructed point values. Always report requested and valid replicate counts, seed, posterior-draw source, statistic definitions, units, and any simulation failures.

## Interpretation and Failure Modes

- A central p-value does not prove the model; it says only that one chosen statistic is not surprising under the fitted predictive distribution.
- Extreme p-values can arise from model form, data errors, an omitted observation mechanism, or a poorly converged posterior.
- Reusing the data in posterior predictive checks makes them conservative for many discrepancies.
- Checking many statistics invites selective interpretation; predeclare decision-relevant discrepancies where possible.
- Failure filtering can hide posterior or prior mass in numerically or physically invalid regions.
- Generic IID replication is not valid for trend, time-series, point-process, or spatial dependence unless `GenerateRandomValues` explicitly preserves that structure.

## Verification and Traceability

Fast tests cover constructors, deterministic seeding, validation, output shapes, p-value arithmetic, summary quantiles, and failed-replicate filtering. They do not establish calibration for each scientific model. Model-specific simulation-based calibration and predictive coverage belong in the Verification project; it was not run during this pass.

Implementation symbols: `PriorPredictiveCheck`, `PosteriorPredictiveCheck`, `PredictiveCheckResults`, `PredictiveSummary`, `ISimulatable<double[]>`, `IModel.Clone`, and `IModel.SetParameterValues`.

## References

<a id="ref-1"></a>[1] G. E. P. Box, “Sampling and Bayes' inference in scientific modelling and robustness,” *Journal of the Royal Statistical Society: Series A*, vol. 143, no. 4, pp. 383–430, 1980.

<a id="ref-2"></a>[2] A. Gelman, X.-L. Meng, and H. Stern, “Posterior predictive assessment of model fitness via realized discrepancies,” *Statistica Sinica*, vol. 6, pp. 733–760, 1996.

<a id="ref-3"></a>[3] J. Gabry et al., “Visualization in Bayesian workflow,” *Journal of the Royal Statistical Society: Series A*, vol. 182, no. 2, pp. 389–402, 2019.

---

[Previous: model comparison](model-comparison.md) | [Next: diagnostics](diagnostics.md)
