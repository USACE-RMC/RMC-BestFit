# Point-Process Verification

This report records the focused Phase 4 evidence for point-process exposure, Poisson-GPA generation, and seasonal changepoint defaults. It does not claim that the full `RMC.BestFit.Verification` project was run.

## TR-004 - Exposure and Rate Definitions

`PointProcessModel` distinguishes exact observed event count and empirical event rate from the fitted GEV-compatible threshold intensity. `Lambda` remains the empirical-rate compatibility alias. Exposure precedence is explicit `TotalYears`, serialized `DataFrame.PointProcessObservationYears`, then the exact year/index span used for manually entered POT data. The final fallback warns because leading and trailing zero-event years cannot be reconstructed from an event-only table.

Fast tests cover source-exposure preservation, explicit override precedence, inferred-span warnings, exact versus non-exact event counting, state refresh, and serialization. Exact observations count as Poisson events; nonseasonal manual records may use year/index values, while every exact observation in a seasonal model must have a valid date for process assignment. Uncertain, interval, and threshold-count rows remain annual/block-indexed magnitude-likelihood information.

For seasonal mixed data, exposure fractions weight the two point-process intensities. Each process produces an exposure-adjusted seasonal maximum, and the annual observation distribution is the independent maximum of those two maxima. It is not a weighted mixture of annual GEV CDFs. A fast regression covers uncertain, interval, and threshold routing through this annual **CompetingRisks** distribution, and a separately coded analytical verification method covers the complete mixed likelihood.

## TR-005 - Poisson-GPA Simulation and Seasonal Priors

### Production process

The fixed-size and duration-based generators use the configured empirical `Lambda` as the Poisson rate. Hosking GEV parameters are converted to Hosking GPA parameters through the Madsen relationship. Seasonal assignment uses the floored changepoint exposure weights; assigned dates fall inside the corresponding block-day support. The weights belong to the seasonal processes. Annual frequency output instead takes the maximum of their two exposure-adjusted seasonal maxima. No GEV prior, sampler setting, tolerance, or seed contract was changed.

### Automatic changepoint priors

For at least ten exact dated events, the default-prior rule rotates the existing monthly occurrence histogram to the configured block-year start, rejects effectively flat structure using the fixed Pearson cutoff 19.675, applies one circular `[1,2,1]/4` smoothing pass, selects one uniquely strongest separated peak pair, and locates a deterministic valley on each arc. Five-month flat windows centered on those valleys are intersected with `K1` in `[1,251)` and `K2` in `[200,367)`.

The procedure evaluates no point-process likelihood and is not a changepoint estimator. Insufficient, flat, unimodal, tied, undated, or incompatible histograms retain the broad supports. Continuous latent values start at valley-cell centers; likelihood, exposure weights, annual distribution conversion, and simulation use the floored integer days.

### Independent fixtures

`PointProcessSeasonalFixture` generates Poisson counts through exponential inter-arrival times and Hosking-GPA marks through an analytical inverse, independently of `PointProcessModel`. PERT timing is restricted to testing whether the rough histogram heuristic places both prior supports. Uniform within-season timing is the posterior-recovery oracle because the production likelihood models season membership but no interior occurrence-time density.

Calendar-year recovery uses block-day changepoints 170 and 350. October-water-year recovery uses shifted block-day changepoints 80 and 260. Both use ordinary automatic priors and the established DEMCzs configuration.

### Recorded and current verification status

| Exact method | Recorded outcome | Current status |
|---|---|---|
| `PointProcessPriorTests.Test_CalendarYearPertHistogram_DefaultPriorsContainBothChangePoints` | Passed 30 July 2026 | Consolidated fixture compiles; focused rerun pending |
| `PointProcessPriorTests.Test_WaterYearPertHistogram_DefaultPriorsContainShiftedChangePoints` | Passed 30 July 2026 | Consolidated fixture compiles; focused rerun pending |
| `PointProcessRecoveryTests.Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | Passed 30 July 2026 | Consolidated fixture compiles; focused rerun pending |
| `PointProcessRecoveryTests.Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | Earlier cell passed with unshifted fixture coordinates | Superseded; current shifted 80/260 cell requires execution |

PERT Bayesian recovery diagnostics pulled K1 toward interior timing mass (calendar truth 170 versus interval `[205,206]`; water-year truth 80 versus `[105,106]`). Those diagnostics are not retained as executable recovery tests. PERT remains a prior-placement stress fixture, not a recovery oracle.

The current source builds with zero warnings. Repository policy prohibited automated execution of Verification methods after the final fixture consolidation, so the current exact cells are not marked passed.

### Exact focused execution required for closeout

Run only these methods, one exact filter at a time:

- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessPriorTests.Test_CalendarYearPertHistogram_DefaultPriorsContainBothChangePoints`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessPriorTests.Test_WaterYearPertHistogram_DefaultPriorsContainShiftedChangePoints`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_NonSeasonalProductionGenerator_RecoversParent`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_MixedObservationLikelihood_MatchesIndependentCalculation`
- `RMC.BestFit.Verification.Univariate.PointProcessTests.PointProcessRecoveryTests.Test_SeasonalMixedObservationLikelihood_MatchesIndependentAnnualMaximumCalculation`

## Traceability

| Concern | Evidence |
|---|---|
| Production model, likelihood, conversion, and simulation | `Models/UnivariateDistribution/PointProcessModel.cs` |
| Analysis orchestration and annual frequency output | `Analyses/Univariate/PointProcessAnalysis.cs` |
| Fast programmatic coverage | `RMC.BestFit.Tests/Univariate/PointProcessModelTests.cs`, `PointProcessAnalysisTests.cs`, `PointProcessChangePointPriorTests.cs`, `DataFrame/ExactDataProcessTests.cs` |
| Independent seasonal fixture | `RMC.BestFit.Verification/Univariate/PointProcessTests/PointProcessSeasonalFixture.cs` |
| Focused scientific cells | `PointProcessPriorTests.cs`, `PointProcessRecoveryTests.cs`, `PointProcessRecoveryTests.Uniform.cs` |