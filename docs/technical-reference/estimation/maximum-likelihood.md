<!-- technical-reference-status: complete -->

# Maximum-Likelihood Estimation

[Estimation index](index.md) | [MAP](maximum-a-posteriori.md) | [Model comparison](model-comparison.md) | [Technical Reference](../index.md)

`MaximumLikelihood` estimates an `IModel` by maximizing `DataLogLikelihood` only. Parameter priors, quantile priors, Jeffreys scale terms, and other `PriorLogLikelihood` contributions are excluded. This distinction is essential: MLE provides a likelihood-based point estimate and local/asymptotic uncertainty, not a posterior distribution.

## Notation and Objective

Let independent observation-information units be indexed by $i=1,\ldots,n$, let $y_i$ denote the observed exact value or censoring/measurement record, and let $L_i(\boldsymbol\theta)$ be its implemented contribution. The mixed-data chapter defines exact, left-censored, right-censored, interval-censored, threshold-count, low-outlier, and uncertain-observation contributions. The data likelihood and log likelihood are

$$
L(\boldsymbol\theta;\mathbf y)=\prod_{i=1}^{n}L_i(\boldsymbol\theta),
\qquad
\ell(\boldsymbol\theta)=\sum_{i=1}^{n}\log L_i(\boldsymbol\theta). \tag{MLE.1}
$$

For a model with parameter vector $\boldsymbol\theta\in\Theta$ and componentwise API bounds $\mathbf a\le\boldsymbol\theta\le\mathbf b$, BestFit computes

$$
\widehat{\boldsymbol\theta}_{\mathrm{ML}}
=\underset{\boldsymbol\theta\in\Theta\cap[\mathbf a,\mathbf b]}{\arg\max}\;
\ell(\boldsymbol\theta). \tag{MLE.2}
$$

`BestParameterSet.Fitness` follows the Numerics optimizer convention and stores the minimized sign of the maximized objective. Consequently, after a successful fit,

$$
\texttt{MaximumLogLikelihood}
=-\texttt{BestParameterSet.Fitness}
=\ell(\widehat{\boldsymbol\theta}_{\mathrm{ML}}). \tag{MLE.3}
$$

The estimator copies initial values and bounds from `Model.Parameters` when it constructs its Numerics optimizer. Changing model parameter values or bounds after constructing `MaximumLikelihood` does not rebuild that optimizer unless the estimator configuration itself triggers setup; construct the estimator after finalizing the model.

## Optimization Algorithms

`OptimizationMethod` selects one of six Numerics algorithms:

| API value | Numerical role | Important restriction |
|---|---|---|
| `Brent` | bounded one-dimensional search | exactly one parameter |
| `BFGS` | bounded quasi-Newton local search | sensitive to starting values and nonsmooth likelihoods |
| `NelderMead` | derivative-free simplex local search | may be slow or scale-sensitive |
| `Powell` | derivative-free direction-set search | local optimum is possible |
| `DifferentialEvolution` | bounded population/global search | stochastic and evaluation-intensive |
| `MultilevelSingleLinkage` | MLSL global search with Nelder-Mead local refinement | stochastic and evaluation-intensive |

The default is `DifferentialEvolution`. Every algorithm receives `Model.DataLogLikelihood`; `Brent` receives a scalar wrapper. `ReportFailure` is passed to the optimizer, traces are disabled, and the optimizer's own Hessian calculation is disabled. `Estimate()` catches optimizer exceptions, records `OptimizationStatus.Failure`, and returns `false`. It calls a fit successful only when the Numerics status is exactly `Success`; a finite best point at another termination status is not published as an estimate.

Global algorithms reduce, but do not eliminate, multiple-optimum risk. For mixture, competing-risk, nonstationary, and strongly correlated models, use multiple seeds or starts and compare both objective values and fitted distribution behavior. A single `Success` status is not evidence of global optimality.

## Curvature and Asymptotic Covariance

When `ComputeHessian` is true, BestFit numerically differentiates the data log likelihood at the optimum using its adaptive finite-difference helper. With observed information

$$
\mathbf H(\widehat{\boldsymbol\theta})
=\left.\frac{\partial^2\ell(\boldsymbol\theta)}
{\partial\boldsymbol\theta\,\partial\boldsymbol\theta^{\mathsf T}}
\right|_{\widehat{\boldsymbol\theta}},
\qquad
\mathbf J_{\mathrm{obs}}=-\mathbf H, \tag{MLE.4}
$$

the local covariance approximation is

$$
\widehat{\operatorname{Var}}(\widehat{\boldsymbol\theta})
=\mathbf J_{\mathrm{obs}}^{-1}. \tag{MLE.5}
$$

`GetCovarianceMatrix()` symmetrizes/regularizes the inverse to be positive definite. If inversion or regularization throws, the current API writes a diagnostic message and returns a zero matrix. A zero standard error is therefore not automatically evidence of certainty; inspect the Hessian path and logs. This failure-signaling concern is tracked in the [review findings](../review-findings.md).

For pointwise score $\mathbf s_i=\partial\log L_i/\partial\boldsymbol\theta$, the implemented sandwich estimator is

$$
\widehat{\mathbf V}_{\mathrm{sand}}
=\mathbf J_{\mathrm{obs}}^{-1}
\left(\sum_i\mathbf s_i\mathbf s_i^{\mathsf T}\right)
\mathbf J_{\mathrm{obs}}^{-1}. \tag{MLE.6}
$$

BestFit obtains each score by central finite differences of `PointwiseDataLogLikelihood`. Equation (MLE.6) is a model-misspecification-robust large-sample approximation, not a correction for serial dependence or clustered observations. It assumes the pointwise units are the independent estimating units. Threshold counts and grouped records must therefore be interpreted using the model's pointwise decomposition rather than as an arbitrary row count.

## Likelihood-Ratio Intervals and a Naming Limitation

For a genuine profile likelihood, nuisance parameters must be reoptimized:

$$
\ell_p(\theta_j)=\max_{\boldsymbol\theta_{-j}}
\ell(\theta_j,\boldsymbol\theta_{-j}),
\qquad
2\{\ell(\widehat{\boldsymbol\theta})-\ell_p(\theta_j)\}
\overset{a}{\sim}\chi^2_1. \tag{MLE.7}
$$

The current `ProfileLikelihood()` and `ParameterConfidenceIntervals()` methods do **not** calculate (MLE.7). They vary $\theta_j$ while holding every nuisance parameter at its joint optimum. The resulting conditional coordinate slice can differ materially from a profile when parameters are correlated. This is [TR-023](../review-findings.md#tr-023); do not label those intervals as peer-review-grade profile-likelihood intervals.

## Information Criteria

For $p=\texttt{NumberOfParameters}$, BestFit returns

$$
\mathrm{AIC}=-2\ell(\widehat{\boldsymbol\theta})+2p,
\qquad
\mathrm{BIC}=-2\ell(\widehat{\boldsymbol\theta})+p\log n. \tag{MLE.8}
$$

`GetBIC(sampleSize)` requires the caller to supply $n$. Use the number of independent observational units represented by the likelihood, not blindly the number of table rows. Compare AIC/BIC only across models fitted to the same response data, observation process, and likelihood constants. Neither criterion is an absolute goodness-of-fit test, and neither propagates parameter uncertainty into flood quantiles.

## Compile-Checked API Workflow

<!-- snippet: maximum-likelihood-workflow -->
```csharp
private static double[] EstimateByMaximumLikelihood(IModel model)
{
    var estimation = new MaximumLikelihood(
        model,
        OptimizationMethod.DifferentialEvolution)
    {
        ComputeHessian = true,
        ReportFailure = false
    };

    if (!estimation.Estimate())
    {
        throw new InvalidOperationException(
            $"MLE failed with optimizer status {estimation.Status}.");
    }

    return estimation.BestParameterSet.Values.ToArray();
}
```

This helper deliberately checks the Boolean result and `Status`. In an engineering workflow, also retain the objective value, optimizer method, bounds, starting values, function-evaluation count, software versions, and any covariance regularization. For high-consequence extrapolation, compare the fitted tail with empirical plotting positions and use Bayesian/posterior or calibrated frequentist uncertainty rather than treating the MLE as known.

## Assumptions, Limitations, and Verification

- The likelihood must correctly represent censoring, measurement error, threshold exposure, dependence, and transformation Jacobians.
- Standard curvature and sandwich results are asymptotic and can be unreliable at parameter boundaries, with weak identification, or for short flood records.
- Finite bounds can determine the optimum. A fitted value on a bound requires scientific review, not merely optimizer acceptance.
- Numerical Hessians are sensitive to scale, flat regions, discontinuities, and finite-difference steps.
- The compile-checked example verifies API conformance only. Computational verification belongs in `RMC.BestFit.Verification`; that suite was not run during this documentation pass.

Implementation symbols: `MaximumLikelihood`, `OptimizationMethod`, `IModel.DataLogLikelihood`, `IModel.PointwiseDataLogLikelihood`, `NumericalDiff.ComputeHessian`, `GetCovarianceMatrix`, `GetSandwichCovarianceMatrix`, `GetAIC`, and `GetBIC`.

## References

<a id="ref-1"></a>[1] R. A. Fisher, “On the mathematical foundations of theoretical statistics,” *Philosophical Transactions of the Royal Society A*, vol. 222, pp. 309–368, 1922.

<a id="ref-2"></a>[2] H. Akaike, “A new look at the statistical model identification,” *IEEE Transactions on Automatic Control*, vol. 19, no. 6, pp. 716–723, 1974.

<a id="ref-3"></a>[3] G. Schwarz, “Estimating the dimension of a model,” *The Annals of Statistics*, vol. 6, no. 2, pp. 461–464, 1978.

<a id="ref-4"></a>[4] H. White, “Maximum likelihood estimation of misspecified models,” *Econometrica*, vol. 50, no. 1, pp. 1–25, 1982.

---

[Estimation index](index.md) | [Next: MAP](maximum-a-posteriori.md)
