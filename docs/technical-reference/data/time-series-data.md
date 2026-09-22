<!-- technical-reference-status: complete -->

# Time-Series Data

[Technical reference](../index.md) | [Input data](input-data.md) | [Time-series analyses](../analysis/time-series.md)

## Purpose

`TimeSeriesElement` is the first scientific collection in the RMC.BestFit project tree. It stores a dated `TimeSeries`, its interval and units, source provenance, and the plots used to inspect chronology, seasonality, autocorrelation, and partial autocorrelation. A time-series element is input data; it does not fit an AR, MA, ARIMA, or ARIMAX model.

## Entry methods and provenance

The supported entry modes are manual entry, HEC-DSS, GHCN, USGS, CHMN, ABOM, GHCN daily precipitation, USGS daily discharge, and USGS daily stage. Source-specific identifiers and raw text are retained where applicable. `SeriesType`, `TimeInterval`, `StartDateTime`, and the selected depth, discharge, or height unit determine how values are interpreted and displayed.

Downloaded or imported observations are normalized into the same dated `TimeSeries` contract. The element reports invalid source identifiers, missing data, inconsistent intervals, and unsupported unit/source combinations before downstream analysis. Network access and the continued availability of an external provider are operational dependencies, not properties verified by the numerical report.

## Persistence and lifecycle

The collection persists metadata, source settings, values, and plot settings in the project database. Open and copy operations restore the established public fields and rebuild plot bridges after deserialization. Changes to the data or settings invalidate downstream results through the project dependency graph.

## Assumptions and limitations

- Observation times must be consistent with the declared interval before a regular time-series analysis is appropriate.
- Missing-value treatment, aggregation, and source quality control remain the analyst's responsibility unless a specific import path states otherwise.
- A downloaded agency series is not automatically validated for a particular hydrologic application.
- AR, MA, ARIMA, and ARIMAX likelihoods and forecasts are defined in the later [time-series analysis](../analysis/time-series.md) chapters.

## Implementation and verification

`TimeSeriesElement.cs` and `TimeSeriesCollection.cs` in the UI's `Elements/TimeSeriesData` folder implement this collection. Fast UI tests cover state, validation, persistence, copying, and HEC-DSS imports. Numerical estimation evidence is in [Time-Series Analyses](../../verification/report/time-series-analyses.md).

---

[Technical reference](../index.md) | [Input data](input-data.md) | [Time-series analyses](../analysis/time-series.md)
