<!-- verification-status: publication-draft -->

# Competing-Risk Analysis

For a Gaussian copula with latent correlation $\rho$, the independent oracle uses

$$\rho_S=\frac{6}{\pi}\arcsin(\rho/2),\qquad
P[\max(X_1,X_2)\le0]=\frac14+\frac{\arcsin(\rho)}{2\pi}.\tag{C.1}$$

Each dependence mode generated 40,000 observations with seed 24681357. Empirical rank tolerance was six standard errors, $6/\sqrt{n-1}$; CDF tolerance was six binomial standard errors plus one observation.

| Dependence mode | Target | Result |
|---|---|---:|
| Independent | $\rho_S=0$, maximum CDF 0.25 | Passed |
| Perfect positive | $\rho_S=1$, maximum CDF 0.5 | Passed |
| Perfect negative limit | Equation (C.1) at the implemented numerical limit | Passed |
| User correlation matrix | Equation (C.1) at the configured correlation | Passed |

Ten supported one- through three-component minimum/maximum designs were generated with seed 12345. Two-component cases used 1,000 observations and three-component cases 1,500. Every MLE fit passed the declared combined-CDF recovery rule. Four independent-minimum Bayesian designs also passed under production defaults.

| Recovery group | Cells | Acceptance | Result |
|---|---:|---|---:|
| MLE, all supported fixture designs | 10 | Maximum CDF error 0.05 or 0.06 | 10 of 10 passed |
| Bayesian, independently supported minimum designs | 4 | CDF bound, $\widehat R<1.1$, ESS greater than 100 | 4 of 4 passed |

The report makes no Bayesian recovery claim for six difficult maximum or correlated designs whose posterior geometry, convergence, or uncertainty-curve behavior did not satisfy the predeclared criteria. No tolerance, prior, sampler, seed, or likelihood was changed to convert those designs into passes.
