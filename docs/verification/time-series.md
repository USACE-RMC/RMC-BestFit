<!-- verification-status: phase-5-in-progress -->

# Time-Series Verification

This chapter records the Phase 5 verification of autoregressive, moving-average, ARIMA, and
ARIMAX models. Numerical claims require analytical, independently implemented, external-package,
or recovery evidence. API, serialization, validation, and state-transition contracts remain in
the fast test projects and are reported here as regression evidence rather than numerical
Verification methods.

## Phase 5 scope

Phase 5 covers TR-035 through TR-041 and TR-046. TR-042 remains closed and receives a refreshed
information-criterion regression. The approved compatibility boundary freezes the public and
protected contracts of `RMC.BestFit.UI` and `RMC.BestFit.App`; model-layer changes are permitted
only when these consumers and existing persisted projects remain unaffected.

The ordered numerical pipeline is:

$$
y_t \longrightarrow g_\lambda(y_t) \longrightarrow \Delta^d g_\lambda(y_t)
\longrightarrow \text{AR/MA/regression likelihood}.
$$

Prediction and simulation must reverse that order:

$$
\text{model-scale values} \longrightarrow \text{inverse differencing}
\longrightarrow g_\lambda^{-1}(\cdot).
$$

## UI and App compatibility baseline

Package 1 captured the pre-Phase-5 UI/App contract before any time-series production change.
`PublicApiContractSnapshot` includes exported types, declared public and protected constructors,
methods, properties, events, fields and enum values, optional parameter defaults, virtual/abstract
modifiers, and generic constraints. Inherited framework members and compiler-generated artifacts
are excluded. The existing Core baseline remains independent and unchanged.

| Assembly | Baseline | Lines | SHA-256 | Fast result |
|---|---|---:|---|---|
| `RMC.BestFit.UI.dll` | `RMC.BestFit.UI.Tests/CoreInfrastructure/PublicApiBaseline.txt` | 853 | `05628C483CB18629DADA80085F3C3DBC37BE648C1F61D0AA24B94BB57C291BAE` | Passed as part of UI 576/576 |
| `RMC-BestFit.dll` | `RMC.BestFit.App.Tests/CoreInfrastructure/PublicApiBaseline.txt` | 1,657 | `241EBBA9760D14D355C48F4D869256FD3DF832CADE37345612299CCEF9832091` | Passed as part of App 431/431 |

The following deterministic compatibility contracts also pass:

- `TimeSeriesModelSerializationCompatibilityTests` restores literal legacy AR, MA, ARIMA, and
  ARIMAX XML with every established configuration attribute, ignores an unknown optional
  attribute, and re-emits the established semantic settings.
- The existing UI legacy `ARMAX` database fixture continues loading into the ARIMAX wrapper.
- `TimeSeriesAnalysisControlSourceTests` pins the two-way
  `Element.ARIMAX.TransformType` XAML binding, item/value member paths, display labels, and all
  four transform enum members.

Commands executed on 20 August 2026 from base commit `8709263` plus the Package 1 working tree:

```powershell
dotnet test src\RMC.BestFit.UI.Tests\RMC.BestFit.UI.Tests.csproj -c Debug `
  --filter "FullyQualifiedName~CoreInfrastructure.PublicApiCompatibilityTests|FullyQualifiedName~TimeSeriesModelSerializationCompatibilityTests" `
  --results-directory .tmp\phase5-package1-ui

dotnet test src\RMC.BestFit.App.Tests\RMC.BestFit.App.Tests.csproj -c Debug `
  --filter "FullyQualifiedName~CoreInfrastructure.PublicApiCompatibilityTests|FullyQualifiedName~TimeSeriesAnalysisControlSourceTests" `
  --results-directory .tmp\phase5-package1-app

dotnet test src\RMC.BestFit.Tests\RMC.BestFit.Tests.csproj -c Debug --no-build `
  --results-directory .tmp\phase5-package1-core

dotnet test src\RMC.BestFit.Api.Tests\RMC.BestFit.Api.Tests.csproj -c Debug --no-build `
  --results-directory .tmp\phase5-package1-api

dotnet build RMC.BestFit.sln -c Debug -p:EnforceXmlDocumentation=true --nologo
```

The MSTest platform executed each complete fast project: UI passed 576/576 in 39.168 s and App
passed 431/431 in 2.419 s. Core passed 3,180/3,180 in 10.293 s and API passed 496/496 in
2.255 s. The strict Debug solution build passed all ten projects with zero warnings or errors in
12.85 s. The documented `scripts/validate-code-xml-docs.ps1` entry point is absent from this
checkout, so the active `EnforceXmlDocumentation=true` solution-build gate was run directly and
the missing-script discrepancy is retained in this report. No production file changed in this
package, and no Verification method was run.

## Phase 5 findings

| Finding | Status | Regression evidence | Numerical/recovery evidence |
|---|---|---|---|
| TR-035 Jeffreys component type | Approved; implementation pending | Planned Core metadata/decomposition tests | Planned independent `-log(sigma)` oracle |
| TR-036 training-only transform fitting | Approved; implementation pending | Planned holdout, state, clone, and XML tests | Planned R transform oracle and transformed recovery |
| TR-037 reintegration index | Approved; implementation pending | Planned `d=1`/`d=2` recurrence tests | Planned independent recurrence oracle and recovery |
| TR-038 AR/MA/ARIMA generation | Approved; implementation pending | Planned fixed-seed and transform-order tests | Planned algebraic and Monte Carlo oracles |
| TR-039 ARIMAX generation | Approved; implementation pending | Planned scale/order/date tests | Planned algebraic, Monte Carlo, and recovery evidence |
| TR-040 invalid scale | Approved; implementation pending | Planned scalar/pointwise guard tests | Planned independent Gaussian decomposition oracle |
| TR-041 ARIMAX alignment | Approved; implementation pending | Planned date, holdout, and Jacobian tests | Planned date-indexed likelihood oracle and recovery |
| TR-042 AIC/BIC kernel | Closed; refresh pending | Planned deterministic routing regression | Planned data-likelihood/MAP oracle |
| TR-046 manual transform rebuild | Approved with TR-036; implementation pending | Planned atomic-state and persistence tests | Planned independent likelihood oracle |

The complete Verification project is not run during Phase 5. Every numerical or recovery result
will be executed as one exact fully qualified method through `scripts/run-verification-test.ps1`.

---

[Verification index](README.md) | [Finalization plan](verification-finalization-plan.md) |
[Scientific findings](../technical-reference/review-findings.md#tr-035)
