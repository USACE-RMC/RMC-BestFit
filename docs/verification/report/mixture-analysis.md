<!-- verification-status: publication-draft -->

# Mixture Analysis

BestFit exposes the physical $K$ weights at the model and EM boundary while sampling an identified $K-1$ vector and deriving the final weight. Tests compare pre-fit data likelihood with Numerics at `1e-10`, fitted engines at `1e-8`, ordered component recovery within 0.1, and Bayesian recovery with 15% relative tolerance plus a 0.15 floor. Bayesian cells also require $\widehat R<1.1$ and ESS greater than 100.

| Model | Parity/EM result | Bayesian result | Sample size |
|---|---:|---:|---:|
| Two-component Normal mixture | Passed, 1.426 s | Passed | 1,000 |
| Zero-inflated two-component Normal mixture | Passed, 1.852 s | Passed | 1,000 |
| Three-component Normal mixture | Passed, 3.247 s | Passed | 1,000 |

The three Bayesian runs used production DEMCzs defaults and completed in the observed range 173 to 456 seconds. The positive-hurdle cell additionally verifies the point mass at zero and the positive-conditioned continuous components. All six reported parity/recovery cells passed their declared acceptance criteria.
