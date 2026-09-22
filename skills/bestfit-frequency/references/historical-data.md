# Collecting and entering flood evidence

## Primary reference: official Bulletin 17C

England and others, *Guidelines for Determining Flood Flow Frequency—Bulletin 17C*,
USGS Techniques and Methods 4–B5, version 1.1 (May 2019),
[publication and revision record](https://pubs.usgs.gov/publication/tm4B5),
[official PDF](https://pubs.usgs.gov/tm/04/b05/tm4b5.pdf),
[DOI](https://doi.org/10.3133/tm4B5).

Read *Data Sources*, *Data Representation Using Flow Intervals and Perception
Thresholds*, appendix 3 (data representations) and appendix 10 (worked examples).
Figures 12 and 3–3 distinguish the magnitude information for a flood from what
could have been observed during a period. Appendix 10 supplies paired flow-interval
and perception-threshold tables for systematic, historical and paleoflood records.
Record the version and actual table/section used in each evidence decision.

Use these concepts when collecting data for Bayesian FFA as well as B17C analysis.
The reference does not establish that every general B17C/PeakFQ input representation
is available through BestFit's current API. If a source cannot be retrieved, retain
the error; do not claim to have reviewed it or use an unverified cached quotation.

## Research and justification

Start with the exact USGS gage/outlet, its peak file, station history and published
flood reports. Inspect qualifier codes, partial dates, relocation, drainage-area
changes, rating extrapolation, regulation and diversions. The API `/source`
response preserves original decoded peak text; its numerical exact series is
not an automatic scientific classification of every downloaded row.

Search the [USGS publication catalog](https://pubs.usgs.gov/), state water science
centers and data releases for the river, gage, basin and known event dates. Use
[NWS reports](https://www.weather.gov/), local forecast-office flood histories and
[NWPS crest information](https://water.noaa.gov/about/api) to corroborate events.
NWPS does not provide a general historical continuous time-series archive.
[USGS Flood Event Viewer](https://www.usgs.gov/tools/flood-event-viewer) provides
event evidence and high-water marks; a mark or stage alone is not a discharge.
Archives, newspapers and engineering studies can support additional candidates
when identity, date and measurement interpretation are documented.

For each event record:

| Record | Required justification |
|---|---|
| Identity | River/reach, gage/outlet, original date, annual index, duplicate-event resolution |
| Magnitude | Original value and units; stage datum/rating or hydraulic method if converted; defensible bounds |
| Regime | Peak versus N-day statistic; regulated versus natural; watershed and station changes |
| Interpretation | Exact, bounded interval, or justified measurement-error distribution; rationale and source locator |
| Retention | URL/title/version, retrieved UTC, page/table, bytes/checksum, accepted/rejected/unresolved decision |

Different dates in one water year do not automatically create multiple annual
observations. Confirm the annual maximum at the target location. Preserve partial
dates and uncertain paleoflood ages as unresolved evidence when they cannot be
assigned to an annual index without introducing an unsupported assumption.

## BestFit entry contract

| Information | Entry and review |
|---|---|
| Known annual discharge | `exactData: {index, value}`; no synthesized values for missing years |
| Bounded event magnitude | `intervalData: {index, lowerBound, upperBound, value?}`; `value` is a display coordinate, not an exact observation |
| Measurement-error model | `uncertainData: {index, distribution:{type,parameters}}`; Bayesian analysis uses it, B17C does not |
| Completeness above one threshold | `thresholdData: {startIndex,endIndex,value,numberAbove}`; inclusive window; split when detectability changes |
| Additional aggregate exceedances | `numberAbove` counts undated exceedances **not already entered** as exact/interval/uncertain observations |
| Unknown year | Leave a gap; do not manufacture zero, an interval or a perception threshold |

BestFit subtracts every explicitly dated observation within a threshold window
from the remaining annual count, including valid observations below that threshold.
Do not reject an interval just because its magnitude is below a perception value.
Do not count the same historical flood both explicitly and in `numberAbove`.
Responses report model-processed counts; `/source` retains submitted counts.

Example from the saved **one-day** Blakely project: an 1882 interval
`[115000,150000]` cfs and a threshold of `110000` over `1870..1922` with
`numberAbove=0` yield **52** censored years plus one explicit event. A separate
`1931..1935` threshold is justified by that example's missing-record evidence,
not merely by the presence of a five-year gap.

An event list, a famous flood, or absence of newspaper coverage does not demonstrate
completeness. Document why a flood above the threshold would have left a preserved
or recorded trace throughout the entire stated period. NWS warning/flood stages
are operational categories, not automatically historical perception limits.
Revisit limits after settlement, channel, rating/datum or gage changes.

BestFit's threshold API has one magnitude and aggregate counts. Do not translate
arbitrary two-sided B17C perception intervals, missing-data sentinel values, or
uncertain event dates into that field. The preparation helper rejects unsupported
threshold fields and all-exceedance aggregate windows that current model processing
cannot preserve. Keep those cases for engineering review; do not modify the model.

Use explicit annual indexes. For dates, the preparation helper uses the declared
calendar year or **ending year** of a specified water year (October by default).
The API's date-only fallback is `DateTime.Year`, not automatic water-year conversion.
For paleofloods state the age-to-index reference and treatment of year zero; never
confuse years before present with negative calendar indexes.
