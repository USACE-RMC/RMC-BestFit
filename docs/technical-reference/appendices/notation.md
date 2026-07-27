<!-- technical-reference-status: complete -->

# Global Notation and Conventions

[Technical reference index](../index.md) | [Parameterization crosswalk](parameterization-crosswalk.md) | [Documentation contract](../documentation-contract.md)

## Typographic conventions

Scalars are italic lower-case letters, vectors are bold lower-case letters, and matrices are bold upper-case letters. Random variables are upper-case where the distinction is material; realized observations are lower-case. A hat denotes an estimate, a tilde a transformed or approximated quantity, and a superscript in parentheses denotes an indexed realization rather than a power when stated.

All logarithms are natural logarithms unless `log10`, $\log_{10}$, or a distribution chapter explicitly says otherwise. Products and sums over empty sets follow the usual conventions 1 and 0. `double.NegativeInfinity` represents log density $\log 0$.

## Core symbols

| Symbol | Definition |
|---|---|
| $y_i$ | Observed or latent hydrologic magnitude for logical record $i$ |
| $t_i$ | Integer time/index attached to record $i$ |
| $x_{ij}$ | Covariate $j$ for record or site $i$ |
| $n$ | Number of observations or time points, defined locally |
| $m$ | Number of logical pointwise likelihood units, defined locally |
| $p$ | Number of fitted parameters; also a probability when locally qualified |
| $\theta$ | Natural model/distribution parameter vector |
| $\beta$ | Trend, regression, or link-space coefficient vector |
| $\Theta$ | Admissible parameter space |
| $f(y\mid\theta)$ | PDF or PMF of $Y$ conditional on $\theta$ |
| $F(y\mid\theta)$ | CDF $\Pr(Y\le y\mid\theta)$ |
| $S(y\mid\theta)$ | Survival function $1-F(y\mid\theta)$ |
| $F^{-1}(p\mid\theta)$ | Left-continuous quantile $\inf\{y:F(y\mid\theta)\ge p\}$ |
| $\alpha$ | Exceedance probability unless a family chapter defines it as a parameter |
| $q_\alpha$ | AEP quantile $F^{-1}(1-\alpha)$ |
| $T=1/\alpha$ | Return period under the stated stationary/event-rate assumptions |
| $L(y\mid\theta)$ | Data likelihood |
| $\ell_D(\theta;y)$ | Data log-likelihood |
| $\pi(\theta)$ | Prior density |
| $\ell_P(\theta)$ | Log-prior |
| $p(\theta\mid y)$ | Posterior density |
| $h(x)$, $h^{-1}(\eta)$ | Link and inverse link |
| $J$, $D$ | Jacobian matrix or determinant, defined locally |

## Estimating-Equation and GMM Symbols

| Symbol | Definition |
|---|---|
| $\mathbf g_i(\theta)$ | pointwise estimating-function vector for logical record $i$ |
| $\overline{\mathbf g}_n(\theta)$ | sample mean of estimating functions |
| $\mathbf S(\theta)$ | covariance of pointwise estimating functions |
| $\mathbf W$ | GMM weighting matrix, ordinarily $\mathbf S^{-1}$ after updating |
| $\mathbf D$ | Jacobian $\partial\overline{\mathbf g}_n/\partial\theta^\mathsf T$ |
| $\mathbf H$ | Hessian of an external-information penalty |
| $\mathbf B$ | penalized GMM bread matrix $\mathbf D^\mathsf T\mathbf W\mathbf D+\mathbf H$ |
| WEDS | weighted error-direction score used by Bulletin 17C linked-MVN heuristics |

## Estimation and Diagnostic Symbols

| Symbol | Definition |
|---|---|
| $\widehat{\theta}_{\mathrm{MLE}}$ | maximizer of data log likelihood over the implemented bounded parameter space |
| $\widehat{\theta}_{\mathrm{MAP}}$ | maximizer of the implemented data-plus-prior log target |
| $Q(\theta)$ | GMM objective under the weighting and penalty convention stated locally |
| $\widehat R$ | implemented unsplit between/within-chain potential scale reduction factor |
| $N_{\mathrm{eff}}$ | implemented autocorrelation effective sample-size estimate |
| $\mathrm{lppd}$ | log pointwise posterior predictive density |
| $p_{\mathrm{WAIC}}$ | sum of posterior variances of pointwise log likelihoods |
| $\mathrm{WAIC}$ | $-2(\mathrm{lppd}-p_{\mathrm{WAIC}})$ |
| $\mathrm{elpd}_{\mathrm{loo}}$ | expected log predictive density under leave-one-out prediction |
| $k$ | generalized-Pareto tail shape used by PSIS; compare it with the draw-count reliability limit |
| $T(y)$ | discrepancy statistic in a predictive check |
| $p_B$ | one-sided predictive tail proportion $\Pr\{T(\widetilde y)\ge T(y)\}$ |

## Time-Series and Rating-Curve Symbols

| Symbol | Definition |
|---|---|
| $B$ | backshift operator, $By_t=y_{t-1}$ |
| $\Delta^d$ | $d$-fold difference operator $(1-B)^d$ |
| $g(y_t)$ | configured response transform |
| $p,d,q$ | AR order, integration/difference order, and MA order |
| $b$ | maximum exogenous-covariate lag in ARIMAX |
| $\phi_j$ | autoregressive coefficient at lag $j$ |
| $\theta_j$ | moving-average coefficient at lag $j$ |
| $\varepsilon_t$ | Gaussian innovation on the fitted model scale |
| $\sigma$ | innovation SD; log10-discharge residual SD in the rating curve |
| $h_k$ | rating-curve zero-flow/activation stage for control $k$ |
| $a_k$ | fitted $\log_{10}$ rating coefficient; $\alpha_k=10^{a_k}$ |
| $\beta_k$ | rating-curve power exponent for control $k$ |
| $q(h;\theta)$ | median discharge predicted at stage $h$ |

## Bivariate and Spatial Symbols

| Symbol | Definition |
|---|---|
| \(U=F_X(X),V=F_Y(Y)\) | probability-integral transforms of bivariate marginals |
| \(C_\psi(u,v)\), \(c_\psi(u,v)\) | copula CDF and density with dependence parameters \(\psi\) |
| \(\lambda_L,\lambda_U\) | lower- and upper-tail dependence coefficients |
| \(S\) | number of sites in a spatial analysis |
| \(\mathbf s_j\) | projected two-dimensional coordinate of site \(j\) |
| \(\mathbf x_j\) | site-covariate vector in the order supplied to `GeneralLinearFunction` |
| \(\xi_j,\alpha_j,\kappa_j\) | Numerics GEV location, scale, and shape at site \(j\) |
| \(\boldsymbol\epsilon_a\) | latent spatial regression-error vector for parameter field \(a\) |
| \(\sigma_a,r_a,p_a\) | spatial-error SD, correlation range, and powered-exponential exponent |
| \(\mathbf R_C\) | Gaussian-copula correlation matrix for within-row site dependence |
| \(w_j\) | multiplier on site \(j\)'s marginal spatial log density |
| \(h_{jk}\) | implemented Euclidean separation between sites \(j\) and \(k\) |

Copula dependence describes association among observations within the same row. Spatial regression errors describe persistent spatial departures of GEV parameter surfaces. These two structures are not interchangeable.

## Probability convention

Frequency-analysis chapters use annual exceedance probability (AEP) $\alpha$ and nonexceedance probability $p=1-\alpha$. Thus

$$
q_\alpha=F^{-1}(1-\alpha).
\tag{1}
$$

Under a stationary annual-maximum model with one independent maximum per year, the conventional return period is $T=1/\alpha$. It is an average recurrence interval, not a schedule. The probability of at least one exceedance in $N$ independent years is

$$
1-(1-\alpha)^N.
\tag{2}
$$

Peaks-over-threshold and nonstationary chapters state their event-rate/time-varying conventions separately; Equation (2) must not be transferred without checking those assumptions.

## Units policy

RMC.BestFit stores numeric values without a runtime unit type. Every analysis must define units in its input provenance and output report.

- Location and quantiles have the same units as the modeled magnitude.
- Scale parameters generally have magnitude units, subject to the exact Numerics family parameterization.
- Shape, skewness, correlations, copula parameters, probabilities, and standardized diagnostics are generally dimensionless.
- A linear time slope has parameter units per index unit; polynomial coefficients have parameter units per index-unit power.
- A regression coefficient has response-parameter units divided by its covariate units.
- Logarithms of dimensional quantities require a stated reference scale in a rigorous physical interpretation. Software commonly applies them numerically; reports should state the input units so a change of units is not mistaken for the same parameterization.

No implicit conversion occurs between cubic feet per second and cubic metres per second, feet and metres, calendar and water years, or daily and annual rates.

## Index and ordering conventions

An API `double[] parameters` uses implementation order, not alphabetical order. The authoritative order is the corresponding model's `Parameters` collection and the family-specific crosswalk. C# indexes are zero-based; equations use one-based mathematical subscripts unless stated.

`DataFrame` threshold start and end indexes are inclusive. The generic `ExactSeries(IList<double>)` constructor assigns integer indexes beginning at zero. Nonstationary hydrologic work should use explicit time indexes and a documented origin.

## Numerical conventions

- Impossible parameter/data combinations return negative infinity in log space.
- Probabilities, scales, denominators, exponents, and integrations may use documented finite floors or clamps. Each model chapter identifies its exact safeguards.
- Equality at a continuous censoring boundary has probability zero, so inclusive/exclusive endpoint notation does not alter a continuous likelihood. It does matter for discrete models.
- A finite calculation is not by itself evidence of identifiability, convergence, adequate fit, or safe extrapolation.
- Deterministic examples state inputs and dependency versions. Stochastic examples state seed, chain configuration, diagnostic criteria, and tolerances.

## Abbreviations

| Abbreviation | Meaning |
|---|---|
| AEP | Annual exceedance probability |
| AR, MA, ARIMA, ARIMAX | Autoregressive; moving average; integrated ARMA; ARIMA with exogenous covariates |
| B17C | Bulletin 17C |
| CDF, PDF, PMF | Cumulative distribution, probability density, probability mass function |
| DEMCz, DEMCzs | Differential-evolution Markov chain variants implemented in RMC.Numerics |
| DIC | Deviance information criterion |
| EMA | Expected Moments Algorithm |
| ESS | Effective sample size |
| GEV, GPD | Generalized extreme-value, generalized Pareto distribution |
| GMM | Generalized method of moments |
| HMC | Hamiltonian Monte Carlo; present in Numerics but not exposed by BestFit's sampler enum |
| LOO, LOOIC | Leave-one-out cross-validation and its information criterion |
| MAP, MLE | Maximum a posteriori, maximum likelihood estimate |
| MCMC | Markov chain Monte Carlo |
| MGBT | Multiple Grubbs-Beck Test |
| NUTS | No-U-Turn Sampler; BestFit's gradient-based sampler option |
| POT | Peaks over threshold |
| PSIS | Pareto-smoothed importance sampling |
| R-hat | Between/within-chain convergence diagnostic |
| WAIC | Widely applicable information criterion |
| WEDS | Weighted error-direction score |

## Source and maintenance rule

Local chapters may reuse a symbol with a clearly stated definition, but must not silently reverse the AEP/nonexceedance, GEV/GPD shape-sign, logarithm-base, or parameter-order conventions. When an implementation uses a legacy name, the chapter gives both the API name and mathematical meaning. The [parameterization crosswalk](parameterization-crosswalk.md) is the controlling index for those translations.

[Technical reference index](../index.md) | [Parameterization crosswalk](parameterization-crosswalk.md) | [Documentation contract](../documentation-contract.md)
