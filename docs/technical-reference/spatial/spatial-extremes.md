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
- coordinates share a projected linear unit; and
- the network is small enough for repeated dense covariance factorizations.

The Bayesian model with complete or partially observed rows, site-specific posterior parameter summaries, site-specific posterior quantiles, and the row/year information criteria are implemented; the missing-site copula marginalization, the data/prior decomposition, the row/year criteria, and the Godambe estimating equations were corrected in Phase 6 Batch 6.3 ([TR-048](../review-findings.md#tr-048), [TR-049](../review-findings.md#tr-049), [TR-055](../review-findings.md#tr-055), [TR-057](../review-findings.md#tr-057)), and leave-one-site-out cross-validation was corrected in Batch 6.4 ([TR-050](../review-findings.md#tr-050) through [TR-053](../review-findings.md#tr-053)). Several ancillary paths are not suitable for decision use until their registered production findings are resolved:

- weighted-likelihood interpretation: [TR-059](../review-findings.md#tr-059); and
- longitude/latitude coordinates: [TR-060](../review-findings.md#tr-060).

Ungauged-site prediction, the regional credible bounds, dependent simulation, the spatial bootstrap, and the uncertainty-method dispatch were corrected in Batch 6.5 ([TR-054](../review-findings.md#tr-054), [TR-056](../review-findings.md#tr-056), [TR-058](../review-findings.md#tr-058), [TR-061](../review-findings.md#tr-061), [TR-062](../review-findings.md#tr-062)), together with the likelihood guard [TR-092](../review-findings.md#tr-092) and the latent-error default bounds [TR-093](../review-findings.md#tr-093).

Composite pairwise likelihood is a future enhancement. The current code always uses either independent marginal contributions or one Gaussian-copula contribution per observation row over the sites observed in that row.

## Notation and Units

| Symbol | Meaning |
|---|---|
| \(Y_{ij}\) | maximum in row \(i=1,\ldots,n\) at site \(j=1,\ldots,S\), in the response unit |
| \(\mathbf s_j=(s_{j1},s_{j2})\) | projected site coordinates in a common linear unit |
| \(\mathbf x_j\) | row of standardized or otherwise scaled site covariates |
| \(\xi_j,\alpha_j,\kappa_j\) | Numerics GEV location, scale, and shape at site \(j\) |
| \(\boldsymbol\beta_a\) | regression coefficients for GEV parameter \(a\) |
| \(\boldsymbol\epsilon_a\) | optional spatial-error vector for parameter \(a\) |
| \(\sigma_a,r_a,p_a\) | spatial-error standard deviation, range, and optional powered-exponential exponent |
| \(R(h)\) | spatial correlation at Euclidean separation \(h\) |
| \(\mathbf R_C\) | Gaussian-copula correlation matrix |
| \(w_j\) | site weight multiplying the marginal log density |
| \(p_E\) | exceedance probability; the nonexceedance probability is \(q=1-p_E\) |

Location, scale, quantiles, and location/scale spatial errors have response units unless a log link is used. Regression-coefficient units depend on the link and covariate scaling. Shape, copula probabilities, correlations, and powered-exponential exponents are dimensionless. Range parameters have exactly the unit of the coordinate columns.

## Site-Level GEV Model

RMC.Numerics 2.1.4 uses the shape sign convention documented in the [GEV chapter](../distributions/generalized-extreme-value.md). For

$$
z_{ij}=\frac{y_{ij}-\xi_j}{\alpha_j},
\qquad t_{ij}=1-\kappa_j z_{ij}>0, \tag{1}
$$

the CDF and density for \(\kappa_j\ne0\) are

$$
F_j(y_{ij})=\exp\!\left[-t_{ij}^{1/\kappa_j}\right], \tag{2}
$$

$$
f_j(y_{ij})=
\frac{1}{\alpha_j}
t_{ij}^{1/\kappa_j-1}
\exp\!\left[-t_{ij}^{1/\kappa_j}\right]. \tag{3}
$$

The \(\kappa_j\to0\) limit is Gumbel:

$$
F_j(y)=\exp\{-\exp[-(y-\xi_j)/\alpha_j]\}, \tag{4}
$$

$$
f_j(y)=\alpha_j^{-1}
\exp\!\left[-\frac{y-\xi_j}{\alpha_j}
-\exp\!\left(-\frac{y-\xi_j}{\alpha_j}\right)\right]. \tag{5}
$$

Thus \(\kappa_j<0\) gives an unbounded heavy upper tail, \(\kappa_j=0\) an exponential-type upper tail, and \(\kappa_j>0\) an upper endpoint \(\xi_j+\alpha_j/\kappa_j\). The nonexceedance quantile is

$$
Q_j(q)=
\begin{cases}
\xi_j+\dfrac{\alpha_j}{\kappa_j}
\left[1-\{-\log(q)\}^{\kappa_j}\right], & \kappa_j\ne0,\\[6pt]
\xi_j-\alpha_j\log[-\log(q)], & \kappa_j=0.
\end{cases} \tag{6}
$$

`SpatialGEVAnalysis` evaluates (6) at \(q=1-p_E\). A return period \(T=1/p_E\) is meaningful only when rows are comparable independent annual trials after accounting for the modeled within-row spatial dependence.

## Covariate Regression and Links

Each GEV parameter has a `GeneralLinearFunction`:

$$
\eta_{a,j}=\beta_{a,0}+\mathbf x_j^\mathsf T\boldsymbol\beta_a,
\qquad a\in\{\xi,\alpha,\kappa\}. \tag{7}
$$

The constructor creates one intercept followed by one coefficient per covariate column. Site \(j\) selects row \(j\) of that function's stored covariate matrix. All matrices used for a given parameter must therefore contain \(S\) rows in the same site order as the data and coordinates.

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

Covariates should be centered and scaled before model construction. This makes the intercept interpretable at a typical site and makes the default slope bounds \([-1,1]\) meaningful. Do not combine raw drainage area in square kilometres, elevation in metres, and precipitation in millimetres under the same default coefficient bounds without intentional rescaling and prior review.

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
h_{jk}=\sqrt{(s_{j1}-s_{k1})^2+(s_{j2}-s_{k2})^2}, \tag{12}
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

Both Gaussian-process and copula covariance evaluators use `CachedMultivariateNormal`. Setting a covariance matrix invalidates its Cholesky/log-determinant cache; repeated density calls at unchanged parameters reuse that factorization. Updating a range or exponent requires a new dense \(S\times S\) factorization, with \(O(S^3)\) time and \(O(S^2)\) storage. Highly colocated sites and very long fitted ranges can make the matrix nearly singular because no nugget is estimated.

Coordinates are not geodesic. Although some API remarks mention latitude/longitude, (12) is the pinned Numerics `Tools.Distance` calculation. Use a defensible projected coordinate reference system, pass both columns in the same linear unit, and interpret all range values in that unit. See [TR-060](../review-findings.md#tr-060).

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

Without copula dependence, `double.NaN` values are skipped and the available marginal contributions remain. With copula dependence, a row whose observed-site set is \(O_i\) contributes the Gaussian-copula density of the observed coordinates with the correlation submatrix \(\mathbf R_{C,O_i}\), that is, (18) restricted to \(O_i\); the unobserved coordinates are integrated out exactly because the Gaussian copula family is closed under marginalization (the copula interprets a missing site as missing at random given the observed sites). A row with a single observed site has no dependence term and a fully missing row contributes nothing. The implementation is `GaussianCopula.LogPDF(z, observedSites)`, which caches the Cholesky factorization of each missingness pattern until the correlation parameters change ([TR-048](../review-findings.md#tr-048), corrected 21 August 2026).

## Full Implemented Kernel

With enabled copula dependence, the contribution of row \(i\) with observed-site set \(O_i\) is

$$
L_i(\Theta)=
c_{\mathbf R_{C,O_i}}(\mathbf u_{i,O_i})
\prod_{j\in O_i} f_j(y_{ij})^{w_j}, \tag{19}
$$

with \(c_{\mathbf R_{C,O_i}}\equiv 1\) when \(|O_i|<2\). Without the copula, remove \(c\). With equal weights, \(w_j=1\). The scalar `DataLogLikelihood` is the observation log likelihood

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

where \(\mathcal E\) is the set of enabled spatial-error fields. The Gaussian-process terms are prior structure on the latent errors (Level 2 of the hierarchy), so they live in `PriorLogLikelihood` and not in `DataLogLikelihood` ([TR-049](../review-findings.md#tr-049), corrected 21 August 2026); the posterior kernel `LogLikelihood` is \(\ell_D+\ell_P\).

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

`SetDefaultParameters` derives intercept starting values and bounds from sitewise sample means and standard deviations, and sizes the latent-error bounds from three times the spread of those site statistics in the space in which the error acts (log space under a log link, raw units under an identity link; ceiling, floor 1.0 — [TR-093](../review-findings.md#tr-093)). A proposal whose site location, scale, or shape is not finite has negative-infinite likelihood ([TR-092](../review-findings.md#tr-092)). Shape intercept is initialized at zero with Uniform\((-0.5,0.5)\). `GeneralLinearFunction` assigns each covariate coefficient Uniform\((-1,1)\). Correlation ranges have Uniform\((\epsilon_{\rm mach},500)\) priors, and the powered-exponential exponent has Uniform\((0.1,2)\).

For an error field with data-derived bound \(M_a\), the implementation assigns

$$
\sigma_a\sim{\rm Uniform}(\epsilon_{\rm mach},M_a),\qquad
\epsilon_{a,j}\sim{\rm Uniform}(-M_a,M_a), \tag{22}
$$

and also multiplies by the joint Gaussian density in (11), which enters \(\ell_P\) in (20a). The Uniform latent-error priors therefore act as truncation constraints in addition to the Gaussian process. The posterior kernel is

$$
\pi(\Theta\mid\mathbf Y)\propto
\exp[\ell_D(\Theta)+\ell_P(\Theta)]. \tag{23}
$$

Because the range bound 500 is fixed rather than derived from the network, coordinate units and extent can place substantial prior mass in an irrelevant region or exclude plausible ranges. Review and, where supported by the API, revise every bound before MCMC.

### Pointwise likelihood

`PointwiseDataLogLikelihood` returns one value per row/year: the row's weighted observed-site marginals plus its observed-subset copula term, so the values sum to `DataLogLikelihood` in (20). `PointwisePriorLogLikelihood` reports each parameter prior and each enabled error process as components that sum to `PriorLogLikelihood` in (20a). WAIC and PSIS-LOO therefore score the row/year predictive unit of the fitted kernel, and the identities `LogLikelihood == DataLogLikelihood + PriorLogLikelihood`, `DataLogLikelihood == sum of pointwise rows`, and `PriorLogLikelihood == sum of pointwise prior components` hold (fast contracts and the `mvtnorm` cells; [TR-049](../review-findings.md#tr-049)).

## Estimation and Output Construction

`SpatialGEVAnalysis.RunAsync` validates the model, raises a cancellable start event, clears stale results, runs `BayesianAnalysis`, and post-processes only when MCMC reports an estimated result. Cancellation is forwarded to the sampler. The analysis constructs:

- `SpatialGEVSiteResults` for each site, containing posterior means and equal-tailed credible limits for \(\xi_j,\alpha_j,\kappa_j\) and \(Q_j(1-p_E)\);
- a point curve from the selected posterior mean or MAP parameter vector; and
- an aggregate `AnalysisResults` curve based on arithmetic averages across sites.

Changing probability ordinates or credible-interval width reprocesses saved posterior draws; it does not rerun MCMC. Changing model structure or parameters clears the fit.

### Leave-one-site-out cross-validation

`RunCrossValidationAsync` builds, for every site \(j\), the training network without that site (`SpatialGEV.CreateReducedModel`: the data column, coordinate row, covariate rows of every trend, copula coordinate, and latent error of site \(j\) are removed; flags, links, the remaining site weights, and every remaining parameter's value, bounds, and prior are copied), validates it, fits it with a fold `BayesianAnalysis` that carries the main analysis's sampler type, defaults policy (resolved against the fold's own parameter count), seed, interval width, output length, and point estimator (and its explicit iteration, chain, thinning, and tuning settings when the defaults are off), and predicts site \(j\) from the fold posterior at its coordinates with its own covariate rows. The T = 100 posterior-mean quantile minus the site's at-site maximum-likelihood GEV quantile is the site prediction error; the RMSE spans T = 2, 5, 10, 25, 50, and 100. The analysis model and posterior are never modified, so `CrossValidationResults` survives the run. `FoldStatus`, `FoldMessages`, `SuccessfulFolds`, and `TotalFolds` record folds without observations, with invalid or unfittable reduced models, or with non-finite predictions; such folds hold NaN metrics and are excluded from the aggregates, and a run with no successful fold throws. CRPS is not computed (zero-filled, documented). See [TR-050](../review-findings.md#tr-050) through [TR-053](../review-findings.md#tr-053); the per-fold latent-error interpolation is inverse-distance weighting, see [TR-054](../review-findings.md#tr-054).

The regional curve reports, for every probability, the posterior summaries of the regional mean quantile computed within each retained draw: the mean curve is its posterior mean (equal to the regional mean of the site posterior means) and the bounds are its equal-tailed posterior quantiles, so cross-site posterior dependence is retained; the mode curve is the regional mean of the point-estimate site curves ([TR-058](../review-findings.md#tr-058)). The regional growth curve keeps its descriptive site-average definition. Spatial AIC/BIC use the observation log likelihood `SpatialGEV.DataLogLikelihood` in (20) at the stored MAP (parameter priors and latent-error process densities excluded, missing sites marginalized), and BIC treats each nonempty row/year as one multivariate observation block rather than counting site cells (`SpatialGEVAnalysis.ComputeInformationCriteria`), so fully missing rows are excluded and contemporaneously dependent sites are not counted as independent replicates. They remain qualified diagnostics: the MAP is not an MLE when priors are nonconstant, and weighted or dependent spatial likelihoods do not automatically satisfy ordinary AIC/BIC regularity assumptions. WAIC and PSIS-LOO at the row/year unit are the preferred comparison tools; see [TR-055](../review-findings.md#tr-055).

`SpatialGEVUncertaintyMethod` selects how `RunAsync` builds the interval bounds after the MCMC fit ([TR-062](../review-findings.md#tr-062)): `BayesianPosterior` keeps the posterior intervals; `BayesianInflated` widens the site and regional intervals by the square root of the variance inflation factor from the empirical intersite correlation (`InflatePosteriorCovariance`); `GodambeSandwich` computes the Godambe covariance at the MAP (`ComputeGodambeCovariance`, both sandwich factors from the row/year estimating equations, explicit failure through `GodambeCovarianceStatus`; [TR-057](../review-findings.md#tr-057)), draws `OutputLength` seeded Gaussian parameter vectors N(MAP, Σ) truncated to the parameter bounds, and propagates them through the site-result machinery; `SpatialBootstrap` runs the temporal block bootstrap (`RunSpatialBootstrapAsync` with `BootstrapReplicates` and `BootstrapBlockSize`): rows are resampled with replacement in contiguous blocks keeping every site, each replicate (`SpatialGEV.CreateResampledModel`) is refitted by maximum a posteriori estimation warm-started at the full-model MAP, failed replicates are excluded with at least half required, and percentile intervals replace the site and regional bounds while `BootstrapResults` records the accounting ([TR-056](../review-findings.md#tr-056)). A method that cannot be applied (unavailable covariance, too few replicates) fails the run explicitly; `AppliedUncertaintyMethod` and `SpatialGEVSiteResults.UncertaintyMethod` record the method actually applied. The Godambe and bootstrap paths are frequentist diagnostics of the Bayesian fit.

## Ungauged Prediction

At a new projected location \(\mathbf s_*\) with matching covariates, the model-level `PredictAtUngauged` computes the regression trend and, for each enabled error field, simple-Gaussian-process conditioning:

$$
E(\epsilon_{a,*}\mid\boldsymbol\epsilon_a)
=\mathbf k_*^\mathsf T\mathbf K_a^{-1}\boldsymbol\epsilon_a, \tag{24}
$$

$$
{\rm Var}(\epsilon_{a,*}\mid\boldsymbol\epsilon_a)
=\sigma_a^2-\mathbf k_*^\mathsf T\mathbf K_a^{-1}\mathbf k_*. \tag{25}
$$

It returns conditional means in the GEV parameter vector and the three conditional error variances. If Cholesky factorization fails after diagonal-jitter attempts, `SpatialRegressionErrors` falls back to inverse-distance weighting.

The analysis-level `PredictAtUngaugedLocation` applies (24)–(25) to every retained posterior draw with that draw's error model and, by default (`SampleConditionalResidual = true`), adds a conditional residual drawn from N(0, (25)) with standard-normal scores generated from the analysis seed before the parallel loop, so the predictive interval carries both parameter uncertainty and the spatial-interpolation uncertainty of the latent errors; with `SampleConditionalResidual = false` it uses the conditional mean only ([TR-054](../review-findings.md#tr-054); the model-level predictor is verified against the R conditional-GP oracle). Its single covariate vector applies to every covariate trend (the trends must share the covariate definition) and is required whenever a trend has covariates: `GeneralLinearFunction.PredictWithCovariates` throws for a covariate trend evaluated without covariates ([TR-052](../review-findings.md#tr-052)). Extrapolation outside the covariate and spatial convex hull should be treated as a scenario, not validated regionalization.

## Site Weights and Pairwise Likelihood

`ComputeEffectiveSampleSizeWeights` calculates preliminary weights

$$
w_j^\star=\frac{1}{1+(S-1)\bar\rho_j},
\qquad
\bar\rho_j=\frac{1}{S-1}\sum_{k\ne j}|\hat\rho_{jk}|, \tag{26}
$$

then rescales them so \(\sum_jw_j=S\). They modify only the marginal part of (20); the full copula contribution remains unweighted. This is relative site weighting, not an effective-sample-size reduction or pairwise composite likelihood. See [TR-059](../review-findings.md#tr-059).

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
- **Marginal adequacy.** Every site follows the Numerics-sign GEV with parameter surfaces in (8)–(10). Physical upper bounds implied by \(\kappa>0\) must be checked.
- **Conditional structure.** The copula describes within-row dependence; Gaussian-process errors describe persistent spatial deviations of parameter surfaces. With few sites, the two layers and their ranges can be weakly separated.
- **No nugget.** Colocated or near-colocated sites and long ranges can produce ill-conditioned covariance matrices.
- **Covariate design.** Collinearity, incompatible scaling, or more regression terms than the network can support produces weak identification and prior sensitivity.
- **Shape complexity.** Site-specific shape errors add \(S\) latent tail parameters plus covariance hyperparameters. Rare-quantile inference can become prior-dominated.
- **Stationarity.** There is no time trend in the spatial model. Changes in climate, regulation, land use, or measurement practice violate a stationary block-maxima interpretation unless encoded outside this class.
- **Missingness.** Rows with missing sites are marginalized exactly under the Gaussian copula, which treats a missing site as missing at random given the observed sites; informative missingness is outside the model.
- **Extrapolation.** Predictions outside observed coordinate or covariate support are not validated by an in-sample fit.
- **Simulation.** With copula dependence enabled, `GenerateRandomValues` simulates rows through the Cholesky factor of the fitted correlation matrix (sample \(i\) of every site is one event; values grouped by site); without it the sites are independent ([TR-061](../review-findings.md#tr-061)).
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

The formulas and parameter bounds in this chapter were checked against RMC.Numerics 2.1.4 commit `828664650c9327b309ee8332e707ccca73588e93` and the current BestFit source. Fast unit tests cover correlation values and validation, cached multivariate-normal behavior, Gaussian-copula density behavior including the observed-subset evaluation, spatial-error parameter round trips and prediction helpers, `SpatialGEV` construction/likelihood components and the data/prior identities, the row/year criteria helper, the Godambe status contract, result DTOs, serialization, and analysis lifecycle. Phase 6 Batch 6.3 (21 August 2026) verified the likelihood against the R `mvtnorm` observed-subset and location-error oracle (eight exact cells), the criteria against a guarded MCMC run, and reran the nine spatial recovery cells; see the [spatial verification chapter](../../verification/spatial-extremes.md). Phase 6 Batch 6.4 (22 August 2026) verified leave-one-site-out cross-validation against independently reduced training models fitted through the production path (three guarded cells). Phase 6 Batch 6.5 (22 August 2026) verified the conditional Gaussian-process predictor against the R conditional-GP oracle, the per-draw prediction and regional posterior against recomputation from the retained draws, the dependent simulation by a seeded 20,000-row check, and the three dispatched uncertainty methods by guarded runs.

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
