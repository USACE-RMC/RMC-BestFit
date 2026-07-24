<!-- technical-reference-status: complete -->

# Scientific Model and Analysis Contracts

[<- Documentation contract](../documentation-contract.md) | [Technical reference index](../index.md) | [Next: Data likelihood ->](../data-frame/index.md)

## Scope

RMC.BestFit separates a statistical model from the analysis that operates on it. A model specifies a probability law and exposes likelihood, prior, pointwise, parameter, validation, and, where applicable, simulation behavior. An analysis validates and orchestrates estimation, cancellation, result construction, and progress reporting. This distinction matters in peer review: an estimator does not define the data-generating process, and a plotted frequency curve is a post-processed model output rather than a likelihood term.

This chapter defines the contracts used throughout the reference. Distribution-specific parameterizations appear in the [distribution chapters](../distributions/index.md); mixed observation likelihoods appear in the [data-frame chapter](../data-frame/index.md); priors appear in [Parameters and priors](parameters-and-priors.md).

## Notation

| Symbol | Meaning |
|---|---|
| $y=(y_1,\ldots,y_m)$ | Logical observations represented by a `DataFrame` or another model-specific container |
| $\theta=(\theta_1,\ldots,\theta_p)$ | Natural-space model parameter vector in the exact order exposed by `Parameters` |
| $L(y\mid\theta)$ | Data likelihood |
| $\ell_D(\theta;y)$ | Data log-likelihood, $\log L(y\mid\theta)$ |
| $\pi(\theta)$ | Joint prior density as implemented by the model |
| $\ell_P(\theta)$ | Log-prior, $\log\pi(\theta)$ |
| $\ell(\theta;y)$ | Unnormalized log-posterior kernel |
| $\ell_{D,j}$ | Pointwise contribution for logical observation $j$ |

All logarithms are natural logarithms unless a chapter explicitly states otherwise. Discharge has the user's supplied discharge units; stage, time, and covariates likewise retain their supplied units. The library does not perform implicit unit conversion.

## Probability contract

For a model with likelihood $L$ and prior $\pi$, the posterior is

$$
p(\theta\mid y)=\frac{L(y\mid\theta)\pi(\theta)}
{\int_\Theta L(y\mid\vartheta)\pi(\vartheta)\,d\vartheta},
\tag{1}
$$

and the quantity returned by `IModel.LogLikelihood` is the unnormalized log-posterior kernel

$$
\ell(\theta;y)=\ell_D(\theta;y)+\ell_P(\theta).
\tag{2}
$$

The method name is historical: `LogLikelihood` includes priors. Code that requires the sampling distribution alone must call `DataLogLikelihood`. The abstract `ModelBase` implements Equation (2) directly and converts a non-finite total to `double.NegativeInfinity`. Individual scientific models may override the method when they add model-specific prior or penalty terms.

`PointwiseDataLogLikelihood` returns

$$
\boldsymbol{\ell}_D(\theta;y)=
(\ell_{D,1},\ldots,\ell_{D,m}),\qquad
\sum_{j=1}^{m}\ell_{D,j}=\ell_D(\theta;y),
\tag{3}
$$

where a *logical observation* is defined by the model. In a stationary univariate model, each exact, uncertain, or interval record is one item and each threshold-count period is one aggregate item even when its multiplicity exceeds one. In a nonstationary univariate model, threshold counts are expanded to index-level contributions using `FullTimeSeries`; the chronology assumption is documented in the [data-frame chapter](../data-frame/index.md). WAIC, PSIS-LOO, and observation-influence calculations depend on this partition, so analysts must compare models using the same logical data partition.

`PointwisePriorLogLikelihood` is the corresponding decomposition of the implemented prior. It is diagnostic infrastructure; prior components are not interchangeable with observations and must not be appended to Equation (3) when calculating observation-wise predictive criteria.

### Impossible states and numerical conventions

The model contract uses `double.NegativeInfinity`, not an arbitrary large negative number, for parameters outside the support, invalid probability differences, non-positive scale parameters, and other impossible states. Estimators and samplers therefore receive a mathematically meaningful zero density. `NaN`, positive infinity, and accidental arithmetic overflow are not acceptable likelihood values; model implementations collapse them to negative infinity or report validation failures before estimation.

Bounds and distribution support are different controls. `ModelParameter.LowerBound` and `UpperBound` constrain estimation and validation. The probability distribution still determines whether a datum has positive density at a candidate parameter vector. A fixed parameter remains part of the vector and the model evaluation; `IsFixed` tells an estimator not to vary it.

## `IModel`

The public model interface combines probability evaluation, metadata, state management, validation, cloning, and XML serialization:

| Member | Statistical meaning | Required consistency |
|---|---|---|
| `Parameters` | Ordered parameter metadata and priors | Order must match every likelihood method |
| `NumberOfParameters` | Parameter-vector dimension | Equals the required evaluation-vector length |
| `UseDefaultFlatPriors` | Requests model defaults rather than configured informative priors | Its exact effect is model-specific and must be set before estimation |
| `LogLikelihood(theta)` | Equation (2) | Equals data plus prior for every finite valid state |
| `DataLogLikelihood(theta)` | $\ell_D$ | Equals the sum of pointwise data contributions |
| `PriorLogLikelihood(theta)` | $\ell_P$ | Includes every implemented prior/Jacobian term |
| `PointwiseDataLogLikelihood(theta)` | Equation (3) | Stable partition across posterior draws |
| `PointwiseDataLogLikelihoodComponents(theta)` | Data decomposition plus type, value, count, and name metadata | Log contributions sum to the scalar data likelihood |
| `PointwisePriorLogLikelihood(theta)` | Named `PriorComponent` decomposition | Log contributions sum to the scalar prior for finite valid states |
| `SetParameterValues(values)` | Mutates current parameter values | Uses the same order as `Parameters` |
| `SetDefaultParameters()` | Reconstructs model-specific defaults | May replace values, bounds, and priors |
| `Clone()` | Deep model copy | Scientific state must be independent of the source object |
| `ToXElement()` | XML representation | Preserves behavior-affecting configured state |
| `Validate()` | Preflight validity and messages | Must be checked before analysis execution |

The interface accepts `double[]`; it does not attach names or units at evaluation time. Callers must preserve `Parameters` order and should never infer order from display text. A wrong-but-length-compatible vector can be statistically meaningless without producing a C# type error.

### Compile-checked likelihood decomposition

The following method is compiled in `RMC.BestFit.Tests`. A Normal model expects location followed by scale, so the candidate vector is $(1400,350)$ in the same discharge units as the four observations.

<!-- snippet: foundations-likelihood-decomposition -->
```cs
private static (double Data, double Prior, double Posterior) EvaluateLikelihood()
{
    var dataFrame = new global::RMC.BestFit.Models.DataFrame
    {
        ExactSeries = new ExactSeries(new[] { 1040.0, 1250.0, 1510.0, 1870.0 })
    };

    var model = new UnivariateDistribution(
        dataFrame,
        UnivariateDistributionType.Normal);

    double[] parameters = { 1400.0, 350.0 };
    double data = model.DataLogLikelihood(parameters);
    double prior = model.PriorLogLikelihood(parameters);
    double posterior = model.LogLikelihood(parameters);

    return (data, prior, posterior);
}
```

This evaluates a candidate state; it does not estimate parameters. Estimation requires the MLE, MAP, GMM, or Bayesian analysis classes described in the estimation chapters. The returned posterior should equal `data + prior` to floating-point precision when all three are finite.

## Specialized model contracts

`IUnivariateModel` adds the distribution, data frame, stationary/nonstationary trend models, quantile-prior controls, Jeffreys-scale-prior control, and frequency-curve operations required by univariate analyses. These properties alter the probability model; they are not presentation settings.

`IGMMModel` exposes the observed moment vector, moment conditions, Jacobian, parameter bounds, and weighting behavior used by generalized method of moments. Its objective is not Equation (2); the GMM chapter defines its quadratic criterion and identification conditions.

`ISimulatable<TData>.GenerateRandomValues(sampleSize, seed)` generates from the current model state. A positive seed makes the pseudo-random stream reproducible under the same implementation and dependency version. Simulation does not by itself draw parameters from their posterior; posterior predictive analysis must select a posterior draw and then simulate conditional data.

## Analysis contract

`IAnalysis` is an orchestration contract rather than a statistical distribution. `Validate()` reports whether a configured analysis can run. `RunAsync(progressReporter)` begins the analysis and `CancelAnalysis()` requests cancellation. `AnalysisStarting` can cancel before computation. `AnalysisCompleted` reports successful, cancelled, or failed termination, and `IsEstimated` states whether usable estimation results exist.

An analysis chapter must therefore document the following sequence:

```text
configured model and data
    -> validation
    -> initialization and estimator orchestration
    -> parameter/posterior results
    -> derived frequency, predictive, or diagnostic results
    -> completion, error, or cancellation state
```

Model fitting ends when parameter or posterior results have been obtained. Quantile curves, credible intervals, probability ordinates, averaging weights, diagnostics, and plots are derived outputs. Their uncertainty treatment must be described separately; substituting a point estimate into a nonlinear output is not equivalent to propagating posterior uncertainty.

## Assumptions, failure modes, and reviewer checks

- Conditional independence is model-specific. For example, the stationary univariate likelihood multiplies logical observation contributions conditional on $\theta$; time-series and spatial models explicitly introduce dependence.
- A finite likelihood is not evidence of adequate fit. Tail extrapolation, temporal dependence, nonstationarity, censoring assumptions, and prior sensitivity require diagnostics appropriate to the analysis.
- Bounds can create optima at the edge. Reports must identify boundary estimates and explain whether the bound is physical, numerical, or merely a default search limit.
- Pointwise predictive criteria are only comparable when the same observations, conditioning assumptions, and pointwise partition are used.
- Serialization reconstructs configured scientific state but is not independent validation of the formulation.
- An analysis cancelled or completed with an error must not be interpreted as estimated merely because partial in-memory results exist; use the completion state and `IsEstimated`.

## Implementation and verification traceability

| Concern | Implementation source | Fast/verification evidence |
|---|---|---|
| Core probability contract | `src/RMC.BestFit/Models/Support/IModel.cs`, `ModelBase.cs` | `src/RMC.BestFit.Tests/Models/Support/ModelBaseTests.cs` where present; model-specific verification suites |
| Parameter metadata | `src/RMC.BestFit/Models/Support/ModelParameter.cs` | `src/RMC.BestFit.Tests/Models/Support/ModelParameterTests.cs` |
| Univariate decomposition | `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs` | `src/RMC.BestFit.Verification/ModelEstimation/` and univariate verification folders |
| Analysis lifecycle | `src/RMC.BestFit/Analyses/Support/IAnalysis.cs`, `AnalysisRunCompletedEventArgs.cs` | analysis-specific unit tests |
| Compile-checked example | `src/RMC.BestFit.Tests/Documentation/Examples/FoundationExamples.cs` | `TechnicalReferenceDocumentationTests` |

The verification project is intentionally long-running and is not part of the normal documentation gate. A validation claim in a later chapter names the exact test or published result on which it relies.

## References

<a id="ref-1"></a>[1] A. Gelman, J. B. Carlin, H. S. Stern, D. B. Dunson, A. Vehtari, and D. B. Rubin, *Bayesian Data Analysis*, 3rd ed. Boca Raton, FL, USA: CRC Press, 2013.

<a id="ref-2"></a>[2] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001. doi: 10.1007/978-1-4471-3675-0.

<a id="ref-3"></a>[3] A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, pp. 1413-1432, 2017. doi: 10.1007/s11222-016-9696-4.

[<- Documentation contract](../documentation-contract.md) | [Technical reference index](../index.md) | [Next: Data likelihood ->](../data-frame/index.md)
