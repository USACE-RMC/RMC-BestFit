<!-- verification-status: publication-draft -->

# Input Data

## Test objective

Input-data processing prepares the observations used by frequency and distribution analyses. These may be entered directly, obtained from an agency record, extracted as annual or seasonal block values, or selected as peaks above a threshold. The software must preserve both the values and what is known about them. An exact measurement, a flood known only to lie between two bounds, and a year known to remain below a perception threshold contribute different information.

## Test and result

The tests construct controlled input records, apply the selected processing settings, save and reopen them, and compare the resulting observations and settings with the expected values. Exact, interval, threshold, and uncertain observations are checked as separate data types. Changes to an input must also identify the project as modified so that an analyst is not unknowingly relying on an outdated saved state.

An important example concerns incomplete years. Selecting an annual maximum from a partial daily record can understate the largest event that actually occurred. The following tests assess whether BestFit warns the analyst about that condition; they do not fill in the unobserved flows.

| Controlled source record | Processing setup | Expected and observed result |
|---|---|---|
| All 365 daily values from 1 January through 31 December 2021 | Calendar-year blocks | No incomplete-record warning. Passed. |
| 334 daily values beginning 1 February 2021 | Calendar-year blocks | Warning for the incomplete first year. Passed. |
| 426 daily values beginning 1 October 2020 | Water-year blocks starting in October | Warning for the incomplete final water year. Passed. |
| Full 2021 calendar with one explicitly missing value | Calendar-year blocks | Warning for missing coverage within the year. Passed. |
| Full 2021 calendar with one omitted daily timestamp | Calendar-year blocks | Warning for the gap, even though no missing-value marker was supplied. Passed. |

The warning cases remain valid input objects; the result is a warning to support the analyst's decision, not automatic removal of the year. Switching to manual observations clears a warning that applies only to block extraction.

Other tests distinguish a calculated low-outlier threshold from one chosen by the analyst. When derived data are cleared, the calculated threshold is reset; a user-defined threshold of 25 remains 25. Saving and reopening preserves the tested observations, plotting positions, and custom axis titles. These checks and the deterministic block and peak calculations passed in the software regression suites.

These checks establish preservation of supplied information. Later chapters test its statistical use through threshold and interval floods, mixed observation types, paired stage-discharge data, and missing-site observations. Input processing does not establish stationarity, independence, record completeness, correct perception thresholds, or application suitability; those remain the analyst's responsibility.
