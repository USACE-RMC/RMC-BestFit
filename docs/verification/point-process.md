<!-- verification-status: finalized -->
# Point-Process Verification

This report records the focused Phase 4 evidence for point-process exposure, Poisson-GPA generation, and seasonal changepoint defaults. It does not claim that the full `RMC.BestFit.Verification` project was run.

## TR-004 - Exposure and Rate Definitions

`PointProcessModel` distinguishes exact observed event count and empirical event rate from the fitted GEV-compatible threshold intensity. `Lambda` remains the empirical-rate compatibility alias. Exposure precedence is explicit `TotalYears`, serialized `DataFrame.PointProcessObservationYears`, then the exact year/index span used for manually entered POT data. The final fallback warns because leading and trailing zero-event years cannot be reconstructed from an event-only table.

Fast tests cover source-exposure preservation, explicit override precedence, inferred-span warnings, exact versus non-exact event counting, state refresh, and serialization. Exact observations count as Poisson events; nonseasonal manual records may use year/index values, while every exact observation in a seasonal model must have a valid date for process assignment. Uncertain, interval, and threshold-count rows remain annual/block-indexed magnitude-likelihood information.

For seasonal mixed data, exposure fractions weight the two point-process intensities. Each process produces an exposure-adjusted seasonal maximum, and the annual observation distribution is the independent maximum of those two maxima. It is not a weighted mixture of annual GEV CDFs. A fast regression covers uncertain, interval, and threshold routing through this annual **CompetingRisks** distribution, and a separately coded analytical verification method covers the complete mixed likelihood.

## TR-005 - Poisson-GPA Simulation and Seasonal Priors

### Production process

The fixed-size and duration-based generators use the configured empirical `Lambda` as the Poisson rate of a nonseasonal process and the exposure-weighted sum of the fitted seasonal threshold intensities, `w_1 Lambda_1 + w_2 Lambda_2`, for a seasonal process. Hosking GEV parameters are converted to Hosking GPA parameters through the Madsen relationship, using the empirical rate for a nonseasonal component and each season's fitted intensity for a seasonal component. Seasonal assignment uses the exposure-weighted intensities `w_j Lambda_j`; assigned dates fall inside the corresponding block-day support. The weights belong to the seasonal processes. Annual frequency output instead takes the maximum of their two exposure-adjusted seasonal maxima. No GEV prior, sampler setting, tolerance, or seed contract was changed.

### Automatic changepoint priors

For at least ten exact dated events, the default-prior rule rotates the existing monthly occurrence histogram to the configured block-year start, rejects effectively flat structure using the fixed Pearson cutoff 19.675, applies one circular `[1,2,1]/4` smoothing pass, selects one uniquely strongest separated peak pair, and locates a deterministic valley on each arc. Five-month flat windows centered on those valleys are intersected with `K1` in `[1,251)` and `K2` in `[200,367)`.

The procedure evaluates no point-process likelihood and is not a changepoint estimator. Insufficient, flat, unimodal, tied, undated, or incompatible histograms retain the broad supports. Continuous latent values start at valley-cell centers; likelihood, exposure weights, annual distribution conversion, and simulation use the floored integer days.

### Independent fixtures

`PointProcessSeasonalFixture` generates Poisson counts through exponential inter-arrival times and Hosking-GPA marks through an analytical inverse, independently of `PointProcessModel`. PERT timing is restricted to testing whether the rough histogram heuristic places both prior supports. Uniform within-season timing is the posterior-recovery oracle because the production likelihood models season membership but no interior occurrence-time density.

Calendar-year and October-water-year recovery both use block-day changepoints 170 and 350. The paired fixture changes only the generated date origin and model block convention, so any recovery difference identifies a calendar/water-year coordinate defect rather than a different parent model. Recovery fixtures use 1,000 observations, ordinary automatic priors, and the untouched `BayesianAnalysis` defaults: DEMCzs, four chains, 1,500 warmup iterations, 3,000 sampling iterations, thinning 20, seed 12345, simulation defaults enabled, and posterior-mean reporting. The separate PERT prior-placement cell retains shifted `80/260` targets to exercise histogram rotation.

### Chunk 8 external-package crosswalk

The frozen stationary compatibility cell uses SciPy's generalized-Pareto and Poisson distributions only for claims those distributions directly support. RMC.BestFit models exact values strictly above threshold `u = 80` over a known exposure `T` in years. The annual exceedance intensity is

$$
\Lambda(u)=\left[1+\xi\frac{u-\mu}{\sigma}\right]^{-1/\xi},
$$

with the exponential limit used at `xi = 0`. Conditional excesses `Y = X-u` follow SciPy `genpareto(c=xi, loc=0, scale=sigma_u)`, where `sigma_u = sigma + xi(u-mu)`, and the event count over `T` years follows SciPy `poisson(mu=T Lambda(u))`. Numerics stores Hosking `Kappa = -xi`, so the frozen cell uses RMC.BestFit/Numerics `(Mu, Sigma, Kappa) = (100, 20, -0.1)` and SciPy `(loc, scale, c) = (0, 18, +0.1)` for conditional excesses. Observation and exposure units are events and years; `Lambda` is annual, not a sample-size or seasonal count parameter.

The artifact covers the stationary count probability, conditional tail above `x = 120`, and the compatible annual-maximum `p = 0.99` response. Calendar versus water-year origin and seasonal exposure annualization are deliberately absent: SciPy's univariate `genpareto`/`poisson` objects do not implement this repository's two-process seasonal likelihood. Those claims remain with the separate independent analytical seasonal likelihood, simulation, and recovery constructions below; no package-parity claim is made for them.

### Historical Phase 4 guarded results

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

This 31 July checkpoint is retained as historical evidence. Chunk 8 below supersedes its method identities and recovery acceptance for current-source status. PERT remains prior-placement evidence only because its interior timing law is absent from the fitted likelihood.

## Chunk 8 current-source reconciliation

Chunk 8 replaces completion assertions with predeclared uncertainty evidence. Before every Bayesian run, the cell now confirms that the generating vector lies inside every configured prior, parent likelihood beats a collapsed alternative, sample count/exposure/threshold/block origin agree, Numerics `Kappa` is the negative of Coles `xi`, and seasonal intensities annualize with the floored changepoint exposure fractions. Nonseasonal coordinates use empirical central 95% posterior intervals. Seasonal component recovery uses the likelihood-native Poisson-GPA coordinates: threshold intensity, GPA scale at the threshold, and Hosking Kappa. For component (s), the fixed-size mixture probability is (p_s=w_s\Lambda_s/\sum_j w_j\Lambda_j), so its effective count is (N_s=1000p_s), not 1000. Intensity uses the analytical Poisson standard error; GPA scale and Kappa use Numerics analytical maximum-likelihood covariance scaled to (N_s); every absolute standardized error must be at most 1.96. Every monitored fitted GEV coordinate still requires R-hat below 1.10 and ESS at least 100; changepoints use floored central-95% sets; component threshold-intensity and conditional-tail response bands remain mandatory. Seeds, total N=1000, priors, DEMCzs defaults, and production code are unchanged.

The equal-intensity fixtures have (p_1=186/366) and (p_2=180/366), giving (N_1=508.1967213114754) and (N_2=491.8032786885246). In the 12-versus-4 unequal-intensity fixture, the event-mixture weights include both exposure and intensity, giving (p_1=0.75609756097561), (p_2=0.24390243902439), (N_1=756.09756097561), and (N_2=243.90243902439). Using exposure fraction alone for the unequal fixture would contradict the production generator's event-assignment crosswalk.

The two N=4000 PERT methods are now explicitly prior-placement evidence. A separate test implementation constructs the rotated monthly occurrence histogram, applies the documented circular smoothing and peak/valley rule, and compares both production initial values and support bounds exactly. It does not invoke an estimator or call the production prior helper. The fast `PointProcessChangePointPriorTests` continue to own deterministic defaults, fallbacks, bounds, custom-prior preservation, and serialization.

The frozen stationary artifact records Python 3.12.13, NumPy 2.5.2, and SciPy 1.17.1 values. It verifies the Poisson-GPA likelihood and compatible stationary responses only; seasonal external-package parity is not claimed. Generator SHA-256 is `0d085434034e484a94dc5db3e212127f9043372e376dcbc2904ae7e650a9af56`; artifact SHA-256 is `54a57ea459ba1a71dc9e828672dda83386ed5e137348f9c2740567ba242d7d5b`.

Each method below was run alone through `scripts/run-verification-test.ps1`, and each listed TRX was inspected as a one-result file.

| Exact method | Isolated result directory | TRX duration | Outcome |
|---|---|---:|---|
| `PointProcessLikelihoodOracleTests.NonseasonalMixedObservations_MatchIndependentLikelihood` | `20260830-075825-...NonseasonalMixedObservations_MatchIndependentLikelihood` | 0.037 s | Passed |
| `PointProcessLikelihoodOracleTests.SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood` | `20260830-075831-...SeasonalMixedObservations_MatchIndependentAnnualMaximumLikelihood` | 0.043 s | Passed |
| `PointProcessPriorTests.Test_CalendarYearPertHistogram_MatchesIndependentPriorPlacementOracle` | `20260830-081024-...CalendarYearPertHistogram_MatchesIndependentPriorPlacementOracle` | 0.308 s | Passed |
| `PointProcessPriorTests.Test_WaterYearPertHistogram_MatchesIndependentPriorPlacementOracle` | `20260830-081031-...WaterYearPertHistogram_MatchesIndependentPriorPlacementOracle` | 0.307 s | Passed |
| `PointProcessExternalPackageOracleTests.StationaryPoissonGpa_MatchesSciPyArtifact` | `20260830-081037-...StationaryPoissonGpa_MatchesSciPyArtifact` | 0.037 s | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalProductionGenerator_RecoversParent` | `20260830-081738-...NonSeasonalProductionGenerator_RecoversParent` | 6.720 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalProductionGenerator_RecoversParentAndBothChangePoints` | `20260830-090132-...SeasonalProductionGenerator_RecoversParentAndBothChangePoints` | 31.456 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalProductionGenerator_WithUnequalIntensities_RecoversParentAndBothChangePoints` | `20260830-090050-...UnequalIntensities_RecoversParentAndBothChangePoints` | 28.918 s | Passed |
| `PointProcessRecoveryTests.Test_CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | `20260830-090216-...CalendarYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | 29.746 s | Passed |
| `PointProcessRecoveryTests.Test_WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | `20260830-090303-...WaterYearUniformSeasonality_AutomaticPriorsRecoverParentAndBothChangePoints` | 28.106 s | Passed |
| `PointProcessRecoveryTests.Test_NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail` | `20260830-082100-...NonSeasonalSimulation_MatchesPoissonRateAndConditionalTail` | 0.048 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails` | `20260830-082119-...SeasonalSimulation_MatchesSeasonRatesAssignmentsAndConditionalTails` | 0.081 s | Passed |
| `PointProcessRecoveryTests.Test_SeasonalSimulation_WithUnequalIntensities_MatchesSeasonRatesAssignmentsAndConditionalTails` | `20260830-082125-...UnequalIntensities_MatchesSeasonRatesAssignmentsAndConditionalTails` | 0.085 s | Passed |

All thirteen current Chunk 8 identities pass. The earlier equal-intensity season-two raw-Mu posterior-interval miss was resolved by applying the approved Option B oracle in the likelihood-native Poisson-GPA coordinates with the correct seasonal mixture effective counts. An intermediate unequal-intensity run used exposure fraction alone and failed season-two intensity at standardized error `2.0625878190885034`; correcting the crosswalk to the generator's event-mixture probability produced the final passing run. This was an oracle correction only: no seed, prior, sampler, likelihood, generator, production algorithm, or convergence rule changed.

### Verification-space covering set

| Interaction | Timing/generator | Analytical likelihood | Simulation | Bayesian recovery | External package |
|---|---|---|---|---|---|
| Nonseasonal | Production Poisson-GPA | Passed mixed-observation and frozen stationary likelihood | Passed rate/tail | Passed | Passed stationary SciPy cell |
| Seasonal, equal intensity | Production Poisson-GPA | Passed annual-maximum mixed likelihood | Passed rates/assignments/tails | Passed effective-mixture-N Poisson-GPA recovery | Not package-compatible |
| Seasonal, unequal intensity | Production Poisson-GPA | Exposure/intensity identities checked before MCMC | Passed unequal rates/assignments/tails | Passed | Not package-compatible |
| Calendar-year automatic priors | Independent PERT and uniform timing | Parent versus collapsed likelihood precheck | Covered by independent fixtures | Passed uniform-timing recovery | Block origin not package-supported |
| October water-year automatic priors | Independently shifted PERT and uniform timing | Exact calendar/water block-day likelihood parity | Covered by independent fixtures | Passed uniform-timing recovery | Block origin not package-supported |

This set covers each scientifically distinct interaction without a Cartesian expansion. Accepted external-evidence gaps are seasonal two-process package parity and block-origin support; those remain represented by independent analytical constructions. There is no remaining Chunk 8 execution gap.

## Traceability

| Concern | Evidence |
|---|---|
| Production model, likelihood, conversion, and simulation | `Models/UnivariateDistribution/PointProcessModel.cs` |
| Analysis orchestration and annual frequency output | `Analyses/Univariate/PointProcessAnalysis.cs` |
| Fast programmatic coverage | `RMC.BestFit.Tests/Univariate/PointProcessModelTests.cs`, `PointProcessAnalysisTests.cs`, `PointProcessChangePointPriorTests.cs`, `DataFrame/ExactDataProcessTests.cs` |
| Independent seasonal fixture | `RMC.BestFit.Verification/Univariate/PointProcessTests/PointProcessSeasonalFixture.cs` |
| Focused scientific cells | `PointProcessPriorTests.cs`, `PointProcessLikelihoodOracleTests.cs`, `PointProcessExternalPackageOracleTests.cs`, `PointProcessRecoveryTests.cs`, `PointProcessRecoveryTests.Uniform.cs` |
| Frozen package artifact and generator | `verification/data/point-process/stationary-poisson-gpa-scipy-oracle.json`, `verification/python/point-process/generate_point_process_scipy_oracle.py` |
