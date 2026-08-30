<!-- verification-status: finalized -->

# Mixture Verification

## Scope

This report covers Phase 4 findings TR-006, TR-007, and TR-008:

- the full-$K$ public model and identified $K-1$ BestFit sampler parameterization;
- a coherent point mass at zero with positive-conditioned components; and
- explicit EM failure for impossible rows.

It also records the deterministic contracts for EM-seeded Bayesian initialization.

BestFit model configuration, configured priors, public likelihood methods, EM output, and model XML retain all $K$ weights. New mixture `MCMCResults` store the identified $K-1$ sampled weights. Existing full-$K$ posterior results open as-is without migration.

## Implemented Contracts

### TR-006 — weights and proposal purity

Numerics accepts and returns all $K$ weights and continues to copy and normalize its caller vector:

$$
\widetilde w_k=m\dfrac{w_k}{\sum_{j=1}^{K}w_j},
\qquad m=1\ \text{or}\ 1-\pi_0.
$$

BestFit's public model and EM boundary is also full $K$. Its sampler target stores only $w_1,\ldots,w_{K-1}$ and derives

$$
w_K=m-\sum_{k=1}^{K-1}w_k.
$$

Negative or nonfinite sampled or derived weights receive log target $-\infty$; no proposal is clamped, normalized, or mutated. The expanded full-$K$ vector is passed to the established likelihood and prior methods, so every configured weight prior remains active. New `MCMCResults` persist $K-1$ weights directly. Scientific post-processing reconstructs the physical full-$K$ vector locally, while parameter-indexed sampler diagnostics expose only stored coordinates. EM continues to return all $K$ weights.

### TR-007 — positive hurdle

Numerics now has no negative support when zero inflation is enabled, reports the CDF jump and atom at zero, conditions each continuous component on $X>0$, and uses that law for density, log density, quantiles, and simulation. BestFit derives $\pi_0$ from exact values equal to zero divided by all exact annual records. Mixed observation likelihoods add the atom separately and condition continuous contributions on positive support.

### TR-008 — impossible rows

Numerics and BestFit EM throw **InvalidOperationException** with row index and value when a required row probability is zero or nonfinite. Fast BestFit coverage includes exact, uncertain, interval, and threshold rows. Negative exact observations are rejected in positive-hurdle models.

### EM-seeded identified Bayesian initialization

`MixtureModel.ExpectationMaximization` remains the documented approximate-MLE estimator and
retains Numerics parity. Parameter priors are deliberately not inserted into its responsibility
equations or public covariance result.

`MixtureAnalysis` removes the derived $w_K$ coordinate from the EM center and uses the leading
identified covariance block directly. The public EM weight covariance is responsibility-based:

$$
\boldsymbol\Sigma_w
=N_{\mathrm{eff}}^{-1}
\left[m\operatorname{diag}(\mathbf w)-\mathbf w\mathbf w^{\mathsf T}\right].
$$

It has the required negative off-diagonal entries and zero row sums. There is no mixture-specific
MAP refinement, posterior Hessian, chain-copying step, or post-hoc result conversion.

The sampler population uses covariance multiplier 1.5, the configured sampler seed, and at most
20 replacement draws after an invalid proposal. Every candidate is expanded to full $K$ and scored
by the complete posterior, including the derived weight's configured prior, and the best finite
candidates seed the chains. A failed EM population resets the sampler completely to randomized initialization. No
DEMCzs sampling, proposal, output, interval, estimator, or seed default changes.

## Automated Evidence

### Numerics

Reachable Numerics commit **3e69a93** contains the focused correction and tests; it is the rebased equivalent of the original implementation object.

- Release build: zero warnings and zero errors for net481, net8.0, net9.0, and net10.0.
- Focused mixture suite: 20/20 passed, including the existing two 2D and one 3D recovery fixtures.
- Current .NET 10 Release suite: 2,024/2,024 passed. The focused correction was also validated across all configured target frameworks before subsequent tests were added.

### BestFit fast tests

Fast coverage includes full-$K$ public counts and prior XML; $K-1$ sampler dimensions, names,
prior evaluation, infeasible rejection, and posterior persistence; proposal immutability;
responsibility-based covariance; sampled-coordinate diagnostics; legacy full-$K$ results; local
physical expansion for parameter tables, diagnostics, and frequency curves; exact-only atom
derivation; mixed likelihoods; negative exact values; invalid positive mass; impossible rows for
every observation family; deterministic EM-population construction; stale-state reset;
full-posterior fitness; and best-member chain seeding.

The mixture-focused Core batch, the complete Core binary gate (including public-API
compatibility), the strict Debug solution/XML compilation, and the UI, App, and API suites
pass. The focused Numerics mixture class passes on .NET 10. Historical pre-parameterization
Bayesian recovery pass counts are not promoted into a current passing claim.

## Focused Recovery Results

### Chunk 10A parameterization and identification crosswalk

All six current recovery identities use exactly N=1000 observations from production generation
seed 12345. Ordinary two-component Bayesian estimation uses seed 22345, positive-hurdle
two-component estimation uses 32345, and ordinary three-component estimation uses 42345. These
seeds and the production EM/DEMCzs settings are fixed before any result is observed.

| Fixture | Physical law and expected counts | Fitted coordinates | Identification and oracle |
|---|---|---|---|
| Ordinary two-Normal | Weights (0.3, 0.7); Normal (mean 0, sd 1) and Normal (mean 3, sd 0.1); expected component counts (300, 700) | Public/EM: both physical weights, then each component mean and standard deviation. Sampler: first weight only, then the component parameters; final weight is `1 - w1`. | Components are preidentified by strictly ascending means. EM parent inclusion uses the responsibility-count weight covariance and observed-likelihood Normal-parameter covariance. Bayesian full-K weights are reconstructed and components sorted by mean per draw before central-95% intervals. |
| Ordinary three-Normal | Weights (0.2, 0.3, 0.5); Normal (0, 1), Normal (3, 0.1), Normal (5, 2); expected counts (200, 300, 500) | Public/EM: all three physical weights, then mean/sd pairs. Sampler: first two weights, then parameter pairs; `w3 = 1 - w1 - w2`. | Same ascending-mean rule and uncertainty sources. The component likelihood information, rather than N=1000 assigned independently to every coordinate, supplies parameter uncertainty. |
| Positive-hurdle two-Normal | Atom `pi0=0.1`; continuous physical weights (0.3, 0.6); positive-conditioned Normal (3, 0.1) and Normal (5, 2); expected counts: atom 100, components 300 and 600 | Public/EM: both continuous weights, then mean/sd pairs. The fitted model fixes the atom at its observed sample fraction, so the fitted parent crosswalk keeps `w1=0.3` and closes `w2=(1-pi0_observed)-w1`. Sampler: first continuous weight, then parameter pairs; the same final-weight reconstruction is applied per draw. The atom is not sampled. | The atom uses its N=1000 binomial 95% standardized-error rule. Continuous weights use the effective positive responsibility count; parameter intervals use the positive-conditioned likelihood information. Components remain ordered by mean. |

The frequentist recovery identities no longer treat BestFit/Numerics agreement as scientific
evidence. Their primary evidence is generated-parent inclusion using the EM covariance described
above. The Bayesian identities require every ordered physical weight, mean, and standard deviation
truth inside its central 95% posterior interval; every stored sampler coordinate must also have
R-hat below 1.10 and ESS at least 100. Prior support, full-K/K-1 reconstruction, ascending-mean
ordering, and parent likelihood discrimination against the fresh collapsed/default coordinates are
checked before estimation. Predictive CDF ordinates are reserved for a failed or demonstrably weak
component identification; they are not substituted after seeing a miss.

For independent overlap, the frozen ordinary two-Normal artifact is generated with Python 3.12.13,
NumPy 2.5.2, SciPy 1.18.1, and scikit-learn 1.9.0. It records a NumPy-PCG64 seed-12345 N=1000 sample,
the exact parent likelihood, the scikit-learn diagonal-covariance fit, and fixed parent/fitted CDF
ordinates. scikit-learn does not implement BestFit's positive-conditioned zero-hurdle law, so no
external-package parity is claimed for that fixture; its exact law is checked directly by the
generated-parent likelihood and recovery construction.

### Chunk 10A exact results - 30 August 2026

Every completed method below ran separately through the guarded runner and produced exactly one
TRX result. The method names ending in `_Parity` are retained for identity continuity, but their
scientific oracle is now generated-parent recovery; same-ecosystem parity was removed from their
acceptance path.

| Exact method | Verification contract | Guarded duration | Status |
|---|---|---:|---|
| `NormalMixture2D_Recovery_Parity` | Two-component EM parent inclusion from responsibility/observed-likelihood covariance | 0.373 s | Passed |
| `ZeroInflatedNormalMixture2D_Recovery_Parity` | Separate atom plus positive-hurdle EM parent inclusion | 0.744 s | Passed |
| `NormalMixture3D_Recovery_Parity` | Three-component EM parent inclusion | 1.371 s | Passed |
| `NormalMixture2D_BayesianRecovery` | Reconstructed full-K central-95% posterior inclusion and diagnostics | 1:47.770 | Passed |
| `ZeroInflatedNormalMixture2D_BayesianRecovery` | Atom plus reconstructed positive-mass central-95% posterior inclusion and diagnostics | 4:04.938 | Passed |
| `NormalMixture3D_BayesianRecovery` | Reconstructed full-K central-95% posterior inclusion and diagnostics | 4:05.235 | Passed |
| `MixtureExternalPackageOracleTests.OrdinaryNormalMixture2D_MatchesScikitLearnArtifact` | Frozen scikit-learn fit, independent likelihood, and CDF overlap | 0.242 s | Passed |

The external-package artifact method agrees with its frozen likelihood within $10^{-9}$, CDF
ordinates within $10^{-12}$, and fitted physical coordinates within $10^{-4}$ scaled. The
positive-hurdle law is intentionally outside the scikit-learn parity claim. No seed, prior, sampler,
estimator, law, tolerance, or production default was changed. The first three post-edit Bayesian
runs passed unsorted posterior arrays to `Statistics.Percentile` while declaring them sorted; their
two false exclusions and one nonfinite interval were discarded as test-oracle evidence. Sorting the
same retained arrays before central-95% evaluation produced the passing exact TRXs above.

## Closeout State

TR-007 and TR-008 remain complete because the positive-hurdle and impossible-row contracts did not
change. All six current recovery identities and the independent scikit-learn identity are verified
under the common acceptance rule. The complete Verification project was not run.
