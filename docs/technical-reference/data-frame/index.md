# Input Data Frame

[<- Previous: Models Overview](../models/overview.md) | [Back to Index](../../index.md) | [Next: Distribution API ->](../distributions/index.md)

The **DataFrame** is the fundamental data structure in ***RMC-BestFit*** for storing and organizing observations for statistical analysis. Unlike simple arrays of numbers, the DataFrame supports the complex data types encountered in real-world flood frequency analysis: exact observations, uncertain measurements with error distributions, interval-censored data from paleoflood studies, and perception thresholds for historical floods.

This chapter provides comprehensive coverage of the DataFrame structure, each data type it supports, and how these data types are used in flood frequency analysis.

## Why Data Types Matter

Consider analyzing flood risk at a dam site. Your data might include:

1. **50 years of systematic records** from a streamflow gauge — exact annual maxima
2. **A 1927 flood** estimated from high-water marks — uncertain, with a measurement error distribution
3. **Paleoflood deposits** indicating floods between 80,000 and 120,000 cfs within the last 500 years — interval-censored
4. **Newspaper accounts** noting no floods exceeded the 1894 bridge deck elevation (35,000 cfs capacity) between 1850-1920 — perception threshold

Each observation type contributes different information to the analysis. ***RMC-BestFit's*** DataFrame handles all of these seamlessly, and the likelihood functions automatically incorporate each data type correctly.

## The DataFrame Structure

### Overview

A DataFrame contains four collections, one for each data type:

```cs
public class DataFrame : INotifyPropertyChanged
{
    /// <summary>
    /// Exact observations (point values with known magnitude).
    /// </summary>
    public ExactSeries ExactSeries { get; set; }

    /// <summary>
    /// Uncertain observations (values with measurement error distributions).
    /// </summary>
    public UncertainSeries UncertainSeries { get; set; }

    /// <summary>
    /// Interval-censored observations (bounded ranges).
    /// </summary>
    public IntervalSeries IntervalSeries { get; set; }

    /// <summary>
    /// Threshold data (perception thresholds with exceedance counts).
    /// </summary>
    public ThresholdSeries ThresholdSeries { get; set; }

    /// <summary>
    /// The plotting position parameter. Default is 0.0 (Weibull).
    /// </summary>
    public double PlottingParameter { get; set; }

    /// <summary>
    /// The number of events per time unit (typically 1 for annual maxima).
    /// </summary>
    public double Lambda { get; }
}
```

### Creating a DataFrame

```cs
using RMC.BestFit.Models;
using Numerics.Distributions;

// Create an empty DataFrame
var df = new DataFrame();

// Add exact observations
var annualPeaks = new double[] { 45000, 52000, 38000, 61000, 49000, /* ... */ };
df.ExactSeries = new ExactSeries(annualPeaks);

// Add uncertain observation (historical flood with measurement error)
df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));

// Add interval-censored observation (paleoflood)
df.IntervalSeries.Add(new IntervalData(1500, 60000, 80000, 100000));

// Add perception threshold
var threshold = new ThresholdData(1850, 1920, 40000);
threshold.NumberBelow = 70;  // Years below threshold
df.ThresholdSeries.Add(threshold);
```

---

## Exact Data

### Definition

**Exact data** are observations where the magnitude is known with negligible measurement error. These form the foundation of most flood frequency analyses.

### Mathematical Treatment

Each exact observation $y_i$ contributes directly to the likelihood:

```math
\mathcal{L}_{\text{exact}}(\theta) = \prod_{i=1}^{n} f(y_i | \theta)
```

where $f(y | \theta)$ is the probability density function of the assumed distribution.

### The ExactData Class

```cs
public class ExactData : Data
{
    /// <summary>
    /// The time index (e.g., year).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The observed value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The plotting position (probability) for frequency plots.
    /// </summary>
    public double PlottingPosition { get; set; }

    /// <summary>
    /// Whether this observation is flagged as a low outlier.
    /// </summary>
    public bool IsLowOutlier { get; set; }
}
```

### Creating Exact Data

```cs
// From array of values (indexes automatically assigned 0, 1, 2, ...)
df.ExactSeries = new ExactSeries(new double[] { 45000, 52000, 38000, 61000 });

// With explicit indexes (e.g., water years)
df.ExactSeries.Add(new ExactData(1970, 45000));
df.ExactSeries.Add(new ExactData(1971, 52000));
df.ExactSeries.Add(new ExactData(1972, 38000));
df.ExactSeries.Add(new ExactData(1973, 61000));

// With explicit plotting position (rare)
df.ExactSeries.Add(new ExactData(1970, 45000, plottingPosition: 0.5, isLowOutlier: false));
```

### Use Cases

| Application | Description |
|-------------|-------------|
| Annual peak flows | Primary systematic record from gauging station |
| Monthly/daily maxima | Sub-annual frequency analysis |
| Precipitation depths | Point rainfall observations |
| Wind speeds | Annual maximum gusts |

---

## Uncertain Data

### Definition

**Uncertain data** are observations with significant measurement error that should be propagated into the analysis. Each observation is represented by a probability distribution rather than a point value.

### Mathematical Treatment

For uncertain observations, we integrate over the measurement error distribution:

```math
\mathcal{L}_{\text{uncertain}}(\theta) = \prod_{j=1}^{m} \int f(y | \theta) \cdot g_j(y) \, dy
```

where $g_j(y)$ is the measurement error distribution for observation $j$.

In practice, this integral is approximated numerically or via Monte Carlo integration during MCMC sampling.

### The UncertainData Class

```cs
public class UncertainData : Data
{
    /// <summary>
    /// The time index (e.g., year).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The central (most likely) value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The measurement error distribution.
    /// </summary>
    public IUnivariateDistribution Distribution { get; set; }
}
```

### Supported Error Distributions

Any distribution from the Numerics library can represent measurement error:

| Distribution | Use Case |
|--------------|----------|
| `Normal` | Symmetric error, known standard deviation |
| `LogNormal` | Positive-only error, multiplicative uncertainty |
| `Triangular` | Bounded error with most likely value |
| `Uniform` | Bounded error, no preference within range |
| `TruncatedNormal` | Bounded symmetric error |

### Creating Uncertain Data

```cs
using Numerics.Distributions;

// Normal measurement error: mean 85,000, SD 10,000
df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 10000)));

// Log-normal error: geometric mean 75,000, multiplicative SD factor 1.2
df.UncertainSeries.Add(new UncertainData(1913, new LogNormal(Math.Log(75000), Math.Log(1.2))));

// Triangular error: minimum 70,000, most likely 90,000, maximum 110,000
df.UncertainSeries.Add(new UncertainData(1927, new Triangular(70000, 90000, 110000)));

// Uniform error: anywhere between 50,000 and 70,000 equally likely
df.UncertainSeries.Add(new UncertainData(1862, new Uniform(50000, 70000)));
```

### Use Cases

| Application | Description |
|-------------|-------------|
| Historical floods | High-water marks with indirect discharge estimates |
| Paleoflood peaks | Geologic evidence with dating and magnitude uncertainty |
| Reconstructed flows | Tree-ring or other proxy-based estimates |
| Regional estimates | Transferred data with uncertainty |

---

## Interval Data

### Definition

**Interval-censored data** are observations where only the range is known — the true value lies somewhere between a lower and upper bound. This is common in paleoflood hydrology where geologic evidence constrains flood magnitude but doesn't provide exact values.

### Mathematical Treatment

The likelihood contribution from an interval-censored observation is the probability mass within the interval:

```math
\mathcal{L}_{\text{interval}}(\theta) = \prod_{k=1}^{p} \left[ F(y_k^U | \theta) - F(y_k^L | \theta) \right]
```

where $F$ is the cumulative distribution function, $y_k^L$ is the lower bound, and $y_k^U$ is the upper bound.

### The IntervalData Class

```cs
public class IntervalData : Data
{
    /// <summary>
    /// The time index (e.g., year or period identifier).
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// The lower bound of the interval.
    /// </summary>
    public double LowerValue { get; set; }

    /// <summary>
    /// The most likely value (central estimate).
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// The upper bound of the interval.
    /// </summary>
    public double UpperValue { get; set; }
}
```

### Creating Interval Data

```cs
// Paleoflood: between 80,000 and 120,000 cfs, most likely around 100,000
// Index 1500 indicates approximate year (can also be arbitrary identifier)
df.IntervalSeries.Add(new IntervalData(1500, 80000, 100000, 120000));

// Another paleoflood event
df.IntervalSeries.Add(new IntervalData(1200, 90000, 110000, 130000));
```

### Use Cases

| Application | Description |
|-------------|-------------|
| Slackwater deposits | Floods that deposited sediment at known elevations |
| Scour indicators | Channel erosion suggesting minimum flood magnitude |
| Botanical evidence | Flood-scarred trees indicating stage range |
| Archaeological evidence | Flood damage to structures of known elevation |

---

## Threshold Data

### Definition

**Threshold data** encode information about floods that did or did not exceed a perception threshold during a period of observation. This is crucial for extending records beyond the systematic gauging period using historical information.

### Mathematical Treatment

Threshold data contribute binomial-like terms to the likelihood. If $k$ floods exceeded threshold $y_T$ during a period of $n$ years:

```math
\mathcal{L}_{\text{threshold}}(\theta) = \binom{n}{k} \left[ 1 - F(y_T | \theta) \right]^k \cdot \left[ F(y_T | \theta) \right]^{n-k}
```

The threshold also adjusts the conditional distribution of non-exceedances (floods that occurred but stayed below the threshold).

### The ThresholdData Class

```cs
public class ThresholdData : Data
{
    /// <summary>
    /// The start year of the perception period.
    /// </summary>
    public int StartYear { get; set; }

    /// <summary>
    /// The end year of the perception period.
    /// </summary>
    public int EndYear { get; set; }

    /// <summary>
    /// The perception threshold value.
    /// </summary>
    public double Value { get; set; }

    /// <summary>
    /// Effective number of events below the threshold after overlap processing.
    /// </summary>
    public int NumberBelow { get; internal set; }

    /// <summary>
    /// User-supplied exceedance count; reads return the current effective count.
    /// </summary>
    public int NumberAbove { get; set; }
}
```

`NumberAbove` is the only count supplied by callers. Internally, the data frame retains that
source count separately and recomputes effective `NumberAbove` and `NumberBelow` values from it
whenever explicit, interval, uncertain, or threshold data change. Reprocessing is idempotent:
repeated calls with unchanged input produce the same counts. XML continues to store the source
count in the existing `NumberAbove` attribute, and effective counts are rebuilt when a data frame
is restored.

### Creating Threshold Data

```cs
// Historical period 1850-1920: threshold was 40,000 cfs (e.g., bridge deck elevation)
// We know no floods exceeded this threshold during these 71 inclusive years.
var threshold = new ThresholdData(1850, 1920, 40000)
{
    NumberAbove = 0
};
df.ThresholdSeries.Add(threshold);

// Another period with two floods known to have exceeded the threshold.
var threshold2 = new ThresholdData(1800, 1850, 50000)
{
    NumberAbove = 2
};
df.ThresholdSeries.Add(threshold2);
```

### Use Cases

| Application | Description |
|-------------|-------------|
| Historical accounts | Newspaper records of "highest flood since..." |
| Infrastructure evidence | Bridges, buildings that survived/were damaged |
| Paleostage indicators | Geologic evidence of non-exceedance bounds |
| Perception thresholds | Bulletin 17C systematic threshold analysis |

---

## Plotting Positions

### Theory

Plotting positions assign empirical probabilities to ranked observations for display on frequency plots. For observation ranked $i$ out of $n$ (from largest to smallest), the general formula is:

```math
p_i = \frac{i - a}{n + 1 - 2a}
```

where $a$ is the plotting position parameter.

### Common Plotting Position Formulas

| Name | Parameter $a$ | Formula | Recommended For |
|------|---------------|---------|-----------------|
| Weibull | 0.0 | $i/(n+1)$ | General use, unbiased for uniform |
| Hazen | 0.5 | $(i-0.5)/n$ | Quick approximation |
| Cunnane | 0.40 | $(i-0.4)/(n+0.2)$ | GEV, LP3 distributions |
| Gringorten | 0.44 | $(i-0.44)/(n+0.12)$ | Gumbel distribution |
| Blom | 0.375 | $(i-0.375)/(n+0.25)$ | Normal distribution |

### Setting the Plotting Parameter

```cs
// Create DataFrame
var df = new DataFrame();
df.ExactSeries = new ExactSeries(annualPeaks);

// Set plotting parameter (default is 0.0 for Weibull)
df.PlottingParameter = 0.0;   // Weibull
// df.PlottingParameter = 0.44;  // Gringorten
// df.PlottingParameter = 0.40;  // Cunnane

// Plotting positions are calculated automatically when the series changes
// Access via individual data items
foreach (var item in df.ExactSeries)
{
    Console.WriteLine($"Value: {item.Value}, Plotting Position: {item.PlottingPosition:F4}");
}
```

### Recommendation

Use **Weibull (a = 0.0)** as the default. It's the most commonly used formula in hydrology and makes no distributional assumptions about the data.

---

## Combining Data Types

### The Full Likelihood

When a DataFrame contains multiple data types, the total likelihood is the product of contributions from each type:

```math
\mathcal{L}(\theta) = \mathcal{L}_{\text{exact}}(\theta) \cdot \mathcal{L}_{\text{uncertain}}(\theta) \cdot \mathcal{L}_{\text{interval}}(\theta) \cdot \mathcal{L}_{\text{threshold}}(\theta)
```

Or in log-space:

```math
\ell(\theta) = \ell_{\text{exact}}(\theta) + \ell_{\text{uncertain}}(\theta) + \ell_{\text{interval}}(\theta) + \ell_{\text{threshold}}(\theta)
```

***RMC-BestFit*** handles this automatically — simply populate the appropriate series and run the analysis.

### Example: Complete Flood Frequency Dataset

```cs
using RMC.BestFit.Models;
using Numerics.Distributions;

var df = new DataFrame();

// 1. Systematic record (50 years of exact annual peaks)
var systematicPeaks = new double[] {
    45000, 52000, 38000, 61000, 49000, 55000, 42000, 67000, 39000, 48000,
    51000, 36000, 58000, 44000, 53000, 47000, 62000, 41000, 50000, 37000,
    54000, 46000, 59000, 43000, 56000, 40000, 63000, 35000, 57000, 45000,
    50000, 42000, 58000, 39000, 52000, 47000, 61000, 44000, 55000, 38000,
    49000, 53000, 36000, 60000, 46000, 54000, 41000, 57000, 43000, 48000
};
df.ExactSeries = new ExactSeries(systematicPeaks);

// 2. Historical flood (1889) with uncertain magnitude
df.UncertainSeries.Add(new UncertainData(1889, new Normal(85000, 12000)));

// 3. Paleoflood deposits indicating two ancient floods
df.IntervalSeries.Add(new IntervalData(1500, 70000, 90000, 110000));
df.IntervalSeries.Add(new IntervalData(1200, 80000, 100000, 120000));

// 4. Historical perception threshold (no floods > 80,000 cfs from 1800-1889)
var threshold = new ThresholdData(1800, 1889, 80000);
threshold.NumberBelow = 89;
threshold.NumberAbove = 1;  // The 1889 flood itself
df.ThresholdSeries.Add(threshold);

// 5. Non-exceedance bound from paleostage (no floods > 150,000 cfs in last 500 years)
var paleoBound = new ThresholdData(1520, 2020, 150000);
paleoBound.NumberBelow = 500;
paleoBound.NumberAbove = 0;
df.ThresholdSeries.Add(paleoBound);

Console.WriteLine($"Dataset summary:");
Console.WriteLine($"  Exact observations: {df.ExactSeries.Count}");
Console.WriteLine($"  Uncertain observations: {df.UncertainSeries.Count}");
Console.WriteLine($"  Interval observations: {df.IntervalSeries.Count}");
Console.WriteLine($"  Threshold periods: {df.ThresholdSeries.Count}");
Console.WriteLine($"  Total record length: {df.TotalRecordLength()} years");
```

---

## XML Serialization

DataFrames can be saved and loaded via XML for persistence:

```cs
using System.Xml.Linq;

// Save to XML
var xElement = df.ToXElement();
xElement.Save("flood_data.xml");

// Load from XML
var loadedXml = XElement.Load("flood_data.xml");
var loadedDf = new DataFrame(loadedXml);
```

---

## Validation

Before running an analysis, validate the DataFrame:

```cs
var validation = df.Validate();
if (!validation.IsValid)
{
    Console.WriteLine("Validation errors:");
    foreach (var message in validation.ValidationMessages)
    {
        Console.WriteLine($"  - {message}");
    }
}
else
{
    Console.WriteLine("DataFrame is valid.");
}
```

Common validation issues:
- Duplicate indexes across series
- Invalid values (NaN, Infinity)
- Inconsistent threshold periods
- Index out of valid range

---

## Best Practices

### Data Quality

1. **Review outliers**: Use exploratory plots before analysis
2. **Document data sources**: Track provenance of each observation
3. **Quantify uncertainty honestly**: Don't understate measurement error
4. **Check temporal consistency**: Ensure indexes don't overlap incorrectly

### Choosing Data Types

| Question | Data Type |
|----------|-----------|
| Do you know the exact value? | ExactData |
| Is there significant measurement error? | UncertainData |
| Do you only know a range? | IntervalData |
| Do you know floods did/didn't exceed a level? | ThresholdData |

### Sample Size Considerations

| Model Complexity | Minimum Recommended Sample |
|------------------|---------------------------|
| 2-parameter (Normal) | 15-20 observations |
| 3-parameter (GEV, LP3) | 25-30 observations |
| Mixture models | 50+ observations |
| Regional models | 100+ site-years |

Historical and paleoflood data can effectively increase sample size for extreme quantiles, even if they don't add precision at lower return periods.

---

## Implementation Sources

Primary source paths: `src/RMC.BestFit/Models/DataFrame/DataFrame.cs`, `src/RMC.BestFit/Models/DataFrame/DataTypes`, `src/RMC.BestFit/Models/DataFrame/DataCollections`, and `src/RMC.BestFit/Models/DataFrame/ThresholdDiagnostics.cs`.

---

## References

<a id="1">[1]</a>
Stedinger, J.R. and Cohn, T.A. (1986). "Flood frequency analysis with historical and paleoflood information." *Water Resources Research*, 22(5), 785-793.

<a id="2">[2]</a>
O'Connell, D.R.H., Ostenaa, D.A., Levish, D.R., and Klinger, R.E. (2002). "Bayesian flood frequency analysis with paleohydrologic bound data." *Water Resources Research*, 38(5), 1058.

<a id="3">[3]</a>
England, J.F., Cohn, T.A., Faber, B.A., et al. (2019). *Guidelines for Determining Flood Flow Frequency: Bulletin 17C*. U.S. Geological Survey Techniques and Methods, Book 4, Chapter B5.

<a id="4">[4]</a>
Hosking, J.R.M. and Wallis, J.R. (1997). *Regional Frequency Analysis: An Approach Based on L-Moments*. Cambridge University Press.

---

[<- Previous: Models Overview](../models/overview.md) | [Back to Index](../../index.md) | [Next: Distribution API ->](../distributions/index.md)
