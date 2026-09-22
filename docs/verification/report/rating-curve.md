<!-- verification-status: publication-draft -->

# Rating-Curve Analysis

A rating curve converts a measured river stage into discharge. The practical verification question is
whether BestFit can reproduce a specified stage-discharge relation, including the additional flow that
begins when water reaches a second or third hydraulic control. These examples use synthetic gaugings
with known generating curves, so both the discharge calculation and the fitted uncertainty can be checked.

The 19 methods comprise three likelihood comparisons, three analytical continuity checks, three
worked-example maximum-likelihood comparisons, and five generating-model designs fitted by both maximum
likelihood and Bayesian estimation. The numerical examples retain their supplied stage and
discharge units. Activation stages have stage units, exponents are dimensionless, and a control
coefficient has discharge units divided by stage units raised to its exponent.

## Discharge-space likelihood

### Example setup and control definitions

The worked example starts with 300 daily pairs dated from 1 January 2000. Stored workbook draws place
stage uniformly between 1 and 20. At each stage, discharge is calculated from the controls below and
multiplied by a random base-10 lognormal error. The standard deviation of log10 error is 0.05. These
are simulated measurements, not observations from a gauging station.

| Hydraulic control | Activation stage $h_k$ | Discharge coefficient $\alpha_k$ | Exponent $\beta_k$ | Included in |
|---|---:|---:|---:|---|
| Triangular main channel | 1 | 1.753463 | 2.666667 | All three models |
| First rectangular overbank | 10 | 883.689894 | 1.67 | Two- and three-control models |
| Second rectangular overbank | 15 | 4069.426757 | 1.67 | Three-control model |

The workbook describes rectangular overbank widths of 20 and 50 ft. The numerical comparison uses its
stored coefficients and stage-discharge values unchanged; it does not independently validate the
hydraulic derivation of those coefficients. Previously active controls continue carrying flow when a
new control activates. With $K$ controls, the median discharge and observation law are

$$
q(h)=\sum_{k=1}^{K}\alpha_k(h-h_k)^{\beta_k}\mathbf{1}_{h>h_k},
\qquad \log_{10}Q_i=\log_{10}q(h_i)+\epsilon_i,
\quad \epsilon_i\sim N(0,\sigma^2).\tag{R.1}
$$

Here $q(h)$ is the underlying median rating relation; $Q_i$ is a simulated observed discharge, and
$\sigma$ describes measurement scatter on the log10 scale. The indicator contributes zero until that
control's activation stage is exceeded. BestFit estimates $a_k=\log_{10}\alpha_k$ internally.

### Comparison and results

The independent Python/SciPy calculation evaluates the probability density of every positive
discharge. Transforming the observations to log10 discharge requires a change-of-variables factor
$1/(Q_i\ln 10)$ when expressing the density in discharge units. Omitting that factor would change
reported likelihood values and model-comparison criteria, even though it would not move the optimum.

| Model, 300 observations | Independent discharge-space log likelihood at generating truth | Absolute tolerance | BestFit result |
|---|---:|---:|---|
| One control | -1577.7594156516989 | $10^{-8}$ | Passed |
| Two controls | -1850.3134085800252 | $10^{-8}$ | Passed |
| Three controls | -1888.5687896304166 | $10^{-8}$ | Passed |

BestFit's total, per-observation, and component likelihood calculations reproduce the independent
discharge-space density. A separate base-10 LogNormal density calculation also reproduces the terms.
This verifies the observation-density calculation at the declared parameters; it is separate from
whether an estimator can find suitable parameters from data.

## Activation continuity

A practitioner expects discharge to change smoothly as water first reaches an overbank. The
continuity checks isolate the added control's contribution just below and above its activation stage.
For positive exponent $\beta_k$, that contribution is $\alpha_k\epsilon^{\beta_k}$ at a stage offset
$\epsilon$ above activation and approaches zero as the offset shrinks.

Three analytical checks cover the following cases.

| Setup | Independent target and result |
|---|---|
| Second control; exponents 0.1, 1, 1.67, and 2.5; offsets $10^{-3}$, $10^{-6}$, and $10^{-9}$ | The two-sided discharge increment equals the main-channel increment plus the added power-law term, within $10^{-10}$ relative to the discharge scale. Passed. |
| Deliberate zero exponent | The new control jumps by its full coefficient at activation. The analytical jump is reproduced within $10^{-9}$ relative. Passed. This demonstrates why zero must be excluded. |
| Three controls at the default exponent lower bound, 0.1 | Added contributions decrease as the stage offset shrinks from $10^{-6}$ to $10^{-12}$. Passed. |

For the last case, the analytical ratio is

$$
(10^{-12}/10^{-6})^{0.1}=0.2511886432.\tag{R.2}
$$

The small exponent approaches zero slowly, but it remains continuous. Fast tests separately check
that the default bounds exclude zero. Continuity alone does not establish that the chosen controls
adequately describe a real channel.

## Recovery

### Fitting the three worked-example models

The three maximum-likelihood tests use a **separate 1,000-pair replication** of the workbook recipe,
with generator seed 20260822, stages between 1 and 20, and log10 error standard deviation 0.05. The
independent calculation minimizes squared log10 residuals from multiple starting points in SciPy and
estimates the error scale from those residuals. BestFit fits the same data with Differential Evolution,
unchanged optimizer tolerances, and the example's parameter bounds and prior settings.

The following are generating values and **independent SciPy comparison targets**, not a table of
BestFit fitted coordinates.

| Quantity | Generating value | One-control reference fit | Two-control reference fit | Three-control reference fit |
|---|---:|---:|---:|---:|
| Main-channel activation $h_1$ | 1 | 1.000633 | 1.000989 | 1.001032 |
| Main-channel log coefficient $a_1$ | 0.243897 | 0.244804 | 0.246160 | 0.246300 |
| Main-channel exponent $\beta_1$ | 2.666667 | 2.664527 | 2.661335 | 2.660905 |
| First overbank activation $h_2$ | 10 | - | 9.981416 | 9.936705 |
| First overbank log coefficient $a_2$ | 2.946300 | - | 2.936743 | 2.899111 |
| First overbank exponent $\beta_2$ | 1.67 | - | 1.678032 | 1.730227 |
| Second overbank activation $h_3$ | 15 | - | - | 15.332091 |
| Second overbank log coefficient $a_3$ | 3.609533 | - | - | 3.828382 |
| Second overbank exponent $\beta_3$ | 1.67 | - | - | 1.341175 |
| Log10 error standard deviation $\sigma$ | 0.05 | 0.052251 | 0.052240 | 0.051935 |

First, BestFit's likelihood at the independent optimum must agree within $10^{-8}$. Second, the
BestFit optimum must lie within the joint 95% likelihood-ratio region around that independent optimum,
and the generating parent must lie within the corresponding region around the BestFit optimum.
The likelihood-ratio comparison measures the loss of fit for the entire parameter combination;
its cutoffs are 9.487729, 14.067140, and 18.307038 for 4, 7, and 10 fitted quantities, respectively.

All three comparisons pass. The available results do not include BestFit's fitted
coordinates, so the reference values above must not be presented as observed BestFit output. The
three-control reference fit also illustrates why individual control parameters can move appreciably
while combined flow remains constrained by the gaugings.

## Generating-model recovery

### Five calibration designs

These ten tests ask whether estimation recovers the known curve under distinct combinations of
calibration range, observation noise, and overbank activation. Each design contains 1,000 aligned
stage-discharge pairs with independent Normal error in log10 discharge. Stage is treated as exact.
Both estimators use the same data within each design. The following coefficients are on the physical
discharge scale, rather than their internally stored logarithms.

| Design and generator seed | Calibration-stage range | Known controls: activation; coefficient; exponent | Log10 error SD |
|---|---|---|---:|
| Standard single control; 12345 | 1-10 | Main channel: 0.5; 10; 2 | 0.05 |
| Low-noise single control; 54321 | 1-12 | Main channel: 0.3; 15; 1.8 | 0.02 |
| Wide calibration range; 99999 | 1-25 | Main channel: 0; 5; 2 | 0.05 |
| Bankfull transition; 44444 | 0.5-12 | Main channel: 0.3; 12; 2.2. Overbank: 6; 190.4; 1.6667 | 0.04 |
| Three-control activation; 66666 | 0.5-10 | Low flow: 0.2; 5; 2.5. Main channel: 3; 50; 1.6667. Overbank: 7; 150; 1.6667 | 0.04 |

The response is checked at the following stages, specified before examining fitted results.

| Design | Stages used for the complete discharge comparison |
|---|---|
| Standard single control | 1.25, 3, 6, 9.5 |
| Low-noise single control | 1.25, 3, 7, 11.5 |
| Wide calibration range | 1.25, 5, 15, 24.5 |
| Bankfull transition | 0.75, 2, 5.5, 6.5, 9, 11.5 |
| Three-control activation | 0.75, 2, 2.75, 3.25, 5.5, 6.75, 7.25, 8.5, 9.75 |

The bankfull sample has 495 observations below stage 6 and 505 at or above it. The three-control
sample has 270 below stage 3, 406 between stages 3 and 7, and 324 at or above 7. Thus the highest
control is informed by fewer gaugings than the entire curve. These counts help explain identification;
they are not substitutes for the covariance or posterior used to calculate uncertainty.

### Estimation and acceptance

For a single control, every MLE parameter must be within 1.96 estimated standard errors of its parent.
The standard errors come from the unregularized curvature of the fitted likelihood. In multi-control
models, several hydraulic quantities can compensate for each other; the coordinate rule therefore
applies to the identifiable error scale, while the combined discharge is checked on the complete grid.

MLE response uncertainty is formed from 20,000 bounded multivariate-Normal parameter draws using the
full fitted covariance, using seed 20260831. Independent log10 observation errors, using seed
20260901, are then added to each simulated curve.
The largest standardized departure across all stages in each draw determines a single simultaneous
95% predictive band. This makes the acceptance statement apply to the whole declared grid.

Bayesian estimation uses retained posterior draws for parameter uncertainty and independent log10
errors with seed 20260902 for observation uncertainty. Its simultaneous predictive band is centred on the posterior-mode
(MAP) curve. Every sampled parameter must have $\widehat R<1.10$ and effective sample size at least
100; these assess chain agreement and the amount of independent information in the draws. All
single-control parent parameters, and the multi-control error scale, must lie in their central 95%
posterior intervals. Every parent must be inside its prior support. These recovery designs keep
their prior settings with automatic flat priors and the additional Jeffreys scale rule disabled.

### Results and interpretation

All five MLE and all five Bayesian tests pass. The MLE outputs for the three single-control
designs provide direct fitted-versus-parent comparisons:

| Design | Zero-flow stage: parent / fitted | Log10 coefficient: parent / fitted | Exponent: parent / fitted | Error SD: parent / fitted |
|---|---|---|---|---|
| Standard | 0.5 / 0.490708 | 1 / 0.991367 | 2 / 2.009298 | 0.05 / 0.049202 |
| Low noise | 0.3 / 0.309034 | 1.176091 / 1.180771 | 1.8 / 1.796011 | 0.02 / 0.019849 |
| Wide range | 0 / -0.026289 | 0.698970 / 0.680915 | 2 / 2.013448 | 0.05 / 0.049292 |

Selected Bayesian discharge outputs and multi-control intervals are shown
below. These are examples from the full-grid checks, not replacements for checking every stage.

| Design and stage | Parent discharge | BestFit Bayesian MAP discharge | BestFit simultaneous 95% Bayesian predictive band |
|---|---:|---:|---|
| Standard, stage 6 | 302.500 | 302.276 | [227.740, 401.206] |
| Low noise, stage 7 | 460.284 | 460.611 | [410.797, 516.466] |
| Wide range, stage 15 | 1125.000 | 1123.643 | [845.862, 1492.647] |
| Bankfull transition, stage 6.5 | 724.391 | 741.266 | [576.299, 953.454] |
| Three-control activation, stage 8.5 | 2144.093 | 2197.933 | [1701.739, 2838.807] |

The MLE predictive bands at the last two comparison points are [578.013, 950.262] and
[1695.479, 2851.016], respectively, and both contain the generating discharge. For the low-noise design,
the narrower relative predictive band is consistent with its smaller observation-error scale. In the
multi-control designs, passing response checks does not establish separate recovery of every hydraulic
coefficient or activation stage.

These are inclusion checks for fixed generated datasets. They do not estimate how often a nominal
95% band covers truth over repeated new datasets, and they do not establish calibration beyond the
observed stage range. Equivalence with R `bdrc` is not claimed: matching control law, error density,
segmentation, and parameterization has not been established.
