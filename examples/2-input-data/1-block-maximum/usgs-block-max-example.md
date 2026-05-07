# Block Maximum Example

## Overview

This example demonstrates extracting annual maximum values from a daily discharge time series using the **Block Maximum** method. Two Input Data elements are created from the same source time series -- one using calendar year blocks (January--December) and one using water year blocks (October--September) -- to illustrate how the choice of time block affects the resulting annual maximum series.

Block maximum extraction is the most common method for creating input data for flood frequency analysis. It divides the continuous daily record into fixed-length blocks and returns the maximum value within each block, producing exactly one observation per year.

## Data Source

- **Station:** USGS 01134500, Moose River at Victory, VT
- **Source Time Series:** Daily mean discharge, downloaded from the USGS NWIS API
- **Record:** 1947--present
- **Units:** Cubic feet per second (cfs)

The Moose River at Victory is a perennial stream in northeastern Vermont, draining a 75 square mile watershed in the Green Mountains. Spring snowmelt typically produces the annual peak flow, usually occurring between March and May.

## What's Inside

Open `usgs-block-max-example.bestfit` in RMC-BestFit. The Project Explorer shows the following elements:

| Element | Type | Description |
|---------|------|-------------|
| USGS - 01134500 - Daily Discharge | Time Series Data | Daily mean discharge from USGS NWIS |
| USGS - 01134500 - Block Max - Calendar Year | Input Data | Annual maximum using January--December blocks |
| USGS - 01134500 - Block Max - Water Year | Input Data | Annual maximum using October--September blocks |

![Project Explorer showing the time series and two block maximum Input Data elements](../images/block-max-project-explorer.png)
*Figure 1: Project Explorer with time series and block maximum elements*

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/2-input-data/1-block-maximum/`
3. Open `usgs-block-max-example.bestfit`
4. Expand **Time Series Data** and **Input Data** in the Project Explorer

### Exploring the Source Time Series

1. Click **USGS - 01134500 - Daily Discharge** in the Project Explorer
2. The **Time Series** tab displays the full daily hydrograph spanning 1947 to present
3. Click the **Seasonality** tab to see the annual cycle -- note the spring snowmelt peak between March and May, which is typical of New England rivers

![Daily discharge hydrograph for Moose River at Victory, VT](../images/block-max-daily-discharge.png)
*Figure 2: Daily discharge hydrograph for Moose River at Victory, VT*

### Exploring the Calendar Year Block Maximum

1. Click **USGS - 01134500 - Block Max - Calendar Year** in the Project Explorer
2. The **Chronology** tab shows the extracted annual maximum series plotted against year
3. Click the **Frequency** tab to see the empirical frequency curve (plotting positions)
4. Click the **Seasonality** tab to see when annual maxima occur -- most fall in spring (March--May)
5. The **Density**, **Histogram**, and **Q-Q** tabs provide additional views of the sample distribution
6. The **ACF** and **PACF** tabs show the autocorrelation structure of the annual maximum series

![Chronology plot of calendar year annual maxima](../images/block-max-calendar-chronology.png)
*Figure 3: Calendar year annual maximum series*

![Frequency plot showing empirical plotting positions](../images/block-max-calendar-frequency.png)
*Figure 4: Empirical frequency curve for calendar year annual maxima*

### Exploring the Water Year Block Maximum

1. Click **USGS - 01134500 - Block Max - Water Year** in the Project Explorer
2. Compare the chronology with the calendar year series -- values may differ in years where the annual peak occurs near the January boundary
3. The water year (October 1 through September 30) is the standard block used in U.S. flood frequency practice because it keeps the winter-spring flood season within a single year

![Chronology plot of water year annual maxima](../images/block-max-wateryear-chronology.png)
*Figure 5: Water year annual maximum series*

### Comparing Calendar Year vs Water Year

The calendar year and water year series will often be identical for stations where annual peaks occur far from the year boundary. For the Moose River, spring snowmelt peaks (March--May) fall well within both the calendar year and the water year, so the two series are nearly identical. However, for stations with winter flood seasons, the choice of time block can significantly affect the results.

**Why water year is preferred for flood frequency analysis:**

- In the U.S., the water year begins on October 1 and ends on September 30
- This keeps the primary flood season (typically winter through spring) within a single year
- Calendar year blocks can split a single flood season across two years, potentially missing the true annual maximum or double-counting events
- Bulletin 17C and most U.S. flood frequency guidelines specify the water year

### Viewing the Properties Panel

1. With either Input Data element selected, open the **Properties** panel
2. The panel shows the block maximum configuration:
   - **Exact Data Method:** Time Series
   - **Time Series Element:** The source daily discharge element
   - **Block Function:** Maximum
   - **Time Block:** Calendar Year or Water Year
   - **Start Month / End Month:** Defines the custom block boundaries (relevant when Time Block is set to Custom Year)

![Properties panel showing block maximum settings](../images/block-max-properties.png)
*Figure 6: Properties panel for a block maximum Input Data element*

### Creating Your Own Block Maximum Element

To create a new block maximum Input Data element:

1. Right-click **Input Data** in the Project Explorer and select **Create New**
2. Enter a descriptive name for the element
3. In the Properties panel:
   - Set **Exact Data Method** to **Time Series**
   - Select a **Time Series Element** from the dropdown (must already exist in the project)
   - Set **Block Function** to **Maximum** (or Minimum, Mean)
   - Set **Time Block** to **Water Year**, **Calendar Year**, or **Custom Year**
   - If using Custom Year, set the **Start Month** and **End Month**
4. The Input Data element will automatically process the time series and extract the block maximum values

## Block Maximum vs USGS Peak Download

There are two ways to obtain annual peak flow data for flood frequency analysis:

| Approach | Source | Peak Type | Requires Time Series |
|----------|--------|-----------|---------------------|
| **Block Maximum** (this example) | Daily mean discharge time series | Daily maximum | Yes |
| **USGS Peak Download** ([see example](../2-usgs-peak-discharge/usgs-peak-download-example.md)) | USGS peak-flow database | Instantaneous peak | No |

The USGS peak discharge values are the single highest *instantaneous* streamflow each water year, which is always greater than or equal to the daily mean maximum. For most applications, the USGS peak download is preferred because it captures the true peak. Block maximum from daily data is useful when peak data are not available, when working with non-USGS data sources, or when analyzing other variables (precipitation, stage, temperature).

## Key Settings Reference

| Setting | Calendar Year | Water Year | Notes |
|---------|--------------|------------|-------|
| Exact Data Method | Time Series | Time Series | Both reference the same daily discharge |
| Block Function | Maximum | Maximum | Also supports Minimum, Mean |
| Time Block | Calendar Year | Water Year | Water year: Oct 1 -- Sep 30 |
| Start Month | 1 (January) | 10 (October) | Defines block start |
| End Month | 12 (December) | 9 (September) | Defines block end |

## Next Steps

- **Distribution Fitting** -- Fit multiple distributions (Normal, Log-Normal, GEV, LP3, etc.) to the annual maximum series and compare goodness-of-fit
- **Univariate Distribution Analysis** -- Perform Bayesian frequency analysis with a selected distribution to estimate flood quantiles with full uncertainty
- **Bulletin 17C** -- For USGS regulatory applications, use the water year block maximum series as input to a Bulletin 17C analysis with the Log-Pearson Type III distribution
- **Compare with Peak Download** -- See the [USGS Peak Discharge example](../2-usgs-peak-discharge/usgs-peak-download-example.md) to compare block maxima from daily data with official USGS instantaneous peaks
