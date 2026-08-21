<!-- verification-status: phase-5-complete -->

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
| `RMC.BestFit.UI.dll` | `RMC.BestFit.UI.Tests/CoreInfrastructure/PublicApiBaseline.txt` | 853 | `05628C483CB18629DADA80085F3C3DBC37BE648C1F61D0AA24B94BB57C291BAE` | Final gate passed as part of UI 578/578 |
| `RMC-BestFit.dll` | `RMC.BestFit.App.Tests/CoreInfrastructure/PublicApiBaseline.txt` | 1,657 | `241EBBA9760D14D355C48F4D869256FD3DF832CADE37345612299CCEF9832091` | Final gate passed as part of App 444/444 |

The following deterministic compatibility contracts also pass:

- `TimeSeriesModelSerializationCompatibilityTests` restores literal legacy AR, MA, ARIMA, and
  ARIMAX XML with every established configuration attribute, ignores an unknown optional
  attribute, and re-emits the established semantic settings.
- The existing UI legacy `ARMAX` database fixture continues loading into the ARIMAX wrapper.
- The App transform selector binds two-way to `Element.ARIMAX.TransformType` and lists the four
  transform enum members; this is documented App behaviour rather than a tested contract, because
  the source-text regression that only matched App source files was removed.

Commands executed on 20 August 2026 from base commit `8709263` plus the Package 1 working tree:

```powershell
dotnet test src\RMC.BestFit.UI.Tests\RMC.BestFit.UI.Tests.csproj -c Debug `
  --filter "FullyQualifiedName~CoreInfrastructure.PublicApiCompatibilityTests|FullyQualifiedName~TimeSeriesModelSerializationCompatibilityTests" `
  --results-directory .tmp\phase5-package1-ui

dotnet test src\RMC.BestFit.App.Tests\RMC.BestFit.App.Tests.csproj -c Debug `
  --filter "FullyQualifiedName~CoreInfrastructure.PublicApiCompatibilityTests" `
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
- `ARIMAX_JeffreysScaleMetadata_IsTheReferenceForOtherModels`.

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

The App residual plot iterates the shorter differenced training count with the retained response
timestamps; this App behaviour is documented rather than pinned by a test, because the source-text
regression that only matched App source files was removed. UI/App signature and legacy/new XML
regressions are part of their complete passing suites.

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
Each pointwise value carries the Gaussian term of its model step plus the change-of-variable term
`(lambda - 1) log(y)` of the raw observation evaluated at that step, so the pointwise values sum
to the scalar likelihood and expose the per-observation Jacobian contribution.
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

**Disposition and behavior.** The original off-by-one defect is corrected, and a serious
post-correction conditioning regression introduced during Phase 5 has also been corrected.
ARIMA and ARIMAX calculate exactly `T-d+h` transformed-difference values and map model step `k`
to raw slot `k+d`. The Phase 5 implementation initially reintegrated every fitted difference from
the first transformed observation. That converted conditional one-step fitted values into a
single simulated path: training innovations accumulated from the start of the record, training
intervals widened with time, and forecasting began from a synthetic accumulated level rather than
the final observed training state.

Prediction now uses observed lower-order states throughout the training window and at the first
forecast boundary. For `d=1`, transformed fitted and forecast levels obey
`zHat[r] = zObserved[r-1] + wHat[r]` for `1 <= r < T`,
`zHat[T] = zObserved[T-1] + wHat[T]`, and
`zHat[r] = zHat[r-1] + wHat[r]` only for `r > T`. For `d>1`, the same rule is applied at every
lower difference order: observed level/difference states at raw index `r-1` condition training
and the first forecast, then generated states advance later forecast horizons. The inverse
response transform is applied once after reconstruction. For `d=0`, the original recursion, draw
order, and fixed-seed values remain bit for bit.

Every existing prediction tuple signature and component name remains unchanged. Component arrays
have raw output length `T+h`; slots `0...d-1` are zero conditioning entries, and model component
`k` is stored at raw slot `k+d`. ARIMAX prediction uses the Package 5 exact-date level-covariate
map for both observed and regularly extended response dates. The separate
`GenerateRandomValues` simulation contracts in TR-038 and TR-039 remain unchanged: generation is
a complete simulated path from observed/zero initialization anchors, whereas `Predict` is
conditional on observations through the training boundary.

**Compatibility.** No UI/App public or protected signature, XAML binding, property name, enum,
or serialization meaning changed. On 21 August 2026, the complete serial gates passed Core
3,237/3,237, UI 578/578, App 444/444, and API 498/498. The UI and App signature-baseline tests are
included in those passing suites. The strict Debug solution build with
`EnforceXmlDocumentation=true` passed all ten projects with zero warnings and zero errors in
21.06 s. The documented XML-validation script remains absent from this checkout.

**Fast regressions.** `TimeSeriesPredictionReintegrationTests` retains the linear/quadratic,
transform, component-index, horizon, output-length, and exact `d=0` fixed-seed contracts. It now
also contains deliberately irregular training paths that distinguish conditional prediction from
complete-path simulation:

- `AutoRegressivePrediction_ConditionsOnTrainingAndRecursesAfterBoundary` and
  `MovingAveragePrediction_ConditionsOnTrainingAndRecursesAfterBoundary` confirm the unchanged
  AR/MA observation-to-forecast boundary behavior;
- `ArimaD1Prediction_ConditionsOnTrainingAndForecastBoundary` and
  `ArimaD2Prediction_ConditionsOnObservedDifferenceStatesAtBoundary` distinguish observed
  training states, the first forecast anchor, and later recursive forecasts;
- `ArimaxD1Prediction_ConditionsOnTrainingAndForecastBoundary` combines the corrected boundary
  with the exact-date level-covariate map; and
- `LogArimaD1Prediction_ConditionsOnTransformedTrainingBoundary` proves conditioning occurs on
  transformed levels before the single inverse transform.

Before the production correction, the complete Core run failed exactly the four new ARIMA/ARIMAX
boundary cases while the AR and MA audit cases passed. After the correction, Core passed
3,237/3,237. In the App the blue training and red prediction intervals share raw index
`TrainingTimeSteps-1`, so the display begins prediction at the training boundary; this is
documented App behaviour rather than a tested contract, because the source-text regression that
only matched App source files was removed.

**Independent oracle.** The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle`.
The corrected hand oracle uses an irregular ARIMA(0,2,0) response `[1,4,10,999]`, `T=3`, `h=2`,
intercept two, `Transform.None`, and `seed=-1`; the holdout sentinel is unused and the expected
conditional path is `[1,4,9,18,28]`. Its logarithmic ARIMAX(0,1,0,0) case uses transformed raw
levels `[1,1.5,1.6,9]`, `T=3`, `h=2`, exact-date covariates `[999,0.1,0.1,0.1,0.1]`, coefficient
one, and `seed=-1`; the expected original-scale path is `exp([1,1.1,1.6,1.7,1.8])`. The absolute
tolerance remains `1E-10`.

The additional exact method
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionUncertaintyBeginsAtForecastBoundary`
uses exactly 1,000 fixed seeds for both ARIMA(0,1,0) and ARIMAX(0,1,0,0), unit innovation scale,
raw response `[10,14,15,20,999]`, `T=4`, and `h=3`. The analytical variance is one at every
conditional training point and at forecast horizon one, then two and three at horizons two and
three. Means and variances use the predeclared four-Monte-Carlo-standard-error bounds and the
existing three-percent variance floor. The verification source SHA-256 is
`073A416A9617A9A274409E74D14554CBED27A6BCDA0C222BDF1B49888955B2CC`.

**Execution evidence and history.** The 20 August history remains material. Commit `3d79c31`
corrected the difference-vector length but incorrectly reconstructed the prediction as a complete
path from the first anchor. The perfect linear, quadratic, and exponential fixtures could not
distinguish that path from conditional fitted values. When recovery later exposed the mismatch,
commit `1c0cecd` changed the independent R fixture and C# assertion to the same incorrect
complete-path result instead of correcting production. That oracle change was unauthorized and
has been removed; the failed recovery evidence is retained below in the recovery history.

On 21 August 2026, .NET SDK 10.0.303 and MSTest.Sdk 3.6.4 built against the configured local
Numerics project from commit `65045e0` plus the scoped prediction correction. The guarded commands
were:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionReintegrationMatchesHandRecurrenceOracle'
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.ArimaAndArimaxPredictionUncertaintyBeginsAtForecastBoundary'
```

The hand method passed 1/1 in 0.436 s under `20260821-090812-...`; its TRX SHA-256 is
`AC14DB6C7747FC475E0B0F0EC2444FD54F324185DAAEAEEFBD146E673FA02025`.
The 1,000-realization method passed 1/1 in 0.230 s under `20260821-090212-...`; its TRX SHA-256 is
`22004CE9CC70AD9F5EF63F0CA0834CA15D30F62F55AE27330BED0F9CACE14E9A`.
The first guarded attempt could not read the installed NuGet configuration inside the filesystem
sandbox and ran no test; the identical command passed with approved access. No seed, tolerance,
prior, sampler, MCMC setting, optimizer, likelihood definition, or convergence default changed.
The complete Verification project was not run.

### Transformed-scale forecast audit after TR-037 closure

**Disposition.** A 21 August 2026 visual follow-up questioned whether transformed forecasts were
feeding original-scale observations into prior AR/MA steps. The audit found no production scale
mixing and made no model, analysis, UI, App, API, serialization, likelihood, transform, sampler,
seed, or tolerance change. AR and MA read transformed training lags/residuals and inverse-transform
their completed model paths once. ARIMA and ARIMAX read transformed differences, reconstruct
transformed levels through the boundary rules above, and inverse-transform the completed level
path once. The additive regression and verification coverage below now makes that scale contract
explicit instead of relying on code inspection or a visually plausible plot.

**Airline Passengers diagnostic.** The working example that produced the questioned plot was an
ARIMAX(1,1,1) with no covariates, Yeo-Johnson exponent
`0.0412155127218334`, `T=115`, `h=29`, intercept `0.017526237146370967`,
AR coefficient `-0.22761321776070637`, MA coefficient `0.35925490065184296`, and innovation scale
`0.12444102029549843`. The final raw training observation `491` transforms independently to
`7.06221400682384`; its final observed transformed difference is `0.1556208364630951`. The
independent conditional recurrence gives first-forecast difference `0.02702139291183551`, first
forecast transformed level `7.089235399735676`, and first raw forecast `501.40125132635757`. At
horizon 29 it gives transformed level `7.5782095317801845` and raw deterministic forecast
`730.3461558653945`, exactly matching the stored curve.

The ARMA impulse-response oracle gives transformed-level standard deviation
`0.7401022009264098` at horizon 29. The monotone Yeo-Johnson inverse maps the fixed-parameter 90%
transformed interval to approximately `[283.025305878621, 1816.5032375758447]` on the raw scale.
Posterior parameter uncertainty widens the stored result to approximately
`[275.367421455945, 1975.644338354631]`. This asymmetry is the expected combination of integrated
innovation variance and nonlinear inverse transformation; it is not produced by a raw-scale lag.
The committed reference example before the interactive transform change used `Transform.None`
and ended at deterministic forecast `553.2532812569322` with interval
`[256.0906398108524, 859.3306824489538]`. Those plots represent different fitted models and their
raw-scale interval widths are not directly comparable. The modified example database remains
unstaged and was not altered by this audit.

**Regression and independent verification.** Core regression
`TimeSeriesPredictionReintegrationTests.TransformedPredictions_UseOnlyModelScaleLagAndResidualStates`
uses raw observations equal to exponentials of deliberately small model-scale values. Its AR(1),
MA(1), ARIMA(1,1,1), and ARIMAX(1,1,1) hand recurrences fail immediately if any original-scale lag
or residual enters prediction. The exact Verification method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.TransformedArimaAndArimaxForecastsMatchModelScaleOracle`.
It independently implements the Yeo-Johnson transform/inverse, conditional ARMA(1,1) recurrence,
first-difference reintegration, and accumulated ARMA impulse-response variance. The fixture uses
transformed levels `[6,6.15,6.11,6.2,6.18,8]`, `lambda=0.04`, `T=5`, `h=5`, intercept `0.02`,
`phi=-0.2`, `theta=0.35`, and `sigma=0.12`; the final value is an unused holdout sentinel. Exact
deterministic paths use `1E-10` absolute tolerance. ARIMA and ARIMAX each use exactly 1,000 fixed
seeds; transformed final-horizon mean and variance use four Monte Carlo standard errors with the
established three-percent variance floor. Verification source SHA-256 is
`D2B8CE7E71EF16A9AC110CD40FB9676BCB20B4C10F82E3C83A5B5CF6253CA887`.

**Execution evidence and failure history.** The exact guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.TransformedArimaAndArimaxForecastsMatchModelScaleOracle'
```

The first executed oracle under `20260821-102617-...` failed 0/1 in 0.047 s because the newly
written independent oracle repeated the earlier boundary defect: it used the final fitted
transformed level instead of the final observed transformed level at horizon one. Production
returned the correct `6.19033725`; the erroneous oracle expected `6.24080225`. Only the oracle's
boundary predicate changed from `< T` to `<= T`; production and all acceptance rules remained
unchanged. The failed TRX SHA-256 is
`E6B6CE9D05218AC9CB37AC88A574AC5EC61B8279F979C3AA25B8070B8AD14BAB`.
The corrected method passed 1/1 in 0.192 s under `20260821-102650-...`; its TRX SHA-256 is
`639A15CDC7C52E5885116E3F69BD9ECEC355F2C80C6D7245C5B64168FE34BEE8`.
No Bayesian analysis was invoked by this numerical method, so no MCMC setting was set or changed.
The post-audit strict Debug solution build with `EnforceXmlDocumentation=true` passed all ten
projects with zero warnings and zero errors in 19.50 s. Serial fast gates passed Core
3,238/3,238 in 10.322 s, UI 578/578 in 1 min 29.859 s, App 444/444 in 3.862 s, and API 498/498
in 2.041 s. The UI/App signature baselines and model API baseline are included in those suites and
remain exact; no production signature changed. `git diff --check` passed. The documented
`scripts/validate-code-xml-docs.ps1` remains absent, so the strict build was the XML-documentation
fallback. The complete Verification project was not run.

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

## TR-042 — information criteria use data likelihood at MAP

**Disposition and behavior.** The Phase 2 correction remains closed and is refreshed here as a
Phase 5 regression contract. AR, MA, ARIMA, ARIMAX, and rating-curve result builders evaluate
`DataLogLikelihood` exactly once at the stored MAP parameter vector and pass that data-only value
to AIC/BIC. Prior density is excluded. With constant priors, the analytical MAP and constrained
MLE parameter vectors coincide; nonconstant priors may move MAP but are never added to the
reported criteria. This package changes no production behavior.

**Compatibility and fast regression.** No Core, UI, App, or API public/protected signature, XAML
binding, property, enum, XML name, or persisted meaning changed. The deterministic
`AnalysisInformationCriteriaRoutingTests.TimeSeriesCriteria_UseOneDataLikelihoodCallAtMap` test
uses an injected one-row posterior and a counting order-zero AR model. It proves one data-
likelihood call at the stored MAP, zero posterior/prior calls, and hand AIC/BIC formulas. The final
serial gates pass Core 3,227/3,227, UI 578/578, App 440/440, and API 498/498; the UI and App runs
include their exact signature-baseline checks. The strict serial Debug solution build with
`EnforceXmlDocumentation=true` passes all ten projects with zero warnings/errors in 17.23 s. The
documented XML validation script remains absent, so this strict build is the active XML gate.

**Independent numerical oracle.** The exact method is
`RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.InformationCriteriaUseDataLikelihoodAtMapAndExcludePrior`.
It injects a single stored MAP row into each concrete analysis and independently computes

$$
\mathrm{AIC}=-2\ell_{\mathrm{data}}(\hat\theta_{MAP})+2k,\qquad
\mathrm{BIC}=-2\ell_{\mathrm{data}}(\hat\theta_{MAP})+k\log n.
$$

The AR, MA, ARIMA, and ARIMAX fixtures use 40 daily responses with order zero and MAP
`[10,1.5]`; their default raw training boundaries are retained. The rating-curve fixture uses 20
exact stage/discharge pairs and MAP `[0.5,1.0,1.5,0.05]`. Active Jeffreys scale priors make each
posterior kernel numerically distinguishable from its data likelihood. A separate flat-prior
order-zero Gaussian cell derives mean and maximum-likelihood scale from the 32-point training
prefix, proves the same vector is a local optimum for data and posterior objectives, and runs the
production MLE and MAP estimators from the model defaults, accepting the analytical optimum and
MAP/MLE parity at `1E-3`. The four time-series criterion cells compare the production data
log-likelihood with an independent iid Gaussian evaluation of the training window at `1E-10`
before forming the criteria; the rating-curve cell is a routing check on the production
likelihood. Criterion acceptance is `1E-10` absolute. No sampler, simulation, external package, or
source artifact is invoked; the largest fixture has 40 time steps and the injected posterior has
one row, both below the Phase 5 cap of 1,000.

The fast-test and Verification source SHA-256 values are respectively
`BD26248DFA8012737571E7EA857675642EF01A0917CFE65BF10237A9E83B43F1` and
`CE0FC65CA76125EF4DA73E7A5A460ACD352D4F059C785C13BC700CB54FF29F14`. The passing TRX SHA-256
is `629C76B20DF807626C718C59E5152C59E17D72A8CDD4FA700F8EFB3604F96E08`.

**Execution evidence and failure history.** On 20 August 2026, .NET SDK 10.0.303 and MSTest.Sdk
3.6.4 built against the configured local Numerics project. From commit `a4db99a` plus the scoped
Package 9 test/report diff, the guarded command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesVerificationTests.InformationCriteriaUseDataLikelihoodAtMapAndExcludePrior'
```

The final exact method passed 1/1 in 0.329 s; its TRX is under
`TestResults/VerificationFocused/20260820-130334-...`. Two initial compile attempts ran no test:
the first exposed test-only namespace/type ambiguities and the second used a nonexistent MAP enum
member; aliases and the established `PosteriorMode` member corrected those fixture errors. The
next runs retained deliberately invalid fixtures and failed rather than masking them: six ARIMAX
observations violated its 30-observation validation minimum (`20260820-130014-...`), six rating
pairs violated its 10-pair minimum (`20260820-130113-...`), and the analytical Gaussian oracle
used the 40-value response rather than the model's 32-value training prefix
(`20260820-130139-...`). Correcting only those fixture boundaries produced the final pass; no
tolerance, formula, likelihood, prior, optimizer, sampler, seed, or convergence default changed.
An initial sandboxed final rerun could not read the user NuGet configuration and ran no test; the
same exact guarded command then passed with the configured environment. The first UI gate retry
collided with an already-running test logger; after that process completed, the isolated UI suite
passed 578/578. The complete Verification project was not run.

The default-settings audit in commit `ccd5842` removed the test-only point-estimator assignment from
this oracle. An exact guarded rerun from that commit passed 1/1 in 0.611 s under
`20260820-140838-...`; the TRX SHA-256 is
`A6C65950ACB89E016E90A33C4226FD2027410E30E8CAC1A97F1B15080C7F5E5A`. The oracle now asserts
the default posterior-mean selection and still proves that AIC/BIC use the stored MAP data
likelihood. No `BayesianAnalysis` setting is assigned.

## Integrated recovery matrix — complete

**Fixture and execution contract.** Package 10 adds the committed R artifact
`verification/data/time-series/phase5-recovery-fixtures.json` and generator
`verification/r/time-series/generate_phase5_recovery_fixtures.R`. The artifact implements AR(1),
MA(1), logarithmic ARIMA(1,1,1), and differenced ARIMAX(1,1,0) with a dated level covariate
without calling Numerics or BestFit. Every fixture contains exactly 1,000 raw observations retained
after 110 discarded stationary ARMA recursion steps. AR and MA therefore generate 1,110 model
steps; differenced ARIMA and ARIMAX generate 1,109 model-scale differences before the approved raw
anchor supplies the first retained level. AR and MA retain the established seed `12345`; ARIMA and
ARIMAX retain approved seeds `51037` and `51038`. The burn-in follows the repository convention
`max(p,q) * 10 + 100` and is conservative relative to R `stats::arima.sim`'s root-dependent default.
The burn-in generator was committed as `c3b924f`; the regenerated artifact was committed as
`5493304` before any corrected C# result. Later oracle-only prediction-path corrections retained the
same samples and model recurrences. The final artifact and generator SHA-256 values are respectively
`D1A1C4F1B519BF8FCE438164FB0E3DCC8669F6704AC7FBEE3375D5188661E228` and
`2361F7A938F3FB330EAAF0DF4D7EAD23A42B4D3B80A0447E5893B6A07B5427C5`.

The 1,000 limit applies to retained fixture observations and the separate generator-moment methods;
it does not cap MCMC. The four Bayesian cells use the resolved production `BayesianAnalysis`
defaults without assigning any sampler or analysis setting. For the three-parameter AR, MA, and
ARIMA cells these are DEMCzs, six chains, thinning 30, 3,500 iterations, 1,750 warmup iterations,
300 initialization iterations, 10,000 retained rows, seed 12345, posterior mean, and the standard
dimension-scaled jump, jump-threshold, snooker, and noise defaults. The tests assert these values
before sampling and again after results are returned. The production 90% reporting interval remains
unchanged; the predeclared central 95% recovery interval is calculated independently from the 10,000
retained draws. After the prediction-oracle correction, the recovery-source SHA-256 is
`DD003691DEFDD3BD19FFCAB0C6E00C1B2F1D4404B478DDB342C954E027AE3125`.

**Predeclared matrix.** The exact methods and current dispositions are:

| Cell | Fully qualified method | Disposition |
|---|---|---|
| AR MLE | `RMC.BestFit.Verification.TimeSeriesAnalysis.AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1` | Passed 1/1 after approved 110-step burn-in correction |
| AR Bayesian | `RMC.BestFit.Verification.TimeSeriesAnalysis.ARAnalysisTests.Test_EstimateParameters_AR1` | Passed 1/1 with unchanged production DEMCzs defaults |
| MA MLE | `RMC.BestFit.Verification.TimeSeriesAnalysis.MovingAverageMLERecoveryTests.Test_EstimateParameters_MA1` | Passed 1/1 at the unchanged 5% gate |
| MA Bayesian | `RMC.BestFit.Verification.TimeSeriesAnalysis.MAAnalysisTests.Test_EstimateParameters_MA1` | Passed 1/1 with unchanged production DEMCzs defaults |
| ARIMA MLE | `RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.MleArima111LogD1RecoversGeneratingParameters` | Passed 1/1 against the direct conditional-likelihood optimum, profiles, same-point likelihood, and boundary-conditioned prediction oracle |
| ARIMA Bayesian | `RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.BayesianArima111LogD1RecoversGeneratingParameters` | Passed 1/1 with unchanged production DEMCzs defaults; sampled MAP agrees with the independent default-prior posterior MAP |
| ARIMAX MLE | `RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.MleArimax10D1LevelCovariateRecoversGeneratingParameters` | Passed 1/1 against the date-indexed conditional optimum using the unchanged production Differential Evolution default |
| ARIMAX Bayesian | `RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.BayesianArimax10D1LevelCovariateRecoversGeneratingParameters` | Passed 1/1 with unchanged production DEMCzs defaults; sampled MAP agrees with the independent default-prior posterior MAP |

**Recovery checkpoint.** The exact guarded AR MLE command was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.AutoRegressiveMLERecoveryTests.Test_EstimateParameters_AR1'
```

Before the burn-in correction, the corrected-seed run failed 0/1 in 0.619 s under
`20260820-132714-...`. The estimated process
mean was `10.569486830922324`, outside the unchanged generating value and 5% gate `10 ± 0.5`.
The TRX SHA-256 is
`0C8794B6B68C2969027D2181739BFBFD800D148C1EED78C752D292E65662CBF3`. This is insufficient
evidence of a production algorithm defect: under the new 1,000-observation ceiling, the retained
5% large-sample gate can reject ordinary finite-sample variation. It is nevertheless a failed
predeclared recovery criterion, so execution stopped exactly as approved. No alternate seed,
tolerance, optimizer, likelihood, prior, sampler, formula, or convergence default was tried. After
source review established that the independent fixture omitted standard stationary initialization,
Haden Smith approved 110 discarded steps while retaining 1,000 observations and the unchanged gate.
The generator self-checks reproduce every retained AR/MA/ARIMA/ARIMAX recurrence at `1E-12`.
Commit `a9e9bbd` additionally makes every C# recovery cell assert the burn-in, retained-sample, and
model-step budgets before estimation.

The first sandboxed corrected run could not read the user NuGet configuration, failed during SDK
resolution, and ran no test. The same exact guarded command then passed 1/1 in 0.499 s from commit
`a9e9bbd` under `20260820-134940-...`. Its TRX SHA-256 is
`AEDDD87466F9FB3757C1CBEB0E1D08F4940095175AD2DD5224CEEBA158146B81`. This result resolves the
AR MLE stop condition without changing the retained sample size, seed, 5% gate, production code,
optimizer, likelihood, prior, or numerical defaults.

The next exact guarded command selected only
`RMC.BestFit.Verification.TimeSeriesAnalysis.ARAnalysisTests.Test_EstimateParameters_AR1`. Its
pre-sampling assertions confirmed the unchanged production DEMCzs defaults. The test instance then
used six chains, thinning one, 833 configured iterations, 416 configured warmup iterations, 300
unchanged initialization iterations, and 1,000 retained rows; the configured outer sampler budget
was exactly `833 + ceil(1000/6) = 1,000` steps per chain. The test failed 0/1 after 2.256 s under
`20260820-135105-...` because the intercept R-hat was `1.1478771`, above the predeclared strict
threshold `< 1.1`. The intercept truth-in-95%-interval and MAP-within-25% assertions precede this
gate and passed. The intercept ESS and every later parameter assertion were not reached. The TRX
SHA-256 is `326063CA79A0E538DE42AC222A52EC5EE3142C0945F760788CF705541CFC7136`.
No additional seed, chain, iteration, prior, sampler, threshold, or production-default configuration
was attempted. The remaining six recovery cells stopped unrun, and the complete Verification
project was not run.

That result is retained as failure history but is not evidence for the approved recovery contract:
the test had replaced the production iteration, warmup, thinning, output-length, and interval
settings after asserting them. Commit `ccd5842` removed the cap and every time-series Verification
assignment to a `BayesianAnalysis` setting. A source audit found no remaining setting assignment,
and Core, UI, App, and API gates passed 3,230/3,230, 578/578, 443/443, and 498/498 respectively.

The corrected recovery methods were then run one at a time with .NET SDK 10.0.303 and MSTest.Sdk
3.6.4. Each command had the form
`scripts/run-verification-test.ps1 -Test '<fully-qualified-method>'`; the fully qualified methods
are listed in the matrix above. Actual evidence from commit `ccd5842` is:

| Cell | Duration | Result directory | TRX SHA-256 |
|---|---:|---|---|
| AR Bayesian | 24.611 s | `20260820-140856-...` | `7E9032EC34973A06D7162BE379A3C77E9D19260037240F8D8A2B5965509287FC` |
| MA MLE | 0.198 s | `20260820-140942-...` | `FE353686EA1307BEED322A3590E1557C14AEA9D094B1B905D8956C9435AA113F` |
| MA Bayesian | 24.097 s | `20260820-140953-...` | `94328FA21C63782206BF22565D4D4DF8355CC7BFF2E6B21F8B52AD3C7EB71C59` |
| ARIMA MLE | 0.061 s | `20260820-141029-...` | `86ACB856588C87401B804E07664921CB4D6D9D683574E52362522DE28258E236` |
| ARIMA MLE, independent-oracle contract | 0.066 s | `20260820-155040-...` | `0172E35779491F201B3CC4473CE0FB17B4F04E079FF6D39F7C48828EF9229AEF` |

The required `scripts/validate-code-xml-docs.ps1` file is absent from this checkout. The first strict
fallback solution build could not replace the executable held by the running BestFit desktop
process and therefore failed only at the copy step. The application was not stopped. Repeating the
same Debug build with `EnforceXmlDocumentation=true` in an isolated artifacts directory succeeded
with zero warnings and zero errors.

AR Bayesian and MA Bayesian each returned exactly the default 10,000 retained rows, retained every
asserted default before and after sampling, contained the generating truth in the independently
calculated central 95% interval, met the 25% MAP, `< 1.1` R-hat, and `> 100` ESS gates, produced
finite data/prior likelihoods, and passed their one-step generating recurrence. ARIMA MLE stopped at
its MA coefficient: `0.3230227122104127` versus generating `0.25`, an absolute error
`0.0730227122104127` greater than the fixed 15% allowance `0.0375`. No alternate seed, fixture,
tolerance, optimizer, likelihood, prior, sampler, or algorithm was attempted. ARIMA Bayesian and
both ARIMAX cells remain unrun. The complete Verification project was not run.

**Approved ARIMA MLE verification correction, frozen before reevaluation.** Independent diagnosis
reproduced the C# conditional-likelihood optimum rather than identifying a production defect. The
committed R 4.4.3 generator directly evaluates the exact fixture recurrence with the first
difference conditioned and zero initial innovation, profiles nuisance parameters, and includes the
logarithmic-transform Jacobian over raw indices `2...999`. It obtains
`phi=0.400116305060459`, `theta=0.323088117478691`, `sigma=0.0392046536277865`, and data log
likelihood `-2889.20015515601`. Its independently profiled 95% intervals are
`[0.311503785384669, 0.484293437691628]`, `[0.232573294769585, 0.40844941072935]`, and
`[0.0375456561516624, 0.0409895227567874]`; each contains its generating value. The theta standard
error is `0.0448675484525076`, so the rejected fixed `±0.0375` gate has only approximately
`59.67%` asymptotic coverage for this fixture. R `stats::arima` independently gives theta
`0.323065157866208` by CSS and `0.32246945930031` by exact ML.

Before another C# result is inspected, the replacement acceptance contract is fixed as: absolute
C#-to-R differences no greater than `1E-3` for phi/theta, `1E-5` for sigma, and `1E-5` for total
data log likelihood; all three generating values inside the independent 95% profile-likelihood
intervals; deterministic recurrence tolerance `1E-12`; finite data and prior likelihoods; and the
unchanged one-step prediction check. The fixture, seed, 110-step burn-in, 1,000 retained raw
observations, optimizer, likelihood, production code, and all Bayesian settings remain unchanged.
The artifact `phase5-arima-mle-recovery-oracle.json` has SHA-256
`73137D84A69FF69B681BF4FB466DA1C1692A0718C70BE37206134D87465F865F`; its generator has SHA-256
`61E7C788CA6FBADAD9274AD1338B6F38A6BDCC167A347073B8DBEC8A719B90C2`. This acceptance contract
will be committed before the C# verification method is changed or rerun.

The oracle package was committed as `e0a380e`; the consuming fast regression and exact-method
contract were committed as `55687f2` before evaluation. The strict isolated Debug/XML solution
build then passed with zero warnings and zero errors. Core, UI, App, and API gates passed
3,231/3,231, 578/578, 443/443, and 498/498. The first UI process completed successfully after the
command yielded; a concurrent retry ran no tests because MSTest's fixed internal log was still held
by that successful process. No production, UI, App, API, optimizer, sampler, seed, or default changed.

The only Verification command run for this package was:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.MleArima111LogD1RecoversGeneratingParameters'
```

From commit `55687f2`, it failed 0/1 after 0.066 s under `20260820-155040-...`; the TRX SHA-256 is
`0172E35779491F201B3CC4473CE0FB17B4F04E079FF6D39F7C48828EF9229AEF`. Every parameter comparison
against the independent R optimum passed, as did all metadata and fixture-contract assertions that
precede it. The first failing assertion compared the C# estimate's data log likelihood
`-2889.2001666477972` with the R optimum likelihood `-2889.20015515601`. Their absolute difference
is `1.1491787E-5`, which exceeds the frozen `1E-5` gate by `1.491787E-6`. Execution stopped. The
method was not rerun at that checkpoint, the tolerance was not changed, and ARIMA Bayesian plus both
ARIMAX cells were still unrun. The full Verification project was not run.

**Final recovery correction and evidence.** The four final exact guarded commands were:

```powershell
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.MleArima111LogD1RecoversGeneratingParameters'
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.BayesianArima111LogD1RecoversGeneratingParameters'
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.MleArimax10D1LevelCovariateRecoversGeneratingParameters'
& .\scripts\run-verification-test.ps1 -Test `
  'RMC.BestFit.Verification.TimeSeriesAnalysis.Phase5TimeSeriesRecoveryTests.BayesianArimax10D1LevelCovariateRecoversGeneratingParameters'
```

Commit `fc4bc30` corrected the test to compare R and C# likelihoods at the same parameter vector.
That exact rerun then exposed the prediction defect: the independent R fixture correctly expected
the forecast level to start from the last observed training level, while C# returned a complete
fitted path accumulated from the first transformed anchor. The failed run under
`20260820-160443-...` took 0.072 s and has TRX SHA-256
`2CC71E1401C064797C364F33322F3B9DC369A59D78D759CFAB78743E89209871`.
The response at that checkpoint was wrong: commit `1c0cecd` changed the R fixture and C# assertion
to accept the broken C# path. That change did not repair production and is not valid verification
evidence. On 21 August 2026, the incorrect `prediction_complete_path_zero_innovation` field was
removed, the original independent `next_raw_zero_innovation` boundary oracle was restored, and
production prediction was corrected. This failure history is retained explicitly rather than
rewritten as a fixture defect.

The first unchanged-default ARIMA Bayesian run correctly placed every generating value inside its
central 95% interval, but the test then compared sampled posterior MAP theta
`0.32337201602419413` with generating theta `0.25`. That is not an apples-to-apples MAP oracle. The
failed 28.295 s run under `20260820-161059-...` has TRX SHA-256
`EFABD3CF2D4395813E47A22B407B04BE339937B8335A5B70E5B5B7E1F2ED894E`. Commit `e481d08` added the
independent posterior MAP under the exact default priors and changed no MCMC setting. The test uses
`analysis.Results.MAP.Values`, not the configured posterior-mean reporting estimator. It still
requires truth inside each central 95% interval, R-hat below `1.1`, ESS above `100`, unchanged
resolved DEMCzs defaults, finite likelihoods, and the prediction recurrence. The final method passed
1/1 in 30.642 s under `20260820-161545-...`; its TRX SHA-256 is
`E0D8F100B38262DC906D2B80A7840687A36A104F9A7F71AC8D24B2BBAA48C3A5`.

For ARIMAX, the first forced-Nelder-Mead recovery run failed at intercept `0.01` versus generating
`0.25` under `20260820-161641-...`; its TRX SHA-256 is
`298EF51119473AEFC7CED11436D1906AC85276599DB2BBE4C5F8F3F9040078FA`. The independent R oracle
then proved exact C#/R recurrence and likelihood parity at common truth and optimum vectors. A second
forced-Nelder-Mead run reproduced the boundary result
`[0.01,1.5413760616540739,0.45915689732647091,0.52245725824654321]` and failed under
`20260820-162235-...`; its TRX SHA-256 is
`09419D0287C265030D018BD3EBAE2240CE672BC5BEB7C1BAF7E0F803302DA82F`. R's profiled and full
four-parameter Nelder-Mead fits from the same default start both reached the interior conditional
optimum `[0.236896678521822,1.54037088921619,0.364772089115024,0.505264570723146]`. This isolated
the discrepancy to the test's forced bounded-Nelder-Mead choice, not ARIMAX likelihood or alignment.
The final recovery therefore uses `MaximumLikelihood` exactly as production does: unchanged,
deterministic Differential Evolution with its existing defaults. No optimizer implementation or
default changed. It passed 1/1 in 0.516 s under `20260820-162605-...`; its TRX SHA-256 is
`87E30EAF17970928FCE9B6D7C0C4214DFF37FF736EA91A36C3B316DB06E20939`.

ARIMAX Bayesian first passed its truth-centered contract in 39.796 s under
`20260820-162628-...` (TRX SHA-256
`0928E5EFD72BBBDD1E090307A67C87D64370B8230756CAC43F1D9231A6F4D864`). It was then strengthened,
without changing any sampler setting, to compare sampled MAP with the independent default-prior
posterior MAP and to check the data, prior, and posterior values at that common vector. The final run
passed 1/1 in 37.241 s under `20260820-162822-...`; its TRX SHA-256 is
`8838CD0559E092A828AD50C386CDDF16AEDEF66A5BC272DA0B310A9C2D7183D2`.

The prediction-corrected four-cell rerun on 21 August 2026 used the exact guarded commands above.
ARIMA MLE passed in 0.228 s (TRX SHA-256
`5F998E5E97ED9414C51FB223CBD001DBFEAF96AB5E1BB3F984E3380D908600BC`), ARIMA Bayesian passed
with unchanged production DEMCzs defaults in 31.124 s
(`00BBC3B51BEF795765CA62B37692BAB250B2C81BF6CAC82425214A1DC036F837`), ARIMAX MLE passed in
0.586 s (`04890DACF63F317220709A1F0D42FD4A825610D1340E3B432A4DCCEF994F8D0D`), and ARIMAX Bayesian
passed with unchanged production DEMCzs defaults in 33.615 s
(`37BB4DC19D139A43DD57075EA37A911AAF9D061850C2D8FEACA11EC074384560`).

The committed ARIMA and ARIMAX oracles use R 4.4.3, jsonlite 2.0.0, and digest 0.6.39. The ARIMA
generator and artifact SHA-256 values are respectively
`55DFBE0DA4641EBE83682845037FBDEF213119593DE8D7412A3A98315C1AF3D6` and
`D4CA6E860A3A39A3F08C38973B9543B198050C9AA3EBDC6A61D235EDBD9BB43C`. The ARIMAX generator and
artifact SHA-256 values are respectively
`4F94E214EF00B9A6B5E86DFBEE5C74144E14AF1AB6A44E5274A643606BC3D973` and
`79E1034654393CD93BF7D29575BA8FDED565C6591C5A42BE4133EB20084EDD59`. The recovery fixture generator
and artifact hashes are respectively
`DD003691DEFDD3BD19FFCAB0C6E00C1B2F1D4404B478DDB342C954E027AE3125` and
`EFC4C3EEAEF40AB162671F2CCCF34E19CFE19650F46CC8711EDD956C2F39ED9C`.

**Failure history.** The initial R generation attempt could not read repository renv junctions in
the sandbox and wrote no artifact; the same script ran in the configured environment. The first C#
compile exposed three test-only type/index errors and ran no test; explicit established types fixed
them. The first AR MLE run used mistakenly assigned AR/MA seeds `51035/51036` and failed the AR
coefficient gate (`0.5458503824113965` versus `0.6 ± 0.03`) under `20260820-132537-...`. That run
did not represent the approved existing-seed contract. The generator, artifact, and manifest were
then committed with seed `12345` before the corrected-seed reevaluation. The subsequent missing
burn-in failure and its approved correction remain recorded rather than replaced. The capped AR
Bayesian failure is retained as superseded test-configuration history. The final operative evidence
is eight recovery passes. All failed and superseded runs above remain part of the audit trail and
were not replaced silently. The later complete-path oracle mistake and its correction are also
retained above. The operative evidence is the eight historical recovery passes plus the four
prediction-affected cells rerun against the restored boundary oracle.

## Final repository gates

The final gates were executed serially on 20 August 2026 from Phase 5 code commit `b0dff5c` while
preserving unrelated working-tree changes. The separately named
`scripts/validate-code-xml-docs.ps1` script is absent from this checkout. Its strict build fallback
was therefore run as:

```powershell
dotnet build RMC.BestFit.sln -c Debug -p:EnforceXmlDocumentation=true `
  --artifacts-path TestResults/Phase5Final/Build
```

The build passed in 18.52 s with zero warnings and zero errors. A namespace scan found no exact
`RMC.BestFit` declaration, deleted singular `RMC.BestFit.Model` namespace, or broad
`using RMC.BestFit;` import. The fast projects were then run serially with Microsoft Testing
Platform `--report-trx` output:

| Gate | Result | Duration | TRX SHA-256 |
|---|---:|---:|---|
| Core | 3,231/3,231 | 10.682 s | `0285AF8889F9ACCDA5CE8E9A883F3FDB3FDF59D12564E577D2E789EAC2874496` |
| UI | 578/578 | 35.051 s | `3D4CDEB4820639A5EDC9C42257ED966C04A730B75576408D50FE1A283B694E5D` |
| App | 443/443 | 2.803 s | `3135C96C298C1668BE7FFE9D294FB89DED165E6E616717E75C422C0B798A2365` |
| API | 498/498 | 1.387 s | `00FBEA971EED3FB73DCBA242B6EC0A2A58A6883169874A0596A89CF37E5087A2` |

The prediction correction gates were rerun serially on 21 August 2026 from commit `65045e0` plus
the scoped correction. The strict ten-project Debug/XML build passed in 21.06 s with zero warnings
and zero errors. The first final Core attempt passed 3,236 and failed one unrelated asynchronous
univariate result-refresh test with a `NullReferenceException`; no code or setting was changed.
The unchanged complete rerun passed 3,237/3,237 in 7.491 s. UI passed 578/578 in 34.036 s, App
passed 444/444 in 3.588 s, and API passed 498/498 in 1.964 s. These reruns include the UI/App
signature baselines, the App plot-split regression, and the Core public-API baseline. No full
Verification run was performed.

The UI and App signature baselines remain byte-for-byte unchanged at SHA-256
`05628C483CB18629DADA80085F3C3DBC37BE648C1F61D0AA24B94BB57C291BAE` and
`241EBBA9760D14D355C48F4D869256FD3DF832CADE37345612299CCEF9832091`. The final Core baseline is
SHA-256 `90A23BC7A863A6A4D3D10E6EBD8DF5FFE80A501E82369611DF6D888050A3A78B`; commit `ac661e9` changed
its pre-Phase-5 content by exactly four additive read-only `TransformLambda` properties, one each on
AR, MA, ARIMA, and ARIMAX. No other Core signature changed. The passing UI suite includes legacy,
new transform-state, and unknown-optional-attribute XML contracts. The full Verification project was
not run; every Phase 5 numerical and recovery result was an exact guarded one-method invocation.

## Phase 5 findings

| Finding | Status | Regression evidence | Numerical/recovery evidence |
|---|---|---|---|
| TR-035 Jeffreys component type | Complete | Three Core metadata/decomposition regressions pass | Analytical four-scale oracle passes 1/1 at `1E-12` |
| TR-036 training-only transform fitting | Complete | Core holdout/state/clone plus UI XML/copy/undo and API mapping pass | R training-only profile oracle passes 1/1; transformed ARIMA recovery passes |
| TR-037 reintegration index and prediction boundary | Complete after corrective audit | AR/MA boundary audits plus irregular ARIMA/ARIMAX `d=1`/`d=2`, explicit transformed lag/residual separation, component-map, length, horizon, holdout-sentinel, and `d=0` golden regressions pass | Corrected hand recurrence, boundary variance, transformed ARMA recurrence/variance methods using 1,000 realizations, and four prediction-affected MLE/Bayesian recoveries pass |
| TR-038 AR/MA/ARIMA generation | Complete | Six transform/order/anchor/length and exact legacy-seed regressions pass | Two algebraic plus 1,000-step moment methods pass 1/1; failed 50,000-step overflow history retained |
| TR-039 ARIMAX generation | Complete | Seven scale/order/date/anchor/extension and exact legacy-seed regressions pass | Algebraic plus 1,000-step moment method and date-indexed ARIMAX MLE/Bayesian recovery pass |
| TR-040 invalid scale | Complete | Six Core invalid/valid parity cases pass | Gaussian/prior oracle passes 1/1 at `1E-12`/exact rejection |
| TR-041 ARIMAX alignment | Complete | Seven Core date/holdout/validation/decomposition/state-refresh regressions plus App residual-index contract pass | Independent R date-indexed likelihood oracle and both ARIMAX recovery cells pass |
| TR-042 AIC/BIC kernel | Closed; refresh complete | Counting data-likelihood/MAP routing regression passes in Core 3,230/3,230 | Five-analysis data-only criterion and flat-prior parity oracle passes 1/1 with default point estimator |
| TR-046 manual transform rebuild | Complete | Atomic rebuild, canonicalization, ignored `lambda2`, persistence, and invalidation regressions pass | Independent transformed likelihood oracle passes 1/1 at fixed cross-language tolerance |
| Integrated recovery | Complete | Eight exact cells implemented; fixtures assert 110-step burn-in and 1,000 retained observations; Bayesian cells assert unchanged defaults before and after sampling; corrected boundary and transformed-scale regressions pass in Core 3,238/3,238 | All eight exact recovery cells pass historically; all four prediction-affected ARIMA/ARIMAX cells pass again against the restored boundary oracle, with independent conditional MLE/posterior-MAP oracles and truth retained as a central-95% coverage criterion |

The complete Verification project was not run during Phase 5. Every numerical and recovery result
was executed as one exact fully qualified method through `scripts/run-verification-test.ps1`.

---

[Verification index](README.md) | [Finalization plan](verification-finalization-plan.md) |
[Scientific findings](../technical-reference/review-findings.md#tr-035)
