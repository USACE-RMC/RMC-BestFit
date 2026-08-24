<!-- verification-status: publication-draft -->

# System Under Test and Verification Methodology

## System under test

All results in this draft are tied to the following reproducible configuration.

| Component | Checkpoint |
|---|---|
| RMC.BestFit | Version 2.0.0; commit `4304fb39f8e162cdb746083108042df88e153afc` |
| RMC.Numerics source used for peer review | Commit `90a63a46394db9ef95e72b0fcbba943408110636` |
| Published package compatibility baseline | RMC.Numerics 2.1.4 |
| Runtime | .NET on Windows, 64-bit process |
| Verification execution | One fully qualified method per test process, with a uniquely named TRX record |
| Bayesian workhorse | DEMCzs production defaults unless a test states otherwise |
| Default reproducibility seed | 12345 unless a fixture declares another seed |

The source checkpoint and package baseline answer different questions. The source checkpoint identifies the numerical code used for the peer-review evidence. The package baseline identifies the published dependency version against which public compatibility and serialized payloads were checked.

## Terminology

**Verification** asks whether the software implements the specified equations and algorithms correctly. **Validation** asks whether a model is an adequate representation of the physical process for a particular application. This report primarily provides verification. Recovery and coverage studies contribute limited validation evidence for their declared simulation designs, but they do not validate a model for every field application.

## Evidence hierarchy

Evidence is classified before results are interpreted:

1. **Analytical oracle.** A closed-form value, identity, derivative, recurrence, or hand calculation independent of the production path.
2. **Independent numerical implementation.** A separately written R or Python calculation with its generator, environment, source inputs, and hash recorded.
3. **External scientific package.** A calculation from packages such as R `loo`, `posterior`, `gmm`, `bbmle`, `mvtnorm`, `copula`, or Python SciPy.
4. **Published result.** A table, worked example, or standard with sufficient parameterization detail to reproduce the comparison.
5. **Recovery experiment.** Data are generated from known parameters and refitted; acceptance is based on parameter, curve, quantile, and diagnostic criteria.
6. **Coverage experiment.** Repeated simulated data sets test whether nominal confidence or credible intervals contain the generating quantity at the declared rate.

An estimator that merely completes, converges, returns finite values, or agrees with another production path is not independently verified. Constructor, validation, state, cache, serialization, exception, and fixed-calculation tests remain in the fast regression projects and are not counted as scientific verification cells.

## Deterministic comparisons

For a scalar reference value $r$ and software value $s$, absolute and relative errors are

$$e_{\mathrm{abs}}=|s-r|,\qquad e_{\mathrm{rel}}=\frac{|s-r|}{\max(|r|,e_0)},\tag{M.1}$$

where $e_0$ is the stated scale floor. A cell passes when its declared absolute or relative error is no greater than the predeclared tolerance. Vector and matrix comparisons apply the same rule elementwise unless the test states a norm or specialized statistic.

## Recovery experiments

Recovery tests predeclare the generating model, retained sample size, seed, fitted coordinates, and response quantity. Maximum-likelihood recovery compares the fitted optimum with the independent optimum or generating parameter. Bayesian recovery ordinarily requires:

- the generating parameter or curve to lie inside the stated central posterior interval;
- rank-normalized $\widehat R<1.1$ for monitored coordinates;
- effective sample size greater than the declared minimum, commonly 100; and
- a point or curve estimate within the declared relative tolerance.

Recovery is evaluated on the scientifically interpretable quantity whenever parameter non-identifiability makes individual coordinates misleading. Mixture weights and rating-curve segments are examples where ordered or curve-based assertions are important.

## Coverage experiments

For $B$ independent repetitions and indicator $I_b$ that the interval contains the truth, empirical coverage is

$$\widehat C=\frac{1}{B}\sum_{b=1}^{B}I_b.\tag{M.2}$$

The Monte Carlo standard error at nominal coverage $C_0$ is

$$\operatorname{MCSE}(\widehat C)=\sqrt{\frac{C_0(1-C_0)}{B}}.\tag{M.3}$$

A coverage claim is reported only when the repetition count, nominal target, completion rule, and Monte Carlo acceptance interval were declared before execution. Re-enabled but unexecuted assertions are not reported as results.

## Bayesian diagnostics

Rank-normalized split $\widehat R$, bulk effective sample size, and tail effective sample size are compared with R `posterior`. PSIS-LOO values, smoothed importance weights, Pareto $k$, and importance-sampling effective sample size are compared with R `loo`. A numerically equal aggregate alone is insufficient; the pointwise decomposition and diagnostic vectors are also compared.

## Artifact and execution controls

Independent oracle artifacts are committed beneath `verification/data/` with generation metadata and SHA-256 entries in the data manifest. Recovery fixtures record their generating recipe, sample size, seed, parameterization, and acceptance thresholds. Long-running methods are executed separately to prevent fixed result logs or shared test hosts from coupling otherwise independent evidence cells. The complete verification project is not used as a publication command; the report is supported by the explicit method-level evidence set described in its chapters and internal claim-evidence ledger.

## Controlled publication refresh

The following exact-method refresh was completed on 24 August 2026 at the controlled BestFit and Numerics commits in Table 1. It is a reproducibility check on selected cells from the larger evidence matrices reported in the analysis chapters; it does not replace or enlarge those claim sets.

| Refresh group | Test design | Result |
|---|---|---:|
| Analytical, external-package, published-source, and independently implemented oracle methods | One exact method and one TRX record per guarded invocation | 55 of 55 passed |
| Representative MLE, Bayesian, likelihood, and posterior-propagation recovery methods | Point process through spatial extremes, using the predeclared seeds and acceptance rules | 15 of 15 passed |
| **Total** | No production algorithm, tolerance, sampler setting, seed, or reference contract changed | **70 of 70 passed** |
