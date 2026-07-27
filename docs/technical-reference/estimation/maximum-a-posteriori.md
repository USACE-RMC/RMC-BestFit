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

Equation (MAP.5) is the covariance of a local Gaussian/Laplace approximation only when the posterior is sufficiently regular and the mode is interior. `TryGetCovarianceMatrix(out covariance)` returns `false` when curvature is unavailable or cannot produce finite positive variances; its zero-valued out placeholder is not an uncertainty estimate. `GetCovarianceMatrix()` and dependent standard-error and influence methods throw `InvalidOperationException` for that failure. `CovarianceStatus` distinguishes `NotComputed`, `Available`, `Regularized`, and `Failed`, and `CovarianceDiagnostic` records a repair or failure. A positive-definite repair is returned only after validation and is reported as `Regularized`.

## Profile Posterior Kernel and Interval Interpretation

`MaximumAPosteriori.ProfileLikelihood()` fixes one parameter and reoptimizes every free nuisance parameter against the complete posterior kernel in (MAP.3). Profiling proceeds outward from the fitted mode and warm-starts each nuisance optimization from the preceding solution. `ParameterConfidenceIntervals()` applies its chi-squared cutoff to the same nuisance-optimized posterior profile.

These methods now preserve parameter correlation, but they are not Bayesian credible intervals: the prior is included, posterior probability mass is not integrated, and the chi-squared likelihood-ratio calibration is only a local asymptotic convention for this posterior-kernel profile. Use MCMC marginal quantiles for Bayesian credible intervals. With constant flat priors, the profiled posterior differs from the MLE data-likelihood profile only by an additive constant and the two interval calculations agree apart from numerical error.

The correlated-quadratic verification under [TR-023](../review-findings.md#tr-023) checks every profile ordinate and the 90% interval against R `bbmle` and a closed-form nuisance optimum. A second analytical fixture confirms that an informative nuisance prior participates in the reoptimization.

## AIC and BIC Methods

`GetAIC()` and `GetBIC(sampleSize)` evaluate the data log likelihood at the MAP parameter vector:

$$
\mathrm{AIC}_{\mathrm{MAP}}=-2\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})+2p,
\qquad
\mathrm{BIC}_{\mathrm{MAP}}=-2\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})+p\log n. \tag{MAP.6}
$$

Parameter-prior, Jeffreys, and quantile-prior log densities are excluded from both criteria. This prevents normalized prior constants and prior parameterization from directly shifting the reported values.

When every active prior is constant over the relevant parameter region, the MAP and constrained MLE coincide. Equation (MAP.6) is then the same likelihood criterion used by `MaximumLikelihood`, apart from numerical error in locating the mode. `UseDefaultFlatPriors=true` is not sufficient by itself if a Jeffreys scale term, quantile prior, or another nonconstant prior remains active.

With informative or otherwise nonconstant priors, the MAP generally differs from the MLE. The reported value remains the data-likelihood score at that prior-influenced point, but it is not conventional AIC or BIC because the classical penalty does not account for the prior. Use DIC, WAIC, or verified PSIS-LOO for Bayesian model comparison, subject to their documented assumptions and diagnostics.

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

Implementation symbols: `MaximumAPosteriori`, `CovarianceComputationStatus`, `IModel.LogLikelihood`, `IModel.DataLogLikelihood`, `IModel.PriorLogLikelihood`, `NumericalDiff.ComputeHessian`, `TryGetCovarianceMatrix`, `GetCovarianceMatrix`, `ProfileLikelihood`, `ParameterConfidenceIntervals`, `GetAIC`, and `GetBIC`.

## References

<a id="ref-1"></a>[1] T. Bayes, “An essay towards solving a problem in the doctrine of chances,” *Philosophical Transactions of the Royal Society of London*, vol. 53, pp. 370–418, 1763.

<a id="ref-2"></a>[2] L. Tierney and J. B. Kadane, “Accurate approximations for posterior moments and marginal densities,” *Journal of the American Statistical Association*, vol. 81, no. 393, pp. 82–86, 1986.

<a id="ref-3"></a>[3] A. Gelman et al., *Bayesian Data Analysis*, 3rd ed. CRC Press, 2013.

---

[Previous: MLE](maximum-likelihood.md) | [Next: GMM](generalized-method-of-moments.md)
