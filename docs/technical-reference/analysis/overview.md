<!-- technical-reference-status: complete -->

# Analysis Architecture and Lifecycle

[<- Link functions](../support/link-functions.md) | [Technical reference index](../index.md) | [Next: Distribution fitting ->](distribution-fitting.md)

## Model fitting versus analysis

An RMC.BestFit model defines a data-generating distribution, likelihood, priors, parameters, and validation. An analysis coordinates work around that model: initialization, estimation, cancellation, progress, uncertainty propagation, construction of frequency or forecast outputs, serialization, and completion notification.

This separation prevents three common category errors:

1. A sampler configuration is not a probability model.
2. A fitted point estimate is not an uncertainty analysis.
3. A plotted return-level curve is a derived result, not an additional likelihood contribution.

The exact scientific sequence is analysis-specific. Later chapters document what each `RunAsync` implementation performs and which outputs it builds.

## `IAnalysis` contract

All scientific analysis classes implement `IAnalysis`, which also implements `INotifyPropertyChanged`.

| Member | Contract | Interpretation |
|---|---|---|
| `Validate()` | Returns `(IsValid, ValidationMessages)` | Preflight assessment of configured state; no successful estimate is implied |
| `RunAsync(progressReporter)` | Executes asynchronously | Orchestrates the analysis-specific estimation and result pipeline |
| `CancelAnalysis()` | Requests cancellation | Cooperative signal; it does not synchronously abort executing numerical code |
| `IsEstimated` | Read-only through the interface | Indicates that the analysis considers usable estimation results available |
| `AnalysisStarting` | Event with `CancelEventArgs` | Allows a handler to cancel before work begins |
| `AnalysisCompleted` | Event with `AnalysisRunCompletedEventArgs` | Reports success, cancellation, or an exception after termination |

Contrary to the former reference page, `IAnalysis` does not expose `ClearResults`, and callers cannot set `IsEstimated` through the interface. Individual concrete analyses may expose additional result-reset or post-processing methods.

`AnalysisRunCompletedEventArgs` derives from `AsyncCompletedEventArgs`. `Succeeded` is the explicit success flag; inherited `Cancelled` and `Error` describe the other terminal states. A reliable handler considers these states together rather than assuming that a completed task produced results.

## Base-class lifecycle support

`AnalysisBase` supplies event invocation, `IsEstimated` state, property notification, and a lazily created `CancellationTokenSource`. `ResetCancellationToken()` disposes any previous source and creates a fresh token for a new run. `CancelAnalysis()` signals the current source.

Derived analyses remain responsible for observing or forwarding the token, setting terminal state, and raising completion consistently. Cancellation latency therefore depends on where the underlying optimizer, sampler, bootstrap, or output loop checks its token. A cancellation request should be treated as pending until the run terminates and its completion state is known.

`AnalysisStarting` is a final pre-run hook, not a replacement for `Validate()`. A typical lifecycle is

```text
configured model/data
    -> Validate()
    -> AnalysisStarting (may cancel)
    -> reset cancellation token
    -> initialize estimator/model state
    -> estimate
    -> construct derived uncertainty/results
    -> set IsEstimated only when usable
    -> AnalysisCompleted(success | cancelled | error)
```

A concrete chapter must identify deviations from this sequence. Consumers should not use partially populated objects after a cancelled or failed run unless that concrete API explicitly guarantees their meaning.

## Bayesian analysis contract

`IBayesianAnalysis` extends `IAnalysis` with two members:

| Member | Meaning |
|---|---|
| `BayesianAnalysis` | Estimator configuration, MCMC results, and diagnostics owned by `RMC.BestFit.Estimation` |
| `AnalysisResults` | Nullable `UncertaintyAnalysisResults` constructed from the Bayesian output |

These objects answer different questions. `BayesianAnalysis` describes posterior sampling in parameter space. `AnalysisResults` contains analysis-specific propagated outputs. The latter can remain null if estimation or result construction does not complete.

For a derived scalar $g(\theta)$, full posterior propagation uses draws $\theta^{(s)}$:

$$
g^{(s)}=g\!\left(\theta^{(s)}\right),
\qquad s=1,\ldots,S.
\tag{1}
$$

Credible intervals and posterior summaries are calculated from $\{g^{(s)}\}$. In general,

$$
E[g(\theta)\mid y]\ne g(E[\theta\mid y]),
\tag{2}
$$

so a curve evaluated only at mean or MAP parameters does not represent posterior output uncertainty.

## Compile-checked validated run

The following workflow compiles against the current API. It performs explicit validation, awaits the analysis, and verifies both the lifecycle flag and propagated results.

<!-- snippet: analysis-validated-run -->
```cs
private static async Task<UnivariateAnalysis> RunValidatedAnalysis(
    UnivariateDistribution model)
{
    var analysis = new UnivariateAnalysis(model);
    var validation = analysis.Validate();
    if (!validation.IsValid)
    {
        throw new InvalidOperationException(
            string.Join(Environment.NewLine, validation.ValidationMessages));
    }

    await analysis.RunAsync();
    if (!analysis.IsEstimated || analysis.AnalysisResults is null)
    {
        throw new InvalidOperationException(
            "The analysis did not produce uncertainty results.");
    }

    return analysis;
}
```

This is an orchestration example, not a recommended MCMC length or convergence decision. The Bayesian MCMC chapter defines sampler configuration and acceptance gates. A production application should also observe `AnalysisCompleted`, report progress, preserve cancellation state, and persist versions and seeds.

## Scientific analysis inventory

| Domain | Analysis classes | Principal responsibility |
|---|---|---|
| Distribution screening | `FittingAnalysis` | Fit candidate families and construct point-estimate comparison outputs |
| Univariate frequency | `UnivariateAnalysis` | Bayesian parameter inference, probability ordinates, frequency results, diagnostics |
| Peaks over threshold | `PointProcessAnalysis` | Point-process model estimation and exceedance-frequency outputs |
| Multiple populations/processes | `MixtureAnalysis`, `CompetingRiskAnalysis` | Propagate component posterior realizations into combined distributions |
| Synthesis/averaging | `CompositeAnalysis` | Combine already estimated alternatives using the selected composite rule |
| Bulletin 17C | `Bulletin17CAnalysis` | Specialized EMA/GMM and uncertainty workflow for its supported parent families |
| Bivariate dependence | `BivariateAnalysis` | Marginal/dependence fitting and joint uncertainty outputs |
| Coincident frequency | `CoincidentFrequencyAnalysis` | Integrate bivariate uncertainty through a response surface |
| Rating curve | `RatingCurveAnalysis` | Fit stage-discharge model and propagate rating uncertainty |
| Time series | `ARAnalysis`, `MAAnalysis`, `ARIMAAnalysis`, `ARIMAXAnalysis` | Fit dynamics and construct forecast/training outputs |
| Spatial extremes | `SpatialGEVAnalysis` | Fit regional GEV hierarchy, dependence, and site predictions |

The table states orchestration roles only. It does not imply that every class uses the same estimator, likelihood, result layout, or serialization constructor.

## Initialization and multiple optima

Many analyses use preliminary estimates to initialize Bayesian sampling or complex optimization. This is computational scaffolding, not a second source of data. A failed initializer can prevent a run even when the mathematical posterior exists; conversely, a successful initializer does not establish global optimality or posterior convergence.

Mixture, competing-risk, spatial, nonstationary, and high-order time-series models can have symmetric modes, label switching, boundary modes, or strongly correlated parameters. An analysis chapter must identify its initialization strategy, bounds, restart behavior, and what happens when an optimizer reports a non-success status.

## Derived outputs and uncertainty

Analysis-specific post-processing can include:

- probability ordinates and plotting positions;
- return levels, frequency curves, and credible bands;
- forecasts and predictive intervals;
- posterior predictive simulations;
- model comparison criteria and averaging weights;
- influence, leverage, and prior-sensitivity components;
- serialization-ready result objects.

Each output has its own conditioning statement. A parameter credible interval describes posterior parameter uncertainty. A credible interval for a latent frequency curve propagates parameter uncertainty. A posterior predictive interval additionally includes new-event variability. These intervals are not interchangeable and must be labeled precisely.

## Validation and failure-state discipline

Before `RunAsync`, check at least:

- the concrete analysis `Validate()` result;
- model/data units, indexes, support, bounds, and priors;
- estimator settings and reproducibility controls;
- requested output probabilities and extrapolation range;
- upstream dependencies for composite or coincident analyses.

After termination, check:

- `AnalysisCompleted.Succeeded`, `Cancelled`, and `Error` where events are used;
- `IsEstimated` and required non-null result objects;
- optimizer or sampler status, not only absence of an exception;
- convergence and effective sample diagnostics for MCMC;
- invalid posterior realizations excluded during output construction;
- warnings generated by model-specific validation and diagnostics.

Do not serialize a failed analysis and later infer success merely from the presence of some result arrays. Terminal state and result completeness are part of reproducibility metadata.

## Batch execution

`BatchAnalysisRunner` coordinates multiple `IAnalysis` instances with `BatchAnalysisOptions`, per-analysis `BatchAnalysisResult`, progress, cancellation, and dependency checks. It can detect missing estimated prerequisites for composite and coincident analyses. Batch execution changes scheduling, not statistical independence: sharing data or upstream analyses still induces dependence among outputs.

For peer-review runs, record the input ordering, options, dependency graph, individual durations/statuses, and software versions. A batch-level success count must not conceal one failed life-safety analysis.

## Serialization boundary

Concrete analyses own their XML representation and reconstruction rules; neither `IAnalysis` nor `AnalysisBase` declares a universal `ToXElement`. Configuration, estimator output, and derived results may have different persistence semantics. A chapter that shows serialization must use that concrete class's actual constructor and methods and state whether stored results are trusted, recomputed, or invalidated when inputs change.

## Implementation and evidence

| Concern | Implementation source | Evidence |
|---|---|---|
| Public lifecycle | `src/RMC.BestFit/Analyses/Support/IAnalysis.cs` | lifecycle tests for concrete analyses |
| Base cancellation/events | `AnalysisBase.cs`, `AnalysisRunCompletedEventArgs.cs` | analysis support unit tests |
| Bayesian outputs | `IBayesianAnalysis.cs`, `src/RMC.BestFit/Estimation/BayesianAnalysis.cs` | Bayesian analysis unit/verification tests |
| Batch orchestration | `BatchAnalysisRunner.cs`, `BatchAnalysisOptions.cs`, `BatchAnalysisResult.cs` | batch runner unit tests |
| Compile-checked example | `src/RMC.BestFit.Tests/Documentation/Examples/AnalysisExamples.cs` | `TechnicalReferenceDocumentationTests` |

## References

<a id="ref-1"></a>[1] A. Gelman et al., *Bayesian Data Analysis*, 3rd ed. Boca Raton, FL, USA: CRC Press, 2013.

<a id="ref-2"></a>[2] A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, pp. 1413-1432, 2017. doi: 10.1007/s11222-016-9696-4.

[<- Link functions](../support/link-functions.md) | [Technical reference index](../index.md) | [Next: Distribution fitting ->](distribution-fitting.md)
