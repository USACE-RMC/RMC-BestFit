<!-- technical-reference-status: complete -->

# Distribution Verification Matrix

[Distribution index](index.md) | [Verification report](../../verification/report/data-distributions-b17c.md)

Distribution verification separates formula evaluation at a common parameter point from the result of fitting an optimizer or sampler. The first isolates parameterization and numerical functions; the second also tests estimation under its declared design.

| Family | Independent formula and fitted-objective reference |
|---|---|
| Exponential | Analytical solution and SciPy shifted exponential |
| Gamma | SciPy Gamma, converting shape/scale order |
| Generalized Extreme Value | SciPy GEV, whose shape agrees with Numerics and is opposite the Coles convention |
| Generalized Logistic | R `lmomco`, Hosking parameterization |
| Generalized Normal | R `lmomco`, Hosking transformed Normal |
| Generalized Pareto | SciPy GPD, reversing shape sign and retaining the location boundary |
| Gumbel | SciPy Gumbel |
| Kappa Four | SciPy four-parameter Kappa and analytical limiting-family identities |
| Ln-Normal | SciPy lognormal, converting latent log parameters into response mean and SD |
| Logistic | SciPy Logistic |
| Log-Normal | Analytical base-10 transformed Normal and SciPy |
| Log-Pearson Type III | SciPy Pearson III on base-10 logs with the original-measure Jacobian |
| Normal | Analytical Normal MLE and SciPy |
| Pearson Type III | SciPy Pearson III with mean/SD/skew mapping |
| Weibull | SciPy Weibull, converting shape/scale order |

## Formula and optimum acceptance

At common external parameters, PDF, CDF, quantile, and likelihood values must agree within $10^{-8}$ absolute plus $10^{-7}$ relative tolerance. Separately fitted optima are compared through twice the absolute log-likelihood difference and the declared joint 95% chi-square threshold for two, three, or four parameters. This measures the loss of fit for the parameter combination rather than demanding identical optimizer coordinates. For the free-location Pareto boundary, it is a declared comparison screen, not proof that ordinary likelihood-ratio asymptotics apply.

The executable sources are [SciPy comparisons](../../../src/RMC.BestFit.Verification/DistributionFitting/ScipyDistributionFittingVerificationTests.cs), [R comparisons](../../../src/RMC.BestFit.Verification/DistributionFitting/LmomcoDistributionFittingVerificationTests.cs), and the [Kappa limits](../../../src/RMC.BestFit.Verification/DistributionFitting/KappaFourZeroShapeVerificationTests.cs). The [artifact manifest](../../../verification/data/MANIFEST.md) records package versions, input conversions, seeds, generators, and hashes. The [catalog](../../verification/verification-catalog.json) names exact methods and acceptance rules; the report records their results.

## Recovery and published examples

The fifteen-family Bayesian designs use 1,000 observations and unchanged production sampler settings. Generating coordinates must lie in central 95% posterior intervals, with R-hat below 1.10 and ESS at least 100. Exponential and Pareto zero-location boundaries are checked through an identified quantile rather than pretending an interior parameter interval applies. These are verification acceptance limits; the MCMC chapter gives the stricter diagnostic guidance for an applied analysis. A single retained realization does not demonstrate repeated-sampling coverage.

The [MLE family sources](../../../src/RMC.BestFit.Verification/DistributionFitting/UnivariateDistributionMLETests.cs), [Bayesian family sources](../../../src/RMC.BestFit.Verification/Univariate/ValidationTests/UnivariateValidationTests.cs), and [fitting recovery sources](../../../src/RMC.BestFit.Verification/DistributionFitting/FittingAnalysisRecoveryTests.cs) define the individual designs. The report also reconciles fifteen real-source comparisons, including differences in estimation method and textbook versus tabulated sample statistics. Their scope must not be extended to arbitrary tails or sample sizes.

## Combined distributions

Point-process occurrence/exposure, mixture memberships, competing-risk dominance, and composite uncertainty each introduce contracts beyond the component families. Their chapters and the [verification-evidence map](../appendices/verification-evidence.md) identify those independent comparisons. Agreement of component PDFs alone does not establish correctness of a combined likelihood or uncertainty calculation.
