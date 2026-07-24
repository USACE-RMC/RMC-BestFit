# Distribution Fitting Verification

## Scope

The first phase verifies the 15 supported univariate families, the fitting pipeline, goodness-of-fit metrics, and findings TR-001, TR-002, TR-009, and TR-010.

## Distribution matrix

| Family | Primary oracle | Secondary oracle | Status |
|---|---|---|---|
| Normal | Closed-form MLE | SciPy | Planned |
| Log10-Normal | Closed-form transformed MLE | SciPy | Analytical verification passed; SciPy secondary pending |
| Ln-Normal | Closed-form transformed MLE | SciPy | Planned |
| Exponential | Closed form / published data | SciPy | Planned |
| Gamma | lmomco | SciPy | Planned |
| GEV | lmomco | SciPy | Planned |
| Generalized Logistic | lmomco | Published hydrology example | Planned |
| Generalized Normal | lmomco | Published hydrology example | Planned |
| Generalized Pareto | lmomco | SciPy | Planned |
| Gumbel | lmomco | SciPy | Planned |
| Kappa Four | Analytical branch identities | SciPy and lmomco | Zero-shape branch verified; family validation active |
| Logistic | Closed form / published data | SciPy | Planned |
| Log-Pearson III | Transformed Pearson III | lmomco | Planned |
| Pearson III | lmomco | SciPy | Planned |
| Weibull | lmomco | SciPy | Planned |

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
