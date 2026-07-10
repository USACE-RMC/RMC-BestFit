# Distribution Fitting Analysis

[<- Previous: Analyses Overview](overview.md) | [Back to Index](../../index.md) | [Next: Univariate Analysis ->](univariate.md)

The **Distribution Fitting Analysis** (`FittingAnalysis`) is a powerful tool that automatically fits all 15 supported univariate distributions to a dataset, ranks them by goodness-of-fit criteria, and provides a comparative summary for model selection. This is often the first step in flood frequency analysis — quickly surveying which distribution families best describe the data before conducting detailed Bayesian analysis on selected candidates.

## Why Automated Fitting Matters

Consider a hydrologist analyzing 50 years of annual peak flows at a new site. Which distribution should be used?

- **Log-Pearson Type III** is the Bulletin 17C standard in the United States
- **Generalized Extreme Value (GEV)** is widely used internationally
- **Generalized Logistic** is the UK Flood Estimation Handbook standard
- Regional guidance might suggest other options

Rather than fitting each distribution manually, `FittingAnalysis` fits all 15 in parallel and produces a ranked comparison. This reveals:

1. Which distributions fit the data well
2. Which produce similar vs. different estimates
3. Whether the data have unusual features (heavy tails, bimodality)

## Mathematical Background

### Maximum Likelihood Fitting

For each distribution, `FittingAnalysis` finds the parameters that maximize the log-likelihood:

```math
\hat{\theta} = \arg\max_{\theta} \ell(\theta) = \arg\max_{\theta} \sum_{i=1}^{n} \log f(y_i | \theta)
```

where $f(y | \theta)$ is the probability density function parameterized by $\theta$.

### Model Comparison Criteria

To compare models with different numbers of parameters, we use information criteria that penalize complexity:

#### Akaike Information Criterion (AIC)

```math
\text{AIC} = -2\ell(\hat{\theta}) + 2k
```

where $k$ is the number of parameters. AIC estimates the expected Kullback-Leibler divergence from the true distribution. **Lower AIC is better.**

#### Bayesian Information Criterion (BIC)

```math
\text{BIC} = -2\ell(\hat{\theta}) + k \log(n)
```

BIC penalizes complexity more heavily than AIC for $n > 7$. It's consistent — selecting the true model as $n \to \infty$ if it's among the candidates.

#### Root Mean Square Error (RMSE)

RMSE measures the average deviation between observed and predicted quantiles at the plotting positions:

```math
\text{RMSE} = \sqrt{\frac{1}{n}\sum_{i=1}^{n}\left(y_i - F^{-1}(p_i | \hat{\theta})\right)^2}
```

where $p_i$ is the plotting position for observation $i$ and $F^{-1}$ is the inverse CDF (quantile function).

### Supported Distributions

`FittingAnalysis` fits all 15 distributions in the ***RMC-BestFit*** library:

| Distribution | Parameters | Typical Use |
|--------------|------------|-------------|
| Exponential | 2 | Peaks-over-threshold excesses |
| Gamma | 3 | Precipitation, positive-valued data |
| **GEV** | 3 | Annual maxima (international standard) |
| Generalized Logistic | 3 | UK FEH standard |
| Generalized Normal | 3 | Symmetric with flexible kurtosis |
| Generalized Pareto | 3 | Threshold excesses |
| Gumbel | 2 | Annual maxima (light tail) |
| Kappa Four | 4 | Very flexible, research use |
| Ln-Normal | 2 | Right-skewed data |
| Logistic | 2 | Growth curves |
| Log-Normal | 2 | Multiplicative processes |
| **Log-Pearson Type III** | 3 | Bulletin 17C standard (US) |
| Normal | 2 | Symmetric data, transformed flows |
| Pearson Type III | 3 | Skewed data |
| Weibull | 3 | Minima, wind speeds |

---

## Using FittingAnalysis

### Basic Usage

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;

// Create DataFrame with flood data
var df = new DataFrame();
var annualPeaks = new double[] {
    45000, 52000, 38000, 61000, 49000, 55000, 42000, 67000, 39000, 48000,
    51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000, 37000,
    54000, 46000, 59000, 43000, 56000, 40000, 63000, 35000, 57000, 45000
};
df.ExactSeries = new ExactSeries(annualPeaks);

// Create fitting analysis
var analysis = new FittingAnalysis(df);

// Run analysis (fits all 15 distributions in parallel)
await analysis.RunAsync();

// Display ranked results
Console.WriteLine("Distribution Fitting Results (ranked by AIC):");
Console.WriteLine($"{"Rank",-6} {"Distribution",-25} {"AIC",12} {"BIC",12} {"RMSE",12}");
Console.WriteLine(new string('-', 70));

var ranked = analysis.FittedDistributions
    .Where(fd => fd.FitSucceeded)
    .OrderBy(fd => fd.AIC)
    .ToList();

for (int i = 0; i < ranked.Count; i++)
{
    var fd = ranked[i];
    Console.WriteLine($"{i + 1,-6} {fd.Distribution?.Type,-25} {fd.AIC,12:F2} {fd.BIC,12:F2} {fd.RMSE,12:F1}");
}
```

### Output Example

```
Distribution Fitting Results (ranked by AIC):
Rank   Distribution              AIC          BIC         RMSE
----------------------------------------------------------------------
1      LogPearsonTypeIII         598.23       603.45      1245.3
2      GeneralizedExtremeValue   598.87       604.09      1312.7
3      PearsonTypeIII            599.12       604.34      1287.4
4      GeneralizedLogistic       600.45       605.67      1356.8
5      GeneralizedNormal         601.23       606.45      1298.6
6      LogNormal                 602.56       606.04      1423.1
7      LnNormal                  602.56       606.04      1423.1
8      Gumbel                    604.89       608.37      1567.2
9      Normal                    615.34       618.82      2145.8
...
```

### Accessing Fitted Parameters

```cs
// Get the best-fitting distribution
var best = analysis.FittedDistributions
    .Where(fd => fd.FitSucceeded)
    .OrderBy(fd => fd.AIC)
    .First();
var bestDistribution = best.Distribution
    ?? throw new InvalidOperationException("Best fit has no distribution.");

Console.WriteLine($"\nBest distribution: {bestDistribution.Type}");
Console.WriteLine("Parameters:");

foreach (var param in bestDistribution.Parameters)
{
    Console.WriteLine($"  {param.Name}: {param.Value:F4}");
}
```

### Computing Return Levels

```cs
// Get return levels from the best-fitting distribution
var returnPeriods = new int[] { 2, 5, 10, 25, 50, 100, 200, 500 };

Console.WriteLine("\nReturn Level Estimates:");
Console.WriteLine($"{"Return Period",-15} {"Return Level",15}");
Console.WriteLine(new string('-', 32));

foreach (int T in returnPeriods)
{
    double p = 1 - 1.0 / T;  // Exceedance probability
    double rl = bestDistribution.InverseCDF(p);
    Console.WriteLine($"{T,-15} {rl,15:F0}");
}
```

---

## The FittedDistribution Class

Each fitted distribution is represented by a `FittedDistribution` object:

```cs
public class FittedDistribution
{
    /// <summary>
    /// The fitted univariate distribution with estimated parameters.
    /// </summary>
    public UnivariateDistributionBase? Distribution { get; }

    /// <summary>
    /// Whether MLE converged successfully.
    /// </summary>
    public bool FitSucceeded { get; }

    /// <summary>
    /// Akaike Information Criterion.
    /// </summary>
    public double AIC { get; }

    /// <summary>
    /// Bayesian Information Criterion.
    /// </summary>
    public double BIC { get; }

    /// <summary>
    /// Root Mean Square Error at plotting positions.
    /// </summary>
    public double RMSE { get; }
}
```

---

## Configuring Probability Ordinates

By default, `FittingAnalysis` computes results at standard probability levels. You can customize these:

```cs
// Clear default ordinates and add custom ones
analysis.ProbabilityOrdinates.Clear();
analysis.ProbabilityOrdinates.AddRange(new double[] {
    0.99, 0.98, 0.96, 0.90, 0.80, 0.50, 0.20, 0.10, 0.04, 0.02, 0.01, 0.005, 0.002
});

await analysis.RunAsync();
```

---

## Interpreting Results

### AIC Differences

The magnitude of AIC differences matters:

| ΔAIC from Best | Interpretation |
|----------------|----------------|
| 0-2 | Substantial support, essentially equivalent |
| 2-4 | Some support, consider both models |
| 4-7 | Considerably less support |
| > 10 | Essentially no support |

If multiple distributions have ΔAIC < 2, consider model averaging or conducting detailed Bayesian analysis on all of them.

### When Results Disagree

Sometimes AIC and BIC select different models:
- **AIC** tends to select more complex models (lower penalty)
- **BIC** tends to select simpler models (higher penalty for complexity)

If they disagree substantially, the data may not strongly favor any particular model. Consider:
1. Using model averaging (weighted by AIC weights)
2. Conducting sensitivity analysis with multiple distributions
3. Using physical reasoning to select

### Warning Signs

Watch for these issues in the fitting results:

| Issue | Possible Cause |
|-------|----------------|
| Many distributions fail to converge | Unusual data, outliers, wrong units |
| Very different AIC among similar distributions | Boundary effects, numerical issues |
| Shape parameter at bound | Distribution may not be appropriate |
| Extremely low RMSE for complex model | Possible overfitting |

---

## Working with Different Data Types

`FittingAnalysis` works with any DataFrame, including those with uncertain, interval, and threshold data:

```cs
var df = new DataFrame();

// Systematic record
df.ExactSeries = new ExactSeries(systematicPeaks);

// Historical flood with uncertainty
df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));

// Perception threshold
var threshold = new ThresholdData(1800, 1900, 50000);
threshold.NumberBelow = 100;
df.ThresholdSeries.Add(threshold);

// Fitting analysis handles all data types correctly
var analysis = new FittingAnalysis(df);
await analysis.RunAsync();
```

The likelihood for each distribution automatically incorporates all data types as described in the [Input Data Frame](../data-frame/index.md) chapter.

---

## Parallel Execution

`FittingAnalysis` fits all 15 distributions in parallel for performance. On a typical workstation, the entire analysis completes in seconds:

```cs
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
await analysis.RunAsync();
stopwatch.Stop();

Console.WriteLine($"Fitted 15 distributions in {stopwatch.ElapsedMilliseconds} ms");
```

Typical timing: 500-2000 ms for 30-100 observations.

---

## XML Serialization

Save and restore fitting analysis results:

```cs
// Save results
var xElement = analysis.ToXElement();
xElement.Save("fitting_results.xml");

// Restore (requires original DataFrame)
var loadedXml = XElement.Load("fitting_results.xml");
var restoredAnalysis = new FittingAnalysis(df, loadedXml);

// Access restored results
foreach (var fd in restoredAnalysis.FittedDistributions)
{
    Console.WriteLine($"{fd.Distribution?.Type}: AIC = {fd.AIC:F2}");
}
```

---

## Best Practices

### 1. Start with FittingAnalysis

Before detailed Bayesian analysis, run `FittingAnalysis` to:
- Survey which distributions fit well
- Identify data issues (outliers, wrong units)
- Guide distribution selection

### 2. Don't Blindly Trust AIC

AIC identifies the best-fitting model among candidates, but:
- The true distribution may not be among the 15 candidates
- Physical reasoning should inform selection
- Regulatory requirements may mandate specific distributions

### 3. Check Tail Behavior

Different distributions can fit the bulk of the data similarly but differ dramatically in the tails. Compare 100-year and 500-year return levels:

```cs
Console.WriteLine("\n100-year and 500-year Return Levels:");
Console.WriteLine($"{"Distribution",-25} {"RL-100",12} {"RL-500",12}");

foreach (var fd in ranked.Take(5))
{
    if (fd.Distribution is null)
    {
        continue;
    }

    double rl100 = fd.Distribution.InverseCDF(0.99);
    double rl500 = fd.Distribution.InverseCDF(0.998);
    Console.WriteLine($"{fd.Distribution.Type,-25} {rl100,12:F0} {rl500,12:F0}");
}
```

### 4. Follow Up with Bayesian Analysis

`FittingAnalysis` provides point estimates only. For uncertainty quantification, follow up with `UnivariateAnalysis`:

```cs
// Select best distribution type from fitting analysis
var selectedDistribution = ranked.First().Distribution
    ?? throw new InvalidOperationException("Selected fit has no distribution.");
var bestType = selectedDistribution.Type;

// Create full model with same distribution type
var model = new UnivariateDistribution(df, bestType);

// Run Bayesian analysis for full uncertainty quantification
var bayesianAnalysis = new UnivariateAnalysis(model);
bayesianAnalysis.BayesianAnalysis.Iterations = 10000;
bayesianAnalysis.BayesianAnalysis.WarmupIterations = 5000;
await bayesianAnalysis.RunAsync();

// Now have full posterior distributions
var results = bayesianAnalysis.BayesianAnalysis.Results;
```

---

## API Reference

### FittingAnalysis Class

```cs
public class FittingAnalysis : AnalysisBase, IProbabilityOrdinates
{
    // Constructors
    public FittingAnalysis(DataFrame dataFrame);
    public FittingAnalysis(DataFrame dataFrame, XElement xElement);

    // Properties
    public DataFrame DataFrame { get; }
    public ProbabilityOrdinates ProbabilityOrdinates { get; }
    public List<UnivariateDistributionBase> DistributionList { get; }
    public List<FittedDistribution> FittedDistributions { get; }
    public bool IsEstimated { get; }

    // Methods
    public override Task RunAsync(SafeProgressReporter? progressReporter = null);
    public override (bool IsValid, List<string> ValidationMessages) Validate();
    public void ClearResults();
    public XElement ToXElement();
}
```

---

## Example: Complete Workflow

```cs
using RMC.BestFit.Analyses;
using RMC.BestFit.Models;
using System.Xml.Linq;

// Step 1: Load data
var df = new DataFrame();
df.ExactSeries = new ExactSeries(LoadAnnualPeaks("station_12345.csv"));

// Step 2: Run fitting analysis
var fitting = new FittingAnalysis(df);
await fitting.RunAsync();

// Step 3: Display top 5 distributions
var top5 = fitting.FittedDistributions
    .Where(fd => fd.FitSucceeded)
    .OrderBy(fd => fd.AIC)
    .Take(5)
    .ToList();

Console.WriteLine("Top 5 Distributions by AIC:\n");
foreach (var fd in top5)
{
    if (fd.Distribution is null)
    {
        continue;
    }

    Console.WriteLine($"{fd.Distribution.Type}:");
    Console.WriteLine($"  AIC: {fd.AIC:F2}, BIC: {fd.BIC:F2}");
    Console.WriteLine($"  100-yr: {fd.Distribution.InverseCDF(0.99):F0} cfs\n");
}

// Step 4: Select and run detailed Bayesian analysis
var topDistribution = top5.First().Distribution
    ?? throw new InvalidOperationException("Selected fit has no distribution.");
var selectedType = topDistribution.Type;
var model = new UnivariateDistribution(df, selectedType);
var bayesian = new UnivariateAnalysis(model);
bayesian.BayesianAnalysis.Iterations = 10000;
await bayesian.RunAsync();

// Step 5: Report results with uncertainty
Console.WriteLine($"\n{selectedType} Bayesian Results:");
var results = bayesian.BayesianAnalysis.Results;

for (int i = 0; i < model.Parameters.Count; i++)
{
    var stats = results.ParameterResults[i].SummaryStatistics;
    Console.WriteLine($"  {model.Parameters[i].Name}: {stats.Mean:F3} ± {stats.StandardDeviation:F3}");
}

// Step 6: Save results
fitting.ToXElement().Save("fitting_results.xml");
bayesian.ToXElement().Save("bayesian_results.xml");
```

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Analyses/DistributionFitting/FittingAnalysis.cs`, `src/RMC.BestFit/Analyses/DistributionFitting/FittedDistribution.cs`, `src/RMC.BestFit/Estimation/MaximumLikelihood.cs`, and `src/RMC.BestFit/Models/UnivariateDistribution/UnivariateDistribution.cs`.

---

## References

<a id="1">[1]</a>
Akaike, H. (1974). "A new look at the statistical model identification." *IEEE Transactions on Automatic Control*, 19(6), 716-723.

<a id="2">[2]</a>
Schwarz, G. (1978). "Estimating the dimension of a model." *Annals of Statistics*, 6(2), 461-464.

<a id="3">[3]</a>
Hosking, J.R.M. and Wallis, J.R. (1997). *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press.

<a id="4">[4]</a>
England, J.F., Cohn, T.A., Faber, B.A., et al. (2019). *Guidelines for Determining Flood Flow Frequency: Bulletin 17C*. U.S. Geological Survey Techniques and Methods, Book 4, Chapter B5.

---

[<- Previous: Analyses Overview](overview.md) | [Back to Index](../../index.md) | [Next: Univariate Analysis ->](univariate.md)
