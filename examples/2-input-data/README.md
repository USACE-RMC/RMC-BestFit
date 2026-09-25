# Input data: define the sample and observation process

An Input Data element organizes the observations used by a frequency analysis. It can hold exact values, measurement-error distributions, intervals and perception thresholds. Start by deciding what one observation represents and how many years were actually observed; the choice of a statistical distribution comes later.

| Tutorial | Saved sample | Main decision |
|---|---|---|
| [Annual block maxima](1-block-maximum/usgs-block-max-example.md) | Moose River daily-mean maxima, calendar and water years | Choose the annual grouping and review incomplete boundary blocks. |
| [USGS annual instantaneous peaks](2-usgs-peak-discharge/usgs-peak-download-example.md) | Moose River and Orestimba Creek | Preserve source qualifiers and understand low-outlier flags. |
| [Orestimba flow POT](3-peaks-over-threshold/usgs-peaks-over-threshold-example.md) | 78 daily-flow events above 650 cfs | Distinguish retained-event span from full observation exposure. |
| [Big Bear precipitation POT](3-peaks-over-threshold/ghcn-peaks-over-threshold-example.md) | 233 daily precipitation events above 1 inch | Review threshold, separation, partial years and missing observations. |

## Understand the observation types

| Type | What it says | What it does not say |
|---|---|---|
| Exact | Use the recorded magnitude as a point observation for this model. | The physical measurement has literally no error. |
| Uncertain | A specified distribution describes measurement uncertainty. | Only two endpoints or an interval have been supplied. |
| Interval | The event magnitude lies between the recorded bounds. | The magnitude is uniformly distributed between those bounds. |
| Threshold | An observation window and perception level constrain the unobserved events. | Every unrecorded year had zero flow. |

MGBT low-outlier flags remain attached to the saved exact-series rows. The selected estimator determines their statistical treatment. Do not remove flagged years or infer a distinct physical population solely from the flag. Historical perception windows and measurement-error observations require their own evidence and cannot be inferred from a filename.

## Work in a reproducible sequence

1. Open a working copy and inspect the source record described in the tutorial.
2. Confirm variable, units, date convention, completeness and observation method.
3. Read the extraction settings and compare the saved input grid with chronology.
4. Inspect frequency and, for POT inputs, the threshold diagnostics. Five-step separation is an extraction rule, not proof of independence.
5. Record the chosen sample and observation exposure before creating an analysis.

Annual maxima and POT peaks need different probability interpretations. A daily-mean maximum also differs from an instantaneous annual peak. For a POT model, count years with no events when they were observed, and distinguish missing coverage from a zero event count. Current extraction retains the source calendar-year span, while the older Orestimba snapshot lacks that field; its tutorial documents the resulting saved-rate difference.

Continue to [Chapter 4](../4-univariate-distribution-analysis/README.md) for Bayesian and Bulletin 17C analyses. Use the [figure instructions](../README.md#reproducing-the-figures) to reproduce these Python figures from the saved source geometry.
