<!-- technical-reference-status: complete -->

# Bivariate Copula Analysis

[<- Previous: Composite Analysis](composite.md) | [Back to Index](../index.md) | [Next: Coincident Frequency ->](coincident-frequency.md)

`BivariateDistribution` represents dependence between two already-specified continuous marginal models. `BivariateAnalysis` performs Bayesian estimation of the copula parameter block and converts posterior draws into joint exceedance probabilities. The implemented fit is deliberately two-stage: it does **not** re-estimate either marginal and it does **not** include marginal densities in its estimation objective.

This distinction matters in flood studies. A copula fit to peak flow and flood volume answers a dependence question conditional on the selected marginal distributions. It does not, by itself, propagate uncertainty in those marginal fits.

## Applicability

Use this analysis when observations are naturally paired by the same event or time index and the engineering question involves joint behavior, for example peak flow with flood volume, rainfall depth with duration, or coincident tributary peaks. Copulas separate marginal behavior from dependence through Sklar's theorem [1], [2]. They do not make unmatched records paired, correct nonrepresentative sampling, or establish physical causation.

The implemented data path accepts only index-matched `ExactData` observations for which neither value is marked as a low outlier. Censored, interval, threshold, and uncertain observations in the marginal data frames do not enter the copula fit. Fit those information types in the univariate stages, then recognize that the bivariate stage still uses only the surviving exact pairs.

## Notation

| Symbol | Meaning |
|---|---|
| \(X,Y\) | continuous hydrologic variables |
| \(F_X,F_Y\) | fitted marginal CDFs, treated as fixed by `BivariateDistribution` |
| \(f_X,f_Y\) | corresponding marginal densities |
| \(U=F_X(X),V=F_Y(Y)\) | probability-integral transforms |
| \(C_\psi(u,v)\) | copula CDF with dependence parameter vector \(\psi\) |
| \(c_\psi(u,v)=\partial^2 C_\psi/(\partial u\partial v)\) | copula density |
| \(n_p\) | number of eligible, index-matched exact pairs |
| \(\lambda_L,\lambda_U\) | lower- and upper-tail dependence coefficients |
| \(p_E\) | annual exceedance probability; return period \(T=1/p_E\) only under the usual annual-trial interpretation |

The variables retain their own units; \(u,v,\psi,\lambda_L,\lambda_U\), probabilities, and rank measures are dimensionless.

## Joint Model

For continuous marginals, Sklar's representation is

$$
H(x,y)=P(X\le x,Y\le y)=C_\psi\!\left(F_X(x),F_Y(y)\right). \tag{1}
$$

The corresponding theoretical joint density is

$$
h(x,y)=c_\psi(u,v)f_X(x)f_Y(y),
\qquad u=F_X(x),\quad v=F_Y(y). \tag{2}
$$

Equation (2) defines the full probability model, but the BestFit bivariate estimator uses only \(\log c_\psi\). Marginal fitting is an upstream responsibility.

For thresholds \(x,y\), Numerics defines

$$
P(X>x\ \text{and}\ Y>y)=1-u-v+C_\psi(u,v), \tag{3}
$$

$$
P(X>x\ \text{or}\ Y>y)=1-C_\psi(u,v). \tag{4}
$$

`BivariateAnalysis` constructs its frequency curve with the AND probability in (3). Do not interpret an AND return period as interchangeable with a marginal or OR return period [3], [4].

## Implemented Copula Families

The table follows RMC.Numerics 2.1.4 at commit `828664650c9327b309ee8332e707ccca73588e93`. Let \(\Phi_\rho\) and \(t_{\rho,\nu}\) denote standardized bivariate Normal and Student-\(t\) CDFs.

| `CopulaType` | \(C_\psi(u,v)\) | BestFit parameter bounds | Tail dependence |
|---|---|---|---|
| `AliMikhailHaq` | \(uv/[1-\theta(1-u)(1-v)]\) | \(-1+\epsilon<\theta<1-\epsilon\) | \(\lambda_L=\lambda_U=0\) |
| `Clayton` | \((u^{-\theta}+v^{-\theta}-1)^{-1/\theta}\), with the independence limit at \(\theta=0\) | \(-1\le\theta\le100\) | \(\lambda_L=2^{-1/\theta}\) for \(\theta>0\); \(\lambda_U=0\) |
| `Frank` | \(-\theta^{-1}\log[1+(e^{-\theta u}-1)(e^{-\theta v}-1)/(e^{-\theta}-1)]\) | \([0.001,100]\) when sample Kendall \(\tau>0\), otherwise \([-100,-0.001]\) | \(\lambda_L=\lambda_U=0\) |
| `Normal` | \(\Phi_\rho(\Phi^{-1}u,\Phi^{-1}v)\) | \(-1+\epsilon<\rho<1-\epsilon\) | \(\lambda_L=\lambda_U=0\) for \(|\rho|<1\) |
| `Gumbel` | \(\exp\{-[( -\log u)^\theta+(-\log v)^\theta]^{1/\theta}\}\) | \(1\le\theta\le100\) | \(\lambda_U=2-2^{1/\theta}\), \(\lambda_L=0\) |
| `Joe` | \(1-[(1-u)^\theta+(1-v)^\theta-(1-u)^\theta(1-v)^\theta]^{1/\theta}\) | \(1\le\theta\le100\) | \(\lambda_U=2-2^{1/\theta}\), \(\lambda_L=0\) |
| `StudentT` | \(t_{\rho,\nu}(t_\nu^{-1}u,t_\nu^{-1}v)\) | \(-1+\epsilon<\rho<1-\epsilon,\ 2+10^{-10}\le\nu\le30\) | symmetric nonzero tails except at limiting cases; see (5) |

Here \(\epsilon\) is `Tools.DoubleMachineEpsilon`. The Gumbel and Joe families represent positive association only. Clayton emphasizes lower-tail co-occurrence, while Gumbel and Joe emphasize the upper tail. For maxima such as peak and volume, upper-tail behavior is often the engineering focus. The Student-\(t\) copula has

$$
\lambda_L=\lambda_U=
2t_{\nu+1}\!\left[-\sqrt{\frac{(\nu+1)(1-\rho)}{1+\rho}}\right]. \tag{5}
$$

The Gaussian copula is asymptotically tail independent even when \(\rho\) is large. A good central fit therefore does not establish adequate joint-tail behavior.

### Copula densities

For the five implemented Archimedean families, let \(g\) be the generator such that

$$
C(u,v)=g^{-1}\!\left(g(u)+g(v)\right).
$$

Numerics evaluates the density as

$$
c(u,v)=
-\frac{g''(C(u,v))g'(u)g'(v)}{[g'(C(u,v))]^3}. \tag{6}
$$

The generators are \(g(t)=\log[(1-\theta(1-t))/t]\) for AMH, \(g(t)=(t^{-\theta}-1)/\theta\) for Clayton, \(g(t)=-\log[(e^{-\theta t}-1)/(e^{-\theta}-1)]\) for Frank, \(g(t)=(-\log t)^\theta\) for Gumbel, and \(g(t)=-\log[1-(1-t)^\theta]\) for Joe. Equation (6), together with the explicit CDFs above, fully specifies their densities.

For the Normal copula, with \(z_1=\Phi^{-1}(u)\) and \(z_2=\Phi^{-1}(v)\),

$$
c_\rho(u,v)=\frac{1}{\sqrt{1-\rho^2}}
\exp\left[
-\frac{\rho^2z_1^2+\rho^2z_2^2-2\rho z_1z_2}
{2(1-\rho^2)}
\right]. \tag{7}
$$

For Student-\(t\), \(c_{\rho,\nu}\) is the standardized bivariate-\(t\) density divided by the two univariate-\(t\) densities. Numerics evaluates its logarithm directly with gamma functions and quadratic form \(z_1^2-2\rho z_1z_2+z_2^2\) to avoid underflow [5].

## Pair Construction

`SetSampleData()` sorts eligible exact observations by `Index` and performs a two-pointer match. An index present in only one marginal is discarded. Low outliers are excluded independently before matching. The resulting pair count can therefore be much smaller than either marginal record length.

For pseudo-likelihood, each pair is

$$
(\tilde u_i,\tilde v_i)=
(1-p_{X,i},1-p_{Y,i}), \tag{8}
$$

where `PlottingPosition` stores exceedance probability and `PlottingPositionComplement` is the nonexceedance pseudo-observation. If either data frame has stale or invalid pseudo-observations, BestFit calls its plotting-position routine before rebuilding the pair set. Pseudo-observations must lie strictly inside \((0,1)\).

For inference from margins (IFM), the stored pair is the raw \((x_i,y_i)\), and probability transforms are recomputed from the fixed marginal distributions during every likelihood evaluation [6].

## Likelihood, Prior, and Posterior

### Pseudo-likelihood

The semiparametric copula log pseudo-likelihood is

$$
\ell_{PL}(\psi)=\sum_{i=1}^{n_p}
\log c_\psi(\tilde u_i,\tilde v_i). \tag{9}
$$

### Inference from margins

With previously estimated marginals,

$$
\ell_{IFM}(\psi\mid\hat\eta_X,\hat\eta_Y)=
\sum_{i=1}^{n_p}
\log c_\psi\!\left[
F_X(x_i;\hat\eta_X),F_Y(y_i;\hat\eta_Y)
\right]. \tag{10}
$$

`DataLogLikelihood` returns (9) or (10). It does not add \(\log f_X+\log f_Y\), so it is a dependence-stage likelihood rather than the full joint-data likelihood from (2). `PointwiseDataLogLikelihood` returns the \(n_p\) summands for WAIC and PSIS-LOO calculations on paired events.

BestFit creates one `ModelParameter` per copula parameter and assigns an independent Uniform prior over the Numerics bounds. Thus

$$
p(\psi\mid\mathcal D,\hat\eta_X,\hat\eta_Y)
\propto \exp\{\ell(\psi)\}
\prod_{r=1}^{d_\psi}
\frac{\mathbf 1(a_r\le\psi_r\le b_r)}{b_r-a_r}. \tag{11}
$$

Student-\(t\) has parameters `Dependency (θ)` and `DegreesOfFreedom`; all other families have one dependence parameter. Student-\(t\) initializes \(\nu=5\); other parameters initialize at the midpoint of their bounds. Frank's prior interval is selected from the sign of sample Kendall \(\tau\), so its support is data-dependent.

## Estimation and Output Construction

`BivariateAnalysis.RunAsync` validates the model, rebuilds pairs, runs `BayesianAnalysis`, and then constructs joint-frequency uncertainty. Arithmetic exceptions and non-finite copula or marginal-CDF evaluations are converted to `double.NegativeInfinity`, rejecting the proposal without terminating the chain.

For every requested \((x_k,y_k)\) ordinate, the point curve uses (3) at the configured MAP or posterior-mean copula parameter. Posterior mean and credible limits are calculated from the same AND probability over copula draws. The marginals remain fixed for every draw. Consequently these bands quantify copula-parameter uncertainty conditional on the marginal fits, not total bivariate uncertainty.

The reported RMSE compares \(C(F_{nX},F_{nY})\) with the empirical bivariate CDF at each matched pseudo-observation and divides the squared-error sum by \(n_p-1\). It requires at least two pairs. `GenerateRandomValues(sampleSize, seed)` delegates to the Numerics copula's Latin-hypercube simulation and transforms both uniforms through the attached marginal inverse CDFs.

The AIC/BIC outputs use the copula data log likelihood at the stored MAP and the number of matched event pairs for BIC; copula-prior densities are excluded. When the copula prior is constant over the relevant region, MAP coincides with the constrained copula MLE and the criteria have their usual likelihood interpretation conditional on the already-fitted marginals. With an informative copula prior, use DIC, WAIC, or verified PSIS-LOO instead. All comparisons remain conditional on the fixed marginal distributions and require the same paired events and likelihood convention; see [TR-047](../review-findings.md#tr-047).

## Compile-Checked Workflow

The following configuration uses annual peak flow in cubic feet per second and event flood volume in a consistent volume unit. In an actual study, estimate and diagnose `peakModel` and `volumeModel` first; then pass their fitted distributions into this copula stage.

<!-- snippet: bivariate-workflow -->
```csharp
private static BivariateAnalysis ConfigureBivariateAnalysis()
{
    double[] peakFlow =
    {
        1_240, 1_510, 1_370, 1_860, 2_110, 1_740,
        2_430, 2_080, 2_760, 2_350, 3_020, 2_690
    };
    double[] floodVolume =
    {
        18.2, 22.5, 20.1, 27.8, 31.4, 25.7,
        35.9, 30.6, 40.8, 34.2, 44.7, 39.1
    };

    var peakData = new RMC.BestFit.Models.DataFrame
    {
        ExactSeries = new ExactSeries(peakFlow)
    };
    var volumeData = new RMC.BestFit.Models.DataFrame
    {
        ExactSeries = new ExactSeries(floodVolume)
    };

    var peakModel = new UnivariateDistribution(
        peakData,
        UnivariateDistributionType.GeneralizedExtremeValue);
    var volumeModel = new UnivariateDistribution(
        volumeData,
        UnivariateDistributionType.LogNormal);

    var jointModel = new BivariateDistribution(
        peakModel,
        volumeModel,
        CopulaType.Gumbel)
    {
        CopulaEstimationMethod =
            CopulaEstimationMethod.InferenceFromMargins
    };
    jointModel.SetSampleData();

    return new BivariateAnalysis(jointModel);
}
```

Before interpreting joint-tail probabilities, compare plausible families with different tail structures, inspect empirical-versus-fitted joint probabilities, test sensitivity to each marginal, and examine influential paired events. A visually acceptable Gaussian copula is not evidence of upper-tail dependence.

## Assumptions and Limitations

- Pairs are independent and identically distributed across event index after any selection procedure; serial dependence or clustered storms require separate treatment.
- Each pair represents the same physical event or sampling interval. Index matching is mechanical, not scientific validation.
- Both marginals are continuous and correctly parameterized. Ties and discrete variables weaken rank-based pseudo-likelihood assumptions.
- IFM treats marginal estimates as fixed. Bivariate posterior intervals omit marginal parameter and marginal model uncertainty.
- Only exact, non-low-outlier pairs enter the copula likelihood; censoring and measurement error are not propagated jointly.
- Dependence is stationary over the pair record; no copula trend model is exposed.
- Tail extrapolation is controlled strongly by family choice and a small number of extreme pairs. Report sensitivity rather than a single unqualified joint return period.
- The Student-\(t\) upper bound \(\nu=30\) intentionally treats larger values as practically Gaussian at typical hydrologic sample sizes.

## Validation and Traceability

Implementation: `Models/BivariateDistribution/BivariateDistribution.cs` and `Analyses/Bivariate/BivariateAnalysis.cs`. Copula formulas, constraints, tail-dependence properties, and simulation come from the pinned Numerics files under `Distributions/Bivariate Copulas`. Fast BestFit tests cover construction, pair matching, parameter propagation, likelihood decomposition, simulation shape, validation, XML round trips, and analysis lifecycle. Numerics tests contain fixed CDF, density, tail-dependence, inverse-transform, and parameter-constraint assertions for each family. Long-running estimator recovery sources under `RMC.BestFit.Verification/Bivariate` were inspected but not executed during this documentation pass.

## References

<a id="ref-1"></a>[1] A. Sklar, “Fonctions de répartition à n dimensions et leurs marges,” *Publ. Inst. Statist. Univ. Paris*, vol. 8, pp. 229–231, 1959.

<a id="ref-2"></a>[2] R. B. Nelsen, *An Introduction to Copulas*, 2nd ed. New York, NY, USA: Springer, 2006.

<a id="ref-3"></a>[3] A.-C. Favre, S. Quessy, and M. H. T. T. Nguyen, “Multivariate hydrological frequency analysis using copulas,” *Water Resour. Res.*, vol. 40, W01101, 2004, doi: 10.1029/2003WR002456.

<a id="ref-4"></a>[4] F. Serinaldi, “An uncertain journey around the tails of multivariate hydrological distributions,” *Water Resour. Res.*, vol. 49, no. 10, pp. 6527–6547, 2013, doi: 10.1002/wrcr.20531.

<a id="ref-5"></a>[5] S. Demarta and A. J. McNeil, “The t copula and related copulas,” *Int. Stat. Rev.*, vol. 73, no. 1, pp. 111–129, 2005, doi: 10.1111/j.1751-5823.2005.tb00254.x.

<a id="ref-6"></a>[6] H. Joe, “Asymptotic efficiency of the two-stage estimation method for copula-based models,” *J. R. Stat. Soc. B*, vol. 67, no. 3, pp. 409–419, 2005.

<a id="ref-7"></a>[7] C. Genest, K. Ghoudi, and L.-P. Rivest, “A semiparametric estimation procedure of dependence parameters in multivariate families of distributions,” *Biometrika*, vol. 82, no. 3, pp. 543–552, 1995.

---

[<- Previous: Composite Analysis](composite.md) | [Back to Index](../index.md) | [Next: Coincident Frequency ->](coincident-frequency.md)
