<!-- verification-status: publication-draft -->

# Time-Series Data

## Test objective

Time-series data are measurements with dates or times, such as daily discharge or an irregular sequence of stage measurements. Before fitting a model, the software must preserve the values, their timing, missing observations, and the units assigned to them. A missing daily value must not become zero, and an empty file block must not hide observations stored later in the record.

## Test and result

The tests construct short records with known values and dates, import records from HEC-DSS, and save and reopen project data. Each operation is followed by a direct comparison with the supplied values and the expected dates. Additional cases use long gaps and records spread over several storage blocks to check that the import preserves the complete series.

| Test setup | Procedure and expected result | Result |
|---|---|---|
| Three daily values: 1, a DSS missing-value marker, and 3 | Import the series. Preserve 1 and 3 and represent the middle day as missing. Four supported missing-value markers are checked. | Passed; valid values agree within $10^{-12}$ and the missing day remains identifiable. |
| Three daily values: 0, 1, and 0 | Import using the same path as the missing-value examples. | Passed; both zeros remain valid measurements. |
| Daily period-average values of 1,950 | Convert the DSS end-of-period timestamp at midnight on 2 October 1905 to the represented day, 1 October 1905. | Passed; the value is unchanged and assigned to the correct day. |
| Regular and irregular records with omitted times | Fill absent expected steps in a regular record with missing values; preserve only the supplied timestamps in an irregular record. | Passed; the two types of record retain their different time meanings. |
| Sparse records, empty blocks, and adjacent storage boundaries | Read all relevant blocks and compare the complete sequence of timestamps and values. | Passed; later data remain present and boundary observations occur once. |
| Imported data saved and reopened | Compare values, gaps, interval information, and period conversion after reopening. | Passed; project persistence preserves the tested record. |

Malformed input also receives deliberate checks. Duplicate timestamps, inconsistent array lengths, and conflicting blocks are rejected instead of being returned as a successful partial import. User-assigned labels and plot settings are checked separately when copying or reopening a series.

These are deterministic software checks: the expected answer is the original supplied record or a prescribed timestamp conversion. They passed in the software regression suites and are not included in the 328-method numerical-verification inventory.

Preserving these inputs supports the statistical comparisons in the time-series analysis chapter. Network availability, provider revisions, station quality, and the analyst's treatment of missing values remain outside these checks.
