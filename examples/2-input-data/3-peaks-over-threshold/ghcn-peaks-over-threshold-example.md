# GHCN Peaks-Over-Threshold Example

## Overview

This example demonstrates applying the **Peaks-Over-Threshold (POT)** method to daily precipitation data, showing that the technique is not limited to streamflow analysis. A threshold of 1.0 inch is applied to daily precipitation from a cooperative observer station in the mountains of southern California, extracting independent storm events for precipitation frequency analysis.

Comparing this example with the [USGS POT streamflow example](usgs-peaks-over-threshold-example.md) in the same folder illustrates how the POT method adapts to different hydrological variables and climates.

## Data Source

- **Agency:** NOAA National Centers for Environmental Information (NCEI)
- **Network:** Global Historical Climatology Network -- Daily (GHCN-Daily)
- **Station:** USC00040741, Big Bear Lake, CA
- **Variable:** Daily precipitation
- **Record:** 1960--present (approximately 67 years)
- **Units:** Inches (in)

Big Bear Lake is a mountain community at approximately 6,750 feet elevation in the San Bernardino Mountains of southern California. The station receives most of its precipitation from Pacific storm systems between November and April, with occasional summer monsoon events. Annual precipitation averages roughly 25 inches, much of it falling as snow in winter.

## What's Inside

Open `ghcn-peaks-over-threshold-example.bestfit` in RMC-BestFit. The Project Explorer shows:

| Element | Type | Description |
|---------|------|-------------|
| GHCN-USC00040741-Precipitation | Time Series Data | Daily precipitation from NOAA GHCN |
| GHCH-USC00040741-POT | Input Data | POT series with threshold = 1.0 inch, minimum separation = 5 days |

> **Note:** The Input Data element name contains a typo ("GHCH" instead of "GHCN"). This is a known issue in the example file that will be corrected in a future release. The element functions correctly despite the name.

![Project Explorer showing time series and POT Input Data elements](../images/ghcn-pot-project-explorer.png)
*Figure 1: Project Explorer with precipitation time series and POT elements*

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/2-input-data/3-peaks-over-threshold/`
3. Open `ghcn-peaks-over-threshold-example.bestfit`
4. Expand **Time Series Data** and **Input Data** in the Project Explorer

### Exploring the Source Time Series

1. Click **GHCN-USC00040741-Precipitation** in the Project Explorer
2. The **Time Series** tab displays the full daily precipitation record -- note the episodic nature, with most days recording zero precipitation and occasional large storm events
3. Click the **Seasonality** tab to see the Mediterranean climate pattern: precipitation is concentrated in winter (November--April) with dry summers

![Daily precipitation record for Big Bear Lake, CA](../images/ghcn-pot-daily-precipitation.png)
*Figure 2: Daily precipitation at Big Bear Lake -- note the seasonal concentration in winter months*

### Exploring the POT Input Data

1. Click **GHCH-USC00040741-POT** in the Project Explorer
2. The **Chronology** tab shows all extracted storm events plotted against time
3. Click the **Frequency** tab to see the empirical frequency curve
4. Click the **Seasonality** tab to confirm events are concentrated in winter months

![Chronology of POT precipitation events](../images/ghcn-pot-chronology.png)
*Figure 3: Chronology of precipitation events exceeding 1.0 inch*

![Frequency plot of POT precipitation events](../images/ghcn-pot-frequency.png)
*Figure 4: Empirical frequency curve for POT precipitation events*

### Understanding the POT Configuration

**Threshold (1.0 inch):** Daily precipitation totals exceeding 1.0 inch are considered as potential storm events. This threshold is low enough to capture a meaningful number of events per year while filtering out light rain and drizzle that do not represent significant storms.

**Minimum Steps Between Peaks (5 days):** After identifying a peak, the algorithm requires at least 5 days before the next peak can be selected. For daily precipitation, this helps ensure that consecutive rainy days within a single storm system are not counted as separate events.

**Result:** Approximately 233 independent storm events extracted from 67 years of record, yielding an average rate of roughly 3.5 events per year. This is substantially higher than the streamflow POT example (~0.83 events/year), reflecting the fact that precipitation POT naturally captures more events because individual storms regularly exceed a moderate threshold.

### Comparing with Streamflow POT

This example pairs well with the [USGS Orestimba Creek POT example](usgs-peaks-over-threshold-example.md) in the same folder:

| | Precipitation (this example) | Streamflow (USGS example) |
|---|---|---|
| **Threshold** | 1.0 inch | 650 cfs |
| **Event rate** | ~3.5 per year | ~0.83 per year |
| **Total events** | ~233 | 78 |
| **Record length** | ~67 years | ~94 years |
| **Climate** | Mediterranean (winter storms) | Semi-arid (episodic rainfall) |
| **Seasonality** | Strong winter peak | Strong winter peak |

The higher event rate in precipitation POT is typical: a single storm system produces a measurable precipitation event on the day it occurs, but may or may not produce a streamflow peak above the threshold depending on antecedent conditions, infiltration, and basin response.

### Viewing the Threshold Diagnostic Plots

As with the streamflow POT example, RMC-BestFit provides three threshold diagnostic plots:

1. **MRL (Mean Residual Life) Plot** -- Shows the mean excess as a function of threshold. Look for approximate linearity above the selected threshold.
2. **Modified Scale Stability Plot** -- The adjusted scale parameter should stabilize above the threshold.
3. **Shape Stability Plot** -- The shape parameter should stabilize above the threshold.

![MRL plot for precipitation threshold selection](../images/ghcn-pot-mrl.png)
*Figure 5: Mean Residual Life plot for precipitation threshold*

### Viewing the Properties Panel

1. With the POT Input Data element selected, open the **Properties** panel
2. The panel shows the POT configuration:
   - **Exact Data Method:** Peaks-Over-Threshold Series
   - **Time Series Element:** GHCN-USC00040741-Precipitation
   - **Threshold:** 1.0 (inches)
   - **Min Steps Between Peaks:** 5 (days)

![Properties panel showing precipitation POT settings](../images/ghcn-pot-properties.png)
*Figure 6: Properties panel for precipitation POT element*

### Creating Your Own Precipitation POT Element

To create a POT element from precipitation data:

1. First, ensure you have a daily precipitation time series element in your project (download from GHCN, ABOM, or enter manually)
2. Right-click **Input Data** in the Project Explorer and select **Create New**
3. In the Properties panel:
   - Set **Exact Data Method** to **Peaks-Over-Threshold Series**
   - Select the precipitation **Time Series Element**
   - Set an initial **Threshold** -- for daily precipitation in inches, 0.5--2.0 inches is a typical starting range depending on climate
   - Set **Min Steps Between Peaks** to 3--5 days for daily precipitation (shorter than streamflow because precipitation events are more distinct)
4. Review the threshold diagnostic plots and adjust as needed

**Threshold guidance for precipitation:**

- Arid/semi-arid climates: Lower thresholds (0.25--0.5 inch) to capture enough events
- Temperate climates: Moderate thresholds (0.5--1.5 inches)
- Tropical/high-rainfall climates: Higher thresholds (1.0--3.0 inches)
- Target an event rate of 2--5 events per year as a starting point

## Connection to Other Examples

This example uses the same GHCN station (USC00040741, Big Bear Lake, CA) as the [GHCN download example](../../1-time-series-data/2-ghcn-download/ghcn-download-example.md) in the Time Series Data folder. The time series download example shows the raw daily precipitation; this example shows how to extract independent storm events for frequency analysis.

## Key Settings Reference

| Setting | Value | Notes |
|---------|-------|-------|
| Exact Data Method | Peaks-Over-Threshold Series | Extracts peaks above a threshold from a time series |
| Time Series Element | GHCN-USC00040741-Precipitation | Source daily precipitation |
| Threshold | 1.0 inch | Moderate threshold for mountain precipitation |
| Min Steps Between Peaks | 5 days | Ensures storm independence |
| Extracted Events | ~233 | Over ~67 years of record |
| Event Rate | ~3.5 per year | Typical for precipitation POT at this threshold |

## Next Steps

- **Distribution Fitting** -- Fit the Generalized Pareto distribution to the precipitation exceedances
- **Univariate Distribution Analysis** -- Perform a Poisson-GP frequency analysis to estimate precipitation return levels (e.g., the 100-year daily precipitation)
- **Streamflow POT** -- Compare this precipitation POT with the [USGS streamflow POT example](usgs-peaks-over-threshold-example.md) to understand how the same method applies to different variables
- **Block Maximum** -- See the [Block Maximum example](../1-block-maximum/usgs-block-max-example.md) for an alternative approach using annual maxima
