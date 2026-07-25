# Model Estimation and Diagnostics Verification

## Status

Phase 2 is active. The Log10-Normal MLE/MAP/GMM baseline, Gaussian prior and quadratic-penalty equivalence, and GMM objective/covariance scaling are verified. Observation influence, leverage, model-comparison criteria, profile likelihood, and overidentified GMM remain in progress and are not covered by the conclusions below.

## Log10-Normal fixture

The deterministic exact-data fixture is defined in base-10 log space by

$$
x_i\in\{1.1,1.4,1.7,2.0,2.3,2.6,2.9\},
\qquad y_i=10^{x_i}. \tag{ME.1}
$$

It has $n=7$, $\bar x=2$, and $\sum_i(x_i-\bar x)^2=2.52$. The committed oracle is [log10-normal-estimation-equivalence.json](../../verification/data/model-estimation/log10-normal-estimation-equivalence.json), and the executable evidence is [Log10NormalEstimationEquivalenceTests.cs](../../src/RMC.BestFit.Verification/ModelEstimation/Log10NormalEstimationEquivalenceTests.cs).

### Estimator baselines

For exact Log10-Normal likelihood inference,

$$
\widehat\mu_{\mathrm{MLE}}=\bar x=2,
\qquad
\widehat\sigma^2_{\mathrm{MLE}}
=\frac1n\sum_i(x_i-\bar x)^2=0.36. \tag{ME.2}
$$

The flat-prior MAP reproduces this MLE. Bulletin 17C GMM deliberately uses the Bessel-corrected second moment,

$$
\widehat\sigma^2_{\mathrm{GMM}}
=\frac1{n-1}\sum_i(x_i-\bar x)^2=0.42. \tag{ME.3}
$$

Thus MAP and GMM agree on $\mu$ and on the information-combination rule, while retaining their documented finite-sample $n$ versus $n-1$ scale conventions. Equation (ME.3) is not an MLE correction request; it is the intended unbiased GMM moment estimator.

## Gaussian prior and quadratic-penalty equivalence

Let the external information be

$$
\mu\sim N(m,\tau^2). \tag{ME.4}
$$

The Bulletin 17C parameter penalty is

$$
P_\mu(\mu)=\frac{1}{2n}\frac{(\mu-m)^2}{\tau^2}. \tag{ME.5}
$$

Consequently, `ParameterPenalty.MSE` equals $\tau^2$ directly. Multiplying the penalized GMM objective by $n$ recovers the Gaussian negative-log-prior kernel; no additional division of $\tau^2$ by $n$ is permitted.

For either estimator, let $\widehat\mu_L$ and $V_L$ be its unpenalized location estimate and variance. Independent Gaussian information gives

$$
V_{\mathrm{post}}
=\left(\frac1{V_L}+\frac1{\tau^2}\right)^{-1},
\qquad
\widehat\mu_{\mathrm{post}}
=V_{\mathrm{post}}
\left(\frac{\widehat\mu_L}{V_L}+\frac m{\tau^2}\right). \tag{ME.6}
$$

The tests use three predeclared regimes relative to each estimator's own $SE_L=\sqrt{V_L}$:

| Regime | Prior scale and center | Verified location effect | Verified variance effect |
|---|---|---|---|
| Wide centered | $\tau=100SE_L$, $m=\widehat\mu_L$ | No material shift | Negligible contraction |
| Narrow centered | $\tau=0.1SE_L$, $m=\widehat\mu_L$ | No material shift | Approximately 99% contraction |
| Narrow shifted | $\tau=0.1SE_L$, $m=\widehat\mu_L+2SE_L$ | Material shift toward prior | Large contraction |

Sigma is reestimated in every MAP and GMM fit. Both posterior $\mu$ and posterior $\operatorname{Var}(\mu)$ match equation (ME.6); no fixed-sigma shortcut is used.

## GMM objective, gradient, and covariance scaling

Unpenalized GMM reports the conventional objective

$$
Q_n=\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n,
$$

while its estimating-equation gradient is $\mathbf D^{\mathsf T}\mathbf W\mathbf g_n$, one half of the scalar objective derivative. An independent central difference confirms that exact factor. The positive constant leaves the stationary point unchanged. GMM covariance is formed from the estimating-equation bread and meat, not from the numerical Hessian of $Q_n$, so the constant does not scale the reported covariance.

With a penalty, the optimized objective is

$$
Q_{n,P}=\frac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n+P,
$$

and the supplied gradient exactly matches an independent central difference. For efficient $\mathbf W=\mathbf S^{-1}$ and the default Bulletin 17C Gaussian-information covariance path, the penalty Hessian is included consistently so the sandwich covariance reduces to inverse total precision. The focused equation-(ME.6) result verifies both the point estimate and covariance consequence. No production-code change is required for this scaling convention.

The corresponding timeless implementation treatment is in [Generalized Method of Moments](../technical-reference/estimation/generalized-method-of-moments.md#gaussian-parameter-penalties).

## Focused verification evidence

| Exact verification method | Independent oracle | Acceptance tolerance | Result |
|---|---|---|---|
| `FlatPriorMap_MatchesClosedFormLog10NormalMle` | Equations (ME.1)-(ME.2) and direct likelihood | $10^{-5}$ parameters; $10^{-8}$ likelihood | Passed |
| `UnpenalizedB17CGmm_MatchesExactSampleMoments` | Equations (ME.1) and (ME.3) | $10^{-5}$ parameters; $10^{-10}$ objective | Passed |
| `MapMuPriorRegimes_MatchAnalyticalFitAndVarianceInfluence` | Normal score and full analytical Hessian | $2\times10^{-5}$ to $2\times10^{-3}$ scaled | Passed |
| `GmmMuPenaltyRegimes_MatchAnalyticalFitAndVarianceInfluence` | Moment equations and analytical GMM bread | $2\times10^{-5}$ to $2\times10^{-3}$ scaled | Passed |
| `MapAndGmmMuPosterior_MatchesInverseVarianceWeighting` | Equation (ME.6), mean and variance | $10^{-4}$ centered means; shifted mean $0.01SE_L$; variance 0.2%-1.5% | Passed |
| `PenalizedObjectiveGradient_MatchesIndependentCentralDifference` | Independently coded central difference | $10^{-8}$ absolute | Passed |
| `UnpenalizedEstimatingGradient_IsHalfConventionalObjectiveDerivative` | Independently coded central difference and exact factor $1/2$ | $10^{-8}$ absolute | Passed |

Each method was run separately through `scripts/run-verification-test.ps1`; the complete Verification project was not executed. All focused builds used the local Numerics project and .NET 10, with zero build warnings and errors.

## Influence and leverage work in progress

Exact leave-one-out refits will provide the observation-influence oracle. Full-Hessian and deletion-covariance comparisons will provide the leverage oracle; no sum-to-$p$ assertion is accepted without a derivation. GMM-specific diagnostics will not be labeled Pareto $k$.

## External model-comparison oracles

- R `loo` 2.10.0 for WAIC and PSIS-LOO.
- R `BayesianTools` 0.1.9 for the selected DIC convention.
- R `gmm` 1.9-1 for estimates, covariance, and Hansen's $J$.
- R `bbmle` 1.0.25.1 for nuisance-reoptimized profiles.
- Python ArviZ 1.2.0 as a secondary WAIC/LOO comparison.

The pending fixtures use committed deterministic posterior draws and pointwise log-likelihood matrices so package parity is not confounded with sampler randomness.
