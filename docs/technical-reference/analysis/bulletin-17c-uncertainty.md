<!-- technical-reference-status: complete -->

# Bulletin 17C Uncertainty, Calibration, and Diagnostics

[Previous: estimation](bulletin-17c-estimation.md) | [Bulletin 17C overview](bulletin-17c.md) | [Technical Reference](../index.md)

`Bulletin17CAnalysis` propagates GMM sampling uncertainty by generating an ensemble of valid parent-distribution parameter sets. The public enum offers direct multivariate normal, linked multivariate normal, parametric bootstrap, and a studentized pivotal bootstrap. These are frequentist sampling distributions and produce confidence intervals. They are not posterior draws and do not produce Bayesian credible intervals, even though compatibility properties retain `BayesianAnalysis`, `MCMCResults`, `MAP`, `PosteriorMean`, and `CredibleIntervalWidth` names.

## Asymptotic Parameter Covariance

Let

$$
\mathbf D=\frac{\partial\overline{\mathbf g}_n}{\partial\boldsymbol\theta^\mathsf T},
\qquad
\mathbf H=\frac{\partial^2P}{\partial\boldsymbol\theta\partial\boldsymbol\theta^\mathsf T}, \tag{1}
$$

where the penalty Hessian already contains its $1/n$ scaling. `GeneralizedMethodOfMoments.GetCovariance` constructs the bread

$$
\mathbf B=\mathbf D^\mathsf T\mathbf W\mathbf D+\mathbf H. \tag{2}
$$

The default robust covariance is

$$
\widehat{\operatorname{Var}}(\widehat{\boldsymbol\theta})
=\frac1n\mathbf B^{-1}\mathbf M\mathbf B^{-1}, \tag{3}
$$

with

$$
\mathbf M=mathbf D^\mathsf T\mathbf W\mathbf S\mathbf W\mathbf D
+I_R\mathbf H. \tag{4}
$$

$I_R=1$ when `PenaltyIsRandom` is true—the default appropriate to a regional estimate with sampling error—and zero for deterministic regularization. If `sandwich` is false, the implementation returns $\mathbf B^{-1}/n$. Matrices are regularized to be symmetric positive definite before inversion. A covariance that remains nonfinite or lacks a positive finite diagonal is unusable for pivotal sampling.

## The Four Uncertainty Engines

Let $B$ be `BayesianAnalysis.OutputLength`, $c$ be `BayesianAnalysis.CredibleIntervalWidth`, and $s$ be `BayesianAnalysis.PRNGSeed`. In this analysis those properties mean requested ensemble size, confidence level, and reproducibility seed.

### Direct multivariate normal

The direct method assumes

$$
\boldsymbol\theta^{(b)}\sim
N_p\!\left(\widehat{\boldsymbol\theta},
\widehat{\boldsymbol\Sigma}_{\theta}\right). \tag{5}
$$

It uses Latin-hypercube normal draws seeded by $s$. Every draw is checked against the Numerics parent distribution. An invalid Latin-hypercube draw receives up to ten ordinary MVN retries with deterministic per-index seeds. Rejected entries are omitted rather than replaced by the point estimate. If more than half of requested draws are rejected, or fewer than two survive, the method aborts uncertainty construction and retains only the GMM point estimate.

### Linked multivariate normal

The default method transforms each parameter through a distribution-aware link $\boldsymbol\eta=\mathbf h(\boldsymbol\theta)$. With diagonal analytical link Jacobian $\mathbf J_h$,

$$
\widehat{\boldsymbol\Sigma}_{\eta}
=\mathbf J_h(\widehat{\boldsymbol\theta})
\widehat{\boldsymbol\Sigma}_{\theta}
\mathbf J_h(\widehat{\boldsymbol\theta})^\mathsf T, \tag{6}
$$

and the algorithm samples

$$
\boldsymbol\eta^{(b)}\sim
N_p(\widehat{\boldsymbol\eta},\widehat{\boldsymbol\Sigma}_{\eta}),
\qquad
\boldsymbol\theta^{(b)}=\mathbf h^{-1}(\boldsymbol\eta^{(b)}). \tag{7}
$$

The exact link assignment is:

| Parent/parameter | Implemented link policy |
|---|---|
| Gamma scale and shape | positive-support `LogASinHLink`; relative standard error controls skew/tail settings |
| Pearson III/LP3 location | `ASinHLink`; indicator $0.5\widehat\gamma+\mathrm{WEDS}_{\mu}$, capped by `EpsilonMax = 0.5` |
| Pearson III/LP3 scale | the same positive-support relative-SE `LogASinHLink` |
| Pearson III/LP3 skew | `ASinHLink`; fitted-skew direction, WEDS magnitude, asymmetric caps, and an SE-dependent tail parameter |
| Normal, Log-Normal, Exponential location | `ASinHLink` driven by signed location WEDS, with `EpsilonMax = 0.5` |
| Normal, Log-Normal, Exponential scale | positive-support relative-SE `LogASinHLink` |

WEDS is the weighted error-direction score calculated from natural-parameter pointwise moment errors before temporary links are installed. Positive-parameter links use relative standard error so their tuning is invariant to measurement units. The supporting [link-function chapter](../support/link-functions.md) defines the `ASinHLink` and `LogASinHLink` maps and derivatives.

Linked draws undergo the same validity/retry logic as direct MVN. If the linked covariance cannot be sampled or the linked method otherwise returns null, `RunUncertaintyQuantificationAsync` silently attempts direct MVN and emits only a debug message for that fallback. Users who require an auditable record of the selected engine should retain the diagnostic log as well as the serialized configuration.

The links and their tuning constants are implementation-specific variance-stabilization heuristics, not formulas prescribed by Bulletin 17C or Cohn et al. Their calibration must be supported by coverage evidence over the intended data regimes [1], [2].

### Parametric bootstrap

For each replicate $b$ the code:

1. simulates a data frame from the fitted parent while retaining the source observation-information structure through `DataFrame.BootstrapDataFrame`;
2. clones the Bulletin 17C model, reprocesses its data, and randomizes enabled penalty targets;
3. refits by GMM;
4. rejects fits whose squared Mahalanobis distance from the parent estimate exceeds the $1-1/(5B)$ quantile of $\chi^2_p$; and
5. retries at most ten times.

The configured external-information target is resampled with standard deviation $\sqrt{\mathrm{MSE}}$. `SetRandomPenaltyFunction` centers parameter targets on the fitted parent parameter and quantile targets on the fitted parent quantile. This propagates both record and external-information uncertainty.

If all ten attempts fail, the implementation inserts the parent parameter vector and records a failed replicate. Thus the returned array still contains exactly $B$ finite entries. This preserves operational completion but creates point mass at the parent estimate and can narrow intervals. The failure count and retry/rejection counts must accompany any published interval.

### `BiasCorrectedBootstrap`: implemented pivotal bootstrap

Despite the enum name, this branch is a link-space studentized pivotal construction, not the ordinary bias-corrected percentile (BC) or acceleration-corrected BCa algorithm. It has three phases:

1. Generate and refit $B$ parametric bootstrap samples, retaining $\widehat{\boldsymbol\theta}^{*(b)}$ and its covariance $\widehat{\boldsymbol\Sigma}^{*(b)}$. Failed replicates fall back to the parent estimate and covariance.
2. Fit a Yeo-Johnson link to location samples and, when present, shape samples; use a log link for scale. A failed or boundary-railed Yeo-Johnson fit falls back to identity.
3. In link space, let $\widehat{\boldsymbol\eta}=h(\widehat{\boldsymbol\theta})$, and let $\mathbf L$ and $\mathbf L_b^*$ be Cholesky factors of the parent and replicate link-space covariances. Form

$$
\mathbf z_b=(\mathbf L_b^*)^{-1}
(\widehat{\boldsymbol\eta}-\widehat{\boldsymbol\eta}^{*(b)}), \tag{8}
$$

add independent normal smoothing with standard deviation $0.01/\sqrt p$, clip every component to $[-6,6]$, and return

$$
\boldsymbol\theta^{(b)}
=h^{-1}\!\left(\widehat{\boldsymbol\eta}+\mathbf L\mathbf z_b\right). \tag{9}
$$

Invalid phase-three transforms remain unset. Because bootstrap methods require exactly $B$ valid output sets before publishing, any unset final entries cause the entire uncertainty result to be withheld. The enum/XML-description mismatch is recorded as a production review finding.

## From Parameter Ensemble to Frequency Curves

After sampling, `RunUncertaintyQuantificationAsync` removes null, dimensionally invalid, and nonfinite sets; sanitizes nonfinite `Fitness` fields; and constructs an `MCMCResults` compatibility object. Its “MAP” is the GMM point estimate. Its “posterior mean” is the arithmetic mean of the uncertainty ensemble. Neither quantity is Bayesian in this workflow.

At annual exceedance probability $\alpha$, each retained parent draw produces

$$
q_{\alpha}^{(b)}=F_X^{-1}(1-\alpha\mid\boldsymbol\theta^{(b)}). \tag{10}
$$

`UncertaintyAnalysisResults` summarizes these draws with pointwise empirical confidence limits using the configured level $c$. It stores frequency results rather than parameter sets again (`recordParameterSets: false`). Changing probability ordinates or the interval level reprocesses the stored ensemble without rerunning GMM or resampling.

Report the following with every result: parent family, data units, low-outlier rule and threshold, all perception thresholds and record lengths, penalty targets and MSEs, uncertainty engine, $B$, $s$, confidence level, rejection/fallback counts, covariance warnings, and whether linked MVN fell back to direct MVN.

## Cohn-Style Diagnostic Intervals

`ComputeCohnStyleConfidenceIntervals()` is a separate public diagnostic and does not supply the main `AnalysisResults`. It uses two-node-per-dimension nested quadrature around the GMM estimate. At each outer point it recomputes covariance, constructs an inner grid, estimates the covariance of quantile and quantile standard error, and applies a Cohn-style adjusted Student-$t$ formula with regression coefficient $\beta_1$ and effective degrees of freedom $\nu$ [2]. It enforces monotone lower and upper curves afterward.

The implementation's quantile helper always instantiates a Pearson III distribution in log space and exponentiates interval bounds by $10$. It is therefore mathematically an LP3 diagnostic. The public method does not guard against the five other supported parent families; use outside LP3 is an open high-severity review finding. The companion asymptotic-quantile-variance report uses the same hard-coded helper.

## Lifecycle, Cancellation, and Failure Semantics

`RunAsync` waits for in-flight result reprocessing, clears prior state, preprocesses thresholds, runs GMM off the calling thread, sets `IsEstimated`, runs uncertainty once, then builds frequency results. Cancellation clears partial state. An exception clears state and is rethrown. A GMM point estimate can remain marked estimated even when uncertainty fails, in which case `AnalysisResults` remains null and `UncertaintyDiagnosticMessage` explains the degradation.

Changing `UncertaintyMethod` clears results. `CancelAnalysis()` cancels the outer analysis and parallel sampling. Bootstrap output is all-or-nothing; MVN output may contain fewer than $B$ draws when the rejection rate is no greater than 50 percent, so reported Monte Carlo resolution must use the retained count rather than the requested count.

## Validation Evidence and Required Calibration

Fast unit tests cover configuration, serialization, linked-function behavior, WEDS direction, Yeo-Johnson fallback, result DTOs, and report diagnostics. The long-running Verification source contains parent-family covariance checks, seven worked-example parameter checks, uncensored and censored coverage experiments, and bootstrap comparisons. It was not executed during this documentation work.

Before peer-review release, coverage summaries must be generated by the user from narrowly selected Verification tests and archived with seeds, parent parameters, sample sizes, censoring designs, interval levels, replicate counts, and tolerances. In particular, linked-MVN tuning, Mahalanobis truncation, failed-replicate replacement, and pivotal smoothing/clipping require empirical calibration; source-code intent alone is not validation.

Implementation symbols: `Bulletin17CAnalysis.RunAsync`, `RunUncertaintyQuantificationAsync`, `GetParameterSetsFromMultivariateNormal`, `GetParameterSetsFromLinkedMultivariateNormal`, `GetParameterSetsFromParametricBootstrap`, `GetParameterSetsFromPivotalBootstrap`, `ComputeCohnStyleConfidenceIntervals`, and `GeneralizedMethodOfMoments.GetCovariance`.

## References

<a id="ref-1"></a>[1] J. F. England, Jr. et al., *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*, U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 2019. doi: 10.3133/tm4B5.

<a id="ref-2"></a>[2] T. A. Cohn, W. L. Lane, and J. R. Stedinger, “Confidence intervals for Expected Moments Algorithm flood quantile estimates,” *Water Resources Research*, vol. 37, no. 6, pp. 1695–1706, 2001.

<a id="ref-3"></a>[3] T. J. DiCiccio and B. Efron, “Bootstrap confidence intervals,” *Statistical Science*, vol. 11, no. 3, pp. 189–228, 1996.

<a id="ref-4"></a>[4] I.-K. Yeo and R. A. Johnson, “A new family of power transformations to improve normality or symmetry,” *Biometrika*, vol. 87, no. 4, pp. 954–959, 2000.

<a id="ref-5"></a>[5] M. C. Jones and A. Pewsey, “Sinh-arcsinh distributions,” *Biometrika*, vol. 96, no. 4, pp. 761–780, 2009.

---

[Previous: estimation](bulletin-17c-estimation.md) | [Bulletin 17C overview](bulletin-17c.md)
