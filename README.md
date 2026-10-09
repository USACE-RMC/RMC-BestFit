# RMC-BestFit

The `rmc-bestfit` plugin provides the maintained `bestfit-frequency` skill for
statistical analysis and plotting with RMC-BestFit. It organizes historical and
regional flood evidence, prepares input chronology, and runs supported flood
frequency workflows through the local API. It also renders supplied frequency,
bivariate, rating-curve, time-series, regression, and diagnostic results as PNG
and SVG without starting a new fit. Numerical estimation remains in BestFit;
the bundled Python helpers prepare requests and render its results.

## Requirements and setup

The plugin contains instructions, readable Python helpers, and synthetic teaching
inputs. It does not include BestFit binaries or runtimes. A new analysis requires
a compatible [RMC-BestFit v2.0.1 source checkout](https://github.com/USACE-RMC/RMC-BestFit/tree/v2.0.1),
a terminal with a writable workspace, the .NET 10 SDK, Python and its listed
dependencies, package network access, a persistent child process, and loopback
HTTP access in the same execution environment. The compatible source uses
RMC.Numerics 2.2.0. Plugin version 0.3.3 is separate from application version 2.0.1.

Read the bundled [setup instructions](skills/bestfit-frequency/references/setup.md)
before execution. They describe cloning the compatible source, installing Python
dependencies, building and starting the headless API, and retaining provenance.
The Python clients send analysis inputs to that session-local API. Outputs and
source evidence are written to the workspace. Research can fetch public source
material, and setup fetches source and dependencies; this package does not
configure a hosted connector or automatically start a service when installed.

Read the bundled [plugin privacy policy](PRIVACY.md), also available at the
[public policy URL](https://github.com/USACE-RMC/RMC-BestFit/blob/main/docs/plugin-privacy.md),
for information processed, recipients, retention, and user controls.

## Use and verification

In Claude Code, invoke `/rmc-bestfit:bestfit-frequency` after installing the
plugin. Ask it to prepare the bundled synthetic study and display its chronology
before fitting. Follow the [installation and live acceptance guide](skills/bestfit-frequency/references/install.md)
for other surfaces and subsequent analysis checks.

Successful installation or package validation does not prove that a particular
Claude session supports the required runtime. Report missing capabilities and
retain the actual session's validation evidence. Integration checks demonstrate
execution and artifact delivery; they do not establish scientific validity.
Preserve original inputs, effective configuration, diagnostics, and unresolved
engineering judgments. Final engineering adoption remains the user's decision.

## License and source

Published by the USACE Risk Management Center under the
[0BSD license](skills/bestfit-frequency/LICENSE). The application, maintained skill,
and issue tracker are in the [RMC-BestFit repository](https://github.com/USACE-RMC/RMC-BestFit).
