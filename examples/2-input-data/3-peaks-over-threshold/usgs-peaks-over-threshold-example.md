# USGS Peaks-Over-Threshold Example

## Overview

This example demonstrates the **Peaks-Over-Threshold (POT)** method for extracting flood events from a daily discharge time series. Unlike the block maximum approach (which returns exactly one value per year), POT selects all peaks exceeding a user-defined threshold, producing a partial-duration series that can include multiple events in wet years and zero events in dry years.

The example uses daily discharge from an ephemeral stream in California to show how POT works with highly variable, event-driven hydrology.

## Data Source

- **Station:** USGS 11274500, Orestimba Creek near Newman, CA
- **Source Time Series:** Daily mean discharge, downloaded from the USGS NWIS API
- **Record:** 1932--present (approximately 94 years)
- **Units:** Cubic feet per second (cfs)

Orestimba Creek is an ephemeral stream draining the eastern foothills of the Diablo Range into the San Joaquin Valley. Flow is highly intermittent -- the creek is dry for much of the year and floods only during Pacific storm events, primarily between November and April. This makes it an instructive example for POT analysis, where the threshold must be set high enough to capture meaningful flood events while filtering out base flow noise.

## What's Inside

Open `usgs-peaks-over-threshold-example.bestfit` in RMC-BestFit. The Project Explorer shows:

| Element | Type | Description |
|---------|------|-------------|
| USGS - 11274500 - Daily Discharge | Time Series Data | Daily mean discharge from USGS NWIS |
| USGS - 11274500 - Peaks-Over-Threshold | Input Data | POT series with threshold = 650 cfs, minimum separation = 5 days |

![Project Explorer showing time series and POT Input Data elements](../images/usgs-pot-project-explorer.png)
*Figure 1: Project Explorer with time series and POT elements*

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/2-input-data/3-peaks-over-threshold/`
3. Open `usgs-peaks-over-threshold-example.bestfit`
4. Expand **Time Series Data** and **Input Data** in the Project Explorer

### Exploring the Source Time Series

1. Click **USGS - 11274500 - Daily Discharge** in the Project Explorer
2. The **Time Series** tab displays the full daily hydrograph -- note the highly episodic nature of the flow, with long periods of zero or near-zero discharge punctuated by sharp flood peaks
3. Click the **Seasonality** tab to confirm that floods are concentrated in the winter wet season (November--April)

![Daily discharge hydrograph for Orestimba Creek](../images/usgs-pot-daily-discharge.png)
*Figure 2: Daily discharge for Orestimba Creek -- highly episodic, event-driven hydrology*

### Exploring the POT Input Data

1. Click **USGS - 11274500 - Peaks-Over-Threshold** in the Project Explorer
2. The **Chronology** tab shows all 78 extracted peaks plotted against time
3. Click the **Frequency** tab to see the empirical frequency curve of the extracted peaks
4. Click the **Seasonality** tab to see when peaks occur -- concentrated in winter months

![Chronology of POT events extracted from Orestimba Creek](../images/usgs-pot-chronology.png)
*Figure 3: Chronology of 78 peaks over the 650 cfs threshold*

![Frequency plot of POT events](../images/usgs-pot-frequency.png)
*Figure 4: Empirical frequency curve for POT events*

### Understanding the POT Configuration

The POT extraction uses two key parameters:

**Threshold (650 cfs):** Only daily discharge values exceeding 650 cfs are considered as potential peaks. This threshold was selected using the threshold diagnostic plots (MRL, modified scale, and shape stability).

**Minimum Steps Between Peaks (5 days):** After identifying a peak above the threshold, the algorithm requires at least 5 days before the next peak can be selected. This ensures that each extracted event represents an independent flood, not a secondary crest within the same storm.

**Result:** 78 independent flood events extracted from 94 years of record, yielding an average event rate of approximately 0.83 events per year. Some years have no events (the creek did not reach 650 cfs), while particularly wet years may have two or more independent floods.

### Viewing the Threshold Diagnostic Plots

RMC-BestFit provides three diagnostic plots to help select an appropriate threshold:

1. **MRL (Mean Residual Life) Plot** -- Click the **MRL** tab. This plot shows the mean excess over the threshold as a function of threshold level. A roughly linear relationship above a certain threshold suggests that the Generalized Pareto distribution is a reasonable model for the exceedances. The selected threshold should be in the region where the MRL plot appears approximately linear.

2. **Modified Scale Stability Plot** -- Click the **Modified Scale** tab. This plot shows the estimated scale parameter (adjusted for threshold) as a function of threshold. The parameter should be roughly constant above an appropriate threshold.

3. **Shape Stability Plot** -- Click the **Shape** tab. This plot shows the estimated shape parameter as a function of threshold. Like the scale plot, the shape parameter should stabilize above an appropriate threshold.

![MRL plot for threshold selection](../images/usgs-pot-mrl.png)
*Figure 5: Mean Residual Life plot supporting the 650 cfs threshold*

![Modified scale stability plot](../images/usgs-pot-modified-scale.png)
*Figure 6: Modified scale stability plot*

![Shape stability plot](../images/usgs-pot-shape.png)
*Figure 7: Shape stability plot*

### Viewing the Properties Panel

1. With the POT Input Data element selected, open the **Properties** panel
2. The panel shows the POT configuration:
   - **Exact Data Method:** Peaks-Over-Threshold Series
   - **Time Series Element:** The source daily discharge element
   - **Threshold:** 650 (cfs)
   - **Min Steps Between Peaks:** 5 (days, matching the daily time interval)

![Properties panel showing POT settings](../images/usgs-pot-properties.png)
*Figure 8: Properties panel for a POT Input Data element*

### Creating Your Own POT Element

To create a new Peaks-Over-Threshold Input Data element:

1. Right-click **Input Data** in the Project Explorer and select **Create New**
2. Enter a descriptive name for the element
3. In the Properties panel:
   - Set **Exact Data Method** to **Peaks-Over-Threshold Series**
   - Select a **Time Series Element** from the dropdown
   - Set the **Threshold** value -- start with a value that captures a reasonable number of events per year (typically 1--5 events per year is a good target)
   - Set **Min Steps Between Peaks** to ensure independence (5--10 days for daily discharge data)
4. Review the threshold diagnostic plots (MRL, Modified Scale, Shape) to confirm your threshold choice
5. Adjust the threshold if the diagnostic plots suggest a different value

**Threshold selection guidance:**

- Too low: Captures noise and dependent events, inflating the sample size and biasing results
- Too high: Too few events, wasting information from the record
- A rate of 1--3 events per year is often a good starting point for streamflow data
- The diagnostic plots provide objective support for threshold selection

## POT vs Block Maximum

| | Peaks-Over-Threshold (this example) | Block Maximum |
|---|---|---|
| **Events per year** | Variable (0, 1, 2, or more) | Exactly 1 |
| **Information use** | More efficient -- uses all large events | Discards second-largest events |
| **Threshold sensitivity** | Requires threshold selection (subjective) | No threshold needed |
| **Distribution model** | Generalized Pareto / Poisson-GP | GEV or other annual max distributions |
| **Best for** | Short records, event-driven hydrology | Long records, well-behaved annual cycle |

For Orestimba Creek, the POT approach is particularly informative because:
- The block maximum series contains many years of zero or very low flow (captured as MGBT low outliers in the [peak download example](../3-peaks-over-threshold/../2-usgs-peak-discharge/usgs-peak-download-example.md))
- The POT approach naturally handles years with no events (they contribute through the Poisson rate) without needing censoring

## Key Settings Reference

| Setting | Value | Notes |
|---------|-------|-------|
| Exact Data Method | Peaks-Over-Threshold Series | Extracts peaks above a threshold from a time series |
| Time Series Element | USGS - 11274500 - Daily Discharge | Source daily discharge |
| Threshold | 650 cfs | Supported by MRL and stability plots |
| Min Steps Between Peaks | 5 days | Ensures hydrological independence |
| Extracted Events | 78 | Over 94 years of record |
| Event Rate | ~0.83 per year | Some years have no events above the threshold |

## Next Steps

- **Distribution Fitting** -- Fit the Generalized Pareto distribution (or other distributions) to the POT series
- **Univariate Distribution Analysis** -- Perform a point process or Poisson-GP frequency analysis to estimate return levels
- **Compare with Annual Maxima** -- See the [USGS Peak Discharge example](../2-usgs-peak-discharge/usgs-peak-download-example.md) to compare POT results with annual maximum frequency analysis for the same station
- **Precipitation POT** -- See the [GHCN POT example](ghcn-peaks-over-threshold-example.md) for a POT analysis applied to daily precipitation data
