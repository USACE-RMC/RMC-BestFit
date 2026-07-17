# Model Estimation

[<- Previous: Composite Distributions](../distributions/composite.md) | [Back to Index](../../index.md) | [Next: Diagnostics ->](diagnostics.md)

Parameter estimation is the process of finding the values that make a model best explain observed data. ***RMC-BestFit*** is **Bayesian estimation software first and foremost**, providing full uncertainty quantification through Markov Chain Monte Carlo (MCMC) sampling. It also supports Maximum Likelihood Estimation (MLE) for point estimates and the Generalized Method of Moments (GMM) for specific applications.

This chapter provides in-depth coverage of all estimation methods available in ***RMC-BestFit***, including the mathematical foundations, practical implementation, convergence diagnostics, and model comparison techniques.

## Why Estimation Method Matters

Consider fitting a Generalized Extreme Value (GEV) distribution to 50 years of annual peak flows. Different estimation methods give different information:

| Method | Output | Use Case |
|--------|--------|----------|
| **MLE** | Point estimates, standard errors | Quick analysis, operational forecasting |
| **Bayesian MCMC** | Full posterior distributions | Uncertainty quantification, decision-making under uncertainty |
| **GMM** | Point estimates, moment conditions | Robust to distributional assumptions |
| **MAP** | Posterior mode | Regularized point estimate with prior information |

For life-safety applications like dam and levee risk assessment, **Bayesian MCMC is the recommended approach** because it properly propagates parameter uncertainty to design quantiles and risk estimates.

---

## Maximum Likelihood Estimation (MLE)

### Theoretical Foundation

Maximum Likelihood Estimation finds the parameter values $\hat{\theta}$ that maximize the probability of observing the data:

```math
\hat{\theta}_{\text{MLE}} = \arg\max_{\theta} \mathcal{L}(\theta | \mathbf{y}) = \arg\max_{\theta} \prod_{i=1}^{n} f(y_i | \theta)
```

Equivalently, we maximize the log-likelihood:

```math
\hat{\theta}_{\text{MLE}} = \arg\max_{\theta} \ell(\theta) = \arg\max_{\theta} \sum_{i=1}^{n} \log f(y_i | \theta)
```

### Properties of MLE

Under regularity conditions, MLE has desirable asymptotic properties:

1. **Consistency**: $\hat{\theta}_{\text{MLE}} \xrightarrow{p} \theta_0$ as $n \to \infty$
2. **Asymptotic Normality**: $\sqrt{n}(\hat{\theta}_{\text{MLE}} - \theta_0) \xrightarrow{d} N(0, I^{-1}(\theta_0))$
3. **Efficiency**: Achieves the Cramér-Rao lower bound asymptotically

where $I(\theta)$ is the Fisher Information matrix:

```math
I(\theta) = -E\left[\frac{\partial^2 \ell(\theta)}{\partial \theta \partial \theta^T}\right]
```

### Implementation in RMC-BestFit

The `MaximumLikelihood` class provides MLE for any model implementing `IModel`:

```cs
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;
using Numerics.Mathematics.Optimization;

// Create a model (e.g., GEV distribution)
var df = new DataFrame();
df.ExactSeries = new ExactSeries(annualPeakFlows);
var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

// Configure MLE
var mle = new MaximumLikelihood(model);

// Choose optimization method
mle.OptimizerMethod = OptimizationMethod.NelderMead;           // Local optimizer (fast)
// mle.OptimizerMethod = OptimizationMethod.MultilevelSingleLinkage;  // Global optimizer (robust)
// mle.OptimizerMethod = OptimizationMethod.DifferentialEvolution;    // Global optimizer (very robust)

// Run estimation
mle.Estimate();

// Check results
if (mle.IsEstimated)
{
    Console.WriteLine("MLE Results:");
    Console.WriteLine($"  Log-likelihood: {mle.BestParameterSet.Fitness:F2}");

    for (int i = 0; i < model.Parameters.Count; i++)
    {
        Console.WriteLine($"  {model.Parameters[i].Name}: {mle.BestParameterSet.Values[i]:F4}");
    }

    // Apply estimates to model
    model.SetParameterValues(mle.BestParameterSet.Values);
}
```

### Optimization Methods

***RMC-BestFit*** provides several optimization algorithms through the Numerics library:

| Method | Type | Speed | Robustness | Recommended For |
|--------|------|-------|------------|-----------------|
| `NelderMead` | Local | Fast | Low | Well-behaved likelihoods |
| `BFGS` | Local | Fast | Medium | Smooth likelihoods |
| `Powell` | Local | Medium | Medium | Non-differentiable objectives |
| `DifferentialEvolution` | Global | Slow | High | Multimodal likelihoods |
| `MultilevelSingleLinkage` | Global | Medium | High | General use |
| `ParticleSwarm` | Global | Medium | Medium | High-dimensional problems |

**Recommendation**: Use `MultilevelSingleLinkage` for most applications. It balances computational cost with robustness to local optima.

### Handling Multiple Local Optima

Many hydrologic models have multimodal likelihoods. For example, rating curves with the power-law form $Q = \alpha(h - \xi)^\beta$ have parameter trade-offs: different combinations of $(\xi, \alpha, \beta)$ can produce similar fits.

To increase confidence in finding the global optimum:

```cs
// Run multiple optimizations from different starting points
var bestFitness = double.NegativeInfinity;
ParameterSet bestParams = default;

for (int trial = 0; trial < 10; trial++)
{
    // Random starting point within bounds
    var random = new Random(trial);
    var startValues = new double[model.Parameters.Count];
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        var p = model.Parameters[i];
        startValues[i] = p.LowerBound + random.NextDouble() * (p.UpperBound - p.LowerBound);
    }
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        model.Parameters[i].Value = startValues[i];
    }

    var mle = new MaximumLikelihood(model, OptimizationMethod.NelderMead);
    mle.Estimate();

    if (mle.IsEstimated && mle.BestParameterSet.Fitness > bestFitness)
    {
        bestFitness = mle.BestParameterSet.Fitness;
        bestParams = mle.BestParameterSet;
    }
}

Console.WriteLine($"Best log-likelihood across trials: {bestFitness:F2}");
```

---

## Bayesian MCMC Estimation

### Theoretical Foundation

Bayesian inference treats parameters as random variables with probability distributions. We start with a **prior distribution** $\pi(\theta)$ encoding our beliefs before seeing data, then update to a **posterior distribution** after observing data $\mathbf{y}$:

```math
\pi(\theta | \mathbf{y}) = \frac{\mathcal{L}(\mathbf{y} | \theta) \cdot \pi(\theta)}{\int \mathcal{L}(\mathbf{y} | \theta) \cdot \pi(\theta) \, d\theta}
```

This is **Bayes' theorem**. The denominator (marginal likelihood or evidence) is typically intractable, but MCMC methods sample from the posterior without computing it.

### Why Bayesian?

Bayesian estimation offers several advantages for hydrologic applications:

1. **Full Uncertainty Quantification**: Get probability distributions, not just point estimates
2. **Natural Framework for Prediction**: Predictive distributions integrate over parameter uncertainty
3. **Incorporation of Prior Information**: Engineering judgment can inform priors
4. **Coherent Decision-Making**: Posterior distributions support risk-based decisions
5. **Flexible Model Comparison**: WAIC, LOO-CV, and Bayes factors

### The DEMCzs Sampler

***RMC-BestFit*** uses the **Differential Evolution Markov Chain with snooker update (DEMCzs)** algorithm [[1]](#1) as its primary MCMC sampler. DEMCzs is particularly well-suited for:

- **High-dimensional problems**: Efficient even with 10+ parameters
- **Correlated parameters**: Adapts proposal scale and direction automatically
- **Multimodal posteriors**: Multiple chains can explore different modes

The algorithm maintains $N$ chains (typically $N = 3$) that evolve in parallel. Each chain proposes new states using differences between other chains:

```math
\theta^* = \theta^{(i)} + \gamma \cdot (\theta^{(r_1)} - \theta^{(r_2)}) + \varepsilon
```

where $r_1, r_2$ are randomly selected chains and $\gamma$ is a scaling factor.

### Implementation in RMC-BestFit

The `BayesianAnalysis` class provides MCMC estimation:

```cs
using RMC.BestFit.Estimation;
using RMC.BestFit.Models;

// Create and configure model
var df = new DataFrame();
df.ExactSeries = new ExactSeries(annualPeakFlows);
var model = new UnivariateDistribution(df, UnivariateDistributionType.GeneralizedExtremeValue);

// Configure Bayesian analysis
var bayesian = new BayesianAnalysis(model);

// Sampling parameters
bayesian.Type = BayesianAnalysis.SamplerType.DEMCzs;  // Default sampler
bayesian.Iterations = 10000;           // Total iterations after warmup
bayesian.WarmupIterations = 5000;      // Burn-in period (discarded)
bayesian.ThinningInterval = 1;         // Keep every nth sample (1 = no thinning)

// Run MCMC
await bayesian.RunAsync();

// Access results
var results = bayesian.Results;

Console.WriteLine("Posterior Summary:");
Console.WriteLine($"{"Parameter",-20} {"Mean",10} {"SD",10} {"2.5%",10} {"97.5%",10} {"R-hat",8} {"ESS",8}");
Console.WriteLine(new string('-', 78));

for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    Console.WriteLine($"{model.Parameters[i].Name,-20} {stats.Mean,10:F3} {stats.StandardDeviation,10:F3} " +
                      $"{stats.LowerCI,10:F3} {stats.UpperCI,10:F3} {stats.Rhat,8:F3} {stats.ESS,8:F0}");
}
```

### Prior Distributions

Prior distributions encode beliefs about parameters before seeing data. ***RMC-BestFit*** supports several approaches:

#### Flat (Uniform) Priors

Non-informative within bounds:

```cs
model.Parameters[0].PriorDistribution = new Uniform(lowerBound, upperBound);
```

#### Weakly Informative Priors

Regularize extreme values without strong assumptions:

```cs
// Normal prior centered at expected value with wide standard deviation
model.Parameters[0].PriorDistribution = new Normal(expectedValue, 10 * expectedSD);
```

#### Informative Priors

Incorporate engineering knowledge:

```cs
// Prior based on regional analysis or expert judgment
model.Parameters[0].PriorDistribution = new Normal(regionalEstimate, regionalUncertainty);
```

#### Jeffreys' Prior for Scale Parameters

For scale parameters (standard deviation, etc.), Jeffreys' prior is non-informative:

```math
\pi(\sigma) \propto \frac{1}{\sigma}
```

This is the default when `UseJeffreysRuleForScale = true`:

```cs
bayesian.UseJeffreysRuleForScale = true;  // Default
```

### Quantile Priors

A unique feature of ***RMC-BestFit*** is support for **quantile priors** — incorporating expert judgment about specific return levels:

```cs
// Add prior information: "The 100-year flood is probably between 50,000 and 80,000 cfs"
var qPrior = new QuantilePrior();
qPrior.ReturnPeriod = 100;
qPrior.Distribution = new Normal(65000, 7500);  // Mean 65,000, SD 7,500

model.QuantilePriors.Add(qPrior);
```

Quantile priors are particularly valuable when:
- Historical or paleoflood information suggests a range for extreme quantiles
- Regional studies provide guidance on expected flood magnitudes
- Engineering judgment bounds the plausible range of design values

### MCMC Diagnostics

Successful MCMC requires verifying that chains have converged to the target distribution.

#### R-hat (Potential Scale Reduction Factor)

R-hat compares within-chain and between-chain variance [[2]](#2):

```math
\hat{R} = \sqrt{\frac{\hat{\text{Var}}(\theta | \mathbf{y})}{W}}
```

where $W$ is the within-chain variance. **Rule of thumb**: $\hat{R} < 1.1$ indicates convergence.

```cs
var rhat = results.ParameterResults[i].SummaryStatistics.Rhat;
if (rhat > 1.1)
{
    Console.WriteLine($"Warning: {model.Parameters[i].Name} has R-hat = {rhat:F3}. Consider more iterations.");
}
```

#### Effective Sample Size (ESS)

ESS measures the number of independent samples accounting for autocorrelation:

```math
\text{ESS} = \frac{nm}{1 + 2\sum_{k=1}^{\infty} \rho_k}
```

where $\rho_k$ is the lag-$k$ autocorrelation. **Rule of thumb**: ESS > 400 for reliable posterior summaries.

```cs
var ess = results.ParameterResults[i].SummaryStatistics.ESS;
if (ess < 400)
{
    Console.WriteLine($"Warning: {model.Parameters[i].Name} has ESS = {ess:F0}. Consider more iterations or thinning.");
}
```

#### Visual Diagnostics

Trace plots and density plots help diagnose convergence:

```cs
// Access raw samples for plotting
var samples = results.Output;  // List<ParameterSet>

// Extract parameter values for custom visualization
var parameterSamples = new double[samples.Count];
for (int j = 0; j < samples.Count; j++)
{
    parameterSamples[j] = samples[j].Values[parameterIndex];
}

// Use your preferred plotting library (e.g., ScottPlot, OxyPlot)
```

---

## Maximum A Posteriori (MAP) Estimation

MAP estimation finds the mode of the posterior distribution:

```math
\hat{\theta}_{\text{MAP}} = \arg\max_{\theta} \pi(\theta | \mathbf{y}) = \arg\max_{\theta} \left[ \ell(\theta) + \log \pi(\theta) \right]
```

MAP is a regularized version of MLE — the prior acts as a penalty on extreme parameter values.

```cs
using RMC.BestFit.Estimation;

var map = new MaximumAPosteriori(model);
map.Estimate();

if (map.IsEstimated)
{
    Console.WriteLine("MAP Estimates:");
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        Console.WriteLine($"  {model.Parameters[i].Name}: {map.BestParameterSet.Values[i]:F4}");
    }
}
```

**When to use MAP**:
- Quick point estimate with prior regularization
- Starting point for MCMC
- When full posterior isn't needed

---

## Generalized Method of Moments (GMM)

GMM estimates parameters by matching sample moments to theoretical moments:

```math
\hat{\theta}_{\text{GMM}} = \arg\min_{\theta} g(\theta)^T W g(\theta)
```

where $g(\theta)$ is a vector of moment conditions and $W$ is a weighting matrix.

```cs
using RMC.BestFit.Estimation;

var gmm = new GeneralizedMethodOfMoments(model)
{
    EstimationStrategy = GeneralizedMethodOfMoments.GMMEstimationStrategy.Iterative,
    MaxGMMIterations = 100
};

if (gmm.Estimate())
{
    Console.WriteLine("GMM Estimates:");
    for (int i = 0; i < model.Parameters.Count; i++)
    {
        Console.WriteLine($"  {model.Parameters[i].Name}: {gmm.BestParameterSet.Values[i]:F4}");
    }

    Console.WriteLine($"Optimization passes: {gmm.GMMIterations}");
    Console.WriteLine($"Confirmed converged: {gmm.ConvergedWithinTolerance}");
}
```

For iterative GMM, `GMMIterations` counts attempted optimization passes from 1 through
`MaxGMMIterations` and never exceeds the configured limit. `ConvergedWithinTolerance` is true
only when an iterative comparison pass satisfies the absolute parameter-distance or relative
objective-change criterion, including convergence on the final permitted pass. It remains false
for iteration exhaustion, optimizer failure, one-step and two-step strategies, and a one-pass run
that has no comparison. The confirmed state is persisted in GMM XML; legacy XML without the
attribute restores conservatively as not confirmed converged.

**Advantages of GMM**:
- Robust to distributional misspecification
- Only requires moment assumptions, not full likelihood
- Efficient two-step estimator available

**Note**: RMC-BestFit uses GMM only for specific applications. **MLE or Bayesian MCMC are preferred** for most analyses.

---

## Model Comparison

After fitting multiple models, we need criteria to select among them.

### Information Criteria

#### Akaike Information Criterion (AIC)

```math
\text{AIC} = -2\ell(\hat{\theta}) + 2k
```

where $k$ is the number of parameters. Lower AIC is better.

#### Bayesian Information Criterion (BIC)

```math
\text{BIC} = -2\ell(\hat{\theta}) + k \log(n)
```

BIC penalizes complexity more heavily than AIC for $n > 7$.

```cs
// Compute AIC/BIC after MLE
double logLik = mle.BestParameterSet.Fitness;
int k = model.Parameters.Count(p => !p.IsFixed);
int n = df.ExactSeries.Count;

double aic = -2 * logLik + 2 * k;
double bic = -2 * logLik + k * Math.Log(n);

Console.WriteLine($"AIC: {aic:F2}");
Console.WriteLine($"BIC: {bic:F2}");
```

### Deviance Information Criterion (DIC)

DIC is a Bayesian alternative to AIC:

```math
\text{DIC} = \bar{D} + p_D
```

where $\bar{D}$ is the posterior mean deviance and $p_D$ is the effective number of parameters.

### WAIC and LOO-CV

For fully Bayesian model comparison, ***RMC-BestFit*** supports:

- **WAIC** (Widely Applicable Information Criterion): Uses pointwise predictive densities
- **LOO-CV** (Leave-One-Out Cross-Validation) with PSIS (Pareto-Smoothed Importance Sampling)

These are computed from MCMC output using the `PointwiseDataLogLikelihood` method.

---

## Practical Recommendations

### Choosing an Estimation Method

| Situation | Recommended Method |
|-----------|-------------------|
| Quick exploratory analysis | MLE |
| Uncertainty in design values matters | Bayesian MCMC |
| Prior information available | Bayesian MCMC |
| Very large dataset (n > 10,000) | MLE, then Bayesian if needed |
| Model selection among many candidates | MLE with AIC/BIC, then Bayesian for finalist |
| Life-safety decision | Bayesian MCMC (mandatory) |

### MCMC Settings

| Parameter | Typical Value | Guidance |
|-----------|---------------|----------|
| `Iterations` | 10,000-50,000 | More for complex models |
| `WarmupIterations` | 50% of Iterations | Allow chains to find high-probability region |
| `ThinningInterval` | 1-10 | Increase if storage is limited |
| Chains | 3+ | Minimum 3 for R-hat computation |

### Troubleshooting Convergence

| Problem | Symptom | Solution |
|---------|---------|----------|
| Slow mixing | High autocorrelation, low ESS | More iterations, reparameterize |
| Non-convergence | R-hat > 1.1 | Longer warmup, better initialization |
| Multimodality | Chains stuck in different regions | Use DEMCzs, more chains |
| Numerical issues | NaN in likelihood | Check parameter bounds, transforms |

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Estimation/BayesianAnalysis.cs`, `src/RMC.BestFit/Estimation/MaximumLikelihood.cs`, `src/RMC.BestFit/Estimation/MaximumAPosteriori.cs`, `src/RMC.BestFit/Estimation/GeneralizedMethodOfMoments.cs`, `src/RMC.BestFit/Estimation/NumericalDiff.cs`, and `src/RMC.BestFit/Models/Support/IModel.cs`.

---

## References

<a id="1">[1]</a>
ter Braak, C.J.F. and Vrugt, J.A. (2008). "Differential Evolution Markov Chain with snooker updater and fewer chains." *Statistics and Computing*, 18(4), 435-446.

<a id="2">[2]</a>
Gelman, A. and Rubin, D.B. (1992). "Inference from iterative simulation using multiple sequences." *Statistical Science*, 7(4), 457-472.

<a id="3">[3]</a>
Vehtari, A., Gelman, A., and Gabry, J. (2017). "Practical Bayesian model evaluation using leave-one-out cross-validation and WAIC." *Statistics and Computing*, 27(5), 1413-1432.

<a id="4">[4]</a>
Spiegelhalter, D.J., Best, N.G., Carlin, B.P., and van der Linde, A. (2002). "Bayesian measures of model complexity and fit." *Journal of the Royal Statistical Society: Series B*, 64(4), 583-639.

<a id="5">[5]</a>
Jeffreys, H. (1946). "An invariant form for the prior probability in estimation problems." *Proceedings of the Royal Society A*, 186, 453-461.

---

[<- Previous: Composite Distributions](../distributions/composite.md) | [Back to Index](../../index.md) | [Next: Diagnostics ->](diagnostics.md)
