<!-- verification-status: publication-draft -->

# Spatial-Extremes Analysis

Regional flood analysis uses information from several sites. The practical questions are whether
BestFit represents dependence between sites, handles a missing observation without inventing a flood,
and carries uncertainty into predictions at an ungauged location or into a regional frequency curve.

The examples below use synthetic networks with specified generalized extreme-value (GEV)
distributions. Location controls the distribution's level, scale its spread, and shape its tail.
Flow magnitudes are in consistent numerical units, without assignment to a named
river or a discharge unit. Projected recovery-network distances are in kilometres. A Gaussian
copula joins the site distributions through dependence between their Normal-transformed probabilities;
a latent Gaussian process instead describes spatial departures in a parameter such as location.
Those two uses of spatial correlation are checked separately.

The chapter covers 31 verification methods, supported by independent R or Python calculations,
analytical targets, or known generating parents.

## Likelihood oracle

### Network and missing-data setup

The observation-likelihood example contains twelve row/year blocks at five sites. A row is the
collection of floods for one synthetic year. All sites have GEV location 100, scale 30, and shape
-0.1 in the Hosking/Numerics sign convention. Location and scale are represented by natural-log
parameters internally, while shape is used directly. Exponential copula correlation is
$\rho(d)=\exp(-d/15)$, where $d$ is distance in the projected coordinate units.

| Site | Projected X coordinate | Projected Y coordinate |
|---|---:|---:|
| 1 | 0 | 0 |
| 2 | 12 | 3 |
| 3 | 25 | 0 |
| 4 | 8 | 18 |
| 5 | 30 | 22 |

Some years omit one or more sites. R evaluates the GEV densities of the observations that exist and
the Gaussian-copula density using only those sites' correlation submatrix. This is marginalization:
the absent site is integrated out, not replaced by zero or another placeholder. A wholly missing
year contributes zero to the log likelihood.

| Row/year | Sites with observations | Independent row log-likelihood target |
|---:|---|---:|
| 5 | 1, 3, 4, 5 | -23.7536861453931 |
| 6 | 2, 3, 4 | -14.894701 |
| 7 | 1, 2, 4, 5 | -21.970089 |
| 8 | 1, 5 | -9.952734 |
| 9 | None | 0 |

### Comparisons and results

Eight methods assess the observation density and the separation between data and prior information.
A companion seven-complete-row example removes missingness. A further example gives each site's
log-location a fixed spatial departure: 0.05, -0.03, 0.02, -0.04, and 0.01 at sites 1-5. The Gaussian
process for those departures has standard deviation 0.06 and range 20. Its probability density is
a process/prior contribution, not an extra observed flood.

| Comparison | Independent target or rule | BestFit result |
|---|---|---|
| Missing-site total likelihood | -238.89272936973; absolute agreement within $10^{-8}$ | Passed |
| Missing-site contribution of each year | Each R row within $10^{-10}$; row sum equals total within $10^{-8}$ | Passed |
| Seven complete rows with copula | Total -168.321519223767; total tolerance $10^{-8}$ and row tolerance $10^{-10}$ | Passed |
| GEV margins without copula | Missing-data total -238.363666518069 within $10^{-8}$ | Passed |
| Observation likelihood with location departures | Observation-only total -169.071665098156 within $10^{-8}$, excluding process log density 8.46428373793472 | Passed |
| Equivalent posterior decompositions | Data plus prior gives the same posterior kernel within $10^{-8}$ under both decompositions | Passed |
| Total and component decomposition | Year contributions sum to data likelihood; prior components sum to prior; data plus prior equals posterior, within $10^{-8}$ | Passed |
| Likelihood gradients | Independently calculated small-perturbation derivatives of the total and summed rows agree within $10^{-4}$ | Passed |

These results check probability calculations for the stated missingness patterns and parameter
settings. They do not demonstrate that ignoring an unobserved site's value is appropriate for every
missing-data mechanism in an actual flood record.

### Information criteria

A separate Bayesian fit of the missing-site example checks that model-comparison criteria use
annual vectors as their observation units. There are twelve rows, of which eleven contain data.
AIC and BIC are independently reconstructed from the observation likelihood at the sampled
posterior mode (MAP); BIC uses eleven nonempty years. WAIC is independently reconstructed from
the retained per-year likelihoods. One Pareto diagnostic for leave-one-out assessment is retained
per row, including the empty row.

BestFit reports data log likelihood -237.5295, AIC 483.0590, BIC 484.6506, and WAIC 482.7811.
AIC/BIC agree with independent calculations within $10^{-8}$, and WAIC and its effective-parameter
term agree within $10^{-9}$ relative. Maximum $\widehat R$ is 1.0004 and minimum
effective sample size is 9,233. These are BestFit estimation outputs, not the fixed likelihood
targets in the previous table. The criteria check passes.

## Prediction, distance, uncertainty, and recovery

### Distance and conditional spatial prediction

Before fitting or prediction can be trusted, distances and spatial interpolation must be correct.
Two methods check distance conventions. Cartesian distances on the five-site network equal direct
Euclidean distances exactly. The geodesic example uses latitude/longitude pairs (38.90, -77.04),
(39.29, -76.61), (40.44, -79.99), (37.54, -77.44), and (41.88, -87.63). R applies the haversine formula
with Earth radius 6,371.0088 km. Distances agree within $10^{-9}$ km; exponential correlations with
150-km range and five conditional prediction moments agree within $10^{-10}$. Both methods pass.

A third method checks simple kriging: the conditional mean and remaining variance of a Gaussian
spatial departure at a target, given the departures at observed sites. R independently solves the
covariance equations on the five-site projected network. Five targets, including an observed site,
are evaluated under three process settings: SD/range 0.06/20, 0.15/6, and 0.30/80, giving fifteen
comparisons. All means and variances agree within $10^{-10}$; uncertainty at the observed site is
zero within $10^{-9}$.

For example, at target (10, 10) under the first setting, the independent conditional departure is
-0.02367145054 with variance 0.001297213485. Adding the departure to log-location $\ln 100$ and
exponentiating gives physical location 97.66065206. These reference quantities show why prediction
must distinguish a log-scale departure from a physical flow parameter.

### Dependent simulation

The simulation check configures a five-site GEV model with exponential copula range 35 and generates
20,000 joint site vectors using seed 20260822. No estimator is run in this check. Each simulated
value is mapped through its configured GEV distribution to a Normal score, so empirical intersite
correlations can be compared with the specified copula matrix independently of marginal skewness.

Every pairwise correlation is within 0.02 of its configured target. At every site, the empirical
10th, 50th, and 90th percentiles are within 3% of the analytical GEV quantiles. The check passes.
It supports the stated simulation law and tolerances, not a fitted model's prediction accuracy
for an external river network.

### N=1000 recovery

The recovery experiments contain ten sites with 100 complete observations per site. Thus there
are 1,000 scalar flood values, but only 100 ten-site row/year vectors. With a copula, each year is
one multivariate likelihood contribution; neither the scalar count nor the MCMC draw count is
an interchangeable count of independent years.

The independent homogeneous networks use ten sites on a partial Cartesian grid with 25-km
spacing: four sites in each of the first two rows and two in the third. Copula and regression
networks use the corresponding layout with $100/3$-km spacing. The following table specifies all
eight estimator-recovery cases. Location and scale are fitted on their natural-log scales.

| Estimator and design | Generating location, scale, shape, and additional quantities | Generator seed | BestFit result |
|---|---|---:|---|
| MLE, independent homogeneous sites | Location 10,000; scale 3,000; shape -0.1 | 54321 | Passed |
| MLE, homogeneous sites with copula | Location 10,000; scale 3,000; shape -0.1; exponential range 40 km | 66666 | Passed |
| Bayesian, independent homogeneous sites | Location 10,000; scale 3,000; shape -0.1 | 12345 | Passed |
| Bayesian, homogeneous sites with copula | Location 10,000; scale 3,000; shape -0.1; exponential range 40 km | 33333 | Passed |
| Bayesian, location regression | Log-location intercept 8.987; X effect 0.005/km; Y effect 0.008/km; scale 3,000; shape -0.1 | 66666 | Passed |
| Bayesian, positive shape | Location 10,000; scale 3,000; shape 0.1 | 11111 | Passed |
| Bayesian, zero shape | Location 10,000; scale 3,000; shape 0, the Gumbel limit | 22222 | Passed |
| Bayesian, negative shape | Location 10,000; scale 3,000; shape -0.2 | 33333 | Passed |

The regression case gives different site locations according to

$$
\ln \mu_j=8.987+0.005X_j+0.008Y_j,\qquad \sigma_j=3000,\qquad \kappa_j=-0.1.\tag{S.1}
$$

Here $X_j$ and $Y_j$ are a site's coordinates in kilometres, $\mu_j$ is its GEV location, $\sigma_j$
its GEV scale, and $\kappa_j$ its shape. The homogeneous models replace that regression with a
single common location. The copula models additionally estimate range in
$\rho(d)=\exp(-d/\mathrm{range})$. The signed-shape cases exercise positive, zero, and negative
values under the declared GEV convention without changing the estimation method.

For MLE, every generating parameter must be within 1.96 standard errors of its fitted value.
The standard errors come from unregularized observed-information covariance; singular or regularized
covariance is a failure. Bayesian tests require every parent inside its central 95% posterior
interval, $\widehat R<1.10$, and effective sample size at least 100 for each fitted quantity.
If an already valid interval is narrower than 5% of a nonzero parent's magnitude, the secondary
point estimate must also be within 5%; that rule never replaces the interval requirement.
Every parent must lie inside its unchanged parameter bounds and prior support.

MLE uses Differential Evolution with estimator seed 12345. Bayesian estimation uses production
DEMCzs settings: 3,500 iterations, 1,750 warmup iterations, retained output length 10,000, and seed
12345. Three-, four-, and five-parameter cases have 6, 8, and 10 chains, respectively, with thinning
30, 40, and 50 and initial populations 300, 400, and 500. All eight recovery tests pass. The
available results do not include fitted coordinates or interval endpoints, so the table reports
the generating parents and the outcomes of the declared comparisons.

These recovery models have no latent spatial-error field. Their intervals concern uncertainty in
estimated parameters, not residual spatial-process variation or a future year's flood. Inclusion
of truth in one generated dataset does not establish repeated-sample 95% coverage.

## Independent correlation and held-out prediction checks

Three analytical comparisons ask whether the selected correlation model gives the intended
dependence at distances 0, 5, 10, 20, and 30, using range 20. All five values per family agree with
the independent Python calculation within $10^{-9}$.

| Correlation family | Definition or characteristic | Reference at distance 10 | Reference at distance 30 |
|---|---|---:|---:|
| Exponential | $\rho(d)=\exp(-d/20)$ | 0.606530660 | 0.223130160 |
| Powered exponential | $\rho(d)=\exp[-(d/20)^{1.6}]$ | 0.719012183 | 0.147616623 |
| Spherical | Cubic decrease to exactly zero at range 20, and zero beyond | 0.312500000 | 0 |

Two further checks assess prediction at a withheld site. The copula example fits 120 complete
three-site years at projected coordinates (0, 0), (10, 0), and (0, 12), then predicts at (9, 9).
An independent SciPy fit supplies both the optimum and an unregularized uncertainty calculation.
BestFit's likelihood at that optimum agrees within $10^{-7}$; its fitted optimum lies within the
four-parameter joint 95% likelihood-ratio region, whose cutoff is 9.487729.

| Held-out annual exceedance probability | Independent quantile target | Independent standard error | BestFit comparison |
|---|---:|---:|---|
| 0.50 | 20.635440 | 0.232776 | BestFit within 1.96 standard errors; passed |
| 0.10 | 25.306526 | 0.433969 | BestFit within 1.96 standard errors; passed |
| 0.02 | 29.205737 | 0.823428 | BestFit within 1.96 standard errors; passed |

The covariate example uses four fixed site log-locations 2.1, 2.5, 2.7, and 3.15, with two covariates
per site: (-1, 0), (0, 1), (1, -1), and (2, 0.5). Independent ordinary least squares predicts the
held-out covariate row (0.5, -0.25). The target log-location is 2.586206897, or physical location
13.27930617; BestFit agrees within $10^{-9}$. Parameter prediction variance is 0.00001808363,
while adding residual variation gives 0.00007555489. This check passes and keeps those uncertainty
sources distinct. It does not fit a GEV sample at each of the four supplied site summaries.

## Posterior prediction and uncertainty

Five independent checks distinguish uncertainty about parameters, unobserved spatial departures,
and future observations. Fixed-draw examples verify the calculation applied to supplied draws;
they assess prediction formulas separately from posterior estimation.

| Calculation | Setup, independent procedure, and result |
|---|---|
| Ungauged prediction for each draw | Three latitude/longitude sites near Denver, Boulder, and Colorado Springs predict at (39.5501, -105.7821). Four fixed process-parameter draws each get an independent haversine/covariance solve and log-link transformation. All conditional means, variances, and physical locations agree within $10^{-9}$. Passed. |
| Regional posterior curve | Nine fixed parameter draws, three site covariates (-1, 0.5, 1.5), and exceedance probabilities 0.5, 0.1, and 0.02. For each draw, calculate each site's GEV quantile and then average across sites; summarize those regional averages with linearly interpolated 2.5th/97.5th percentiles. Means and endpoints agree within $10^{-9}$. Passed. |
| Godambe covariance | Three sites and 24 complete years. Independent finite differences give likelihood curvature $H$ and the sum of annual score outer products $J$. The unregularized covariance $H^{-1}JH^{-1}$ agrees with BestFit within $2\times10^{-5}$ relative; reconstruction from stored matrices agrees within $10^{-9}$. Passed. |
| Temporal block bootstrap | Twelve three-site annual vectors are resampled in wrapping blocks of four complete years, preserving the sites within each row. Five fixed replicates are fitted independently in SciPy and by BestFit. All five BestFit fits succeed; physical-parameter, site-quantile, and regional-quantile interval endpoints meet 2% relative tolerance, with 0.02 absolute tolerance near zero. Passed. |
| Variance inflation | Ten three-site years give independent average absolute intersite correlation 0.9997195942. The resulting variance inflation factor is 2.9994391884. Site and regional interval transformations agree with the independent calculation within $10^{-9}$. Passed. |

The regional fixed-draw example yields the following independent targets. Because each draw's
regional average is formed before taking percentiles, the interval is for the regional average
curve, not a collection of site intervals and not a region-wide flood maximum.

| Exceedance probability | Regional mean quantile | Central 95% interval from the supplied draws |
|---|---:|---|
| 0.50 | 21.951292 | [20.841282, 23.119005] |
| 0.10 | 27.064969 | [25.834934, 28.310139] |
| 0.02 | 31.247433 | [29.648213, 32.894444] |

Variance inflation keeps an interval's midpoint and multiplies its half-width by
$\sqrt{2.9994391884}=1.731888908$; scale lower limits are truncated at zero. The five-replicate
bootstrap is a reproducible calculation check, not a sufficiently large experiment to establish
the long-run coverage of bootstrap intervals.

## Conclusion

The 31 methods comprise eight likelihood checks, one criteria check, three distance/kriging checks,
one simulation check, eight estimator recoveries, three correlation checks, two held-out prediction
checks, and five posterior-prediction/uncertainty checks. The calculations and recovery outcomes
support the stated network sizes, missingness patterns, covariates, dependence laws, and uncertainty
sources. Large-network performance, recovery of a latent spatial-error field, and simultaneous
predictive coverage remain outside the demonstrated recovery evidence.
