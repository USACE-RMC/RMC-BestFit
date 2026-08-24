<!-- verification-status: publication-draft -->

# Bivariate and Coincident-Frequency Analyses

## Bivariate copula estimation

An independent Python/SciPy implementation fits six one-parameter copula families to twelve fixed samples of one hundred matched observations. It implements each copula density directly, checks it against a numerical mixed derivative of the CDF, performs a 401-point grid scan, and refines the optimum with bounded minimization. BestFit must reproduce the independent optimum and maximum log likelihood; the historical R `copula` target is retained as a secondary comparison.

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

Production optima agree with the independent coordinates within `1e-5` relative with a `1e-6` scale floor, and maximum log likelihood agrees within `1e-8`. Historical R coordinates use a broader `1e-3` compatibility tolerance because those fixtures used a different pseudo-observation convention.

## Bivariate recovery and coincident frequency

Seven copula families were generated and recovered with production DEMCzs defaults. All seven Bayesian cells passed; the Student t cell completed in 470 seconds. Coincident-frequency response surfaces for independent, positively correlated, and negatively correlated Normal sums were compared with the closed form

$$X+Y\sim N(\mu_X+\mu_Y,\sigma_X^2+\sigma_Y^2+2\rho\sigma_X\sigma_Y).\tag{B.1}$$

All three response-surface cells passed. Independent posterior resampling also matched the closed-form Normal-sum posterior mean within 0.02 and interval limits within 0.05.

## Conclusion

Twelve independent copula-optimum cells, seven Bayesian copula-recovery cells, three closed-form coincident-frequency cells, and one posterior-resampling cell passed. The recovery claim remains conditional on the fixed marginal fits, paired events, and parameter regions used by the fixtures.
