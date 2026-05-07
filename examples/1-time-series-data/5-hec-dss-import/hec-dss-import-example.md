# HEC-DSS Import Example

## Overview

This example demonstrates importing time series data from a **HEC-DSS** file. HEC-DSS (Data Storage System) is the standard binary file format used by the U.S. Army Corps of Engineers Hydrologic Engineering Center (HEC) suite of software, including HEC-HMS, HEC-RAS, and HEC-ResSim.

The example contains hourly inflow and outflow hydrographs for Grapevine Dam from a reservoir simulation. It also demonstrates the **Alternative Time Series** feature, which allows overlaying multiple time series on the same plot for visual comparison.

## Data Source

- **Format:** HEC-DSS (`.dss` binary file)
- **File:** `dss-example-data.dss` (included in this folder)
- **Entry Method in BestFit:** `HECDSS`
- **Required Parameters:** DSS file path + DSS data pathname

### HEC-DSS Pathname Format

HEC-DSS organizes data using a 6-part pathname:
```
/A-Part/B-Part/C-Part/D-Part/E-Part/F-Part/
```

| Part | Meaning | Example Value |
|------|---------|---------------|
| A | Project or basin | (empty in this example) |
| B | Location | `GRAPEVINE INFLOW` or `GRAPEVINE LAKE-RELEASE` |
| C | Parameter | `FLOW` |
| D | Date range | Auto-detected by BestFit |
| E | Time interval | `1Hour` |
| F | Version or run label | `RUN:2007_MAR-JUL` |

## What's Inside

This project contains 2 time series elements imported from the same DSS file:

| Element | DSS Pathname (B/C parts) | Units |
|---------|--------------------------|-------|
| Grapevine Dam - Inflow | `GRAPEVINE INFLOW/FLOW` | cfs |
| Grapevine Dam - Outflow | `GRAPEVINE LAKE-RELEASE/FLOW` | cfs |

### About the Data

This dataset represents a reservoir routing simulation for Grapevine Dam (Trinity River basin, Texas). The inflow hydrograph shows the flood entering the reservoir, while the outflow (release) hydrograph shows the controlled release from the dam. Comparing the two demonstrates how the reservoir attenuates flood peaks.

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/1-time-series-data/5-hec-dss-import/`
3. Open `hec-dss-import-example.bestfit`
4. The Project Explorer will show 2 elements under **Time Series Data**

![RMC-BestFit Project Explorer showing the 2 HEC-DSS time series elements](../images/hec-dss-project-explorer.png)
*Figure 1: Project Explorer with HEC-DSS time series elements*

### Exploring the Inflow Hydrograph

1. Click **Grapevine Dam - Inflow** in the Project Explorer
2. The **Time Series** tab displays the hourly inflow hydrograph
3. Notice the sharp flood peak followed by a gradual recession

![Time series plot showing the hourly inflow hydrograph for Grapevine Dam](../images/hec-dss-inflow-ts-plot.png)
*Figure 2: Hourly inflow hydrograph for Grapevine Dam*

### Comparing Inflow and Outflow with Alternative Time Series

RMC-BestFit's **Alternative Time Series** feature lets you overlay another time series on the same plot:

1. With the **Grapevine Dam - Inflow** element selected, look at the top-left of the time series plot
2. Find the **Alternative Time Series** selector (dropdown)
3. Select **Grapevine Dam - Outflow** from the dropdown
4. The outflow hydrograph will be overlaid on the inflow plot, visually demonstrating the reservoir's flood attenuation effect

![Time series plot showing inflow and outflow hydrographs overlaid using the Alternative Time Series feature](../images/hec-dss-inflow-outflow-comparison.png)
*Figure 3: Inflow vs. outflow -- the Alternative Time Series feature shows how the reservoir attenuates the flood peak*

### Viewing the Properties Panel

1. With an element selected, open the **Properties** panel
2. The panel shows HEC-DSS-specific configuration:
   - **Entry Method:** HEC-DSS
   - **Full File Path:** Path to the `.dss` file
   - **DSS Pathname:** The 6-part pathname identifying the dataset within the DSS file
   - **Import** button to re-import from the DSS file

![Properties panel showing HEC-DSS file path, DSS pathname, and Import button](../images/hec-dss-properties-panel.png)
*Figure 4: Properties panel for a HEC-DSS time series element*

### Importing Your Own HEC-DSS Data

To create a new HEC-DSS time series element:

1. Right-click **Time Series Data** in the Project Explorer and select **Create New**
2. In the Properties panel, set **Entry Method** to **HEC-DSS**
3. Click **Browse** to select a `.dss` file
4. The **DSS Path Selector** window will open, displaying all available pathnames in the file
5. Select the desired dataset and click **OK**
6. Click **Import** to load the data

![DSS Path Selector window showing available pathnames in the DSS file](../images/hec-dss-path-selector.png)
*Figure 5: DSS Path Selector window -- browse and select datasets from a HEC-DSS file*

## Key Settings

| Setting | Value in This Example | Notes |
|---------|----------------------|-------|
| Entry Method | HEC-DSS | Reads from a local `.dss` file using the Hec.Dss library |
| DSS File | `dss-example-data.dss` | Must be accessible on the local filesystem |
| DSS Pathname | 6-part path (e.g., `//GRAPEVINE INFLOW/FLOW//1Hour/RUN:2007_MAR-JUL/`) | Identifies the specific dataset within the DSS file |
| Time Interval | OneHour | Automatically detected from the DSS data |

## Next Steps

- **Reservoir Analysis:** Compare inflow and outflow statistics to quantify flood attenuation performance
- **Create Input Data:** Reference the inflow time series in an Input Data element to extract peak values for frequency analysis
- **Time Series Analysis:** Fit ARIMA models to the hourly inflow series to characterize the temporal structure of flood events
- **Import Additional Data:** Use the same `.dss` file to import stage, precipitation, or other parameters stored alongside the flow data
