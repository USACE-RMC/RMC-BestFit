# Univariate distribution analysis

Use these examples to connect flood evidence, model assumptions and saved frequency results. Begin with a stationary Bayesian fit. Add historical information, measurement uncertainty or trends only after understanding how the observations enter the model. The specialized Bulletin 17C analyses use GMM with frequentist uncertainty; the collection contains both methods.

## Choose an example

| Learning task | Tutorial | What to inspect |
|---|---|---|
| First stationary Bayesian fit | [ARR/FLIKE examples](1-univariate-analysis/2-arr-flike/arr-flike-examples.md) | LP3 and GEV alternatives, historical information and skew priors. |
| Historical and causal information | [Viglione et al.](1-univariate-analysis/1-information-expansion/viglione-et-al-2013.md) | Observation windows, interval events and independently justified quantile priors. |
| Familiar U.S. flood datasets | [Bayesian Bulletin 17C datasets](1-univariate-analysis/3-bulletin17C-examples/bulletin-17c-bayesian-examples.md) | Six Bayesian fits and the restored Back Creek GMM comparison. |
| Uncertain record extension | [Sinnemahoning Bayesian](1-univariate-analysis/4-measurement-errors/sinnemahoning-move3-bayesian.md) | Measured years versus reconstructed years and their measurement-error distributions. |
| Study background awaiting a project | [Blakely Bayesian reference](1-univariate-analysis/1-information-expansion/blakely-mountain-dam-bayesian.md) | Published three-day inflow context; the intended saved Bayesian project is not available here. |
| GMM and frequentist uncertainty | [Bulletin 17C collection](2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.md) | Seven datasets, multivariate-normal and bias-corrected-bootstrap results. |
| Skew and quantile penalties | [Blakely B17C](2-bulletin-17C-analysis/2-information-expansion/blakely-mountain-dam-b17c.md) | One-day inflows, historical/paleoflood evidence, imperial and metric projects, and a retained optimizer warning. |
| GMM with uncertain observations | [Sinnemahoning B17C](2-bulletin-17C-analysis/3-measurement-errors/sinnemahoning-move3-b17c.md) | Exact versus uncertain extensions and companion Bayesian fits. |
| Nonstationary annual peaks | [Brays Bayou](1-univariate-analysis/5-nonstationary-ffa/nsffa-brays-bayou-texas.md) | Conditional frequency curves, alternative trend functions and a saved model average. |
| Trends and external priors | [OC Fisher](1-univariate-analysis/5-nonstationary-ffa/nsffa-oc-fisher-dam.md) | Stationary/nonstationary priors, a long-period sinusoid and an unpopulated model-average result. |
| Recognizing trend forms | [Synthetic nonstationary data](1-univariate-analysis/5-nonstationary-ffa/nsffa-synthetic-data.md) | Nine distinct datasets; descriptive fitted examples, not a new recovery experiment. |
| Peaks over a threshold | [Point-process precipitation](3-point-process-analysis/point-process-examples.md) | Exposure, block-year starts, threshold selection and seasonal event rates. |
| Multiple populations | [Mixture distributions](4-mixture-analysis/mixture-distribution-examples.md) | Two/three Normal components and a fixed empirical zero mass. |
| Combining flood mechanisms | [Snow/rain composite models](5-composite-analysis/mixed-population-examples.md) | Maximum competing risks versus a weighted mixture and the data each requires. |

## Read a frequency result

1. Open the tutorial's linked project with **File > Open** and save a working copy. Select the exact input and analysis names shown in the guide.
2. Read the variable, units, years, missing periods, low-outlier flags and threshold windows. A window's duration is not a count of measured floods.
3. Identify the estimator. Bayesian credible limits describe parameter or quantile uncertainty; GMM confidence limits use a different uncertainty construction. Neither interval is the range containing the corresponding percentage of future floods.
4. Inspect the saved diagnostics and warnings. For Bayesian fits, examine every parameter's trace, R-hat, effective sample size and tail uncertainty. Do not treat one summary cutoff or a completed run as acceptance.
5. For a trend model, identify the evaluation time/index and covariates. A conditional annual probability does not describe the entire historical record or a multi-year risk by itself.

The tutorials preserve saved results, including incomplete or questionable cases. They do not establish EMA equivalence or design acceptance. Review the [author issue log](../../docs/example-issues-for-haden.md) before adopting a teaching configuration in a study.

All displayed figures come from the shared Python plotting utilities and saved BestFit geometry. See [figure reproduction](../README.md#reproducing-the-figures). Continue to [bivariate analysis](../5-bivariate-distribution-analysis/README.md) when the question involves two variables or a joint response.
