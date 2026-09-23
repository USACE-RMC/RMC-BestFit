<!-- verification-status: finalized -->
# Distribution Fitting Verification

## Scope

The first phase verifies the 15 supported univariate families, the fitting pipeline, goodness-of-fit metrics, and findings TR-001, TR-002, TR-009, TR-010, TR-063, and TR-064.

Phase 1 is closed for its approved scope. The 3 August 2026 normalized checkpoint confirms all nine distribution-fitting artifact hashes match `verification/data/MANIFEST.md`; Numerics .NET 10 Release 2,024/2,024 and Core/UI/App 3,116/568/428 pass with zero failures.

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

## Chunk 6A generated-parent recovery preparation

`FittingAnalysisRecoveryTests` adds one separately named generated-parent recovery cell for each
of the 15 supported default candidates. Each cell uses exactly 1,000 generated scalar observations,
seed `12345`, and the unmodified default `FittingAnalysis` candidate list. It requires overall
analysis completion, locates the generating-family `FittedDistribution`, and requires only that
candidate's `FitSucceeded` result; it does not assert an AIC, BIC, RMSE, or model-selection rank.

| Families | Parent/uncertainty design | Current evidence status |
|---|---|---|
| Normal, LogNormal, Exponential, Gamma, GEV, Gumbel, Logistic, Weibull | Explicit seeded parent vectors; fitted Numerics `ParameterCovariance(1000, MaximumLikelihood)` standardized-parent error at most 1.96 | Verified by authorized exact focused runs on 29 August 2026 |
| LnNormal | Explicit real-space parent `(mean=3.5, standard deviation=0.4)`; actual and parent distributions compared in Numerics covariance coordinates `(Mu, Sigma^2)` using untransformed `ParameterCovariance(1000, MaximumLikelihood)` | Verified by an authorized exact focused run on 29 August 2026 |
| Log-Pearson III, Pearson III | Explicit moment parents; actual and parent distributions compared in Numerics MLE covariance coordinates `(Mu, 1/Beta, Alpha)` using untransformed `ParameterCovariance(1000, MaximumLikelihood)` | Verified by authorized exact focused runs on 29 August 2026 |
| Generalized Pareto | Parent `(xi=0, alpha=20, kappa=0.15)`; covariance for alpha/kappa and Q(0.99) `QuantileVariance(0.99, 1000, MaximumLikelihood)` response band for the zero-location design | Verified by an authorized exact focused run on 29 August 2026 |
| Generalized Logistic, Generalized Normal, Kappa Four | Explicit seeded parent vectors; same-data auxiliary production `MaximumLikelihood.ParameterConfidenceIntervals(alpha: 0.05)` profile intervals must be finite, ordered, contain the parent, and contain the actual `FittingAnalysis` coordinate | Verified by authorized exact focused runs on 29 August 2026 after the RMC.BestFit finite-bracket and deterministic warm-start correction |

The secondary 5% point/curve criterion is conditional: it is evaluated only when the existing
95% band is narrower than 5% of a nonzero parent. The Generalized Pareto boundary coordinate is
evaluated in response space. The class is marked `[DoNotParallelize]` because each default-list
analysis internally fits 15 candidates in parallel; this avoids a method-level 15-by-15 estimator
fan-out when an approved suite run eventually occurs.

The eight historical `FittingAnalysisTests` fixed-data comparisons remain in source for provenance
but are no longer discovered or cataloged. Their 1%-10% coordinate bands lacked covariance or
profile-likelihood justification, while the generated-parent matrix and the SciPy/lmomco joint
likelihood-region oracles provide stronger current evidence for all 15 families. Their final
one-result passing runs under `20260831-195737-...` through `20260831-195840-...` are retained as
historical executions; no pass was transferred.

The separate 15-cell `UnivariateDistributionMLETests` real-data matrix remains current because its
published hydrologic datasets and recorded reference points are scientifically distinct. On 1
September 2026 its historical 1%-10% coordinate bands were replaced by joint 95%
likelihood-ratio regions with chi-square degrees of freedom equal to the fitted coordinate count.
Natural-log Normal references are explicitly converted from log-space location/scale to the
physical mean/standard-deviation order used by the fitted `LnNormal` model. All 15 exact identities
passed with one inspected TRX result each under `20260901-142634-...` through
`20260901-142721-...`. The discarded `20260901-140711-...Test_LnNormal_MLE` run had exactly one
failed result and exposed the missing parameter crosswalk; it is not evidence.

### Chunk 6A exact execution - 29 August 2026

Each identity was run separately through `scripts/run-verification-test.ps1` with its exact fully
qualified method name. Twelve cells passed: Normal, LogNormal, LnNormal, Exponential, Gamma,
Generalized Extreme Value, Generalized Pareto, Gumbel, Logistic, Log-Pearson Type III, Pearson Type
III, and Weibull. Their reviewed TRX records are under the corresponding timestamped
`TestResults/VerificationFocused/20260829-1620*` through `20260829-1623*` directories, and those
catalog identities are now verified.

The first Generalized Logistic, Generalized Normal, and Kappa Four runs reached a successful
default-list `FittingAnalysis` generating-family fit and auxiliary MLE, then exposed unsupported or
nonconvergent parameter-0 profile evaluations. The authorized RMC.BestFit correction now probes
outward to finite, model-constrained sign-changing endpoints, verifies them with Numerics
`Brent.Bracket`, solves them with `Brent.Solve`, and retries strict nuisance optimization from cached
successful profile starts in deterministic nearest-coordinate order. It changes no chi-squared
threshold, nuisance convergence requirement, optimizer default, Brent default, seed, parent, or
acceptance threshold.

Final one-method reruns of the three affected cells all passed. Their reviewed TRX records are:

- Generalized Logistic: `TestResults/VerificationFocused/20260829-165506-RMC_BestFit_Verification_DistributionFitting_FittingAnalysisRecoveryTests_GeneralizedLogistic_N1000_FittingAnalysisRecoversGeneratingFamily/haden_HADEN_2026-08-29_22_55_25.116.trx`.
- Generalized Normal: `TestResults/VerificationFocused/20260829-165528-RMC_BestFit_Verification_DistributionFitting_FittingAnalysisRecoveryTests_GeneralizedNormal_N1000_FittingAnalysisRecoversGeneratingFamily/haden_HADEN_2026-08-29_22_55_33.154.trx`.
- Kappa Four: `TestResults/VerificationFocused/20260829-165445-RMC_BestFit_Verification_DistributionFitting_FittingAnalysisRecoveryTests_KappaFour_N1000_FittingAnalysisRecoversGeneratingFamily/haden_HADEN_2026-08-29_22_54_53.189.trx`.

All 15 new generated-parent identities are therefore verified. The preliminary failed runs remain
retained as diagnostic evidence for the production correction.

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

**Verification:** Passed. The PDF derivative and quantile/CDF round-trip methods were run separately through the guarded runner at absolute tolerance `1e-10`. The normalized Numerics .NET 10 Release gate records 2,024 passing tests with zero failures. The [evidence artifact](../../verification/data/distribution-fitting/kappa-four-zero-shape.json) records the baseline failures, passing results, source commits, and TRX paths.

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

**Verification:** Passed at absolute tolerance `1e-12`, including paired-permutation invariance. The normalized Numerics .NET 10 Release gate records 2,024 passing tests with zero failures. The [evidence artifact](../../verification/data/distribution-fitting/parameter-adjusted-rmse.json) records the baseline failure, corrected result, source commits, and TRX paths.

## TR-010 - all-candidate failure reports overall success

A deterministic outlier fixture completed with `IsEstimated == true` while all 15 `FittedDistribution.FitSucceeded` values were false. Source inspection confirms that the outer run sets `IsEstimated = true` whenever the parallel loop itself completes, without requiring a successful candidate.

**Disposition:** Confirmed defect by deterministic behavioral reproduction and source inspection.

**Implementation:** Fixed without public API changes. `IsEstimated` and `AnalysisCompleted.Succeeded` now require at least one successful candidate; partial-success runs remain successful.

**Verification:** Passed by deterministic all-failed and partial-success regressions. The normalized Core Debug gate records 3,116 passing tests with zero failures. No Verification-project method was added because this is a state-semantic regression, not a numerical validation claim. See the [evidence artifact](../../verification/data/distribution-fitting/fitting-analysis-success-state.json).

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

**Verification:** Passed for all 39 oracle observations at absolute tolerance `1e-12`. Fast regressions also prove that deliberately non-derived serialized positions survive a round trip and that non-finite transient input retains the previous non-throwing behavior. The normalized Debug regression gate records Core 3,116, UI 568, and App 428 passing tests with zero failures; the public API baseline and enforced XML-documentation build also passed. See the [evidence artifact](../../verification/data/distribution-fitting/dataframe-series-replacement.json).

## TR-064 - distribution-fitting optimizer likelihood region

The common-data external validation fits Gumbel, Normal, and Logistic to one deterministic sample. BestFit uses differential evolution, whose stopping rule detects convergence in objective values across the population. The SciPy oracle uses a local configuration that converges in parameter or gradient space. Coordinate differences are retained as diagnostics, but are not used as statistical acceptance thresholds.

| Gumbel parameter | SciPy oracle | BestFit | Relative coordinate difference |
|---|---:|---:|---:|
| Gumbel location | 93.11234799935337 | 93.1129321294911 | 6.27e-6 |
| Gumbel scale | 13.157628567998076 | 13.157823249599968 | 1.4796e-5 |

The exact focused methods are:

- `RMC.BestFit.Verification.DistributionFitting.FittingAnalysisCriteriaVerificationTests.FittedParameters_MatchIndependentOracle`
- `RMC.BestFit.Verification.DistributionFitting.FittingAnalysisCriteriaVerificationTests.CriteriaRankingWeightsAndConfiguredOrder_MatchIndependentOracle`

**Disposition:** Rejected non-defect. No production change was made.

**Acceptance rationale:** The two fitted vectors must lie in the same joint 95% likelihood-ratio region, using \(2|\ell_{\mathrm{SciPy}}-\ell_{\mathrm{BestFit}}|\leq\chi^2_{0.95,k}\), where \(k\) is the candidate's fitted-coordinate count. This is a statistical objective-space comparison and replaces the former arbitrary scaled-coordinate tolerance. Maximum log likelihood, AIC, and BIC evaluated at the same parameter vector retain the tighter cross-language numerical tolerances of `1e-8` absolute plus `1e-7` relative. RMSE magnitudes are not compared at different optimizer-returned parameter vectors. Instead, the BestFit RMSE equation is checked directly to `1e-10`, inverse-RMSE weights are checked from the actual RMSE values to `1e-12`, and the cross-optimizer RMSE ranking must agree exactly.

**Verification:** Both focused methods passed the normalized contract in one-result guarded runs on 1 September 2026. The parameter run is recorded under `TestResults/VerificationFocused/20260901-111937-...`; the criteria, ranking, and weight run is recorded under `TestResults/VerificationFocused/20260901-111945-...`. See the [evidence artifact](../../verification/data/distribution-fitting/fitting-analysis-optimizer-precision.json).

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

**Verification:** Passed in a one-result guarded run under `20260901-111953-...`. Production Differential Evolution used untouched default tolerances. At \(n=7\), fitted \(\mu\) and \(\sigma\) are judged against central-95% intervals from the known Normal-MLE covariance, \(\operatorname{SE}(\widehat\mu)=\sigma/\sqrt n\) and \(\operatorname{SE}(\widehat\sigma)=\sigma/\sqrt{2n}\). The fitted point must also occupy the analytical optimum's joint 95% likelihood-ratio region with two degrees of freedom. The fitted 0.9 quantile uses a central-95% delta-method interval propagated through \(q=10^{\mu+z_{0.9}\sigma}\). Direct and pointwise likelihood, CDF, and analytical-quantile comparisons retain tight tolerances because they evaluate deterministic formulas at the same declared coordinates. See the [evidence artifact](../../verification/data/distribution-fitting/log10-normal-closed-form.json).

## External family-oracle execution

Thirteen exact methods in `ScipyDistributionFittingVerificationTests` and two exact methods in `LmomcoDistributionFittingVerificationTests` were run separately through the guarded runner. Each method requires the production and external optima to occupy the same joint 95% likelihood-ratio region, then verifies data log likelihood, representative CDF values, and representative quantiles at common declared coordinates with tight numerical tolerances. The C# tests consume only committed JSON and never invoke Python or R at runtime.

SciPy 1.18.1 supplies the overlapping family implementations. Generalized Pareto and Log-Pearson III use deterministic differential evolution in the generator because SciPy's default local start converged to an inferior stationary point for the declared fixtures. The Generalized Pareto free-location boundary is disclosed explicitly; its chi-square region is a conservative joint likelihood screen rather than a claim of regular coordinate-wise asymptotics. R 4.4.3 with `lmomco` 2.5.7 supplies Generalized Logistic and Generalized Normal. Parameter conversions, package versions, seeds, generator commands, and source hashes are recorded in:

- [SciPy family oracles](../../verification/data/distribution-fitting/scipy-family-oracles.json)
- [lmomco family oracles](../../verification/data/distribution-fitting/lmomco-family-oracles.json)
- [Verification data manifest](../../verification/data/MANIFEST.md)

All 15 current family-specific methods and both current multi-candidate `FittingAnalysis`
methods passed their normalized contracts in serial one-result guarded executions on 1 September 2026. Those results verify the individual-family
external/package claims and the declared common-data ranking, criteria, RMSE-formula, and weighting
claims; they do not establish a pass result for the new Chunk 6A generated-parent recovery cells.
