<!-- verification-status: publication-draft -->

# Mixture Analysis

BestFit exposes physical full-$K$ weights at the model and EM boundary while sampling an identified $K-1$ vector and deriving the final weight. Chunk 10A separates same-ecosystem implementation parity from scientific evidence. Components are preidentified by ascending Normal mean. EM recovery requires absolute standardized parent error at most 1.96 from its responsibility-count weight covariance and observed-likelihood component covariance. Bayesian recovery reconstructs full-$K$ weights per draw and requires every ordered physical truth inside its central 95% posterior interval, with $\widehat R<1.10$ and ESS at least 100 for every stored coordinate.

| Model | EM recovery result | Bayesian result | Sample size |
|---|---:|---:|---:|
| Two-component Normal mixture | Passed, 0.373 s | Passed, 1:47.770 | 1,000 |
| Zero-inflated two-component Normal mixture | Passed, 0.744 s | Passed, 4:04.938 | 1,000 |
| Three-component Normal mixture | Passed, 1.371 s | Passed, 4:05.235 | 1,000 |

The frozen ordinary two-Normal artifact uses Python 3.12.13, NumPy 2.5.2, SciPy 1.18.1, and scikit-learn 1.9.0 on a NumPy-PCG64 seed-12345 sample. Its exact likelihood/CDF and diagonal-covariance fitted coordinates pass the external overlap method. scikit-learn does not implement the positive-conditioned zero-hurdle law, so that behavior has no external-package claim. All three Bayesian runs pass under unchanged production defaults. Earlier post-edit runs incorrectly declared unsorted retained arrays as sorted to the percentile routine; those false failures are discarded, and no seed, prior, sampler, parent, or acceptance rule was tuned.
