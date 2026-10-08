<!-- technical-reference-status: complete -->

# Spatial GEV and Regional Extreme-Value Analysis

[<- Previous: Time Series](../analysis/time-series.md) | [Back to Index](../index.md) | [Next: Trend and Link Functions ->](../support/trend-and-link-functions.md)

`SpatialGEV` is a hierarchical block-maxima model for annual or otherwise aligned maxima observed at multiple sites. Site-specific Generalized Extreme Value (GEV) parameters are regression functions of site covariates, optionally augmented by spatial Gaussian-process errors. A Gaussian copula can represent dependence among sites within the same observation row. `SpatialGEVAnalysis` estimates the joint parameter vector with Bayesian MCMC and constructs site-level frequency curves.

This is not an index-flood estimator and it does not automatically define a homogeneous region. The analyst supplies the network, alignment, coordinates, covariates, regression structure, correlation family, and probability ordinates. Those choices are part of the scientific model.

## Applicability and Current Availability

Use the model when:

- each row represents the same block or event period at every included site;
- site maxima are adequately described by GEV marginals;
- spatial variation can be represented by the supplied covariates and residual correlation;
- coordinates share a projected linear unit (Cartesian metric) or are latitude/longitude pairs in decimal degrees (geodesic metric); and
- the network is small enough for repeated dense covariance factorizations.

The Bayesian model supports complete or partially observed rows, site-specific posterior parameter summaries, site-specific posterior quantiles, row/year information criteria, observed-subset copula marginalization, explicit data/prior decomposition, Godambe estimating equations, and leave-one-site-out cross-validation. The coordinate metric and the interpretation of optional site weights must be selected explicitly for the application, as described below. Ungauged-site prediction, regional credible bounds, dependent simulation, spatial bootstrap uncertainty, and uncertainty-method dispatch are implemented and covered by focused oracle or recovery tests.

Composite pairwise likelihood is a future enhancement. The current code always uses either independent marginal contributions or one Gaussian-copula contribution per observation row over the sites observed in that row.

## Notation and Units

| Symbol | Meaning |
|---|---|
| $Y_{ij}$ | maximum in row $i=1,\ldots,n$ at site $j=1,\ldots,S$, in the response unit |
| $\mathbf s_j=(s_{j1},s_{j2})$ | site coordinates: projected (X, Y) in a common linear unit (Cartesian metric) or (latitude, longitude) in decimal degrees (geodesic metric) |
| $\mathbf x_j$ | row of standardized or otherwise scaled site covariates |
| $\xi_j,\alpha_j,\kappa_j$ | Numerics GEV location, scale, and shape at site $j$ |
| $\boldsymbol\beta_a$ | regression coefficients for GEV parameter $a$ |
| $\boldsymbol\epsilon_a$ | optional spatial-error vector for parameter $a$ |
| $\sigma_a,r_a,p_a$ | spatial-error standard deviation, range, and optional powered-exponential exponent |
| $R(h)$ | spatial correlation at separation $h$ (planar Euclidean or great-circle kilometres by metric) |
| $\mathbf R_C$ | Gaussian-copula correlation matrix |
| $w_j$ | site weight multiplying the marginal log density |
| $p_E$ | exceedance probability; the nonexceedance probability is $q=1-p_E$ |

Location, scale, quantiles, and location/scale spatial errors have response units unless a log link is used. Regression-coefficient units depend on the link and covariate scaling. Shape, copula probabilities, correlations, and powered-exponential exponents are dimensionless. Range parameters use the projected coordinate unit for Cartesian distance and kilometres for geodesic distance.

## Site-Level GEV Model

RMC.Numerics 2.2.0 uses the shape sign convention documented in the [GEV chapter](../distributions/generalized-extreme-value.md). For

$$
z_{ij}=\frac{y_{ij}-\xi_j}{\alpha_j},
\qquad t_{ij}=1-\kappa_j z_{ij}>0, \tag{1}
$$

the CDF and density for $\kappa_j\ne0$ are

$$
F_j(y_{ij})=\exp\!\left[-t_{ij}^{1/\kappa_j}\right], \tag{2}
$$

$$
f_j(y_{ij})=
\frac{1}{\alpha_j}
t_{ij}^{1/\kappa_j-1}
\exp\!\left[-t_{ij}^{1/\kappa_j}\right]. \tag{3}
$$

The $\kappa_j\to0$ limit is Gumbel:

$$
F_j(y)=\exp\{-\exp[-(y-\xi_j)/\alpha_j]\}, \tag{4}
$$

$$
f_j(y)=\alpha_j^{-1}
\exp\!\left[-\frac{y-\xi_j}{\alpha_j}
-\exp\!\left(-\frac{y-\xi_j}{\alpha_j}\right)\right]. \tag{5}
$$

Thus $\kappa_j<0$ gives an unbounded heavy upper tail, $\kappa_j=0$ an exponential-type upper tail, and $\kappa_j>0$ an upper endpoint $\xi_j+\alpha_j/\kappa_j$. The nonexceedance quantile is

$$
Q_j(q)=
\begin{cases}
\xi_j+\dfrac{\alpha_j}{\kappa_j}
\left[1-\{-\log(q)\}^{\kappa_j}\right], & \kappa_j\ne0,\\[6pt]
\xi_j-\alpha_j\log[-\log(q)], & \kappa_j=0.
\end{cases} \tag{6}
$$

`SpatialGEVAnalysis` evaluates (6) at $q=1-p_E$. A return period $T=1/p_E$ is meaningful only when rows are comparable independent annual trials after accounting for the modeled within-row spatial dependence.

## Covariate Regression and Links

Each GEV parameter has a `GeneralLinearFunction`:

$$
\eta_{a,j}=\beta_{a,0}+\mathbf x_j^\mathsf T\boldsymbol\beta_a,
\qquad a\in\{\xi,\alpha,\kappa\}. \tag{7}
$$

The constructor creates one intercept followed by one coefficient per covariate column. Site $j$ selects row $j$ of that function's stored covariate matrix. All matrices used for a given parameter must therefore contain $S$ rows in the same site order as the data and coordinates.

With optional error terms, the implemented parameter maps are

$$
\xi_j=
\begin{cases}
\exp(\eta_{\xi,j}+\epsilon_{\xi,j}), & \text{log link},\\
\eta_{\xi,j}+\epsilon_{\xi,j}, & \text{identity link},
\end{cases} \tag{8}
$$

$$
\alpha_j=
\begin{cases}
\exp(\eta_{\alpha,j}+\epsilon_{\alpha,j}), & \text{log link},\\
\max(\eta_{\alpha,j}+\epsilon_{\alpha,j},\epsilon_{\rm mach}), & \text{identity link},
\end{cases} \tag{9}
$$

$$
\kappa_j=\eta_{\kappa,j}+\epsilon_{\kappa,j}. \tag{10}
$$

Log links for location and scale are enabled by default. A log location link is appropriate only for a strictly positive response on the modeled scale. The identity-scale path clamps nonpositive predictions rather than rejecting them; the log scale is preferable for smooth inference.

Covariates should be centered and scaled before model construction. This makes the intercept interpretable at a typical site and makes the default slope bounds $[-1,1]$ meaningful. Do not combine raw drainage area in square kilometres, elevation in metres, and precipitation in millimetres under the same default coefficient bounds without intentional rescaling and prior review.

## Spatial Regression Errors

For each enabled parameter field,

$$
\boldsymbol\epsilon_a\mid\sigma_a,\boldsymbol\phi_a
\sim \mathcal N_S\!\left(
\mathbf0,\,
\sigma_a^2\mathbf R_a(\boldsymbol\phi_a)
\right). \tag{11}
$$

The implementation has no nugget term. At separation

$$
h_{jk}=\begin{cases}
\sqrt{(s_{j1}-s_{k1})^2+(s_{j2}-s_{k2})^2}, & \text{Cartesian metric (projected units)},\\[4pt]
2R_\oplus\arcsin\sqrt{\sin^2\tfrac{\Delta\varphi}{2}+\cos\varphi_j\cos\varphi_k\sin^2\tfrac{\Delta\lambda}{2}}, & \text{geodesic metric (km, } R_\oplus=6371.0088\text{ km)},
\end{cases} \tag{12}
$$

the available correlations are

$$
\rho_{\rm exp}(h;r)=\exp(-h/r), \qquad r>0, \tag{13}
$$

$$
\rho_{\rm pexp}(h;r,p)=
\exp[-(h/r)^p], \qquad r>0,\quad 0.1\le p\le2, \tag{14}
$$

and

$$
\rho_{\rm sph}(h;r)=
\begin{cases}
1-\dfrac{3}{2}\dfrac{h}{r}
+\dfrac{1}{2}\left(\dfrac{h}{r}\right)^3, & 0\le h<r,\\
0, & h\ge r.
\end{cases} \tag{15}
$$

`CorrelationFunctionType` names these `Exponential`, `PoweredExponential`, and `Spherical`. Every enabled component has its own correlation parameters: the observation copula, location errors, scale errors, and shape errors do not share a range automatically.

Both Gaussian-process and copula covariance evaluators use `CachedMultivariateNormal`. Setting a covariance matrix invalidates its Cholesky/log-determinant cache; repeated density calls at unchanged parameters reuse that factorization. Updating a range or exponent requires a new dense $S\times S$ factorization, with $O(S^3)$ time and $O(S^2)$ storage. Highly colocated sites and very long fitted ranges can make the matrix nearly singular because no nugget is estimated.

`SpatialDistanceMetric` selects the separation in (12) for the copula, the latent-error covariances, kriging, and the inverse-distance fallback. `Cartesian` (default) is the pinned Numerics `Tools.Distance` planar calculation: use a defensible projected coordinate reference system, pass both columns in the same linear unit, and interpret the range values in that unit. `Geodesic` interprets each row as (latitude, longitude) in decimal degrees (validated to |lat| ≤ 90, |lon| ≤ 180) and returns great-circle kilometres, so the range values are kilometres. The default range prior Uniform($\epsilon_{\rm mach}$, 500) is the same number in both metrics and should be reviewed for the network at hand. Components created by `ConfigureForProperCoverage` adopt the model's metric; components assigned directly must be built with the same metric, which `Validate` checks.

## Gaussian-Copula Observation Dependence

For a complete row, define

$$
u_{ij}=F_j(y_{ij}),\qquad
z_{ij}=\Phi^{-1}(u_{ij}),\qquad
\mathbf z_i=(z_{i1},\ldots,z_{iS})^\mathsf T. \tag{16}
$$

The copula matrix has entries

$$
(\mathbf R_C)_{jk}=
\begin{cases}
1, & j=k,\\
\rho_C(h_{jk};\boldsymbol\phi_C), & j\ne k.
\end{cases} \tag{17}
$$

and density

$$
c_{\mathbf R_C}(\mathbf u_i)
=\frac{\phi_S(\mathbf z_i;\mathbf0,\mathbf R_C)}
{\prod_{j=1}^{S}\phi(z_{ij})}. \tag{18}
$$

The Gaussian copula captures within-row association while retaining the GEV marginal tails. It is asymptotically tail independent unless correlations approach one; a high fitted correlation does not by itself establish joint upper-tail dependence.

The copula requires meaningful row alignment. If site records refer to different water years, event definitions, or aggregation windows, the fitted dependence is not interpretable. Serial dependence between rows is also outside this model.

### Missing observations

Without copula dependence, `double.NaN` values are skipped and the available marginal contributions remain. With copula dependence, a row whose observed-site set is $O_i$ contributes the Gaussian-copula density of the observed coordinates with the correlation submatrix $\mathbf R_{C,O_i}$, that is, (18) restricted to $O_i$; the unobserved coordinates are integrated out exactly because the Gaussian copula family is closed under marginalization (the copula interprets a missing site as missing at random given the observed sites). A row with a single observed site has no dependence term and a fully missing row contributes nothing. The implementation is `GaussianCopula.LogPDF(z, observedSites)`, which caches the Cholesky factorization of each missingness pattern until the correlation parameters change.

## Full Implemented Kernel

With enabled copula dependence, the contribution of row $i$ with observed-site set $O_i$ is

$$
L_i(\Theta)=
c_{\mathbf R_{C,O_i}}(\mathbf u_{i,O_i})
\prod_{j\in O_i} f_j(y_{ij})^{w_j}, \tag{19}
$$

with $c_{\mathbf R_{C,O_i}}\equiv 1$ when $|O_i|<2$. Without the copula, remove $c$. With equal weights, $w_j=1$. The scalar `DataLogLikelihood` is the observation log likelihood

$$
\ell_D(\Theta)=
\sum_{i=1}^{n}\left[
\log c_{\mathbf R_{C,O_i}}(\mathbf u_{i,O_i})
+\sum_{j\in O_i}w_j\log f_j(y_{ij})
\right], \tag{20}
$$

and the prior log density evaluated by `PriorLogLikelihood` is

$$
\ell_P(\Theta)=
\sum_{r=1}^{d_\Theta}\log p_r(\Theta_r)
+\sum_{a\in\mathcal E}
\log\phi_S(\boldsymbol\epsilon_a;\mathbf0,\sigma_a^2\mathbf R_a), \tag{20a}
$$

where $\mathcal E$ is the set of enabled spatial-error fields. The Gaussian-process terms are prior structure on the latent errors (Level 2 of the hierarchy), so they live in `PriorLogLikelihood` and not in `DataLogLikelihood`; the posterior kernel `LogLikelihood` is $\ell_D+\ell_P$.

The flat parameter order is:

1. copula correlation parameters, when enabled;
2. location regression coefficients, intercept first;
3. scale regression coefficients;
4. shape regression coefficients;
5. location-error block, when enabled;
6. scale-error block, when enabled; and
7. shape-error block, when enabled.

Each error block is ordered

$$
(\sigma_a,\ \boldsymbol\phi_a,\ \epsilon_{a,1},\ldots,\epsilon_{a,S}). \tag{21}
$$

### Priors and bounds

`SetDefaultParameters` derives intercept starting values and bounds from sitewise sample means and standard deviations, and sizes the latent-error bounds from three times the spread of those site statistics in the space in which the error acts (log space under a log link, raw units under an identity link; ceiling, floor 1.0). A proposal whose site location, scale, or shape is not finite has negative-infinite likelihood. Shape intercept is initialized at zero with Uniform$(-0.5,0.5)$. `GeneralLinearFunction` assigns each covariate coefficient Uniform$(-1,1)$. Correlation ranges have Uniform$(\epsilon_{\rm mach},500)$ priors, and the powered-exponential exponent has Uniform$(0.1,2)$.

For an error field with data-derived bound $M_a$, the implementation assigns

$$
\sigma_a\sim{\rm Uniform}(\epsilon_{\rm mach},M_a),\qquad
\epsilon_{a,j}\sim{\rm Uniform}(-M_a,M_a), \tag{22}
$$

and also multiplies by the joint Gaussian density in (11), which enters $\ell_P$ in (20a). The Uniform latent-error priors therefore act as truncation constraints in addition to the Gaussian process. The posterior kernel is

$$
\pi(\Theta\mid\mathbf Y)\propto
\exp[\ell_D(\Theta)+\ell_P(\Theta)]. \tag{23}
$$

Because the range bound 500 is fixed rather than derived from the network, coordinate units and extent can place substantial prior mass in an irrelevant region or exclude plausible ranges. Review and, where supported by the API, revise every bound before MCMC.

### Pointwise likelihood

`PointwiseDataLogLikelihood` returns one value per row/year: the row's weighted observed-site marginals plus its observed-subset copula term, so the values sum to `DataLogLikelihood` in (20). `PointwisePriorLogLikelihood` reports each parameter prior and each enabled error process as components that sum to `PriorLogLikelihood` in (20a). WAIC and PSIS-LOO therefore score the row/year predictive unit of the fitted kernel, and the identities `LogLikelihood == DataLogLikelihood + PriorLogLikelihood`, `DataLogLikelihood == sum of pointwise rows`, and `PriorLogLikelihood == sum of pointwise prior components` hold in fast contracts and the `mvtnorm` oracle cells.

## Estimation and Output Construction

`SpatialGEVAnalysis.RunAsync` validates the model, raises a cancellable start event, clears stale results, runs `BayesianAnalysis`, and post-processes only when MCMC reports an estimated result. Cancellation is forwarded to the sampler. The analysis constructs:

- `SpatialGEVSiteResults` for each site, containing posterior means and equal-tailed credible limits for $\xi_j,\alpha_j,\kappa_j$ and $Q_j(1-p_E)$;
- a point curve from the selected posterior mean or MAP parameter vector; and
- an aggregate `AnalysisResults` curve based on arithmetic averages across sites.

Changing probability ordinates or credible-interval width reprocesses saved posterior draws; it does not rerun MCMC. Changing model structure or parameters clears the fit.

### Leave-one-site-out cross-validation

`RunCrossValidationAsync` builds, for every site $j$, the training network without that site (`SpatialGEV.CreateReducedModel`: the data column, coordinate row, covariate rows of every trend, copula coordinate, and latent error of site $j$ are removed; flags, links, the remaining site weights, and every remaining parameter's value, bounds, and prior are copied), validates it, fits it with a fold `BayesianAnalysis` that carries the main analysis's sampler type, defaults policy (resolved against the fold's own parameter count), seed, interval width, output length, and point estimator (and its explicit iteration, chain, thinning, and tuning settings when the defaults are off), and predicts site $j$ from the fold posterior at its coordinates with its own covariate rows. The T = 100 posterior-mean quantile minus the site's at-site maximum-likelihood GEV quantile is the site prediction error; the RMSE spans T = 2, 5, 10, 25, 50, and 100. The analysis model and posterior are never modified, so `CrossValidationResults` survives the run. `FoldStatus`, `FoldMessages`, `SuccessfulFolds`, and `TotalFolds` record folds without observations, with invalid or unfittable reduced models, or with non-finite predictions; such folds hold NaN metrics and are excluded from the aggregates, and a run with no successful fold throws. CRPS is not computed (zero-filled, documented). The per-fold latent-error interpolation uses inverse-distance weighting.

The regional curve reports, for every probability, the posterior summaries of the regional mean quantile computed within each retained draw: the mean curve is its posterior mean (equal to the regional mean of the site posterior means) and the bounds are its equal-tailed posterior quantiles, so cross-site posterior dependence is retained; the mode curve is the regional mean of the point-estimate site curves. The regional growth curve keeps its descriptive site-average definition. Spatial AIC/BIC use the observation log likelihood `SpatialGEV.DataLogLikelihood` in (20) at the stored MAP (parameter priors and latent-error process densities excluded, missing sites marginalized), and BIC treats each nonempty row/year as one multivariate observation block rather than counting site cells (`SpatialGEVAnalysis.ComputeInformationCriteria`), so fully missing rows are excluded and contemporaneously dependent sites are not counted as independent replicates. They remain qualified diagnostics: the MAP is not an MLE when priors are nonconstant, and weighted or dependent spatial likelihoods do not automatically satisfy ordinary AIC/BIC regularity assumptions. WAIC and PSIS-LOO at the row/year unit are the preferred comparison tools.

`SpatialGEVUncertaintyMethod` selects how `RunAsync` builds the interval bounds after the MCMC fit: `BayesianPosterior` keeps the posterior intervals; `BayesianInflated` widens the site and regional intervals by the square root of the variance inflation factor from the empirical intersite correlation (`InflatePosteriorCovariance`); `GodambeSandwich` computes the Godambe covariance at the MAP (`ComputeGodambeCovariance`, both sandwich factors from the row/year estimating equations, explicit failure through `GodambeCovarianceStatus`), draws `OutputLength` seeded Gaussian parameter vectors N(MAP, Σ) truncated to the parameter bounds, and propagates them through the site-result machinery; `SpatialBootstrap` runs the temporal block bootstrap (`RunSpatialBootstrapAsync` with `BootstrapReplicates` and `BootstrapBlockSize`): rows are resampled with replacement in contiguous blocks keeping every site, each replicate (`SpatialGEV.CreateResampledModel`) is refitted by maximum a posteriori estimation warm-started at the full-model MAP, failed replicates are excluded with at least half required, and percentile intervals replace the site and regional bounds while `BootstrapResults` records the accounting. A method that cannot be applied (unavailable covariance, too few replicates) fails the run explicitly; `AppliedUncertaintyMethod` and `SpatialGEVSiteResults.UncertaintyMethod` record the method actually applied. The Godambe and bootstrap paths are frequentist diagnostics of the Bayesian fit.

## Ungauged Prediction

At a new location in the selected coordinate metric $\mathbf s_*$ with matching covariates, the model-level `PredictAtUngauged` computes the regression trend and, for each enabled error field, simple-Gaussian-process conditioning:

$$
E(\epsilon_{a,*}\mid\boldsymbol\epsilon_a)
=\mathbf k_*^\mathsf T\mathbf K_a^{-1}\boldsymbol\epsilon_a, \tag{24}
$$

$$
{\rm Var}(\epsilon_{a,*}\mid\boldsymbol\epsilon_a)
=\sigma_a^2-\mathbf k_*^\mathsf T\mathbf K_a^{-1}\mathbf k_*. \tag{25}
$$

It returns conditional means in the GEV parameter vector and the three conditional error variances. If Cholesky factorization fails after diagonal-jitter attempts, `SpatialRegressionErrors` falls back to inverse-distance weighting.

The analysis-level `PredictAtUngaugedLocation` applies (24)–(25) to every retained posterior draw with that draw's error model and, by default (`SampleConditionalResidual = true`), adds a conditional residual drawn from N(0, (25)) with standard-normal scores generated from the analysis seed before the parallel loop, so the predictive interval carries both parameter uncertainty and the spatial-interpolation uncertainty of the latent errors; with `SampleConditionalResidual = false` it uses the conditional mean only. The model-level predictor is verified against the R conditional-GP oracle. Its single covariate vector applies to every covariate trend (the trends must share the covariate definition) and is required whenever a trend has covariates: `GeneralLinearFunction.PredictWithCovariates` throws for a covariate trend evaluated without covariates. Extrapolation outside the covariate and spatial convex hull should be treated as a scenario, not validated regionalization.

## Site Weights and Pairwise Likelihood

`ComputeCorrelationHeuristicSiteWeights` (formerly `ComputeEffectiveSampleSizeWeights`, kept as an obsolete forwarding alias) calculates preliminary weights

$$
w_j^\star=\frac{1}{1+(S-1)\bar\rho_j},
\qquad
\bar\rho_j=\frac{1}{S-1}\sum_{k\ne j}|\hat\rho_{jk}|, \tag{26}
$$

then rescales them so $\sum_jw_j=S$. They modify only the marginal part of (20); the copula contribution of each row remains unweighted. This is a relative correlation-based down-weighting heuristic, not an effective-sample-size reduction or a pairwise composite likelihood, and no composite-likelihood (Godambe) uncertainty adjustment follows from it; `ConfigureForProperCoverage(useWeightedLikelihood: true)` applies it and its remarks say so.

No method evaluates

$$
\ell_{\rm pair}(\Theta)=
\sum_{i=1}^{n}\sum_{j<k}
\log f_{jk}(y_{ij},y_{ik};\Theta). \tag{27}
$$

Godambe adjustment for (27), Bayesian composite-likelihood calibration, and pair selection are future design work [5]–[7]. Documentation and engineering reports must not imply that these methods are already implemented.

## Compile-Checked Configuration

The example uses ten complete annual-maxima rows at six sites. Flow is in cubic feet per second, coordinates are projected kilometres, and the two illustrative covariates are standardized log drainage area and standardized mean annual precipitation. The location and scale regressions use both covariates; shape is intercept-only to reduce weakly identified tail regression. The snippet configures but does not run MCMC.

<!-- snippet: spatial-gev-workflow -->
```csharp
private static SpatialGEVAnalysis ConfigureSpatialGevAnalysis()
{
    double[,] annualMaximumFlow =
    {
        { 1_240, 1_820, 2_810, 3_640, 4_930, 6_110 },
        { 1_510, 2_090, 3_260, 4_020, 5_410, 6_750 },
        { 1_370, 1_960, 3_040, 3_810, 5_120, 6_430 },
        { 1_860, 2_480, 3_790, 4_690, 6_080, 7_520 },
        { 2_110, 2_730, 4_120, 5_060, 6_540, 8_010 },
        { 1_740, 2_310, 3_510, 4_390, 5_770, 7_160 },
        { 2_430, 3_090, 4_480, 5_510, 7_030, 8_640 },
        { 2_080, 2_690, 4_060, 4_970, 6_420, 7_910 },
        { 2_760, 3_410, 4_910, 6_020, 7_590, 9_180 },
        { 2_350, 2_980, 4_370, 5_360, 6_880, 8_430 }
    };
    double[,] projectedCoordinatesKm =
    {
        { 12.0, 18.0 },
        { 29.0, 24.0 },
        { 46.0, 31.0 },
        { 61.0, 43.0 },
        { 79.0, 57.0 },
        { 96.0, 66.0 }
    };
    double[,] standardizedSiteCovariates =
    {
        { -1.31, -1.18 },
        { -0.78, -0.61 },
        { -0.24, -0.16 },
        {  0.29,  0.22 },
        {  0.82,  0.71 },
        {  1.22,  1.02 }
    };

    var location = new GeneralLinearFunction(
        "GEV location",
        standardizedSiteCovariates);
    var scale = new GeneralLinearFunction(
        "GEV scale",
        standardizedSiteCovariates);
    var shape = new GeneralLinearFunction("GEV shape");

    var model = new SpatialGEV(
        annualMaximumFlow,
        projectedCoordinatesKm,
        location,
        scale,
        shape);
    model.ConfigureForProperCoverage(
        CorrelationFunctionType.PoweredExponential,
        includeScaleErrors: false,
        includeShapeErrors: false,
        useWeightedLikelihood: false);

    return new SpatialGEVAnalysis(model);
}
```

Before estimation, inspect every `model.Parameters` entry, set scientifically justified bounds/priors, configure the MCMC seed and chain length through `BayesianAnalysis`, and retain convergence evidence. `ConfigureForProperCoverage` enables the full Gaussian copula and location spatial errors; the method name is not a coverage guarantee. For a new study, begin with simpler nested structures and add dependence components only when the network and record support them.

## Assumptions, Identifiability, and Failure Modes

- **Block definition.** Rows are comparable maxima from aligned blocks; asynchronous event pairing is not repaired by the model.
- **Marginal adequacy.** Every site follows the Numerics-sign GEV with parameter surfaces in (8)–(10). Physical upper bounds implied by $\kappa>0$ must be checked.
- **Conditional structure.** The copula describes within-row dependence; Gaussian-process errors describe persistent spatial deviations of parameter surfaces. With few sites, the two layers and their ranges can be weakly separated.
- **No nugget.** Colocated or near-colocated sites and long ranges can produce ill-conditioned covariance matrices.
- **Covariate design.** Collinearity, incompatible scaling, or more regression terms than the network can support produces weak identification and prior sensitivity.
- **Shape complexity.** Site-specific shape errors add $S$ latent tail parameters plus covariance hyperparameters. Rare-quantile inference can become prior-dominated.
- **Stationarity.** There is no time trend in the spatial model. Changes in climate, regulation, land use, or measurement practice violate a stationary block-maxima interpretation unless encoded outside this class.
- **Missingness.** Rows with missing sites are marginalized exactly under the Gaussian copula, which treats a missing site as missing at random given the observed sites; informative missingness is outside the model.
- **Extrapolation.** Predictions outside observed coordinate or covariate support are not validated by an in-sample fit.
- **Simulation.** With copula dependence enabled, `GenerateRandomValues` simulates rows through the Cholesky factor of the fitted correlation matrix (sample $i$ of every site is one event; values grouped by site); without it the sites are independent.
- **Computational scaling.** Full covariance factorization is cubic in site count for each changed spatial-parameter vector, plus one factorization per distinct missingness pattern.

For life-safety applications, report posterior sensitivity to correlation family, covariate set, shape structure, priors, influential years, network definition, and coordinate system. Do not publish a regional return level without stating whether it is a site value, arithmetic site average, normalized growth factor, simultaneous-event quantity, or an areal aggregate.

## Validation and Traceability

Implementation symbols:

- `Models/SpatialExtremes/SpatialGEV.cs`;
- `Models/SpatialExtremes/CopulaModels/GaussianCopula.cs`;
- `Models/SpatialExtremes/CopulaModels/SpatialRegressionErrors.cs`;
- `Models/SpatialExtremes/CopulaModels/CachedMultivariateNormal.cs`;
- `Models/SpatialExtremes/SpatialCorrelation/*.cs`;
- `Models/TrendFunctions/GeneralLinearFunction.cs`; and
- `Analyses/SpatialExtremes/SpatialGEVAnalysis.cs` plus its result DTOs.

The formulas and parameter bounds in this chapter were checked against the current Numerics source checkpoint and the current BestFit source. Fast unit tests cover correlation values and validation, cached multivariate-normal behavior, Gaussian-copula density behavior including the observed-subset evaluation, spatial-error parameter round trips and prediction helpers, `SpatialGEV` construction/likelihood components and the data/prior identities, the row/year criteria helper, the Godambe status contract, result DTOs, serialization, and analysis lifecycle. Focused numerical tests verify the likelihood against the R `mvtnorm` observed-subset and location-error oracle (eight exact cells), the criteria against a guarded MCMC run, and the current retained spatial recovery evidence. The current independent completeness matrix pins all three correlation laws, a fitted held-out Gaussian-copula fold, held-out covariate regression, draw-specific conditional-GP prediction, fixed-draw regional aggregation, both Godambe factors and their sandwich covariance, temporal whole-row block resampling, and the analytical VIF transformation. Historical production-versus-production fold, prediction, and uncertainty-dispatch cells are retained only as design history; the corresponding state and dispatch behavior remains fast-test owned. The geodesic metric is checked against an R haversine oracle and the Cartesian default against planar distances. See [Spatial-Extremes Analysis](../../verification/report/spatial-extremes.md#likelihood-oracle).

### Current independent correlation and fold matrix

Independent numerical targets distinguish correlation laws, fitted folds, and held-out prediction. Basic Exponential, Powered Exponential, and Spherical correlation functions are pinned on a common
distance grid that includes zero, the Spherical range boundary, and a beyond-range point. Powered Exponential
uses smoothness 1.6 so it is not merely the exponential special case. A fixed Cartesian held-out fold freezes
the independently fitted SciPy optimum of a three-site marginal-GEV plus Gaussian-copula likelihood, then
compares production Differential Evolution and the held-out quantiles with unregularized observed-information
uncertainty. A separate two-covariate fold recomputes OLS coefficients, the held-out log-link mean, its physical location,
and parameter and observation prediction variances by normal equations. Missing-site scoring and fold accounting remain fast contracts. The historical
production-versus-production cross-validation cells are no longer current Verification declarations.

### Current independent prediction and uncertainty matrix

The fixed-draw conditional-GP cell uses a geodesic coordinate matrix and independently evaluates
the conditional mean, conditional variance, and prediction for four parameter/error draws at one ungauged target.
The regional cell independently transforms nine supplied link-space intercept/slope and log-scale draws, with physical shape, at three
predeclared ordinates, averages each draw across sites, and only then computes the posterior mean
and equal-tailed bounds. These cells isolate the prediction and aggregation formulas from MCMC and
from production result aggregation.

The Godambe generator independently differentiates the row/year objective to obtain `H` and row-score `J`;
the executable test reconstructs the frozen `H^-1 J H^-1` target and compares the production sandwich.
The bootstrap cell uses MT19937 seed 24681357 to draw five wrapping temporal block replicates from twelve
complete row/year vectors with block size four; each selected row keeps its entire site vector. Bounded SciPy
flat-prior MAP fits (optimizer seed 20260837 plus replicate) independently produce the physical-parameter,
site-quantile, and regional-quantile interval targets checked against the five production default-DE refits.
The variance-inflation cell applies the exact centre-plus-`sqrt(VIF)` analytical transformation to results
derived from a fixed 10-by-3 observation matrix rather
than accepting interval widening alone. Parameter uncertainty, conditional-GP residual uncertainty,
bootstrap resampling uncertainty, and numerical tolerances are therefore recorded as distinct
quantities. Generator/runtime versions, seeds, inputs, parameter order, tolerances, and SHA-256 hashes
are frozen in the verification manifest and the associated independent artifact. The `1e-9` absolute tolerance covers
cross-runtime arithmetic roundoff, `2e-5` relative covers independent Godambe finite-difference cancellation,
and the bootstrap's 2% relative/0.02 near-zero tolerance is below every frozen fitted-output interval width.

### Current N=1000 recovery matrix

The retained recovery sample size $N$ is the site-by-time cross-product: ten sites with 100 observations each,
for total scalar N=1,000. Each retained fixture contains 100 complete ten-site row/year vectors, so the
likelihood contribution count is 100 multivariate rows. Posterior draws, warmup iterations, and numerical
quadrature points are not counted. All recovery coordinates use the Cartesian distance metric.
The ten-site independent grid is the partial 25 km grid `(0,0)`, `(25,0)`, `(50,0)`, `(75,0)`,
`(0,25)`, `(25,25)`, `(50,25)`, `(75,25)`, `(0,50)`, `(25,50)`. Copula and regression fixtures use
the same ten-site partial-grid pattern at spacing `100/3`.

The homogeneous flat order is `[log(location), log(scale), shape]`. Copula models prepend physical range,
and the location-regression order is `[beta0, betaX, betaY, log(scale), shape]` with X and Y taken from the
coordinate matrix. An independent row contributes one marginal GEV density per site; a dependent row also
contributes one full-network Gaussian-copula density. All fitted coordinates in the retained designs are
monitored as identified scalar parameters. No retained model contains a latent spatial-error field, so no weak
latent coordinate is substituted by a site-quantile or regional-curve interval and no conditional-GP residual
uncertainty is mixed with parameter uncertainty.

The two MLE cells use unchanged default Differential Evolution and require an unregularized
observed-information covariance. Each parent coordinate must have absolute standardized error no greater than
1.96; singular or regularized information is explicit failure. The six Bayesian cells retain DEMCzs and seed
12345. Three-, four-, and five-coordinate models use 6/8/10 chains, thinning 30/40/50, and initial populations
300/400/500; all use 3,500 iterations, 1,750 warmup iterations, and output length 10,000. The tests assert these
dimension-dependent settings at the default 90% interval width before changing only the reported interval to
95%. Every parent must lie in that central 95% interval, every coordinate must have R-hat below 1.10
and ESS at least 100, and the secondary 5% point rule applies only to an already resolved interval. The
production range support Uniform `(epsilon, 500)`, shape bounds `[-0.5, 0.5]`, data-derived trend bounds,
priors, initial values, and sampler defaults are unchanged and contain every generating parent.

The eight retained distinctions are independent homogeneous MLE and Bayesian baselines, exponential-copula
MLE and Bayesian fits, one X/Y location-regression Bayesian fit, and positive, zero, and strongly negative
shape regimes. These comparisons support recovery for the stated ten-site by 100-row design. They do not
test inverse-sample-size precision scaling merely by comparing interval widths. Recovery is supported only for these network dimensions,
parents, seeds, supports, and model structures; it does not establish latent-error recovery, large-network
performance, conditional spatial prediction, or repeated-realization coverage.

## References

<a id="ref-1"></a>[1] S. Coles, *An Introduction to Statistical Modeling of Extreme Values*. London, U.K.: Springer, 2001.

<a id="ref-2"></a>[2] B. Renard, “A Bayesian hierarchical approach to regional frequency analysis,” *Water Resources Research*, vol. 47, W11513, 2011, doi: 10.1029/2010WR010089.

<a id="ref-3"></a>[3] B. Renard and M. Lang, “Use of a Gaussian copula for multivariate extreme value analysis: Some case studies in hydrology,” *Advances in Water Resources*, vol. 30, no. 4, pp. 897–912, 2007, doi: 10.1016/j.advwatres.2006.08.001.

<a id="ref-4"></a>[4] D. Cooley, D. Nychka, and P. Naveau, “Bayesian spatial modeling of extreme precipitation return levels,” *Journal of the American Statistical Association*, vol. 102, no. 479, pp. 824–840, 2007, doi: 10.1198/016214506000000780.

<a id="ref-5"></a>[5] S. A. Padoan, M. Ribatet, and S. A. Sisson, “Likelihood-based inference for max-stable processes,” *Journal of the American Statistical Association*, vol. 105, no. 489, pp. 263–277, 2010, doi: 10.1198/jasa.2009.tm08577.

<a id="ref-6"></a>[6] C. Varin, N. Reid, and D. Firth, “An overview of composite likelihood methods,” *Statistica Sinica*, vol. 21, no. 1, pp. 5–42, 2011.

<a id="ref-7"></a>[7] M. Ribatet, D. Cooley, and A. C. Davison, “Bayesian inference from composite likelihoods, with an application to spatial extremes,” *Statistica Sinica*, vol. 22, no. 2, pp. 813–845, 2012.

---

[<- Previous: Time Series](../analysis/time-series.md) | [Back to Index](../index.md) | [Next: Trend and Link Functions ->](../support/trend-and-link-functions.md)
