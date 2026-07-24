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

consistent with (GMM.4). Without a penalty, `GetGradient()` still returns $\mathbf D^{\mathsf T}\mathbf W\mathbf g_n$, one half of the derivative of (GMM.3). The minimizer is unchanged under a constant objective scaling, but an objective/gradient scale mismatch can affect BFGS line-search behavior and should be corrected or explicitly validated before claiming optimizer parity.

Penalties are generic deterministic functions. In Bulletin 17C they encode external parameter or quantile information with an $n^{-1}$-scaled half-quadratic form. `PenaltyIsRandom` determines whether the penalty Hessian also enters the sandwich meat; it does not transform the penalty into a normalized Bayesian prior.

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

The extra penalty term in (GMM.8) is intended for a random external target such as regional skew. A fixed ridge-type penalty should set `PenaltyIsRandom = false`. Both $\mathbf S$ and $\mathbf B$ are regularized before inversion. A caught covariance failure returns a zero matrix through the public API, so zero variances require failure review rather than scientific interpretation.

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
- Source verification includes GMM and Bulletin 17C cases, but the long-running Verification suite was not executed in this documentation pass.

Implementation symbols: `IGMMModel`, `GeneralizedMethodOfMoments`, `MomentConditionFunction`, `PointwiseMomentConditionFunction`, `Q`, `GetS`, `GetJacobian`, `GetCovariance`, `ProfileQ`, `Estimate`, and `PostProcess`.

## References

<a id="ref-1"></a>[1] L. P. Hansen, “Large sample properties of generalized method of moments estimators,” *Econometrica*, vol. 50, no. 4, pp. 1029–1054, 1982.

<a id="ref-2"></a>[2] W. K. Newey and D. McFadden, “Large sample estimation and hypothesis testing,” in *Handbook of Econometrics*, vol. 4, 1994, pp. 2111–2245.

<a id="ref-3"></a>[3] A. Hall, *Generalized Method of Moments*. Oxford University Press, 2005.

---

[Previous: MAP](maximum-a-posteriori.md) | [Next: Bayesian MCMC](bayesian-mcmc.md)
