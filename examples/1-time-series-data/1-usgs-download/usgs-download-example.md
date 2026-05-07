# USGS Download Example

## Overview

This example demonstrates downloading time series data from the **U.S. Geological Survey (USGS) National Water Information System (NWIS)** API. It showcases all eight USGS series types available in RMC-BestFit, using data from four gaging stations across the eastern United States.

The USGS NWIS is the most commonly used data source for flood frequency analysis in the United States. RMC-BestFit connects directly to the USGS web services to download daily, instantaneous, peak, and field-measured data.

## Data Source

- **Agency:** U.S. Geological Survey (USGS)
- **API:** National Water Information System (NWIS) Web Services
- **Website:** https://waterdata.usgs.gov
- **Entry Method in BestFit:** `USGS`
- **Required Parameter:** USGS Site Number (8-15 digits)

### USGS Series Types

| Series Type | Description | Typical Interval | Use Case |
|-------------|-------------|-------------------|----------|
| Daily Discharge | Daily mean streamflow | 1 day | Long-term flow statistics, annual maxima extraction |
| Daily Stage | Daily mean gage height | 1 day | Long-term stage statistics |
| Instantaneous Discharge | Unit-value (typically 15-min) streamflow | Irregular | High-resolution flood hydrographs |
| Instantaneous Stage | Unit-value gage height | Irregular | High-resolution stage records |
| Peak Discharge | Annual peak instantaneous streamflow | 1 per year | Direct input to flood frequency analysis |
| Peak Stage | Annual peak gage height | 1 per year | Peak stage frequency analysis |
| Measured Discharge | Individual field measurements (rated) | Irregular | Rating curve development, QA checks |
| Measured Stage | Gage height at time of field measurement | Irregular | Paired with measured discharge for rating curves |

## What's Inside

This project contains 8 time series elements from 4 USGS gaging stations:

| Element | Station | Series Type | Units |
|---------|---------|-------------|-------|
| USGS - 01134500 - Daily Discharge | Moose River at Victory, VT | DailyDischarge | cfs |
| USGS - 07010000 - Daily Stage | Mississippi River at St. Louis, MO | DailyStage | ft |
| USGS - 01646500 - Instantaneous Discharge | Potomac River near Washington, DC | InstantaneousDischarge | cfs |
| USGS - 01646500 - Instantaneous Stage | Potomac River near Washington, DC | InstantaneousStage | ft |
| USGS - 01614000 - Peak Discharge | Back Creek near Jones Springs, WV | PeakDischarge | cfs |
| USGS - 01614000 - Peak Stage | Back Creek near Jones Springs, WV | PeakStage | ft |
| USGS - 01570500 - Measured Discharge | Susquehanna River at Harrisburg, PA | MeasuredDischarge | cfs |
| USGS - 01570500 - Measured Stage | Susquehanna River at Harrisburg, PA | MeasuredStage | ft |

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/1-time-series-data/1-usgs-download/`
3. Open `usgs-download-example.bestfit`
4. The Project Explorer will show 8 elements under **Time Series Data**

![RMC-BestFit Project Explorer showing all 8 USGS time series elements](../images/usgs-project-explorer.png)
*Figure 1: Project Explorer with all USGS time series elements*

### Exploring Daily Discharge (Moose River)

1. Click **USGS - 01134500 - Daily Discharge** in the Project Explorer
2. The **Time Series** tab displays the full daily hydrograph
3. Click the **Seasonality** tab to see the annual cycle of streamflow -- note the spring snowmelt peak typical of New England rivers
4. Click the **ACF** and **PACF** tabs to view the autocorrelation structure of daily flows

![Time series plot showing daily discharge for Moose River at Victory, VT](../images/usgs-daily-discharge-ts-plot.png)
*Figure 2: Daily discharge hydrograph for Moose River at Victory, VT (USGS 01134500)*

![Seasonality plot for Moose River daily discharge showing spring snowmelt peak](../images/usgs-daily-discharge-seasonality.png)
*Figure 3: Seasonality plot for Moose River*

### Exploring Instantaneous Data (Potomac River)

1. Click **USGS - 01646500 - Instantaneous Discharge** in the Project Explorer
2. This element contains unit-value observations at irregular (typically 15-minute) intervals
3. The time series plot may take a moment to render for large datasets
4. Zoom in on individual flood events to see the high-resolution hydrograph shape

![Time series plot showing instantaneous discharge for Potomac River](../images/usgs-instantaneous-discharge-ts-plot.png)
*Figure 4: Instantaneous discharge for Potomac River near Washington, DC (USGS 01646500)*

### Exploring Peak Data (Back Creek)

1. Click **USGS - 01614000 - Peak Discharge** in the Project Explorer
2. This element contains one value per year -- the annual peak instantaneous discharge
3. Peak discharge data is the primary input for flood frequency analysis and can be used directly in a Univariate Analysis element without needing to extract annual maxima from daily data
4. Click **USGS - 01614000 - Peak Stage** to see the corresponding annual peak gage heights

![Time series plot showing annual peak discharge for Back Creek near Jones Springs, WV](../images/usgs-peak-discharge-ts-plot.png)
*Figure 5: Annual peak discharge for Back Creek near Jones Springs, WV (USGS 01614000)*

### Exploring Measured Data (Susquehanna River)

1. Click **USGS - 01570500 - Measured Discharge** in the Project Explorer
2. This element contains individual field measurements taken during site visits -- not a continuous record
3. Measured discharge and stage pairs are commonly used for rating curve development

![Time series plot showing individual field measurements for Susquehanna River](../images/usgs-measured-discharge-ts-plot.png)
*Figure 6: Field-measured discharge for Susquehanna River at Harrisburg, PA (USGS 01570500)*

### Viewing the Properties Panel

1. With any element selected, open the **Properties** panel
2. The panel shows the USGS-specific configuration:
   - **Entry Method:** USGS
   - **USGS Site Number:** The 8-digit site identifier
   - **Data Type:** The selected series type (e.g., Daily Discharge)
   - **Download** button to refresh the data from USGS NWIS

![Properties panel showing USGS site number, data type dropdown, and Download button](../images/usgs-properties-panel.png)
*Figure 7: Properties panel for a USGS time series element*

### Downloading Your Own USGS Data

To create a new USGS time series element from scratch:

1. Right-click **Time Series Data** in the Project Explorer and select **Create New**
2. In the Properties panel, set **Entry Method** to **USGS**
3. Enter a valid **USGS Site Number** (8-15 digits). You can look up site numbers at https://waterdata.usgs.gov
4. Select the desired **Data Type** from the dropdown
5. Click **Download**
6. The time series will be retrieved from the USGS NWIS API and displayed

## Key Settings

| Setting | Value in This Example | Notes |
|---------|----------------------|-------|
| Entry Method | USGS | Connects to USGS NWIS web services |
| Site Number | 8 digits (e.g., `01134500`) | Must be a valid USGS surface water site |
| Time Interval | OneDay (daily), Irregular (instantaneous/measured/peak) | Automatically detected from downloaded data |
| Discharge Unit | CubicFeetPerSecond | Default for USGS data |
| Height Unit | Feet | Default for USGS data |

## Next Steps

- **Flood Frequency Analysis:** Use the peak discharge series directly in a Univariate Analysis element -- no need to extract annual maxima from daily data
- **Create Input Data:** Alternatively, reference the daily discharge time series in an Input Data element, apply an annual maximum block function using Water Year, and extract the annual peak flow series
- **Rating Curve:** Use the measured discharge and stage pair to develop a stage-discharge rating curve with the Rating Curve Analysis element
- **Time Series Analysis:** Fit an ARIMA model to the daily discharge series to analyze temporal dependence structure
