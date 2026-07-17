# Models Overview

[<- Previous: Getting Started](../../getting-started.md) | [Back to Index](../../index.md) | [Next: Input Data Frame ->](../data-frame/index.md)

In ***RMC-BestFit***, a **model** is any mathematical representation of a physical process that can be fitted to data. Models define the relationship between parameters and observations, specify prior distributions for Bayesian inference, and compute likelihood functions that measure how well parameter values explain the data.

This chapter introduces the model architecture in ***RMC-BestFit***, describes the core interfaces that all models implement, surveys the pre-built models available in the library, and demonstrates how to create custom models for specialized applications.

## Why Models Matter

Statistical analysis begins with a model — an assumption about how nature generates the data we observe. A flood frequency analyst might assume annual peak flows follow a Log-Pearson Type III distribution. A rating curve developer assumes discharge is a power function of stage. A regional hydrologist might assume extreme value parameters vary smoothly with elevation and distance from the coast.

The choice of model determines:
- **What questions we can answer**: A stationary model cannot detect trends; a univariate model cannot capture dependence between variables
- **What parameters we estimate**: Each model has its own set of interpretable parameters
- **How uncertainty propagates**: The model structure determines how parameter uncertainty translates to predictive uncertainty
- **What data are required**: Complex models need more data to estimate reliably

***RMC-BestFit*** provides a rich library of pre-built models for common hydrologic applications, along with a flexible framework for creating custom models when the built-in options don't fit your needs.

## The Model Architecture

### The `IModel` Interface

All models in ***RMC-BestFit*** implement the `IModel` interface, which defines the contract for Bayesian inference:

```cs
public interface IModel
{
    /// <summary>
    /// Gets the list of model parameters with their prior distributions.
    /// </summary>
    List<ModelParameter> Parameters { get; }

    /// <summary>
    /// Computes the log-posterior: log-likelihood plus log-prior.
    /// </summary>
    double LogLikelihood(double[] parameters);

    /// <summary>
    /// Computes only the data log-likelihood (excludes priors).
    /// </summary>
    double DataLogLikelihood(double[] parameters);

    /// <summary>
    /// Computes only the log-prior.
    /// </summary>
    double PriorLogLikelihood(double[] parameters);

    /// <summary>
    /// Computes pointwise log-likelihood for each observation.
    /// Required for WAIC and LOO-CV diagnostics.
    /// </summary>
    double[] PointwiseDataLogLikelihood(double[] parameters);

    /// <summary>
    /// Computes pointwise log-prior for each parameter.
    /// </summary>
    double[] PointwisePriorLogLikelihood(double[] parameters);
}
```

The distinction between `LogLikelihood` and `DataLogLikelihood` is crucial for Bayesian inference:

- **`LogLikelihood`** returns $\log p(\text{data} | \theta) + \log \pi(\theta)$ — the unnormalized log-posterior. This is what MCMC samplers maximize.

- **`DataLogLikelihood`** returns only $\log p(\text{data} | \theta)$ — the likelihood contribution. This is used for computing DIC, WAIC, and other model comparison criteria.

- **`PriorLogLikelihood`** returns $\log \pi(\theta)$ — the prior contribution. Useful for prior predictive checks.

### The `ModelParameter` Class

Each parameter in a model is represented by a `ModelParameter` object that encapsulates:

```cs
public class ModelParameter : INotifyPropertyChanged
{
    /// <summary>
    /// The parameter name (for display and serialization).
    /// </summary>
    public string Name { get; set; }

    /// <summary>
    /// The current parameter value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The prior distribution for Bayesian inference.
    /// </summary>
    public IUnivariateDistribution PriorDistribution { get; set; }

    /// <summary>
    /// Lower bound for optimization and sampling.
    /// </summary>
    public double LowerBound { get; set; }

    /// <summary>
    /// Upper bound for optimization and sampling.
    /// </summary>
    public double UpperBound { get; set; }

    /// <summary>
    /// Whether the parameter is fixed (not estimated).
    /// </summary>
    public bool IsFixed { get; set; }

    /// <summary>
    /// The parameter transform for MCMC sampling.
    /// </summary>
    public ParameterTransform Transform { get; set; }
}
```

**Key features:**

- **Prior distributions**: Any `IUnivariateDistribution` can serve as a prior — Normal, Uniform, Gamma, etc.
- **Bounds**: Physical constraints (e.g., variance must be positive) are enforced via bounds
- **Fixed parameters**: Set `IsFixed = true` to hold a parameter constant during estimation
- **Transforms**: Parameters can be sampled on transformed scales (log, logit) for better MCMC mixing

### Parameter Transforms

MCMC sampling works best when parameters are unbounded and roughly symmetric. The `ParameterTransform` enum provides common transformations:

| Transform | Forward | Inverse | Use Case |
|-----------|---------|---------|----------|
| `None` | $\theta$ | $\theta$ | Unbounded location parameters |
| `Log` | $\log(\theta)$ | $\exp(\phi)$ | Positive scale parameters |
| `Logit` | $\text{logit}(\theta)$ | $\text{logit}^{-1}(\phi)$ | Parameters bounded in (0,1) |
| `Asinh` | $\text{asinh}(\theta)$ | $\text{sinh}(\phi)$ | Heavy-tailed or near-zero parameters |

When a transform is specified, MCMC sampling occurs in the transformed space $\phi$, and the Jacobian of the transformation is automatically included in the likelihood.

---

## Pre-Built Models

***RMC-BestFit*** includes a comprehensive library of models for hydrologic and statistical applications.

### Univariate Distributions

The library supports 15 univariate probability distributions commonly used in flood frequency analysis:

| Distribution | Class | Parameters | Typical Application |
|--------------|-------|------------|---------------------|
| Normal | `Normal` | $\mu, \sigma$ | Symmetric data, transformed flows |
| Log-Normal | `LogNormal` | $\mu, \sigma$ | Right-skewed flows, rainfall |
| Ln-Normal | `LnNormal` | $\mu_{\ln}, \sigma_{\ln}$ | Alternative log-normal parameterization |
| Exponential | `Exponential` | $\xi, \alpha$ | Peaks-over-threshold excesses |
| Gamma | `GammaDistribution` | $\xi, \alpha, \beta$ | Precipitation depths |
| Gumbel | `Gumbel` | $\xi, \alpha$ | Annual maxima (light upper tail) |
| **GEV** | `GeneralizedExtremeValue` | $\xi, \alpha, \kappa$ | Annual maxima (flexible tail) |
| Generalized Logistic | `GeneralizedLogistic` | $\xi, \alpha, \kappa$ | UK flood frequency standard |
| Generalized Normal | `GeneralizedNormal` | $\xi, \alpha, \kappa$ | Symmetric with flexible kurtosis |
| **Generalized Pareto** | `GeneralizedPareto` | $\xi, \alpha, \kappa$ | Peaks-over-threshold |
| Logistic | `Logistic` | $\xi, \alpha$ | Growth curves, bounded data |
| Kappa Four | `KappaFour` | $\xi, \alpha, \kappa, h$ | Very flexible 4-parameter family |
| Pearson Type III | `PearsonTypeIII` | $\mu, \sigma, \gamma$ | General skewed data |
| **Log-Pearson Type III** | `LogPearsonTypeIII` | $\mu, \sigma, \gamma$ | Bulletin 17C standard |
| Weibull | `Weibull` | $\xi, \alpha, \kappa$ | Minima, low flows, wind speeds |

The distributions in **bold** are most commonly used in flood frequency analysis.

### Distribution Extensions

Beyond simple univariate fitting, ***RMC-BestFit*** supports:

| Model Type | Class | Description |
|------------|-------|-------------|
| **Mixture** | `MixtureModel` | Two-population models (e.g., rain vs. snowmelt floods) |
| **Competing Risks** | `CompetingRisksModel` | Annual maximum of multiple processes |
| **Nonstationary** | `UnivariateDistribution` with trends | Time-varying parameters |
| **Peaks-Over-Threshold** | `PointProcessModel` | Exceedances above threshold |

### Time Series Models

For temporal dependence modeling:

| Model | Class | Order | Description |
|-------|-------|-------|-------------|
| AR(p) | `AutoRegressive` | $p$ | Autoregressive |
| MA(q) | `MovingAverage` | $q$ | Moving average |
| ARIMA(p,d,q) | `ARIMA` | $p, d, q$ | Integrated ARMA |
| ARIMAX | `ARIMAX` | $p, d, q$ + covariates | ARIMA with exogenous variables |

### Rating Curves

Stage-discharge relationships:

| Model | Class | Segments | Parameters |
|-------|-------|----------|------------|
| Power Law | `RatingCurve` | 1 | $\xi, \log_{10}\alpha, \beta, \sigma$ |
| Two-Segment | `RatingCurve` | 2 | + $h_2, \log_{10}\alpha_2, \beta_2$ |
| Three-Segment | `RatingCurve` | 3 | + $h_3, \log_{10}\alpha_3, \beta_3$ |

### Spatial Models

For regional frequency analysis:

| Model | Class | Description |
|-------|-------|-------------|
| Spatial GEV | `SpatialGEV` | GEV with spatially-varying parameters |
| Gaussian Copula | `GaussianCopula` | Spatial dependence structure |

### Bivariate Distributions

For joint analysis of two variables:

| Model | Class | Description |
|-------|-------|-------------|
| Bivariate | `BivariateDistribution` | Copula-based joint distribution |
| Coincident Frequency | `CoincidentFrequencyAnalysis` | Joint exceedance probabilities |

---

## Creating Custom Models

When the pre-built models don't fit your needs, you can create custom models by implementing the `IModel` interface.

### Example: Custom Regression Model

Consider a simple linear regression model: $y_i = \alpha + \beta x_i + \varepsilon_i$ where $\varepsilon_i \sim N(0, \sigma^2)$.

```cs
using Numerics.Distributions;
using RMC.BestFit;
using RMC.BestFit.Models;

public class LinearRegressionModel : IModel
{
    private readonly double[] _x;
    private readonly double[] _y;

    public LinearRegressionModel(double[] x, double[] y)
    {
        _x = x ?? throw new ArgumentNullException(nameof(x));
        _y = y ?? throw new ArgumentNullException(nameof(y));

        if (x.Length != y.Length)
            throw new ArgumentException("x and y must have same length");

        // Initialize parameters with flat priors
        Parameters = new List<ModelParameter>
        {
            new ModelParameter
            {
                Name = "α (intercept)",
                Value = 0,
                LowerBound = -1000,
                UpperBound = 1000,
                PriorDistribution = new Uniform(-1000, 1000)
            },
            new ModelParameter
            {
                Name = "β (slope)",
                Value = 0,
                LowerBound = -100,
                UpperBound = 100,
                PriorDistribution = new Uniform(-100, 100)
            },
            new ModelParameter
            {
                Name = "σ (error SD)",
                Value = 1,
                LowerBound = 1e-6,
                UpperBound = 100,
                PriorDistribution = new Uniform(1e-6, 100),
                Transform = ParameterTransform.Log  // Sample on log scale
            }
        };
    }

    public List<ModelParameter> Parameters { get; }

    public double LogLikelihood(double[] parameters)
    {
        return DataLogLikelihood(parameters) + PriorLogLikelihood(parameters);
    }

    public double DataLogLikelihood(double[] parameters)
    {
        double alpha = parameters[0];
        double beta = parameters[1];
        double sigma = parameters[2];

        if (sigma <= 0) return double.NegativeInfinity;

        double logLik = 0;
        for (int i = 0; i < _x.Length; i++)
        {
            double predicted = alpha + beta * _x[i];
            double residual = _y[i] - predicted;
            logLik += Normal.StandardPDF(residual / sigma) - Math.Log(sigma);
        }
        return logLik;
    }

    public double PriorLogLikelihood(double[] parameters)
    {
        double logPrior = 0;
        for (int i = 0; i < Parameters.Count; i++)
        {
            if (!Parameters[i].IsFixed)
            {
                logPrior += Parameters[i].PriorDistribution.LogPDF(parameters[i]);
            }
        }
        return logPrior;
    }

    public double[] PointwiseDataLogLikelihood(double[] parameters)
    {
        double alpha = parameters[0];
        double beta = parameters[1];
        double sigma = parameters[2];

        var pointwise = new double[_x.Length];
        for (int i = 0; i < _x.Length; i++)
        {
            double predicted = alpha + beta * _x[i];
            double residual = _y[i] - predicted;
            pointwise[i] = Normal.StandardPDF(residual / sigma) - Math.Log(sigma);
        }
        return pointwise;
    }

    public double[] PointwisePriorLogLikelihood(double[] parameters)
    {
        var pointwise = new double[Parameters.Count];
        for (int i = 0; i < Parameters.Count; i++)
        {
            pointwise[i] = Parameters[i].IsFixed ? 0 :
                           Parameters[i].PriorDistribution.LogPDF(parameters[i]);
        }
        return pointwise;
    }
}
```

### Using the Custom Model

Once implemented, custom models work with all ***RMC-BestFit*** estimation methods:

```cs
// Create model with data
var model = new LinearRegressionModel(xData, yData);

// Maximum Likelihood Estimation
var mle = new MaximumLikelihood(model);
mle.Estimate();
Console.WriteLine($"MLE intercept: {mle.BestParameterSet.Values[0]:F3}");
Console.WriteLine($"MLE slope: {mle.BestParameterSet.Values[1]:F3}");

// Bayesian MCMC
var bayesian = new BayesianAnalysis(model);
bayesian.Iterations = 10000;
bayesian.WarmupIterations = 5000;
await bayesian.RunAsync();

var results = bayesian.Results;
Console.WriteLine($"Posterior mean intercept: {results.ParameterResults[0].SummaryStatistics.Mean:F3}");
Console.WriteLine($"Posterior mean slope: {results.ParameterResults[1].SummaryStatistics.Mean:F3}");
```

### Best Practices for Custom Models

1. **Return `-∞` for invalid parameters**: If parameters violate constraints (e.g., negative variance), return `double.NegativeInfinity` rather than throwing an exception.

2. **Use logarithmic computation**: When multiplying many probabilities, work in log-space to avoid numerical underflow.

3. **Guard against numerical issues**: Check for `NaN`, `Inf`, and division by zero.

4. **Implement pointwise likelihoods**: These are required for WAIC and LOO-CV model comparison.

5. **Use appropriate transforms**: Scale parameters should typically use `ParameterTransform.Log`.

6. **Document parameter interpretations**: Clear parameter names help users understand results.

---

## Model Hierarchy

The following diagram shows the inheritance relationships between model classes in ***RMC-BestFit***:

```
IModel
├── ModelBase (abstract)
│   ├── UnivariateDistributionBase
│   │   ├── Normal, LogNormal, GEV, etc.
│   │   ├── MixtureModel
│   │   ├── CompetingRisksModel
│   │   └── PointProcessModel
│   ├── TimeSeriesModelBase
│   │   ├── AutoRegressive
│   │   ├── MovingAverage
│   │   ├── ARMA → ARIMA → ARIMAX
│   │   └── ARMAX
│   ├── RatingCurve
│   ├── SpatialGEV
│   └── BivariateDistribution
```

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/Support/IModel.cs`, `src/RMC.BestFit/Models/Support/ModelBase.cs`, `src/RMC.BestFit/Models/Support/ModelParameter.cs`, `src/RMC.BestFit/Models/UnivariateDistribution`, `src/RMC.BestFit/Models/BivariateDistribution`, `src/RMC.BestFit/Models/RatingCurve`, `src/RMC.BestFit/Models/TimeSeries`, and `src/RMC.BestFit/Models/SpatialExtremes`.

---

## References

<a id="1">[1]</a>
Hosking, J.R.M. and Wallis, J.R. (1997). *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press.

<a id="2">[2]</a>
Coles, S. (2001). *An Introduction to Statistical Modeling of Extreme Values*. Springer.

<a id="3">[3]</a>
Gelman, A., Carlin, J.B., Stern, H.S., Dunson, D.B., Vehtari, A., and Rubin, D.B. (2013). *Bayesian Data Analysis*, Third Edition. CRC Press.

<a id="4">[4]</a>
ter Braak, C.J.F. and Vrugt, J.A. (2008). "Differential Evolution Markov Chain with snooker updater and fewer chains." *Statistics and Computing*, 18(4), 435-446.

---

[<- Previous: Getting Started](../../getting-started.md) | [Back to Index](../../index.md) | [Next: Input Data Frame ->](../data-frame/index.md)
