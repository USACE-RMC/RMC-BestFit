# Model Estimation and Diagnostics Verification

## Status

Phase 2 is complete for its approved scope. The Log10-Normal MLE/MAP/GMM baseline, Gaussian prior and quadratic-penalty equivalence, GMM objective/covariance scaling, scoped fit/variance/combined-influence diagnostics, DIC, WAIC, PSIS-LOO, Hansen J, overidentified one-step fitting, MLE/MAP nuisance profiling, fixed-weight and efficient GMM sandwich covariance, ARWMH realized-state covariance, NUTS gradient and acceptance-contract integration, and rank-normalized R-hat and conservative bulk/tail ESS are verified. TR-023 and TR-032 retain their public signatures; TR-027 exposes explicit covariance status and failure contracts; TR-028 remains a documented, verified limitation.

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
| `Log10NormalObservationInfluence_MatchesRGmmOracle` | R `gmm` 1.9.1 score and bread | $10^{-5}$ absolute | Passed |
| `VanishingCenteredPenalty_PreservesObservationInfluenceScale` | Objective-scale invariance | $10^{-4}$ absolute for finite wide penalty | Passed |
| `MapMuPriorRegimes_SeparateFitAndVarianceInfluence` | Gaussian score and generalized variance | Predeclared qualitative separations | Passed |
| `GmmMuPenaltyRegimes_SeparateFitAndVarianceInfluence` | Gaussian-equivalent penalty score and generalized variance | Predeclared qualitative separations | Passed |
| `CenteredPriorAndPenalty_VarianceInfluenceDeclinesWithSampleSize` | Information scaling from $n=7$ to $n=70$ | Strict ordering and negligible centered Cook influence | Passed |
| `MapVarianceInfluence_FullCurvaturePreservesMaterialMagnitudeAndLeadingRanking` | Analytical Log10-Normal observation Hessian | $0.003$ absolute; exact leading-three order | Passed |
| `MLE_ProfileLikelihood_MatchesRTrueProfile` | R `bbmle` plus closed-form nuisance optimum | $10^{-8}$ parameters/interval; $10^{-10}$ log likelihood | Passed - TR-023 MLE correction |
| `MAP_ProfileLikelihood_WithFlatPriors_MatchesRTrueProfile` | R `bbmle` plus constant flat-prior shift | $10^{-8}$ parameters/interval; $10^{-10}$ log likelihood | Passed - TR-023 MAP correction |
| `MAP_ProfileLikelihood_WithInformativePrior_ProfilesFullPosteriorKernel` | Closed-form informative-prior nuisance optimum | $10^{-8}$ parameters; $10^{-10}$ log posterior | Passed - full posterior profiling |
| `SampleFromPriors_SoftJointPrior_CurrentlyDrawsIndependentMarginals` | Analytical independent Uniform marginals and coupled-prior density | Predeclared correlation, separation, and density checks | Passed - TR-028 characterized |

Each method was run separately through `scripts/run-verification-test.ps1`; the complete Verification project was not executed. All focused builds used the local Numerics project and .NET 10, with zero build warnings and errors.

## Profile Likelihood, Covariance Failure, and Joint-Prior Characterization

### Profile likelihood (TR-023)

The committed [profile-likelihood oracle](../../verification/data/model-estimation/profile-likelihood-oracle.json) is generated by [generate_profile_likelihood_oracle.R](../../verification/r/model-estimation/generate_profile_likelihood_oracle.R) with R 4.4.3 and `bbmle` 1.0.25.1. C# reads only the committed JSON. The two-parameter fixture uses

$$
\ell(\theta_1,\theta_2)
=
-\frac{\theta_1^2-2\rho\theta_1\theta_2+\theta_2^2}
{2(1-\rho^2)},
\qquad \rho=0.8.
$$

At fixed $\theta_1$, nuisance reoptimization gives $\widehat\theta_2(\theta_1)=\rho\theta_1$ and the true profile $\ell_p(\theta_1)=-\theta_1^2/2$. Holding the nuisance parameter at its joint optimum instead gives the coordinate slice $-\theta_1^2/[2(1-\rho^2)]$. R `bbmle`, direct one-dimensional nuisance optimization, and the closed form agree over all eight grid values.

The corrected MLE and flat-prior MAP methods match the true profile at all eight oracle values and return the nuisance-optimized 90% interval $[-1.6448536270,1.6448536270]$. An additional analytical fixture places an informative Gaussian prior on the nuisance parameter and verifies that MAP reoptimization follows the complete posterior-kernel optimum rather than the data-only optimum or former coordinate slice. These are profile-posterior cutoff intervals, not posterior credible intervals; MCMC marginal quantiles remain the Bayesian interval product.

### Covariance failure status (TR-027)

Deterministic fast tests place MLE and MAP in an estimated state with a singular Hessian and force a GMM moment-condition exception. The public `Try` methods return `false`, report `CovarianceComputationStatus.Failed`, retain a diagnostic, and supply only a documented zero-valued placeholder; the existing getters and standard-error paths throw `InvalidOperationException`. Separate tests verify `Available` for a well-conditioned MLE covariance, `Regularized` for a covariance candidate requiring symmetry/positive-definite repair, and stable numeric enum values. This verifies that numerical failure is now distinguishable from estimated uncertainty and that repair is visible to callers.

### Joint-prior predictive sampling (TR-028)

A fixed-seed 20,000-draw fixture uses independent Uniform$(-1,1)$ parameter marginals plus a narrow soft prior term $Y\mid X\sim N(X,0.05^2)$. `PriorPredictiveCheck.SampleFromPriors()` draws the two marginal columns independently: their correlation remains near zero, $E[(Y-X)^2]$ remains near the independent-marginal value $2/3$, and fewer than 12% of draws satisfy $|Y-X|<0.1$. Each draw's fitness nevertheless equals the negative full coupled prior log density. This exact focused test verifies the documented limitation without changing the production sampler.

## Numerics MCMC Verification

Status: TR-025, TR-029, and TR-030 are fixed and passed focused deterministic tests in Numerics and BestFit. The changes preserve sampler configuration, trajectory targets, public diagnostic signatures, concise report labels, and serialized result fields.

### Adaptive Random-Walk Metropolis-Hastings (TR-025)

Haario, Saksman, and Tamminen's Adaptive Metropolis algorithm estimates proposal covariance from the realized Markov-chain history. A rejected or infeasible proposal repeats the retained state and therefore belongs in that history. The independently maintained R `BayesianTools` implementation retains the preceding chain row on rejection and adapts from the stored chain, while Python `pymcmcstat` identifies its covariance adaptation as the Haario-Saksman-Tamminen method. Both sources support the preserved distinction: continual updating is intentional, while omitting repeated states from an early segment was not.

The Numerics fixtures force every proposal to be rejected at the origin. Both 12-transition runs now record all 12 repeated retained states, whether the nominal warmup boundary is 50 or five. The BestFit integration fixture exercises `BayesianAnalysis.SetUpSampler()` with four chains; each performs 125 rejected raw transitions and each covariance records all 125.

Focused Numerics methods:

- `Test_MCMCSamplerDiagnostics.ARWMH_RejectedWarmupTransitionsEnterCovariance`
- `Test_MCMCSamplerDiagnostics.ARWMH_RejectedPostWarmupTransitionsContinueEnteringCovariance`

Focused BestFit method:

- `NumericsMcmcFindingTests.Arwmh_BestFitWiringRecordsEveryRealizedStateInAdaptiveCovariance`

### No-U-Turn Sampler diagnostics (TR-030)

Hoffman and Gelman's NUTS dual averaging uses a trajectory acceptance statistic, not a count of whether each completed transition returned a retained state. PyMC exposes mean tree acceptance, divergences, energy and energy change, depth, tree size, and step size. BlackJAX exposes acceptance rate, divergence, energy, expansion count, and integration-step count. These independently implemented interfaces establish the diagnostic quantities expected from NUTS; exact paths are not a portable cross-package oracle because random-number streams and trajectory implementations differ.

A seeded two-dimensional Gaussian Numerics fixture confirms that `MCMCSampler.AcceptanceRates` retains its established accepted-transition/sample-count meaning while `NUTS.HamiltonianAcceptanceRates` exposes the distinct mean post-warmup trajectory statistic. When `MCMCResults` is constructed from NUTS, its existing `AcceptanceRates` field stores the Hamiltonian statistic so BestFit can persist and report it. Divergence, maximum-depth, tree/leapfrog, step-size, and E-BFMI arrays remain non-null live-sampler diagnostics on `NUTS`; they are not `MCMCResults` properties and are not serialized or displayed by BestFit. Interim JSON containing the removed fields remains readable because unknown properties are ignored. A direct identity test matches Stan's `mean(diff(E)^2) / var(E)` convention. The streaming accumulators require constant memory and no extra target or gradient evaluations.

Focused Numerics methods:

- `Test_MCMCSamplerDiagnostics.NUTS_DiagnosticArraysAreNonNullAndEmptyBeforeSampling`
- `Test_MCMCSamplerDiagnostics.NUTS_AcceptanceContractsRemainSeparatedAndResultsPersistHamiltonianRates`
- `Test_MCMCSamplerDiagnostics.MCMCResults_RetainsBaselineNullabilityAndOmitsNutsDiagnostics`
- `Test_MCMCSamplerDiagnostics.NUTS_EnergyBayesianFractionOfMissingInformationMatchesStanFormula`
- `Test_MCMCInitialization.NutsInitializationUsesConfiguredGradientAndReducesLikelihoodWork`

Focused BestFit methods:

- `NumericsMcmcFindingTests.Nuts_BestFitResultsUseHamiltonianAcceptanceWithoutDetailedDiagnostics`
- `NumericsMcmcFindingTests.Nuts_BestFitNumericalGradientMatchesPosteriorGradient`

The historical NUTS gradient-routing issue was narrower than the reporting defect: the reasonable-step-size initialization heuristic once bypassed a caller-supplied analytic gradient. Numerics commit `33dc1af` corrected that route, and its permanent unit regression passes. BestFit intentionally supplies no analytic gradient; its focused coupled-prior method confirms the default bounded finite differences differentiate the complete posterior passed by `BayesianAnalysis.SetUpSampler()`. All C# methods use deterministic inline targets and require no R or Python runtime.

### Rank-normalized convergence diagnostics (TR-029)

Numerics now implements Vehtari et al.'s rank-normalized split R-hat and folded rank-normalized split R-hat, storing their maximum in the existing `Rhat` field. Its existing scalar `ESS` field stores the minimum of rank-normalized bulk ESS and pooled 0.05/0.95 quantile ESS. Autocovariances use zero-padded FFTs and Geyer's multi-chain initial-positive and initial-monotone paired sequence. The existing 51-lag original-scale averaged ACF remains unchanged for plotting, and the diagnostic calculations perform no model-target evaluations.

The committed [MCMC diagnostics oracle](../../verification/data/model-estimation/mcmc-diagnostics-oracle.json) is generated by [generate_mcmc_diagnostics_oracle.R](../../verification/r/model-estimation/generate_mcmc_diagnostics_oracle.R) with R 4.4.3 and `posterior` 1.7.0. Nine deterministic fixtures cover IID chains, autocorrelation, shifted means, scale disagreement, sticky tails, ties, constants, warmup removal, and chain permutation. R records rank-normalized R-hat, bulk ESS, lower-tail ESS, upper-tail ESS, and their conservative minimum. C# verification consumes only the committed JSON artifact.

Focused Numerics methods:

- `Test_MCMCDiagnostics.Test_ModernDiagnostics_MatchRPosteriorReference`
- `Test_MCMCDiagnostics.Test_GelmanRubin_FoldedRanksDetectScaleMismatch`
- `Test_MCMCDiagnostics.Test_ModernDiagnostics_EdgeCases`

Focused BestFit methods `NumericsMcmcFindingTests.RankNormalizedRhat_MatchesRPosteriorOracle` and `ConservativeEss_MatchesRPosteriorBulkAndTailOracle` establish exact artifact parity. Fast report tests prove that R-hat 1.005 passes and 1.02 warns under the 1.01 readiness threshold. Public methods, result properties, and serialization signatures are unchanged; single-chain R-hat and invalid, constant, or insufficient diagnostic input remain `NaN`.

Sources: [Haario et al. Adaptive Metropolis paper](https://projecteuclid.org/journals/bernoulli/volume-7/issue-2/An-adaptive-Metropolis-algorithm/bj/1080222083.full), [BayesianTools sampler source](https://github.com/florianhartig/BayesianTools/blob/master/BayesianTools/R/mcmcRun.R), [pymcmcstat sampler documentation](https://pymcmcstat.readthedocs.io/en/latest/pymcmcstat.samplers.html), [Hoffman-Gelman NUTS paper](https://jmlr.org/papers/v15/hoffman14a.html), [PyMC NUTS diagnostics](https://www.pymc.io/projects/docs/en/stable/api/generated/pymc.NUTS.html), [BlackJAX `NUTSInfo`](https://blackjax-devs.github.io/blackjax/autoapi/blackjax/mcmc/nuts/index.html), [RStan HMC diagnostics](https://mc-stan.org/rstan/reference/check_hmc_diagnostics.html), [CmdStan `diagnose`](https://mc-stan.org/docs/2_39/cmdstan-guide/diagnose_utility.html), [R `posterior` R-hat](https://mc-stan.org/posterior/reference/rhat.html), [R `posterior` bulk ESS](https://mc-stan.org/posterior/reference/ess_bulk.html), and [R `posterior` tail ESS](https://mc-stan.org/posterior/reference/ess_tail.html).

A before/after .NET 10 Release benchmark used the same deterministic four-chain, ten-parameter fixture, 100 synthetic observations, thinning 20, and output length 10,000. Across five measured runs after warmup, the baseline median was 1,232.328 ms and the modern median was 1,243.330 ms, an increase of 0.893%. Both variants performed exactly 439,645 target evaluations. The result satisfies the predeclared no-new-evaluations and no-more-than-5% end-to-end acceptance limits.

## Fit influence, variance influence, and combined leverage

The diagnostics deliberately keep two effects separate. At a MAP estimate, component $k$ has local score $\mathbf s_k$ and posterior covariance $\boldsymbol\Sigma$. Its fit influence is the Cook score quadratic

$$
D_k^{\mathrm{MAP}}
=\frac{\mathbf s_k^{\mathsf T}\boldsymbol\Sigma\mathbf s_k}{p}. \tag{ME.7}
$$

Observation variance influence uses the local curvature trace. Prior variance influence uses the finite generalized-variance change evaluated at the fitted mode,

$$
V_k^{\mathrm{prior}}
=\frac1p\left|
\log\det(\boldsymbol\Sigma_{-k})-
\log\det(\boldsymbol\Sigma)
\right|. \tag{ME.8}
$$

The displayed combined leverage is the additive ranking index

$$
L_k=D_k+V_k. \tag{ME.9}
$$

It is not classical hat-matrix leverage and is not expected to sum to $p$. Plot percentages are each component's share of total combined influence. The same definitions are used for observations and prior or penalty components, while the variance calculation remains appropriate to the component size: a first-order trace for one observation and a finite log-determinant change for a prior or penalty that can supply substantial curvature.

For the displaced narrow-prior Log10-Normal fixture, replacing the current observation diagonal-curvature trace with the analytical full Hessian changes every variance-influence value by less than $0.003$ and preserves the three leading observations exactly. This establishes that cross-curvature does not materially alter the scoped ranking; it is not a universal claim for other models.

### GMM calibration against R

For GMM, define

$$
\mathbf e_i=\mathbf D^{\mathsf T}\mathbf W\mathbf g_i,
\qquad
\mathbf B=\mathbf D^{\mathsf T}\mathbf W\mathbf D+\mathbf H_P. \tag{ME.10}
$$

The diagnostic curvature always uses the half-quadratic objective

$$
Q_{\mathrm{diag}}(\boldsymbol\theta)
=\frac12\mathbf g_n^{\mathsf T}\mathbf W\mathbf g_n+P(\boldsymbol\theta),
\qquad
\boldsymbol\Sigma_Q=\{\nabla^2 Q_{\mathrm{diag}}\}^{-1}. \tag{ME.11}
$$

This diagnostic convention is independent of whether a penalty is enabled. It does not change the public optimizer objective, estimating gradient, point estimate, penalty Hessian, or reported GMM covariance. Observation fit and variance influence are

$$
D_i^{\mathrm{GMM}}
=\frac{\mathbf e_i^{\mathsf T}\boldsymbol\Sigma_Q\mathbf e_i}{n^2p},
\qquad
V_i^{\mathrm{GMM}}
=\frac{|\mathbf e_i^{\mathsf T}\mathbf B^{-1}\mathbf e_i|}{np}. \tag{ME.12}
$$

The independent artifact [gmm-influence-oracle.json](../../verification/data/model-estimation/gmm-influence-oracle.json), generated by [generate_gmm_influence_oracle.R](../../verification/r/model-estimation/generate_gmm_influence_oracle.R) with R 4.4.3, `gmm` 1.9.1, and `sandwich` 3.1.2, gives

$$
\sum_i D_i^{\mathrm{GMM}}=0.0880102040816327,
\quad
\sum_i V_i^{\mathrm{GMM}}=0.616071428571429,
\quad
\sum_i L_i=0.704081632653061. \tag{ME.13}
$$

Every pointwise and aggregate BestFit value agrees within the predeclared $10^{-5}$ numerical-Hessian tolerance. Enabling a centered penalty with width $100SE_L$ leaves the observation diagnostics unchanged within $10^{-4}$, proving that diagnostic scale does not depend on the presence of a negligible penalty.

### Prior and penalty behavior

MAP priors and GMM penalties reproduce the intended regimes on their respective Cook scales:

- wide and centered: negligible fit and variance influence;
- narrow and centered: negligible fit influence and strong variance influence;
- narrow and shifted by $2SE_L$: both fit and variance influence;
- fixed centered prior or penalty: variance influence decreases from $n=7$ to $n=70$, while fit influence remains negligible.

MAP and GMM Cook magnitudes are not compared to each other because one derives from likelihood scores and the other from least-squares estimating equations. The tests compare rankings and prior/penalty behavior within each estimator.

Pareto $k$ and PSIS-LOO remain separate from local Cook/leverage diagnostics and are verified in their dedicated section below; exact leave-one-out refits are not part of the default runtime path.

## AIC and BIC Evaluated at MAP

Status: passed by focused regression and complete source-call-site audit on 25 July 2026.

Bayesian analyses use the stored MAP parameter vector but evaluate only the data log likelihood:

$$
\mathrm{AIC}_{\mathrm{MAP}}=-2\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})+2k,
\qquad
\mathrm{BIC}_{\mathrm{MAP}}=-2\ell_D(\widehat{\boldsymbol\theta}_{\mathrm{MAP}})+k\log n. \tag{ME.14}
$$

The audited call sites are `MaximumAPosteriori`, `UnivariateAnalysis`, `PointProcessAnalysis`, `MixtureAnalysis`, `CompetingRiskAnalysis`, `BivariateAnalysis`, `RatingCurveAnalysis`, `ARAnalysis`, `MAAnalysis`, `ARIMAAnalysis`, `ARIMAXAnalysis`, and `SpatialGEVAnalysis`. Every site calls `DataLogLikelihood` at the stored MAP; none passes `LogLikelihood` to the AIC/BIC helpers.

The focused methods `MaximumAPosterioriTests.Test_GetAIC_ReturnsFiniteValue` and `Test_GetBIC_ReturnsFiniteValue` use bounded uniform priors with nonzero normalization constants. Each test proves that the reported criterion equals the data-likelihood formula in (ME.14) and differs from the value obtained from the posterior kernel. Both methods passed through `scripts/run-verification-test.ps1 -Test <fully-qualified-method-name>`.

Spatial GEV uses each nonempty row/year as one multivariate BIC observation block. Its criteria retain the documented spatial likelihood, latent-error, missing-site, weighting, and dependence limitations. Bulletin 17C separately reports pseudo-AIC/pseudo-BIC by evaluating the LP3 data likelihood at its GMM solution; those values are not included in the Bayesian MAP claim above.

## DIC and WAIC External Package Parity

Status: passed by two exact focused methods on 25 July 2026.

The committed artifact [model-comparison-oracle.json](../../verification/data/model-estimation/model-comparison-oracle.json) is generated by [generate_model_comparison_oracle.R](../../verification/r/model-comparison/generate_model_comparison_oracle.R) with R 4.4.3, `BayesianTools` 0.1.9, and `loo` 2.10.0. It contains five exact Normal observations, forty deterministic posterior draws for location and scale, and the complete $40\times5$ pointwise data-log-likelihood matrix. R is used only by the generator; the C# tests consume the committed JSON artifact.

For the selected `BayesianTools::DIC` convention,

$$
\overline D=13.0457484595105,
\qquad
D(\overline{\boldsymbol\theta})=12.6377539947693,
\qquad
p_D=0.407994464741220,
\qquad
\mathrm{DIC}=13.4537429242518. \tag{ME.15}
$$

`InformationCriterionOracleTests.DIC_MatchesRBayesianToolsOracle` independently reconstructs every quantity from BestFit's data likelihood and proves that `BayesianAnalysis.DIC` agrees within the artifact's predeclared $10^{-10}$ absolute tolerance.

For `loo::waic` applied to the same draw-by-observation matrix,

$$
\mathrm{lppd}=-6.35851654848624,
\qquad
p_{\mathrm{WAIC}}=0.351001866165060,
\qquad
\mathrm{elpd}_{\mathrm{WAIC}}=-6.70951841465130,
\qquad
\mathrm{WAIC}=13.4190368293026. \tag{ME.16}
$$

`InformationCriterionOracleTests.WAIC_MatchesRLooOracle` first compares every BestFit pointwise log-likelihood value with the R matrix, then verifies `WAIC_pD`, lppd, elpd, and `WAIC` within $10^{-10}$.

## PSIS-LOO and Pareto Diagnostics

Status: passed. Six exact focused methods verify the corrected BestFit implementation against the pinned R oracle, including aggregate and pointwise LOO, Pareto smoothing across all tail regimes, draw-count reliability classification, and the single-pass performance contract.

The committed [PSIS-LOO oracle](../../verification/data/model-estimation/psis-loo-oracle.json) is generated by [generate_psis_loo_oracle.R](../../verification/r/model-comparison/generate_psis_loo_oracle.R) with R 4.4.3, `loo` 2.10.0, and `posterior` 1.7.0. The generator applies `loo::psis` and `loo::loo` to the complete deterministic pointwise matrix already used for WAIC. It records normalized and unnormalized smoothed log weights, Pareto $k$, importance-sampling effective sample size, tail length, all five pointwise LOO columns, aggregate estimates and standard errors, the sample-size-dependent diagnostic threshold, the Pareto-$k$ table, and the overall Monte Carlo standard error. The C# tests use only this committed JSON.

For the 40-draw, five-observation fixture, R and BestFit agree on

$$
\mathrm{elpd}_{\mathrm{loo}}=-6.70094841652149,
\qquad
p_{\mathrm{loo}}=0.342431868035246,
\qquad
\mathrm{LOOIC}=13.4018968330430,
\qquad
\mathrm{SE}(\mathrm{LOOIC})=1.95756902272540. \tag{ME.17}
$$

All five pointwise ELPD contributions and Pareto-$k$ estimates also pass. The Pareto values are $(0.0682,-0.0302,0.4116,0.3062,0.3209)$. Aggregate and pointwise quantities use a $10^{-10}$ absolute tolerance; smoothed log weights, Pareto $k$, and effective sample size use $10^{-8}$.

Six separate 256-ratio fixtures cover bounded ($k<0$), light, moderate, high, nonfinite-mean ($k>1$), and degenerate tails. Every unnormalized smoothed log weight, fitted $k$, and effective sample size matches the artifact. The degenerate fixture correctly returns $k=+\infty$.

For $S=40$, `loo` 2.10.0 uses the reliability threshold

$$
k_{\mathrm{threshold}}=\min\left(1-\frac{1}{\log_{10}S},0.7\right)=0.375803649418215. \tag{ME.18}
$$

The third observation has $k=0.411627035764065$ and is therefore flagged. BestFit applies this draw-count threshold to Bayesian reliability and category summaries and preserves it through XML round-trip. Historical public constructors retain their earlier fixed categories for API and serialization compatibility.

The runtime checks are structural rather than timing-dependent. Default Bayesian completion evaluates `PointwiseDataLogLikelihood` exactly once for each of the $S$ retained draws, shares the transient $n\times S$ matrix between WAIC and PSIS, then releases it. A later influence request reuses the retained $O(n)$ pointwise ELPD and Pareto-$k$ arrays and performs no additional pointwise likelihood evaluations. The generalized-Pareto fit uses the pinned bounded fixed grid rather than an iterative optimizer for every observation. No exact leave-one-out refits are run by default.

This verification matches the pinned reference with `r_eff = 1`. The current implementation does not estimate chain-relative efficiency for the PSIS tail length and does not implement exact or moment-matched refits for observations above the reliability limit.

Focused methods:

- `RlooOracle_InternalIdentitiesAreConsistent`
- `PSISLOO_MatchesRLooOracle`
- `PsisTailRegimes_MatchRLooOracle`
- `ParetoInfluence_UsesRloo210DiagnosticThreshold`
- `DefaultInformationCriteria_EvaluatePointwiseLikelihoodOnce`
- `InfluenceDiagnostics_ReuseCachedPointwiseLikelihood`

## GMM Specification, Covariance, and Legacy Influence Verification

Status: TR-026, TR-032, and TR-034 passed their approved corrections. Fixed-weight one-step and efficient two-step sandwich covariance both match the self-checking R oracle.

The committed [GMM specification oracle](../../verification/data/model-estimation/gmm-specification-oracle.json) is generated by [generate_gmm_specification_oracle.R](../../verification/r/model-estimation/generate_gmm_specification_oracle.R) with R 4.4.3 and `gmm` 1.9.1. It records parameters, objectives, specification statistics, objective weights, covariance weights, covariance matrices, and standard errors for fixed-weight and efficient two-step fits. The generator independently reconstructs each centered IID sandwich from its bread and meat before writing JSON. The C# methods read only the committed artifact. The deterministic fixture has one parameter and two moments,

$$
g_{1i}(\theta)=x_i-\theta,
\qquad
g_{2i}(\theta)=z_i(x_i-\theta). \tag{ME.19}
$$

With a fixed identity weighting matrix, R obtains

$$
\widehat\theta_{\mathrm{fixed}}=2.28992700729929,
\qquad
Q(\widehat\theta_{\mathrm{fixed}})=0.317956204379562. \tag{ME.20}
$$

BestFit now accepts the overidentified `OneStep` specification, reports it as valid, and matches the R parameter and objective within the artifact tolerances. A subsequent `PostProcess(computeJstat: true)` deliberately leaves `JStat` and `JStatPval` as `NaN` because a generic fixed weight does not automatically have the efficient-weight Hansen chi-squared interpretation.

For the efficient two-step fit, BestFit and R agree on $\widehat\theta=1.93548454750500$ and $Q=1.00755078518454$. R `gmm::specTest` reports

$$
J=nQ=10.0755078518454,
\qquad
p_{\chi^2_1}=0.00150253202968641. \tag{ME.21}
$$

BestFit preserves the unpenalized moment objective evaluated with the strategy-selected weight before covariance processing can replace `W`. `PostProcess(computeJstat: true)` therefore reproduces R's $J$ and p-value within $10^{-7}$ and $10^{-8}$ respectively. Penalized fits and generic fixed-weight one-step fits leave both fields as `NaN` rather than receiving an unsupported Hansen label.

For arbitrary fixed identity weighting, R `gmm` with `vcov="iid"` and the closed-form sandwich both report variance $0.145652392138090$. BestFit retains that fixed weight in bread and meat and agrees within $10^{-8}$. For two-step GMM, the second-step objective uses the covariance estimated after the first fit, while final covariance uses $S^{-1}$ recomputed at the final parameters; R, the analytical reconstruction, and BestFit agree on variance $0.132600447299456$. The generator does not use `vcov="TrueFixed"`, because that option asserts the supplied matrix is already the inverse moment covariance rather than requesting an arbitrary-fixed-weight sandwich.

TR-032 is resolved for supported API use. Both legacy `GetInfluenceDiagnostics` overloads carry a non-error `[Obsolete]` warning that directs callers to `GetLeverageDiagnostics()` or `GetCooksDistance()`. Reflection-based compatibility verification confirms the signatures remain callable and preserve their historical mapping while avoiding compile-time use of the misleading PSIS-shaped contract.

Focused methods:

- `HansenJ_MatchesRGmmSelectedWeightStatistic`
- `OveridentifiedOneStep_MatchesRGmmFixedWeightOracle`
- `OveridentifiedTwoStepSandwichCovariance_MatchesRGmmOracle`
- `OveridentifiedFixedWeightSandwichCovariance_MatchesRGmmOracle`
- `GmmCookInfluence_LegacyPsisAdapterIsObsoleteCompatibilityOnly`

## External Model-Comparison Oracle Status

The scoped DIC, WAIC, PSIS-LOO, Pareto-k, and GMM covariance artifacts are committed and consumed without an R or Python runtime. ArviZ would be a redundant secondary WAIC/LOO implementation, not missing verification evidence or a Phase 2 exit requirement.