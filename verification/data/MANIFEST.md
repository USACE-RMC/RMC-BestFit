# RMC.BestFit Verification Data Manifest

Every committed oracle file must be listed before a C# verification test consumes it.

| Family | File | Oracle | Version | Generator | Seed | Tolerance | SHA-256 | Status |
|---|---|---|---|---|---|---|---|---|
| Kappa Four zero-shape | [kappa-four-zero-shape.json](distribution-fitting/kappa-four-zero-shape.json) | Analytical CDF derivative and inverse | Numerics `8286646` to `3e058eb` | Focused exact-method runner | Deterministic | `1e-10` absolute | `7ac1932c70a93cab2b92b0e5c6a7f19538dbbf8321a45d4a08fd869c8d094d33` | Passed |
| Kappa Four finite shapes | [kappa-four-finite-shapes.json](distribution-fitting/kappa-four-finite-shapes.json) | Parameter-domain and support identities | Numerics `bc11849` | .NET 10 unit regression | Deterministic | `1e-10` CDF/quantile absolute | `5e905e9333b6a963ff21376561275238fe8488071a132fe8d50d371f13dd2c85` | Passed - rejected non-defect |
| Parameter-adjusted RMSE | [parameter-adjusted-rmse.json](distribution-fitting/parameter-adjusted-rmse.json) | Direct squared-residual calculation and permutation invariance | Numerics `3e058eb` to `24bf9f9` | Focused exact-method runner | Deterministic | `1e-12` absolute | `ca0bb1d328db61c0051ea6a8eb1c6efe4afdd7b8333f51daef8ebdf38bc73d17` | Passed |
| FittingAnalysis success state | [fitting-analysis-success-state.json](distribution-fitting/fitting-analysis-success-state.json) | Deterministic state regressions | BestFit current source | Core unit gate | Deterministic | Exact boolean/count assertions | `eb180fbf4228ed1a6bccc533b672e40e677e82d844cc716bc6841e46ab503686` | Passed |
| Log10-Normal closed-form fitting | [log10-normal-closed-form.json](distribution-fitting/log10-normal-closed-form.json) | Analytical transformed-normal MLE and Jacobian likelihood | BestFit `25f7b3c` + test source hash | Focused exact-method runner | Deterministic | `1e-5` parameters; `1e-8` likelihood | `8987ee548cc56ff34f091ff14d81c06d2e959cfe2a166ec598bb0bd0ff17a0d0` | Passed |
| Distribution fitting | Pending | Analytical formulas, SciPy, and lmomco | Pending locked restore | Pending | Deterministic | See `docs/verification/methodology.md` | Pending | Planned |
| Model comparison | Pending | R loo and BayesianTools; ArviZ secondary | Pending locked restore | Pending | Deterministic draws | See `docs/verification/methodology.md` | Pending | Planned |
| GMM and profiles | Pending | R gmm and bbmle | Pending locked restore | Pending | Deterministic | See `docs/verification/methodology.md` | Pending | Planned |

Generated files are reviewed and committed deliberately. Verification tests read committed artifacts and never invoke R, Python, package managers, or network services at test runtime.
