<!-- verification-status: publication-draft -->

# Point-Process Analysis

## Claim, model, and oracle

The independent fixture generates Poisson event counts through exponential inter-arrival times and generalized-Pareto marks through an analytical inverse. It separately checks empirical event exposure, fitted threshold intensity, seasonal block assignment, calendar-year versus October-water-year coordinates, and mixed-observation likelihood contributions. Recovery uses 1,000 observations and production DEMCzs defaults.

## Recovery and deterministic results

| Verification cell | Evidence | Result |
|---|---|---:|
| Calendar-year prior placement | Deterministic occurrence-histogram oracle | Passed |
| Water-year prior placement | Rotated occurrence-histogram oracle | Passed |
| Calendar-year seasonal recovery | Generating parent and both changepoints | Passed |
| Water-year seasonal recovery | Same parent under shifted block origin | Passed |
| Nonseasonal production recovery | Generating parent | Passed |
| Seasonal production recovery | Generating parent and both changepoints | Passed |
| Nonseasonal simulation | Poisson rate and conditional-tail oracle | Passed |
| Seasonal simulation | Rates, assignment, dates, and conditional tails | Passed |
| Nonseasonal mixed likelihood | Independent density/CDF calculation | Passed |
| Seasonal mixed likelihood | Independent annual-maximum calculation | Passed |

The paired calendar and water-year fixtures hold changepoints at days 170 and 350 and change only the block-year origin. Generated magnitudes and block days agree exactly; dates differ by 92 days; the parent likelihoods agree within `1e-10`. The mixed-likelihood comparison uses `2e-7` tolerance. The six source-affected recovery and simulation identities received fresh exact one-result passing TRXs on 2 September 2026 without changing the production likelihood, sampler settings, seed, or acceptance rule.

## Limitations and provenance

The recovery claims are conditional on the declared threshold, exposure definition, GPD parameterization,
season boundaries, and generated event process. Simulation moment checks do not replace parameter recovery,
and completion or finite output alone is not counted as scientific evidence. Exact identities, seeds, sample
sizes, and acceptance rules are recorded in the verification catalog.
