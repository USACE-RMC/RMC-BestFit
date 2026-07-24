<!-- technical-reference-status: complete -->

# Probability Distributions

[Back to Technical Reference](../index.md) · [Common univariate model](univariate.md) · [Parameterization crosswalk](../appendices/parameterization-crosswalk.md)

RMC.BestFit wraps RMC.Numerics probability distributions with mixed-observation likelihoods, parameter trends, priors, posterior inference, diagnostics, and hydrologic analysis outputs. The family chapters define the exact Numerics 2.1.4 constructor order and sign/log conventions; those definitions control the likelihood used by BestFit.

## Supported Univariate Families

| Family | API order | Essential convention | Chapter |
|---|---|---|---|
| Exponential | $(\xi,\alpha)$ | Location, scale | [Exponential](exponential.md) |
| Gamma | $(\theta,k)$ | Scale before shape | [Gamma](gamma.md) |
| Generalized extreme value | $(\xi,\alpha,\kappa)$ | Numerics shape is negative of common Coles shape | [GEV](generalized-extreme-value.md) |
| Generalized logistic | $(\xi,\alpha,\kappa)$ | Same support/sign transform as Numerics GEV | [GLO](generalized-logistic.md) |
| Generalized Normal | $(\xi,\alpha,\kappa)$ | Hosking GNO, not exponential-power family | [GNO](generalized-normal.md) |
| Generalized Pareto | $(\xi,\alpha,\kappa)$ | Negative Numerics shape is heavy upper tail | [GPD](generalized-pareto.md) |
| Gumbel | $(\xi,\alpha)$ | Zero-shape GEV | [Gumbel](gumbel.md) |
| Kappa Four | $(\xi,\alpha,\kappa,h)$ | Two shape parameters; open limiting-case finding | [Kappa Four](kappa-four.md) |
| Ln-Normal | $(m,s)$ | Natural-space arithmetic mean and SD | [Ln-Normal](ln-normal.md) |
| Logistic | $(\xi,\alpha)$ | Symmetric exponential tails | [Logistic](logistic.md) |
| Log-Normal | $(\mu_Y,\sigma_Y)$ | Moments of $Y=\log_{10}X$ | [Log-Normal](log-normal.md) |
| Log-Pearson III | $(\mu_Y,\sigma_Y,\gamma_Y)$ | Moments of $Y=\log_{10}X$ | [LP3](log-pearson-type-iii.md) |
| Normal | $(\mu,\sigma)$ | Arithmetic mean and SD | [Normal](normal.md) |
| Pearson III | $(\mu,\sigma,\gamma)$ | Arithmetic moments; reflected for negative skew | [PIII](pearson-type-iii.md) |
| Weibull | $(\lambda,k)$ | Scale before shape; zero lower endpoint | [Weibull](weibull.md) |

## Cross-Family Modeling Layers

The [common univariate model](univariate.md) explains how a selected family participates in exact, uncertain, censored, interval, threshold-count, stationary, and nonstationary likelihoods. The [data-frame chapter](../data-frame/index.md) defines each observation contribution, and [parameters and priors](../models/parameters-and-priors.md) defines bounds, fixed coefficients, Jeffreys scale priors, and quantile priors.

Advanced constructions have separate formulations:

- [Mixture models](mixture.md) represent latent populations.
- [Competing risks](competing-risks.md) combine maxima or minima of processes.
- [Point-process models](point-process.md) combine threshold exceedance occurrence and magnitude.
- [Composite analysis](composite.md) combines model outputs rather than asserting one parent density.

## Selection Discipline

Distribution selection must consider the sampling scheme, support, endpoint, tail class, generating process, parameter uncertainty, and extrapolation objective. Information criteria summarize relative predictive fit under their assumptions; they do not certify physical plausibility. Rare return levels should be reported with posterior uncertainty and the fitted tail/endpoint convention.

---

[Back to Technical Reference](../index.md)
