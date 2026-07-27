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

Identification is classified as underidentified ($q<p$), just identified ($q=p$), or overidentified ($q>p$). BestFit refuses an underidentified fit unless a penalty delegate is present. `OneStep` supports just-identified and overidentified systems using the initial identity or caller-supplied fixed weighting matrix; `TwoStep` and `Iterative` estimate an efficient weight from the moment covariance.

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

Setting `PenaltyIsRandom = false` instead reports the frequentist sampling covariance of a fixed regularized estimator: the penalty curvature remains in the bread but not in the meat. That quantity is not the Gaussian posterior variance in (GMM.8b). Both $\mathbf S$ and $\mathbf B$ are regularized before inversion. `TryGetCovariance(parameters, sandwich, out covariance)` returns `false` when a finite covariance with positive diagonal variances cannot be produced; its zero-valued out placeholder is not an uncertainty estimate. `GetCovariance(...)`, `GetCovarianceMatrix()`, and downstream paths that require covariance throw `InvalidOperationException` for the same failure. `CovarianceStatus` records `Available`, materially `Regularized`, or `Failed`, and `CovarianceDiagnostic` supplies the corresponding explanation.

Fixed-weight one-step and efficient two-step covariance are externally verified against R `gmm` 1.9.1 and direct analytical reconstruction of (GMM.8). `OneStep` recomputes $\mathbf S$ at the fitted parameters but retains the configured fixed $\mathbf W$ in both bread and meat; the identity-weight fixture gives variance $0.145652392138090$. `TwoStep` and `Iterative` use $\mathbf S^{-1}$ recomputed at the final fitted parameters for covariance; the two-step fixture gives $0.132600447299456$. Both BestFit results agree within $10^{-8}$. The R generator uses `vcov="iid"` for the arbitrary fixed weight; `vcov="TrueFixed"` would instead assert that the supplied matrix is already the inverse moment covariance.

## Fit, Variance, and Combined Influence

Observation diagnostics use the pointwise estimating-equation score

$$
\mathbf e_i=\mathbf D^{\mathsf T}\mathbf W\mathbf g_i. \tag{GMM.8c}
$$

Cook fit influence and variance influence are

$$
D_i=\frac{\mathbf e_i^{\mathsf T}\boldsymbol\Sigma_Q\mathbf e_i}{n^2p},
\qquad
V_i=\frac{|\mathbf e_i^{\mathsf T}\mathbf B^{-1}\mathbf e_i|}{np}, \tag{GMM.8d}
$$

where $\mathbf B$ is (GMM.6) and

$$
\boldsymbol\Sigma_Q=
\left\{\nabla^2\left(
\frac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n+P
\right)\right\}^{-1}. \tag{GMM.8e}
$$

The half-quadratic diagnostic objective is used whether or not a penalty exists. That fixed convention keeps the numerical Hessian on the same scale as the estimating score and bread; enabling an effectively flat penalty cannot double or halve observation Cook values. It is isolated to diagnostics and does not change `Q`, `GetGradient`, estimation, covariance, or penalty propagation.

Penalty fit influence uses the penalty score with $\boldsymbol\Sigma_Q$. Penalty variance influence uses the finite log generalized-variance change after removing that penalty. `LeverageDiagnostics.Leverage` is $D+V$, an estimator-specific combined ranking index. It is not a hat-matrix diagonal and is not expected to sum to $p$.

For the seven-point Log10-Normal moment fixture, R `gmm` 1.9.1 gives aggregate observation values $\sum D_i=0.0880102040816327$ and $\sum V_i=0.616071428571429$. BestFit reproduces every pointwise and aggregate value within $10^{-5}$. Wide-centered, narrow-centered, and narrow-shifted quadratic penalties respectively show negligible influence, variance-only influence, and both fit and variance influence. A fixed centered penalty contributes less variance influence as sample size increases.

See [Observation, Prior, and Leverage Diagnostics](influence-diagnostics.md#gmm-influence) for interpretation and UI behavior.

## Overidentification Statistic

In efficient unpenalized GMM, Hansen's statistic is

$$
J=n\,\mathbf g_n(\widehat{\boldsymbol\theta})^{\mathsf T}
\widehat{\mathbf S}^{-1}
\mathbf g_n(\widehat{\boldsymbol\theta})
\overset{a}{\sim}\chi^2_{q-p}. \tag{GMM.9}
$$

`PostProcess(computeJstat: true)` evaluates (GMM.9) with the unpenalized moment objective and the weighting matrix selected by the completed fit. It populates `JStat` and `JStatPval` only for unpenalized overidentified `TwoStep` or `Iterative` fits. A generic fixed-weight `OneStep` fit and any penalized fit leave both fields as `NaN`, because those cases do not automatically carry the efficient-weight Hansen chi-squared interpretation. The deterministic R `gmm` 1.9.1 fixture gives $J=10.0755078518454$ and $p=0.00150253202968641$; BestFit agrees within the declared oracle tolerances.

## Profile-Q Products

`ProfileQ(trueProfile: true)` fixes one parameter and reoptimizes the remainder using Brent for one nuisance dimension or Nelder-Mead otherwise. Like the corrected MLE and MAP methods, this is structurally a profile objective; nuisance parameters are not held at their joint fit. `trueProfile: false` evaluates a fixed-nuisance slice. `ProfileConfidenceIntervals` and `ProfilePercentiles` inherit the GMM quadratic/chi-squared approximation and remain asymptotic; boundaries, penalties, weak identification, and regularization can invalidate nominal coverage.

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

Implementation symbols: `IGMMModel`, `GeneralizedMethodOfMoments`, `CovarianceComputationStatus`, `MomentConditionFunction`, `PointwiseMomentConditionFunction`, `Q`, `GetS`, `GetJacobian`, `TryGetCovariance`, `GetCovariance`, `ProfileQ`, `Estimate`, and `PostProcess`.
Verification evidence: [GMM objective and covariance scaling](../../verification/model-estimation.md#gmm-objective-gradient-and-covariance-scaling) and [GMM specification/covariance tests](../../verification/model-estimation.md#gmm-specification-covariance-and-legacy-influence-verification).

## References

<a id="ref-1"></a>[1] L. P. Hansen, “Large sample properties of generalized method of moments estimators,” *Econometrica*, vol. 50, no. 4, pp. 1029–1054, 1982.

<a id="ref-2"></a>[2] W. K. Newey and D. McFadden, “Large sample estimation and hypothesis testing,” in *Handbook of Econometrics*, vol. 4, 1994, pp. 2111–2245.

<a id="ref-3"></a>[3] A. Hall, *Generalized Method of Moments*. Oxford University Press, 2005.

---

[Previous: MAP](maximum-a-posteriori.md) | [Next: Bayesian MCMC](bayesian-mcmc.md)
