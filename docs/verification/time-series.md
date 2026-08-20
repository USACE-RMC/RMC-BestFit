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

## TR-036 and TR-046 — Atomic transform-state lifecycle

**Disposition and behavior.** Both confirmed defects are corrected as one state contract. Before
this package, fitted Box-Cox and Yeo-Johnson exponents used the complete response, so holdout
values could change calibration, and `SetTransformParameters` changed backing fields without
rebuilding the transformed response, differences, Jacobian, defaults, or analysis results. After
this package, AR, MA, ARIMA, and ARIMAX fit the exponent from raw observations
`[0, TrainingTimeSteps)` and then apply that frozen value to the complete response. A manual
exponent atomically rebuilds every dependent model state; existing analysis handlers receive the
established transform-change notification and clear results. ARIMAX default parameter
initialization now uses only its transformed/differenced training prefix, preventing holdout
leakage through default values.

`TransformLambda` is an additive, read-only, `[Browsable(false)]` model property. The unchanged
`SetTransformParameters(double lambda1 = 0, double lambda2 = 0)` signature accepts `lambda2`
without inspecting it; the second value remains an intentionally ignored compatibility
placeholder. A non-finite `lambda1` is rejected. `Transform.None` and `Transform.Logarithmic`
canonicalize the exponent to zero and discard manual intent. Automatic exponents refit when the
response, training boundary, or transform type changes; a manual exponent remains fixed when only
the training boundary changes. Changing `TransformType` resets manual state and recomputes.

**Persistence and compatibility.** Existing XML names and meanings are preserved. New XML adds
invariant-culture optional `TransformLambda` and `TransformLambdaIsManual` attributes. Missing
attributes retain the legacy automatic-fit path. A restored fitted value is used directly for the
loaded response and refits after a later data or training-window change; a restored manual value
remains fixed across training-window changes. Model clone, UI copy, save/open, undo/redo, and API
request/result mapping preserve the same effective state. Legacy XML, new XML, and XML with an
unknown optional attribute pass. The UI and App public/protected signature baselines remain exact;
the Core baseline differs only by the approved four additive `TransformLambda` getters. Existing
prediction, generation, and component tuple signatures are unchanged.

**Fast regressions.** `RMC.BestFit.Tests.TimeSeriesModels.TimeSeriesTransformStateTests` owns the
read-only/non-browsable surface, atomic rebuild, ignored-`lambda2`, holdout isolation, fitted versus
manual provenance, XML/clone, canonicalization, invalid-primary-exponent, differenced-state, and
analysis-invalidation contracts. UI tests add
`TimeSeriesModelSerializationCompatibilityTests.ManualTransformLambda_NewXml_RoundTripsAllModelTypes`
and `TimeSeriesAnalysisTests.ManualTransformLambda_CopyUndoAndRedoPreserveEffectiveState`. API
DTO/service tests cover omitted and supplied request values, JSON wire names, all four family
mappings, and invalid/irrelevant request combinations. The complete fast results are Core
3,201/3,201 in 8.467 s, UI 578/578 in 41.087 s, App 438/438 in 1.904 s, and API 498/498 in
1.964 s. UI/App signature checks are included in those complete runs. The final strict Debug
solution build with `EnforceXmlDocumentation=true` passed all ten projects with zero warnings and
errors in 6.79 s. The documented XML-validation script remains absent from this checkout.

**Independent oracle.** Both exact methods read
[phase5-transform-lambda-oracle.json](../../verification/data/time-series/phase5-transform-lambda-oracle.json),
which was generated and committed at `adc61c7` before C# output was evaluated. The R generator
independently implements the Box-Cox and Yeo-Johnson profile likelihoods, transformation/Jacobian
formulas, and fixed-coefficient conditional Gaussian recurrences. The fitting fixtures use raw
training prefixes of six observations with three holdout observations: positive
`[1.1,1.3,1.8,2.7,5,12]` for Box-Cox and mixed-sign `[-4,-2,-0.5,0.5,2,4]` for Yeo-Johnson.
Expected exponents are `-0.541199913033891` and `1.00000001490262`. Alternate holdouts are
`[1800,0.031,5400]` and `[-1200,900,0.01]` and must not change training state.

The manual fixture uses ten raw values, `TrainingTimeSteps=8`, Yeo-Johnson `lambda=0.6`,
`phi=0.35`, `theta=-0.25`, and `sigma=0.8`. The expected AR/ARIMA/ARIMAX conditional likelihood
is `-18.8438349293109`; the MA likelihood is `-66.4277204273909`. No random seed or simulated
sample size applies. Cross-language acceptance was fixed at `1E-8` absolute or `1E-7` relative;
deterministic state identities use `1E-12`. R 4.4.3, jsonlite 2.0.0, and digest 0.6.39 produced
the artifact. Generator SHA-256 is
`decc17e5b0c66ac07dc07b2b1cacc43c2c366e9cc2b6ca3fb58be4e8f0e78410`; artifact SHA-256 is
`4bf26766a7f7f0598d29b9ab1bb9d5e6d85c57d981064a930dbc6befa95d783c`.

**Execution evidence and history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4
built against the configured local Numerics project. From commit `adc61c7` plus the scoped
production/test diff, these guarded commands ran separately:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.TransformLambdaMatchesIndependentTrainingOnlyOracle'

& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ManualTransformLambdaRebuildMatchesIndependentLikelihoodOracle'
```

Both focused builds reported zero warnings/errors. The methods passed 1/1 in 0.523 s and 1/1 in
0.271 s; their TRX directories begin `20260820-105550-...` and `20260820-105603-...`. The initial
guarded invocation was sandbox-blocked while reading the installed NuGet configuration and ran no
test; the same exact command then ran with approved SDK access. Earlier compilation/regression
runs exposed an ambiguous test enum, an unavailable MSTest helper, no-data canonicalization, and
ARIMAX default initialization from the full differenced series. Those package defects were fixed.
A preliminary strict solution build also reported one warning in the concurrently modified,
out-of-scope `ResultsMapperTests.cs`; after that independent edit settled, the unchanged Package 4
tree passed the clean strict build recorded above.
A differenced ARIMAX state test also underflowed through the already recorded TR-041 holdout-index
defect; its deterministic state fixture was narrowed so Package 4 does not pre-empt Package 5's
approved alignment correction. No seed, tolerance, prior, sampler, optimizer, likelihood
definition, or convergence default changed. The complete Verification project was not run.

## TR-041 — ARIMAX differencing, date, covariate, and Jacobian alignment

**Disposition and behavior.** The confirmed defect is corrected for likelihood, pointwise
decomposition, residuals, validation, and residual plotting. Previously, a raw training prefix of
`T` observations selected the first `T` differences, so `d>0` admitted holdout responses; Numerics
differencing reset the first result to the first raw date; ARIMAX selected covariates by position
`k` even though model step `k` represented raw response `k+d`; the conditional start included the
covariate lag order; and the transform Jacobian covered the wrong raw observations. The App then
indexed the shorter differenced residual series with the raw training count.

After correction, one map governs these paths. A raw training prefix `[0,T)` produces exactly
`T-d` transformed differences. Model step `k` maps to raw response index `r=k+d` and retains the
timestamp of that later raw observation. Conditional evaluation starts at `k=max(p,q)`. Each
level covariate is selected by exact timestamp at the corresponding raw-response date and is never
differenced; its lag `j` uses the date at model step `k-j`. The Box-Cox/Yeo-Johnson Jacobian covers
raw response indices `d+max(p,q)` through `T-1`. Response and covariate values at raw indices
`T...N-1` cannot enter training state, defaults, residuals, or likelihood. The residual plot uses
the differenced training count and those preserved dates.

Missing or duplicate covariate timestamps required by the training map emit explicit validation
errors. Scalar evaluation returns exact negative infinity, while pointwise arrays and component
lists retain their documented conditional lengths and metadata with negative-infinity values.
There is no positional fallback. Extra covariate dates outside the required window are harmless.
This package does not change the separately scoped prediction reintegration or generation
algorithms; TR-037 and TR-039 remain open.

**Compatibility.** No public or protected UI/App signature, XAML binding, property name, enum,
existing XML element/attribute meaning, prediction/generation tuple, or `GenerateRandomValues`
signature changed. The Core public API is also unchanged in this package. The final full fast runs
pass Core 3,208/3,208, UI 578/578, App 440/440, and API 498/498. Both captured UI/App signature
baselines match exactly. The strict Debug solution build with `EnforceXmlDocumentation=true`
passes all ten projects with zero warnings/errors in 3.03 s. The documented
`scripts/validate-code-xml-docs.ps1` command remains absent from this checkout, so the active
strict solution-build fallback was used.

**Fast regressions.** `RMC.BestFit.Tests.TimeSeriesModels.ARIMAXAlignmentTests` owns seven
deterministic tests:

- `Differencing_PreservesLaterRawDatesAndTrainingBoundary` covers `d=0,1,2`, later raw dates,
  full/training difference values, and the exact `T-d` boundary;
- `TrainingState_IsolatedFromResponseAndCovariateHoldout` mutates both holdout tails and pins
  training values, defaults, residuals, and likelihood;
- `ShiftedCovariate_IsRejectedWithoutPositionalFallback` uses a same-length one-period shift and
  pins validation, exact negative infinity, and decomposition shapes;
- `CovariateValidation_RejectsRequiredDuplicatesAndAllowsExtraDates` separates required-date
  uniqueness from harmless outside-window dates; and
- `CovariateTimestampMutation_AtomicallyRefreshesNumericalAlignment` proves direct timestamp
  edits invalidate and restore numerical alignment without a separate validation call; and
- `ConditionalOrderChanges_RebuildAlignedJacobian` proves post-attachment AR/MA order changes
  rebuild the conditional Jacobian range; and
- `DifferencedLikelihood_UsesDateIndexedLevelCovariateAndAlignedJacobian` hand-computes a
  differenced recurrence and pins scalar/pointwise/component equality at `1E-12`.

`RMC.BestFit.App.Tests.GUI.TimeSeriesAnalysisControlSourceTests.ResidualPlot_UsesDateAlignedDifferencedCount`
pins the App plot loop to the shorter differenced count and retained response timestamps. UI/App
signature and legacy/new XML regressions are part of their complete passing suites.

**Independent R oracle.** The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaxDifferencedLikelihoodMatchesDateIndexedIndependentOracle`.
It reads [phase5-arimax-alignment-oracle.json](../../verification/data/time-series/phase5-arimax-alignment-oracle.json),
which was generated and committed at `4e3f42c` before C# output was evaluated. Its independent R
implementation applies Box-Cox transformation, successive first differences with later raw dates,
an exact-date join to undifferenced level covariates, the declared ARMA recurrence, the conditional
change-of-variable Jacobian, and the Gaussian log density without calling production code.

The fixed daily fixture begins `2001-02-03`, contains ten positive raw responses and a dated,
time-varying covariate, and uses `TrainingTimeSteps=8`, two holdout observations, Box-Cox
`lambda=0.4`, `p=q=1`, `b=0`, intercept `0.25`, level-covariate coefficient `1.1`, `phi=0.3`,
`theta=-0.2`, and `sigma=0.75`. It evaluates `d=0,1,2`. Alternate response holdout values are
`[3002,0.041]`; the artifact contains every expected date, transformed difference, mapped raw
index, matched covariate, prediction, residual, Jacobian, pointwise value, and scalar likelihood.
No random seed or simulated sample size applies. The tolerance was fixed at `1E-10` absolute.

R 4.4.3, jsonlite 2.0.0, and digest 0.6.39 produced the artifact against source commit
`ac661e9229d661f89064eca4f3b035a4e67e90bd`. Generator SHA-256 is
`27a8d86bc8a0f6b2488674d2e6899735b5d83d7ea4fd703a98b00e0e8f8058f8`; artifact SHA-256 is
`11d82c2c984989e2f2bc177d6293453b43387b3c58fc7e738e95eb845ec22b21`. Both match the manifest.

**Execution evidence and history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4
built against the configured local Numerics project. From oracle commit `4e3f42c` plus the scoped
Package 5 production/test diff, the guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaxDifferencedLikelihoodMatchesDateIndexedIndependentOracle'
```

The final focused build reported zero warnings/errors and the exact method passed 1/1 in 0.370 s.
Its TRX is under `TestResults/VerificationFocused/20260820-113316-...`. Earlier pre-finalization
package runs also passed 1/1 in 0.541 s and 0.406 s and are retained as preliminary evidence. The first guarded attempt
could not read the installed NuGet configuration inside the filesystem sandbox and ran no test;
the identical exact command then passed with approved SDK access. Initial regression compilation
exposed test-only `TimeSeries` namespace and `Transform` enum ambiguities, which were resolved by
explicit aliases. The test platform ignored a requested fast-test filter, ran the complete Core
project, and one unrelated cancellation timing test failed transiently; the unchanged test passed
in the final serial Core run of 3,208/3,208. The first strict build after adding the conditional-order
regression reported two missing test XML parameter tags; documentation was completed and the final
strict build passed. No seed, tolerance, prior, sampler, optimizer,
likelihood definition, or convergence default was changed. The complete Verification project was
not run.

## TR-037 — ARIMA and ARIMAX prediction reintegration

**Disposition and behavior.** The confirmed off-by-one defect is corrected. Previously, both
prediction paths allocated `T+h` entries on the differenced scale, overwrote the first entry with
an integration anchor, and consequently discarded the first stored difference while shifting the
remaining recurrence. For `d>0`, they now calculate exactly `T-d+h` transformed-difference
values. Model step `k` maps to raw slot `k+d`; inverse differencing begins with the first `d`
observed transformed levels and reconstructs exactly `T+h` transformed levels; the inverse
transform is applied once after reconstruction. For `d=0`, the original recursion, draw order,
and fixed-seed values are retained bit for bit.

Every existing prediction tuple signature and component name remains unchanged. Component arrays
have raw output length `T+h`; slots `0...d-1` are zero conditioning entries, and model component
`k` is stored at raw slot `k+d`. ARIMAX prediction uses the Package 5 exact-date level-covariate
map for both observed and regularly extended response dates. No generation path changes in this
package; TR-038 and TR-039 remain open.

**Compatibility.** No UI/App public or protected signature, XAML binding, property name, enum,
or serialization meaning changed. The UI and App signature-baseline tests pass in their complete
suites. Core passes 3,213/3,213, UI 578/578, App 440/440, and API 498/498. The strict Debug
solution build with `EnforceXmlDocumentation=true` passes all ten projects with zero warnings or
errors in 9.56 s. The documented XML-validation script remains absent from this checkout.

**Fast regressions.** `TimeSeriesPredictionReintegrationTests` covers ARIMA `d=1` linear
reconstruction with zero and positive forecast horizons, ARIMA `d=2` quadratic reconstruction,
ARIMAX `d=1` exact-date level covariates, logarithmic ARIMA/ARIMAX integration before inverse
transformation, raw output/component lengths, and the zero conditioning prefix. Its fixed-seed
control pins every established `Transform.None`, `d=0` output and component value bit for bit for
both models. The older `ARIMAX_Predict_Differenced_TrainingCIBoundedBySigma` regression was
removed because it required re-anchoring every in-sample prediction to the preceding observation,
which directly contradicted the approved complete-path reintegration contract. The replacement
tests evaluate exact recurrence identities without Monte Carlo thresholds.

**Independent oracle.** The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle`.
The embedded analytical oracle uses ARIMA(0,2,0) with intercept two, raw training levels
`[1,4,9,16,25,36]`, `T=6`, `h=2`, `Transform.None`, and no innovations (`seed=-1`); the first
two levels and constant second differences produce `[1,4,9,16,25,36,49,64]`. Its ARIMAX case
uses ARIMAX(0,1,0,0), no intercept, eight exact-date level-covariate values
`[999,1,1,1,1,1,1,1]`, coefficient one, raw training levels `exp(1)...exp(6)`, `T=6`, `h=2`,
`Transform.Logarithmic`, and no innovations. The resulting model-scale first differences are one
and the raw oracle is `exp(1)...exp(8)`. The fixed absolute tolerance is `1E-10`; no external
package, stochastic sample, or external artifact is applicable. The verification source SHA-256
is `FEB8326A8F70EA56463F8515E16E162F8301583B2BAE65A3C656946D6814B577`.

**Execution evidence and history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4
built against the configured local Numerics project. From commit `02f766b` plus the scoped
Package 6 production/test/report diff, the guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle'
```

The exact method passed 1/1 in 0.393 s. Its TRX is under
`TestResults/VerificationFocused/20260820-114723-...`. The first guarded attempt could not read
the installed NuGet configuration inside the filesystem sandbox and ran no test; the identical
exact command then passed with approved SDK access. Initial test compilation exposed an ambiguous
test-only `Transform` import and an integer-to-double method-group mismatch, both corrected before
execution. The first complete Core run then exposed the obsolete in-sample re-anchoring contract
described above; after replacing that contradictory contract with exact approved recurrences, the
final Core run passed. No seed, tolerance, prior, sampler, optimizer, likelihood definition, or
convergence default changed. The complete Verification project was not run.

## TR-038 — AR, MA, and ARIMA transformed generation

**Disposition and behavior.** The confirmed generation defect is corrected. Previously, AR and
MA returned their completed recursion directly on the transformed model scale, while ARIMA always
simulated `sampleSize` stationary ARMA values and ignored both `DOrder` and `TransformType`. AR
and MA now complete the entire recursion on model scale and inverse-transform the finished vector
once. ARIMA generates exactly `max(0,sampleSize-d)` transformed highest-order differences,
integrates the complete vector, and inverse-transforms once, returning exactly `sampleSize`
values.

When ARIMA has attached data, the first `min(d,sampleSize)` transformed observations are its
integration anchors; without data, the transformed anchors are zero. If `sampleSize<=d`, the
method returns only those requested observed/zero anchors after inverse transformation and draws
no innovations. Existing validation, parameter order, method signatures, and positive sample-size
requirement remain. `Transform.None` with `d=0` preserves the pre-change fixed-seed AR, MA, and
ARIMA sequences bit for bit.

**Compatibility.** No UI/App public or protected signature, XAML binding, property, enum, XML
name, or persisted meaning changed. UI/App signature baselines pass in their complete suites.
Core passes 3,219/3,219, UI 578/578, App 440/440, and API 498/498. The strict Debug solution
build with `EnforceXmlDocumentation=true` passes all ten projects with zero warnings/errors in
21.16 s. The separately documented XML-validation script remains absent.

**Fast regressions.** `TimeSeriesGenerationTransformTests` owns six contracts: logarithmic AR
inverse transformation after the complete recurrence; manual-lambda Box-Cox MA inversion;
Yeo-Johnson ARIMA(1,1,1) integration before inversion; attached versus zero transformed anchors
for `d=2`; attached/unattached behavior when `sampleSize<=d`; and exact fixed-seed
`Transform.None`/`d=0` arrays for AR, MA, and ARIMA. Expected transformed paths and inverse
formulas are evaluated independently at `1E-12` except the bit-for-bit golden arrays, which use
exact double equality.

**Independent numerical oracles.** The exact methods are
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArAndMaTransformedGeneratorsMatchIndependentOracle`
and
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaDifferencedTransformedGeneratorMatchesIndependentOracle`.
The first uses the pre-change model-scale AR(2) seed `13579` and MA(2) seed `13580` sequences and
independently applies exponential and Box-Cox (`lambda=0.5`) inverse formulas. The second uses the
pre-change ARMA(1,1) seed `13581` model-scale sequence as seven first differences, an observed
Yeo-Johnson transformed anchor of two, `lambda=0.6`, and independent integration/inversion.
Algebraic acceptance is `1E-10` absolute.

Each method generates exactly 1,000 seeded raw/model steps. AR and MA evaluate all 1,000
independent model-scale Gaussian values using zero dynamic coefficients, intercept `0.2`,
`sigma=0.6`, and seeds `52037`/`52038`. ARIMA uses `(p,d,q)=(0,1,0)`, intercept `0.2`,
`sigma=0.5`, zero transformed anchor, logarithmic transformation, and seed `52039`; its 1,000
raw steps contain 999 independently generated first differences. Mean acceptance is four Monte
Carlo standard errors. Variance acceptance is the larger of four analytical variance standard
errors or 3% relative. The 1,000-step cap supersedes the initially planned 50,000 values by
explicit user direction on 20 August 2026 and is consistent with other repository recovery
fixtures. No external artifact or R package applies; the independent formulas are embedded.
Verification source SHA-256 is
`3879366DC56194AD76011DCD0E60B2A42ED0C6FEE415026BF95DC6AE282B6DAB`.

**Execution evidence and failure history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk
3.6.4 built against the configured local Numerics project. From commit `3d79c31` plus the scoped
Package 7 diff, the final guarded commands were:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArAndMaTransformedGeneratorsMatchIndependentOracle'

& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaDifferencedTransformedGeneratorMatchesIndependentOracle'
```

With the approved 1,000-step fixtures, the methods passed 1/1 in 0.199 s and 1/1 in 0.204 s. Their
TRX directories begin `20260820-122714-...` and `20260820-122728-...`. The ARIMA method was then
made literally step-capped by changing its request from 1,001 raw outputs/1,000 differences to
1,000 raw outputs/999 differences; the guarded rerun passed 1/1 in 0.483 s under
`20260820-123708-...`. Before the 1,000-step direction, the
AR/MA method passed its 50,000-value fixture in 0.363 s, but the ARIMA method failed: a
positive-drift 50,000-step logarithmic random walk exceeded the finite double range after inverse
transformation, consecutive infinities yielded a NaN recovered difference, and the sample mean
was NaN rather than `0.2` within `0.00894427190999916`. That failed TRX remains under
`20260820-120231-...`; it was not erased or reclassified as production evidence. The correction
changed only the user-directed sample count and literal generated-step cap. Seeds, generating
parameters, formulas, tolerances, production algorithms, priors, samplers, and convergence
defaults were unchanged.
The complete Verification project was not run.

## TR-039 — ARIMAX transformed and differenced generation

**Disposition and behavior.** The confirmed scale-order defect is corrected. Previously,
`ARIMAX.GenerateRandomValues` built a transformed/differenced deterministic mean, transformed that
mean a second time, added an innovation, and immediately inverse-transformed each recursion step.
The next AR/MA step consequently combined raw-scale simulated values with model-scale means and
residuals. For `d>0`, it then cumulatively integrated those already inverse-transformed values and
seeded each integration level with the intercept.

Generation now evaluates intercept, trend, seasonality, exact-date level-covariate, AR, MA, and
innovation terms entirely on the transformed/highest-difference model scale. It generates exactly
`max(0,sampleSize-d)` model steps, integrates the completed difference vector using the first `d`
observed transformed response levels when data are attached or zero transformed anchors otherwise,
and inverse-transforms the completed `sampleSize` level vector once. Requests with
`sampleSize<=d` return only the requested observed/zero anchors after inversion and draw no
innovations. Model step `k` uses the level covariate whose exact timestamp matches raw response
index `k+d`; covariates and their lags are not differenced. Missing or duplicate required
timestamps throw an explicit `InvalidOperationException` rather than falling back to position.

The existing three-argument `GenerateRandomValues(int,int,List<TimeSeries>?)` signature, explicit
covariate override, `None`/block-bootstrap/KNN extension choices, output length, parameter order,
positive-size validation, and extension seed policy remain. With no attached response, the first
generation covariate defines the response-date calendar and every other covariate must match it by
exact date. `Transform.None` with `d=0` preserves the pre-change seed-24682 sequence bit for bit.

**Compatibility.** No Core, UI, App, or API public/protected signature, XAML binding, property,
enum, XML name, or persisted meaning changed. The complete UI and App suites include their exact
signature-baseline checks and pass. Core passes 3,226/3,226, UI 578/578, App 440/440, and API
498/498. A serial strict Debug solution build with `EnforceXmlDocumentation=true` passes all ten
projects with zero warnings/errors in 9.76 s. The documented
`scripts/validate-code-xml-docs.ps1` remains absent, so the strict build is the active XML gate.

**Fast regressions.** `TimeSeriesArimaxGenerationTests` owns seven deterministic contracts:

- `ArimaxYeoJohnsonD1Generation_UsesDateAlignedModelScaleRecurrence` pins an ARIMAX(1,1), `d=1`,
  time-varying level-covariate recurrence, observed transformed anchor, integration, and single
  Yeo-Johnson inverse at `1E-12`;
- `ArimaxLogGeneration_DeterministicComponentsRemainOnModelScale` isolates intercept, linear
  trend, Fourier seasonality, and level-covariate shifts on logarithmic scale;
- `ArimaxD1Generation_UsesObservedOrZeroAnchors` and
  `ArimaxGeneration_SampleSizeAtOrBelowD_ReturnsRequestedAnchors` cover attached/unattached and
  short-request anchor rules;
- `ArimaxGeneration_ExplicitCovariatesUseExactResponseDates` proves an explicitly supplied
  covariate gives identical values even when its ordinate order is reversed;
- `ArimaxGeneration_MissingRequiredCovariateTimestampThrows` pins missing-date rejection; and
- `ArimaxNoneD0FixedSeedGeneration_RetainsGoldenArrayBitForBit` pins the complete pre-change
  sequence exactly.

The existing `ARIMAXTests` block-bootstrap, KNN, and explicit-covariate override regressions pass in
the same complete Core run, covering the unchanged extension policy and length behavior.

**Independent numerical oracle.** The exact method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaxTransformedDifferencedGeneratorMatchesIndependentOracle`.
Its algebraic cell uses the pre-change ARIMAX(1,1) model-scale seed-24682 sequence, an observed
Yeo-Johnson transformed anchor of two, `lambda=0.6`, intercept `0.25`, level-covariate coefficient
`1.1`, `phi=0.3`, `theta=-0.2`, `sigma=0.75`, and daily level covariates
`[99,2,-1,0.5,3,-2,1.5,4]`. Model step zero matches raw index one, so the sentinel 99 is not used.
The independent oracle cumulatively integrates the seven fixed differences and applies the
closed-form Yeo-Johnson inverse. Algebraic acceptance is `1E-10` absolute.

The moment cell requests exactly 1,000 generated raw steps from logarithmic ARIMAX(0,1,0,0),
leaving 999 innovations. It uses seed `52040`, zero transformed anchor, intercept `0.02`,
alternating dated covariate values `-1/+1`, coefficient `0.05`, and `sigma=0.4`. The independent
calculation log-transforms the output, first-differences it, and removes the exact-date deterministic
term. Mean acceptance is four Monte Carlo standard errors; variance acceptance is the larger of
four analytical variance standard errors or 3% relative. No external package or artifact applies;
the independent formulas are embedded. Production, fast-test, and Verification source SHA-256
values are respectively `55D38426AE41AB795AB3A1F112D3C41BBF0D21BCFB9A6BDE1C0CFBB68990E817`,
`277309DD1A62DD6CA237B296A8B958DE34D7F016E51F25B984E26BDD24F7FF71`, and
`93DDA459D28B9EEB2D69E464E93BDA1FC84C46D26D62082ADD74B01318F28409`.

**Execution evidence and failure history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk
3.6.4 built against the configured local Numerics project. From commit `b2332c0` plus the scoped
Package 8 diff, the guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaxTransformedDifferencedGeneratorMatchesIndependentOracle'
```

The final exact method passed 1/1 in 0.444 s; its TRX is under
`TestResults/VerificationFocused/20260820-124611-...`. The first guarded attempt stopped during
compilation because `Select(Math.Log)` was ambiguous between indexed and non-indexed overloads; it
ran no test. Replacing the test-only method group with an explicit lambda resolved that compile
error without changing the oracle. The first strict solution build then encountered transient
locks on the API static-web-assets cache and UI output DLL and failed with one warning/one error;
the serial `-m:1` rerun passed with zero warnings/errors. Neither failure was erased or treated as
numerical evidence. No production signature, extension policy, seed rule, tolerance, prior,
sampler, optimizer, likelihood definition, or convergence default changed. The final independent
ARIMAX MLE/Bayesian recovery pair remains Package 10 evidence. The complete Verification project
was not run.

## Phase 5 findings

| Finding | Status | Regression evidence | Numerical/recovery evidence |
|---|---|---|---|
| TR-035 Jeffreys component type | Complete | Three Core metadata/decomposition regressions pass | Analytical four-scale oracle passes 1/1 at `1E-12` |
| TR-036 training-only transform fitting | Complete | Core holdout/state/clone plus UI XML/copy/undo and API mapping pass | R training-only profile oracle passes 1/1 at fixed cross-language tolerance; transformed recovery remains Package 10 |
| TR-037 reintegration index | Complete | ARIMA/ARIMAX `d=1`/`d=2`, transform, component-map, length, horizon, and `d=0` golden regressions pass | Hand recurrence oracle passes 1/1 at `1E-10`; predictive recovery checks remain Package 10 |
| TR-038 AR/MA/ARIMA generation | Complete | Six transform/order/anchor/length and exact legacy-seed regressions pass | Two algebraic plus 1,000-step moment methods pass 1/1; failed 50,000-step overflow history retained |
| TR-039 ARIMAX generation | Complete | Seven scale/order/date/anchor/extension and exact legacy-seed regressions pass | Algebraic plus 1,000-step moment method passes 1/1; recovery remains Package 10 |
| TR-040 invalid scale | Complete | Six Core invalid/valid parity cases pass | Gaussian/prior oracle passes 1/1 at `1E-12`/exact rejection |
| TR-041 ARIMAX alignment | Complete | Seven Core date/holdout/validation/decomposition/state-refresh regressions plus App residual-index contract pass | Independent R date-indexed likelihood oracle passes 1/1 at `1E-10`; recovery remains Package 10 |
| TR-042 AIC/BIC kernel | Closed; refresh pending | Planned deterministic routing regression | Planned data-likelihood/MAP oracle |
| TR-046 manual transform rebuild | Complete | Atomic rebuild, canonicalization, ignored `lambda2`, persistence, and invalidation regressions pass | Independent transformed likelihood oracle passes 1/1 at fixed cross-language tolerance |

The complete Verification project is not run during Phase 5. Every numerical or recovery result
will be executed as one exact fully qualified method through `scripts/run-verification-test.ps1`.

---

[Verification index](README.md) | [Finalization plan](verification-finalization-plan.md) |
[Scientific findings](../technical-reference/review-findings.md#tr-035)
