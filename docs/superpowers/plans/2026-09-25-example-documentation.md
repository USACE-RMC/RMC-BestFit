# BestFit example documentation implementation plan

Approved in the task conversation on 2026-09-25. Work on `documentation-verification-updates`; no push or publication is authorized.

## Goal and constraints

Complete the example teaching collection with accurate SQLite descriptions, source-backed results tables, Python figures matching desktop plotting conventions, and portable skill references. Preserve numerical methods, inputs, settings, seeds, priors, tolerances, and saved result payloads. Use compatible saved results and rerun only missing or stale cases with their original settings. Any scientific configuration change needs Haden Smith's decision.

The initial inventory contains 41 Markdown files, 32 active projects and 152 missing image links. Remove the Susquehanna and Middle Fork Willamette tutorials and their two backups; remove ABOM's manual `Time Series_7` test element. Use Haden's existing Blakely Bayesian project once its path is supplied; do not invent a replacement.

## Sessions

1. Reconcile main, fix-software-updater and python-example-retooling into documentation-verification-updates. Preserve both existing skill workflows. Repair relevant plotting/export defects, establish example inventory and reproducible export/render/description-audit tooling.
2. Complete the ten time-series-data and input-data tutorials, including sources, sampling conventions, screening and threshold diagnostics.
3. Complete stationary Bayesian examples: Viglione, ARR/FLIKE, Bayesian Bulletin 17C collection, Sinnemahoning and the supplied Blakely project.
4. Complete Bulletin 17C examples, Blakely including metric case, and Sinnemahoning; distinguish GMM/EMA and frequentist/Bayesian uncertainty.
5. Complete Brays Bayou, OC Fisher, synthetic nonstationary and point-process tutorials.
6. Complete mixture, composite, copula, sum-of-Normals and Waimea tutorials.
7. Complete synthetic/Mississippi rating curves, synthetic/classic time series and regression tutorials.
8. Render and inspect every retained tutorial and figure; finish indexes, skill references, packaging and completion evidence.

For each example: inspect saved observations/configuration/results and primary references; update descriptions with a cell-preservation audit; replace generic prose and tables; export model-owned coordinates; extend the shared plotting library as needed; generate PNG/SVG/PlotSpec; inspect all figures and the rendered page; validate and commit the logical package.

## Interfaces

- The maintained renderer is `skills/bestfit-frequency/bestfit_plots`; retain existing commands and PlotSpec compatibility.
- Numerical preparation remains in BestFit/Numerics. Python handles display only.
- Use the desktop reference exporter on disposable project copies. No migration/save operation may alter source observations or saved results.
- Keep an inventory mapping project, element, tutorial, figure, provenance and completion state. Store current progress in `docs/example-documentation-progress.md`.
- Package an examples reference guide with both skill/plugin formats. Do not require the sibling notebook checkout to regenerate the repository tutorials.

## Validation

Run relevant Python, exporter, packaging and documentation checks; all four fast .NET suites are mandatory for completed packages. Run the XML gate for C# documentation changes. No blanket Verification run. Database integrity and all unapproved cell changes must be checked. Inspect every PNG and every rendered Markdown page, not just contact sheets. No placeholder figures, empty template tables, broken local links or unsupported claims may pass completion.

## Consultation checkpoints

Blakely Bayesian source path is pending. Obtain missing study rationale for Blakely penalties, MOVE.3 provenance/dependence, Brays/OC Fisher trend interpretation, and Waimea response-surface/RR-prior provenance when primary sources do not resolve them. Preserve the mixed-method Bulletin 17C project as a documented comparison unless directed otherwise. Preserve and disclose Nile's source-date discrepancy and Airline's diagnostic limitations; do not tune them to make examples appear successful.

Each session ends in validated local commits and a handoff stating branch/HEAD, checks, completed examples and unresolved questions. Preserve unrelated work, including `graphify-out/`.
