# CHMN Download Example

## Overview

This example demonstrates downloading hydrometric data from the **Water Survey of Canada (WSC)**, accessed through the Canadian Hydrometric Monitoring Network (CHMN). It showcases all six CHMN series types using data from a single station on the Lillooet River in British Columbia.

The Water Survey of Canada operates over 2,800 active hydrometric stations across Canada. RMC-BestFit connects to the WSC Real-Time Hydrometric Data API and Historical Data Archive to download daily, instantaneous, and peak flow records.

## Data Source

- **Agency:** Water Survey of Canada (WSC), Environment and Climate Change Canada
- **API:** Real-Time Hydrometric Data / Historical Hydrometric Data
- **Website:** https://wateroffice.ec.gc.ca
- **Entry Method in BestFit:** `CHMN`
- **Required Parameter:** Station ID (7 characters)

### CHMN Station ID Format

Canadian hydrometric station IDs are 7 characters:
- First 2 digits: major drainage area code
- Next 2 characters: sub-basin identifier (letter + letter)
- Last 3 digits: station number within the sub-basin

Example: `08MG005` = Major drainage area 08 (Pacific), sub-basin MG (Lillooet), station 005.

### CHMN Series Types

| Series Type | Description | Typical Interval | Use Case |
|-------------|-------------|-------------------|----------|
| Daily Discharge | Daily mean streamflow | 1 day | Long-term flow statistics, flood frequency |
| Daily Stage | Daily mean water level | 1 day | Long-term stage statistics |
| Instantaneous Discharge | Real-time (typically 5-min) streamflow | 5 minutes | High-resolution flood hydrographs |
| Instantaneous Stage | Real-time water level | 5 minutes | High-resolution stage records |
| Peak Discharge | Annual peak instantaneous discharge | Irregular (annual) | Direct input to flood frequency analysis |
| Peak Stage | Annual peak water level | Irregular (annual) | Peak stage frequency analysis |

## What's Inside

This project contains 6 time series elements, all from the same station:

| Element | Series Type | Units |
|---------|-------------|-------|
| CHMN - 08MG005 - Daily Discharge | DailyDischarge | cms |
| CHMN - 08MG005 - Daily Stage | DailyStage | m |
| CHMN - 08MG005 - Instanteneous Discharge | InstantaneousDischarge | cms |
| CHMN - 08MG005 - Instantaneous Stage | InstantaneousStage | m |
| CHMN - 08MG005 - Peak Discharge | PeakDischarge | cms |
| CHMN - 08MG005 - Peak Stage | PeakStage | m |

### Station Details

**Lillooet River near Pemberton, BC (08MG005)** -- Located on the Lillooet River in the Coast Mountains of British Columbia. Drains a large glacierized catchment with a pronounced spring/summer snowmelt and glacial melt season. Part of the Reference Hydrometric Basin Network (RHBN), a set of minimally impacted stations used for climate change monitoring. One of the longest continuous hydrometric records in British Columbia.

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/1-time-series-data/3-chmn-download/`
3. Open `chmn-download-example.bestfit`
4. The Project Explorer will show 6 elements under **Time Series Data**

![RMC-BestFit Project Explorer showing all 6 CHMN time series elements for station 08MG005](../images/chmn-project-explorer.png)
*Figure 1: Project Explorer with all CHMN time series elements*

### Exploring Daily Discharge

1. Click **CHMN - 08MG005 - Daily Discharge** in the Project Explorer
2. The **Time Series** tab displays the daily streamflow record beginning in 1914
3. Notice the strong seasonal pattern: low winter baseflow and high summer flows from snowmelt and glacial melt
4. Click the **Seasonality** tab to see the annual flow cycle peaking in June-July
5. The **ACF** tab shows strong serial correlation typical of daily streamflow

![Time series plot showing daily discharge for Lillooet River](../images/chmn-daily-discharge-ts-plot.png)
*Figure 2: Daily discharge for Lillooet River near Pemberton, BC (CHMN 08MG005)*

![Seasonality plot for Lillooet River showing summer snowmelt/glacial melt peak](../images/chmn-daily-discharge-seasonality.png)
*Figure 3: Seasonality plot -- glacial-fed river with June-July peak typical of Coast Mountain catchments*

### Exploring Peak Discharge

1. Click **CHMN - 08MG005 - Peak Discharge** in the Project Explorer
2. This element contains annual peak instantaneous discharge values
3. Peak data is the most commonly used input for flood frequency analysis and can be used directly in a Univariate Analysis element without extracting annual maxima from daily data

![Time series plot showing annual peak discharge values for Lillooet River](../images/chmn-peak-discharge-ts-plot.png)
*Figure 4: Annual peak discharge for Lillooet River (CHMN 08MG005)*

### Exploring Instantaneous Data

1. Click **CHMN - 08MG005 - Instanteneous Discharge** in the Project Explorer
2. This element contains observations at 5-minute intervals from the WSC real-time data feed
3. The instantaneous data captures the full shape of individual flood hydrographs
4. Note: instantaneous data from WSC is typically limited to the recent real-time period

![Time series plot showing 5-minute instantaneous discharge for Lillooet River](../images/chmn-instantaneous-discharge-ts-plot.png)
*Figure 5: Instantaneous (5-minute) discharge for Lillooet River (CHMN 08MG005)*

### Viewing the Properties Panel

1. With any element selected, open the **Properties** panel
2. The panel shows CHMN-specific configuration:
   - **Entry Method:** CHMN
   - **CHMN Site Number:** The 7-character station ID
   - **Data Type:** The selected series type
   - **Download** button to refresh the data

![Properties panel showing CHMN site number, data type dropdown, and Download button](../images/chmn-properties-panel.png)
*Figure 6: Properties panel for a CHMN time series element*

### Downloading Your Own CHMN Data

To create a new CHMN time series element:

1. Right-click **Time Series Data** in the Project Explorer and select **Create New**
2. In the Properties panel, set **Entry Method** to **CHMN**
3. Enter a valid **CHMN Station ID** (7 characters, e.g., `08MG005`)
4. Select the desired **Data Type** from the dropdown
5. Click **Download**

You can search for Canadian hydrometric stations at https://wateroffice.ec.gc.ca

## Key Settings

| Setting | Value in This Example | Notes |
|---------|----------------------|-------|
| Entry Method | CHMN | Connects to Water Survey of Canada web services |
| Station ID | 7 characters (`08MG005`) | Format: 2 digits + 2 letters + 3 digits |
| Time Interval | OneDay (daily), FiveMinute (instantaneous), or Irregular (peak) | Automatically detected from downloaded data |

## Next Steps

- **Flood Frequency Analysis:** Use the peak discharge series directly in a Univariate Analysis element for flood frequency estimation
- **Create Input Data from Daily Series:** Alternatively, reference the daily discharge in an Input Data element and apply an annual maximum block function to extract peaks
- **Compare Methods:** Compare frequency estimates from the official WSC peak series versus annual maxima extracted from the daily record
- **Time Series Analysis:** Fit an ARIMA model to the daily discharge to analyze temporal structure
