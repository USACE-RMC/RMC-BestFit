# Time-series data: establish the record before analysis

A time-series element stores observations and their source settings. This chapter teaches the first decisions in an analysis: what was measured, in which units, at what interval, and over what observed period. All projects contain saved records and can be explored offline. Network access is needed only to refresh a download.

| Tutorial | Saved source | Main lesson |
|---|---|---|
| [USGS records](1-usgs-download/usgs-download-example.md) | Eight series at five U.S. gages | Daily means, instantaneous values, annual peaks and field measurements answer different questions. |
| [GHCN precipitation and snowfall](2-ghcn-download/ghcn-download-example.md) | Big Bear Lake and Paradise, California | Check variable-specific units and missing values; the retained snowfall scale has a documented error. |
| [Canadian hydrometric records](3-chmn-download/chmn-download-example.md) | Lillooet River, 08MG005 | Compare coverage and metric units across six discharge and stage products. |
| [Australian water data](4-abom-download/abom-download-example.md) | Six series from three stations | Read precipitation, flow and stage settings separately and review gaps. |
| [HEC-DSS hydrographs](5-hec-dss-import/hec-dss-import-example.md) | Supplied Grapevine Dam DSS file | Match pathnames, intervals and units before comparing inflow with release. |
| [Manual entry](6-manual-entry/manual-entry-example.md) | Airline passengers, Nile volume and Mauna Loa CO2 | Check dates and units against the supplied CSV; the Nile date discrepancy is retained and explained. |

## Begin with one project

1. Open its `.bestfit` file with **File > Open** and save a working copy.
2. Select the named **Time Series Data** element. Read Properties and the data grid before the plot.
3. Confirm the source identifier, variable, units, first and last dates, and missing values.
4. Compare chronology and seasonality. A seasonal band shows the spread of observed monthly values; it is not uncertainty in a fitted flood quantile.
5. State the engineering quantity you need before deriving a frequency sample.

A zero missing-value count describes stored rows, not necessarily complete coverage between dates. This distinction is particularly important for event records and irregular measurements. A stage measurement is relative to a datum; a negative gage height is not a negative water depth. Field-measured stage and discharge must be paired by reliable timestamps or measurement identifiers, not row number.

The manual-entry folder supplies [airline](6-manual-entry/airline-passengers.csv), [Nile](6-manual-entry/nile-river-flow.csv) and [CO2](6-manual-entry/mauna-loa-co2.csv) files. The HEC-DSS tutorial links its local source file. Keep these sources with the working project.

## Continue the workflow

Use [Chapter 2](../2-input-data/README.md) to create annual or threshold-based frequency inputs. Use [Chapter 7](../7-time-series-analysis/README.md) when the question concerns serial dependence or forecasting in the original time series. The [shared figure instructions](../README.md#reproducing-the-figures) explain how the Python figures are regenerated from BestFit plot geometry.
