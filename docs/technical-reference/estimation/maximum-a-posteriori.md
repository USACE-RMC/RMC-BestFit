<!-- technical-reference-status: complete -->

# Maximum A Posteriori Estimation

[Estimation index](index.md) | [MLE](maximum-likelihood.md) | [Bayesian MCMC](bayesian-mcmc.md) | [Technical Reference](../index.md)

`MaximumAPosteriori` maximizes `IModel.LogLikelihood`, whose BestFit contract is the data log likelihood plus every configured prior contribution. MAP is a posterior mode, not a replacement for posterior simulation: it does not by itself provide credible intervals, tail-probability uncertainty, multimodality weights, or posterior predictive uncertainty.

## Posterior Formulation

Let the data likelihood be $L(\boldsymbol\theta;\mathbf y)$ and let the complete implemented prior density—including parameter priors, quantile priors, Jeffreys scale factors, and transformation Jacobians—be $\pi(\boldsymbol\theta)$. Bayes' rule gives

$$
p(\boldsymbol\theta\mid\mathbf y)
=\frac{L(\boldsymbol\theta;\mathbf y)\pi(\boldsymbol\theta)}
{\int_{\Theta}L(\mathbf u;\mathbf y)\pi(\mathbf u)\,d\mathbf u}. \tag{MAP.1}
$$

Because the denominator does not depend on $\boldsymbol\theta$, the MAP estimate is

$$
\widehat{\boldsymbol\theta}_{\mathrm{MAP}}
=\arg\max_{\boldsymbol\theta\in\Theta}
\left\{\ell(\boldsymbol\theta)+\log\pi(\boldsymbol\theta)\right\}. \tag{MAP.2}
$$

BestFit evaluates the objective as

$$
\texttt{Model.LogLikelihood}
=\texttt{DataLogLikelihood}+\texttt{PriorLogLikelihood}. \tag{MAP.3}
$$

After success, `MaximumLogLikelihood` equals the maximum value of (MAP.3), despite the property's likelihood-only name. It is a full log-posterior kernel. `BestParameterSet.Fitness` stores its negative under the Numerics optimizer convention.

MAP depends on parameterization: the mode of $p(\theta\mid y)$ does not generally transform into the mode of $p(g(\theta)\mid y)$. The Jacobian of a scientifically intended reparameterization therefore matters. Quantile-prior Jacobians documented in the priors chapter are part of the implemented target, whereas an arbitrary display transformation is not.

## Optimization and Failure States

The supported `OptimizationMethod` values and bounds behavior are identical to `MaximumLikelihood`, but every optimizer receives `Model.LogLikelihood` instead of `DataLogLikelihood`. The default is bounded Differential Evolution. `Brent` is restricted to one parameter; MLSL uses Nelder-Mead for local refinement.

`Estimate()` returns `true` only for `OptimizationStatus.Success`. Exceptions are caught, `Status` becomes `Failure`, and the method returns `false`. The class does not automatically retry with a second optimizer. For multimodal posteriors, compare multiple global-search runs and verify that the reported mode is not simply a narrow local peak with negligible posterior mass.

## Laplace Curvature Approximation

If `ComputeHessian` is enabled, BestFit evaluates the Hessian of the full posterior kernel at the mode:

$$
\mathbf H_{\mathrm{post}}
=\left.\frac{\partial^2}{\partial\boldsymbol\theta\partial\boldsymbol\theta^{\mathsf T}}
\{\ell(\boldsymbol\theta)+\log\pi(\boldsymbol\theta)\}
\right|_{\widehat{\boldsymbol\theta}_{\mathrm{MAP}}}. \tag{MAP.4}
$$

The returned inverse-curvature matrix is

$$
\widehat{\mathbf V}_{\mathrm{Laplace}}
=(-\mathbf H_{\mathrm{post}})^{-1}. \tag{MAP.5}
$$

Equation (MAP.5) is the covariance of a local Gaussian/Laplace approximation only when the posterior is sufficiently regular and the mode is interior. BestFit regularizes the matrix to positive definiteness. On inversion failure it returns a zero matrix after diagnostic logging, so callers must not interpret zero diagonal entries as exact posterior certainty.

## Coordinate Slices Are Not Intervals

`MaximumAPosteriori.ProfileLikelihood()` varies one coordinate while fixing all others at the MAP. `ParameterConfidenceIntervals()` finds a chi-squared cutoff on those same posterior-kernel slices. These products are neither true likelihood profiles nor Bayesian credible intervals:

- nuisance parameters are not reoptimized;
- the prior is included in the sliced objective;
- posterior probability mass is not integrated;
- the chi-squared likelihood-ratio approximation is not justified merely by replacing likelihood with posterior density.

This behavior is recorded as [TR-023](../review-findings.md#tr-023). Use MCMC marginal quantiles for implemented Bayesian credible intervals, and use a corrected MLE profile implementation for frequentist likelihood-ratio intervals.

## AIC and BIC Methods

The class exposes `GetAIC()` and `GetBIC(sampleSize)`, but they substitute the full posterior-kernel value at the MAP into the familiar formulas:

$$
\mathrm{AIC}_{\mathrm{API}}=-2\{\ell(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})+
\log\pi(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})\}+2p, \tag{MAP.6}
$$

with the analogous $p\log n$ BIC penalty. These are not conventional AIC or BIC. Normalized prior-density constants and parameterization can shift (MAP.6), even when the prior is flat over finite bounds. Do not compare models with these values. Use MLE likelihood AIC/BIC or posterior predictive criteria computed from pointwise data likelihoods. The broader criterion-label issue is tracked in [TR-011](../review-findings.md#tr-011).

## Compile-Checked API Workflow

<!-- snippet: maximum-a-posteriori-workflow -->
```csharp
private static double[] EstimateByMaximumAPosteriori(IModel model)
{
    var estimation = new MaximumAPosteriori(
        model,
        OptimizationMethod.DifferentialEvolution)
    {
        ComputeHessian = true,
        ReportFailure = false
    };

    if (!estimation.Estimate())
    {
        throw new InvalidOperationException(
            $"MAP failed with optimizer status {estimation.Status}.");
    }

    return estimation.BestParameterSet.Values.ToArray();
}
```

In practice, validate prior support and units before fitting, retain the decomposition into data and prior log contributions, compare multiple starts, and use the MAP primarily as an initializer or representative mode. For design quantiles, run the Bayesian analysis and propagate every retained posterior draw through the quantile function.

## Assumptions, Limitations, and Verification

- All prior components must define one coherent joint prior on the documented parameterization.
- Improper priors can yield a usable posterior, but their arbitrary normalizing constants prevent marginal-likelihood interpretation and complicate model comparison.
- Modes on bounds and singular curvature require scientific review.
- A local Gaussian approximation can be seriously misleading for skewed, heavy-tailed, truncated, weakly identified, or multimodal posteriors.
- Compile checking verifies the API example. Numerical parameter-recovery and posterior-comparison tests reside in the prohibited long-running Verification project and were not executed here.

Implementation symbols: `MaximumAPosteriori`, `IModel.LogLikelihood`, `IModel.DataLogLikelihood`, `IModel.PriorLogLikelihood`, `NumericalDiff.ComputeHessian`, `GetCovarianceMatrix`, `ProfileLikelihood`, `ParameterConfidenceIntervals`, `GetAIC`, and `GetBIC`.

## References

<a id="ref-1"></a>[1] T. Bayes, “An essay towards solving a problem in the doctrine of chances,” *Philosophical Transactions of the Royal Society of London*, vol. 53, pp. 370–418, 1763.

<a id="ref-2"></a>[2] L. Tierney and J. B. Kadane, “Accurate approximations for posterior moments and marginal densities,” *Journal of the American Statistical Association*, vol. 81, no. 393, pp. 82–86, 1986.

<a id="ref-3"></a>[3] A. Gelman et al., *Bayesian Data Analysis*, 3rd ed. CRC Press, 2013.

---

[Previous: MLE](maximum-likelihood.md) | [Next: GMM](generalized-method-of-moments.md)
