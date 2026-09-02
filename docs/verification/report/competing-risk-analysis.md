<!-- verification-status: publication-draft -->

# Competing-Risk Analysis

## Dependence theory and independent simulation oracle

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

## Identified recovery designs

The historical 20-cell estimator cross-product was thinned to five BestFit recovery cells
over three predeclared, clearly identified dog-leg fixtures. Each uses seed 12345 and exactly
1,000 scalar composite observations. The labeled verification generator records theoretical
cause shares, fixed-seed hard wins, likelihood-responsibility soft counts, component dominance
mass, and responsibility crossovers before estimator execution.

| Fixture | Theoretical shares | Hard wins | BestFit results |
|---|---:|---:|---|
| Independent minimum: Weibull(50,1) + Weibull(80,3) | 72.7%, 27.3% | 720, 280 | MLE passed; Bayesian passed |
| Independent maximum: Weibull(100,3) + Gumbel(80,20) | 48.7%, 51.3% | 495, 505 | MLE passed; Bayesian passed without optional Jeffreys scale multiplier |
| Correlated minimum: Weibull(50,1) + Weibull(80,3), rho=0.6 | 78.7%, 21.3% | 776, 224 | MLE passed; Bayesian intentionally not run |

MLE uncertainty comes from the full competing-risk observed-information matrix without a ridge,
with every generating coordinate required to have absolute standardized error at most 1.96.
Bayesian recovery requires every ordered generating coordinate inside its central 95 percent
posterior interval, $\widehat R<1.10$, and ESS at least 100. All five retained exact BestFit
identities produced fresh one-result passing TRXs on 2 September 2026.

The redesigned maximum has one interior responsibility crossover at composite probability 0.474
and the expected Gumbel extreme-tail re-entry at 0.987. BestFit and Numerics MLE each recover it
under the full-likelihood coordinate rule. The former Weibull(80,2) + Gumbel(60,10) fixture remains
a documented single-start local-mode failure rather than a recovery test; direct cause-sum
evaluation ruled out a log-PDF formulation defect.

The optional Jeffreys scale multiplier makes the Bayesian maximum posterior kernel increase when
the Weibull scale approaches its lower boundary and that component disappears. An exact
default-prior diagnostic found a rank-3-of-4 MAP information matrix and a Weibull-scale 95 percent
interval of roughly [4.44E-12, 38.17], excluding the parent 100. With only that optional multiplier
disabled, while retaining the bounded parameter priors and every data, sampler, seed, and
acceptance setting, the exact Bayesian maximum identity passed. The production prior default was
not changed. A balanced three-Weibull candidate was separately removed after MLE
boundary/observed-information failures and a Bayesian parent-interval miss showed that cause
balance alone did not identify its coordinates.

## Limitations and provenance

Cause labeling, component order, dependence mode, and every estimator-owned coordinate are fixed before
execution. Recovery is not claimed for the former local-mode fixture, the removed balanced three-component
candidate, or the correlated Bayesian case. The default-prior boundary diagnostic is retained as a
limitation, not counted as a passing recovery result. Generator seed 12345, N=1000, parameter ordering,
priors, and covariance source are recorded in the source and catalog.
