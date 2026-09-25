# Example documentation progress

Plan: [2026-09-25 example documentation](superpowers/plans/2026-09-25-example-documentation.md).

## Current state

- Session 1 complete; Session 2 in progress. Starting HEAD: `0cb6ab9eca1cedef86509cb61b82132e9b48ba57`.
- Switched to the user-requested `documentation-verification-updates` branch and fast-forwarded it to local main after fetching origin.
- Updater merge committed as `1fe9b9a`; plotting merge committed as `9fbf0ec`.
- Merged baseline: build and desktop exporter build have zero warnings/errors; 5,055 fast .NET tests and 101 Python tests passed. Three inherited Python integration tests required the sibling notebook runtime; standalone coverage passed 98 tests.
- Regression tests reproduced JSON error-response serialization, incorrect B17C uncertain-data warnings, uncertainty legend, nonfinite coordinate, contour-label, seasonality and axis-label defects. Presentation fixes are validated without changing estimation.
- Current shared package: 113 Python tests and 15 subtests pass (including 3 optional notebook integrations and 3 metadata-audit tests); all four fast .NET suites pass (5,055 tests). Strict XML gate passes across 960 source files. All 84 archived desktop variants convert and render through the new portable desktop adapter; representative frequency, contour and dated-residual PNGs were inspected.
- Inventory completed for all 32 projects. Nine data-preparation projects now have audited description updates; only the approved descriptions and ABOM test-row deletion changed. The GHCN source-unit decision is pending.
- New source-data issue: the saved Paradise GHCN snowfall has 38 nonzero days at one-tenth of NOAA snowfall. The source records use millimetres; the downloader uses the precipitation conversion for tenths of millimetres. Source audit: NOAA `USC00046685.dly`, SHA-256 `faac099a002d30adbc341a1d87a3094cd38cda7319d50a1c906663a03f6c953d`; 1975-01-29 is 445 mm but saved as 1.7519685039370079 inches. A user decision on correcting these inputs is pending; the Numerics downloader is outside this change.
- Unrelated `graphify-out/` is preserved.
- Blakely Bayesian project location remains pending; independent work continues.

## Decisions

- Work in the named checkout/branch as requested; do not create a different implementation branch.
- Preserve prior validation receipts and original backups except the two explicitly removed rating examples.
- The approved conversation is the specification; this tracked plan and progress log provide continuity across sessions.
- Validate saved results; rerun only missing/stale cases using original settings.

## Completed sessions

1. Foundation: branch reconciliation; API error serialization and B17C warning correction; portable desktop adapter and display fixes; immutable inventory, figure regeneration and transactional metadata-audit tools. Four fast .NET suites pass (5,055 tests), Python checks pass (113 tests, 15 subtests), and desktop exporter smoke checks pass (3 tests). The XML gate passed across 960 source files. Review findings on fill transparency and axis bounds/direction were corrected and independently rechecked. Old geometry snapshots need `--refresh` to capture axis direction.

## Session 2 work in progress

Ten data-preparation tutorials have new drafts. Nineteen Chapter 1 figures rendered; fifteen Chapter 2 figures are being generated. Individual visual/page review and index updates remain. An App-test failure caught a removed release-note opening in the examples index; the required opening was restored and all 444 App tests then passed.
