<!-- technical-reference-status: complete -->

# Generalized Method of Moments

[Estimation index](index.md) | [Bulletin 17C estimation](../analysis/bulletin-17c-estimation.md) | [Diagnostics](diagnostics.md) | [Technical Reference](../index.md)

`GeneralizedMethodOfMoments` estimates an `IGMMModel` or user-supplied moment-condition delegates. It is BestFit's only moments-based estimator. It does not evaluate a data likelihood or posterior unless a particular model separately supplies one; GMM standard errors, penalties, and resampling ensembles must not be relabeled as Bayesian outputs.

## Population and Sample Moment Conditions

Let $Z_i$ denote one independent observational unit and let $\mathbf m(Z_i,\boldsymbol\theta)\in\mathbb R^q$ be a vector of estimating functions satisfying

$$
E\{\mathbf m(Z_i,\boldsymbol\theta_0)\}=\mathbf 0. \tag{GMM.1}
$$

The sample mean and its asymptotic covariance are

$$
\mathbf g_n(\boldsymbol\theta)
=\frac1n\sum_{i=1}^{n}\mathbf m(Z_i,\boldsymbol\theta),
\qquad
\mathbf S(\boldsymbol\theta_0)
=\operatorname{Var}\{\sqrt n\,\mathbf g_n(\boldsymbol\theta_0)\}. \tag{GMM.2}
$$

The `MomentConditionFunction` delegate returns both `G` and `S`; an `IGMMModel` supplies the same quantities through its scientific implementation. `PointwiseMomentConditionFunction`, when present, supports observation-level influence calculations. `SampleSize` is $n$, `NumberOfMomentConditions` is $q$, and `NumberOfParameters` is $p$.

Identification is classified as underidentified ($q<p$), just identified ($q=p$), or overidentified ($q>p$). BestFit refuses an underidentified fit unless a penalty delegate is present. The current API also refuses `OneStep` for an overidentified problem, although one-step GMM with a fixed positive-definite weight is defined in standard theory; users must select `TwoStep` or `Iterative` for such specifications.

## Objective and Penalties

With positive-definite weighting matrix $\mathbf W$, ordinary GMM minimizes

$$
Q_n(\boldsymbol\theta)
=\mathbf g_n(\boldsymbol\theta)^{\mathsf T}
\mathbf W\mathbf g_n(\boldsymbol\theta). \tag{GMM.3}
$$

This is the exact `Q` implementation when no penalty delegate exists. When a penalty $P(\boldsymbol\theta)$ is installed, the code switches to

$$
Q_{n,P}(\boldsymbol\theta)
=\frac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n
+P(\boldsymbol\theta). \tag{GMM.4}
$$

The half factor makes the implemented gradient

$$
\nabla Q_{n,P}
=\mathbf D^{\mathsf T}\mathbf W\mathbf g_n+\nabla P,
\qquad
\mathbf D=\frac{\partial\mathbf g_n}{\partial\boldsymbol\theta^{\mathsf T}}, \tag{GMM.5}
$$

consistent with (GMM.4). Without a penalty, `GetGradient()` returns $\mathbf D^{\mathsf T}\mathbf W\mathbf g_n$, one half of the derivative of (GMM.3). This is the estimating-equation direction. Multiplying it by two changes neither its roots nor the GMM point estimate. GMM covariance is computed from the estimating-equation bread and meat, not from a numerical Hessian of the scalar optimizer objective, so this constant is not propagated into covariance. When a penalty is present, equations (GMM.4) and (GMM.5) are an exact objective-gradient pair; this is the scale that combines data curvature with penalty curvature.

### Gaussian parameter penalties

For a Gaussian prior or regional estimate on one parameter,

$$
\mu\sim N(m,\tau^2),
$$

`ParameterPenalty` contributes

$$
P_\mu(\mu)=\frac{1}{2n}\frac{(\mu-m)^2}{\tau^2},
\qquad
\nabla P_\mu=\frac{\mu-m}{n\tau^2},
\qquad
H_\mu=\frac{1}{n\tau^2}. \tag{GMM.5a}
$$

Therefore `ParameterPenalty.MSE` is the Gaussian variance $\tau^2$ itself. The division by $n$ already occurs inside the penalty; callers must not divide the prior variance by $n$ again. Multiplying the complete objective (GMM.4) by $n$ gives the usual negative-log-posterior kernel,

$$
nQ_{n,P}
=\frac n2\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n
+\frac12\frac{(\mu-m)^2}{\tau^2}. \tag{GMM.5b}
$$

Bulletin 17C parameter penalties use this Gaussian information interpretation. Quantile penalties apply the same half-quadratic construction after mapping parameters to the selected quantile.

## Weighting Strategies and Optimization

The API implements:

1. `OneStep`: minimize using the initial identity or supplied $\mathbf W$;
2. `TwoStep`: fit once, set $\mathbf W=\widehat{\mathbf S}^{-1}$, and refit;
3. `Iterative`: repeatedly update $\mathbf W=\widehat{\mathbf S}^{-1}$ and refit until absolute parameter distance or relative objective change meets tolerance.

The default is iterative GMM with BFGS, at most 100 GMM passes, 2,000 function evaluations per pass, and absolute/relative tolerances of $10^{-8}$. If BFGS fails and `UseFallbackOptimizer` is true, the estimator tries Nelder-Mead and uses it for later passes. Other `OptimizationMethod` values are also supported. `IsEstimated` means a finite best parameter vector was retained after a nonfailure optimizer termination; it can be true even when `ConvergedWithinTolerance` is false. Always report `Status`, `GMMIterations`, `ConvergenceHistory`, `TotalFunctionEvaluations`, and the strict-convergence flag.

`GetS()` regularizes the moment covariance to be symmetric positive definite. Numerical Jacobians and penalty Hessians use boundary-aware finite differences when analytic delegates are absent.

## Asymptotic Covariance

Let $\mathbf H_P=\partial^2P/\partial\boldsymbol\theta\partial\boldsymbol\theta^{\mathsf T}$. BestFit defines

$$
\mathbf B=\mathbf D^{\mathsf T}\mathbf W\mathbf D+\mathbf H_P. \tag{GMM.6}
$$

The non-sandwich covariance is

$$
\widehat{\mathbf V}_{B}=\frac1n\mathbf B^{-1}. \tag{GMM.7}
$$

The sandwich form is

$$
\widehat{\mathbf V}_{S}
=\frac1n\mathbf B^{-1}\mathbf M\mathbf B^{-1},
\qquad
\mathbf M=\mathbf D^{\mathsf T}\mathbf W\mathbf S\mathbf W\mathbf D
+\mathbb I_{\mathrm{random\ penalty}}\mathbf H_P. \tag{GMM.8}
$$

For efficient weighting, $\mathbf W=\mathbf S^{-1}$. With the default Bulletin 17C external-information interpretation (`PenaltyIsRandom = true`),

$$
\mathbf M=\mathbf D^{\mathsf T}\mathbf W\mathbf D+\mathbf H_P=\mathbf B,
\qquad
\widehat{\mathbf V}_S=\frac1n\mathbf B^{-1}. \tag{GMM.8a}
$$

This is also the inverse posterior curvature for the Gaussian penalty in (GMM.5a). For an independent location block with unpenalized estimate $\widehat\mu_L$ and variance $V_L$, the prior and data precisions add:

$$
V_{\mathrm{post}}
=\left(\frac1{V_L}+\frac1{\tau^2}\right)^{-1},
\qquad
\widehat\mu_{\mathrm{post}}
=V_{\mathrm{post}}
\left(\frac{\widehat\mu_L}{V_L}+\frac m{\tau^2}\right). \tag{GMM.8b}
$$

A wide centered prior leaves both quantities effectively unchanged; a narrow centered prior leaves the location unchanged while contracting its variance; and a narrow displaced prior changes both. The Log10-Normal verification reproduces all three cases for MAP and Bulletin 17C GMM while reestimating $\sigma$ in every fit.

Setting `PenaltyIsRandom = false` instead reports the frequentist sampling covariance of a fixed regularized estimator: the penalty curvature remains in the bread but not in the meat. That quantity is not the Gaussian posterior variance in (GMM.8b). Both $\mathbf S$ and $\mathbf B$ are regularized before inversion. A caught covariance failure returns a zero matrix through the public API, so zero variances require failure review rather than scientific interpretation.

## Overidentification Statistic

In efficient unpenalized GMM, Hansen's statistic is

$$
J=n\,\mathbf g_n(\widehat{\boldsymbol\theta})^{\mathsf T}
\widehat{\mathbf S}^{-1}
\mathbf g_n(\widehat{\boldsymbol\theta})
\overset{a}{\sim}\chi^2_{q-p}. \tag{GMM.9}
$$

The current `PostProcess(computeJstat: true)` does not evaluate (GMM.9). It forms a projected residual-moment covariance divided by $n$, inverts it, and calculates $\mathbf g_n^{\mathsf T}\mathbf V^{-1}\mathbf g_n$. The projection is rank deficient in exact arithmetic, and the implementation catches some inversion failures by returning a zero matrix. Until this path is independently reconciled with Hansen's statistic, do not publish `JStat` or `JStatPval` as a standard overidentification test. This discrepancy is recorded in the review register.

## Profile-Q Products

`ProfileQ(trueProfile: true)` fixes one parameter and reoptimizes the remainder using Brent for one nuisance dimension or Nelder-Mead otherwise. Unlike the MLE/MAP coordinate-slice methods, this is structurally a profile objective. `trueProfile: false` evaluates a fixed-nuisance slice. `ProfileConfidenceIntervals` and `ProfilePercentiles` inherit the GMM quadratic/chi-squared approximation and remain asymptotic; boundaries, penalties, weak identification, and regularization can invalidate nominal coverage.

## Compile-Checked API Workflow

<!-- snippet: gmm-workflow -->
```csharp
private static GeneralizedMethodOfMoments ConfigureIterativeGmm(
    IGMMModel model)
{
    return new GeneralizedMethodOfMoments(
        model,
        OptimizationMethod.BFGS)
    {
        EstimationStrategy =
            GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
        UseFallbackOptimizer = true,
        MaxGMMIterations = 100,
        MaxFunctionEvaluations = 2_000,
        AbsoluteTolerance = 1E-8,
        RelativeTolerance = 1E-8
    };
}
```

After configuration, call `IsValid(out errors)`, `Estimate()`, inspect both `Status` and `ConvergedWithinTolerance`, and only then call `PostProcess()` if covariance is required. A practical hydrologic report should identify the observational unit, every moment, $q$, $p$, starting weight, final weight, regularization, penalty interpretation, and convergence history.

## Assumptions, Limitations, and Verification

- Equation (GMM.1) must be scientifically justified; adding moments does not guarantee identification or efficiency.
- The covariance in (GMM.2) must match dependence, censoring, grouping, and unequal information content.
- Optimal weighting is asymptotic and estimated; small-sample behavior can be poor.
- Strongly collinear moments make $\mathbf S$ and $\mathbf B$ ill-conditioned, so regularization can materially affect estimates and intervals.
- Penalized estimates require a declared fixed-versus-random interpretation.
- Focused analytical verification confirms the unpenalized estimating-gradient factor, exact penalized objective gradient, unbiased Log10-Normal moment solution, and Gaussian inverse-variance posterior mean and variance. Verification is performed one exact method at a time; no conclusion depends on executing the complete long-running suite.

Implementation symbols: `IGMMModel`, `GeneralizedMethodOfMoments`, `MomentConditionFunction`, `PointwiseMomentConditionFunction`, `Q`, `GetS`, `GetJacobian`, `GetCovariance`, `ProfileQ`, `Estimate`, and `PostProcess`.
Verification evidence: [Model Estimation and Diagnostics Verification](../../verification/model-estimation.md#gmm-objective-gradient-and-covariance-scaling).

## References

<a id="ref-1"></a>[1] L. P. Hansen, “Large sample properties of generalized method of moments estimators,” *Econometrica*, vol. 50, no. 4, pp. 1029–1054, 1982.

<a id="ref-2"></a>[2] W. K. Newey and D. McFadden, “Large sample estimation and hypothesis testing,” in *Handbook of Econometrics*, vol. 4, 1994, pp. 2111–2245.

<a id="ref-3"></a>[3] A. Hall, *Generalized Method of Moments*. Oxford University Press, 2005.

---

[Previous: MAP](maximum-a-posteriori.md) | [Next: Bayesian MCMC](bayesian-mcmc.md)
