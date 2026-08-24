<!-- technical-reference-status: complete -->

# Input Data

[Time-series data](time-series-data.md) | [Data-frame model](../data-frame/index.md) | [Distribution fitting](../analysis/distribution-fitting.md)

## Purpose

`InputData` is the second scientific collection in the project tree. It owns the `DataFrame` supplied to fitting and univariate analyses and provides the workflow that converts manual, agency, block-series, or peaks-over-threshold input into exact, interval, threshold, or uncertain observations.

## Entry and derivation modes

Exact data may be entered manually, derived as a block series, derived as a peaks-over-threshold series, or downloaded as USGS peak discharge or peak stage. When input is derived from `TimeSeriesElement`, the configuration records the block function and window, start/end months, smoothing rule and period, threshold, and minimum separation between peaks.

The resulting `DataFrame` is the scientific handoff to the model library. Its chronology, censoring semantics, observation indexes, perception thresholds, plotting positions, and uncertain-observation distributions are defined in the [data-frame chapter](../data-frame/index.md).

## Processing rules

- A block series selects the configured statistic within each declared time block.
- A peaks-over-threshold series applies the configured threshold and minimum inter-peak separation to the source chronology.
- Multiple Grubbs-Beck processing is optional and applies only in the supported Bulletin 17C workflow.
- `IsProcessed` distinguishes a configured source from a completed derived data frame.
- Changes to the source time series or processing controls clear dependent results and require reprocessing.

## Validation and limitations

The element validates its source reference, processing window, threshold/separation controls, data-frame contents, and series-specific requirements. Processing does not establish stationarity, independence, representativeness, perception-threshold correctness, or fitness for a particular frequency model; those remain model and application assumptions.

## Implementation and verification

Implementation is in `RMC.BestFit.UI/Elements/InputData/InputData.cs` and `InputDataCollection.cs`. Fast UI tests cover construction, dirty-state propagation, collection persistence, validation, processing state, and project lifecycle. The numerical report treats data preparation as a controlled input boundary and verifies scientific calculations in the analysis that consumes the resulting frame; see [Input-Data Verification Boundary](../../verification/report/input-data.md).

---

[Time-series data](time-series-data.md) | [Data-frame model](../data-frame/index.md) | [Distribution fitting](../analysis/distribution-fitting.md)
