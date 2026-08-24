<!-- technical-reference-status: complete -->

# Distribution Verification Matrix

[Distribution index](index.md) | [API traceability](../api-traceability.md) | [Verification report](../../verification/report/executive-summary.md)

This matrix records the independent basis for distribution-level claims. A fast contract protects deterministic behavior; a verification cell adds an analytical, independently implemented, external-package, published, recovery, or coverage reference.

| Model or family | Independent evidence | Acceptance basis | Current result |
|---|---|---|---:|
| Normal | Closed-form MLE and SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Log10-Normal | Closed-form transformed MLE | Parameters, original-scale likelihood, CDF, and quantiles | Passed |
| Ln-Normal | SciPy with moment-to-log conversion | Parameters, likelihood, CDF, and quantiles | Passed |
| Exponential | Closed form and SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Gamma | SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Generalized Extreme Value | SciPy with explicit shape-sign conversion | Parameters, likelihood, CDF, and quantiles | Passed |
| Generalized Logistic | R `lmomco` | Parameters, likelihood, CDF, and quantiles | Passed |
| Generalized Normal | R `lmomco` | Parameters, likelihood, CDF, and quantiles | Passed |
| Generalized Pareto | SciPy global optimum | Parameters, likelihood, CDF, and quantiles | Passed |
| Gumbel | SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Kappa Four | Analytical zero-shape identities and SciPy finite-shape cases | PDF derivative, support, CDF-quantile inversion | Passed |
| Logistic | Closed form and SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Log-Pearson Type III | Transformed SciPy Pearson III | Parameters, likelihood, CDF, and quantiles | Passed |
| Pearson Type III | SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Weibull | SciPy | Parameters, likelihood, CDF, and quantiles | Passed |
| Point process | Independent Poisson clocks, GPA inverse marks, analytical likelihood, and recovery | Ten declared cells; 1,000-observation recovery under production defaults | Passed 10 of 10 |
| Finite mixture | Numerics parity and Bayesian generation-recovery | Pre-fit likelihood, EM optimum, parent parameters, posterior diagnostics | Passed 6 of 6 |
| Competing risks | Gaussian-copula rank/CDF identities and recovery | Four analytical, ten MLE, and four supported Bayesian cells | Passed 18 of 18 reported cells |
| Composite/model average | Closed forms, published R `mistr`, Gaussian orthants, and Cartesian posterior enumeration | Ten composite and two posterior-resampling cells | Passed 12 of 12 |

## Artifact controls

External generators record package versions, seeds, conversions, commands, and source hashes. The C# verification methods consume committed JSON or published values and do not invoke R or Python at test time. SHA-256 values are maintained in `verification/data/MANIFEST.md`.

## Interpretation

Passing family cells verify the stated parameter regions and numerical quantities, not every possible tail probability or optimizer start. Passing recovery cells are conditional on the generating model, sample size, seed, production configuration, and acceptance rule. The competing-risk Bayesian claim excludes six maximum or correlated designs that did not satisfy their predeclared diagnostic or recovery gates.

---

[Distribution index](index.md) | [Verification report](../../verification/report/data-distributions-b17c.md)
