<!-- technical-reference-status: complete -->

# Coincident-Frequency Response Analysis

[<- Previous: Bivariate Analysis](bivariate.md) | [Back to Index](../index.md) | [Next: Rating Curve ->](rating-curve.md)

`CoincidentFrequencyAnalysis` propagates a fitted bivariate probability model through a deterministic tabulated response \(Z=g(X,Y)\). A typical use is converting joint flood peak and volume into maximum reservoir elevation obtained from routing simulations. The analysis performs numerical integration and posterior post-processing; it does not fit a new stochastic model to observations of \(Z\).

## Applicability and Workflow Boundary

The upstream `BivariateAnalysis` supplies two continuous marginal distributions and a fitted copula. The user supplies:

- strictly ascending primary ordinates \(x_0,\ldots,x_{M-1}\);
- strictly ascending secondary ordinates \(y_0,\ldots,y_{N-1}\);
- an \(M\times N\) response matrix \(r_{ij}=g(x_i,y_j)\); and
- the number \(K\) of output response bins.

BestFit requires \(M,N\ge2\), \(5\le K\le1000\), finite response values, and strict increase of \(r_{ij}\) along both grid axes. A warning is returned when \(K>100\). The bivariate analysis must already be estimated. These checks establish numerical invertibility; they do not establish that the hydraulic or hydrologic response surface is physically valid.

## Notation

| Symbol | Meaning |
|---|---|
| \(X,Y\) | random forcing variables with fixed or posterior-varying marginals |
| \(Z=g(X,Y)\) | deterministic response of interest |
| \(F_X,F_Y\) | marginal CDFs |
| \(C\) | fitted bivariate copula CDF |
| \(r_{ij}\) | supplied response at \((x_i,y_j)\) |
| \(e_j\) | real-space boundary of the \(j\)-th \(Y\) integration bin |
| \(v_j=F_Y(e_j)\) | boundary in copula space |
| \(\zeta_i=\Phi^{-1}[F_X(x_i)]\) | Normal probability-paper coordinate for the primary grid |
| \(u^*_{kj}\) | \(F_X\) value obtained by inverting response column \(j\) at output \(z_k\) |
| \(F_Z(z)\) | CDF of the derived response |
| \(A_Z(z)=1-F_Z(z)\) | response annual exceedance probability when the inputs represent annual events |

The units of \(Z\) are the units of the supplied response matrix. The analysis never infers or converts those units.

## Continuous Target and Implemented Approximation

For a monotone response, the exact derived CDF is

$$
F_Z(z)=P[g(X,Y)\le z]
=\int P[X\le x^*(z,y)\mid Y=y]\,dF_Y(y), \tag{1}
$$

where \(x^*(z,y)\) solves \(g(x^*,y)=z\). BestFit approximates (1) by partitioning the \(Y\) axis into \(N\) bins represented by the response-surface columns.

### Output grid

The implementation scans the complete response matrix for

$$
z_{\min}=\min_{i,j}r_{ij},
\qquad z_{\max}=\max_{i,j}r_{ij}, \tag{2}
$$

and creates \(K\) endpoint-inclusive, evenly spaced values between them. `SetZOutputValues` supports deserialization and restoration; a new `CreateFrequencyAnalysisResultsAsync` call rebuilds this grid from (2).

### Secondary-variable bins

The real-space edges are

$$
e_0=-\infty,\qquad
e_j=\frac{y_{j-1}+y_j}{2}\ (j=1,\ldots,N-1),\qquad
e_N=+\infty. \tag{3}
$$

They are mapped to

$$
v_0=0,\qquad v_j=F_Y(e_j),\qquad v_N=1. \tag{4}
$$

Thus all lower-tail \(Y\) probability is assigned to the first response column and all upper-tail probability to the last. This is a modeling approximation, not response-surface extrapolation in the \(Y\) direction.

### Primary-axis inversion

For posterior realization \(s\), BestFit calculates

$$
\zeta_i^{(s)}=
\Phi^{-1}\!\left[
F_X(x_i;\eta_X^{(s)})
\right], \tag{5}
$$

after clamping the probability to \([10^{-12},1-10^{-12}]\). Within response column \(j\), the pairs \((r_{ij},\zeta_i^{(s)})\) define a piecewise-linear function. At each \(z_k\), linear interpolation gives \(\zeta^*_{kj}\); values below or above the column range use the first or last segment for linear extrapolation. The copula-scale threshold is

$$
u^*_{kj}=\Phi(\zeta^*_{kj}), \tag{6}
$$

again clamped to the same open interval.

Interpolation in Normal-score space is exactly what the implementation does. It coincides with linear interpolation in \(x\) for a Normal marginal but not for GEV, Log-Pearson III, or other nonlinear marginals.

### Copula integration

For output \(z_k\), the implemented approximation is

$$
\widehat F_Z(z_k)=
\sum_{j=0}^{N-1}
\left[
C(u^*_{kj},v_{j+1})
-C(u^*_{kj},v_j)
\right]. \tag{7}
$$

The boundary identities \(C(u,0)=0\) and \(C(u,1)=u\) are applied analytically, avoiding inverse-Normal calls at exact probability boundaries. Each negative column contribution caused by numerical error is replaced by zero, and the final sum is clamped to \([0,1]\). The reported curve is

$$
\widehat A_Z(z_k)=1-\widehat F_Z(z_k). \tag{8}
$$

Equation (7) is a column-bin quadrature. Accuracy depends on response smoothness, grid density, tail coverage, and the suitability of assigning the entire \(Y\)-bin probability to its representative response column.

## Uncertainty Propagation

The copula posterior from the upstream `BivariateAnalysis` is the required draw source. `MarginalXChain` and `MarginalYChain` are optional:

$$
R=\min(R_C,R_X,R_Y), \tag{9}
$$

where an absent marginal chain is treated as having unlimited length and its configured point-estimate distribution is reused. Draw \(s\) combines copula draw \(s\) with marginal draw \(s\) when supplied. This pairing approximates propagation from separately fitted marginal and copula stages; it is not a joint MCMC fit.

For every \(z_k\), BestFit stores the posterior mean of \(\widehat A_Z^{(s)}(z_k)\) and equal-tail credible limits at the configured width. The point curve uses MAP or posterior-mean parameters according to `BayesianAnalysis.PointEstimator`. The `BayesianAnalysis` owned by coincident-frequency analysis holds presentation settings; it does not run its own chain.

If the copula chain has no output draws, the deterministic point-estimate path remains available: the mean curve is copied from the point curve and confidence limits are `NaN`. If optional marginal chains have fewer draws than the copula chain, the shortest chain truncates propagation.

The response matrix is held fixed for every realization. Hydraulic parameter, routing-model, terrain, operating-rule, and numerical-model uncertainty are therefore absent unless the user represents them outside this API.

## Compile-Checked Workflow

The response below is a monotone maximum-pool-elevation table indexed by peak flow and flood volume. The upstream bivariate analysis must be successfully estimated before `RunAsync`.

<!-- snippet: coincident-frequency-workflow -->
```csharp
private static CoincidentFrequencyAnalysis ConfigureCoincidentFrequency(
    BivariateAnalysis fittedJointModel)
{
    double[] peakFlow = { 1_000, 2_000, 3_000, 4_000 };
    double[] floodVolume = { 10, 20, 30 };
    double[,] maximumPoolElevation =
    {
        { 1_205.1, 1_206.3, 1_207.4 },
        { 1_208.0, 1_209.5, 1_211.0 },
        { 1_211.2, 1_213.0, 1_214.8 },
        { 1_214.0, 1_216.1, 1_218.3 }
    };

    return new CoincidentFrequencyAnalysis(
        fittedJointModel,
        peakFlow,
        floodVolume,
        maximumPoolElevation)
    {
        NumberOfBins = 100
    };
}
```

After configuration, call `Validate()`, examine every error and warning, and only then call `RunAsync()`. Report the flow, volume, and elevation units with the result. A useful sensitivity analysis varies grid resolution, response-domain extent, copula family, both marginals, and optional marginal posterior chains.

## Numerical and Engineering Cautions

- **Monotonicity is mandatory.** Small response noise that reverses a column or row causes validation failure. Smooth only with a physically defensible method that preserves the governing model.
- **The tails are collapsed in \(Y\).** All \(Y<e_1\) mass uses column 0 and all \(Y>e_{N-1}\) mass uses column \(N-1\). Extend the response grid when these regions matter.
- **The primary axis is extrapolated.** Sparse end segments can dominate rare-response probabilities. Plot \((r_{ij},\zeta_i)\) by column and inspect the extrapolated slopes.
- **The output domain is finite.** The generated \(z_k\) values cover only the supplied response minimum and maximum, even though column inversion extrapolates internally.
- **AEP semantics are inherited.** Calling (8) an annual exceedance probability requires the fitted \((X,Y)\) pairs to represent annual trials. Event-based or peaks-over-threshold inputs require an exposure/rate conversion outside this analysis.
- **No response error model is present.** The surface is deterministic and exactly known to the algorithm.
- **Grid refinement is not an uncertainty interval.** Compare successively refined surfaces and treat numerical convergence separately from statistical posterior uncertainty.
- **Probability clipping is numerical protection.** It prevents infinite Normal scores; it also imposes a finite effective tail boundary.

## Validation and Traceability

Implementation: `Analyses/Bivariate/CoincidentFrequencyAnalysis.cs`, with upstream probability operations in `Models/BivariateDistribution/BivariateDistribution.cs` and pinned Numerics copulas. Fast tests cover constructors, validation, monotone-grid requirements, point-estimate integration, boundary mass conservation, marginal-draw propagation, cancellation, serialization, and analysis state. The compile-checked example above is sourced from `BivariateAndSpatialExamples.cs`. Long-running bivariate verification sources were not executed during this documentation pass.

No external verification claim is made for a project-specific response surface. For review, archive the surface-generation model, input version, units, grid-convergence study, and deterministic reproduction command alongside the BestFit project.

## References

<a id="ref-1"></a>[1] A. Sklar, “Fonctions de répartition à n dimensions et leurs marges,” *Publ. Inst. Statist. Univ. Paris*, vol. 8, pp. 229–231, 1959.

<a id="ref-2"></a>[2] R. B. Nelsen, *An Introduction to Copulas*, 2nd ed. New York, NY, USA: Springer, 2006.

<a id="ref-3"></a>[3] S.-C. Kao and R. S. Govindaraju, “Probabilistic structure of storm surface runoff considering the dependence between average intensity and storm duration of rainfall events,” *Water Resour. Res.*, vol. 43, W06410, 2007, doi: 10.1029/2006WR005564.

<a id="ref-4"></a>[4] A.-C. Favre, S. Quessy, and M. H. T. T. Nguyen, “Multivariate hydrological frequency analysis using copulas,” *Water Resour. Res.*, vol. 40, W01101, 2004, doi: 10.1029/2003WR002456.

---

[<- Previous: Bivariate Analysis](bivariate.md) | [Back to Index](../index.md) | [Next: Rating Curve ->](rating-curve.md)
