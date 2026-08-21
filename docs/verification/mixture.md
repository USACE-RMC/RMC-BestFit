<!-- verification-status: draft -->

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

The original six methods generate $n=1000$ observations with seed 12345 through the production
`MixtureModel.GenerateRandomValues` method. Each was previously run separately through
`scripts/run-verification-test.ps1`. The three likelihood/EM parity results remain current because
the public EM algorithm is unchanged. The three Bayesian results predate identified $K-1$ sampling
and are retained only as historical evidence until explicitly authorized focused reruns occur.

| Exact method | Verification contract | Guarded duration | Status |
|---|---|---:|---|
| `NormalMixture2D_Recovery_Parity` | Two-component Normal generation, pre-fit likelihood parity, EM parity, and parent recovery | 1.426 s | Passed |
| `ZeroInflatedNormalMixture2D_Recovery_Parity` | Positive-hurdle generation, likelihood parity, EM parity, and parent recovery | 1.852 s | Passed |
| `NormalMixture3D_Recovery_Parity` | Three-component Normal generation, likelihood parity, EM parity, and parent recovery | 3.247 s | Passed |
| `NormalMixture2D_BayesianRecovery` | Two-component `MixtureAnalysis` posterior recovery and diagnostics | 9.589 s prior run | Ready - focused rerun |
| `ZeroInflatedNormalMixture2D_BayesianRecovery` | Positive-hurdle `MixtureAnalysis` recovery, atom check, and diagnostics | 21.346 s prior run | Ready - focused rerun |
| `NormalMixture3D_BayesianRecovery` | Three-component `MixtureAnalysis` posterior recovery and diagnostics | 12.761 s prior run | Ready - focused rerun |

The parity methods give Numerics and BestFit the same BestFit-generated sample, compare pre-fit data log likelihoods at $10^{-10}$, compare fitted engines at $10^{-8}$, location-sort component labels, and retain absolute recovery tolerance 0.1.

The Bayesian recovery methods retain their declared DEMCzs configurations, seeds, recovery
tolerances, R-hat threshold, ESS threshold, and positive-hurdle atom gate. None was changed to
accommodate the identified sampler. No Verification method was run for this correction.

## Closeout State

TR-007 and TR-008 remain complete because the positive-hurdle and impossible-row contracts did not
change. TR-006 now distinguishes the full-$K$ public/physical boundary from identified $K-1$
posterior storage. The three Bayesian recovery methods remain `Ready - focused rerun`; no passing
claim is made for the changed Bayesian parameterization, and the complete Verification project was
not run.
