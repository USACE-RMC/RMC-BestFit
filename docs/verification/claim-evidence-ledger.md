<!-- verification-status: internal-evidence-control -->

# Verification Claim-Evidence Ledger

This file is an internal publication control and is intentionally excluded from `book-order.txt`. It maps every major public verification claim to its evidence class, executable test group, acceptance basis, and retained result. It does not belong in the verification report narrative.

## Controlled checkpoint

| Item | Controlled value |
|---|---|
| RMC.BestFit | 2.0.0, commit `4304fb39f8e162cdb746083108042df88e153afc` |
| RMC.Numerics source | commit `90a63a46394db9ef95e72b0fcbba943408110636` |
| RMC.Numerics package baseline | 2.1.4 |
| Publication date | 28 August 2026 |
| Execution policy | Exactly one fully qualified verification method per `scripts/run-verification-test.ps1` invocation; never the full project |

## Claim map

| Claim | Public chapter | Evidence basis | Executable evidence | Acceptance and result |
|---|---|---|---|---|
| VC-001: all 15 public univariate families reproduce independent fits and representative distribution values in the tested parameter regions | `report/data-distributions-b17c.md` | SciPy 1.17.1, R `lmomco` 2.5.7, and analytical identities | `ScipyDistributionFittingVerificationTests`; `LmomcoDistributionFittingVerificationTests` | Stored per-quantity absolute/relative tolerances; 17/17 evidence cells pass |
| VC-002: fitting-analysis criteria and Log10-Normal likelihood/RMSE routing reproduce independent calculations | `report/data-distributions-b17c.md` | Closed-form and independently calculated likelihood/criteria | `FittingAnalysisCriteriaVerificationTests` | Elementwise stored tolerances; all declared cells pass |
| VC-003: seven Bulletin 17C examples reproduce published EMA parameter values | `report/data-distributions-b17c.md` | Bulletin 17C example tables | `B17CExampleTests` | Published comparison tolerances; 7/7 pass |
| VC-004: the guarded Bulletin 17C refit population is finite and preserves the declared reliability properties | `report/data-distributions-b17c.md` | Seeded refit/recovery study | Guarded Bulletin 17C refit methods already recorded in the accepted checkpoint | Declared finite-draw and acceptance-count rules; 13,000/13,000 accepted refits |
| VC-005: AIC, BIC, DIC, WAIC, PSIS-LOO, Pareto diagnostics, numerical information, profile likelihood, GMM, and MCMC diagnostics agree with independent calculations | `report/estimation-diagnostics.md` | Closed form, finite difference, R `bbmle`, R `BayesianTools`, R `loo`, R `posterior`, and independent sandwich reconstruction | `InformationCriterionOracleTests`; `McmcNumericalVerificationTests`; `GmmSpecificationVerificationTests`; associated profile/information fixtures | Per-cell tolerances and threshold categories stated in chapter; all reported cells pass |
| VC-006: point-process likelihood and selected generating-model fits reproduce analytical/recovery targets | `report/point-process-analysis.md` | Independent mixed-observation likelihood and seeded recovery | Three selected `PointProcessVerificationTests` methods | Exact/near-exact likelihood gates and declared CDF/parameter recovery gates; selected cells pass |
| VC-007: mixture analyses preserve public full-K behavior while selected MLE and Bayesian fits recover the generating mixture | `report/mixture-analysis.md` | Seeded production-generator recovery | `NormalMixture2D_Recovery_Parity`; `NormalMixture2D_BayesianRecovery` plus four established recovery cells | CDF-first and direct separated-component gates; 6/6 reported cells pass |
| VC-008: competing-risk analytical identities and the accepted MLE/Bayesian subset reproduce their targets | `report/competing-risk-analysis.md` | Closed-form independent/min-max identities and seeded recovery | `CompetingRiskDependencyVerificationTests`; paired constant/increasing two-Weibull MLE and Bayesian methods | Analytical rank/CDF tolerances and CDF-first recovery gates; 18 accepted cells pass; six additional Bayesian fixtures excluded from the claim |
| VC-009: composite analysis recovery passes across weighting, dependence, and data transforms | `report/composite-analysis.md` | Seeded recovery and direct distribution calculations | `CompositeRecoveryTests`; `PosteriorResamplingVerificationTests` | Declared parent-CDF, parameter, weight, and transform gates; 12/12 reported cells pass |
| VC-010: time-series prediction/generation follows the independent recurrence and transformation algebra, and selected MLE/Bayesian models recover generating parameters | `report/time-series-analyses.md` | Independent AR/MA/ARIMA/ARIMAX recurrences, seeded moment tests, and recovery | `TimeSeriesIndependentOracleTests`; selected ARIMA MLE/Bayesian methods | Analytical equality or stated moment/recovery tolerances; 49/49 reported cells pass |
| VC-011: rating-curve likelihood, activation continuity, and three synthetic fits reproduce independent targets | `report/rating-curve.md` | SciPy likelihood/optimum, analytical continuity, and recovery | `RatingCurveLikelihoodOracleTests`; selected three-control MLE/Bayesian methods | Stored likelihood tolerances and curve/parameter gates; 36/36 reported cells pass |
| VC-012: six copula families reproduce independent pseudo-likelihood and IFM optima, with accepted bivariate/CFA recovery | `report/bivariate-analyses.md` | Independent Python copula oracle and seeded recovery | `CopulaEstimationOracleTests`; `RecoverNormalCopulaParameters`; `SumOfNormals_RhoZero_MatchesClosedForm` | Parameter/log-likelihood tolerances and recovery gates; 23/23 reported cells pass |
| VC-013: spatial likelihood, observed-subset marginalization, distances, kriging, prediction, uncertainty propagation, and accepted basic recovery reproduce independent targets | `report/spatial-extremes.md` | R `mvtnorm`, conditional-GP and haversine oracles, direct recomputation, and seeded recovery | `SpatialGEVLikelihoodOracleTests`; `SpatialGEVKrigingOracleTests`; `SpatialGEVDistanceOracleTests`; selected basic MLE/Bayesian recovery | Stored numerical tolerances and declared recovery gates; 30/30 reported cells pass |

## Publication boundaries

The public report does not claim validation outside the tested distributions, parameter regions, censoring patterns, sample sizes, seeds, covariate scenarios, or spatial networks. Six difficult competing-risk Bayesian fixtures, exact leave-one-out refits, arbitrary rating-control matrices, extrapolation coverage, and universal MCMC convergence are not included in the verified claim set.

## Refresh record

The accepted evidence values above derive from the documented 21–22 August 2026 method-level checkpoint. On 24 August 2026, a publication refresh reran 55 selected analytical, external-package, published-source, and independently implemented oracle methods and 15 representative recovery methods against the controlled source commits. All 70 methods passed. Each invocation used `scripts/run-verification-test.ps1`, resolved one exact fully qualified method name, and produced one passing TRX result.

The recovery refresh spans point-process, mixture, competing-risk, bivariate, coincident-frequency, rating-curve, time-series, and spatial-extremes analyses. One verification-only rating-curve fixture reader was aligned with the two timing-field layouts already present in the committed example data before its exact rerun; production code, reference data, test expectations, tolerances, sampler settings, and seeds were unchanged.
