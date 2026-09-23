<!-- verification-status: publication-draft -->

# Mixture Analysis

## Model, identification, and acceptance

A mixture represents observations arising from different populations when the population responsible
for each observation is unknown. For example, a sample may combine two flood-generating mechanisms.
Each component has its own distribution, and its weight is the proportion of observations expected
from that population. An observation comes from one component; the mixture does not add the component
values or select their maximum. These tests use synthetic, unitless observations so the generating
populations and proportions are known exactly.

The verification asks whether BestFit can recover those proportions, component means, and component
standard deviations. It checks two estimation approaches. Expectation-maximization (EM) alternates
between estimating each observation's fractional membership and updating the component parameters.
Bayesian analysis represents uncertainty by sampling plausible weights and component parameters.

### Generating populations

Each design contains exactly 1,000 scalar observations generated with seed 12345. The table gives
the **generating values**, not fitted BestFit results. Expected counts describe long-run proportions;
the realized counts vary with the generated sample.

| Design and component | Generating mean | Generating standard deviation | Share of all observations | Expected count in 1,000 |
|---|---:|---:|---:|---:|
| Ordinary two-component: lower-mean Normal | 0 | 1 | 30% | 300 |
| Ordinary two-component: higher-mean Normal | 3 | 0.1 | 70% | 700 |
| Zero-inflated: exact zero | Not applicable | Not applicable | 10% | 100 |
| Zero-inflated: lower-mean Normal, restricted to positive values | 3 | 0.1 | 30% | 300 |
| Zero-inflated: higher-mean Normal, restricted to positive values | 5 | 2 | 60% | 600 |
| Ordinary three-component: first Normal | 0 | 1 | 20% | 200 |
| Ordinary three-component: second Normal | 3 | 0.1 | 30% | 300 |
| Ordinary three-component: third Normal | 5 | 2 | 50% | 500 |

The zero-inflated design has a separate probability of an exact zero and a continuous distribution
for positive observations. Its listed Normal means and standard deviations describe the component
distributions before conditioning on positive values. It therefore tests a positive *hurdle* model,
not an ordinary Normal mixture with some zeros appended. BestFit sets the zero probability to the
observed fraction of exact zeros. The generating 10% probability is checked against that fraction
using binomial sampling uncertainty; it is not sampled as a Bayesian parameter.

### Procedure

1. Generate each sample, construct a fresh mixture with the correct component families, and check
   that the generating parameters lie within the configured priors and explain the sample better
   than the initial collapsed components.
2. Fit the three samples by EM. Order components by increasing mean so that exchanging component
   labels cannot create an apparent recovery error. Compare each weight, mean, and standard
   deviation with its generating value using its estimated standard error.
3. Fit the same designs by Bayesian analysis with the standard DEMCzs simulation settings and the
   predeclared sampler seeds 22345, 32345, and 42345 for the two-component, hurdle, and
   three-component designs, respectively.
4. Reconstruct all component weights in every retained draw, order the components by mean, and
   compare the generating values with the central 95% posterior intervals. Check sampler diagnostics
   for every sampled parameter.

The weights must sum to one, or to the observed positive fraction in the hurdle model. Consequently,
only the first $K-1$ weights are sampled for $K$ continuous components; the last is the remaining mass.
For the hurdle comparison, the final generating weight is expressed on that same observed positive
mass. This separates sampling variation in the zero count from estimation of the positive components.

EM acceptance requires the absolute estimation error divided by its standard error to be no greater
than 1.96 for every coordinate. Weight uncertainty uses fractional membership counts; component
uncertainty uses the curvature of the observed likelihood. Bayesian acceptance requires all ordered
generating coordinates inside their central 95% intervals, chain agreement $\widehat R<1.10$, and
effective sample size (ESS) at least 100 for each sampled coordinate.

## Recovery results

| Design | EM result | Bayesian result | Meaning of the pass |
|---|---|---|---|
| Ordinary two-component Normal mixture | Passed | Passed | Both component proportions, means, and standard deviations satisfied their respective uncertainty rules |
| Positive-hurdle two-component Normal mixture | Passed | Passed | The zero frequency satisfied its binomial rule; positive-component parameters satisfied their respective uncertainty rules |
| Ordinary three-component Normal mixture | Passed | Passed | All three ordered components satisfied their respective uncertainty rules |

All six comparisons passed their stated uncertainty rules. Recovery allows fitted values to differ
from their generating values by an amount consistent with the estimated uncertainty.

## External evidence, limitations, and provenance

A separate comparison uses an independently generated sample of 1,000 observations from the ordinary
two-component parent above. NumPy's PCG64 generator uses seed 12345; this is a different sample from
the BestFit-generated recovery sample despite the matching seed number. The reference fit is from
scikit-learn 1.9.0, with Python 3.12.13, NumPy 2.5.2, and SciPy 1.18.1. Its diagonal variances are
converted to standard deviations before comparison.

| Quantity | Frozen independent reference | BestFit comparison |
|---|---:|---|
| Lower-mean component weight | 0.304407173 | Within the scaled coordinate tolerance |
| Lower-mean component mean | 0.053369416 | Within the scaled coordinate tolerance |
| Lower-mean component standard deviation | 1.042725781 | Within the scaled coordinate tolerance |
| Higher-mean component weight | 0.695592827 | Within the scaled coordinate tolerance |
| Higher-mean component mean | 3.002147278 | Within the scaled coordinate tolerance |
| Higher-mean component standard deviation | 0.099250007 | Within the scaled coordinate tolerance |
| Log likelihood at the generating parameters | -429.767425439 | Agreement within $10^{-9}$ |
| Log likelihood at the scikit-learn fitted parameters | -428.760759412 | Agreement within $10^{-9}$ |
| Fitted probability of an observation at or below 3 | 0.645482673 | Agreement within $10^{-12}$ |

The procedure first evaluates the likelihood and seven cumulative probabilities at the fixed parent
and scikit-learn parameters, then runs BestFit EM and compares the six fitted coordinates. Each
coordinate tolerance is $10^{-4}\max(1,|\text{reference}|)$. This comparison passed.
The available results do not tabulate BestFit's fitted coordinates, recovery interval endpoints,
or observed zero count; the numerical reference column contains scikit-learn/SciPy values.

Together, recovery and the external comparison support the stated two- and three-component designs.
They do not establish recovery for arbitrary overlapping populations or all possible prior choices.
scikit-learn does not implement the positive-conditioned hurdle law, so no external-package agreement
is claimed for that design. [Supporting calculations](../mixture.md) and the
[external numerical reference](../../../verification/data/mixture/normal-mixture-sklearn-oracle.json)
provide the detailed comparison.
