# Time Series Data Examples

These example projects demonstrate how to import and download time series data in RMC-BestFit 2.0. Each `.bestfit` file showcases a different data source, covering all entry methods supported by the software.

## Examples

| Example | Data Source | Series Types | Description |
|---------|-------------|--------------|-------------|
| [USGS Download](1-usgs-download/usgs-download-example.md) | USGS NWIS | Daily, Instantaneous, Peak, Measured (Discharge + Stage) | Four U.S. gaging stations demonstrating all 8 USGS series types |
| [GHCN Download](2-ghcn-download/ghcn-download-example.md) | NOAA GHCN | Daily Precipitation, Daily Snow | Cooperative observer stations in California |
| [CHMN Download](3-chmn-download/chmn-download-example.md) | Water Survey of Canada | Daily, Instantaneous, Peak (Discharge + Stage) | Lillooet River near Pemberton, BC demonstrating all 6 CHMN series types |
| [ABOM Download](4-abom-download/abom-download-example.md) | Australian BOM | Daily, Instantaneous (Discharge, Stage, Precipitation) | Stations in the ACT and NSW demonstrating all 5 ABOM series types |
| [HEC-DSS Import](5-hec-dss-import/hec-dss-import-example.md) | Local HEC-DSS file | Hourly Discharge | Grapevine Dam inflow/outflow hydrographs with Alternative Time Series comparison |
| [Manual Entry](6-manual-entry/manual-entry-example.md) | Copy-paste from CSV | Monthly, Annual | Three classic time series analysis datasets entered manually |

## CSV Data Files

The following CSV files are provided for the manual entry example. Each file has a Date column and a value column that can be copy-pasted into a Time Series Data element:

| File | Dataset | Frequency | Source |
|------|---------|-----------|--------|
| [`airline-passengers.csv`](6-manual-entry/airline-passengers.csv) | Airline Passengers | Monthly | Box & Jenkins (1970) |
| [`nile-river-flow.csv`](6-manual-entry/nile-river-flow.csv) | Nile River Annual Flow | Annual | Cobb (1978) |
| [`mauna-loa-co2.csv`](6-manual-entry/mauna-loa-co2.csv) | Mauna Loa CO2 | Monthly | NOAA GML |

## Supported Data Sources

RMC-BestFit supports manual data entry, downloads from four web APIs, and imports from one local file format:

| Entry Method | Agency | Required Parameter | Format | Available Series Types |
|--------------|--------|-------------------|--------|----------------------|
| **Manual** | — | — | — | Any (user-defined time interval and unit label) |
| **USGS** | U.S. Geological Survey | Site Number (8-15 digits) | `01134500` | Daily Discharge/Stage, Instantaneous Discharge/Stage, Peak Discharge/Stage, Measured Discharge/Stage |
| **GHCN** | NOAA NCEI | Station ID (11 characters) | `USC00040741` | Daily Precipitation, Daily Snow |
| **CHMN** | Water Survey of Canada | Station ID (7 characters) | `08MG005` | Daily Discharge/Stage, Instantaneous Discharge/Stage, Peak Discharge/Stage |
| **ABOM** | Australian Bureau of Meteorology | Station ID (6 digits) | `410730` | Daily Discharge/Stage/Precipitation, Instantaneous Discharge/Stage |
| **HEC-DSS** | Local file import | DSS file path + pathname | 6-part DSS path | Any time series stored in DSS format |

## Prerequisites

- **RMC-BestFit 2.0** (Beta or later)
- **Internet connection** required for USGS, GHCN, CHMN, and ABOM download examples
- **HEC-DSS example** includes the `.dss` file in the [`5-hec-dss-import/`](5-hec-dss-import/) subfolder -- no internet required
- **Manual entry example** includes CSV files in the [`6-manual-entry/`](6-manual-entry/) subfolder for copy-paste -- no internet required

## How to Use These Examples

1. Open RMC-BestFit 2.0
2. Select **File > Open** and navigate to a subfolder (e.g., `1-usgs-download/`)
3. Open the `.bestfit` file
4. Expand **Time Series Data** in the Project Explorer to see the elements
5. Click on an element to view its time series plot, seasonality, ACF, and PACF tabs
6. Open the Properties panel to see the data source configuration

Each tutorial guide includes step-by-step instructions for exploring the data, viewing the Properties panel, and downloading your own data from the same source.

## Screenshot Images

Screenshot placeholders in the tutorial guides reference images in the `images/` subfolder. To add screenshots:

1. Create an `images/` folder in this directory if it does not exist
2. Capture screenshots from RMC-BestFit matching the descriptions in the alt text
3. Save as PNG with the filename specified in each image reference
4. Naming convention: `<example-name>-<view-name>.png`

## Next Steps

After exploring time series data, the typical workflow continues with:

1. **Input Data** -- Create an Input Data element that references a time series, apply a block function (e.g., annual maximum), and extract the sample for analysis
2. **Distribution Fitting** -- Fit multiple distributions to the extracted sample and compare goodness-of-fit
3. **Univariate Analysis** -- Perform Bayesian frequency analysis with a selected distribution
4. **Time Series Analysis** -- Fit ARIMA/ARIMAX models to the raw time series
