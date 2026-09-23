<!-- verification-status: publication-draft -->

# Executive Summary

## Purpose

This report explains how RMC.BestFit's statistical analyses are tested, what the comparisons show, and where the evidence stops. It is intended for engineers and reviewers who need to assess the software without reading its code. Each analysis chapter describes the observations or generated data, the calculation performed by BestFit, the independent reference, and the meaning of the result.

The central question is whether the software performs its specified calculations correctly. Choosing a model that adequately represents a watershed, measurement process, or risk decision remains a separate engineering judgment.

## Scope and principal findings

The tests combine direct mathematical answers, comparisons with independent R and Python software, published flood-frequency examples, and experiments with known generating models. For example, seven Bulletin 17C examples reproduce 21 published parameter values within 0.001; diagnostic calculations reproduce independent R results to tight numerical tolerances; and generated-data experiments check whether known parameters or responses lie within their stated uncertainty intervals.

| Capability | Scientific evidence | Supported result |
|---|---|---|
| Univariate families and distribution fitting | Fifteen-family recovery, SciPy and R comparisons, analytical identities, and common-data model ranking | Distribution parameterizations, likelihoods, fitted results, and criteria satisfy the specified checks. |
| Estimation and diagnostics | Analytical posteriors and covariance; R `loo`, `posterior`, `gmm`, and `bbmle` | Estimator objectives, profiling, information criteria, and convergence diagnostics reproduce independent references. |
| Bulletin 17C | Published examples, PeakFQ plotting positions, moment covariance, regional penalties, and synthetic recovery | Seven example parameter vectors and the specified moment and penalty calculations satisfy their acceptance rules. |
| Point process, competing risks, mixtures, and composites | Independent likelihoods, dependence identities, external packages, and generating-model recovery | Event rates, combinations of processes, and propagation of uncertainty satisfy the checks for the specified designs. |
| Bivariate and coincident frequency | Independent dependence-model fits, simulated recovery, and response calculations | The tested fits and probabilities of responses driven by two variables match their references. |
| Rating curves | SciPy likelihoods and optima, continuity identities, and parameter and response recovery | One-, two-, and three-control designs satisfy the likelihood and recovery criteria. |
| Time-series models | Independent transforms, recurrences, likelihoods, forecasts, and recovery | AR, MA, ARIMA, and ARIMAX calculations satisfy the specified checks. |
| Spatial extremes | Independent Gaussian-copula and process calculations, uncertainty propagation, and ten-site recovery | The tested likelihood, prediction, distance, and uncertainty calculations match their references. |

The main chapters cover all fifteen analysis types: distribution fitting, univariate, Bulletin 17C, point process, competing risk, mixture, composite, bivariate, coincident frequency, rating curve, autoregressive, moving average, ARIMA, ARIMAX, and spatial extremes. The estimation chapter explains the shared fitting and diagnostic calculations. The data chapters explain checks that preserve measurements, dates, missing values, and observation types before analysis.

Appendix A maps all **328 current test methods in 71 classes** to these explanations. The catalog records 326 verified methods and two tests of accepted limitations. These are inventory counts, not probabilities that a model is correct or percentages of statistical interval coverage.

## How to interpret the results

Analytical and external-package comparisons provide the most direct checks of a numerical calculation. Recovery experiments ask whether a fitted model can recover its generating parameters or responses under a declared sample size, seed, and estimator configuration. A single successful recovery experiment does not demonstrate repeated-sample confidence-interval coverage.

The two accepted limitations concern joint-prior sampling and one-step GMM deletion influence. The generic sampler draws parameter priors independently and therefore does not represent additional coupled prior factors. GMM influence correctly ranks the tested outlier but its magnitude is not calibrated to exact deletion scale. The relevant checks document these boundaries; they do not remove them.

Bulletin 17C bootstrap diagnostics also distinguish delivered fits from converged fits. In the inspected 1,000-refit experiment, all outputs were accepted by the delivery workflow, while 947 met outer convergence and 53 reached the iteration cap. Interval coverage and universal bootstrap convergence are not established by that experiment.

The analysis chapters explain these and other model-specific boundaries. The report's conclusions are restricted to the stated calculations, data designs, and acceptance rules.
