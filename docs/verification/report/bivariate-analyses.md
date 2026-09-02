<!-- verification-status: publication-draft -->

# Bivariate and Coincident-Frequency Analyses

## Bivariate copula estimation

An independent Python/SciPy implementation fits six one-parameter copula families to twelve fixed
samples of one hundred matched observations and fits the two-coordinate Student-t family by MPL and
IFM on an independently generated sample of one thousand pairs. The one-coordinate densities are
checked against a numerical mixed derivative of the CDF; Student-t is checked against SciPy's
bivariate-t over univariate-t density ratio in physical `[rho, nu]` order. BestFit must reproduce the
independent optimum and maximum log likelihood. The historical R `copula` targets are retained only
for the original six families; no historical R parity is claimed for Student t.

| Family | Method | Independent optimum | Historical R target | Difference | Result |
|---|---|---:|---:|---:|---:|
| Ali-Mikhail-Haq | MPL | 0.8321521 | 0.8321504 | `+1.8e-6` | Passed |
| Ali-Mikhail-Haq | IFM | 0.8392378 | 0.8392506 | `-1.3e-5` | Passed |
| Clayton | MPL | 1.5340162 | 1.5340160 | `+1.9e-7` | Passed |
| Clayton | IFM | 1.4852343 | 1.4851670 | `+6.7e-5` | Passed |
| Frank | MPL | 7.7187608 | 7.7187610 | `-2.2e-7` | Passed |
| Frank | IFM | 8.1303210 | 8.1302590 | `+6.2e-5` | Passed |
| Gumbel | MPL | 2.0979531 | 2.0979530 | `+6.6e-8` | Passed |
| Gumbel | IFM | 2.0311985 | 2.0310340 | `+1.6e-4` | Passed |
| Joe | MPL | 2.6643256 | 2.6643260 | `-4.5e-7` | Passed |
| Joe | IFM | 2.9656127 | 2.9652690 | `+3.4e-4` | Passed |
| Gaussian | MPL | 0.8000853 | 0.8000820 | `+3.3e-6` | Passed |
| Gaussian | IFM | 0.7871334 | 0.7871479 | `-1.5e-5` | Passed |
| Student t | MPL | `[0.8157372, 5.0704726]` | Not claimed | N/A | Passed |
| Student t | IFM | `[0.8142171, 5.0055361]` | Not claimed | N/A | Passed |

The one-coordinate cells use production Differential Evolution with untouched default tolerances.
Same-point maximum log likelihood agrees within `1e-8` as a deterministic parameterization check;
scientific optimizer acceptance requires the production optimum inside the joint 95% likelihood-ratio
region (`chi-square(1)=3.841458820694124`). Student-t uses its artifact's same-point numerical check
and the two-coordinate cutoff `chi-square(2)=5.991464547107979`. Historical R coordinates are retained
only as provenance. The twelve original embedded-R methods were deliberately consolidated and removed
from discovery because their `1e-3` coordinate band was not statistically derived.

## Bivariate recovery and coincident frequency

Seven copula families were generated at exactly 1,000 matched pairs. Six one-coordinate families were
recovered under production DEMCzs defaults with central-95% parent inclusion, R-hat below 1.10, and
ESS at least 100. At Haden Smith's direction, the impractical Student-t MCMC identity was removed and
replaced by conditional copula MLE: `rho` satisfies the observed-information standardized-error rule,
while weak `nu` is judged only through the full-covariance delta-method band for symmetric tail
dependence. All seven current recovery identities passed. Coincident-frequency response surfaces for
independent, positively correlated, and negatively correlated Normal sums were compared with the closed form

$$X+Y\sim N(\mu_X+\mu_Y,\sigma_X^2+\sigma_Y^2+2\rho\sigma_X\sigma_Y).\tag{B.1}$$

All three response-surface cells passed after rho recovery was normalized to central-95% parent
inclusion, R-hat below 1.10, and ESS at least 100. Their fixed-grid AEP error remains a separate
numerical discretization check. Independent posterior resampling also matched the closed-form
Normal-sum posterior summaries under its declared deterministic oracle.

A fourth cell covers the distinct monotone nonlinear response
`Z=exp(0.01X+0.01Y)` under positive Gaussian-copula dependence. Because `log(Z)` is an exact linear
combination of correlated Normals, its independent oracle is Lognormal. The response-table numerical
error is checked separately at 0.015 AEP; Normal marginal MLE recovery and copula posterior diagnostics
own parent-fitting uncertainty; and the generating response must lie in central-95% propagated bands
at nonexceedance 0.10, 0.25, 0.50, 0.75, and 0.90. The N=1000 cell passed. Together the three
correlation-sign linear cells and this nonlinear propagation cell cover the distinct interactions
without a redundant Cartesian expansion.

## Conclusion

Fourteen independent copula-optimum cells, six Bayesian copula-recovery cells, one Student-t MLE
recovery cell, three closed-form coincident-frequency cells, one nonlinear response-recovery cell,
and one posterior-resampling cell passed.
The recovery claim remains conditional on fixed marginal fits, paired events, and the parameter regions
used by the fixtures; raw Student-t degrees-of-freedom recovery is not claimed.
