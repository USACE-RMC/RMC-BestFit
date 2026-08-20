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

## TR-035 — Jeffreys prior metadata

**Disposition and behavior.** The confirmed defect is corrected. Before this package, AR, MA,
and ARIMA calculated the correct Jeffreys contribution but labeled it as `ParameterPrior` in
pointwise diagnostic output; ARIMAX already labeled the same contribution as
`JeffreysScalePrior`. Afterward, only those three enum arguments change. The scalar prior,
configured marginal priors, parameter order, likelihood, estimator behavior, and unchanged
ARIMAX name/value/type contract are preserved.

**Compatibility.** No public or protected signature, XAML binding, property name, enum value, or
XML element/attribute changes. UI passes 576/576, App 431/431, and API 496/496 with the Package 1
signature baselines intact. Core passes 3,183/3,183. The strict Debug solution build with
`EnforceXmlDocumentation=true` reports zero warnings and errors.

**Fast regressions.** `RMC.BestFit.Tests.TimeSeriesModels.TimeSeriesPriorMetadataTests` owns:

- `PointwisePriorMetadata_ClassifiesExactlyOneJeffreysScaleComponentWhenEnabled`;
- `PointwisePriorMetadata_SumsToScalarPriorLikelihood`; and
- `ARIMAX_JeffreysScaleMetadata_RemainsEstablishedReference`.

They assert exactly one Jeffreys component when enabled and none when disabled, the `σ` identity,
the independent `-log(sigma)` density, the unchanged ARIMAX reference metadata, and equality of
the pointwise sum with `PriorLogLikelihood` at valid defaults.

**Numerical oracle.** The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.JeffreysScaleMetadataMatchesIndependentPriorOracle`.
It reads [phase5-jeffreys-prior-oracle.json](../../verification/data/time-series/phase5-jeffreys-prior-oracle.json),
which independently tabulates $\log(1/\sigma)=-\log(\sigma)$ for AR at `sigma=0.125`, MA at
`0.5`, ARIMA at `2`, and ARIMAX at `8`. The fixed acceptance rule is `1E-12` absolute. These use
the default AR(1), MA(1), ARIMA(1,0,0), and ARIMAX(1,0,0,0) parameter layouts with no response
attached and `Transform.None`; training boundary, sample size, and random seed are not applicable.
The artifact SHA-256 is
`636a5fea60bd200af418d06ac2e9b5b840cb091a5d5078bd64ae8732fb0e7094` and matches the manifest.

**Execution evidence.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4 built against
the configured local Numerics project. R and external statistical packages were not used for
this analytical identity. From commit `6cb363e` plus the scoped TR-035 package diff, the guarded
command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.JeffreysScaleMetadataMatchesIndependentPriorOracle'
```

The final artifact-backed run built with zero warnings/errors and passed 1/1 in 0.381 s. The TRX
is under `TestResults/VerificationFocused/20260820-100331-...`. An earlier in-code analytical run
also passed 1/1 in 0.385 s; it is retained as preliminary rather than final artifact evidence.
The first Debug build exposed use of an unavailable MSTest `Assert.HasCount` helper and was
corrected to an ordinary count assertion before any test ran. A concurrent UI confirmation then
encountered the test platform's fixed-log file lock; after the original process ended, the suite
was rerun serially and passed 576/576 in 33.027 s. Neither event changed a numerical algorithm,
oracle value, seed, tolerance, prior, sampler, likelihood, or acceptance criterion.

## TR-040 — Invalid innovation-scale parity

**Disposition and behavior.** The confirmed defect is corrected. Previously, scalar data
likelihoods guarded only `sigma<=0`, while pointwise and component paths constructed a Numerics
`Normal` first; positive infinity could also reach the distribution constructor. All four models
now require finite `sigma>0` before Gaussian evaluation. Zero, a negative scale, NaN, positive
infinity, and negative infinity return exact negative infinity from scalar data/prior and combined
likelihoods. Pointwise data arrays and `DataComponent` lists retain their valid-path lengths,
indices, values, types, counts, and labels with negative-infinity contributions. Pointwise priors
retain names/types/counts and mark the scale marginal and Jeffreys term impossible.

**Compatibility.** No public/protected signature, validation exception outside numerical
evaluation, XAML binding, property, enum, or serialization change. Core passes 3,189/3,189, UI
576/576, App 431/431, and API 496/496; both UI/App signature baselines remain exact. The strict
Debug solution build with `EnforceXmlDocumentation=true` passes all ten projects with zero
warnings/errors.

**Fast regressions.** `RMC.BestFit.Tests.TimeSeriesModels.TimeSeriesInvalidScaleTests` owns the
five-row `InvalidInnovationScale_ReturnsNegativeInfinityAcrossAllPaths` contract and the
`FinitePositiveInnovationScale_RetainsValidEvaluation` control. Each row evaluates AR, MA,
ARIMA, and ARIMAX and compares rejected decomposition metadata with the corresponding valid
path. Every final recovery cell will also require a finite likelihood at its recovered parameter
set; no separate estimator run is justified for this numerical-domain guard.

**Numerical oracle.** The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.InvalidScaleBehaviorMatchesScalarAndPointwiseOracle`.
It reads [phase5-invalid-scale-oracle.json](../../verification/data/time-series/phase5-invalid-scale-oracle.json).
The raw response is `[1.25,-0.5,2,0.75,-1.5]`, with AR(1), MA(1), ARIMA(1,0,0), and
ARIMAX(1,0,0,0), zero dynamic coefficients, no intercept, `Transform.None`, five training
observations, no holdout, and `sigma=1.75`. The oracle independently applies
$-\tfrac12\log(2\pi)-\log(\sigma)-e^2/(2\sigma^2)$, uniform widths 4 and 100, and the Jeffreys
$-\log(\sigma)$ term. Expected data totals are `-7.067278509050176` for AR/ARIMA/ARIMAX and
`-8.800934871006598` for MA; expected prior total is `-6.551080335043405`. Seed and simulated
sample size are not applicable. Valid acceptance is `1E-12` absolute; every invalid result must
equal negative infinity. Artifact SHA-256
`09fb544892e8e9d268ab4fd4c3b13c6262d50755042665c2a06fcde28d0e0001` matches the manifest.

**Execution evidence and history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4
built against the configured local Numerics project; no R or external package was required. From
commit `c7b08d4` plus the scoped TR-040 diff, the final guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.InvalidScaleBehaviorMatchesScalarAndPointwiseOracle'
```

The artifact-backed method built with zero warnings/errors and passed 1/1 in 0.409 s; its TRX is
under `TestResults/VerificationFocused/20260820-101753-...`. The first compile exposed an
ambiguous test-only `Transform` enum import and was corrected by fully qualifying the established
BestFit enum. The first Core run passed all invalid-scale rows but its auxiliary `sigma=1E-4`
control assumed a finite normal density where the unchanged Numerics implementation underflows;
the control was corrected to representative `sigma=0.1` and Core then passed 3,189/3,189. No
production formula, oracle value, seed, tolerance, prior, sampler, likelihood definition, or
acceptance criterion changed. The complete Verification project was not run.

## Phase 5 findings

| Finding | Status | Regression evidence | Numerical/recovery evidence |
|---|---|---|---|
| TR-035 Jeffreys component type | Complete | Three Core metadata/decomposition regressions pass | Analytical four-scale oracle passes 1/1 at `1E-12` |
| TR-036 training-only transform fitting | Approved; implementation pending | Planned holdout, state, clone, and XML tests | Planned R transform oracle and transformed recovery |
| TR-037 reintegration index | Approved; implementation pending | Planned `d=1`/`d=2` recurrence tests | Planned independent recurrence oracle and recovery |
| TR-038 AR/MA/ARIMA generation | Approved; implementation pending | Planned fixed-seed and transform-order tests | Planned algebraic and Monte Carlo oracles |
| TR-039 ARIMAX generation | Approved; implementation pending | Planned scale/order/date tests | Planned algebraic, Monte Carlo, and recovery evidence |
| TR-040 invalid scale | Complete | Six Core invalid/valid parity cases pass | Gaussian/prior oracle passes 1/1 at `1E-12`/exact rejection |
| TR-041 ARIMAX alignment | Approved; implementation pending | Planned date, holdout, and Jacobian tests | Planned date-indexed likelihood oracle and recovery |
| TR-042 AIC/BIC kernel | Closed; refresh pending | Planned deterministic routing regression | Planned data-likelihood/MAP oracle |
| TR-046 manual transform rebuild | Approved with TR-036; implementation pending | Planned atomic-state and persistence tests | Planned independent likelihood oracle |

The complete Verification project is not run during Phase 5. Every numerical or recovery result
will be executed as one exact fully qualified method through `scripts/run-verification-test.ps1`.

---

[Verification index](README.md) | [Finalization plan](verification-finalization-plan.md) |
[Scientific findings](../technical-reference/review-findings.md#tr-035)
