# Progress

## 2026-07-14

- Replaced the `MainProjectNode` local Quick Start Guide PDF action with the online RMC-BestFit User Guide.
- Added a testable connectivity and default-browser launcher that reuses `TimeSeriesDownload.IsConnectedToInternet()`.
- Added deterministic App tests for online, offline, validation, failure propagation, and Help-menu wiring behavior.
- Added main-branch Technical Reference and Example Projects links through the generalized online Help launcher.
- Added active-development note boxes to the technical-reference and example-project indexes.
- Reserved the type-compatible `HelpImage` resource for the User Guide and separated the text-only online resources from application items.
- Replaced the framework's default Terms and Conditions document with the verbatim 0BSD license and optional Zenodo citation guidance.
- Added a README Documentation callout identifying the RMC-BestFit 2.0 documentation and verification materials as under active development.
- Validated the affected App projects with zero warnings and all fast Core, UI, and App tests passing.
- Repository-wide validation remains blocked by pre-existing solution nesting and `YeoJohnsonLink.FitLambda` XML `cref` errors; the required validation script is also absent.
