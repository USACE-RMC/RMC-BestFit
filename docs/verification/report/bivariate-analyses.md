<!-- verification-status: publication-draft -->

# Bivariate and Coincident-Frequency Analyses

A bivariate analysis describes two measurements from the same event. Each variable has its own *marginal distribution*,
describing its values considered separately. A **copula** describes how their percentiles occur together. This permits the
dependence to be changed while retaining the same marginal distributions. Coincident-frequency analysis then combines those
paired inputs through a response relationship, such as the sum of two contributing flows.
The numerical examples use arbitrary, consistent input units.

## Bivariate copula estimation

### Data and comparison procedure

Six one-parameter copula families are tested with both maximum pseudo-likelihood (MPL) and inference functions for margins
(IFM), using twelve fixed samples of 100 matched observations. MPL replaces each measurement by its ascending rank divided by
$n+1$ and estimates dependence from those probabilities. IFM first fits a Normal distribution to each marginal and then
estimates dependence using the fitted marginal probabilities. The independent Normal fits use the sample mean and the
maximum-likelihood standard deviation, whose variance denominator is $n$.

The Student-t copula adds a second parameter, degrees of freedom $\nu$, which controls joint tail behavior. Its external
comparison uses an independently generated sample of 1,000 pairs: $X$ has mean 100 and standard deviation 15, $Y$ has mean 80
and standard deviation 25, and the parent copula has correlation $\rho=0.8$ and $\nu=4$. Generation uses NumPy PCG64 seed
20260830. This sample is also analyzed by both MPL and IFM.

1. Independently calculate each copula density and likelihood using Python 3.12.13, NumPy 2.3.5,
   and SciPy 1.18.1. Check the one-parameter density formulas against cumulative-probability derivatives
   or, for the Gaussian family, a Normal-density ratio. Check the Student-t formula against
   SciPy's bivariate-t to univariate-t density ratio.
2. Find the independent likelihood maximum and preserve the observations, fitted parameters,
   and maximum log likelihood in a frozen reference file.
3. Evaluate BestFit's likelihood at those same reference parameters to check that both calculations
   describe the same probability model.
4. Run BestFit's production Differential Evolution estimator with its default tolerances, and
   compare the likelihood attained with the independent maximum.

### Reference values and results

The parameter values and log likelihoods below are **independent reference results**. For the first five families the parameter
is $\theta$; for Gaussian it is correlation $\rho$. The Student-t rows name both parameters explicitly.

| Family | Method | Independent fitted parameter | Independent maximum log likelihood | BestFit comparison |
|---|---|---|---:|---|
| Ali-Mikhail-Haq | MPL | $\theta=0.8321521$ | 7.245322 | Passed |
| Ali-Mikhail-Haq | IFM | $\theta=0.8392378$ | 7.500811 | Passed |
| Clayton | MPL | $\theta=1.5340162$ | 30.370897 | Passed |
| Clayton | IFM | $\theta=1.4852343$ | 30.315461 | Passed |
| Frank | MPL | $\theta=7.7187608$ | 46.461072 | Passed |
| Frank | IFM | $\theta=8.1303213$ | 49.134008 | Passed |
| Gumbel | MPL | $\theta=2.0979531$ | 39.077868 | Passed |
| Gumbel | IFM | $\theta=2.0311985$ | 40.512847 | Passed |
| Joe | MPL | $\theta=2.6643256$ | 38.271996 | Passed |
| Joe | IFM | $\theta=2.9656127$ | 44.762409 | Passed |
| Gaussian | MPL | $\rho=0.8000853$ | 48.086892 | Passed |
| Gaussian | IFM | $\rho=0.7871334$ | 48.323828 | Passed |
| Student t | MPL | $\rho=0.8157372$; $\nu=5.0704726$ | 557.465595 | Passed |
| Student t | IFM | $\rho=0.8142171$; $\nu=5.0055361$ | 559.839653 | Passed |

All fourteen comparisons passed. BestFit's likelihood at the reference parameters must agree within $10^{-8}$ for a
one-parameter family and $10^{-5}$ for Student t. Optimization is judged separately: twice the absolute difference between
reference and BestFit maximum log likelihood must not exceed the joint 95% likelihood-ratio cutoff, 3.841458820694124 for one
parameter or 5.991464547107979 for two. Passing therefore establishes agreement under the stated likelihood rule, not equality
of every printed parameter digit.

R *copula* estimates also support the first six families, but their unrecorded package version prevents a fully reproducible
package comparison. The independent likelihood calculations above define the present acceptance rule. No R-package comparison is
claimed for Student t.

## Bivariate recovery and coincident frequency

### Recovery of known dependence

Each recovery experiment generates exactly 1,000 matched pairs with Normal marginals: $X$ has mean 100 and standard deviation
15; $Y$ has mean 80 and standard deviation 25. The same observation index pairs $X$ with $Y$. The copula families and generating
parameters are:

| Copula family | Generating dependence parameter | Generation seed | Recovery result |
|---|---|---:|---|
| Ali-Mikhail-Haq | $\theta=0.8$ | 13050 | Bayesian passed |
| Clayton | $\theta=1.5$ | 13049 | Bayesian passed |
| Frank | $\theta=8$ | 13048 | Bayesian passed |
| Gumbel | $\theta=2$ | 13047 | Bayesian passed |
| Joe | $\theta=3$ | 13046 | Bayesian passed |
| Gaussian | $\rho=0.8$ | 13045 | Bayesian passed |
| Student t | $\rho=0.8$; $\nu=4$ | 13051 | Conditional MLE passed for correlation and symmetric tail dependence |

The procedure first fits each Normal marginal by maximum likelihood and requires its mean and standard deviation within 1.96
estimated standard errors of the generating values. It then holds those marginal fits fixed while estimating the copula. Each
generating copula must be inside prior support and explain the sample better than the corresponding independence limit.

The six one-parameter families use the standard DEMCzs simulation settings and seed 12345. Their generating dependence parameter
must lie inside its central 95% posterior interval, with $\widehat R<1.10$ and ESS at least 100. Student t uses conditional MLE.
Its correlation must satisfy the 1.96-standard-error rule; its combined tail dependence must satisfy that rule using uncertainty
from both fitted coordinates and their covariance. Tail dependence describes the tendency for both variables to be extreme
together. The weakly identified raw degrees-of-freedom parameter is not claimed recovered. All seven recovery tests passed.

### Linear coincident-frequency response

The three linear experiments use $Z=X+Y$, the same Normal marginal parents, and 1,000 pairs generated with seed 12345.
Correlation is set to 0, 0.5, or -0.5. Positive dependence makes the sum more variable, while negative dependence reduces its
variation. Its exact Normal law is

$$
X+Y\sim N(\mu_X+\mu_Y,\sigma_X^2+\sigma_Y^2+2\rho\sigma_X\sigma_Y).\tag{B.1}
$$

Here the second argument denotes variance. The generating sum has mean 180 and variance 850, 1,225, or 475 for correlations 0,
0.5, or -0.5, respectively.

For each experiment, fit the Normal marginals by MLE and the Gaussian copula by Bayesian analysis. Build a five-by-five response
table from each fitted marginal's 5th, 25th, 50th, 75th, and 95th percentiles, setting every response to the sum of its two
inputs. BestFit converts that table into an exceedance curve over 50 output bins. Compare it at every output bin with Equation
(B.1) evaluated using the **fitted** means, standard deviations, and posterior-mean correlation.

All three tests passed: maximum absolute AEP error was within 0.05, mean absolute AEP error within 0.01, and exceedance
probability decreased as response increased. Separately, the generating correlation lay inside its central 95% posterior
interval and met the R-hat/ESS rules. The response comparison measures table-integration error conditional on the fitted inputs.
It does not establish recovery of the entire generating response curve or propagation of marginal estimation uncertainty;
marginal uncertainty samples are not supplied in these three experiments.

### Nonlinear coincident-frequency response

A fourth experiment uses $Z=\exp(0.01X+0.01Y)$, correlation 0.5, the same Normal marginal parents, and 1,000 pairs generated
with seed 13055. Its independent reference is Lognormal because $\log Z$ is exactly Normal with mean 1.8 and standard deviation
0.35.

The procedure fits and checks both marginal distributions, then fits and checks the Gaussian copula. It builds a 17-by-17
response table spanning marginal probabilities 0.001 to 0.999 and uses 61 output bins. At the fitted parameters, the numerical
exceedance curve must agree with the exact Lognormal law within 0.015 AEP. This comparison isolates response-table error.

Uncertainty propagation is checked separately. The experiment supplies 2,000 independent asymptotic MLE uncertainty draws for
each Normal marginal, with seeds 24680 and 24681, and combines them with the retained copula posterior draws. The marginal draws
are frequentist approximations, not Bayesian posterior samples. At generating nonexceedance probabilities 0.10, 0.25, 0.50,
0.75, and 0.90, the known response AEP must lie inside the central 95% propagated band. The nonlinear experiment passed the
fitting, numerical-error, and response-band checks.

### Independent combination of marginal uncertainty

A separate sum-response experiment supplies 100 equally spaced possible means for each Normal marginal: 0-5 for $X$ and 1-6 for
$Y$, with standard deviation 0.70 for both and fixed correlation 0. Each support is repeated to form 5,000 parameter draws. A
35-by-35 response table over input values -5 to 12 is evaluated in 33 output bins using resampling seed 20260803.

At each output bin, the independent calculation evaluates the closed-form Normal-sum AEP for all 10,000 combinations of means.
BestFit's mean AEP must agree within 0.02 and its central 50% interval endpoints within 0.05, both before and after reversing
the second marginal's draw order. Both variants passed. Pairing draws only by their stored positions produces a mean-AEP error
of at least 0.10 somewhere on the response curve, confirming that the comparison detects artificial dependence between
separately estimated marginals.

## Conclusion

The results support fourteen optimization comparisons, seven copula recoveries, three linear and one nonlinear response
experiment, and independent uncertainty resampling. Actual BestFit fit coordinates, attained likelihoods, maximum response
errors, and interval endpoints are not available for numerical results tables.

Bivariate copula intervals remain conditional on fixed marginal estimates. The results apply to paired observations, the stated
marginal distributions and parameter regions, and the specified response tables. They do not establish raw Student-t
degrees-of-freedom recovery or general nonlinear-response accuracy. [Supporting calculations](../bivariate.md) and the
[independent fitted values](../../../verification/data/bivariate/copula-estimation-oracle.json) provide the detailed
comparisons.
