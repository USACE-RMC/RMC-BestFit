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

## Focused Recovery Execution Required

The following exact methods compile but have not been executed by Codex:

1. **RMC.BestFit.Verification.Univariate.MixtureTests.MixtureRecoveryTests.NormalMixture2D_Recovery_Parity**
2. **RMC.BestFit.Verification.Univariate.MixtureTests.MixtureRecoveryTests.ZeroInflatedNormalMixture2D_Recovery_Parity**
3. **RMC.BestFit.Verification.Univariate.MixtureTests.MixtureRecoveryTests.NormalMixture3D_Recovery_Parity**

Each method:

- generates $n=1000$ observations with seed 12345;
- gives Numerics and BestFit the same sample;
- compares pre-fit data log likelihoods at $10^{-10}$;
- runs both EM entry points;
- reconstructs and location-sorts all physical weights and Normal components;
- compares fitted engines at $10^{-8}$; and
- retains recovery tolerance 0.1 against generating values.

Run only these methods using the repository's guarded verification workflow. Do not widen EM or recovery tolerances if a fixture fails; preserve the output and reconcile the formula or implementation.

## Closeout State

Implementation and fast-test evidence are complete. TR-006/TR-007/TR-008 remain verification-pending until Haden supplies the three focused recovery results. The BestFit batch must not be committed before those results are reconciled.
