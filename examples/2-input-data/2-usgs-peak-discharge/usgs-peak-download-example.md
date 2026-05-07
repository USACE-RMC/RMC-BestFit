# USGS Peak Discharge Download Example

## Overview

This example demonstrates downloading annual peak instantaneous discharge data directly from the **USGS National Water Information System (NWIS)** peak-flow web service. Unlike the block maximum method, which derives annual peaks from a daily time series, this approach downloads the official USGS annual peak values directly into an Input Data element -- no time series element is needed.

The example includes two contrasting stations: a perennial stream with no low outliers, and an ephemeral stream where the **Multiple Grubbs-Beck Test (MGBT)** identifies nearly half the record as anomalously low peaks. This demonstrates how RMC-BestFit handles censored observations in flood frequency analysis.

## Data Source

- **API:** USGS NWIS Peak-Flow Web Service
- **Website:** https://nwis.waterdata.usgs.gov/nwis/peak
- **Entry Method in BestFit:** USGS Peak Discharge
- **Required Parameter:** USGS Site Number (8-15 digits)

### Stations in This Example

| Station | Name | Location | Record | Character |
|---------|------|----------|--------|-----------|
| 01134500 | Moose River at Victory | Vermont | 1947--2025 | Perennial, snowmelt-driven, no low outliers |
| 11274500 | Orestimba Creek near Newman | California | 1932--2025 | Ephemeral, rainfall-driven, 47 MGBT low outliers |

## What's Inside

Open `usgs-peak-download-example.bestfit` in RMC-BestFit. The Project Explorer shows two Input Data elements (no Time Series Data elements -- peak data is downloaded directly):

| Element | Station | MGBT Low Outliers | Description |
|---------|---------|-------------------|-------------|
| USGS - 01134500 - Peak Discharge | Moose River at Victory, VT | 0 | Perennial stream with reliable annual peaks |
| USGS - 11274500 - Peak Discharge | Orestimba Creek near Newman, CA | 47 of 94 years | Ephemeral stream with many zero/low-flow years |

![Project Explorer showing two USGS peak discharge Input Data elements](../images/peak-download-project-explorer.png)
*Figure 1: Project Explorer with USGS peak discharge elements*

## Step-by-Step Guide

### Opening the Project

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to `examples/2-input-data/2-usgs-peak-discharge/`
3. Open `usgs-peak-download-example.bestfit`
4. Expand **Input Data** in the Project Explorer

### Exploring Moose River (01134500) -- No Low Outliers

1. Click **USGS - 01134500 - Peak Discharge** in the Project Explorer
2. The **Chronology** tab shows the annual peak instantaneous discharge from 1947 to present
3. Click the **Frequency** tab to see the empirical frequency curve -- note the smooth distribution with no obvious breaks or gaps in the lower tail
4. This station has no MGBT low outliers -- the annual peak flows are consistent year to year, as expected for a perennial stream fed by snowmelt

![Chronology of Moose River annual peak discharge](../images/peak-download-moose-river-chronology.png)
*Figure 2: Moose River annual peak discharge -- no low outliers*

![Frequency plot for Moose River](../images/peak-download-moose-river-frequency.png)
*Figure 3: Empirical frequency curve for Moose River*

### Exploring Orestimba Creek (11274500) -- 47 Low Outliers

1. Click **USGS - 11274500 - Peak Discharge** in the Project Explorer
2. The **Chronology** tab reveals a striking pattern: many years have very low or zero peak flows, while a few years have large flood peaks exceeding 10,000 cfs
3. Click the **Frequency** tab -- note the clear break in the lower tail where the MGBT has identified 47 low outliers
4. The low outliers appear as threshold-censored observations (shown differently from exact observations in the data grid and plots)

![Chronology of Orestimba Creek showing many low/zero flow years](../images/peak-download-orestimba-chronology.png)
*Figure 4: Orestimba Creek annual peak discharge -- note the many low-flow years*

![Frequency plot for Orestimba Creek showing MGBT threshold](../images/peak-download-orestimba-frequency.png)
*Figure 5: Empirical frequency curve for Orestimba Creek with MGBT low outliers*

### Understanding the Multiple Grubbs-Beck Test (MGBT)

The MGBT is a statistical test that identifies anomalously low peaks in the annual maximum series. These low values often represent years when the stream barely flowed -- they come from a different flood-generating mechanism than the large peaks used for design.

**How it works in RMC-BestFit:**

1. When **Use Multiple Grubbs-Beck Test** is enabled, the MGBT analyzes the peak discharge series to find a low outlier threshold
2. Peaks below the threshold are flagged as **threshold-censored observations** -- they are not discarded, but instead recorded as "the true peak was at or below this value"
3. The frequency analysis (Bayesian or MLE) uses a censored likelihood for these observations, properly accounting for the information they contain without letting them distort the upper-tail estimates

**Orestimba Creek context:** This is an ephemeral stream in California's Central Valley. In dry years, the creek may not flow at all or may produce only minor runoff. The MGBT identifies 47 of 94 years as low outliers -- consistent with the semi-arid, rainfall-driven hydrology where large floods are driven by Pacific storm systems that only affect the watershed in some years.

### Viewing the Properties Panel

1. With either Input Data element selected, open the **Properties** panel
2. The panel shows the USGS peak discharge configuration:
   - **Exact Data Method:** USGS Peak Discharge
   - **USGS Site Number:** The 8-digit site identifier
   - **Use Multiple Grubbs-Beck Test:** Checked (enabled by default)
   - **Download** button to refresh the data from USGS NWIS

![Properties panel showing USGS peak discharge settings](../images/peak-download-properties.png)
*Figure 6: Properties panel for a USGS peak discharge element*

### Downloading Your Own USGS Peak Data

To create a new USGS peak discharge Input Data element:

1. Right-click **Input Data** in the Project Explorer and select **Create New**
2. Enter a descriptive name for the element
3. In the Properties panel:
   - Set **Exact Data Method** to **USGS Peak Discharge**
   - Enter a valid **USGS Site Number** (8-15 digits). Look up site numbers at https://waterdata.usgs.gov
   - Enable or disable the **Use Multiple Grubbs-Beck Test** checkbox
4. Click **Download**
5. The annual peak discharge series will be retrieved from the USGS NWIS peak-flow web service

## Peak Download vs Block Maximum

This example uses the same Moose River station (01134500) as the [Block Maximum example](../1-block-maximum/usgs-block-max-example.md). The key differences:

| | Peak Download (this example) | Block Maximum |
|---|---|---|
| **Source** | USGS peak-flow database | Daily time series processed locally |
| **Peak type** | Instantaneous (highest moment) | Daily mean (24-hour average) |
| **Values** | Always >= block maximum | Always <= peak download |
| **Requires time series** | No | Yes |
| **MGBT support** | Yes (built in) | No (exact observations only) |
| **Non-USGS data** | No (USGS only) | Yes (any time series) |

For U.S. flood frequency analysis, the USGS peak download is generally preferred because instantaneous peaks better represent the true flood hazard. Block maximum from daily data is useful for non-USGS data sources or variables where instantaneous peaks are not available.

## Key Settings Reference

| Setting | Moose River | Orestimba Creek | Notes |
|---------|-------------|-----------------|-------|
| Exact Data Method | USGS Peak Discharge | USGS Peak Discharge | Downloads directly from USGS NWIS |
| USGS Site Number | 01134500 | 11274500 | Must be a valid USGS surface water site |
| Use MGBT | Enabled | Enabled | Identifies anomalously low peaks |
| MGBT Low Outliers | 0 | 47 | Low outliers become threshold-censored |
| Unit Label | Value | Value | USGS peaks are in cfs by convention |

## Next Steps

- **Distribution Fitting** -- Fit multiple distributions to the peak discharge series and compare goodness-of-fit, noting how censored observations affect the fitted distributions
- **Univariate Distribution Analysis** -- Perform Bayesian frequency analysis with full uncertainty quantification
- **Bulletin 17C** -- Use the Log-Pearson Type III distribution with MGBT censoring for regulatory flood frequency analysis per USGS guidelines
- **Compare with Block Maximum** -- See the [Block Maximum example](../1-block-maximum/usgs-block-max-example.md) to compare USGS instantaneous peaks with daily maxima from the same station
- **Peaks-Over-Threshold** -- See the [USGS POT example](../3-peaks-over-threshold/usgs-peaks-over-threshold-example.md) for an alternative to annual maxima using partial-duration series
