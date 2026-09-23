<!-- verification-status: publication-draft -->

# System Under Test and Verification Methodology

## How to read this report

The report is written for engineers and reviewers who need to understand what was tested without reading the software. Each analysis chapter begins with the question the analysis answers, describes the data and the comparison procedure, and explains the results. The equations identify the quantity being checked; the accompanying text explains its meaning. Appendix A provides the complete test index for readers who need to trace a result to its implementation.

Two kinds of data appear throughout the report. **Observed data** come from a published example or a retained measurement record. **Synthetic data** are generated from a model whose parameters are known in advance. Observed data connect the tests to engineering applications. Synthetic data make it possible to ask whether an analysis can recover a known answer. Synthetic magnitudes have no physical unit unless the chapter assigns one.

The following terms describe different parts of an analysis and should not be read as interchangeable results.

| Term | Meaning in this report |
|---|---|
| Parameter | A quantity that defines a model, such as a mean, a standard deviation, or a trend slope. |
| Generating value, or parent value | The parameter chosen before generating synthetic observations; it is the known target in a recovery experiment. |
| Likelihood | A measure of how well a specified set of parameters explains the observed data. A log likelihood is the logarithm of this measure. |
| Prior and posterior | The prior represents information about parameters before using the study data. The posterior combines that information with the data. |
| Posterior draw | One sampled set of parameter values from the posterior. Many draws describe uncertainty; they are not additional observations. |
| Quantile | The value associated with a stated probability. For annual maxima, the 0.99 quantile is exceeded with annual probability 0.01, often called the 100-year event. |
| Confidence interval | An interval constructed by a procedure intended to include the fixed true value at a stated rate over repeated samples. |
| Credible interval | An interval containing a stated share of the posterior probability for a parameter or response. |
| Predictive interval or band | An interval or curve band for new observations, including their variability as well as the uncertainty included by the specified method. |
| Independent reference, also called an oracle | An analytical answer, published result, or calculation made separately from the BestFit calculation being checked. |

An exceedance probability is a probability for each year under the model; a 100-year event is not a prediction that one such event occurs at regular 100-year intervals. A confidence or credible interval for a flood quantile is also different from a predictive interval for a future flood.

## System under test

RMC.BestFit combines data management, statistical models, estimation, and presentation of results. Its numerical calculations use the RMC.Numerics library. This report examines both individual calculations and complete analyses: a correct distribution formula is necessary, but the data preparation, fitting, and uncertainty calculations must also work together.

| Component | Source-review configuration |
|---|---|
| RMC.BestFit | Version 2.0.0; commit `8808f19f3bfa712241e55a0dfa47407ff5a60970` |
| RMC.Numerics source | Commit `7e8e8d1c5f26e045a35ec9fc09367de95ed05b02` |
| Declared package baseline | RMC.Numerics 2.2.0 |
| Runtime | .NET on Windows, 64-bit process |
| Principal Bayesian sampler | DEMCzs, with the configuration declared for each test design |
| Reproducibility | Explicit generators, retained sample sizes, seeds, and acceptance rules |

Local builds can resolve the sibling Numerics source project instead of the package. These are distinct dependency configurations. A run's recorded configuration identifies which was used; package compatibility and numerical results are not inferred solely from a source build.

The report describes the current tests and their supporting evidence. A result marked **passed** means that the recorded check met its stated numerical or statistical acceptance rule. A table headed **reference value** reports the comparison target, not an unrecorded BestFit estimate. Where an execution record preserves the pass outcome but not the fitted values, the chapter reports the target and the accepted comparison without inventing an estimate. Source-review dates do not establish that every method has been rerun against one common binary.

## Verification and validation

**Verification** asks whether software implements its specified equations and algorithms correctly. **Validation** asks whether a model adequately represents a physical process for its intended use. This report primarily provides verification. Synthetic recovery experiments assess behavior under a known data-generating law; they do not establish that the law is suitable for a particular watershed.

## Evidence types

| Evidence | Question answered |
|---|---|
| Analytical calculation | Does the implementation reproduce a value that can be calculated directly from a formula? |
| Independent calculation | Does it agree with a separately written numerical calculation? |
| External scientific package | Does it reproduce an equivalent calculation in a documented R or Python package? |
| Published source | Does it reproduce a worked example or benchmark with a defined parameterization? |
| Recovery experiment | Does estimation recover known generating parameters or responses under the specified design? |

The comparison procedure has four parts: specify the data and model, establish an independent reference, perform the BestFit calculation, and compare the results using a rule fixed for that test. Finishing an analysis or producing a plausible graph does not establish accuracy. Conversely, a test deliberately designed to expose an unsupported behavior can pass by confirming that limitation; the report identifies those cases explicitly.

## Deterministic comparisons

For a reference value $r$ and software value $s$, absolute and relative errors are

$$
e_{\mathrm{abs}}=|s-r|,\qquad e_{\mathrm{rel}}=\frac{|s-r|}{\max(|r|,e_0)}.\tag{M.1}
$$

Here $r$ is the reference, $s$ is the BestFit result, and $e_0$ prevents division by zero when the reference is close to zero. For example, an absolute tolerance of $10^{-8}$ permits a difference no greater than 0.00000001. This is a check of arithmetic agreement, not an allowance for measurement error. For a matrix, the rule ordinarily applies to every entry.

Comparing two calculations at the **same parameter values** isolates formula implementation. Comparing two **fitted sets of parameters** also tests optimization and can use an uncertainty-based criterion. A fit accepted within a 95% region is not thereby shown to reproduce every printed reference digit.

## Recovery experiments

In a recovery experiment, a known model generates the observations and BestFit estimates that model from those observations. Random sampling means that an estimate ordinarily differs from its generating value. The tests therefore assess the difference relative to its uncertainty. For directly estimated maximum-likelihood parameters, a common rule is

$$
\left|\frac{\widehat\theta_j-\theta_{0j}}{\operatorname{SE}(\widehat\theta_j)}\right|\leq 1.96.\tag{M.2}
$$

The symbol $\widehat\theta_j$ denotes the fitted parameter, $\theta_{0j}$ its generating value, and SE its estimated standard error. Thus equation (M.2) requires the difference to be no more than 1.96 standard errors. Other tests assess the entire parameter set using a joint likelihood-ratio region, or vary one parameter while refitting the others to obtain a profile interval. The chapter states which rule applies.

Bayesian recovery checks ordinarily require the generating value inside the central 95% posterior interval, together with the sampling diagnostics described below. Sometimes several combinations of parameters describe nearly the same response. In those cases the test can assess the engineering quantity directly, such as the 0.99 quantile or the discharge curve, rather than requiring every coefficient to be determined separately.

Sample size counts observations, not MCMC draws. For spatial recovery, 100 ten-site vectors contain 1,000 scalar observations but only 100 multivariate row/year likelihood contributions. For point processes, the observation period and event count are reported separately.

## Interval coverage

Recovery and interval coverage answer different questions. Across $B$ independent repetitions, let $I_b$ indicate whether an interval contains its generating truth. Empirical coverage and its Monte Carlo standard error at a nominal target $C_0$ are

$$
\widehat C=\frac{1}{B}\sum_{b=1}^{B}I_b,\qquad
\operatorname{MCSE}(\widehat C)=\sqrt{\frac{C_0(1-C_0)}{B}}.\tag{M.3}
$$

Here $\widehat C$ is the observed fraction of successful inclusions, and MCSE describes its sampling uncertainty. For example, testing one generated dataset and finding its true value inside a 95% interval is a recovery result. Showing that such intervals include the truth about 95% of the time requires many independently generated datasets. This distinction applies to the Bulletin 17C, rating-curve, and spatial results in this report.

## Bayesian diagnostics

Bayesian estimation uses Markov chain Monte Carlo (MCMC) to sample parameter uncertainty. Several chains explore the same posterior. The convergence statistic $\widehat R$ compares variation within and between chains; a value close to one is desirable. Effective sample size (ESS) estimates how much information remains after accounting for dependence between successive draws. A long chain can therefore have a small ESS.

The usual recovery requirements are $\widehat R<1.10$ and ESS of at least 100 for every monitored parameter. These diagnostics support interpretation of the posterior intervals but do not prove model suitability. The diagnostic calculations themselves are compared with the independent R package `posterior`. Predictive checks based on leave-one-out calculations are compared with R `loo`; the estimation chapter explains both their numerical results and their reliability warnings.

## Reproducibility and report coverage

Independent data and numerical references are stored beneath `verification/data/` with generators, environments, parameter conversions, and hashes. The [verification catalog](../verification-catalog.json) records method identities, scientific references, acceptance rules, and evidence locations. Appendix A maps every current test method to a report chapter. The PDF also embeds the current catalog and document metadata as attachments, retaining the complete acceptance and provenance record with the report.

The test index is a traceability aid. The analysis chapters carry the scientific explanation; a reader need not understand a test name or open a source file to understand the experiment. Separate software regression tests check data preservation, saved projects, and error handling. Documentation checks protect the report's links, metadata, and code descriptions. These controls support the analysis workflow and are distinguished from the numerical comparisons reported here.
