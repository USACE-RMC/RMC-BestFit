# Distribution robustness integration

This implements the approved September 8, 2026 distribution repair plan, based on BestFit `b732703` and Numerics `c0d67b9`. Work was isolated from the original checkouts and their unrelated changes. No Verification test, package publication or push is included.

## Behavior

- Aggregate and pointwise quantile priors in `UnivariateDistribution` and `PointProcessModel` use the additive Numerics `LogAbsQuantileJacobian` extension. A finite logarithmic determinant survives raw determinant overflow/underflow; exact singularity still gives negative infinity.
- Mixture EM evaluates exact, censored, interval, positive-conditional and measurement-error observations logarithmically through responsibility normalization. Centering observation logs before adding weights retains relative weights even when a common log density is extremely large in magnitude. Zero-weight components are skipped before singular densities or effective-support checks. Active-component, atom, physical-weight, parameter and serialization rules remain intact.
- Automatic initialization reports unusable samples through model validation while preserving editable parameters and priors. The PointProcess path still refreshes AMS/exposure structure and supplies its existing seasonal changepoint defaults. A later valid sample clears the initialization diagnostic. This does not pad data, invent GEV prior bounds, change optimizers or alter seeds.
- The corrected Numerics LnNormal physical-parameter cache preserves exact serialized mean/SD values through clone and round-trip operations. Existing persisted coordinates and logarithmic bases remain unchanged.

The integration requires Numerics repair commit `b8bf912c11f3dc0a770bd9e54d0ae40d2bccb319`; the existing released package does not contain the new extension. Validation explicitly selects the intended source project with `UseLocalRmcNumerics=true` and `RmcNumericsProjectPath`. No package version or release metadata was changed in this work.

Numerics documents all 15 individual families, CompetingRisks and Mixture, the new GNO/GLO/Kappa local-MLE uncertainty methods, parameter coordinates, regularity domains, independent R/Python fixtures and retained limitations in `docs/distributions/bestfit-robustness-evidence.md`. New covariance methods do not establish global MLE existence or finite-sample coverage. L-moment and product-moment covariance for those three families remain unsupported.

## Validation

All tests use small deterministic contracts; the new fast tests do not run an optimizer or MCMC. The first targeted regressions failed before their corresponding repairs. Full core runs also exposed PointProcess initialization contracts, which were repaired with additional parameter-preservation/recovery tests. A Normal initialization-envelope regression was corrected in Numerics; its seeded RWMH reference then reproduced exactly without changing any expected draw, fitness, acceptance count, seed or tolerance.

| Release gate | Final result |
|---|---|
| Core fast tests | 3,406 passed; one test worker |
| UI fast tests | 593 passed |
| App fast tests | 443 passed |
| XML documentation and namespace scan | Passed, 941 C# source files |
| Numerics Release/XML build | Zero warnings/errors, all four supported frameworks |
| Numerics complete Release tests | 2,675 passed per framework, all four frameworks; 10,700 passes |
| BestFit Verification | Not executed |

The isolated Numerics project used here is `C:/GIT/numerics/artifacts/worktrees/distribution-robustness/Numerics/Numerics.csproj`. Each BestFit test command uses `-c Release -p:EnforceXmlDocumentation=true -p:UseLocalRmcNumerics=true -p:RmcNumericsProjectPath=<that path>`. UI and App builds additionally point `HecDssRoot` at the existing `C:/GIT/hec-dss/dotnet/Hec.Dss/` dependency. These are command-line worktree references; project configuration and dependencies were not replaced.

The MSTest.Sdk 3.6.4 host uses Microsoft.Testing.Platform. Targeted arguments belong after `--`, for example `-- --filter 'FullyQualifiedName~DistributionRobustnessIntegrationTests' --report-trx`; ordinary legacy filter syntax is ignored by this host. Reported counts were checked against the output/TRX. Full gates name each of the three fast projects explicitly and do not invoke the Verification project.

The default core gate twice hit the unrelated `RunAsync_MultipleAnalyses_Parallel` stopwatch assertion under inter-test contention (369 ms and 673 ms versus its unchanged 250 ms limit). That test passed in isolation. The final full core gate adds `--settings docs/validation/distribution-robustness.runsettings` after `--`, using [one test worker](validation/distribution-robustness.runsettings). All cases execute, and concurrency within each test remains unchanged. UI/App retain normal scheduling. The opt-in file does not change project defaults or test tolerances. See [MSTest execution control](https://learn.microsoft.com/en-us/dotnet/core/testing/unit-testing-mstest-writing-tests-controlling-execution) for the configuration mechanism.

[Machine-readable evidence](validation/distribution-robustness.json) records final counts and TRX hashes. Every BestFit test output's Numerics.dll SHA-256 matches the intended Numerics build.

## Numerical limits

The existing dependence backend and GL20 measurement-error rule are retained. This work does not add arbitrary-precision multivariate tails or change the full uncertain-observation integration policy outside the repaired EM path. Generic positive conditioning can still lose a correction if two separately returned component logs have already rounded to the same enormous magnitude, for example a Normal mean of -1e100 conditioned above zero. Numerics records these limits and explicit unresolved numerical domains rather than claiming arbitrary-precision accuracy.

Changes are prepared for reviewed local commits. No push or publication is included.
