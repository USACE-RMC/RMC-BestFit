<!-- technical-reference-status: complete -->

# Bulletin 17C Uncertainty, Calibration, and Diagnostics

[Previous: estimation](bulletin-17c-estimation.md) | [Bulletin 17C overview](bulletin-17c.md) | [Technical Reference](../index.md)

`Bulletin17CAnalysis` propagates GMM sampling uncertainty by generating an ensemble of valid parent-distribution parameter sets. The public enum offers direct multivariate normal, linked multivariate normal, parametric bootstrap, and `BiasCorrectedBootstrap`, whose full technical name is the **bias-corrected pivotal bootstrap**. BestFit reports these as GMM uncertainty ensembles and forms confidence intervals. Smith and Stedinger [6] give the pivotal ensemble an objective/generalized-posterior interpretation when it is paired with the estimator and covariance under the paper's stated conditions; the shared `BayesianAnalysis`, `MCMCResults`, `MAP`, `PosteriorMean`, and `CredibleIntervalWidth` names remain compatibility terminology rather than evidence of an MCMC analysis.

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
2. clones the Bulletin 17C model with that data frame so bounds, links, and penalty configuration are preserved, then randomizes enabled penalty targets once;
3. constructs bounded-midpoint, ROS, distribution-default, and parent-fit starting candidates;
4. ranks finite distinct candidates by the identity-weight penalized GMM objective;
5. refits by GMM from each candidate against the same simulated data and penalty target;
6. only after every candidate is exhausted, generates a fresh realization, for at most ten realizations.

The configured external-information target is resampled with standard deviation $\sqrt{\mathrm{MSE}}$. `SetRandomPenaltyFunction` centers parameter targets on the fitted parent parameter and quantile targets on the fitted parent quantile. This propagates both record and external-information uncertainty.

If all ten realizations fail, the implementation inserts the parent parameter vector and records a failed replicate. Thus the returned array still contains exactly $B$ finite entries, as required by downstream post-processing. This fallback is retained deliberately as an output-length guarantee, not as evidence of a successful refit. It creates point mass at the parent estimate and can narrow intervals; robust initialization is intended to make it exceptional, and the failure, retry, and optimizer-fallback counts must accompany any published interval.

### `BiasCorrectedBootstrap`: bias-corrected pivotal bootstrap

The compact enum member and GUI label describe the method's purpose and are retained for practitioner clarity. Technically, this branch implements the joint bias-corrected pivotal bootstrap of Smith and Stedinger [6], not Efron's scalar bias-corrected percentile (BC) or acceleration-corrected BCa endpoint algorithm. It has three phases:

1. Generate and refit $B$ parametric bootstrap samples with the same ranked candidate strategy, retaining $\widehat{\boldsymbol\theta}^{*(b)}$ and its covariance $\widehat{\boldsymbol\Sigma}^{*(b)}$. Every candidate must also yield a finite regularized covariance and Cholesky factor. A refit that exhausts all candidates and all ten realizations falls back to the parent estimate and covariance to preserve $B$ outputs and increments the failure counter.
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

The two covariance factors are the bias correction. Standardizing with each replicate's $\mathbf L_b^*$ removes local bias and heteroskedasticity from the refit distribution; re-inflating with the parent's $\mathbf L$ expresses the corrected draw in the parent's uncertainty while preserving joint parameter dependence. The pending paper describes the bare construction as exact for an identifiable subclass and second-order, $O(n^{-1})$, under its regularity, link, matching-target, and estimator-covariance pairing conditions. BestFit's Yeo-Johnson adaptation, smoothing/clipping, post-inverse model-bound repair, and failed-refit policy are additional operational choices. The former Mahalanobis truncation was removed under TR-019 after the unguarded reliability sweep.

After inverse linking, non-finite components use the corresponding parent-fit value and out-of-bound components are moved just inside the model's existing lower and upper bounds before non-throwing validation. Any draw that still fails the distribution domain remains unset. Because bootstrap methods require exactly $B$ valid output sets before publishing, any unset final entries cause the entire uncertainty result to be withheld. The enum and XML value are intentionally retained for compatibility; technical documentation uses the full name “bias-corrected pivotal bootstrap” and distinguishes it from BC/BCa.

## From Parameter Ensemble to Frequency Curves

After sampling, `RunUncertaintyQuantificationAsync` removes null, dimensionally invalid, and nonfinite sets; sanitizes nonfinite `Fitness` fields; and constructs an `MCMCResults` compatibility object. This deliberately reuses the same result-storage architecture as Bayesian analyses for persistence and downstream reprocessing. In the Bulletin 17C context, `MAP` is the penalized GMM point estimate, `Output` is the frequentist uncertainty ensemble, `PosteriorMean` is its arithmetic mean, and `CredibleIntervalWidth` is the confidence level. The legacy names do not make these quantities Bayesian, and TR-016 requires no code/API or serialization redesign.

At annual exceedance probability $\alpha$, each retained parent draw produces

$$
q_{\alpha}^{(b)}=F_X^{-1}(1-\alpha\mid\boldsymbol\theta^{(b)}). \tag{10}
$$

`UncertaintyAnalysisResults` summarizes these draws with pointwise empirical confidence limits using the configured level $c$. It stores frequency results rather than parameter sets again (`recordParameterSets: false`). Changing probability ordinates or the interval level reprocesses the stored ensemble without rerunning GMM or resampling.

Report the following with every result: parent family, data units, low-outlier rule and threshold, all perception thresholds and record lengths, penalty targets and MSEs, uncertainty engine, $B$, $s$, confidence level, retry/fallback counts, covariance warnings, and whether linked MVN fell back to direct MVN.

## Cohn-Style Diagnostic Intervals

`ComputeCohnStyleConfidenceIntervals()` is a separate public diagnostic and does not supply the main `AnalysisResults`. It uses two-node-per-dimension nested quadrature around the GMM estimate. At each outer point it recomputes covariance, constructs an inner grid, estimates the covariance of quantile and quantile standard error, and applies a Cohn-style adjusted Student-$t$ formula with regression coefficient $\beta_1$ and effective degrees of freedom $\nu$ [2]. It enforces monotone lower and upper curves afterward.

The quantile helper instantiates Pearson III in base-10 logarithmic space and exponentiates interval bounds by $10$, so the diagnostic is mathematically limited to LP3. `ComputeCohnStyleConfidenceIntervals()` now checks that scope before using the helper: it throws `NotSupportedException` for the five non-LP3 parents and for LP3 data containing low outliers, uncertain observations, interval censoring, or threshold censoring. The report-side asymptotic-quantile-variance calculation uses the same guard and prints an unavailable reason instead of applying LP3 formulas. Cohn value/parity verification remains deferred.

## Lifecycle, Cancellation, and Failure Semantics

`RunAsync` waits for in-flight result reprocessing, clears prior state, preprocesses thresholds, runs GMM off the calling thread, sets `IsEstimated`, runs uncertainty once, then builds frequency results. Cancellation clears partial state. An exception clears state and is rethrown. A GMM point estimate can remain marked estimated even when uncertainty fails, in which case `AnalysisResults` remains null and `UncertaintyDiagnosticMessage` explains the degradation.

Changing `UncertaintyMethod` clears results. `CancelAnalysis()` cancels the outer analysis and parallel sampling. Bootstrap output is all-or-nothing; MVN output may contain fewer than $B$ draws when the rejection rate is no greater than 50 percent, so reported Monte Carlo resolution must use the retained count rather than the requested count.

## Validation Evidence and Required Calibration

Fast unit tests cover configuration, serialization, linked-function behavior, WEDS direction, Yeo-Johnson fallback, pivotal bounds repair, result DTOs, report diagnostics, midpoint-moment construction, ranked candidate validity/order, and the Cohn scope guard for all unsupported parents and data conditions. Fourteen seeded reliability cells for ordinary and pivotal bootstrap across Examples 1 through 7 were executed independently: 1,000 outputs per method for Examples 1-6 and 500 per method for highly censored Example 7. The final unguarded sweep produced 13,000 finite outputs from exactly 13,000 realizations with zero retries, Mahalanobis rejections, optimizer fallbacks, parent substitutions, failed GMM candidates, and final first-chance exceptions from Numerics or RMC.BestFit.

The formal current-path parameter-parity source is `B17CExampleTests.Test_Example1` through `Test_Example7`. Each compares LP3 GMM mean, standard deviation, and skewness with the published Bulletin 17C worked-example values at absolute tolerance `1E-3`. All seven exact methods passed on 28 July 2026 with zero failures or skips. Parent-family covariance checks, uncertain-data variants, pointwise/aggregate moment consistency, coverage experiments, and Cohn interval-value verification are separate claims.

Before peer-review release, compare the bare pivotal construction conditionally with its paired objective/generalized-posterior target, with seeds, parent parameters, sample sizes, censoring designs, interval levels, replicate counts, and tolerances archived. Linked-MVN tuning, failed-replicate replacement, pivotal smoothing/clipping, and the model-bound repair require separate sensitivity evidence. Existing repeated-sampling coverage experiments may characterize deployed interval behavior, but coverage alone is not proof of the pivotal bias correction described in [6].

Implementation symbols: `Bulletin17CAnalysis.RunAsync`, `RunUncertaintyQuantificationAsync`, `GetParameterSetsFromMultivariateNormal`, `GetParameterSetsFromLinkedMultivariateNormal`, `GetParameterSetsFromParametricBootstrap`, `GetParameterSetsFromPivotalBootstrap`, `ComputeCohnStyleConfidenceIntervals`, and `GeneralizedMethodOfMoments.GetCovariance`.

## References

<a id="ref-1"></a>[1] J. F. England, Jr. et al., *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*, U.S. Geological Survey Techniques and Methods, book 4, chap. B5, 2019. doi: 10.3133/tm4B5.

<a id="ref-2"></a>[2] T. A. Cohn, W. L. Lane, and J. R. Stedinger, “Confidence intervals for Expected Moments Algorithm flood quantile estimates,” *Water Resources Research*, vol. 37, no. 6, pp. 1695–1706, 2001.

<a id="ref-3"></a>[3] T. J. DiCiccio and B. Efron, “Bootstrap confidence intervals,” *Statistical Science*, vol. 11, no. 3, pp. 189–228, 1996.

<a id="ref-4"></a>[4] I.-K. Yeo and R. A. Johnson, “A new family of power transformations to improve normality or symmetry,” *Biometrika*, vol. 87, no. 4, pp. 954–959, 2000.

<a id="ref-5"></a>[5] M. C. Jones and A. Pewsey, “Sinh-arcsinh distributions,” *Biometrika*, vol. 96, no. 4, pp. 761–780, 2009.

<a id="ref-6"></a>[6] C. H. Smith and J. R. Stedinger, “A Bias-Corrected Pivotal Bootstrap for Objective-Bayes Parameter Ensembles,” manuscript in preparation, 2026.

---

[Previous: estimation](bulletin-17c-estimation.md) | [Bulletin 17C overview](bulletin-17c.md)
