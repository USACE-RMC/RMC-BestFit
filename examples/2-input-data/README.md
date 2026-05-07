# Input Data Examples

Input Data is the bridge between raw time series and frequency analysis in RMC-BestFit. An Input Data element extracts a statistical sample from a time series (or downloads one directly) and organizes it into a data frame with exact, uncertain, interval-censored, and threshold-censored observations. This sample becomes the input to Distribution Fitting and Univariate Distribution Analysis.

These examples demonstrate the three methods for creating Input Data elements, covering the most common workflows in flood and precipitation frequency analysis.

## Examples

| Example | Method | Description |
|---------|--------|-------------|
| [Block Maximum](1-block-maximum/usgs-block-max-example.md) | Block Maximum from Time Series | Calendar year and water year annual maxima extracted from USGS daily discharge |
| [USGS Peak Discharge](2-usgs-peak-discharge/usgs-peak-download-example.md) | Direct USGS Download | Annual peak discharge downloaded from USGS NWIS, with and without MGBT low outlier detection |
| [USGS Peaks-Over-Threshold](3-peaks-over-threshold/usgs-peaks-over-threshold-example.md) | Peaks-Over-Threshold from Time Series | POT series from USGS daily discharge with a 650 cfs threshold |
| [GHCN Peaks-Over-Threshold](3-peaks-over-threshold/ghcn-peaks-over-threshold-example.md) | Peaks-Over-Threshold from Time Series | POT series from NOAA GHCN daily precipitation with a 1.0 inch threshold |

## Input Data Methods

RMC-BestFit provides three ways to populate an Input Data element:

### Block Maximum from Time Series

Divides a time series into fixed-length blocks (calendar year, water year, or custom period) and returns one value per block -- typically the maximum, but minimum and mean are also available. This is the standard approach for annual maximum flood frequency analysis when working with daily or sub-daily records.

### USGS Peak Discharge Download

Downloads the official annual peak instantaneous discharge series directly from the USGS NWIS peak-flow web service. These values represent the single highest instantaneous streamflow recorded each water year, as published by the USGS. This method bypasses the need for a time series element entirely. It also supports the Multiple Grubbs-Beck Test (MGBT) for detecting anomalously low peaks, which are flagged as threshold-censored observations.

### Peaks-Over-Threshold from Time Series

Selects all values exceeding a user-defined threshold from a time series, with a minimum separation between peaks to ensure independence. Unlike block maximum (which produces exactly one value per block), POT can yield multiple events in wet years and zero events in dry years. The resulting partial-duration series is used with point process or Poisson-GP models for frequency analysis.

## Data Frame Types

Each Input Data element contains a data frame with up to four series types:

| Series | Description | Example |
|--------|-------------|---------|
| **Exact** | Precisely observed values | Annual peak discharge of 5,200 cfs |
| **Uncertain** | Values with measurement error, modeled as distributions | Peak estimated between 4,800 and 5,600 cfs |
| **Interval** | Values known only within a range | Peak between 3,000 and 5,000 cfs (historical flood) |
| **Threshold** | Values known only to be below (or above) a threshold | Peak was below 1,270 cfs (MGBT low outlier) |

The block maximum and POT methods produce exact observations. The USGS peak download method can additionally produce threshold-censored observations when the MGBT detects low outliers.

## Prerequisites

- **RMC-BestFit 2.0** (Beta or later)
- **Internet connection** required for the USGS peak discharge download example and for refreshing time series data in the block maximum and POT examples
- All examples include pre-downloaded data -- you can explore the results without an internet connection

## How to Use These Examples

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to a subfolder (e.g., `1-block-maximum/`)
3. Open the `.bestfit` file
4. Expand **Input Data** in the Project Explorer to see the elements
5. Click on an element to view its chronology, frequency, seasonality, density, histogram, Q-Q, ACF, and PACF tabs
6. Open the Properties panel to see the Input Data configuration

Each tutorial guide includes step-by-step instructions for exploring the data, understanding the Properties panel settings, and creating your own Input Data elements.

## Screenshot Images

Screenshot placeholders in the tutorial guides reference images in the `images/` subfolder. To add screenshots:

1. Create an `images/` folder in this directory if it does not exist
2. Capture screenshots from RMC-BestFit matching the descriptions in the alt text
3. Save as PNG with the filename specified in each image reference
4. Naming convention: `<example-name>-<view-name>.png`

## Next Steps

After creating Input Data, the typical workflow continues with:

1. **Distribution Fitting** -- Fit multiple distributions to the extracted sample and compare goodness-of-fit using information criteria (DIC, WAIC, LOO-CV)
2. **Univariate Distribution Analysis** -- Perform Bayesian frequency analysis with a selected distribution to estimate flood quantiles and their uncertainty
3. **Bulletin 17C Analysis** -- For USGS regulatory applications, perform a Bulletin 17C flood frequency analysis using the Log-Pearson Type III distribution
