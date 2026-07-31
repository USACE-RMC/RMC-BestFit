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

Calendar-year and October-water-year recovery both use block-day changepoints 170 and 350. The paired fixture changes only the generated date origin and model block convention, so any recovery difference identifies a calendar/water-year coordinate defect rather than a different parent model. Recovery fixtures use 1,000 observations, ordinary automatic priors, and the untouched `BayesianAnalysis` defaults: DEMCzs, four chains, 1,500 warmup iterations, 3,000 sampling iterations, thinning 20, seed 12345, simulation defaults enabled, and posterior-mean reporting. The separate PERT prior-placement cell retains shifted `80/260` targets to exercise histogram rotation.

### Current-source guarded results

On 31 July 2026, each of the ten closeout methods was run separately through `scripts/run-verification-test.ps1`. Every invocation source-resolved one fully qualified method and produced exactly one TRX. The full Verification project was not run.

| Exact method | Contract | Duration | Outcome |
|---|---|---:|---|
| `PointProcessPriorTests.Test_CalendarYearPertHistogram_DefaultPriorsContainBothChangePoints` | Calendar-year automatic prior placement | 0.617 s | Passed |
| `PointProcessPriorTests.Test_WaterYearPertHistogram_DefaultPriorsContainShiftedChangePoints` | October-water-year automatic prior placement | 0.722 s | Passed |
| `PointProcessRecoveryTests.Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | Calendar-year Bayesian parent and changepoint recovery | 35.690 s | Passed |
| `PointProcessRecoveryTests.Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | October-water-year block-origin parity and Bayesian recovery | 33.897 s | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalProductionGenerator_RecoversParent` | Production generation and nonseasonal Bayesian recovery | 8.450 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints` | Production generation and seasonal Bayesian recovery | 36.309 s | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail` | Poisson rate and conditional-tail oracle | 0.293 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails` | Seasonal rates, assignments, dates, and conditional-tail oracle | 0.334 s | Passed |
| `PointProcessRecoveryTests.Test_MixedObservationLikelihood_MatchesIndependentCalculation` | Independent nonseasonal mixed-likelihood calculation | 0.294 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalMixedObservationLikelihood_MatchesIndependentAnnualMaximumCalculation` | Independent seasonal annual-maximum mixed likelihood | 0.291 s | Passed after fixture correction |

The initial seasonal mixed-likelihood run exposed a verification-fixture defect rather than a production discrepancy. Its two-year threshold record declared two exceedances, so `ProcessThresholdSeries` correctly suppressed the all-above threshold while the oracle still hard-coded two right-censored terms. The fixture now uses a three-year record, asserts one effective observation below and two above, and derives both oracle contributions from the processed counts. Production density, CDF, interval, and threshold probabilities matched the independent formulas to machine precision before the corrected method passed at the unchanged `2E-7` tolerance.

Using the default configuration and 1,000 observations eliminated the former seasonal production second-Kappa miss: calendar-year uniform recovery, nonseasonal production recovery, and seasonal production recovery all pass. This result supersedes the earlier diagnostic runs that customized chains, warmup, sampling iterations, thinning, seeds, and fixture size.

The original water-year cell changed block-day changepoints from `170/350` to `80/260`; that was a different parent model, not the calendar fixture under an October year origin. Holding the parameters fixed and changing only the date origin and block convention makes the water-year cell pass. Before MCMC, the corrected method verifies identical generated magnitudes and block days, an exact 92-day date-origin shift, and parent data log-likelihood parity at `1E-10`. This confirms that the production generator and block-day likelihood are consistent across calendar and water years.

TR-004 and TR-005 are closed in the approved point-process scope. All ten guarded cells pass. PERT remains prior-placement evidence only because its interior timing law is absent from the fitted likelihood. No production formula, DEMCzs default, prior, or recovery tolerance changed.

## Traceability

| Concern | Evidence |
|---|---|
| Production model, likelihood, conversion, and simulation | `Models/UnivariateDistribution/PointProcessModel.cs` |
| Analysis orchestration and annual frequency output | `Analyses/Univariate/PointProcessAnalysis.cs` |
| Fast programmatic coverage | `RMC.BestFit.Tests/Univariate/PointProcessModelTests.cs`, `PointProcessAnalysisTests.cs`, `PointProcessChangePointPriorTests.cs`, `DataFrame/ExactDataProcessTests.cs` |
| Independent seasonal fixture | `RMC.BestFit.Verification/Univariate/PointProcessTests/PointProcessSeasonalFixture.cs` |
| Focused scientific cells | `PointProcessPriorTests.cs`, `PointProcessRecoveryTests.cs`, `PointProcessRecoveryTests.Uniform.cs` |
