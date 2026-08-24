<!-- verification-status: publication-draft -->

# Input-Data Verification Boundary

## Test objective

The second project collection converts manual, agency, block-series, or peaks-over-threshold inputs into the `DataFrame` consumed by fitting and univariate analyses. The verified boundary is the preservation of observation type, chronology, index, censoring/threshold semantics, and processing configuration.

## Test and result

Fast UI and core tests exercise collection persistence, dirty-state propagation, validation, processing state, plotting-position contracts, exact/interval/threshold/uncertain series construction, and deterministic block/peak calculations. These tests passed in the publication regression gate. They establish data-contract behavior but are not counted as external numerical or recovery evidence.

Downstream chapters describe the independent tests that consume committed frames: distribution fits and functions, Bulletin 17C worked examples, point-process mixed-observation likelihoods, rating-curve aligned pairs, and spatial missing-row likelihoods. All reported downstream results use explicit fixtures and acceptance rules.

## Evidence boundary

Input processing does not prove stationarity, independence, record completeness, perception-threshold correctness, or application suitability. The analyst must validate those assumptions before relying on a downstream frequency model.
