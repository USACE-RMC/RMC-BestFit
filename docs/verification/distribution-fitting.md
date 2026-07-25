# Distribution Fitting Verification

## Scope

The first phase verifies the 15 supported univariate families, the fitting pipeline, goodness-of-fit metrics, and findings TR-001, TR-002, TR-009, TR-010, TR-063, and TR-064.

## Distribution matrix

| Family | Primary oracle | Secondary oracle | Status |
|---|---|---|---|
| Normal | Closed-form MLE | SciPy | Passed - SciPy parameter, likelihood, CDF, and quantile parity |
| Log10-Normal | Closed-form transformed MLE | SciPy | Passed - analytical and SciPy parity |
| Ln-Normal | Closed-form transformed MLE | SciPy | Passed - SciPy parity |
| Exponential | Closed form | SciPy | Passed - SciPy parity |
| Gamma | SciPy | lmomco | Passed - SciPy parity |
| GEV | SciPy | lmomco | Passed - SciPy parity |
| Generalized Logistic | lmomco | Published hydrology formulation | Passed - lmomco parity |
| Generalized Normal | lmomco | Published hydrology formulation | Passed - lmomco parity |
| Generalized Pareto | SciPy differential evolution | lmomco | Passed - global-optimum SciPy parity |
| Gumbel | SciPy | lmomco | Passed - SciPy parity |
| Kappa Four | Analytical branch identities | SciPy | Passed - zero-shape identities and SciPy finite-shape parity |
| Logistic | Closed form | SciPy | Passed - SciPy parity |
| Log-Pearson III | Transformed Pearson III | SciPy differential evolution | Passed - global-optimum SciPy parity |
| Pearson III | SciPy | lmomco | Passed - SciPy parity |
| Weibull | SciPy | lmomco | Passed - SciPy parity |

## TR-001 - Kappa Four zero primary shape

For \(\kappa=0\), \(h\ne0\), \(z=(x-\xi)/\alpha\), the implemented CDF is

$$
F(x)=\left[1-h\exp(-z)\right]^{1/h}.
$$

Independent differentiation and inversion give

$$
f(x)=\frac{\exp(-z)}{\alpha}\left[1-h\exp(-z)\right]^{1/h-1}
$$

and

$$
F^{-1}(p)=\xi-\alpha\log\left(\frac{1-p^h}{h}\right).
$$

The focused analytical tests are:

- `RMC.BestFit.Verification.DistributionFitting.KappaFourZeroShapeVerificationTests.ZeroShapeNonzeroHondo_PdfMatchesAnalyticalDerivative`
- `RMC.BestFit.Verification.DistributionFitting.KappaFourZeroShapeVerificationTests.ZeroShapeNonzeroHondo_QuantileInvertsCdf`

**Disposition:** Confirmed defect. Both exact methods failed against the baseline implementation.

**Implementation:** Fixed without public API changes in RMC.Numerics commit `3e058ebe5917817f3dde5e3b2ed6574d6bab083e`.

**Verification:** Passed. The PDF derivative and quantile/CDF round-trip methods were run separately through the guarded runner at absolute tolerance `1e-10`. The complete Numerics .NET 10 unit gate passed 1,905 tests with zero failures or skips. The [evidence artifact](../../verification/data/distribution-fitting/kappa-four-zero-shape.json) records the baseline failures, passing results, source commits, and TRX paths.

## TR-002 - finite Kappa shape pairs

The Kappa Four support changes with both finite shape parameters. Restrictions on the existence of particular moments do not imply that the distribution parameters themselves are invalid. The regression therefore spans all four sign combinations with $(\kappa,h)$ equal to $(-0.25,-0.5)$, $(-0.25,0.5)$, $(0.25,-0.5)$, and $(0.25,0.5)$.

The focused unit method is:

- `Distributions.Univariate.Test_KappaFour.Test_K4_FiniteShapePairsHaveConsistentSupport`

**Disposition:** Rejected non-defect. Every finite pair is admitted when the scale is positive; the corresponding support is enforced by the CDF, density, and quantile functions.

**Verification:** Passed on Numerics .NET 10 at CDF/quantile absolute tolerance `1e-10`. The method verifies monotone finite quantiles at five probabilities, positive median density, and finite endpoint behavior. The complete Kappa Four class passed 12 tests with zero failures or skips. No BestFit Verification method was added because this is a fast parameter-domain regression owned by Numerics. See the [evidence artifact](../../verification/data/distribution-fitting/kappa-four-finite-shapes.json).

## TR-009 - parameter-adjusted RMSE

For \(n\) paired observations and modeled values with \(k\) fitted parameters, the implemented statistic is

$$
\operatorname{RMSE}
=\left[\frac{1}{n-k}\sum_{i=1}^{n}
\left(\widehat y_i-y_i\right)^2\right]^{1/2},
\qquad 0\le k<n.
$$

The deterministic fixture uses \(y=[0,0,0,0]\), \(\widehat y=[1,2,3,4]\), and \(k=1\). Its independent hand calculation is

$$
\sqrt{\frac{1^2+2^2+3^2+4^2}{4-1}}
=\sqrt{10}
=3.1622776601683795.
$$

The focused analytical method is:

- `RMC.BestFit.Verification.DistributionFitting.GoodnessOfFitRmseVerificationTests.ParameterAdjustedRmse_UsesAllResidualsAndIsPermutationInvariant`

**Disposition:** Confirmed defect. The baseline returned `2.160246899469287`, demonstrating that the final residual was omitted when \(k=1\).

**Implementation:** Fixed without public API changes in RMC.Numerics commit `24bf9f98139b23400bf008df413b0d97330ccfd3`. All \(n\) residuals enter the numerator, \(n-k\) is used only as the denominator, and invalid \(k\) values are rejected.

**Verification:** Passed at absolute tolerance `1e-12`, including paired-permutation invariance. The complete Numerics .NET 10 unit gate passed 1,907 tests with zero failures or skips. The [evidence artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json) records the baseline failure, corrected result, source commits, and TRX paths.

## TR-010 - all-candidate failure reports overall success

A deterministic outlier fixture completed with `IsEstimated == true` while all 15 `FittedDistribution.FitSucceeded` values were false. Source inspection confirms that the outer run sets `IsEstimated = true` whenever the parallel loop itself completes, without requiring a successful candidate.

**Disposition:** Confirmed defect by deterministic behavioral reproduction and source inspection.

**Implementation:** Fixed without public API changes. `IsEstimated` and `AnalysisCompleted.Succeeded` now require at least one successful candidate; partial-success runs remain successful.

**Verification:** Passed by deterministic all-failed and partial-success regressions. The complete .NET 10 core gate passed 3,030 tests with zero failures or skips. No Verification-project method was added because this is a state-semantic regression, not a numerical validation claim. See the [evidence artifact](../../verification/data/distribution-fitting/fitting-analysis-success-state.json).

## TR-063 - whole-series replacement refresh

For an uncensored exact sample with \(n\) observations and default Weibull plotting parameter, ascending order statistic \(i\) has nonexceedance probability

$$
p_i=\frac{i}{n+1}. \tag{5}
$$

The baseline whole-series setter left every new `ExactData.PlottingPosition` at zero. `FittingAnalysis` therefore consumed complemented probability one, evaluated infinite candidate quantiles, and assigned infinite RMSE even when MLE and information criteria were finite.

**Disposition:** Confirmed defect.

**Implementation:** Fixed without public API or XML-schema changes. A valid programmatic series replacement performs one derived-state refresh after attaching the new handlers. Invalid transient frames remain assignable and defer refresh. The XML constructor suppresses all four replacement refreshes, preserves serialized positions exactly, and reprocesses effective threshold counts once.

The focused analytical method is:

- `RMC.BestFit.Verification.DistributionFitting.FittingAnalysisCriteriaVerificationTests.ExactSeriesReplacement_ProducesAnalyticalWeibullPositions`

**Verification:** Passed for all 39 oracle observations at absolute tolerance `1e-12`. Fast regressions also prove that deliberately non-derived serialized positions survive a round trip and that non-finite transient input retains the previous non-throwing behavior. The complete Debug regression gate passed Core 3,032, UI 564, and App 427 tests with zero failures or skips; the public API baseline and enforced XML-documentation build also passed. See the [evidence artifact](../../verification/data/distribution-fitting/dataframe-series-replacement.json).

## TR-064 - distribution-fitting optimizer precision

The common-data external validation fits Gumbel, Normal, and Logistic to one deterministic sample. Differential evolution identifies the same Gumbel likelihood region as SciPy, but the returned point does not meet the predeclared `1e-5` scaled parameter and optimizer-derived tolerances:

| Quantity | SciPy oracle | BestFit | Relative error | Acceptance |
|---|---:|---:|---:|---|
| Gumbel location | 93.11234799935337 | 93.1129321294911 | 6.27e-6 | Passed |
| Gumbel scale | 13.157628567998076 | 13.157823249599968 | 1.4796e-5 | Failed |
| Gumbel RMSE | 3.10635330619202 | 3.106522984310708 | 5.4623e-5 | Failed |

The exact failing methods are:

- `RMC.BestFit.Verification.DistributionFitting.FittingAnalysisCriteriaVerificationTests.FittedParameters_MatchIndependentOracle`
- `RMC.BestFit.Verification.DistributionFitting.FittingAnalysisCriteriaVerificationTests.CriteriaRankingWeightsAndConfiguredOrder_MatchIndependentOracle`

**Disposition:** Confirmed defect. Production behavior is unchanged pending approval of a focused fix.

**Planned correction:** Retain bounded differential evolution as the global search and apply a deterministic bounded local polish from its best point. Accept the polished point only when it remains finite and within bounds and does not reduce data log likelihood. This changes no public signatures or result ordering. See the [failure artifact](../../verification/data/distribution-fitting/fitting-analysis-optimizer-precision.json).

## Log10-Normal analytical verification

For \(x_i=\log_{10}(y_i)\),

$$
\widehat{\mu}=\frac{1}{n}\sum_i x_i,\qquad
\widehat{\sigma}^2=\frac{1}{n}\sum_i(x_i-\widehat{\mu})^2.
$$

For the deterministic log-space values \([1.1,1.4,1.7,2.0,2.3,2.6,2.9]\), the closed-form estimates are \(\widehat\mu=2\) and \(\widehat\sigma=0.6\), where the variance divisor is \(n\). On the original measurement scale, the log likelihood includes the transformation Jacobian:

$$
\ell(\mu,\sigma)
=-n\log(\sigma\sqrt{2\pi})
-\frac{1}{2\sigma^2}\sum_i(x_i-\mu)^2
-\sum_i\log(y_i\ln 10).
$$

The closed-form log likelihood is `-44.431208784723104`; the median is (10^2=100), and the 0.9 quantile is `587.3959385303014`.

The focused method is:

- `RMC.BestFit.Verification.DistributionFitting.Log10NormalFittingVerificationTests.ClosedFormMle_LikelihoodCdfAndQuantileMatchAnalyticalOracle`

**Verification:** Passed. BFGS used verification-only absolute and relative convergence tolerances of `1e-12`. Parameters passed at absolute tolerance `1e-5`, maximum log likelihood at `1e-8`, the direct and pointwise analytical likelihood at `1e-10`, the median CDF at `1e-12`, and the analytical quantile at `1e-9`. The fitted quantile uses the declared optimizer-scale relative tolerance. See the [evidence artifact](../../verification/data/distribution-fitting/log10-normal-closed-form.json).

## External family-oracle execution

Thirteen exact methods in `ScipyDistributionFittingVerificationTests` and two exact methods in `LmomcoDistributionFittingVerificationTests` were run separately through the guarded runner. Each method verifies the fitted parameter vector, maximized data log likelihood, representative CDF values, and representative quantiles. The C# tests consume only committed JSON and never invoke Python or R at runtime.

SciPy 1.16.1 supplies the overlapping family implementations. Generalized Pareto and Log-Pearson III use deterministic differential evolution in the generator because SciPy's default local start converged to an inferior stationary point for the declared fixtures. R 4.4.3 with `lmomco` 2.5.7 supplies Generalized Logistic and Generalized Normal. Parameter conversions, package versions, seeds, generator commands, and source hashes are recorded in:

- [SciPy family oracles](../../verification/data/distribution-fitting/scipy-family-oracles.json)
- [lmomco family oracles](../../verification/data/distribution-fitting/lmomco-family-oracles.json)
- [Verification data manifest](../../verification/data/MANIFEST.md)

All 15 family-specific methods passed. This result verifies the individual family implementations on their declared fixtures; it does not override the open TR-064 failure in the multi-candidate `FittingAnalysis` orchestration path.
