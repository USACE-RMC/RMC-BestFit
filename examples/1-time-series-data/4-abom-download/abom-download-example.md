# ABOM Download Example

## Overview

This example demonstrates downloading hydrological data from the **Australian Bureau of Meteorology (BOM) Water Data Online** API. It includes daily and instantaneous series for discharge, stage, and precipitation from three monitoring stations in the Australian Capital Territory (ACT) and New South Wales (NSW).

The Bureau of Meteorology collects water data from over 3,500 stations across Australia. RMC-BestFit connects to the BOM Water Data Online API to download streamflow, water level, and precipitation records.

## Data Source

- **Agency:** Australian Bureau of Meteorology (BOM)
- **API:** Water Data Online
- **Website:** http://www.bom.gov.au/waterdata/
- **Entry Method in BestFit:** `ABOM`
- **Required Parameter:** Station ID (6 digits)
- **Additional Parameter:** Depth Unit (for precipitation series only)

### ABOM Series Types

| Series Type | Description | Typical Interval | Use Case |
|-------------|-------------|-------------------|----------|
| Daily Discharge | Daily mean streamflow | 1 day | Long-term flow statistics, flood frequency |
| Daily Stage | Daily mean water level | 1 day | Long-term stage statistics |
| Daily Precipitation | Daily rainfall total | 1 day | Rainfall frequency analysis |
| Instantaneous Discharge | Irregular-interval streamflow | Variable | High-resolution flood hydrographs |
| Instantaneous Stage | Irregular-interval water level | Variable | High-resolution stage records |

## What's Inside

This project contains 6 time series elements from 3 BOM stations:

| Element | Station | Series Type | Units |
|---------|---------|-------------|-------|
| ABOM - 410730 - Daily Precipitation | Cotter River at Gingera, ACT | DailyPrecipitation | mm |
| ABOM - 410730 - Daily Discharge | Cotter River at Gingera, ACT | DailyDischarge | cms |
| ABOM - 410761 - Daily Discharge | Murrumbidgee R. below Lobbs Hole Ck, ACT/NSW | DailyDischarge | cms |
| ABOM - 409202 - Daily Stage | Murray River at Tocumwal, NSW | DailyStage | m |
| ABOM - 410730 - Instantaneous Discharge | Cotter River at Gingera, ACT | InstantaneousDischarge | cms |
| ABOM - 409202 - Instantaneous Stage | Murray River at Tocumwal, NSW | InstantaneousStage | m |

### Station Details

**Cotter River at Gingera, ACT (410730)** -- Headwater catchment in the Australian Capital Territory. Feeds the Cotter Reservoir, part of Canberra's water supply system. This station demonstrates both discharge and precipitation data from the same location.

**Murrumbidgee River below Lobbs Hole Creek, ACT/NSW (410761)** -- Larger catchment on the Murrumbidgee River, downstream of the ACT/NSW border region.

**Murray River at Tocumwal, NSW (409202)** -- Major station on the Murray River in southern NSW, near the Victorian border. A key regulatory station for Murray-Darling Basin water management. Demonstrates stage (water level) data for a large regulated river.

### Note on Missing Data

Some daily series from BOM may contain missing data (NaN values). This is common in Australian records where equipment failures, station closures, or data quality flags result in gaps. RMC-BestFit handles NaN values by excluding them from statistical calculations and displaying gaps in the time series plot.

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/1-time-series-data/4-abom-download/`
3. Open `abom-download-example.bestfit`
4. The Project Explorer will show 6 elements under **Time Series Data**

![RMC-BestFit Project Explorer showing all 6 ABOM time series elements](../images/abom-project-explorer.png)
*Figure 1: Project Explorer with all ABOM time series elements*

### Exploring Daily Precipitation (Cotter River)

1. Click **ABOM - 410730 - Daily Precipitation** in the Project Explorer
2. The **Time Series** tab displays the daily rainfall record
3. Notice the episodic rainfall pattern with occasional high-intensity events
4. Click the **Seasonality** tab to observe the seasonal rainfall distribution

![Time series plot showing daily precipitation for Cotter River at Gingera](../images/abom-precipitation-ts-plot.png)
*Figure 2: Daily precipitation for Cotter River at Gingera, ACT (BOM 410730)*

### Exploring Daily Discharge (Cotter River)

1. Click **ABOM - 410730 - Daily Discharge** in the Project Explorer
2. The time series plot will show gaps where missing (NaN) values occur
3. Click the **Seasonality** tab to see the seasonal flow pattern

![Time series plot showing daily discharge for Cotter River with visible data gaps](../images/abom-daily-discharge-ts-plot.png)
*Figure 3: Daily discharge for Cotter River at Gingera, ACT (BOM 410730)*

### Exploring Instantaneous Stage (Murray River)

1. Click **ABOM - 409202 - Instantaneous Stage** in the Project Explorer
2. This element contains observations at irregular intervals spanning decades
3. The Murray River at Tocumwal shows the regulated flow pattern of a major river system

![Time series plot showing instantaneous stage for Murray River at Tocumwal](../images/abom-instantaneous-stage-ts-plot.png)
*Figure 4: Instantaneous water level for Murray River at Tocumwal, NSW (BOM 409202)*

### Viewing the Properties Panel

1. With any element selected, open the **Properties** panel
2. The panel shows ABOM-specific configuration:
   - **Entry Method:** ABOM
   - **ABOM Site Number:** The 6-digit station ID
   - **Data Type:** The selected series type
   - **Depth Unit:** Shown only for Daily Precipitation (Millimeters in this example)
   - **Download** button to refresh the data

![Properties panel showing ABOM site number, data type dropdown, and Download button](../images/abom-properties-panel.png)
*Figure 5: Properties panel for an ABOM time series element*

### Downloading Your Own ABOM Data

To create a new ABOM time series element:

1. Right-click **Time Series Data** in the Project Explorer and select **Create New**
2. In the Properties panel, set **Entry Method** to **ABOM**
3. Enter a valid **ABOM Station ID** (6 digits, e.g., `410730`)
4. Select the desired **Data Type** from the dropdown
5. For Daily Precipitation, also select the **Depth Unit** (Millimeters, Centimeters, or Inches)
6. Click **Download**

You can search for BOM water monitoring stations at http://www.bom.gov.au/waterdata/

## Key Settings

| Setting | Value in This Example | Notes |
|---------|----------------------|-------|
| Entry Method | ABOM | Connects to BOM Water Data Online API |
| Station ID | 6 digits (e.g., `410730`) | Must be a valid BOM hydrological station |
| Data Type | Varies by element | 5 series types available for ABOM |
| Depth Unit | Millimeters | Only shown for DailyPrecipitation; also supports Inches and Centimeters |
| Time Interval | OneDay (daily) or Irregular (instantaneous) | Automatically detected from downloaded data |

## Next Steps

- **Create Input Data:** Reference a daily discharge time series in an Input Data element and apply an annual maximum block function to extract the annual flood series
- **Precipitation Frequency Analysis:** Use the daily precipitation record for rainfall frequency estimation
- **Compare Catchments:** With discharge records from two catchments (410730 and 410761), compare flood frequency characteristics between a small headwater and a larger downstream basin
