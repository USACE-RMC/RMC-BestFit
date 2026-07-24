<!-- technical-reference-status: complete -->

# Glossary

[Technical reference](../index.md) | [Notation](notation.md) | [Parameterization crosswalk](parameterization-crosswalk.md)

This glossary fixes the vocabulary used across statistical, hydrologic, and API discussions. Local chapters may narrow a definition but should not silently change it.

| Term | Meaning in this reference |
|---|---|
| Aleatory variability | Variation represented as intrinsic randomness under the selected probability model. It is conditional on the model and should not be used as a label for all unexplained error. |
| Annual exceedance probability (AEP) | Probability that an annual quantity exceeds a stated magnitude in one year under the stated stationarity and trial assumptions. |
| Bayesian posterior | Probability distribution proportional to the implemented likelihood times the implemented prior. Posterior summaries are conditional on both. |
| Censoring | Observation of a bound or interval rather than an exact magnitude. Censoring contributes probability mass through a CDF or survival-function difference. |
| Confidence interval | Repeated-sampling interval procedure with stated coverage under a specified data-generating process. It is not synonymous with a Bayesian credible interval. |
| Credible interval | Posterior probability interval conditional on data, likelihood, priors, and computation. |
| Data likelihood | Probability density or mass assigned to observed information, excluding parameter priors. Chapters identify noncanonical implementation decompositions explicitly. |
| Epistemic uncertainty | Uncertainty arising from limited knowledge, model structure, parameter information, or measurement. The label does not by itself define a probability model. |
| Exact observation | Magnitude treated as observed without censoring or a measurement-error distribution. |
| Exposure | Time, area, or opportunity over which events could occur; required to interpret point-process event rates. |
| Frequency curve | Mapping between exceedance probability or return period and a model quantile. It is conditional on model assumptions and input units. |
| Historical information | Flood information outside the systematic gage record, interpreted with a documented historical period and perception process. |
| Identifiability | Ability of the likelihood and prior structure to distinguish parameter values or model components. A converged optimizer does not establish identifiability. |
| Inference from margins (IFM) | Two-stage copula method that treats previously estimated marginal models as fixed while estimating dependence. |
| Link function | Monotone or otherwise defined mapping between natural parameter space and a working regression space. |
| Low outlier | Observation treated through the configured low-outlier/censoring rule. It is not silently deleted from the scientific record. |
| Maximum a posteriori (MAP) | Parameter vector maximizing the implemented posterior kernel, or the highest-target saved draw when that is the documented API convention. |
| Maximum likelihood estimate (MLE) | Parameter vector maximizing the implemented data likelihood within its bounds. |
| Measurement-error model | Probability distribution for an uncertain observation whose convolution with the parent distribution defines its likelihood contribution. |
| Nonexceedance probability | \(F(x)=P(X\le x)\). For AEP \(p_E\), the corresponding quantile probability is \(1-p_E\). |
| Nonstationarity | Explicit dependence of one or more model parameters on time or covariates. It does not arise merely because a sample contains a trend. |
| Parameter prior | Density assigned directly to a fitted coordinate. Quantile priors and penalties are derived terms and are documented separately. |
| Perception threshold | Time-varying magnitude above or below which an event would have been observed or recorded during a historical period. |
| Pointwise likelihood unit | Predictive unit returned by `PointwiseDataLogLikelihood` for WAIC, LOO, or diagnostics. It may be an observation, event pair, or spatial row depending on the model. |
| Posterior predictive distribution | Distribution of replicated or future data after averaging the data model over the posterior parameter distribution. |
| Prior predictive distribution | Distribution implied by the sampling model averaged over the prior before conditioning on the observed sample. |
| Pseudo-likelihood | Objective assembled from transformed observations or component densities that is not the full joint-data likelihood. Its uncertainty interpretation must be stated. |
| Quantile prior | Prior information expressed on one or more distribution quantiles and transformed into the fitted parameter coordinates with the implemented Jacobian convention. |
| Return period | \(T=1/p_E\) under the applicable stationary annual-trial convention. It is an average recurrence measure, not a deterministic schedule. |
| Systematic record | Period during which observations were collected under a regular measurement program with documented completeness. |
| Tail dependence | Limiting probability of one variable being extreme conditional on another being extreme. Central correlation is not a substitute. |
| Threshold observation | Count or record summarized relative to perception bounds; not the same as a single interval-censored observation. |
| Uncertain observation | Observation represented by a measurement distribution rather than one exact value. |
| Verification evidence | Reproducible comparison with theory, a published result, an official standard, or an independently implemented calculation. A test name alone is not evidence of scientific agreement. |

---

[Technical reference](../index.md) | [Notation](notation.md) | [Parameterization crosswalk](parameterization-crosswalk.md)
