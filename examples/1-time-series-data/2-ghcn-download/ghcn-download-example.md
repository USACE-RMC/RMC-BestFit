# GHCN Download Example

## Overview

This example demonstrates downloading daily climate observations from the **NOAA Global Historical Climatology Network (GHCN)**. It includes precipitation and snowfall records from cooperative observer stations in California.

GHCN data is useful for precipitation frequency analysis, snowmelt modeling inputs, and climate trend studies. RMC-BestFit connects to the NOAA National Centers for Environmental Information (NCEI) API to download daily precipitation and snow depth records.

## Data Source

- **Agency:** NOAA National Centers for Environmental Information (NCEI)
- **Network:** Global Historical Climatology Network - Daily (GHCN-D)
- **Website:** https://www.ncei.noaa.gov/products/land-based-station/global-historical-climatology-network-daily
- **Entry Method in BestFit:** `GHCN`
- **Required Parameter:** GHCN Station ID (11 characters)
- **Additional Parameter:** Depth Unit (Inches, Millimeters, or Centimeters)

### GHCN Series Types

| Series Type | Description | Use Case |
|-------------|-------------|----------|
| Daily Precipitation | Total daily precipitation depth | Precipitation frequency analysis, IDF curves |
| Daily Snow | Total daily snowfall depth | Snow accumulation studies, seasonal analysis |

### GHCN Station ID Format

GHCN station IDs are 11 characters long. For U.S. Cooperative Observer (COOP) stations, the format is:
- `USC` + 8-digit COOP station number (e.g., `USC00040741`)

You can search for stations at the NOAA Climate Data Online portal: https://www.ncdc.noaa.gov/cdo-web/

## What's Inside

This project contains 2 time series elements from cooperative observer stations in California:

| Element | Station | Series Type | Units |
|---------|---------|-------------|-------|
| GHCN - USC00040741 - Daily Precipitation | Big Bear Lake, CA | DailyPrecipitation | inches |
| GHCN - USC00046685 - Daily Snow | Paradise, CA | DailySnow | inches |

### Station Details

**Big Bear Lake, CA (USC00040741)** -- Located in the San Bernardino Mountains at approximately 6,790 ft elevation. Mountain climate with significant winter precipitation.

**Paradise, CA (USC00046685)** -- Located in the Sierra Nevada foothills of Butte County. Receives measurable snowfall in winter months.

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/1-time-series-data/2-ghcn-download/`
3. Open `ghcn-download-example.bestfit`
4. The Project Explorer will show 2 elements under **Time Series Data**

![RMC-BestFit Project Explorer showing the 2 GHCN time series elements](../images/ghcn-project-explorer.png)
*Figure 1: Project Explorer with GHCN time series elements*

### Exploring Daily Precipitation (Big Bear Lake)

1. Click **GHCN - USC00040741 - Daily Precipitation** in the Project Explorer
2. The **Time Series** tab displays the daily precipitation record
3. Notice the episodic nature of precipitation -- many zero values with occasional storms
4. Click the **Seasonality** tab to see the wet season (winter) and dry season (summer) pattern typical of California's Mediterranean climate

![Time series plot showing daily precipitation for Big Bear Lake, CA](../images/ghcn-precipitation-ts-plot.png)
*Figure 2: Daily precipitation for Big Bear Lake, CA (GHCN USC00040741)*

![Seasonality plot for Big Bear Lake precipitation showing winter wet season](../images/ghcn-precipitation-seasonality.png)
*Figure 3: Seasonality plot -- California's Mediterranean climate with winter-dominant precipitation*

### Exploring Daily Snowfall (Paradise)

1. Click **GHCN - USC00046685 - Daily Snow** in the Project Explorer
2. The **Time Series** tab shows daily snowfall amounts
3. Values are zero throughout most of the year, with snowfall concentrated in winter months
4. The **Seasonality** tab clearly shows the November-March snow season

![Time series plot showing daily snowfall for Paradise, CA](../images/ghcn-snow-ts-plot.png)
*Figure 4: Daily snowfall for Paradise, CA (GHCN USC00046685)*

### Viewing the Properties Panel

1. With a GHCN element selected, open the **Properties** panel
2. The panel shows GHCN-specific configuration:
   - **Entry Method:** GHCN
   - **GHCN Site Number:** The 11-character station ID
   - **Data Type:** Daily Precipitation or Daily Snow
   - **Depth Unit:** The unit for reported values (Inches in this example)
   - **Download** button to refresh the data

![Properties panel showing GHCN site number, data type, depth unit, and Download button](../images/ghcn-properties-panel.png)
*Figure 5: Properties panel for a GHCN time series element*

### Downloading Your Own GHCN Data

To create a new GHCN time series element:

1. Right-click **Time Series Data** in the Project Explorer and select **Create New**
2. In the Properties panel, set **Entry Method** to **GHCN**
3. Enter a valid **GHCN Station ID** (11 characters, e.g., `USC00040741`)
4. Select the **Data Type** (Daily Precipitation or Daily Snow)
5. Select the **Depth Unit** (Inches, Millimeters, or Centimeters)
6. Click **Download**

## Key Settings

| Setting | Value in This Example | Notes |
|---------|----------------------|-------|
| Entry Method | GHCN | Connects to NOAA NCEI web services |
| Station ID | 11 characters (e.g., `USC00040741`) | Must be a valid GHCN-D station |
| Data Type | DailyPrecipitation or DailySnow | Only two series types available for GHCN |
| Depth Unit | Inches | Also supports Millimeters and Centimeters |
| Time Interval | OneDay | All GHCN data is daily resolution |

## Next Steps

- **Create Input Data:** Reference the daily precipitation time series in an Input Data element, then apply a block function to extract annual maximum daily precipitation for frequency analysis
- **Precipitation Frequency Analysis:** Use annual maxima with a Univariate Analysis element to estimate precipitation quantiles (e.g., 100-year 24-hour precipitation)
- **Seasonal Analysis:** Use the monthly block function to analyze precipitation by season or month
