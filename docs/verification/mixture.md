<!-- verification-status: draft -->

# Mixture Verification

## Scope

This report covers Phase 4 findings TR-006, TR-007, and TR-008:

- identified $K-1$ BestFit weights with a full-$K$ Numerics physical-weight API;
- a coherent point mass at zero with positive-conditioned components; and
- explicit EM failure for impossible rows.

It also records the pending EM-seeded, prior-aware Bayesian-initialization supplement.

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

### EM-seeded prior-aware Bayesian initialization

`MixtureModel.ExpectationMaximization` remains the documented approximate-MLE estimator and
retains Numerics parity. Parameter priors are deliberately not inserted into its responsibility
equations or public covariance result.

`MixtureAnalysis` now uses that EM estimate as the deterministic starting basin for a bounded
Nelder-Mead refinement of `MixtureModel.LogLikelihood`, the full posterior kernel. The refinement
therefore includes parameter priors, the normalized physical-simplex term, Jeffreys scale terms,
and an enabled mixture-quantile prior. Its bounded posterior Hessian supplies the initialization
covariance, including the existing initialization-only regularized Moore-Penrose fallback for
singular information.

The sampler population uses covariance multiplier 1.5, the configured sampler seed, and at most
20 replacement draws after an invalid proposal. Every candidate is scored by the full posterior,
and the best finite candidates seed the chains. A failed MAP refinement, posterior covariance, or
MAP population falls back to the original EM center and covariance while retaining full-posterior
fitness. A failed EM population resets the sampler completely to randomized initialization. No
DEMCzs sampling, proposal, output, interval, estimator, or seed default changes.

## Automated Evidence

### Numerics

Reachable Numerics commit **3e69a93** contains the focused correction and tests; it is the rebased equivalent of the original implementation object.

- Release build: zero warnings and zero errors for net481, net8.0, net9.0, and net10.0.
- Focused mixture suite: 20/20 passed, including the existing two 2D and one 3D recovery fixtures.
- Current .NET 10 Release suite: 2,024/2,024 passed. The focused correction was also validated across all configured target frameworks before subsequent tests were added.

### BestFit fast tests

Fast coverage includes $K-1$ counts and names, final-weight derivation, prior normalization,
proposal immutability, covariance dimensions, exact-only atom derivation, mixed likelihoods,
negative exact values, invalid positive mass, impossible rows for every observation family,
explicit MAP starting-value validation, deterministic posterior-population construction, stale
state reset, full-posterior fitness, and best-member chain seeding.

The changed `MixtureAnalysisTests` and `MaximumAPosterioriTests` groups pass 36/36 and 11/11.
The strict Debug solution build passes with zero warnings and errors, and Verification compiles
without executing a method. After aligning the independent Composite finite oracle with the
approved `XTransform.None` contract, the repository-wide fast gates pass Core 3,140/3,140,
UI 571/571, and App 428/428. These fast results are not promoted into a Bayesian
mixture-initialization passing claim.

## Focused Recovery Results

The original six methods generate $n=1000$ observations with seed 12345 through the production
`MixtureModel.GenerateRandomValues` method. Each was previously run separately through
`scripts/run-verification-test.ps1`. The three likelihood/EM parity results remain current because
the public EM algorithm is unchanged. The three Bayesian results predate the EM-seeded MAP change
and are retained only as historical evidence until explicitly authorized focused reruns occur.

| Exact method | Verification contract | Guarded duration | Status |
|---|---|---:|---|
| `NormalMixture2D_Recovery_Parity` | Two-component Normal generation, pre-fit likelihood parity, EM parity, and parent recovery | 1.426 s | Passed |
| `ZeroInflatedNormalMixture2D_Recovery_Parity` | Positive-hurdle generation, likelihood parity, EM parity, and parent recovery | 1.852 s | Passed |
| `NormalMixture3D_Recovery_Parity` | Three-component Normal generation, likelihood parity, EM parity, and parent recovery | 3.247 s | Passed |
| `NormalMixture2D_BayesianRecovery` | Two-component `MixtureAnalysis` posterior recovery and diagnostics | 9.589 s prior run | Ready - focused rerun |
| `ZeroInflatedNormalMixture2D_BayesianRecovery` | Positive-hurdle `MixtureAnalysis` recovery, atom check, and diagnostics | 21.346 s prior run | Ready - focused rerun |
| `NormalMixture3D_BayesianRecovery` | Three-component `MixtureAnalysis` posterior recovery and diagnostics | 12.761 s prior run | Ready - focused rerun |
| `MixturePriorAwareInitializationVerificationTests.InformativePrior_EmSeededMapInitialization_UsesFullPosterior` | EM starting basin, informative-prior displacement, full-posterior improvement/covariance, and deterministic `UserDefined` population fitness | Not run | Ready - focused run |

The parity methods give Numerics and BestFit the same BestFit-generated sample, compare pre-fit data log likelihoods at $10^{-10}$, compare fitted engines at $10^{-8}$, location-sort component labels, and retain absolute recovery tolerance 0.1.

The Bayesian recovery methods retain their declared DEMCzs configurations, seeds, recovery
tolerances, R-hat threshold, ESS threshold, and positive-hurdle atom gate. None was changed to
accommodate the new initializer. The new informative-prior method runs EM and MAP population
construction only; it does not run MCMC or alter a sampler default.

## Closeout State

TR-006, TR-007, and TR-008 remain complete: the public EM MLE, physical parameterization,
likelihoods, and Numerics parity contracts did not change. The EM-seeded MAP initializer compiles
with fast structural evidence, but its new informative-prior method and the three affected Bayesian
recovery methods remain `Ready - focused run`. No passing claim is made for the changed Bayesian
initialization path, and the complete Verification project was not run.
