# Identifiable Competing-Risk Recovery Design

**Date:** 2026-08-30
**Scope:** BestFit Chunk 9 competing-risk recovery verification and the matching Numerics MLE recovery fixtures
**Authority:** Haden Smith approved thinning the recovery matrix, redesigning every retained experiment for clear identification, retaining BestFit/Numerics MLE duplication as a cross-implementation sanity check, excluding Bayesian MCMC from the correlated fixture, and proceeding with the redesigned maximum after preserving its Bayesian boundary-mode failure as a finding.

## Problem

The current BestFit matrix contains twenty generated-parent recovery methods formed by crossing ten data-generating designs with MLE and Bayesian estimators. Eight designs were copied from Numerics. Several maximum and three-component fixtures do not contain enough visible dog-leg behavior to identify their latent component distributions from 1,000 composite observations:

- some components win fewer than 100 generated observations;
- some components are never the most likely source over a meaningful part of the composite distribution;
- several nominally separated maximum fixtures are nearly single-component composites;
- the existing count gives an inflated impression of scientific coverage because the same poorly identified design is repeated for two estimators.

The oversized matrix defect is experimental design and acceptance, not the production competing-risk log-PDF. The redesigned maximum also exposed a distinct default-prior/MAP boundary mode for Bayesian maximum recovery; that production contract is documented rather than changed. This remediation does not change a production algorithm, likelihood, formula, prior, sampler, seed, optimizer, convergence rule, default, or public/persisted-data contract.

## Identification model

For independent minimum competing risks, component `i` contributes

$$
g_i(y)=f_i(y)\prod_{j\ne i}S_j(y),
\qquad
r_i(y)=\frac{g_i(y)}{\sum_j g_j(y)}.
$$

For independent maximum competing risks, replace each survival function with its CDF:

$$
g_i(y)=f_i(y)\prod_{j\ne i}F_j(y).
$$

For the fixed-correlation bivariate minimum, the component contribution uses the marginal density multiplied by the conditional Gaussian-copula survival probability of the other component. Correlation remains fixed at the generating value; it is not estimated.

Each verification-only fixture records both:

- the hard winning component from the generated latent draws; and
- the soft event contribution $\sum_n r_i(y_n)$ from the observed composite likelihood.

Hard winner counts describe the realized generator, but they do not replace full-likelihood covariance because non-winning components still contribute censoring information. MLE uncertainty therefore comes from the full competing-risk likelihood through an observed-information Hessian or a profile interval. Component data effective sample size is an experimental eligibility diagnostic, not the covariance itself.

Bayesian MCMC effective sample size is a separate chain diagnostic and must never be conflated with component data effective sample size.

## Predeclared experiment gates

A retained design must meet all of these gates at the fixed `N=1000`, seed `12345` realization:

1. Every theoretical component winner share is at least 15 percent.
2. Every realized component has at least 100 hard wins.
3. Every component is the most likely source over at least 10 percent of the parent composite probability scale.
4. A K-component design has K-1 ordered interior dog-leg responsibility crossovers inside composite probabilities 0.10 through 0.90; additional extreme-tail dominance re-entry is retained in the diagnostic.
5. The generating parameters lie inside all estimator bounds and, for Bayesian fits, inside all configured priors.
6. The retained fixture must demonstrate coordinate identification in actual recovery evidence; balanced winner shares and dog-leg crossovers are necessary but not sufficient.

The diagnostics are asserted before estimator execution so a later recovery failure cannot be misreported as a valid test of an unidentifiable fixture.

## Retained experiment matrix

| Design | Parent components | Rule/dependence | Expected winner shares | Fixed-seed hard wins | BestFit estimators | Numerics estimators |
|---|---|---|---:|---:|---|---|
| Two-Weibull minimum dog leg | Weibull(50, 1), Weibull(80, 3) | Minimum, independent | 72.7%, 27.3% | 720, 280 | MLE + Bayesian | MLE |
| Weibull-Gumbel maximum dog leg | Weibull(100, 3), Gumbel(80, 20) | Maximum, independent | 48.7%, 51.3% | 495, 505 | MLE | MLE |
| Correlated two-Weibull minimum dog leg | Weibull(50, 1), Weibull(80, 3), rho=0.6 | Minimum, fixed Gaussian correlation | 78.7%, 21.3% | 776, 224 | MLE only | MLE |

The correlated fixture intentionally has no Bayesian recovery method. Its likelihood is substantially more expensive because the bivariate correlated density is obtained by numerical differentiation. The maximum also has no retained Bayesian method: under the current default scale prior its Weibull component can disappear at the scale boundary while the remaining Gumbel has finite likelihood, so the production MAP initializes a collapsed, rank-deficient posterior mode. The independent minimum retains Bayesian component recovery. No Cholesky cache, prior change, or other production optimization is introduced.

## Removed recovery experiments

The following current designs are deleted from the recovery matrix rather than weakened into aggregate-curve tests:

- minimum Weibull(30, 0.8) plus Weibull(100, 3): borderline 88/12 contribution split;
- the current three-Weibull minimum fixtures: third components contribute only about 1 to 2 percent;
- the replacement three-Weibull bathtub candidate, Weibull(135, 0.7), Weibull(100, 1), and Weibull(96, 4): although its theoretical shares (38.8%, 39.4%, 21.9%), fixed-seed hard wins (400, 396, 204), and dog-leg crossovers passed the pre-fit gates, BestFit MLE drove one shape to its bound, Numerics observed information was not positive definite, and the BestFit Bayesian central interval excluded the first generating scale by a wide margin. The balanced winner design therefore remained coordinate-nonidentifiable and was removed rather than tuned;
- maximum Normal(50, 8) plus Normal(85, 12): first component contributes about 1 percent;
- maximum Weibull(50, 2) plus Gumbel(70, 15): the Weibull never owns a meaningful dog-leg region;
- maximum of three separated Normals: the largest Normal contributes about 99 percent;
- maximum Exponential/Gamma/LogNormal: the Exponential contributes effectively zero and the Gamma does not own a meaningful region;
- correlated maximum Normal(50, 10) plus Normal(65, 12): the lower Normal contributes about 7 percent and owns no meaningful dog-leg region.
- the former maximum Weibull(80, 2) plus Gumbel(60, 10): balanced hard wins alone did not prevent both single-start MLE implementations from selecting the same inferior local mode; it remains a documented failure example rather than a recovery fixture.

Removing these estimator repetitions does not remove analytical competing-risk coverage. Existing independent/dependent CDF, PDF, simulation, correlation, caching, seed, clone, and serialization contracts remain in their current owners.

## Recovery acceptance

For each identified MLE coordinate:

- calculate uncertainty from the full competing-risk data log-likelihood at the fitted optimum;
- require the generating coordinate inside a predeclared central 95 percent interval, equivalently absolute standardized error no greater than 1.96 when the local quadratic approximation is valid;
- report the covariance source and design diagnostics in the assertion evidence.

For each identified Bayesian coordinate:

- reconstruct and monitor the declared component-coordinate ordering;
- require generating truth inside the central 95 percent posterior interval;
- require R-hat below 1.10;
- require chain ESS at least 100.

The analytically known composite CDF/quantile response remains a secondary recovery check, not a substitute for component-coordinate recovery in these deliberately identified fixtures.

## Verification-only labeled generation

The diagnostic generator is private to the Verification test assembly. It reproduces the production call order:

- independent fixtures use `MersenneTwister(12345)`, draw one uniform per component per observation, transform through each component inverse CDF, and take the declared minimum or maximum;
- the correlated fixture uses the same fixed correlation matrix and multivariate-normal-to-uniform transform as production;
- the resulting composite sample must match `CompetingRisks.GenerateRandomValues(1000, 12345)` before its labels are used as evidence.

No label or latent draw is exposed through a production API.

## Exact recovery identities

BestFit retains four estimator methods:

- `MLE_Minimum_TwoWeibullDogLeg_RecoversParent`
- `Bayesian_Minimum_TwoWeibullDogLeg_RecoversParent`
- `MLE_Maximum_WeibullGumbelDogLeg_RecoversParent`
- `MLE_Minimum_CorrelatedTwoWeibullDogLeg_RecoversParent`

Numerics retains three MLE methods over the same generating fixtures:

- `Test_MLE_MinRule_2Dist_Weibull_DogLeg`
- `Test_MLE_MaxRule_2Dist_Weibull_Gumbel_DogLeg`
- `Test_MLE_CorrelatedMinRule_2Dist_Weibull_DogLeg`

The MLE overlap is intentional: Numerics uses its own `CompetingRisks.MLE` machinery while BestFit uses its estimator layer, so matching well-designed fixtures provide a useful cross-implementation sanity check without being treated as an independent scientific oracle.

The retained maximum fixture has balanced theoretical shares 48.7/51.3 percent, hard wins 495/505, soft counts 488.2/511.8, an interior crossover at 0.474, and an expected Gumbel extreme-tail re-entry at 0.987. Both MLE implementations recover it under the full-likelihood central-95-percent coordinate rule. The discarded Bayesian maximum identity selected a disappeared-Weibull boundary mode because of the unchanged default scale prior and MAP initialization; correcting that production contract requires separate approval.

## Documentation and evidence boundary

The catalog, inventory, competing-risk verification chapter, mirrored report chapter, and master-plan checkpoint must record:

- why the matrix was thinned;
- the design gates and realized source shares;
- which current identities replaced historical identities;
- exact isolated TRX evidence for every retained BestFit method;
- current failures as findings rather than tuned fixtures.

No full Verification suite or Bulletin 17C confidence-interval coverage method may run. Chunk 11A remains out of scope.
