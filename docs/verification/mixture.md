# Mixture Verification

## Scope

This report covers Phase 4 findings TR-006, TR-007, and TR-008:

- identified $K-1$ BestFit weights with a full-$K$ Numerics physical-weight API;
- a coherent point mass at zero with positive-conditioned components; and
- explicit EM failure for impossible rows.

The correction intentionally provides no migration or parameterization version for posterior results saved under the former normalized $K$-weight workflow. Those mixture results require re-estimation.

## Implemented Contracts

### TR-006 — weights and proposal purity

Numerics still accepts and returns all $K$ physical weights, but copies every caller array before assignment or normalization. BestFit tracks $w_1,\ldots,w_{K-1}$ and derives

$$
w_K=m-\sum_{j=1}^{K-1}w_j,
\qquad m=1\ \text{or}\ 1-\pi_0.
$$

All likelihood, prior, posterior reconstruction, point-estimate, frequency-result, and simulation paths use one nonmutating conversion. The flat simplex prior has log density

$$
\log\Gamma(K)-(K-1)\log m.
$$

EM result and covariance dimensions, model parameter count, and AIC/BIC dimension use $K-1$ weights.

### TR-007 — positive hurdle

Numerics now has no negative support when zero inflation is enabled, reports the CDF jump and atom at zero, conditions each continuous component on $X>0$, and uses that law for density, log density, quantiles, and simulation. BestFit derives $\pi_0$ from exact values equal to zero divided by all exact annual records. Mixed observation likelihoods add the atom separately and condition continuous contributions on positive support.

### TR-008 — impossible rows

Numerics and BestFit EM throw **InvalidOperationException** with row index and value when a required row probability is zero or nonfinite. Fast BestFit coverage includes exact, uncertain, interval, and threshold rows. Negative exact observations are rejected in positive-hurdle models.

## Automated Evidence

### Numerics

Commit **1462e35** contains the focused Numerics correction and tests.

- Release build: zero warnings and zero errors for net481, net8.0, net9.0, and net10.0.
- Focused mixture suite: 20/20 passed, including the existing two 2D and one 3D recovery fixtures.
- Full Release suite: 2,016/2,016 passed independently on each target framework.

### BestFit fast tests

Fast coverage includes $K-1$ counts and names, final-weight derivation, prior normalization, proposal immutability, covariance dimensions, exact-only atom derivation, mixed likelihoods, negative exact values, invalid positive mass, and impossible rows for every observation family.

The Core Debug suite passed 3,101/3,101 tests. The strict XML Debug build, Release solution build, public API baseline, UI 564/564, App 428/428, and Verification compilation also passed with zero failures or build warnings. The repository-wide gates are recorded in **docs/PROGRESS.md**.

## Focused Recovery Results

All six methods generate $n=1000$ observations with seed 12345 through the production `MixtureModel.GenerateRandomValues` method. Each method was run separately through `scripts/run-verification-test.ps1`, source-resolved to one fully qualified method, and produced exactly one passing TRX under `TestResults/VerificationFocused`.

| Exact method | Verification contract | Guarded duration | Status |
|---|---|---:|---|
| `NormalMixture2D_Recovery_Parity` | Two-component Normal generation, pre-fit likelihood parity, EM parity, and parent recovery | 1.426 s | Passed |
| `ZeroInflatedNormalMixture2D_Recovery_Parity` | Positive-hurdle generation, likelihood parity, EM parity, and parent recovery | 1.852 s | Passed |
| `NormalMixture3D_Recovery_Parity` | Three-component Normal generation, likelihood parity, EM parity, and parent recovery | 3.247 s | Passed |
| `NormalMixture2D_BayesianRecovery` | Two-component `MixtureAnalysis` posterior recovery and diagnostics | 9.589 s | Passed |
| `ZeroInflatedNormalMixture2D_BayesianRecovery` | Positive-hurdle `MixtureAnalysis` recovery, atom check, and diagnostics | 21.346 s | Passed |
| `NormalMixture3D_BayesianRecovery` | Three-component `MixtureAnalysis` posterior recovery and diagnostics | 12.761 s | Passed |

The parity methods give Numerics and BestFit the same BestFit-generated sample, compare pre-fit data log likelihoods at $10^{-10}$, compare fitted engines at $10^{-8}$, location-sort component labels, and retain absolute recovery tolerance 0.1.

The Bayesian methods use DEMCzs with four chains, 1,500 warmup iterations, 3,000 sampling iterations, thinning interval 5, 5,000 output draws, 90% credible intervals, deterministic sampler seeds, and posterior mode as the recovery estimate. Physical weights use absolute tolerance 0.1; component parameters use $\max(0.15, 0.15|\theta|)$. Every coordinate must have finite split R-hat below 1.1 and conservative ESS above 100. The positive-hurdle method also checks the empirical atom against a five-standard-error binomial bound.

## Closeout State

The Phase 4 mixture subset is complete. TR-006, TR-007, and TR-008 are implemented and verified by fast contracts, three guarded cross-engine parity methods, and three guarded Bayesian generation-and-recovery methods. The broader Phase 4 remains open for competing-risk and composite work; the point-process subset is also closed.
