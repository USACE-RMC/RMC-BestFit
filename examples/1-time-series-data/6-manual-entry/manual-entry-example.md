# Manual Data Entry Example

## Overview

This example demonstrates how to enter time series data manually in RMC-BestFit by copy-pasting from CSV files. The project contains three classic datasets widely used in time series analysis textbooks and statistical software documentation.

Manual entry is useful when your data comes from published tables, spreadsheets, or other sources not covered by the built-in download options (USGS, GHCN, CHMN, ABOM, HEC-DSS).

## Datasets

| Element | Dataset | Frequency | Period | Unit Label | Source |
|---------|---------|-----------|--------|------------|--------|
| Airline Passengers | Box-Jenkins airline data | Monthly | 1949--1960 | Passengers (thousands) | Box & Jenkins (1970) |
| Nile River Flows | Nile annual flow at Aswan | Annual | 1871--1970 | Flow (10⁸ m³) | Cobb (1978) |
| Mauna Loa CO2 | Atmospheric CO2 at Mauna Loa | Monthly | 1958--2023 | CO2 (ppm) | NOAA GML |

### Airline Passengers

The Box-Jenkins airline passenger dataset records monthly totals of international airline passengers from 1949 to 1960. It is one of the most widely used examples in time series analysis, originally published in *Time Series Analysis: Forecasting and Control* (Box & Jenkins, 1970). The series exhibits:

- **Strong seasonality** with peaks in summer months (June--August)
- **Multiplicative trend** -- the seasonal amplitude grows proportionally with the level
- **Upward trend** reflecting growth in commercial aviation

This dataset is commonly used to demonstrate seasonal ARIMA (SARIMA) modeling, seasonal differencing, and log transformations for variance stabilization.

### Nile River Flows

The Nile River dataset records annual flow volumes at Aswan from 1871 to 1970, measured in units of 10⁸ cubic meters. Published by Cobb (1978) and widely used in the R `datasets` package, the series is notable for:

- **A level shift around 1898** coinciding with the construction of the first Aswan Dam
- **Low-order autocorrelation** suitable for AR(1) modeling
- **Change-point detection** -- the abrupt shift makes this a classic example for structural break analysis

The annual frequency and level shift make this dataset ideal for introducing autoregressive models and change-point methods.

### Mauna Loa CO2

Monthly average atmospheric CO2 concentrations recorded at the Mauna Loa Observatory in Hawaii, maintained by the NOAA Global Monitoring Laboratory (GML). This is the longest continuous record of directly measured atmospheric CO2 and is widely known as the Keeling Curve. The series exhibits:

- **Strong upward trend** driven by fossil fuel emissions
- **Regular seasonal cycle** caused by Northern Hemisphere vegetation uptake in spring/summer
- **Nonlinear trend** -- the rate of increase accelerates over the record

This dataset is commonly used to demonstrate seasonal decomposition, trend-cycle separation, and ARIMAX modeling with deterministic seasonal components.

## What's Inside

Open `manual-entry-example.bestfit` in RMC-BestFit. The Project Explorer shows three Time Series Data elements:

![Project Explorer showing the three manual entry elements: Airline Passengers, Nile River Flows, and Mauna Loa CO2](../images/manual-entry-project-explorer.png)
*Figure 1: Project Explorer with three manually entered time series*

Each element contains the full dataset already entered. Click on any element to view the time series plot and explore its statistical properties.

## Step-by-Step Guide

### Opening the Project

1. Launch RMC-BestFit 2.0
2. Select **File > Open** and navigate to this folder
3. Open `manual-entry-example.bestfit`
4. Expand **Time Series Data** in the Project Explorer

### Exploring the Time Series Tabs

Click on an element to open it. The main view shows four tabs:

1. **Time Series** -- The raw data plotted against time. Look for trend, seasonality, and level shifts.
2. **Seasonality** -- A seasonal subseries plot showing data grouped by month (or other period). Useful for identifying recurring patterns.
3. **ACF** -- Autocorrelation function. Slowly decaying ACF suggests trend or nonstationarity; periodic peaks suggest seasonality.
4. **PACF** -- Partial autocorrelation function. Helps identify AR order -- significant spikes at lags 1 through *p* suggest an AR(*p*) model.

![Time series plot of Airline Passengers showing upward trend with growing seasonal amplitude](../images/manual-entry-airline-ts-plot.png)
*Figure 2: Airline Passengers time series showing multiplicative seasonal pattern*

![ACF plot of Airline Passengers showing slowly decaying autocorrelation with seasonal peaks](../images/manual-entry-airline-acf.png)
*Figure 3: ACF of Airline Passengers -- periodic peaks at lags 12, 24, 36 indicate seasonality*

![Time series plot of Nile River Flows showing level shift around 1898](../images/manual-entry-nile-ts-plot.png)
*Figure 4: Nile River annual flows with visible level shift*

![Time series plot of Mauna Loa CO2 showing upward trend with seasonal cycle](../images/manual-entry-co2-ts-plot.png)
*Figure 5: Mauna Loa CO2 with accelerating trend and annual seasonal cycle*

### Viewing the Properties Panel

Open the Properties panel (click **Properties** in the toolbar or press **F4**) to see the configuration for each element:

- **Entry Method** -- Set to *Manual* for all three elements
- **Time Interval** -- *One Month* for Airline Passengers and Mauna Loa CO2; *One Year* for Nile River Flows
- **Unit Label** -- Displayed on the Y-axis of the time series plot
- **Start Date** -- The date of the first observation

![Properties panel showing Entry Method = Manual, Time Interval = One Month, and Unit Label for Airline Passengers](../images/manual-entry-properties.png)
*Figure 6: Properties panel for a manually entered time series*

## Creating Your Own Manual Entry Element

To enter a new time series manually from a CSV file:

### 1. Create the Element

1. Right-click **Time Series Data** in the Project Explorer
2. Select **Create New**
3. Enter a descriptive name for the element

### 2. Configure the Properties

In the Properties panel:

1. Set **Entry Method** to *Manual*
2. Set **Time Interval** to match your data frequency (e.g., *One Month*, *One Year*, *One Day*)
3. Set **Start Date** to the date of the first observation
4. Set **Unit Label** to describe the data units (e.g., "Discharge (cfs)", "Precipitation (mm)")

### 3. Paste the Data

1. Open the CSV file in a spreadsheet application or text editor
2. Select the **value column only** (not the date column -- dates are computed from the Start Date and Time Interval)
3. Copy the values to the clipboard
4. In RMC-BestFit, click on the data grid in the Time Series tab
5. Select the first cell in the **Value** column
6. Paste (**Ctrl+V**)

The dates are automatically generated based on the Start Date and Time Interval settings. For regular-interval data (monthly, annual, daily), you only need to paste the values.

### 4. Verify

After pasting, check:

- The time series plot updates to show your data
- The date range matches your expected period of record
- The number of observations matches your source data
- The Y-axis label shows your Unit Label

## CSV Files Provided

Three CSV files are included in this folder for reference and practice:

| File | Columns | Rows |
|------|---------|------|
| `airline-passengers.csv` | Date, Passengers | 144 |
| `nile-river-flow.csv` | Date, Flow | 100 |
| `mauna-loa-co2.csv` | Date, CO2 | 790 |

Each file uses a simple two-column format with ISO date strings (YYYY-MM-DD) and numeric values. The Date column is provided for reference -- when pasting into RMC-BestFit, you only need the value column.

## Key Settings Reference

| Setting | Purpose | Example Values |
|---------|---------|---------------|
| Entry Method | How data is entered | Manual, USGS, GHCN, CHMN, ABOM, HEC-DSS |
| Time Interval | Spacing between observations | One Month, One Year, One Day, One Hour |
| Start Date | Date of the first observation | 1949-01-01, 1871-01-01 |
| Unit Label | Y-axis label and data description | Passengers (thousands), Flow (10⁸ m³), CO2 (ppm) |

## Next Steps

After entering time series data, continue with:

1. **Input Data** -- Create an Input Data element that references a time series, apply a block function (e.g., annual maximum), and extract the sample for frequency analysis
2. **Time Series Analysis** -- Fit ARIMA/ARIMAX models to the raw time series for forecasting and trend analysis (see examples in `../../7-time-series-analysis/`)
3. **Distribution Fitting** -- Fit multiple distributions to the extracted sample and compare goodness-of-fit

## References

- Box, G. E. P., & Jenkins, G. M. (1970). *Time Series Analysis: Forecasting and Control*. Holden-Day.
- Cobb, G. W. (1978). The Problem of the Nile: Conditional Solution to a Changepoint Problem. *Biometrika*, 65(2), 243--251.
- NOAA Global Monitoring Laboratory. Trends in Atmospheric Carbon Dioxide. https://gml.noaa.gov/ccgg/trends/
