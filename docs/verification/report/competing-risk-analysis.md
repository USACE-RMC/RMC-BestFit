<!-- verification-status: publication-draft -->

# Competing-Risk Analysis

A competing-risk distribution describes the minimum or maximum of several potential outcomes.
For a flood maximum, different mechanisms may each produce an event, but only the largest value
is observed. For a minimum, the first or weakest outcome controls. Unlike a mixture, this model
does not randomly select one population using fixed proportions. Each component's chance of
controlling the observed value depends on its distribution and its dependence on the other components.

Verification addresses two questions: does simulation preserve the requested dependence, and can
estimation recover the separate component distributions from observations of only their minimum or
maximum? The examples are synthetic and use arbitrary, consistent magnitude units.

## Dependence theory and independent simulation reference

The four simulation tests use two Normal components with mean 0 and standard deviation 1.
Each generates 40,000 maxima with seed 24681357. The fraction of maxima at or below zero is compared
with a probability calculated independently. A Gaussian copula supplies the dependence: it links
component percentiles through correlated Normal scores, with correlation denoted by $\rho$.

A second comparison checks rank correlation, which measures whether the two component values tend
to rise and fall together. To inspect those ranks through the maximum-generation interface, the test
uses two additional runs: one component has mean 1,000 and the other mean 0, both with standard
deviation 1, then their means are exchanged. The separation makes each run reveal one component's
ordering without reproducing the production sampling algorithm.

For Gaussian dependence, the expected rank correlation $\rho_S$ and zero-threshold probability are

$$
\rho_S=\frac{6}{\pi}\arcsin(\rho/2),\qquad
P[\max(X_1,X_2)\le0]=\frac14+\frac{\arcsin(\rho)}{2\pi}.\tag{C.1}
$$

| Dependence setting | Independent target | BestFit result |
|---|---|---|
| Independent components, $\rho=0$ | Rank correlation 0; probability of maximum at or below zero 0.25 | Passed both comparisons |
| Perfect positive dependence | Rank correlation 1; probability 0.5 | Passed both comparisons |
| Perfect negative limiting dependence | Equation (C.1), evaluated at $\rho=-1+\sqrt{\epsilon}$, where $\epsilon$ is machine precision | Passed both comparisons |
| User-supplied correlation, $\rho=0.6$ | Equation (C.1), evaluated at the specified correlation | Passed both comparisons |

The rank tolerance is $6/\sqrt{n-1}$. The probability tolerance is six binomial standard errors plus
$1/n$, with $n=40{,}000$. These bounds allow expected simulation variation.
All four dependence settings passed both comparisons.

## Identified recovery designs

Each recovery design contains 1,000 observed minima or maxima, generated with seed 12345.
A Weibull distribution is specified by its **scale and shape**; a Gumbel distribution by its
**location and scale**. These parameters are not interchangeable with means and standard deviations.

| Design | First generating component | Second generating component | Dependence |
|---|---|---|---|
| Independent minimum | Weibull scale 50, shape 1 | Weibull scale 80, shape 3 | Independent |
| Independent maximum | Weibull scale 100, shape 3 | Gumbel location 80, scale 20 | Independent |
| Correlated minimum | Weibull scale 50, shape 1 | Weibull scale 80, shape 3 | Gaussian correlation fixed at 0.6 |

The tests deliberately require both components to leave recognizable information in the observed
sample. Before fitting, each must control at least 15% of outcomes in theory, win at least 100 of
the generated comparisons, and receive at least 100 observations when fractional likelihood
memberships are summed. Each must dominate at least 10% of the combined probability range, and
the dominant component must change within nonexceedance probabilities 0.10 to 0.90. This change
produces the bend sometimes called a *dog leg* in the combined frequency curve.

| Design | Theoretical shares, first/second | Observed winning counts, first/second | Probability at the interior change in dominance |
|---|---|---|---:|
| Independent minimum | 72.7% / 27.3% | 720 / 280 | 0.790 |
| Independent maximum | 48.7% / 51.3% | 495 / 505 | 0.474 |
| Correlated minimum | 78.7% / 21.3% | 776 / 224 | 0.827 |

Winning counts are known from the synthetic generation. The fitted model receives only the scalar
minimum or maximum, so it must infer component parameters without those labels. The count and
dominance checks establish suitability of the experiment; they do not replace parameter recovery.

### Estimation and comparison procedure

1. Generate the observed sample, establish the identification checks above, and verify the
   minimum/maximum convention, dependence, parameter ordering, and prior support.
2. Estimate all component parameters by maximum likelihood (MLE). For two Weibulls, order their
   parameters by increasing shape. Calculate uncertainty from the curvature of the complete
   competing-risk likelihood, without adding an artificial term to make the matrix invertible.
3. Require every generating parameter to be within 1.96 estimated standard errors of its MLE.
   Also check the combined distribution at nonexceedance probabilities 0.10, 0.25, 0.50, 0.75, and 0.90.
4. For the two independent designs, run Bayesian estimation with the standard DEMCzs settings.
   Require every ordered generating parameter inside its central 95% posterior interval,
   $\widehat R<1.10$, and ESS at least 100. Check the same combined-response probabilities.

### Estimation results

| Design | MLE | Bayesian analysis |
|---|---|---|
| Independent minimum | Passed | Passed |
| Independent maximum | Passed | Passed with the optional Jeffreys scale multiplier disabled |
| Correlated minimum | Passed | Not tested |

All five recovery comparisons passed. The independent maximum provides a numerical example:

| Parameter | Generating value | BestFit MLE, rounded |
|---|---:|---:|
| Weibull scale | 100 | 102.391 |
| Weibull shape | 3 | 3.219 |
| Gumbel location | 80 | 78.491 |
| Gumbel scale | 20 | 19.600 |

These fitted values satisfied the full-likelihood standard-error rule. Equivalent fitted-parameter
and interval tables for the other recovery comparisons, and the realized simulation statistics,
are not available for tabulation.

### Prior sensitivity and interpretation

The independent maximum is a material boundary case. With the optional Jeffreys scale multiplier
enabled, the comparison selected a Weibull scale near $1.11\times10^{-16}$; the component
effectively disappeared from the maximum. Its 95% interval, approximately
$[4.44\times10^{-12},38.17]$, excluded the generating scale 100. That configuration failed recovery. The
passing Bayesian experiment disables the optional multiplier while retaining bounded parameter
priors, the generating sample, and the standard sampler settings. It does not establish recovery
under the default scale multiplier.

The maximum also has a second dominance change at probability 0.987, where the Gumbel again controls
the extreme tail. This does not replace its required interior change at 0.474.
Balanced component contributions alone do not guarantee identifiable parameters outside the
specified recovery designs.

The results support these specified two-component models and four simulation dependence settings.
They do not establish arbitrary-component recovery or correlated Bayesian recovery. Agreement with
a separate Numerics estimator is useful corroboration but is not independent scientific evidence.
[Supporting calculations](../competing-risks.md) provide the dependence comparisons, fitted values,
and prior-sensitivity results.
