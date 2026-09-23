<!-- verification-status: publication-draft -->

# Time-Series Analyses

Time-series models describe how an observation depends on earlier observations, earlier unexplained
shocks, and external information such as a dated covariate. Verification must therefore check more
than a fitted coefficient: a correct fit can still produce a wrong forecast if dates, differences,
transformations, or the last observed value are handled incorrectly.

This chapter describes 24 verification methods: eight estimator-recovery tests, twelve
shared calculation checks, and four additional model-interaction checks. The recovery data are
synthetic numerical series, with no assigned discharge or other physical unit. An innovation is the
new, unpredictable Normal error at a time step; its standard deviation is measured on the model's
working scale.

## AR, MA, ARIMA, and ARIMAX oracle design

An autoregressive model (AR) carries forward departures of past observations from their mean.
A moving-average model (MA) carries forward past innovations. ARIMA combines those mechanisms after
differencing, which means subtracting the preceding value to model change rather than level.
ARIMAX adds external, dated covariates. In ARIMA$(p,d,q)$, the three orders are the number of
autoregressive lags, the number of differences, and the number of moving-average lags.

The independent R generator discards 110 initialization steps before retaining 1,000 observations
for each recovery design. AR and MA use monthly observations. ARIMA and ARIMAX use daily observations;
one difference leaves 999 model-scale changes before further conditioning on lags. The starting
state and innovation history are specified in the generator, rather than obtained from BestFit.

### Shared calculation checks

The twelve checks below isolate operations used across the four model families. An independent
reference calculation is called an oracle. These calculations are specified directly in R or in
the test mathematics, rather than obtained by calling another BestFit analysis.

| Calculation and setup | Independent comparison and outcome |
|---|---|
| Scale prior, all four families | Four scalar evaluations verify the Jeffreys scale contribution, whose log density includes $-\ln\sigma$, and its metadata to $10^{-12}$. Passed. |
| Invalid innovation scale, all four families | Positive-scale Gaussian density and prior values agree to $10^{-12}$; zero and negative scales are rejected as impossible in every declared scalar and per-observation path. Passed. |
| Automatically fitted Box-Cox and Yeo-Johnson transforms | R fits the transformation using six training observations only. Changing the three later observations cannot change the fitted transformation. Saved/restored settings are also checked. Passed. |
| Manually specified transformation | Eight training values, Yeo-Johnson power 0.6, AR coefficient 0.35, MA coefficient -0.25, and innovation SD 0.8 are transformed independently before reconstructing the likelihood. Passed. |
| Dated ARIMAX likelihood | An R calculation independently matches response differences with same-date covariates, accounts for lags and transformation density, and checks every likelihood contribution to $10^{-10}$. Changing holdout responses leaves the training likelihood unchanged. Passed. |
| Reconstructing levels after differencing | Short ARIMA and ARIMAX examples deliberately depart from the recurrence within training. Hand calculations require forecasts to begin at the last observed training level, without using the holdout value. Agreement is within $10^{-10}$. Passed. |
| Forecast uncertainty at the training boundary | An independently calculated innovation-variance recurrence checks when forecast uncertainty starts accumulating. Variances and standard deviations agree to $10^{-10}$. Passed. |
| Transformed stochastic forecasts | Forecasts evolve on the transformed model scale and undergo one inverse transformation. Deterministic values agree to $10^{-10}$; 1,000-seed moment comparisons meet their analytical sampling-error bounds. Passed. |
| Transformed AR and MA generation | Independent algebraic recurrences and Gaussian mean/variance calculations check generated series, including 1,000-seed moment experiments. Passed. |
| Transformed and differenced ARIMA generation | An independent recurrence checks changes, reconstructed levels, and final-horizon Gaussian moments over 1,000 seeds. Passed. |
| Transformed and differenced ARIMAX generation | Dates, covariate contributions, model-scale changes, and reconstruction are independently calculated; deterministic and 1,000-seed moment checks pass. |
| Information criteria, all four families | AIC and BIC are recalculated from observation likelihood at the stored MAP, excluding the prior. Agreement is within $10^{-10}$, and the prior-inclusive alternative is discriminated. Associated flat-prior Normal fits also satisfy declared uncertainty and joint-likelihood checks. Passed. |

For the automatically fitted transformations, the Box-Cox training values are 1.1, 1.3, 1.8, 2.7, 5,
and 12; the independent fitted power is -0.5411999130. The Yeo-Johnson training values are -4, -2,
-0.5, 0.5, 2, and 4; the fitted power is approximately 1. Both automatic and manual comparisons use
the stored cross-language rule of $10^{-8}$ absolute or $10^{-7}$ relative agreement. The holdout
experiment establishes that future observations do not leak into transformation fitting.

A small second-difference example makes the forecast-boundary question concrete. The observed
training levels are 1, 4, and 10. With a next second difference of 2, the first forecast is 18 and
the next is 28. A later stored value of 999 is a deliberate holdout sentinel and must not influence
either forecast. In the uncertainty check, a random walk with innovation variance 1 has forecast
variances 1, 2, and 3 at horizons one, two, and three. Its in-training conditional variances do not
accumulate as if the historical observations were unobserved forecasts.

## Recovery

Each family is fitted by maximum likelihood (MLE) and Bayesian estimation. For Bayesian recovery,
every generating parameter must lie in its central 95% posterior interval, every $\widehat R$ must
be below 1.10, and every effective sample size must be at least 100. These diagnostics check agreement
between chains and the independent information in the retained draws. The production sampler settings
and declared priors are retained. MLE acceptance is stated separately for each model below.

### Autoregressive model

**Setup.** The AR(1) series contains 1,000 monthly observations beginning in January 2002, generated
with seed 12345. Its long-run mean is $\mu=10$, lag-one coefficient is $\phi=0.6$, and innovation
standard deviation is $\sigma=5$. There is no response transform, differencing, or covariate.

$$
Y_t=\mu+\phi(Y_{t-1}-\mu)+\epsilon_t,
\qquad \epsilon_t\sim N(0,\sigma^2).\tag{TS.1}
$$

**Procedure.** BestFit estimates the mean, lag coefficient, and innovation scale from the retained
series. The MLE uses the required Differential Evolution configuration and unregularized observed
information: each fitted quantity must be within 1.96 estimated standard errors of its parent, and
the fitted likelihood must be at least as high as the parent likelihood. Bayesian estimation uses
the interval and diagnostic rules above. A separate Python least-squares calculation supplies a
closed-form conditional optimum.

**Results and meaning.** Both recovery tests pass. The independent optimum is mean 10.127594,
lag coefficient 0.610755, and innovation SD 4.939809. These are external reference values. At the generating parameters, the
independent next observation with no new innovation is 5.421625410, and BestFit's recurrence agrees
within $10^{-12}$. The result checks persistence about a stable mean, not a trending or seasonal AR model.

### Moving-average model

**Setup.** The MA(1) series contains 1,000 monthly observations beginning in January 2003, with seed
12345, mean $\mu=10$, lag-one innovation coefficient $\theta=0.6$, and innovation SD $\sigma=5$.
There is no transform, differencing, or covariate. Its specified positive-sign convention is

$$
Y_t=\mu+\epsilon_t+\theta\epsilon_{t-1}.\tag{TS.2}
$$

**Procedure.** Innovations are reconstructed recursively from the observations. The conditional
sum-of-squares objective is the sum of their squared values, with the Normal density supplying the
likelihood and scale terms. BestFit fits the three quantities by MLE and Bayesian estimation.
MLE requires each parent within 1.96 unregularized observed-information standard errors and a
likelihood no worse than at the parent. Independent Python optimization evaluates the same recurrence.

**Results and meaning.** Both recovery tests pass. The independent reference optimum is mean
10.073259, MA coefficient 0.610861, and innovation SD 4.941178. The next zero-innovation value at the
generating parameters is 5.547955597 and is reproduced within $10^{-12}$. Unlike AR persistence, this
model's direct dependence on a shock ends after the declared MA lag; the recurrence test checks that distinction.

### ARIMA model

**Setup.** The ARIMA(1,1,1) example contains 1,000 positive daily values beginning on 1 January 2004,
with initial level 100 and seed 51037. It takes the natural logarithm of the response, then one
difference. The resulting changes have AR coefficient 0.45, MA coefficient 0.25, and innovation SD
0.04. There is no intercept or drift and no covariate. Thus the error scale is in log-response-change
units, not the units of the original positive observations.

**Procedure.** R independently generates the changes and optimizes their conditional likelihood,
including the transformation density. BestFit fits the same three parameters. Its maximum must be
within the joint 95% likelihood-ratio region of the independent R maximum, with cutoff 7.814728.
Each generating parameter must also fall within its independent one-parameter 95% likelihood profile,
which allows the other parameters to be refitted while that parameter is varied. Bayesian recovery
uses the common central-95% and diagnostic rules, plus an independent data/prior likelihood comparison.

| Parameter | Generating value | Independent R optimum | Independent 95% profile interval |
|---|---:|---:|---|
| AR coefficient | 0.45 | 0.400116 | [0.311504, 0.484293] |
| MA coefficient | 0.25 | 0.323088 | [0.232573, 0.408449] |
| Innovation SD | 0.04 | 0.039205 | [0.037546, 0.040990] |

**Results and meaning.** Both recovery tests pass. At the generating parameters, the next
zero-innovation log difference is -0.0290494944510721. Adding it to the last observed log level and
exponentiating gives the independent next-response target 332.259097907995; BestFit agrees within
$10^{-10}$. This target verifies reconstruction at known parameters, not an observed forecast from
a fitted parameter set. The tests support the stated log/difference combination and its boundary handling.

### ARIMAX model

**Setup.** The ARIMAX(1,1,0) series contains 1,000 daily levels beginning on 1 January 2005, with
initial level 10 and seed 51038. It uses one difference, no response transform, and no MA term.
A dimensionless dated covariate is the sum of a 17-day sine wave and half a 31-day cosine wave.
The expected daily change is $m_t=0.25+1.5X_t$; departures from that expectation have AR coefficient
0.4 and innovation SD 0.5. The covariate stays on its original level scale and is matched by date.

**Procedure.** Both estimators fit the change intercept, covariate effect, AR coefficient, and error
scale. The production and independent R maxima must occupy the four-parameter joint 95%
likelihood-ratio region, and the generating point must be in that region; the cutoff is 9.487729.
Independent calculations also check same-point likelihood, prior contribution, and the date-aligned
forecast. Bayesian recovery applies the same interval and diagnostic rules used above.

| Parameter | Generating value | Independent R optimum |
|---|---:|---:|
| Mean-change intercept | 0.25 | 0.236897 |
| Covariate coefficient | 1.5 | 1.540371 |
| AR coefficient | 0.4 | 0.364772 |
| Innovation SD | 0.5 | 0.505265 |

**Results and meaning.** Both recovery tests pass. At the generating parameters, the next
covariate value is -0.9204878758, the next zero-innovation change is -1.2555476231495, and the
reconstructed response is 251.211255651498. BestFit agrees with the forecast recurrence within
$10^{-10}$. The values in the table are independent R estimates.

## Current time-series coverage

Four additional Python comparisons extend beyond the first-order recovery cases. They evaluate
known parameter settings and independent objectives; they do not add unperformed higher-order
estimator-recovery claims.

| Check | Setup | Numerical target and result |
|---|---|---|
| First-order conditional objectives | The AR(1) and MA(1) recovery series above; independent residuals, optimum, likelihood, and observed-information uncertainty | Parent and optimum likelihoods and residual prefixes agree within $10^{-9}$. The independent parent standardized errors are all below 1.96. Passed. |
| Second-order AR and MA | 1,000 values after 110 discarded steps. AR(2): mean 12, lag coefficients 0.55 and -0.22, error SD 1.75. MA(2): mean 12, lag coefficients 0.5 and -0.28, error SD 1.75 | One-step targets are 12.104644614 and 12.027055310. Residuals, likelihoods, and responses agree within $10^{-9}$; independently calculated roots confirm stable/invertible responses. Passed. |
| Pure AR and MA through ARIMA | The same second-order series evaluated as ARIMA(2,0,0) and ARIMA(0,0,2), without differencing | Both have 998 scored conditional terms. The ARIMA MA initialization differs from the standalone MA's 1,000-term convention and is checked explicitly. Likelihoods and forecasts agree within $10^{-9}$. Passed. |
| Trend, seasonality, and two covariates together | ARIMAX(1,0,1), 1,000 monthly observations from January 2000. Intercept 20; trend 0.015 per step; annual sine/cosine coefficients 3 and -1.5; covariate coefficients 1.25 and -0.8; AR 0.45; MA 0.3; error SD 1.2 | Correct date alignment gives log likelihood -1597.134695090. Deliberately matching by array position gives -939039.172443445. The correct 999-term result agrees within $10^{-9}$. Passed. |

For the last case, the covariates begin one and two months before the response series. This creates
a test that can detect matching values by position rather than calendar date. The second-order
checks also examine how a single shock propagates through future values, rather than accepting a
likelihood comparison alone. The first-order objective check includes an inferior AR optimizer
coordinate as a negative control: independently recalculating its likelihood prevents a
reported optimizer success from being mistaken for scientific recovery.

## Conclusion

The eight recovery tests pass their declared parameter-uncertainty and forecast-recurrence rules.
The sixteen independent calculation checks support the specified transformations, date alignment,
likelihoods, forecast boundaries, generators, and criteria. The available recovery results do not include
a full table of BestFit estimates and credible intervals; external optima are therefore labelled
as references throughout this chapter.

A known-parent inclusion result for one dataset does not measure repeated-sample 95% interval
coverage. The separate simulation-moment checks assess generated means and variances, not fitted
interval coverage. Practical forecast conclusions remain conditional on the tested orders,
chronology, transforms, covariates, and Normal innovation law.
