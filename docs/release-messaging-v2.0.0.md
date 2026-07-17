# RMC-BestFit v2.0.0 Release Messaging

## PR Message

Title: `Prepare v2.0.0 official release`

```markdown
## Summary

This PR prepares RMC-BestFit for the official v2.0.0 release.

It switches production dependencies back to published NuGet packages, removes beta.5 metadata, synchronizes App/UI/project serialization version strings, and adds a repeatable release workflow for future BestFit releases.

## Release Significance

Version 1.0 focused on three core desktop workflows:

- Input Data
- Distribution Fitting
- Univariate Analysis

Version 2.0.0 is a major expansion into a full Bayesian hydrologic analysis platform, including:

- Time Series elements and download workflows
- Peaks-over-threshold data creation
- Nonstationary frequency analysis
- Mixture models
- Composite workflows for competing risks, mixtures, and model averaging
- Point process analysis
- Bivariate analysis
- Coincident frequency analysis
- Rating curve analysis
- Time series analysis
- A reusable `RMC.BestFit` .NET model-library package

## Validation

- [ ] `dotnet restore`
- [ ] `dotnet build -c Debug`
- [ ] `dotnet build -c Release`
- [ ] `dotnet test src/RMC.BestFit.Tests -c Debug --no-build`
- [ ] `dotnet test src/RMC.BestFit.UI.Tests -c Debug --no-build`
- [ ] `dotnet test src/RMC.BestFit.App.Tests -c Debug --no-build`
- [ ] `dotnet test src/RMC.BestFit.Api.Tests -c Debug --no-build`
- [ ] `dotnet pack src/RMC.BestFit/RMC.BestFit.csproj -c Release -o packages /p:Version=2.0.0`
- [ ] Inspect NuGet package metadata and dependencies
```

## GitHub Release Message

Title: `RMC-BestFit v2.0.0`

````markdown
RMC-BestFit v2.0.0 is the official release of the next generation of BestFit.

Version 1.0 gave users a focused set of tools for Input Data, Distribution Fitting, and Univariate Analysis. Version 2.0.0 transforms BestFit into a much broader Bayesian hydrologic analysis platform, with new data workflows, new model families, modern diagnostics, expanded uncertainty analysis, and the first public `RMC.BestFit` .NET model-library package.

## Major New Capabilities

- **Time Series Elements and Download APIs**
  - Create, manage, plot, and transform time series directly in BestFit.
  - Download hydrologic and precipitation time series from supported public data sources.
  - Build block-maximum and peaks-over-threshold datasets from time series workflows.

- **Peaks-Over-Threshold Workflows**
  - Extract independent threshold exceedance events from time series.
  - Use diagnostic plots and threshold controls to support POT model setup.
  - Move from raw time series to frequency-analysis-ready input data inside one project.

- **Nonstationary Frequency Analysis**
  - Fit models where distribution parameters vary with time or covariates.
  - Evaluate changing flood-frequency behavior with Bayesian uncertainty propagation.
  - Support engineering workflows where stationarity is no longer a defensible assumption.

- **Mixture Models**
  - Represent multiple flood-generating populations in a single analysis.
  - Estimate mixture weights and component distributions with uncertainty.
  - Support hydrologic settings where one distribution cannot represent all processes.

- **Composite Analysis**
  - Combine alternatives through competing-risk, mixture, and model-averaging workflows.
  - Use information criteria such as WAIC, LOO-CV, DIC, and AIC to support model comparison and weighting.
  - Carry model uncertainty into frequency estimates instead of forcing a single-model decision too early.

- **Point Process Analysis**
  - Fit point process / peaks-over-threshold models for extreme-value analysis.
  - Support workflows that use more information than annual maxima alone.
  - Connect threshold exceedance data directly to Bayesian frequency estimates.

- **Bivariate Analysis**
  - Model dependence between paired hydrologic variables using copula-based workflows.
  - Estimate joint behavior for variables that cannot be treated independently.
  - Support applications such as flow-stage, inflow-volume, and other paired-risk problems.

- **Coincident Frequency Analysis**
  - Evaluate response-frequency relationships driven by dependent variables.
  - Support joint exceedance and conditional frequency workflows.
  - Bring bivariate dependence into practical risk and design calculations.

- **Rating Curve Analysis**
  - Fit stage-discharge relationships with uncertainty.
  - Support multi-segment rating curves for more complex hydraulic controls.
  - Carry rating-curve uncertainty into downstream hydrologic assessment workflows.

- **Time Series Analysis**
  - Fit time series models, including AR/MA/ARIMA/ARIMAX-style workflows.
  - Include covariates and forecasting workflows where appropriate.
  - Extend BestFit beyond frequency curves into temporal modeling and prediction.

## Platform and Developer Updates

- `RMC.BestFit.dll` is now separated from the desktop UI and available as a reusable model library.
- The desktop application, UI/project layer, and API source remain in the same repository.
- The project is modernized for .NET 10.
- The release uses published `RMC.Numerics` and `RMC.Wpf.Framework` NuGet dependencies.
- The software is released under the permissive 0BSD license.

## Install the Model Library

```bash
dotnet add package RMC.BestFit --version 2.0.0
```

## Thank You

Thank you to everyone who tested the 2.0 beta releases, opened projects, reported issues, reviewed outputs, and pushed the software through real flood-risk workflows. Version 2.0.0 is a major step forward for Bayesian hydrologic analysis in BestFit.
````

## LinkedIn Post

```markdown
RMC-BestFit 2.0.0 is officially released.

This is not a small update. Version 1.0 focused on three core workflows: Input Data, Distribution Fitting, and Univariate Analysis.

Version 2.0.0 turns RMC-BestFit into a much broader Bayesian hydrologic analysis platform.

New and expanded capabilities include:

- Time Series elements for managing, plotting, transforming, and analyzing temporal hydrologic data
- Download workflows for bringing public time series data directly into BestFit projects
- Peaks-over-threshold tools for extracting independent extreme events from time series
- Nonstationary flood frequency analysis with time- and covariate-varying parameters
- Mixture models for flood records influenced by multiple generating processes
- Composite analysis workflows for competing risks, mixtures, and model averaging
- Point process analysis for threshold exceedance frequency modeling
- Bivariate analysis with copula-based dependence modeling
- Coincident frequency analysis for joint and conditional risk workflows
- Rating curve analysis with uncertainty for stage-discharge relationships
- Time series analysis, including AR/MA/ARIMA/ARIMAX-style workflows
- Bayesian diagnostics, model comparison, posterior uncertainty propagation, and information-criterion-based model averaging
- A reusable .NET model library, `RMC.BestFit`, available as a NuGet package for programmatic workflows

The goal of RMC-BestFit 2.0 is to help engineers and researchers carry more of the real uncertainty in hydrologic data, models, and assumptions into flood-risk decisions.

I am especially excited to share a screenshot gallery showing the new time series tools, POT workflows, nonstationary analysis, mixture/composite models, bivariate and coincident frequency tools, rating curves, and Bayesian diagnostics.

Huge thanks to everyone who tested the beta releases, reviewed examples, reported issues, and helped make this official release stronger.

RMC-BestFit 2.0.0 is a big step forward for Bayesian flood frequency and hydrologic risk analysis.
```

## Screenshot Gallery Suggestions

- Time Series element with downloaded USGS/GHCN data
- POT extraction workflow and threshold diagnostics
- Nonstationary univariate analysis with covariates/trend controls
- Mixture or competing-risk analysis results
- Composite model averaging results table/plot
- Point process analysis frequency curve
- Bivariate copula analysis
- Coincident frequency response surface
- Multi-segment rating curve fit
- Time series analysis forecast/diagnostic plots
- Bayesian diagnostics: trace, posterior density, autocorrelation, influence/LOO-style diagnostics