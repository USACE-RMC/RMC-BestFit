<!-- technical-reference-status: complete -->

# Univariate Distribution Verification Matrix

[Distribution index](index.md) · [API traceability](../api-traceability.md) · [Scientific findings](../review-findings.md)

This matrix separates three kinds of evidence that must not be conflated:

1. The pinned RMC.Numerics source establishes the implemented density, CDF, quantile, moments, parameter constraints, and estimation routines.
2. The compiled documentation fixture establishes that every displayed C# call matches the current .NET API.
3. `RMC.BestFit.Verification` contains long-running parameter-recovery or published-result comparisons. These tests are listed for traceability but are not executed by Codex under the repository policy; listing a test is not a claim about the latest run.

All family API snippets are compiled by `DistributionExamples` and checked byte-for-byte by `TechnicalReferenceDocumentationTests.CompletedPages_CSharpBlocksMatchCompiledSourceRegions`.

## Estimation and External-Result Evidence

| Family | BestFit verification symbol | External basis recorded by the test | Scope and caveat |
|---|---|---|---|
| Normal | `UnivariateAnalysisMAPTests.Test_Normal_MAP` | Rao and Hamed (2000), Tippecanoe River Table 5.1.1 | Bayesian analysis completes; MAP mean/scale within 5% tolerances |
| Ln-Normal | `Test_LnNormal_MAP` | Rao and Hamed (2000), Wabash River Table 1.8.1 | Checks internal natural-log location/scale after fitting; public natural-moment crosswalk requires separate unit checks |
| Log-Normal | `Test_LogNormal_MAP` | Rao and Hamed (2000), Wabash River Table 1.8.1 | Base-10 log mean/scale within 5% tolerances |
| Generalized Normal | `Test_GeneralizedNormal_MAP` | R `datasets` air-quality wind data and R `lmom` estimates | MAP compared with L-moment targets; 5% location/scale and 10% relative shape tolerances |
| Exponential | `Test_Exponential_MAP` | Rao and Hamed (2000), Wabash River Example 6.1.1 | Location/scale within 5% tolerances |
| Gamma | `Test_GammaDist_MAP` | Bobée and Ashkar (1991), Harricana River Table 1.2 | Scale/shape within 5% tolerances |
| Pearson III | `Test_PearsonTypeIII_MAP` | Bobée and Ashkar (1991), Harricana River Table 1.2 | Moment parameters within 5% tolerances |
| Log-Pearson III | `Test_LogPearsonTypeIII_MAP` | Bobée and Ashkar (1991), Harricana River Table 1.2 | Log mean/scale within 5%, log skew within 10% |
| Gumbel | `Test_Gumbel_MAP` | Rao and Hamed (2000), Sugar Creek Example 7.2.1 | Location/scale within 5% tolerances |
| Weibull | `Test_Weibull_MAP` | Tippecanoe River data; two-parameter R-Stan comparison | Scale/shape within 5%; not a comparison with the cited three-parameter textbook form |
| GEV | `Test_GeneralizedExtremeValue_MAP` | Rao and Hamed (2000), White River Example 7.1.1 | Location/scale within 5%; absolute shape tolerance 0.01 |
| GPD | `Test_GeneralizedPareto_MAP` | Rao and Hamed (2000), White River at Mt. Carmel Example 8.3.1 | Location/scale within 5%, shape within 10%; conditional magnitude fit, not complete POT occurrence validation |
| Kappa Four | `Test_Kappa4_MAP` | R air-quality wind data and R `lmom` estimates | Nonzero fitted shapes within stated tolerances; does not cover the open zero-$\kappa$ branches in TR-001 |
| Logistic | `Test_Logistic_MAP` | Rao and Hamed (2000), Tippecanoe River Example 9.1.1 | Location/scale within 5% |
| Generalized Logistic | `Test_GeneralizedLogistic_MAP` | Rao and Hamed (2000), East Fork White River; textbook/data-summary discrepancy noted in test | 10% tolerances; source test explicitly warns that table summaries and raw-data moments differ |

The named tests configure Bayesian analyses and compare MAP output to existing targets. They do not by themselves verify every PDF/CDF branch, censored likelihood, nonstationary trend, prior, or rare-tail probability.

## Deterministic Distribution Evidence Required

For every family, a release-quality numerical evidence set should contain:

- density normalization on each support regime;
- monotonicity and boundary values of $F$;
- $F(Q(p))\approx p$ over central and tail probabilities;
- analytic moments where they exist;
- special and limiting-family identities;
- transformation Jacobians for log families;
- parameter-dependent endpoint tests; and
- independent package parity after applying the exact parameterization crosswalk.

The pinned Numerics repository includes direct distribution tests for several families. In particular, `Test_Numerics.Distributions.Univariate.Test_KappaFour` verifies R `lmom` L-moment estimates, nonzero-shape CDF values, nonzero-shape CDF/quantile inversion, and derivative calculations. It does not test the zero-$\kappa$ density or the $\kappa=0,h\ne0$ inverse branch; the isolated audit values are recorded in TR-001.

## Reproduction Policy

Do not replace the evidence above with hand-written output. A reviewer reproducing a long-running test should run only the narrowly selected verification method and record the commit, runtime, seed, diagnostic settings, and observed tolerance. Codex will not execute the full verification project.

---

[Distribution index](index.md)
