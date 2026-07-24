<!-- technical-reference-status: complete -->

# Parameterization Crosswalk

[Global notation](notation.md) · [Technical reference](../index.md) · [API traceability](../api-traceability.md)

This crosswalk prevents natural space, log/link space, Numerics parameters, trend coefficients, and displayed results from being conflated.

## Universal Mappings

| Context | Vector supplied to likelihood | Mapping |
|---|---|---|
| Stationary `UnivariateDistribution` | Numerics `Distribution.GetParameters` order | One `ConstantTrend` coefficient per family parameter |
| Nonstationary `UnivariateDistribution` | Concatenated trend coefficients | Trend $k$ predicts Numerics family parameter $k$ at each index |
| Parameter priors | Same fitted coefficient vector | One stored prior per `ModelParameter`; extra scale/quantile terms are separate |
| Quantile prior | Derived, not an extra fitted coordinate | $q_a(\theta)=F^{-1}(1-a\mid\theta)$ plus implemented multi-quantile Jacobian |
| Link | Natural $x$ to working $\eta=h(x)$ | `DLink(x)=d\eta/dx`; probability Jacobian is caller-specific |
| Result | Estimator or posterior coefficient vector | Quantiles and curves are post-processing; no silent reorder |

## Univariate Families

| Family / class | Public order | Units and convention | Support/tail sign | Status |
|---|---|---|---|---|
| `Exponential` | $(\xi,\alpha)$ | Location and scale; $\alpha>0$ | $x\ge\xi$ | Complete |
| `GammaDistribution` | $(\theta,k)$ | Scale (data units), then dimensionless shape | $x\ge0$ | Complete |
| `GeneralizedExtremeValue` | $(\xi,\alpha,\kappa)$ | Numerics $\kappa=-\xi_{\text{Coles}}$ | $\kappa<0$ heavy upper; $\kappa>0$ bounded upper | Complete |
| `GeneralizedLogistic` | $(\xi,\alpha,\kappa)$ | Hosking transform; same Numerics sign as GEV | $\kappa<0$ heavy upper; $\kappa>0$ bounded upper | Complete |
| `GeneralizedNormal` | $(\xi,\alpha,\kappa)$ | Hosking GNO; not exponential-power | $\kappa<0$ unbounded upper; $\kappa>0$ bounded upper | Complete |
| `GeneralizedPareto` | $(\xi,\alpha,\kappa)$ | Numerics $\kappa=-\xi_{\text{common}}$ | $\kappa<0$ heavy upper; $\kappa>0$ bounded upper | Complete |
| `Gumbel` | $(\xi,\alpha)$ | Location and scale | All real; exponential upper tail | Complete |
| `KappaFour` | $(\xi,\alpha,\kappa,h)$ | Second shape is `Hondo` | Shape-dependent | Blocked by TR-001 |
| `LnNormal` | $(m,s)$ | Arithmetic mean and SD of $X$ | $x>0$; internally converts to natural-log parameters | Complete |
| `Logistic` | $(\xi,\alpha)$ | Location and scale | All real; symmetric exponential tails | Complete |
| `LogNormal` | $(\mu_Y,\sigma_Y)$ | Mean/SD of $Y=\log_{10}X$ | $x>0$ | Complete |
| `LogPearsonTypeIII` | $(\mu_Y,\sigma_Y,\gamma_Y)$ | Mean/SD/skew of $Y=\log_{10}X$ | Shape-dependent endpoint after exponentiation | Complete |
| `Normal` | $(\mu,\sigma)$ | Arithmetic mean and SD | All real | Complete |
| `PearsonTypeIII` | $(\mu,\sigma,\gamma)$ | Arithmetic mean/SD/skew | Positive skew finite lower; negative skew finite upper | Complete |
| `Weibull` | $(\lambda,k)$ | Scale before shape; zero lower endpoint | $x\ge0$ | Complete |

## Important Derived Mappings

For `LnNormal`, public natural-space moments map to internal log parameters by

$$\sigma_{\ln}^2=\log[1+(s/m)^2],\qquad \mu_{\ln}=\log m-\sigma_{\ln}^2/2.\tag{XW.1}$$

For Pearson III and log-Pearson III, moment parameters map to shifted-Gamma coordinates by

$$a=4/\gamma^2,\qquad\beta=\sigma\gamma/2,\qquad\xi=\mu-2\sigma/\gamma.\tag{XW.2}$$

For the GEV, GLO, GNO, and GPD shape transform,

$$y=-\kappa^{-1}\log[1-\kappa(x-\xi)/\alpha],\tag{XW.3}$$

with the continuous $\kappa=0$ branch. For GEV and GPD, the commonly published extreme-value shape is $-\kappa$.

## Advanced Univariate and Bulletin 17C Mappings

| Model | Fitted vector and derived quantities | Status |
|---|---|---|
| Point process, one season | $(\mu,\sigma,\kappa)$ with Numerics $\kappa=-\xi_{\mathrm{Coles}}$; threshold and exposure are model properties, not fitted coordinates | Complete |
| Point process, two seasons | $(k_1,k_2,\mu_1,\sigma_1,\kappa_1,\mu_2,\sigma_2,\kappa_2)$; $k_1,k_2$ are day-of-year change points | Complete |
| Mixture, $K$ components | $K$ raw weights followed by each component's Numerics parameter block; implementation normalizes all $K$ weights, creating the TR-006 redundancy | Complete with finding |
| Competing risks | Concatenated component parameter blocks; minimum/maximum and dependence are model properties | Complete |
| Composite analysis | No independent fitted vector; combines already fitted child parameter realizations and derived criterion weights | Complete |
| Bulletin 17C | Parent natural parameters: two for Exponential/Gamma/Normal/Log-Normal and $(\mu,\sigma,\gamma)$ for P3/LP3; LP3 and Log-Normal moments are in base-10 log space | Complete |
| Bulletin 17C optimizer | `LinkController.ForLocationScaleShape()` coefficients are inverse-linked before moments and penalties; linked-MVN uncertainty installs separate distribution-aware temporary links | Complete |
| Bulletin 17C penalties | Derived objective terms, not fitted coordinates: parameter target/MSE or $F^{-1}(1-\alpha)$ target/MSE, optionally in log space | Complete |

## Estimation and Diagnostic Mappings

| API | Coordinates and reported objective | Status |
|---|---|---|
| `MaximumLikelihood` | Natural model coordinates within `ModelParameter` bounds; optimizer minimizes negative data log likelihood; `MaximumLogLikelihood` reverses the stored fitness sign | Complete |
| `MaximumAPosteriori` | Same coordinates; optimizer minimizes the negative full `IModel.LogLikelihood` target; `MaximumLogLikelihood` is therefore a posterior-kernel value | Complete with TR-023 |
| `GeneralizedMethodOfMoments` | Natural `IGMMModel` coordinates; moment vector length $q$, parameter-vector length $p$, weighting matrix $q\times q$ | Complete with TR-026, TR-033, TR-034 |
| `BayesianAnalysis` | Bounded natural coordinates targeting `IModel.LogLikelihood`; options are DEMCz, DEMCzs, ARWMH, and NUTS | Complete with TR-024, TR-025, TR-029, TR-030 |
| Posterior results | Marginal summaries use `MCMCResults.Output`; `MAP` is the highest-target sampled output state, not a continuous optimizer | Complete |
| DIC/WAIC/LOO | Pointwise units are `PointwiseDataLogLikelihood` components; PSIS-LOO is scientifically unavailable under TR-024 | Complete with finding |

## Time-Series and Rating-Curve Mappings

| Model | Public parameter order and scale | Status |
|---|---|---|
| `AutoRegressive` | optional transformed-scale mean $\mu$; $\phi_1,\ldots,\phi_p$; transformed-scale $\sigma$ | Complete with findings |
| `MovingAverage` | optional transformed-scale mean $\mu$; $\theta_1,\ldots,\theta_q$; transformed-scale $\sigma$ | Complete with findings |
| `ARIMA` | optional mean/drift $\mu$ of $\Delta^d g(y)$; AR block; MA block; innovation $\sigma$ | Complete; $d>0$ prediction unavailable under TR-037/TR-038 |
| `ARIMAX` | optional $\mu$; polynomial trend; sine/cosine; covariate-by-lag blocks; AR; MA; $\sigma$ | Complete; affected configurations restricted by TR-037/TR-039/TR-041 |
| Transform | raw $y$ to $g(y)$ before differencing; $\lambda$ is plug-in preprocessing, not a fitted `ModelParameter` | Complete with TR-036/TR-046 |
| Rating curve | repeating $(h_k,a_k,\beta_k)$ blocks then $\sigma$, where $a_k=\log_{10}\alpha_k$ and $\sigma$ is log10-discharge SD | Complete with TR-043/TR-044 |

## Bivariate and Spatial Mappings

| Model | Public parameter order and scale | Status |
|---|---|---|
| `BivariateDistribution` | Copula parameter block only; the two marginal `UnivariateDistribution` models are fixed upstream and are not appended to the fitted vector | Complete with TR-047 |
| Bivariate pseudo-likelihood | Copula receives matched nonexceedance pseudo-observations \((\widetilde u_i,\widetilde v_i)\) | Complete |
| Bivariate IFM | Copula receives \(F_X(x_i;\widehat\eta_X),F_Y(y_i;\widehat\eta_Y)\); marginal estimates remain fixed | Complete |
| `SpatialGEV` | copula correlation block; location regression; scale regression; shape regression; enabled location/scale/shape error blocks | Complete with TR-048 through TR-062 |
| Spatial regression | each `GeneralLinearFunction` is \((\beta_0,\beta_1,\ldots,\beta_K)\), with stored covariate row \(j\) selecting site \(j\) | Complete |
| Spatial error block | \((\sigma_a,\boldsymbol\phi_a,\epsilon_{a,1},\ldots,\epsilon_{a,S})\); \(\boldsymbol\phi_a=(r_a)\) or \((r_a,p_a)\) | Complete |
| Spatial GEV links | default \(\xi_j=\exp(\eta_{\xi,j}+\epsilon_{\xi,j})\), \(\alpha_j=\exp(\eta_{\alpha,j}+\epsilon_{\alpha,j})\), and \(\kappa_j=\eta_{\kappa,j}+\epsilon_{\kappa,j}\) | Complete |
| Spatial coordinates | Euclidean projected coordinates; correlation range has the same linear unit | Complete with TR-060 |

---

[Global notation](notation.md) · [Technical reference](../index.md)
