<!-- technical-reference-status: complete -->

# Bulletin 17C Estimation: Expected Moments as Penalized GMM

[Bulletin 17C overview](bulletin-17c.md) | [Technical Reference](../index.md) | [Next: uncertainty and diagnostics](bulletin-17c-uncertainty.md)

`Bulletin17CDistribution` implements a stationary expected-moments estimator through the `IGMMModel` contract. It is not an `IModel`: the class exposes moment conditions, their covariance, optional penalties, parameter bounds, and simulation, but it exposes no data log likelihood, prior log density, posterior, or pointwise log likelihood. Consequently, `Bulletin17CAnalysis` is a frequentist GMM workflow even though it reuses a `BayesianAnalysis` object as an uncertainty-results container.

## Applicability and Supported Parents

Let $X$ denote annual peak discharge in the units supplied by the data frame. The transformed variable used by the estimating equations is

$$
Y=T(X)=
\begin{cases}
\log_{10}X, & \text{Log-Normal or Log-Pearson Type III},\\
X, & \text{otherwise}.
\end{cases} \tag{1}
$$

The six supported parent families and their natural parameter vectors are:

| `UnivariateDistributionType` | Moment-space parent | $\boldsymbol\theta$ | Conditions |
|---|---|---|---|
| `Exponential` | Exponential | location, scale | positive scale |
| `GammaDistribution` | Gamma | scale, shape | both positive |
| `LogNormal` | Normal for $Y=\log_{10}X$ | mean, standard deviation | positive standard deviation and $X>0$ |
| `LogPearsonTypeIII` | Pearson III for $Y=\log_{10}X$ | mean, standard deviation, skewness | positive standard deviation and $X>0$ |
| `Normal` | Normal | mean, standard deviation | positive standard deviation |
| `PearsonTypeIII` | Pearson III | mean, standard deviation, skewness | valid Pearson III support |

The implementation rejects all other distribution types and always reports `IsNonstationary == false`. Bulletin 17C practice ordinarily uses LP3; the other parents are BestFit extensions and should not be described as Bulletin 17C-prescribed alternatives without separate justification [1].

## Data-Generating Distribution and Likelihood Status

For a selected parent $F_Y(y\mid\boldsymbol\theta)$, the conceptual data-generating model is independent annual values

$$
Y_i\mid\boldsymbol\theta\sim F_Y(\cdot\mid\boldsymbol\theta). \tag{2}
$$

Under an ordinary observed-data likelihood, an exact transformed value would contribute $f_Y(y_i\mid\boldsymbol\theta)$; an interval $A_i=[a_i,b_i]$ would contribute $F_Y(b_i)-F_Y(a_i)$; a left-censored value would contribute $F_Y(b_i)$; a right-censored value would contribute $1-F_Y(a_i)$; and an uncertain observation with measurement density $m_i$ would contribute

$$
\int f_Y(y\mid\boldsymbol\theta)m_i(y)\,dy. \tag{3}
$$

Threshold counts would add the corresponding powers of the below- and above-threshold probabilities, up to parameter-independent combinatorial constants. Equation (3) describes the likelihood that a likelihood-based analysis could use. **The Bulletin 17C implementation does not evaluate or optimize this likelihood.** It replaces likelihood contributions with conditional moment information. Therefore AIC, BIC, DIC, WAIC, PSIS-LOO, and Bayesian posterior probabilities are not defined by this model.

## Expected-Moment Conditions

Write the first three central features of the parent as

$$
\boldsymbol\tau(\boldsymbol\theta)
=\left(\mu,\sigma^2,\mu_3\right)^\mathsf T,
\qquad
\mu_3=\gamma\sigma^3, \tag{4}
$$

truncated to the number $q$ of parent parameters. In the implemented families, $q=p$: two conditions for the two-parameter parents and three for Pearson III/LP3. Thus the unpenalized specification is just identified.

For information set $A_i$, define

$$
\mathbf h_i(\boldsymbol\theta)=
\begin{bmatrix}
E(Y_i\mid A_i,\boldsymbol\theta)\\
E\{(Y_i-\mu)^2\mid A_i,\boldsymbol\theta\}\\
E\{(Y_i-\mu)^3\mid A_i,\boldsymbol\theta\}
\end{bmatrix},
\qquad
\mathbf g_i=\mathbf h_i-\boldsymbol\tau. \tag{5}
$$

For an exact non-low-outlier value $y_i$, BestFit instead uses the finite-sample-corrected vector

$$
\mathbf h_i=
\begin{bmatrix}
y_i\\
c_2(y_i-\mu)^2\\
c_3(y_i-\mu)^3
\end{bmatrix},
\quad
c_2=\frac{N_s}{N_s-1},
\quad
c_3=\frac{N_s^2}{(N_s-1)(N_s-2)}, \tag{6}
$$

where $N_s$ counts exact observations not flagged as low outliers. A correction is set to one when its denominator is unavailable. The sample estimating vector is

$$
\overline{\mathbf g}_n(\boldsymbol\theta)
=\frac{1}{n}\sum_{i=1}^{n}\mathbf g_i(\boldsymbol\theta), \tag{7}
$$

where `DataFrame.TotalRecordLength()` supplies $n$ and threshold counts expand by their counts.

### Observation-specific construction

- **Low outliers.** All flagged exact observations are represented as one repeated left-censored group from the numerical lower support to `LowOutlierThreshold`.
- **Interval observations.** Numerics `ConditionalMoments(lower, upper)` supplies the conditional raw/central moments in equation (5).
- **Perception-threshold counts.** `NumberBelow` repeats the left-censored condition and `NumberAbove` repeats the right-censored condition.
- **Uncertain observations.** Twenty-point Gauss-Legendre quadrature integrates equation (5)'s moment functions over the supplied measurement-error distribution. Infinite supports are truncated to its $10^{-8}$ and $1-10^{-8}$ quantiles and normalized by retained measurement mass. This is measurement-distribution averaging of estimating functions, not the convolution likelihood in equation (3).

Conditional integrations are clipped to parent-distribution support approximated by inverse CDFs at machine-epsilon probabilities. Invalid parameters or nonfinite moments return a `double.MaxValue` estimating vector and a zero covariance accumulator so the optimizer rejects the point.

## Moment Covariance and Weighting

The implementation forms

$$
\widehat{\mathbf S}(\boldsymbol\theta)
=\frac1n\sum_i \widehat{E}(\mathbf g_i\mathbf g_i^\mathsf T)
-\overline{\mathbf g}_n\overline{\mathbf g}_n^\mathsf T. \tag{8}
$$

For uncensored exact data with no low outliers, closed-form fourth through sixth central moments are used for Normal, Gamma, and Pearson III covariance terms. For Pearson III,

$$
\begin{aligned}
\mu_4&=\sigma^4\left(3+\tfrac32\gamma^2\right),\\
\mu_5&=\sigma^5\gamma(10+3\gamma^2),\\
\mu_6&=\sigma^6\left(15+\tfrac{65}{2}\gamma^2+\tfrac{15}{2}\gamma^4\right).
\end{aligned} \tag{9}
$$

When low outliers are present, that model-based path is disabled globally. Censored and interval groups use the approximation

$$
E(\mathbf g_i\mathbf g_i^\mathsf T\mid A_i)
\approx E(\mathbf g_i\mid A_i)E(\mathbf g_i\mid A_i)^\mathsf T, \tag{10}
$$

not the full conditional second moment. Uncertain rows add an integrated measurement-error second-moment matrix, so within-measurement uncertainty contributes to $\widehat{\mathbf S}$. Before inversion, the estimator regularizes $\widehat{\mathbf S}$ to be symmetric positive definite.

## Penalized GMM Objective

With weighting matrix $\mathbf W$, unpenalized `GeneralizedMethodOfMoments.Q` is

$$
Q_0(\boldsymbol\eta)
=\overline{\mathbf g}_n(\boldsymbol\eta)^\mathsf T
\mathbf W\overline{\mathbf g}_n(\boldsymbol\eta), \tag{11}
$$

where $\boldsymbol\eta$ is the optimizer/link-space vector and the model inverse-links it before calculating moments. When at least one penalty is installed, the code uses the half-quadratic convention

$$
Q_P(\boldsymbol\eta)
=\frac12\overline{\mathbf g}_n^\mathsf T\mathbf W\overline{\mathbf g}_n
+P(\boldsymbol\theta). \tag{12}
$$

For parameter information with target $a_j$ and mean-squared error $v_j$, an enabled natural-space penalty is

$$
P_j(\theta_j)=\frac{(\theta_j-a_j)^2}{2v_j n}. \tag{13}
$$

If `ParameterPenalty.UseLog` is true, the implementation applies the delta-method log form

$$
P_j(\theta_j)=
\frac{\{\log\theta_j-\log a_j\}^2}
{2(v_j/a_j^2)n}. \tag{14}
$$

For a quantile target $a_r$ at annual exceedance probability $\alpha_r$,

$$
P_r(\boldsymbol\theta)=
\frac{\{z_r(\boldsymbol\theta)-a_r\}^2}{2v_r n},
\quad
z_r=F_X^{-1}(1-\alpha_r\mid\boldsymbol\theta), \tag{15}
$$

with both $z_r$ and the configured target interpreted in base-10 log space when `UseLog10` is true. These are quadratic external-information penalties, not normalized prior densities. Calling them priors or deriving a posterior from equations (12)--(15) would overstate the implementation.

## Initialization and Optimization

`Bulletin17CAnalysis.RunAsync` first processes threshold series, installs `LinkController.ForLocationScaleShape()`, initializes parameters, and constructs the deterministic penalty delegate. Initial values and bounds come from the Numerics parent distribution. If low outliers or threshold records exist, `GetNonparametricMomentsROS(useLog10)` supplies a regression-on-order-statistics starting point, which the parent converts to parameters. An out-of-bounds or failed initial value is replaced by the midpoint of its bounds.

`GeneralizedMethodOfMoments` defaults to iterative GMM:

1. start with identity $\mathbf W$ unless a matrix is supplied;
2. minimize the current objective using BFGS, with automatic Nelder-Mead fallback;
3. update $\mathbf S$ and $\mathbf W=\mathbf S^{-1}$;
4. repeat until absolute parameter distance or relative objective change meets its configured tolerance, or the iteration limit is reached.

One-step and two-step strategies also exist on the estimator, but `Bulletin17CAnalysis` constructs a fresh estimator and does not expose a pre-run hook for changing those defaults. The model provides no analytical Jacobian, so GMM differentiates the mean estimating equations numerically. `Estimate()` can return a usable parameter set without strict optimizer convergence; engineering workflows must inspect `GMM.Status`, the objective/convergence history, covariance condition, and report diagnostics.

## Identification, Assumptions, and Failure Modes

- Because $q=p$, ordinary Hansen over-identification testing is unavailable; enabled penalties add information but do not create independently testable sample moments.
- Annual observations and record-information groups are treated as independent contributions to the estimating equations.
- Censoring and perception thresholds must describe the observation process. Incorrect thresholds create systematic conditional-moment bias.
- Equation (10) understates the within-interval contribution relative to a full conditional covariance; its effect should be examined for heavily censored records.
- ROS initialization assists numerical convergence but is not the final estimator.
- The specialized estimator is stationary. Trends, covariates, and generic Bayesian priors require a different model.
- LP3 parameters are moments of $\log_{10}X$; exponentiating a mean or interval is not interchangeable with taking a discharge-space expectation.

## Verification and Traceability

The prohibited long-running Verification project contains parameter assertions for all seven Bulletin 17C worked examples, including systematic-only, low-outlier, broken-record, historical, crest-stage threshold, combined historical/outlier, and paleoflood cases. Those source assertions use an absolute parameter tolerance of $10^{-3}$ for the published LP3 examples. Separate source tests cover covariance for six parents, external-information penalties, uncertain-data variants, pointwise/aggregate moment consistency, and coverage experiments. This documentation pass inspected those assertions but did **not** execute `RMC.BestFit.Verification`; no new numerical validation claim is made here.

The repository's legacy “Comparison with EMA” PDF evaluates the earlier Bayesian likelihood workflow against EMA and expressly says the comparison does not validate either method. It is useful historical context but is not validation evidence for the current specialized GMM implementation. This evidence gap is recorded in the review-findings register.

Implementation symbols: `Bulletin17CDistribution.MomentConditions`, `PointwiseMomentConditions`, `SetPenaltyFunction`, `SetRandomPenaltyFunction`, `GeneralizedMethodOfMoments.Q`, `GetS`, `GetJacobian`, and `Estimate`.

## References

<a id="ref-1"></a>[1] J. F. England, Jr. et al., *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*, U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 2019. doi: 10.3133/tm4B5.

<a id="ref-2"></a>[2] T. A. Cohn, W. L. Lane, and W. G. Baier, “An algorithm for computing moments-based flood quantile estimates when historical flood information is available,” *Water Resources Research*, vol. 33, no. 9, pp. 2089–2096, 1997.

<a id="ref-3"></a>[3] L. P. Hansen, “Large sample properties of generalized method of moments estimators,” *Econometrica*, vol. 50, no. 4, pp. 1029–1054, 1982.

<a id="ref-4"></a>[4] W. K. Newey and D. McFadden, “Large sample estimation and hypothesis testing,” in *Handbook of Econometrics*, vol. 4, 1994, pp. 2111–2245.

---

[Bulletin 17C overview](bulletin-17c.md) | [Next: uncertainty and diagnostics](bulletin-17c-uncertainty.md)
