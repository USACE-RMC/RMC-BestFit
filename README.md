# RMC-BestFit

RMC-BestFit is an open-source .NET codebase for Bayesian estimation, distribution fitting, and frequency analysis in flood risk and water resources engineering. Version 2.0 separates the reusable statistical model library from the desktop application, UI/project layer, and service interfaces so the core methods can be tested, packaged, and reused directly from .NET.

> [!IMPORTANT]
> Public source migration is in progress. The model library and fast unit-test suite are being prepared first. Long-running verification code, verification datasets, and additional validation reports will be added after public review.

## What Is Included

- `RMC.BestFit` model library for data frames, probability models, Bayesian and point-estimation workflows, diagnostics, frequency curves, rating curves, time-series models, bivariate analysis, and spatial-extremes support.
- Fast unit tests for the public model-library surface.
- UI, desktop application, and API source from the 2.0 migration. These projects remain in the repository, but public CI initially validates the portable model library while HEC-DSS packaging is finalized.
- Technical reference documentation and examples for data import, univariate analysis, distribution fitting, bivariate/coincident frequency analysis, rating curves, and time-series analysis.

## NuGet

The first public package target is the model library:

```powershell
dotnet add package RMC.BestFit --version 2.0.0
```

The package contains the portable `RMC.BestFit` library and depends on `RMC.Numerics` 2.x.

## Build And Test

Requirements:

- .NET 10 SDK
- Windows is required for WPF UI/App projects
- The model library and model tests are portable .NET projects

Model-library development loop:

```powershell
dotnet restore src/RMC.BestFit.Tests/RMC.BestFit.Tests.csproj
dotnet build src/RMC.BestFit.Tests/RMC.BestFit.Tests.csproj -c Release --no-restore
dotnet test src/RMC.BestFit.Tests/RMC.BestFit.Tests.csproj -c Release --no-build
```

Create the public NuGet package locally:

```powershell
dotnet pack src/RMC.BestFit/RMC.BestFit.csproj -c Release -o packages /p:Version=2.0.0
```

## Repository Layout

```text
src/RMC.BestFit/              Core model library and analysis code
src/RMC.BestFit.Tests/        Fast unit tests for the model library
src/RMC.BestFit.UI/           Project, serialization, and UI wrapper layer
src/RMC.BestFit.App/          WPF desktop application
src/RMC.BestFit.Api/          REST API and MCP server surface
docs/                         Public technical documentation
examples/                     Example projects and datasets
```

The long-running verification project is intentionally excluded from the public repository for the initial 2.0 source release. Verification materials are being prepared for a follow-on publication.

## Capabilities

RMC-BestFit 2.0 supports Bayesian-first flood-frequency and statistical analysis workflows, including:

- Bayesian MCMC, maximum likelihood, maximum a posteriori, and generalized method of moments estimation.
- Exact, censored, threshold, interval, historical, paleoflood, and uncertain observations.
- Quantile priors and parameter priors for incorporating engineering judgment.
- Univariate, bivariate, mixture, competing-risk, and peaks-over-threshold analyses.
- Rating-curve and time-series analysis support.
- Posterior and prior predictive checks, influence diagnostics, information criteria, and model comparison outputs.

## Documentation

- [Getting started](docs/getting-started.md)
- [API overview](docs/api.md)
- [Technical reference](docs/index.md)
- [References](docs/references.md)
- [Version 1.0 verification report](https://usace-rmc.github.io/RMC-Software-Documentation/source-documents/desktop-applications/rmc-bestfit/verification-report/RMC-BestFit-Verification-Report.pdf)
- [Version 1.0 user's guide](https://usace-rmc.github.io/RMC-Software-Documentation/docs/desktop-applications/rmc-bestfit/users-guide/v1.0/preface/)

## Contributing

Bug reports, feature requests, documentation feedback, and independent validation results are welcome. Please read [CONTRIBUTING.md](CONTRIBUTING.md) before opening a pull request. Review capacity is limited, so proposed code changes should start with an issue discussion.

## Authors

- C. Haden Smith, U.S. Army Corps of Engineers Risk Management Center
- Woodrow L. Fields, U.S. Army Corps of Engineers Risk Management Center
- Brian Skahill, Portland State University

## License

RMC-BestFit is provided under the [Zero-Clause BSD (0BSD)](LICENSE) license.