<!-- verification-status: publication-draft -->

# Time-Series Data Verification Boundary

## Test objective

The software's first project collection stores dated values, interval and unit metadata, source provenance, and diagnostic plots. The publication question at this boundary is whether the project preserves the data supplied to the scientific model; it is not whether an external data provider or a hydrologic record is correct.

## Test and result

Fast UI tests exercise manual construction, validation and property transitions, project persistence and copy behavior, and HEC-DSS import contracts. These tests passed in the publication regression gate. They are deterministic software-contract evidence and are not counted as independent numerical verification.

Independent recurrence, transform, likelihood, generation, and recovery tests consume controlled dated series later in this report. Their passing results establish that the model layer interprets the tested chronology and intervals as stated, but they do not validate arbitrary downloaded records.

## Evidence boundary

The report makes no claim about network availability, provider revisions, station quality, missing-value treatment chosen by an analyst, or the suitability of a series for a specific risk assessment. Those checks precede model verification.
