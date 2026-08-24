<!-- verification-status: publication-draft -->

# Point-Process Analysis

The independent fixture generates Poisson event counts through exponential inter-arrival times and generalized-Pareto marks through an analytical inverse. It separately checks empirical event exposure, fitted threshold intensity, seasonal block assignment, calendar-year versus October-water-year coordinates, and mixed-observation likelihood contributions. Recovery uses 1,000 observations and production DEMCzs defaults.

| Verification cell | Evidence | Duration | Result |
|---|---|---:|---:|
| Calendar-year prior placement | Deterministic occurrence-histogram oracle | 0.617 s | Passed |
| Water-year prior placement | Rotated occurrence-histogram oracle | 0.722 s | Passed |
| Calendar-year seasonal recovery | Generating parent and both changepoints | 35.690 s | Passed |
| Water-year seasonal recovery | Same parent under shifted block origin | 33.897 s | Passed |
| Nonseasonal production recovery | Generating parent | 8.450 s | Passed |
| Seasonal production recovery | Generating parent and both changepoints | 36.309 s | Passed |
| Nonseasonal simulation | Poisson rate and conditional-tail oracle | 0.293 s | Passed |
| Seasonal simulation | Rates, assignment, dates, and conditional tails | 0.334 s | Passed |
| Nonseasonal mixed likelihood | Independent density/CDF calculation | 0.294 s | Passed |
| Seasonal mixed likelihood | Independent annual-maximum calculation | 0.291 s | Passed |

The paired calendar and water-year fixtures hold changepoints at days 170 and 350 and change only the block-year origin. Generated magnitudes and block days agree exactly; dates differ by 92 days; the parent likelihoods agree within `1e-10`. The mixed-likelihood comparison uses `2e-7` tolerance. All ten cells passed without changing the production likelihood, sampler settings, seed, or acceptance rule.
