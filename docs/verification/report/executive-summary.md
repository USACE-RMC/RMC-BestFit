<!-- verification-status: publication-draft -->

# Executive Summary

## Purpose and scope

The purpose of this report is to establish which scientific calculations in RMC.BestFit 2.0 have been compared with evidence independent of the production implementation. The report does not attempt to prove correctness for every possible model, data set, prior, or tail probability. It documents bounded claims whose test design, reference result, and acceptance rule can be inspected and reproduced.

The report follows the application's project-tree order: time-series data; input data; distribution fitting; univariate, Bulletin 17C, point-process, competing-risk, mixture, and composite analyses; bivariate and coincident-frequency analyses; rating-curve analysis; AR, MA, ARIMA, and ARIMAX analyses; and spatial extremes. Maximum-likelihood, maximum-a-posteriori, generalized-method-of-moments, Bayesian estimation, model comparison, and convergence diagnostics are shared foundations.

## Verification conclusion

At the stated software checkpoint, the independently supported cells summarized below satisfy their declared acceptance criteria. The strongest evidence is exact analytical or external-package parity. Recovery evidence is conditional on its generating model, sample size, seed, estimator configuration, and acceptance rule. Simulation coverage is claimed only where a predeclared repetition design and Monte Carlo acceptance interval were executed for the stated checkpoint.

The final source catalog contains 384 declarations and 384 execution units: 326 verified, 56 governed
execution exclusions for the three Bulletin 17C confidence-interval coverage classes, 2 accepted
limitations, and no open gaps.

Time-series-data and input-data persistence, validation, and processing contracts passed the fast regression gate. Those two collection-level results control the handoff to the scientific models but are not counted as independent numerical verification.

| Capability | Verification basis | Result at the accepted checkpoint |
|---|---|---:|
| Fifteen univariate families and multi-candidate fitting | N=1000 recovery, SciPy, R `lmomco`, analytical identities, and independent fitting oracles | Passed under current exact identities |
| Model comparison and convergence diagnostics | R `loo`, R `posterior`, R `gmm`, R `bbmle`, and analytical covariance results | All reported oracle comparisons passed |
| Bulletin 17C worked examples | Published example parameters and PeakFQ plotting positions | Seven parameter examples and three plotting-position comparisons passed |
| Point-process models | Analytical Poisson/GPA calculations, likelihood identities, and N=1000 recovery | Passed under current exact identities |
| Competing risks | Analytical dependence identities and five identified N=1000 recoveries | Passed within the declared fixture scope |
| Finite mixtures | External-package overlap, identified EM uncertainty, and Bayesian generation-recovery | Six retained identities passed |
| Composite analyses | Closed forms, R `mistr`, Gaussian orthants, Cartesian posterior enumeration, and four predictive recoveries | Passed under current exact identities |
| Bivariate and coincident frequency | Independent copula optima, recovery, and closed-form/Lognormal response oracles | Passed under current exact identities |
| Rating curves | SciPy likelihood and MLE optima, analytical continuity, and ten reconciled recovery designs | Passed; historical Bayesian example percentage bands excluded |
| Time-series models | Separate AR, MA, ARIMA, and ARIMAX recurrence, transform, likelihood, forecast, and recovery evidence | Eight retained recovery identities and independent oracle groups passed |
| Spatial extremes | R `mvtnorm`, correlation, cross-validation, prediction, uncertainty, simulation, and 10-site by 100-row recovery | Eight revised recovery identities and independent oracle groups passed |

Bootstrap delivery and accounting results remain engineering evidence and are not counted as scientific
accuracy or interval-coverage claims. The three Bulletin 17C confidence-interval coverage classes remain
execution-excluded historical evidence.

## Interpretation

A passing cell supports the claim stated for that cell. It does not establish universal accuracy outside the tested support, asymptotic regime, sample size, dependence structure, or prior configuration. In particular:

- Competing-risk recovery is limited to the five identified retained fixtures; removed boundary, local-mode, and correlated-Bayesian designs are not claimed as verified.
- Current Bulletin 17C Cohn-value and broad coverage claims are excluded because the corresponding numerical comparisons were not executed at this checkpoint.
- Bivariate copula fitting conditions on fixed fitted marginals; joint marginal-copula posterior estimation is not claimed.
- Spatial weighting is a heuristic influence weighting and not a composite pairwise likelihood or effective-sample-size correction.
- Generic prior-predictive sampling is not a joint-prior sampler when a model contains coupled prior factors beyond independently sampled parameter priors.

These boundaries are limitations of the supported claim set, not evidence that the verified calculations failed.
