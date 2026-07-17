# Spatial Extremes Analysis

[<- Previous: Time Series](../analysis/time-series.md) | [Back to Index](../../index.md) | [Next: Trend and Link Functions ->](../support/trend-and-link-functions.md)

Spatial extremes analysis extends classical univariate extreme value theory to multiple locations, enabling regional flood frequency analysis with rigorous uncertainty quantification. This chapter provides a comprehensive introduction to spatial GEV modeling using Bayesian Hierarchical Models (BHM), suitable for readers with foundational knowledge of probability and statistics but limited exposure to spatial statistics or extreme value theory.

---

## Table of Contents

1. [Introduction and Motivation](#1-introduction-and-motivation)
2. [Foundations: Extreme Value Theory](#2-foundations-extreme-value-theory)
3. [Regional Frequency Analysis](#3-regional-frequency-analysis)
4. [The Bayesian Hierarchical Model Framework](#4-the-bayesian-hierarchical-model-framework)
5. [Spatial Correlation Structures](#5-spatial-correlation-structures)
6. [Gaussian Copula for Inter-Site Dependence](#6-gaussian-copula-for-inter-site-dependence)
7. [Spatial Regression on GEV Parameters](#7-spatial-regression-on-gev-parameters)
8. [Spatially Correlated Regression Errors](#8-spatially-correlated-regression-errors)
9. [Likelihood Formulation](#9-likelihood-formulation)
10. [Bayesian Inference](#10-bayesian-inference)
11. [Model Selection and Validation](#11-model-selection-and-validation)
12. [Implementation in RMC-BestFit](#12-implementation-in-rmc-bestfit)
13. [Assumptions and Limitations](#13-assumptions-and-limitations)
14. [References](#14-references)

---

## 1. Introduction and Motivation

### The Challenge of Rare Events

Hydrologic design—whether for dams, levees, bridges, or stormwater systems—requires estimating the magnitude of floods that occur rarely: the 100-year flood, the 500-year flood, or even rarer events. These rare quantiles lie far beyond the range of observed data at most gauging stations. A typical station with 50 years of record provides only modest information about events with return periods exceeding the record length.

### The Power of Regional Information

The fundamental insight of regional frequency analysis is that nearby locations experience similar flood-generating mechanisms. A station with 30 years of record gains inferential power by "borrowing strength" from neighboring stations. If 10 stations each have 30 years of data, the region collectively contains 300 station-years of information—far more than any single station possesses.

### Why Spatial Models?

Classical regional frequency analysis methods, such as the index flood method [[1]](#1), assume that:
1. All sites share the same standardized distribution (the "growth curve")
2. Sites differ only in a scaling factor (the index flood)
3. Observations at different sites are independent

These assumptions are often violated in practice:
- Distribution parameters vary smoothly across space
- Nearby sites experience correlated flood events
- Environmental covariates (elevation, drainage area, precipitation) influence flood characteristics

Spatial GEV models address these limitations by explicitly modeling:
- **Spatial variation** in distribution parameters through regression
- **Spatial dependence** in observations through copulas
- **Residual spatial correlation** through Gaussian processes

---

## 2. Foundations: Extreme Value Theory

### Block Maxima and the GEV Distribution

The Generalized Extreme Value (GEV) distribution arises naturally as the limiting distribution of properly rescaled block maxima. If $X_1, X_2, \ldots, X_n$ are independent observations from some parent distribution, and $M_n = \max(X_1, \ldots, X_n)$, then under mild regularity conditions [[2]](#2):

```math
\frac{M_n - b_n}{a_n} \xrightarrow{d} G
```

where $G$ is the GEV distribution with CDF:

```math
G(x; \xi, \alpha, \kappa) = \exp\left\{-\left[1 + \kappa\left(\frac{x - \xi}{\alpha}\right)\right]^{-1/\kappa}\right\}
```

for $1 + \kappa(x - \xi)/\alpha > 0$, where:
- $\xi \in \mathbb{R}$ is the **location** parameter (central tendency)
- $\alpha > 0$ is the **scale** parameter (spread)
- $\kappa \in \mathbb{R}$ is the **shape** parameter (tail behavior)

### Interpretation of Parameters

**Location ($\xi$)**: The mode of the distribution occurs near $\xi$. For annual maximum floods, $\xi$ represents a "typical" annual maximum.

**Scale ($\alpha$)**: Controls the spread of the distribution. Larger $\alpha$ means greater variability in annual maxima.

**Shape ($\kappa$)**: Determines tail behavior:
- $\kappa < 0$ (Fréchet type): Heavy upper tail, no upper bound. Common for river floods.
- $\kappa = 0$ (Gumbel type): Exponential tail decay. Limit as $\kappa \to 0$.
- $\kappa > 0$ (Weibull type): Bounded upper tail at $\xi - \alpha/\kappa$.

### Quantile Function

The $p$-quantile (value exceeded with probability $1-p$) is:

```math
x_p = \xi + \frac{\alpha}{\kappa}\left[(-\log p)^{-\kappa} - 1\right]
```

For the Gumbel case ($\kappa = 0$):

```math
x_p = \xi - \alpha \log(-\log p)
```

**Example**: The 100-year flood corresponds to $p = 0.99$ (or equivalently, 1% annual exceedance probability).

---

## 3. Regional Frequency Analysis

### The Index Flood Method

The classical index flood method [[1]](#1) assumes that at each site $j$, the annual maximum $Y_j$ follows:

```math
Y_j = \mu_j \cdot Z
```

where $\mu_j$ is a site-specific index flood (typically the mean or median) and $Z$ is a dimensionless "growth curve" common to all sites in the region. This implies all sites share identical shape and dimensionless scale, differing only by a multiplicative factor.

### Limitations of Classical RFA

1. **Homogeneity assumption**: Requires defining "homogeneous regions" where growth curves are identical—a subjective and often unrealistic requirement.

2. **Independence assumption**: Ignores that nearby sites experience floods simultaneously (e.g., basin-wide storm events).

3. **No covariate information**: Cannot incorporate physical explanatory variables.

4. **Two-stage estimation**: First estimates growth curve, then index floods, without proper uncertainty propagation.

### The Hierarchical Alternative

Bayesian Hierarchical Models (BHM) address these limitations by:
- Allowing parameters to vary spatially through regression
- Modeling inter-site dependence explicitly
- Estimating all parameters simultaneously with full uncertainty propagation
- Eliminating the need for discrete homogeneous regions

---

## 4. The Bayesian Hierarchical Model Framework

The spatial GEV model follows the hierarchical framework developed by Renard et al. [[3]](#3), [[4]](#4), consisting of three levels:

### Level 1: Data Model

At each site $j \in \{1, \ldots, S\}$ with $n_j$ observations:

```math
Y_{ij} \mid \theta_j \sim \text{GEV}(\xi_j, \alpha_j, \kappa_j), \quad i = 1, \ldots, n_j
```

where $\theta_j = (\xi_j, \alpha_j, \kappa_j)$ are the site-specific GEV parameters.

**Spatial Dependence**: Within a given year, observations across sites may be correlated (e.g., a regional storm produces high flows at multiple stations simultaneously). This dependence is captured through a Gaussian copula (see [Section 6](#6-gaussian-copula-for-inter-site-dependence)).

### Level 2: Process Model

GEV parameters at each site are functions of:
1. **Spatial regression**: Linear functions of covariates (location, elevation, drainage area, etc.)
2. **Spatially correlated errors**: Gaussian process deviations from the regression surface

For the location parameter with log-link:

```math
\log(\xi_j) = \beta_0^\xi + \beta_1^\xi X_{1j} + \beta_2^\xi X_{2j} + \varepsilon_j^\xi
```

where:
- $\beta_0^\xi$ is the intercept
- $\beta_k^\xi$ are regression coefficients
- $X_{kj}$ are covariate values at site $j$ (e.g., latitude, longitude, elevation)
- $\varepsilon_j^\xi$ is a spatially correlated error term

The vector of errors $\boldsymbol{\varepsilon}^\xi = (\varepsilon_1^\xi, \ldots, \varepsilon_S^\xi)$ follows:

```math
\boldsymbol{\varepsilon}^\xi \sim \mathcal{N}(\mathbf{0}, \sigma_\xi^2 \mathbf{R}_\xi)
```

where $\mathbf{R}_\xi$ is a correlation matrix determined by a spatial correlation function.

### Level 3: Prior Model

Hyperparameters (regression coefficients, error variance, correlation parameters) receive prior distributions:

```math
\begin{aligned}
\beta_k^\xi &\sim \text{Uniform}(L_\beta, U_\beta) \\
\sigma_\xi &\sim \text{Uniform}(0, U_\sigma) \text{ or Jeffreys prior} \\
\text{range} &\sim \text{Uniform}(0, U_r)
\end{aligned}
```

### Model Schematic

```
┌─────────────────────────────────────────────────────────────────┐
│                    LEVEL 3: PRIORS                              │
│   β (regression coefficients)  σ (error std dev)  r (range)    │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                 LEVEL 2: PROCESS MODEL                          │
│                                                                 │
│   log(ξⱼ) = β₀ + β₁X₁ⱼ + β₂X₂ⱼ + εⱼ    where ε ~ MVN(0, σ²R)   │
│   log(αⱼ) = ...                                                 │
│        κⱼ = ...                                                 │
│                                                                 │
│   Site-specific GEV parameters: θⱼ = (ξⱼ, αⱼ, κⱼ)               │
└─────────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────────┐
│                   LEVEL 1: DATA MODEL                           │
│                                                                 │
│   Yᵢⱼ | θⱼ ~ GEV(ξⱼ, αⱼ, κⱼ)  with Gaussian copula dependence  │
│                                                                 │
│   Site 1: Y₁₁, Y₁₂, ..., Y₁ₙ₁                                   │
│   Site 2: Y₂₁, Y₂₂, ..., Y₂ₙ₂                                   │
│   ...                                                           │
│   Site S: Yₛ₁, Yₛ₂, ..., Yₛₙₛ                                   │
└─────────────────────────────────────────────────────────────────┘
```

---

## 5. Spatial Correlation Structures

Spatial correlation functions describe how similarity between locations decreases with distance. Let $h = \|\mathbf{s}_i - \mathbf{s}_j\|$ denote the Euclidean distance between sites $i$ and $j$. The correlation function $\rho(h)$ must satisfy:
- $\rho(0) = 1$ (perfect correlation at zero distance)
- $0 \leq \rho(h) \leq 1$ for all $h \geq 0$
- $\rho(h) \to 0$ as $h \to \infty$ (decay to independence)
- Positive definiteness (required for valid covariance matrices)

### Basic Exponential

```math
\rho(h) = \exp\left(-\frac{h}{r}\right)
```

where $r > 0$ is the **range** (or **scale**) parameter.

**Properties**:
- Exponential decay with distance
- Practical range (correlation drops to 0.05): approximately $3r$
- Infinitely divisible (valid for any positive definite operation)
- Non-differentiable at the origin (rough process realizations)

**When to use**: Default choice; robust and widely applicable.

### Powered Exponential

```math
\rho(h) = \exp\left(-\left(\frac{h}{r}\right)^p\right)
```

where $r > 0$ is the range and $0 < p \leq 2$ is the **power** parameter.

**Special cases**:
- $p = 1$: Basic exponential
- $p = 2$: Gaussian (squared exponential)

**Properties**:
- Higher $p$ gives smoother correlation decay
- Gaussian ($p = 2$) produces infinitely differentiable (smooth) process realizations
- Practical range varies with $p$

**When to use**: When process smoothness is important; $p \approx 1.5$ often works well.

### Spherical

```math
\rho(h) = \begin{cases}
1 - \frac{3h}{2r} + \frac{h^3}{2r^3} & \text{if } h < r \\
0 & \text{if } h \geq r
\end{cases}
```

**Properties**:
- **Compact support**: Exactly zero correlation beyond range $r$
- Computational advantage for large networks (sparse correlation matrices)
- Transition from correlation to independence at distance $r$

**When to use**: When distant sites are believed truly independent; reduces computational burden.

### Comparison

| Property | Basic Exponential | Powered Exponential | Spherical |
|----------|-------------------|---------------------|-----------|
| Parameters | 1 (range) | 2 (range, power) | 1 (range) |
| Smoothness | Rough | Adjustable | Moderate |
| Compact support | No | No | Yes |
| Computation | O(S²) | O(S²) | O(S² sparse) |

### Implementation in RMC-BestFit

```cs
// Basic exponential correlation
var basicExp = new BasicExponential(range: 30.0);

// Powered exponential (p = 1.5)
var poweredExp = new PoweredExponential(range: 30.0, power: 1.5);

// Spherical correlation
var spherical = new Spherical(range: 50.0);
```

---

## 6. Gaussian Copula for Inter-Site Dependence

### The Copula Concept

When analyzing regional extremes, observations at different sites in the same year are typically not independent—a major storm produces large floods at multiple nearby stations. A **copula** models this dependence structure separately from the marginal distributions [[5]](#5).

**Sklar's Theorem**: Any multivariate distribution can be decomposed as:

```math
F(y_1, \ldots, y_S) = C(F_1(y_1), \ldots, F_S(y_S))
```

where $F_j$ are the marginal CDFs and $C: [0,1]^S \to [0,1]$ is the copula function capturing dependence.

### The Gaussian Copula

The Gaussian copula is defined through the multivariate normal distribution:

```math
C(u_1, \ldots, u_S) = \Phi_{\mathbf{R}}(\Phi^{-1}(u_1), \ldots, \Phi^{-1}(u_S))
```

where:
- $\Phi$ is the standard normal CDF
- $\Phi^{-1}$ is the standard normal quantile function
- $\Phi_{\mathbf{R}}$ is the CDF of the multivariate normal with correlation matrix $\mathbf{R}$

### Copula Density

The copula density is:

```math
c(u_1, \ldots, u_S) = \frac{\phi_{\mathbf{R}}(z_1, \ldots, z_S)}{\prod_{j=1}^S \phi(z_j)}
```

where $z_j = \Phi^{-1}(u_j)$ and $\phi_{\mathbf{R}}$ is the multivariate normal PDF.

The log-copula density is:

```math
\log c(\mathbf{u}) = -\frac{1}{2}\log|\mathbf{R}| - \frac{1}{2}\mathbf{z}^\top(\mathbf{R}^{-1} - \mathbf{I})\mathbf{z}
```

### Constructing the Correlation Matrix

The correlation matrix $\mathbf{R}$ for the Gaussian copula uses the same spatial correlation functions from [Section 5](#5-spatial-correlation-structures):

```math
R_{ij} = \rho(h_{ij})
```

where $h_{ij}$ is the distance between sites $i$ and $j$.

### Joint Likelihood with Copula

The joint density of observations in year $i$ is:

```math
f(y_{i1}, \ldots, y_{iS}) = c(F_1(y_{i1}), \ldots, F_S(y_{iS})) \cdot \prod_{j=1}^S f_j(y_{ij})
```

where $f_j$ and $F_j$ are the site-specific GEV density and CDF.

**Log-likelihood contribution from observation $i$**:

```math
\ell_i = \log c\left(F_1(y_{i1}), \ldots, F_S(y_{iS})\right) + \sum_{j=1}^S \log f_j(y_{ij})
```

### Why Gaussian Copula?

**Advantages**:
1. Mathematically tractable (closed-form density)
2. Flexible range of dependence (correlation matrix)
3. Natural spatial interpretation (correlation decays with distance)
4. Efficient computation via Cholesky decomposition

**Limitations**:
1. Symmetric dependence (cannot model asymmetric tail dependence)
2. Tail independence (joint extremes less likely than heavy-tailed copulas)
3. May underestimate joint flood risk at very high return periods

For flood frequency analysis, the Gaussian copula is typically adequate because:
- Annual maxima from different sites have moderate dependence
- Primary interest is in marginal quantiles, not joint exceedance probabilities
- More complex copulas (e.g., extreme-value copulas) add parameters without clear practical benefit

---

## 7. Spatial Regression on GEV Parameters

### Link Functions

GEV parameters have natural constraints that require appropriate link functions:
- **Location ($\xi$)**: Often positive for flood data, so log-link ensures positivity
- **Scale ($\alpha$)**: Must be positive, so log-link is essential
- **Shape ($\kappa$)**: Typically small ($|\kappa| < 0.5$), so identity link with bounded priors

### Location Parameter

With log-link:

```math
\log(\xi_j) = \beta_0^\xi + \beta_1^\xi X_{1j} + \beta_2^\xi X_{2j} + \cdots + \beta_K^\xi X_{Kj}
```

Equivalently:

```math
\xi_j = \exp\left(\beta_0^\xi + \sum_{k=1}^K \beta_k^\xi X_{kj}\right)
```

**Interpretation**: Regression coefficients represent multiplicative effects. A unit increase in covariate $X_k$ multiplies location by $\exp(\beta_k^\xi)$.

### Scale Parameter

With log-link:

```math
\log(\alpha_j) = \beta_0^\alpha + \sum_{k=1}^K \beta_k^\alpha X_{kj}
```

This ensures $\alpha_j > 0$ for all covariate values.

### Shape Parameter

With identity link:

```math
\kappa_j = \beta_0^\kappa + \sum_{k=1}^K \beta_k^\kappa X_{kj}
```

Shape is kept near zero through bounded priors, typically $|\kappa_j| < 0.5$.

### Common Covariates

For spatial flood frequency analysis, useful covariates include:

| Covariate | Symbol | Effect on Floods |
|-----------|--------|------------------|
| Longitude | $X$ | East-west climate gradients |
| Latitude | $Y$ | North-south climate gradients |
| Elevation | $Z$ | Orographic precipitation effects |
| Drainage area | $A$ | Larger basins → larger floods |
| Mean precipitation | $P$ | Wetter regions → larger floods |
| Basin slope | $S$ | Steeper basins → faster concentration |

### Example: Location Regression

Consider a simple model with X and Y coordinates as covariates:

```math
\log(\xi_j) = \beta_0 + \beta_X \cdot X_j + \beta_Y \cdot Y_j
```

For a 100×100 km region with:
- $\beta_0 = 8.987$ (intercept, log-scale)
- $\beta_X = 0.005$ (X coefficient)
- $\beta_Y = 0.008$ (Y coefficient)

**At corner (0, 0)**:
```math
\xi = \exp(8.987) \approx 8000
```

**At corner (100, 100)**:
```math
\xi = \exp(8.987 + 0.5 + 0.8) = \exp(10.287) \approx 29,500
```

The location parameter increases by a factor of 3.7 across the region due to spatial trends.

---

## 8. Spatially Correlated Regression Errors

### Motivation

Even with spatial regression, residual variation remains that is:
1. **Spatially structured**: Nearby sites have similar residuals
2. **Not captured by covariates**: Due to unmeasured local effects

This variation is modeled as a Gaussian process on the regression residuals.

### Mathematical Formulation

For the location parameter:

```math
\log(\xi_j) = \underbrace{\beta_0^\xi + \sum_{k=1}^K \beta_k^\xi X_{kj}}_{\text{spatial trend}} + \underbrace{\varepsilon_j^\xi}_{\text{spatial error}}
```

where the error vector follows:

```math
\boldsymbol{\varepsilon}^\xi = (\varepsilon_1^\xi, \ldots, \varepsilon_S^\xi)^\top \sim \mathcal{N}(\mathbf{0}, \sigma_\xi^2 \mathbf{R})
```

The covariance matrix is:

```math
\text{Cov}(\varepsilon_i^\xi, \varepsilon_j^\xi) = \sigma_\xi^2 \rho(h_{ij})
```

### Gaussian Process Prior

The spatial errors define a **Gaussian process** prior on deviations from the regression surface. This is equivalent to kriging in geostatistics, providing:
- Smooth interpolation between observed sites
- Proper uncertainty quantification
- Shrinkage toward the regression surface where data is sparse

### Error Parameters

Each parameter with spatial errors has two hyperparameters:

| Hyperparameter | Symbol | Interpretation |
|----------------|--------|----------------|
| Error standard deviation | $\sigma$ | Magnitude of spatial deviations |
| Correlation range | $r$ | Distance over which errors remain correlated |

### The Full Model

With spatial errors on all parameters:

```math
\begin{aligned}
\log(\xi_j) &= \beta_0^\xi + \sum_k \beta_k^\xi X_{kj} + \varepsilon_j^\xi, \quad \boldsymbol{\varepsilon}^\xi \sim \mathcal{N}(\mathbf{0}, \sigma_\xi^2 \mathbf{R}_\xi) \\
\log(\alpha_j) &= \beta_0^\alpha + \sum_k \beta_k^\alpha X_{kj} + \varepsilon_j^\alpha, \quad \boldsymbol{\varepsilon}^\alpha \sim \mathcal{N}(\mathbf{0}, \sigma_\alpha^2 \mathbf{R}_\alpha) \\
\kappa_j &= \beta_0^\kappa + \sum_k \beta_k^\kappa X_{kj} + \varepsilon_j^\kappa, \quad \boldsymbol{\varepsilon}^\kappa \sim \mathcal{N}(\mathbf{0}, \sigma_\kappa^2 \mathbf{R}_\kappa)
\end{aligned}
```

The three error processes $\boldsymbol{\varepsilon}^\xi$, $\boldsymbol{\varepsilon}^\alpha$, $\boldsymbol{\varepsilon}^\kappa$ are assumed independent, though they may share the same correlation function.

---

## 9. Likelihood Formulation

### Full Log-Likelihood

The full log-likelihood combines contributions from:
1. **Marginal GEV likelihoods** at each site
2. **Copula likelihood** for inter-site dependence
3. **Gaussian process priors** on spatial errors

```math
\ell(\theta) = \ell_{\text{data}}(\theta) + \ell_{\text{errors}}(\theta)
```

### Data Likelihood

**Without copula** (conditional independence given parameters):

```math
\ell_{\text{data}} = \sum_{i=1}^{n} \sum_{j=1}^{S} w_j \cdot \log f_{\text{GEV}}(y_{ij}; \xi_j, \alpha_j, \kappa_j)
```

where $w_j$ are optional site weights.

The GEV log-density is:

```math
\log f_{\text{GEV}}(y; \xi, \alpha, \kappa) = -\log\alpha - \left(1 + \frac{1}{\kappa}\right)\log\left(1 + \kappa\frac{y-\xi}{\alpha}\right) - \left(1 + \kappa\frac{y-\xi}{\alpha}\right)^{-1/\kappa}
```

For $\kappa = 0$ (Gumbel case):

```math
\log f_{\text{Gumbel}}(y; \xi, \alpha) = -\log\alpha - \frac{y-\xi}{\alpha} - \exp\left(-\frac{y-\xi}{\alpha}\right)
```

**With copula**:

```math
\ell_{\text{data}} = \sum_{i=1}^{n} \left[\log c_{\mathbf{R}}(u_{i1}, \ldots, u_{iS}) + \sum_{j=1}^{S} w_j \cdot \log f_j(y_{ij})\right]
```

where $u_{ij} = F_j(y_{ij})$ is the probability integral transform.

### Spatial Error Prior Likelihood

The Gaussian process prior on errors contributes:

```math
\ell_{\text{errors}} = \sum_{\theta \in \{\xi, \alpha, \kappa\}} \mathbb{1}_{\text{errors on } \theta} \cdot \log \phi_S(\boldsymbol{\varepsilon}^\theta; \mathbf{0}, \sigma_\theta^2 \mathbf{R}_\theta)
```

where $\phi_S$ is the $S$-dimensional multivariate normal density:

```math
\log \phi_S(\boldsymbol{\varepsilon}; \mathbf{0}, \boldsymbol{\Sigma}) = -\frac{S}{2}\log(2\pi) - \frac{1}{2}\log|\boldsymbol{\Sigma}| - \frac{1}{2}\boldsymbol{\varepsilon}^\top \boldsymbol{\Sigma}^{-1} \boldsymbol{\varepsilon}
```

### Handling Missing Data

Annual maximum series often have gaps. Missing values are handled naturally:
- Omit missing observations from marginal likelihood
- Use only available observations for copula contributions
- No interpolation or imputation required

### Numerical Considerations

1. **Support constraints**: Return $-\infty$ if $1 + \kappa(y-\xi)/\alpha \leq 0$
2. **Scale positivity**: Return $-\infty$ if $\alpha \leq 0$
3. **Cholesky stability**: Add small ridge ($10^{-8}$) to correlation matrix diagonal
4. **Overflow protection**: Use log-scale computations throughout

---

## 10. Bayesian Inference

### Posterior Distribution

The posterior distribution of all parameters is:

```math
\pi(\theta \mid \mathbf{Y}) \propto \mathcal{L}(\mathbf{Y} \mid \theta) \cdot \pi(\theta)
```

where $\theta$ includes:
- Regression coefficients $\beta$
- Error hyperparameters $(\sigma, r)$ for each parameter
- Copula correlation parameters
- Site-specific spatial errors $\varepsilon_j$

### MCMC Sampling

RMC-BestFit uses the **DEMCzs** sampler (Differential Evolution Markov Chain with snooker update) [[6]](#6), which offers:
- Self-tuning proposal distributions
- Robust multimodal exploration
- Parallel chain execution
- No manual tuning required

### Prior Distributions

**Regression coefficients**:
```math
\beta_k \sim \text{Uniform}(L_\beta, U_\beta)
```

Bounds are set wide enough to be non-informative but finite for numerical stability.

**Error standard deviations**:
```math
\sigma \sim \text{Uniform}(\epsilon, U_\sigma) \quad \text{or} \quad \sigma \sim \text{Jeffreys}(\propto 1/\sigma)
```

**Correlation range**:
```math
r \sim \text{Uniform}(\epsilon, U_r)
```

where $U_r$ is typically 2-3 times the maximum inter-site distance.

**Spatial errors**:

The site-specific errors $\varepsilon_j$ are sampled jointly or as part of Gibbs updates, with the Gaussian process prior providing regularization.

### Convergence Diagnostics

| Diagnostic | Target Value | Interpretation |
|------------|--------------|----------------|
| $\hat{R}$ (Gelman-Rubin) | $< 1.1$ | Chains have converged to same distribution |
| ESS (Effective Sample Size) | $> 400$ | Sufficient independent samples |
| Trace plots | No trends, good mixing | Visual confirmation of stationarity |

### Point Estimates

Two point estimators are available:

1. **Posterior Mean**: $\hat{\theta} = E[\theta \mid \mathbf{Y}]$
   - Minimizes squared error loss
   - Affected by posterior skewness

2. **Posterior Mode (MAP)**: $\hat{\theta} = \arg\max_\theta \pi(\theta \mid \mathbf{Y})$
   - Maximum a posteriori estimate
   - Coincides with MLE when priors are flat

---

## 11. Model Selection and Validation

### Information Criteria

**Deviance Information Criterion (DIC)** [[7]](#7):

```math
\text{DIC} = \bar{D} + p_D
```

where $\bar{D}$ is the posterior mean deviance and $p_D$ is the effective number of parameters.

**Watanabe-Akaike Information Criterion (WAIC)** [[8]](#8):

```math
\text{WAIC} = -2 \cdot \text{lppd} + 2 \cdot p_{\text{WAIC}}
```

where lppd is the log pointwise predictive density.

**Lower values indicate better models** (balancing fit and complexity).

### Leave-One-Out Cross-Validation

LOO-CV with Pareto-Smoothed Importance Sampling (PSIS-LOO) [[9]](#9) provides:
- Estimate of out-of-sample predictive accuracy
- Diagnostic for influential observations
- No refitting required

### Posterior Predictive Checks

1. **Quantile comparison**: Compare observed vs. predicted quantiles at each site
2. **Return level plots**: Visual comparison of fitted curves to data
3. **Residual analysis**: Check for spatial patterns in residuals

### Model Hierarchy Testing

Build models of increasing complexity:

| Model | Copula | Regression | Spatial Errors |
|-------|--------|------------|----------------|
| Baseline | No | None | None |
| + Copula | Yes | None | None |
| + Location regression | Yes | Location | None |
| + Scale regression | Yes | Loc + Scale | None |
| + Location errors | Yes | Loc + Scale | Location |
| Full BHM | Yes | All | All |

Compare each step using DIC/WAIC to justify added complexity.

---

## 12. Implementation in RMC-BestFit

### Model Classes

| Class | Description |
|-------|-------------|
| `SpatialGEV` | Main spatial GEV model implementing the full BHM |
| `GeneralLinearFunction` | Spatial regression for GEV parameters |
| `SpatialRegressionErrors` | Gaussian process for spatially correlated errors |
| `GaussianCopula` | Inter-site dependence modeling |
| `BasicExponential` | Exponential correlation function |
| `PoweredExponential` | Powered exponential correlation function |
| `Spherical` | Spherical correlation function |

### Basic Usage

```cs
using RMC.BestFit.Models;
using RMC.BestFit.Models.SpatialExtremes;

// Prepare data: nObs × nSites matrix
double[,] atSiteData = LoadAtSiteData();     // Annual maxima
double[,] coordinates = LoadCoordinates();   // Site coordinates [S × 2]

// Create covariate matrix (X and Y coordinates)
int nSites = coordinates.GetLength(0);
var covariates = new double[nSites, 2];
for (int j = 0; j < nSites; j++)
{
    covariates[j, 0] = coordinates[j, 0]; // X
    covariates[j, 1] = coordinates[j, 1]; // Y
}

// Create GeneralLinearFunction for each parameter
var location = new GeneralLinearFunction("Location", covariates);
var scale = new GeneralLinearFunction("Scale", covariates);
var shape = new GeneralLinearFunction("Shape"); // Intercept only

// Create spatial GEV model
var model = new SpatialGEV(atSiteData, coordinates, location, scale, shape)
{
    UseLogLinkForLocation = true,
    UseLogLinkForScale = true
};
```

### Adding Spatial Errors

```cs
// Create spatial error models
var locErrors = new SpatialRegressionErrors(coordinates, new BasicExponential(range: 30.0));
var sclErrors = new SpatialRegressionErrors(coordinates, new BasicExponential(range: 40.0));

// Enable spatial errors
model.UseLocationErrors = true;
model.LocationErrors = locErrors;

model.UseScaleErrors = true;
model.ScaleErrors = sclErrors;

model.SetDefaultParameters();
```

### Adding Copula Dependence

```cs
// Create Gaussian copula with exponential correlation
var copula = new GaussianCopula(coordinates, new BasicExponential(range: 30.0));

model.UseCopulaDependence = true;
model.SpatialDependence = copula;

model.SetDefaultParameters();
```

### Running Bayesian Analysis

```cs
using RMC.BestFit.Analyses;

var analysis = new SpatialGEVAnalysis(model);

// Configure MCMC
analysis.BayesianAnalysis.Iterations = 20000;
analysis.BayesianAnalysis.WarmupIterations = 10000;
analysis.BayesianAnalysis.ThinningInterval = 10;
analysis.BayesianAnalysis.NumberOfChains = 4;

// Run analysis
await analysis.RunAsync();

// Access results
var map = analysis.BayesianAnalysis.Results.MAP.Values;
var posteriorMean = analysis.BayesianAnalysis.Results.ParameterResults
    .Select(p => p.SummaryStatistics.Mean)
    .ToArray();

// Get site-specific quantiles
foreach (var siteResult in analysis.SiteResults)
{
    Console.WriteLine($"Site: {siteResult.SiteName}");
    Console.WriteLine($"  100-year flood: {siteResult.Q100:F0}");
    Console.WriteLine($"  500-year flood: {siteResult.Q500:F0}");
}
```

---

## 13. Assumptions and Limitations

### Model Assumptions

1. **GEV marginals**: Annual maxima follow GEV distributions at each site
2. **Stationarity**: Distribution parameters constant over time (no trends)
3. **Gaussian copula**: Inter-site dependence adequately captured by Gaussian copula
4. **Gaussian process**: Spatial errors follow multivariate normal distribution
5. **Isotropic correlation**: Correlation depends only on distance, not direction
6. **Euclidean distance**: Distance measured as straight-line (may be inappropriate for river networks)

### Limitations

1. **Computational complexity**: Full model scales as $O(S^3)$ due to matrix operations
2. **No non-stationarity**: Time trends not currently supported (see univariate models)
3. **Limited copula choice**: Only Gaussian copula; no extreme-value copulas
4. **Isotropy**: Cannot model anisotropic correlation (directional dependence)
5. **Fixed correlation functions**: Cannot estimate correlation function parameters jointly
6. **No prediction at ungauged sites**: Currently supports only gauged site estimation

### Data Requirements

| Requirement | Minimum | Recommended |
|-------------|---------|-------------|
| Number of sites | 3 | 10+ |
| Observations per site | 10 | 30+ |
| Total observations | 100 | 500+ |
| Parameter identifiability | 500+ total for full BHM |

### When to Use Simpler Models

| Situation | Recommended Model |
|-----------|-------------------|
| Single site | Univariate GEV |
| Few sites (< 5) | Independent univariate models |
| No spatial trend | Homogeneous regional model |
| Short records | Index flood method |
| Interest in joint exceedance | Consider max-stable processes |

---

## 14. References

<a id="1">[1]</a> J. R. M. Hosking and J. R. Wallis, *Regional Frequency Analysis: An Approach Based on L-Moments*, Cambridge, UK: Cambridge University Press, 1997.

<a id="2">[2]</a> S. Coles, *An Introduction to Statistical Modeling of Extreme Values*, London, UK: Springer, 2001.

<a id="3">[3]</a> B. Renard, V. Garreta, and M. Lang, "An application of Bayesian analysis and MCMC methods to the estimation of a regional trend in annual maxima," *Water Resources Research*, vol. 42, W12422, 2006.

<a id="4">[4]</a> B. Renard, "A Bayesian hierarchical approach to regional frequency analysis," *Water Resources Research*, vol. 47, W11513, 2011.

<a id="5">[5]</a> B. Renard and M. Lang, "Use of a Gaussian copula for multivariate extreme value analysis: Some case studies in hydrology," *Advances in Water Resources*, vol. 30, no. 4, pp. 897-912, 2007.

<a id="6">[6]</a> C. J. F. ter Braak and J. A. Vrugt, "Differential Evolution Markov Chain with snooker updater and fewer chains," *Statistics and Computing*, vol. 18, no. 4, pp. 435-446, 2008.

<a id="7">[7]</a> D. J. Spiegelhalter, N. G. Best, B. P. Carlin, and A. van der Linde, "Bayesian measures of model complexity and fit," *Journal of the Royal Statistical Society: Series B*, vol. 64, no. 4, pp. 583-639, 2002.

<a id="8">[8]</a> S. Watanabe, "Asymptotic equivalence of Bayes cross validation and widely applicable information criterion in singular learning theory," *Journal of Machine Learning Research*, vol. 11, pp. 3571-3594, 2010.

<a id="9">[9]</a> A. Vehtari, A. Gelman, and J. Gabry, "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC," *Statistics and Computing*, vol. 27, no. 5, pp. 1413-1432, 2017.

<a id="10">[10]</a> D. Cooley, D. Nychka, and P. Naveau, "Bayesian spatial modeling of extreme precipitation return levels," *Journal of the American Statistical Association*, vol. 102, no. 479, pp. 824-840, 2007.

<a id="11">[11]</a> A. C. Davison, S. A. Padoan, and M. Ribatet, "Statistical modeling of spatial extremes," *Statistical Science*, vol. 27, no. 2, pp. 161-186, 2012.

<a id="12">[12]</a> M. D. Dettinger, D. R. Cayan, H. F. Diaz, and D. M. Meko, "Large-scale atmospheric forcing of recent trends toward early snowmelt runoff in California," *Journal of Climate*, vol. 11, no. 12, pp. 3064-3078, 1998.

---

## Appendix A: Notation Summary

| Symbol | Description |
|--------|-------------|
| $Y_{ij}$ | Observation $i$ at site $j$ |
| $S$ | Number of sites |
| $n_j$ | Number of observations at site $j$ |
| $\xi_j$, $\alpha_j$, $\kappa_j$ | GEV parameters at site $j$ |
| $\beta_k$ | Regression coefficient |
| $X_{kj}$ | Covariate $k$ at site $j$ |
| $\varepsilon_j$ | Spatial error at site $j$ |
| $\sigma$ | Spatial error standard deviation |
| $r$ | Correlation range parameter |
| $\rho(h)$ | Correlation function |
| $h_{ij}$ | Distance between sites $i$ and $j$ |
| $\mathbf{R}$ | Correlation matrix |
| $C(\cdot)$ | Copula function |
| $c(\cdot)$ | Copula density |

## Appendix B: Progressive Test Hierarchy

RMC-BestFit includes a comprehensive suite of synthetic data generators for validation, building from simple to complex:

| Level | Test Case | Description |
|-------|-----------|-------------|
| 1 | `GetBasicIdentifiableData` | 500 total obs, constant parameters |
| 2 | `GetHomogeneousGridData` | 100×100 km grid, constant parameters |
| 3a | `GetCopulaDataBasicExponential` | Add copula with exponential correlation |
| 3b | `GetCopulaDataPoweredExponential` | Copula with powered exponential |
| 3c | `GetCopulaDataSpherical` | Copula with spherical correlation |
| 4a | `GetRegressionLocationOnly` | Location varies with X, Y |
| 4b | `GetRegressionScaleOnly` | Scale varies with X, Y |
| 4c | `GetRegressionLocationScale` | Both vary |
| 4d | `GetRegressionLocationScaleShape` | All parameters vary |
| 5a | `GetSpatialErrorsLocationOnly` | Add GP errors to location |
| 5b | `GetSpatialErrorsLocationScale` | GP errors on location + scale |
| 5c | `GetFullBHMData` | Complete model with all components |
| 6 | `GetCopulaWithRegressionData` | Copula + regression combined |

This hierarchy validates each component independently before combining them, ensuring correct implementation.

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/SpatialExtremes/SpatialGEV.cs`, `src/RMC.BestFit/Models/SpatialExtremes/CopulaModels`, `src/RMC.BestFit/Models/SpatialExtremes/SpatialCorrelation`, `src/RMC.BestFit/Analyses/SpatialExtremes/SpatialGEVAnalysis.cs`, and `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`.

---

[<- Previous: Time Series](../analysis/time-series.md) | [Back to Index](../../index.md) | [Next: Trend and Link Functions ->](../support/trend-and-link-functions.md)

---

*Last updated: 2026-02-01*
*RMC-BestFit v2.0*
