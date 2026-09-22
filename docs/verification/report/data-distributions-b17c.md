<!-- verification-status: publication-draft -->

# Distribution Fitting, Univariate Analysis, and Bulletin 17C

## Verification objectives

A flood-frequency analysis must do more than draw a smooth curve through observed floods. It must use the intended probability distribution, estimate its parameters consistently, and carry information about missing years, historical floods, and regional studies into the result. This chapter examines those steps for a single response variable, such as annual peak discharge.

Three kinds of comparison are used. Synthetic records test whether estimation recovers a known generating distribution. Independent analytical calculations and statistical packages test distribution formulas and fitted solutions. Published flood studies test complete combinations of systematic, historical, and prior information. Maximum likelihood estimation (MLE) selects the parameters that give the greatest likelihood to the observed data. Bayesian estimation also incorporates prior information and describes the remaining uncertainty with a posterior distribution.

A quantile $Q(p)$ is the value with probability $p$ of not being exceeded. For an annual-maximum series, $Q(0.99)$ is the 1% annual exceedance probability (AEP), or 100-year, discharge. A 95% posterior interval contains the central 95% of the posterior distribution; a single synthetic experiment does not demonstrate 95% long-run coverage. Tables headed **reference** or **generating** contain comparison targets. Measured BestFit outputs are identified separately where available.

## Distribution-family oracles

### Stationary Bayesian family recovery

**Setup.** A stationary distribution has parameters that do not change through the record. Each of the fifteen families below supplies 1,000 simulated scalar observations, with random seed 12345. The sample is fitted using the matching Bayesian model and the production DEMCzs sampler settings. The tests request posterior-mode point estimates and central 95% intervals, retain the model's prior construction and parameter bounds, and explicitly enable the Jeffreys scale rule in the LP3 case. A Jeffreys scale rule assigns prior weight inversely proportional to scale.

Synthetic response magnitudes have no assigned physical unit. Location, mean, scale, and standard deviation are expressed in those response units, except where the table identifies logarithms. Shape and skewness are dimensionless. **Ln-Normal is specified here by the mean and standard deviation of the original positive response**, despite its use of natural logarithms internally. Log-Normal and Log-Pearson Type III (LP3) instead use the mean and standard deviation of the base-10 logarithm of the response. Scale is a distribution parameter and need not equal standard deviation.

| Family | Generating parameters: Bayesian recovery | Generating parameters: MLE and distribution-fitting recovery |
|---|---|---|
| Normal | Mean 100; standard deviation 15 | Mean 100; standard deviation 15 |
| Ln-Normal | Response mean 4.5; response standard deviation 0.4 | Response mean 3.5; response standard deviation 0.4 |
| Log-Normal, base 10 | Log mean 3; log standard deviation 0.5 | Log mean 3; log standard deviation 0.5 |
| Exponential | Location 0; scale 50 | Location 10; scale 25 |
| Gamma | Scale 5; shape 2 | Scale 5; shape 3 |
| Generalized Extreme Value (GEV) | Location 100; scale 20; shape -0.1 | Location 50; scale 15; shape 0.1 |
| Generalized Logistic | Location 100; scale 15; shape 0.1 | Location 75; scale 10; shape 0.15 |
| Generalized Normal | Location 100; scale 15; shape 0.1 | Location 50; scale 10; shape -0.3 |
| Generalized Pareto | Location 0; scale 50; shape 0.1 | Location 0; scale 20; shape 0.15 |
| Gumbel | Location 100; scale 20 | Location 50; scale 15 |
| Kappa Four | Location 100; scale 20; first shape -0.1; second shape 0.1 | Location 100; scale 20; first shape -0.1; second shape 0.1 |
| Logistic | Location 100; scale 10 | Location 75; scale 10 |
| Pearson Type III | Mean 100; standard deviation 20; skewness 0.5 | Mean 100; standard deviation 20; skewness 0.8 |
| Log-Pearson Type III | Log mean 3; log standard deviation 0.5; log skewness 0.2 | Log mean 2; log standard deviation 0.3; log skewness 0.5 |
| Weibull | Scale 100; shape 2.5 | Scale 100; shape 2.5 |

**Procedure.** The known generating parameter must lie in its central 95% posterior interval. Each sampled parameter must also have $\widehat R<1.10$, which checks agreement among sampling chains, and effective sample size (ESS) at least 100, which accounts for dependence among draws. The zero-location Exponential and Generalized Pareto cases assess that boundary through the central 95% interval for $Q(0.99)$; their remaining parameters are checked directly. A secondary point-estimate rule applies only when a nonzero parameter's entire 95% interval is narrower than 5% of its generating magnitude; in that situation, point-estimate error must also be at most 5%.

**Results.** All fifteen recovery methods are verified. This supports recovery of the declared distributions, including positive data, skewed tails, and four-parameter flexibility, under these fixed samples and settings. The evidence supplies acceptance outcomes rather than a complete table of fitted posterior estimates and interval endpoints. These experiments do not establish recovery for every parameter combination or record length.

### Distribution-fitting and MLE recovery

**Setup and procedure.** The right-hand column above describes fifteen additional samples, each with 1,000 observations and seed 12345. Each sample is submitted to the default Distribution Fitting analysis. The generating family must be present in the candidate list and fit successfully; the experiment then checks its estimated parameters against the known values. It does not require that the generating family rank first among all candidates.

Where an analytical parameter covariance is available, a parameter must be within 1.96 estimated standard errors of its generating value. A standard error measures uncertainty in an estimated parameter, rather than the spread of the observations. Generalized Logistic, Generalized Normal, and Kappa Four use 95% profile intervals from an additional likelihood fit: each interval must contain both the known parameter and the Distribution Fitting estimate. A profile interval allows the other parameters to adjust while the parameter under examination is varied. Ln-Normal comparisons are made in natural-log mean and variance; Pearson III and LP3 use the equivalent mean, inverse-scale, and shape coordinates appropriate to their covariance calculation. Generalized Pareto checks scale and shape directly and checks its zero-location direction through a 95% band for $Q(0.99)$. The conditional 5% point rule described above also applies to these fitting checks.

**Results.** All fifteen Distribution Fitting recovery methods are verified. A separate direct-MLE series uses the same right-hand parents for thirteen families: all except base-10 Log-Normal and Kappa Four. Those thirteen recovery checks, plus a fourteenth check of Ln-Normal against the closed-form fit to the same log-transformed sample, are also verified. Together they test the fitting workflow and the estimator directly. They support recovery within the stated uncertainty rules, not exact equality to generating parameters or a guarantee that model-selection criteria identify the true family.

### Nonstationary univariate recovery

**Setup.** A nonstationary model allows a distribution parameter to vary with a covariate. These seven recovery designs use the observation index $t=0,1,\ldots,999$ as that covariate, 1,000 simulated responses, and seed 12345. The index is a dimensionless position in the record, not a calendar year. The Bayesian procedure uses the same production sampling settings and 95% interval convention as the stationary tests. In the table, $\mu$ denotes mean, $\sigma$ standard deviation, $\xi$ location, $\alpha$ scale, and $\kappa$ shape.

| Design | Known distribution and trend | What the comparison tests |
|---|---|---|
| Constant Normal | $\mu=100$, $\sigma=15$ | A constant trend reproduces the stationary parameter-recovery problem. |
| Normal with reciprocal mean | $\mu(t)=1/(0.01+0.00001t)$; $\sigma=15$ | The mean declines from 100 to about 50 while observation variability stays fixed. |
| Normal with reciprocal standard deviation | $\mu=100$; $\sigma(t)=1/[1/15+(1/10-1/15)t/999]$ | Standard deviation declines from 15 to 10 without a change in mean. |
| Normal with sinusoidal standard deviation | $\mu=100$; $\sigma(t)=15+3\sin(2\pi\,0.01t+0.3)$ | Variability cycles between 12 and 18 with a period of 100 index units. |
| GEV with linear shape | $\xi=50$, $\alpha=15$; $\kappa(t)=0.05+0.0001t$ | Tail shape changes while location and scale remain fixed. |
| Generalized Pareto with changing location and scale | $\xi(t)=10+0.02t$; $\alpha(t)=20\exp(0.0005t)$; $\kappa=0.1$ | The threshold location and spread change together. |
| LP3 with changing log mean and log standard deviation | $\mu(t)=3+0.0002t$; $\sigma(t)=0.2\exp(0.0002t)$; log skewness 0.2 | Both the level and variability of base-10 log discharge change. |

**Procedure and results.** Constant parameters are checked for central 95% posterior inclusion. Changing parameters are checked as predicted responses at $t=0,499,999$; the sinusoidal design instead uses $t=0,25,50,75,999$ to sample the cycle. This checks the physically interpretable curve when different combinations of coefficients can describe similar responses. All sampled coefficients must meet $\widehat R<1.10$ and ESS at least 100, and the conditional 5% rule applies to resolved parameters or responses. All seven recovery designs are verified. They establish recovery at the declared response points, not simultaneous coverage of every point on a continuous trend.

An eighth, analytical check examines the reciprocal formula itself. With starting index 10, coefficients 0.02 and 0.001, and evaluation index 510, the response is $1/0.52=1.923076923\ldots$. Its derivatives with respect to the intercept and slope are $-1/0.52^2$ and $-500/0.52^2$. The response agrees within $10^{-12}$; centered numerical derivatives using perturbation $10^{-7}$ agree within $10^{-6}$ and $10^{-4}$, respectively. This verified calculation checks the trend formula independently of sampling.

### Independent distribution functions and fitted solutions

**Setup.** Each family also has a deterministic sample comprising its quantiles at 39 evenly spaced nonexceedance probabilities, from 0.025 to 0.975. The generating distributions are specified below. These are fixed calculation examples, not random samples. SciPy 1.18.1 supplies thirteen independent fitted solutions; R 4.4.3 with lmomco 2.5.7 supplies Generalized Logistic and Generalized Normal. All synthetic magnitudes are in arbitrary response units.

| Family | Distribution used to construct the 39 observations | External fitted $Q(0.9)$ reference, rounded |
|---|---|---:|
| Normal | Mean 100; standard deviation 15 | 117.669110 |
| Ln-Normal | Response mean 100; response standard deviation 30 | 135.351898 |
| Log-Normal, base 10 | Log mean 2; log standard deviation 0.35 | 258.392097 |
| Exponential | Location 5; scale 20 | 48.307450 |
| Gamma | Scale 12; shape 3.5 | 68.821461 |
| GEV | Location 100; scale 20; shape -0.15 | 148.337810 |
| Generalized Logistic | Location 75; scale 10; shape 0.15 | 92.244444 |
| Generalized Normal | Location 50; scale 10; shape -0.3 | 64.255965 |
| Generalized Pareto | Location 10; scale 5; BestFit shape -0.2 | 23.211643 |
| Gumbel | Location 100; scale 20 | 141.590460 |
| Kappa Four | Location 100; scale 20; first shape -0.1; second shape 0.2 | 148.525342 |
| Logistic | Location 100; scale 15 | 130.338836 |
| Pearson Type III | Mean 100; standard deviation 20; skewness 1 | 124.719447 |
| Log-Pearson Type III | Log mean 2; log standard deviation 0.3; log skewness 0.8 | 233.819015 |
| Weibull | Scale 100; shape 2.2 | 140.744883 |

**Procedure.** BestFit and the external implementation fit the same observations. Their maximum log likelihoods are compared using a joint 95% likelihood-ratio region, which assesses all fitted parameters together: twice the absolute log-likelihood difference must not exceed 5.991465, 7.814728, or 9.487729 for two, three, or four parameters. At the same external parameter values, probability density, cumulative distribution function (CDF), quantile, and likelihood calculations must agree within $10^{-8}$ absolute plus $10^{-7}$ relative tolerance. The CDF is the probability of a value at or below the specified magnitude. These formula checks are much tighter than the uncertainty-based comparison between separately fitted optima.

Parameter conventions are reconciled before comparison. Gamma and Weibull reverse the external shape/scale ordering; Ln-Normal converts log parameters into original-response moments; LP3 includes the base-10 transformation in its density. BestFit's GEV shape matches SciPy's GEV shape and has the opposite sign to the common Coles convention. BestFit's Generalized Pareto shape has the opposite sign to SciPy's Pareto shape. The free-location Pareto optimum is on a support boundary, so its likelihood-ratio rule is a declared comparison screen rather than proof of an exact 95% confidence region.

**Results.** All fifteen external comparisons are verified. The displayed quantiles are independent targets, with acceptance evaluated at full precision. Additional Kappa Four checks use location 0, scale 1, first shape 0, and second shape 0.2: the density at response 1 is independently 0.2709847912611985, and analytical CDF inversion returns response 1, both at absolute tolerance $10^{-10}$. Supporting finite-shape checks use first shapes $\pm0.25$, second shapes $\pm0.5$, and probabilities $10^{-6},0.1,0.5,0.9,0.999999$ to verify increasing quantiles, valid support, and CDF inversion. These checks distinguish distribution validity from the separate question of whether particular moments exist.

### Published real-data likelihood comparisons

**Setup.** Fifteen fits use observed river flows or wind speeds. The Indiana discharge records come from Rao and Hamed (2000): Tippecanoe River near Delphi, Wabash River at Lafayette, White River near Nora, Sugar Creek at Crawfordsville, White River at Mount Carmel, and East Fork White River at Seymour. Their discharge unit is cubic feet per second (ft³/s). Harricana River at Amos, Quebec, comes from Bobée and Ashkar (1991) and uses cubic metres per second (m³/s). The New York wind record consists of 153 daily values from May-September 1973 in the R airquality data, in miles per hour (mph).

| Fitted family | Record and observation count | Reference parameters, with labels |
|---|---|---|
| Normal | Tippecanoe; 48 annual peaks | Mean 12,665.21 ft³/s; population standard deviation 4,660.42 ft³/s, calculated from the observations |
| Ln-Normal | Wabash; 85 annual peaks | Natural-log mean 10.716952; natural-log standard deviation 0.447419, calculated from the observations and converted to response moments before comparison |
| Log-Normal, base 10 | Wabash; 85 annual peaks | Log mean 4.654313; log standard deviation 0.194311, calculated from the observations |
| Exponential | Wabash; 85 annual peaks | Location 13,100 ft³/s; scale 36,122.35 ft³/s |
| Gamma | Harricana; 69 annual peaks | Scale $1/0.08833$ m³/s; shape 16.89937 |
| Pearson Type III | Harricana; 69 annual peaks | Mean 191.31739 m³/s; standard deviation 47.01925 m³/s; skewness 0.61897 |
| Log-Pearson Type III | Harricana; 69 annual peaks | Base-10 log mean 2.26878; log standard deviation 0.10621; log skewness -0.02925 |
| Gumbel | Sugar Creek; 53 annual peaks | Location 8,049.6 ft³/s; scale 4,478.6 ft³/s |
| Weibull | Tippecanoe; 48 annual peaks | Scale 14,196.9496 ft³/s; shape 2.9829, from the retained R-Stan weak-prior comparison |
| GEV | White River near Nora; 62 annual peaks | Location 10,849 ft³/s; scale 5,745.6 ft³/s; shape 0.005 |
| Generalized Pareto | Mount Carmel; 287 peaks above 50,000 ft³/s | Location 50,400 ft³/s; scale 55,142.29 ft³/s; shape 0.0945 |
| Logistic | Tippecanoe; 48 annual peaks | Location 12,628.59 ft³/s; scale 2,708.64 ft³/s |
| Generalized Logistic | East Fork White River; 68 annual peaks | Location 30,911.83 ft³/s; scale 9,305.0205 ft³/s; shape -0.144152 |
| Generalized Normal | New York wind; 153 daily values | Location 9.7285364 mph; scale 3.4885029 mph; shape -0.1307169 |
| Kappa Four | New York wind; 153 daily values | Location 8.68360234 mph; scale 3.10384972 mph; first shape 0.14470737; second shape -0.07348014 |

**Procedure and results.** Each BestFit MLE must complete, and the reference point must satisfy the joint 95% likelihood-ratio limit for its parameter count. All fifteen checks are verified. The Normal and log-Normal reference standard deviations use divisor $n$, as required by the closed-form MLE, rather than the sample-statistic divisor $n-1$. The wind references originate in an R L-moment calculation, and the Weibull reference uses a weak-prior R-Stan calculation; their acceptance supports consistency with those reference points, not identity between different estimation methods. The East Fork example also retains the documented difference between textbook summary statistics and the tabulated observations. The independent-package tests above provide the separate fitted-optimum evidence.

### Log10-Normal closed-form test

**Setup.** Seven positive observations are defined by base-10 logarithms 1.1, 1.4, 1.7, 2.0, 2.3, 2.6, and 2.9. This small example makes both the transformation and the likelihood normalization independently calculable. The mean and population standard deviation of the transformed sample are

$$
\widehat\mu=2,\qquad \widehat\sigma=0.6.\tag{D.1}
$$

**Procedure and results.** The original-response log likelihood must include the transformation term $-\sum_i\log(y_i\ln 10)$. Omitting it could leave a plausible-looking fit while producing the wrong likelihood for model comparison. The analytical comparison is verified under the following rules.

| Quantity | Analytical reference | Required agreement |
|---|---:|---|
| Log mean | 2 | Fitted error at most 1.96 standard errors, using $0.6/\sqrt{7}$ |
| Log standard deviation | 0.6 | Fitted error at most 1.96 standard errors, using $0.6/\sqrt{14}$ |
| Joint fitted point | The mean and scale above | Twice the likelihood difference at most 5.991464547107979 |
| Original-response log likelihood at the analytical point | -44.431208784723104 | Absolute error at most $10^{-10}$ |
| CDF at the median response, 100 | 0.5 | Absolute error at most $10^{-12}$ |
| $Q(0.9)$ at the analytical point | 587.3959385303014 | Absolute error at most $10^{-9}$ |

The fitted $Q(0.9)$ is also checked using its 95% band obtained by propagating parameter covariance into quantile uncertainty. This checks the practical response as well as the log-space parameters.

### Common-data fitting test

**Setup and procedure.** Gumbel, Normal, and Logistic are fitted to the same 39-point Normal quantile sample, generated with mean 100 and standard deviation 15 at probabilities 0.025-0.975. This asks whether BestFit calculates and ranks competing fits consistently. The Akaike information criterion (AIC) and Bayesian information criterion (BIC) combine likelihood with a penalty for parameter count; smaller values are preferred. Root mean squared error (RMSE) measures the discrepancy between the ordered observations and fitted quantiles; its parameter-adjusted form is illustrated below.

| Candidate | SciPy reference AIC | SciPy reference BIC | SciPy reference RMSE | Reference inverse-squared-RMSE weight |
|---|---:|---:|---:|---:|
| Normal | 319.329424 | 322.656548 | 1.144404 | 0.459581 |
| Logistic | 321.099336 | 324.426459 | 1.122088 | 0.478043 |
| Gumbel | 323.836960 | 327.164084 | 3.106353 | 0.062376 |

**Results.** The verified checks require AIC ranking Normal, Logistic, Gumbel, and RMSE ranking Logistic, Normal, Gumbel, while retaining the configured display order Gumbel, Normal, Logistic. The differing rankings are expected because likelihood and quantile residuals measure different aspects of fit. At common parameter values, likelihood, AIC, and BIC agree within $10^{-8}$ absolute plus $10^{-7}$ relative tolerance. The independently recomputed criteria/RMSE and normalized weights proportional to $1/\mathrm{RMSE}^2$ agree with BestFit within $10^{-10}$ and $10^{-12}$, respectively.

One numerical comparison documents the small difference between independently optimized Gumbel fits. SciPy returned location 93.1123479994 and scale 13.1576285680; BestFit returned location 93.1129321295 and scale 13.1578232496. Their RMSE values were 3.1063533062 and 3.1065229843. The points satisfy the joint 95% likelihood comparison. This illustrates why agreement is assessed through the objective and uncertainty rule instead of demanding identical optimizer coordinates.

### Parameter-adjusted RMSE

With four residuals of 1, 2, 3, and 4 and one fitted parameter, the independent reference is

$$
\mathrm{RMSE}=\sqrt{\frac{1^2+2^2+3^2+4^2}{4-1}}=\sqrt{10}=3.1622776601683795.\tag{D.2}
$$

BestFit reproduced this value within $10^{-12}$. Reordering the observed/fitted pairs together leaves the answer unchanged. A separate verified plotting-position check uses the 39 observations to recover the analytical probabilities $i/40$ for ranks $i=1,\ldots,39$, within $10^{-12}$. These checks establish the arithmetic and data ordering needed to interpret the common-data fitting comparison.

## Bulletin 17C published examples

**Setup.** The seven official examples test the specialized generalized method of moments (GMM) route for flood-frequency analysis. GMM estimates parameters by matching selected data moments, with the weighting appropriate to this method. Each example fits LP3 to discharges expressed in ft³/s, with parameters defined on the base-10 logarithm of those numeric discharge values. A perception threshold records the magnitude above which a flood would have been noticed during a stated period. An interval observation gives bounds on a flood whose exact magnitude is unknown. Low-outlier censoring retains information that a small observation falls below a threshold. Examples 1, 2, 4, 6, and 7 use the Multiple Grubbs-Beck Test (MGBT) for low-outlier screening.

| Example and site | Record and analysis setup |
|---|---|
| 1. Moose River at Victory, Vermont | 68 annual peaks, 1947-2014; systematic-record example with the prescribed low-outlier processing. |
| 2. Orestimba Creek near Newman, California | 82 annual peaks, 1932-2013, including zero-flow years; low flows are handled by the prescribed censoring procedure. |
| 3. Back Creek near Jones Springs, West Virginia | 56 observed annual peaks, including 22,000 ft³/s in 1936; threshold periods 1932-1938, 1976-1992, and 1999-2003 use 21,000 ft³/s; manual low-outlier threshold 2,000 ft³/s. |
| 4. Arkansas River at Pueblo, Colorado | 81 systematic peaks in 1895-1976 plus intervals for 1864, 1893, 1894, and 1921; historical thresholds extend to 1165. |
| 5. Bear Creek at Ottumwa, Iowa | 50 annual peaks, 1965-2014; nine crest-stage-gage threshold periods, ranging from 560 to 1,180 ft³/s; manual low-outlier threshold 1,200 ft³/s. |
| 6. Santa Cruz River at Lochiel, Arizona | 65 annual peaks, 1949-2013; 12,000 ft³/s perception threshold for 1927-1948; MGBT low-outlier processing. |
| 7. American River at Fair Oaks, California | 77 systematic peaks within 1905-1997; five paleoflood/historical intervals and ten threshold periods, extending to year 1. |

For Example 4 the historical bounds are 41,000-60,000 ft³/s in 1864, 20,000-25,000 in 1893, 35,000-40,000 in 1894, and 80,000-103,000 in 1921. Perception thresholds are 150,000 for 1165-1858, 40,000 for 1859-1892, 19,900 for 1893-1894, and 20,000 for 1977-2004. Example 7 uses bounds 600,000-850,000 in year 650, 400,000-550,000 in each of 1437, 1574, and 1711, and 262,000-300,000 in 1862. Its early thresholds are 599,000 for years 1-1301, 399,000 for 1302-1847, and 261,000 for 1848-1904; missing portions of the later record use 150,000 ft³/s. These long periods supply information about large floods without treating every unobserved year as a measured discharge.

**Procedure and results.** The fitted log mean, log standard deviation, and log skewness are compared with the published targets below, with absolute tolerance 0.001 for each. All 21 comparisons across the seven examples are verified. The table reports the published targets, not fitted BestFit coordinates.

| Example | Published log mean | Published log standard deviation | Published log skewness |
|---:|---:|---:|---:|
| 1 | 3.328623159 | 0.140287994 | 0.396626124 |
| 2 | 3.022663041 | 0.682087092 | -0.929108050 |
| 3 | 3.759834285 | 0.243406211 | 0.144442997 |
| 4 | 3.885777246 | 0.245920859 | 0.817849937 |
| 5 | 3.278686106 | 0.233135027 | -0.925407257 |
| 6 | 3.069106533 | 0.489820622 | -0.462278724 |
| 7 | 4.653457000 | 0.376721000 | -0.101163000 |

Three additional plotting-position comparisons reproduce PeakFQ probabilities for Examples 4, 5, and 7 within $10^{-12}$. For example, the four historical intervals in Example 4 have reference plotting probabilities 0.0091324201, 0.0507219548, 0.0211032951, and 0.0045662100. The oldest paleoflood in Example 7 has reference probability 0.00025. These checks verify how observations are positioned on the frequency plot; they do not verify confidence-band coverage.

## Bulletin 17C covariance, regional information, and recovery

### Uncertainty in fitted moments

**Setup and procedure.** Thirteen checks examine covariance, the matrix describing parameter variances and their co-movement. Six families are tested at sample sizes 25 and 100, using seed 12345: Exponential with location 10 and scale 50; Gamma with scale 5 and shape 2; Normal with mean 100 and standard deviation 15; Pearson III with mean 100, standard deviation 20, and skewness 0.5; base-10 Log-Normal with log mean 3 and log standard deviation 0.5; and LP3 with log mean 3, log standard deviation 0.5, and log skewness 0.2. The thirteenth case uses Moose River Example 1 with 68 observations.

Independent calculations use central moments through order six and the finite-sample corrections for the fitted second and third moments. Frozen Python calculations are cross-checked by a separately implemented calculation within $10^{-12}$ scaled tolerance. BestFit's fitted covariance is then compared at the actual fitted parameters, with $10^{-5}$ scaled plus $10^{-10}$ absolute tolerance. For a concrete reference, the Exponential location/scale variances are 100 and 200 at sample size 25, with covariance -100; at sample size 100 they are 25 and 50, with covariance -25.

**Results.** All thirteen methods are verified. They check both uncertainty magnitude and parameter dependence, including the transformations needed for log-discharge families. Agreement at these cases does not establish that matrix regularization is harmless in every ill-conditioned fit.

### Regional information

**Setup.** Twelve comparisons ask whether an at-site estimate and regional information receive the intended relative weight. The at-site data are deterministic quantile grids at probabilities $i/(n+1)$: Log-Normal with base-10 log mean 3 and log standard deviation 0.5, or LP3 with those same values and log skewness 0.2. Regional targets are log mean 3.25 with mean square error (MSE) 0.004, log standard deviation 0.5 with MSE 0.0015, log skewness 0.3 with MSE 0.05, and log-transformed $Q(0.99)$ of 4.5 with MSE 0.01. MSE expresses uncertainty in the regional estimate and controls its weight.

For one parameter, the independent inverse-variance reference is

$$
\theta_w=\frac{\theta_a/V_a+\theta_r/V_r}{1/V_a+1/V_r},\qquad
V_w=\frac{1}{1/V_a+1/V_r},\tag{D.3}
$$

where subscripts $a$ and $r$ denote at-site and regional estimates and $V$ is their variance or MSE. With several parameters, the reference retains their covariance. Quantile information is propagated through the local sensitivity of the quantile to the parameters. These nonlinear comparisons are approximate and have the declared limits below; the first percentage concerns the weighted value and the second its MSE.

| Family and information applied | Sample sizes | Value/MSE comparison limits |
|---|---|---|
| Log-Normal: log mean only | 25 and 100 | Mean 1% / 1% |
| Log-Normal: log standard deviation only | 25 and 100 | Standard deviation 5% / 5% |
| Log-Normal: both parameters | 25 and 100 | Mean 5% / 5%; standard deviation 10% / 10% |
| Log-Normal: $Q(0.99)$ only | 25 and 100 | Quantile 10% / 20% |
| Log-Normal: both parameters and $Q(0.99)$ | 25 | Mean 5% / 10%; standard deviation 10% / 15%; quantile 10% / 20% |
| LP3: log mean and log skewness | 25 | Mean 5% / 25%; skewness value 15% plus 0.01 absolute, MSE 20% |
| LP3: all three parameters | 25 | Mean 5% / 10%; standard deviation 10% / 15%; skewness value 15% plus 0.01 absolute, MSE 20% |
| LP3: all parameters and $Q(0.99)$ | 25 | Mean 5%, standard deviation 10%, quantile 10%, skewness 15% plus 0.01 absolute; every compared MSE 25% |

**Results.** All twelve comparisons are verified. The simple mean-only case closely reproduces inverse-variance weighting. The wider limits for simultaneous nonlinear information describe the approximation being tested; they are not the acceptance limits for synthetic parent recovery. These deterministic grids do not measure repeated-sample interval coverage.

### Recovery of parameters and the 100-year quantile

Six additional GMM checks each use 1,000 random observations and seed 12345. Their parents are Exponential location 0/scale 50; Gamma scale 5/shape 2; Normal mean 100/standard deviation 15; Pearson III mean 100/standard deviation 20/skewness 0.5; base-10 Log-Normal log mean 3/log standard deviation 0.5; and LP3 log mean 3/log standard deviation 0.5/log skewness 0.2. Parameter error must be at most 1.96 standard errors using the fitted moment-based covariance. Pearson III and LP3 check mean and standard deviation directly and assess the weak skewness direction through inclusion of the known $Q(0.99)$ in its 95% quantile band. All six are verified.

A separate Profile-Q example uses 1,000 LP3 observations with log mean 3, log standard deviation 0.5, log skewness -0.5, and seed 12345. After fitting, the three true-profile parameter intervals must be finite and ordered. The generating $Q(0.99)$ must also lie within the fitted quantile estimate plus or minus 1.96 standard errors, calculated by propagating moment-based parameter uncertainty. The verified result therefore supports the declared 100-year response check. It does not establish generating-parameter inclusion in the profile intervals, or repeated-sample coverage of those intervals.

## Published Bayesian comparisons

### Australian Rainfall and Runoff Flike examples

**Setup.** Flike provides an independent Bayesian calculation using importance sampling. The Hunter River at Singleton record contains 31 annual maximum discharges for 1938-1968; the Wimmera River at Glynwylln record contains 56 annual maxima for 1960-2015. All discharges are in m³/s. BestFit uses its Bayesian univariate analysis with the Jeffreys scale rule enabled and the posterior-mean point-estimate option. A 90% interval extends from the 5th to the 95th posterior percentile.

| Flike case | BestFit setup | Relative limit for every compared mean and interval endpoint |
|---|---|---:|
| 3 | Hunter River, LP3, systematic observations only | 7.5% |
| 4 | Hunter River, LP3, plus one exceedance of 12,525 m³/s during 1820-1937 | 5% |
| 5 | Hunter River, LP3, plus a Normal prior on log skewness with mean 0 and standard deviation 0.3 | 5% |
| 6a | Wimmera River, GEV, without low-outlier censoring | 10% |
| 6b | Wimmera River, GEV, with MGBT low-outlier censoring | 5% |

**Procedure.** For each case, the point-estimate curve and both 90% interval endpoints are compared at four AEPs. The complete Flike target curves are shown below; these are external benchmark values.

| Case | AEP (return period) | Reference mean, m³/s | Reference 90% interval, m³/s |
|---|---|---:|---|
| 3 | 10% (10 years) | 3,929 | 2,229-8,408 |
| 3 | 2% (50 years) | 12,786 | 5,502-51,010 |
| 3 | 1% (100 years) | 19,572 | 7,188-107,122 |
| 3 | 0.2% (500 years) | 47,034 | 11,507-570,635 |
| 4 | 10% (10 years) | 3,294 | 2,181-4,947 |
| 4 | 2% (50 years) | 9,350 | 5,778-16,511 |
| 4 | 1% (100 years) | 13,511 | 7,785-27,687 |
| 4 | 0.2% (500 years) | 28,542 | 12,966-85,583 |
| 5 | 10% (10 years) | 3,598 | 2,172-6,702 |
| 5 | 2% (50 years) | 10,535 | 5,310-26,633 |
| 5 | 1% (100 years) | 15,413 | 7,093-45,087 |
| 5 | 0.2% (500 years) | 33,365 | 12,244-134,107 |
| 6a | 10% (10 years) | 286 | 172-578 |
| 6a | 2% (50 years) | 1,315 | 493-4,975 |
| 6a | 1% (100 years) | 2,481 | 737-12,398 |
| 6a | 0.2% (500 years) | 10,696 | 1,802-101,034 |
| 6b | 10% (10 years) | 227 | 177-311 |
| 6b | 2% (50 years) | 423 | 304-785 |
| 6b | 1% (100 years) | 521 | 354-1,145 |
| 6b | 0.2% (500 years) | 789 | 448-2,813 |

**Results.** All five methods are verified under the case-specific limits above. The references illustrate the effect the comparison is intended to reproduce: historical information reduces Hunter River's 100-year upper interval limit from 107,122 to 27,687 m³/s; regional-skew information gives 45,087 m³/s. Wimmera's low-outlier treatment changes the 100-year reference mean from 2,481 to 521 m³/s. These substantial differences make censoring and prior specification consequential parts of the verification. The comparisons establish consistency with Flike's specified examples; they do not establish which treatment is appropriate for another catchment or demonstrate 90% empirical coverage.

### Kamp at Zwettl information-expansion examples

**Setup.** The GEV examples of Viglione et al. (2013) and Skahill et al. (2016) examine whether information beyond the systematic record changes the inferred flood-frequency curve in the intended way. The Kamp at Zwettl, Austria, supplies either 51 annual peaks for 1951-2001 or 55 for 1951-2005, in m³/s. Temporal information adds historical flood intervals: 180-300 m³/s in 1655, 240-400 in 1803, and 202.5-337.5 in 1829, with representative values 240, 320, and 270. A perception threshold of 300 m³/s covers 1600-1950. Causal information adds a Normal prior on the 500-year discharge, with mean 480 m³/s and standard deviation 80 m³/s. BestFit disables the Jeffreys scale rule for these comparisons.

**Procedure.** Cases 1-8 compare the posterior-mode GEV parameters and the 100- and 1,000-year discharge estimates and their 90% intervals. Location and scale must agree within 5%; shape within 10% for Case 1 and 5% otherwise; every discharge summary within 5%. The comparison targets are:

| Case | Systematic period; added information | Reference location, m³/s | Reference scale, m³/s | Reference shape |
|---|---|---:|---:|---:|
| 1 | 1951-2001; none | 42.9 | 20.2 | -0.096 |
| 2 | 1951-2005; none | 41.7 | 20.7 | -0.310 |
| 3 | 1951-2001; temporal | 43.4 | 21.7 | -0.222 |
| 4 | 1951-2005; temporal | 42.6 | 21.5 | -0.281 |
| 5 | 1951-2001; causal | 41.9 | 21.0 | -0.313 |
| 6 | 1951-2005; causal | 41.6 | 20.8 | -0.333 |
| 7 | 1951-2001; temporal and causal | 42.7 | 21.8 | -0.291 |
| 8 | 1951-2005; temporal and causal | 42.5 | 21.5 | -0.313 |

| Case | Reference 100-year estimate, m³/s | Reference 90% interval, m³/s | Reference 1,000-year estimate, m³/s | Reference 90% interval, m³/s |
|---|---:|---|---:|---|
| 1 | 160 | 130-288 | 241 | 163-649* |
| 2 | 253 | 184-542 | 543 | 317-1,853 |
| 3 | 217 | 176-291 | 399 | 278-647 |
| 4 | 244 | 197-331 | 497 | 347-818 |
| 5 | 258 | 193-307 | 557 | 335-702 |
| 6 | 269 | 217-317 | 604 | 418-747 |
| 7 | 253 | 206-299 | 527 | 369-671 |
| 8 | 264 | 220-308 | 571 | 418-708 |

*The Case 1 test compares the 1,000-year lower endpoint with 163 m³/s; the published value is 183 m³/s in Table 2 of Skahill et al. (2016), reference 29. Acceptance against the retained 163 m³/s target therefore does not establish agreement with that published endpoint.*

Case 9 uses the 1951-2001 record with three Normal quantile priors: the 10-year discharge has mean 100 and standard deviation 20 m³/s; the 100-year discharge has mean 250 and standard deviation 40 m³/s; and the 1,000-year discharge has mean 500 and standard deviation 60 m³/s. Five posterior summaries for each GEV parameter are compared with EvdBayes, each within 5%. This case uses 95% parameter intervals, unlike the 90% discharge intervals above.

| Parameter | EvdBayes mean | EvdBayes standard deviation | EvdBayes 2.5th percentile | EvdBayes median | EvdBayes 97.5th percentile |
|---|---:|---:|---:|---:|---:|
| Location, m³/s | 41.4307 | 3.0918 | 35.5680 | 41.3467 | 47.7391 |
| Scale, m³/s | 20.7700 | 2.4520 | 16.3861 | 20.6195 | 26.0375 |
| Shape, dimensionless | -0.2694 | 0.0488 | -0.3608 | -0.2709 | -0.1699 |

**Results.** All nine methods are verified against their retained targets, subject to the Case 1 endpoint qualification above. The reference curves show why each design matters. Extending the systematic record alone changes the 100-year estimate from 160 to 253 m³/s. With both temporal and causal information, the corresponding estimates are 253 and 264 m³/s, and the interval widths are much smaller. The ninth comparison examines the shape and spread of the posterior parameter distributions rather than only one fitted curve. The comparisons support the stated combinations of systematic, temporal, and prior information; the target values remain distinct from measured BestFit outputs.

## Bootstrap convergence and numerical reliability

The supporting Moose River Example 1 bias-corrected-bootstrap experiment asks a different question: whether repeated fits reach their intended numerical solution. A bootstrap realization is a simulated replacement record that is refitted to assess uncertainty. This experiment fits 1,000 fixed realizations with seed 12345, 68 observations per realization, and regional log-skew information of 0.44 with MSE 0.078. It distinguishes delivery of a fitted result from convergence of the outer GMM iteration, which repeatedly updates moment weights.

| Diagnostic | Observed result |
|---|---:|
| Requested and accepted refits | 1,000 |
| Outer GMM convergence attained | 947 |
| Outer iteration limit reached | 53 |
| Fallbacks from the BFGS inner optimizer | 77 |
| Thrown fitting exceptions | 0 |

Thus, 1,000 returned fits do not mean 1,000 converged outer iterations. Independent supporting calculations verify the supplied gradients for the roundoff-convergence branch and positive definiteness of the regularized moment matrices. Those checks support the inner numerical work. They do not establish convergence of every outer iteration or coverage of the resulting intervals, and a more accurate inner solution alone does not guarantee a smaller error at a finite outer iteration budget. The [supporting experiment](../bfgs-roundoff-evidence-20260917/README.md) retains the configuration and diagnostics.

Numerical Cohn interval values and broad Bulletin 17C interval coverage remain outside the supported claims. The chapter's source and method identifiers are indexed in the report's [test-coverage appendix](test-coverage.md); the setup and interpretation needed to read the comparisons are given above.
