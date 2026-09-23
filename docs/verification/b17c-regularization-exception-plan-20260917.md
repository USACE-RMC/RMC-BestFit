# B17C regularization exception removal plan

Status: both parts approved and implemented on 2026-09-17 in Numerics commit `189a5973559cade7725f33cef4ded8fce4e16ccc`. See the [implementation results and remaining BFGS/GMM diagnosis](b17c-cholesky-and-bfgs-diagnosis-20260917.md). GMM and bootstrap policy were not changed.

## Observed cause

The original seeded Example #1 run threw 429 caught Cholesky exceptions: 420 while constructing the GMM weighting matrix and nine while computing covariance. The repaired BFGS/Jacobian run throws 420: 411 during weighting updates and nine during covariance. The nine covariance exceptions occur while conditioning the **moment covariance S**, before constructing the sandwich bread or final parameter covariance.

`MatrixRegularization.CholeskyAccepts` constructs `CholeskyDecomposition` inside a try/catch to answer a yes/no question. `MakeSymmetricPositiveDefinite` calls this predicate on the symmetric input and on up to eight successively ridged candidates. Every rejected candidate throws, even though rejection is an expected branch of the regularization routine. The outer estimator does not throw for these events.

The reproducible inputs are in [residual-reproductions.json](b17c-repair-evidence-20260917/residual-reproductions.json), with all optimizer passes and the first nine rejected matrices for the affected realizations. Indices are zero based. In the repaired run:

| Realization | Caught exceptions | First raw S minimum eigenvalue | Minimum eigenvalue with old terminal ridge applied to that raw S |
|---|---:|---:|---:|
| 213 | 36: 27 weighting, 9 covariance | -9.0923310711e-6 | -8.5296228985e-6 |
| 402 | 384 weighting | -1.3360061875e-6 | -9.3860075849e-7 |

These raw matrices are indefinite, not just borderline positive definite. NumPy's independent symmetric eigensolver was used to diagnose them; it is not a production dependency. The last column is a calculation on the first raw S, not proof that the terminal branch was reached: realization 213 actually returned unchecked indefinite matrices, whereas realization 402's existing ladder accepted a positive-definite candidate before the terminal branch. This distinction was established by tracing every candidate and return during implementation.

## Part 1: remove expected exceptions while preserving numerical behavior

1. **Freeze the decision and arithmetic contracts.** Add focused Numerics fixtures for the captured matrices, an ordinary positive-definite matrix, an exactly rank-deficient matrix, and positive-definite matrices with very different coordinate scales. Capture every accepted/rejected pivot decision and the returned regularized matrix before changing the implementation. Include values immediately around the existing pivot threshold.
2. **Share the existing Cholesky factorization core.** Extract its current arithmetic and pivot checks into a private/internal routine that returns success or a structured failure reason. Keep the same loop order, default relative tolerance, diagonal scaling, comparison operators, factor entries, and public constructor exceptions. The public constructor continues to throw the same failure for callers that request an invalid decomposition. Add an internal non-throwing factorization path for expected non-positive-definite candidates; add no public API.
3. **Use the internal path in `CholeskyAccepts`.** Replace exception-driven positive-definiteness probing with the returned success flag. Both `GetS` and `ComputeCovariance` already use `MakeSymmetricPositiveDefinite`, so no changes to their equations or calling logic are necessary. Do not introduce an eigenvalue precheck or duplicate the Cholesky arithmetic in the GMM class.
4. **Keep the current regularization decisions unchanged for this part.** Preserve symmetrization, the base ridge, its eight factors of ten, and the terminal fallback. Preserve parameter covariance status, diagnostics, optimizer fallback choices, parameter bounds, penalties, seeds, and sample generation. Invalid arguments and unexpected implementation errors must remain distinguishable from an expected rejected pivot.
5. **Prove parity.** The captured finite-input matrices must have identical accept/reject decisions, factors, selected ridges, and returned matrices. Public throwing constructors must retain their failure behavior. Trace first-chance exceptions while replaying both the original 429-exception fixtures and the repaired 420-exception fixtures: expected positive-definiteness probes should throw **zero** times. Compare all 1,000 fits, convergence flags, outer pass counts, covariances, and bootstrap diagnostics with the pre-change run; exception removal alone must not change them. Measure Debug and Release without tracing or debugger interception. Run Numerics' four-framework gates, BestFit's four fast suites, and scoped B17C covariance/example verification.

Acceptance for Part 1: zero exceptions from expected regularization probes and exact numerical parity. This removes repeated exception construction/unwinding; it does not establish the one-second bootstrap target or repair the GMM cycles.

## Part 2: approved terminal ridge repair

Before the Part 2 repair, `MakeSymmetricPositiveDefinite` had a separate correctness gap: after the eight rejected ridges, it returned a final matrix **without checking it**. With a positive trace, the last attempted ridge was `1e-3 * trace(S)/p`, whereas the unchecked terminal ridge was only `1e-4 * trace(S)/p`. The terminal candidate was therefore smaller than one that already failed.

For realization 213's captured matrix, that terminal ridge is 5.6270817266e-7 and leaves a negative eigenvalue. The fit later obtains a negative GMM quadratic and accepts location and scale at their lower bounds. This is present in the reproducible trace; it must not be described as a sound converged statistical fit merely because the optimizer and existing GMM convergence flags report success. Realization 402 needs regularization of indefinite raw S but receives accepted positive-definite matrices and reaches 100 outer passes. Realization 8 also cycles with positive-definite weights, so fixing this defect alone cannot eliminate every stall.

The implemented repair continues the **existing trace-scaled ridge escalation monotonically** and verifies every candidate with the shared non-throwing Cholesky core. The first eight candidates retain their exact arithmetic. Subsequent candidates multiply the last ridge by ten. The maximum permitted ridge is the last finite decade value for which the candidate diagonals remain finite; exhaustion throws an explicit `InvalidOperationException`. Non-finite input, symmetrization overflow, and unrepresentable ridge scales fail explicitly. No eigenvalue floor, nearest-positive-definite projection, larger default tolerance, or altered moment covariance formula was introduced.

This second part changes the matrices supplied to GMM and can change estimates, covariance, and realization acceptance. Haden Smith authorized it with "Ok proceed with the Cholesky an ridge fixes." Part 1 was validated separately before applying this numerical correction.

## Completed validation and remaining limitation

Part 1 removed all 429 original-run exceptions and all 420 post-BFGS-repair exceptions with exact numerical parity. The latter comparison covered 22,180 candidate decisions/factors, 21,764 returned matrices, and all 1,000 fits and covariances.

The final ridge repair changed only realization 213's fit and covariance; the other 999 were unchanged. All 21,857 returned matrices had positive independently computed eigenvalues, all accepted objectives were nonnegative, and the run threw zero exceptions. The newly corrected realization now exposes a 100-pass stall instead of the previous invalid apparent convergence: 51 of 1,000 fits reach the limit. The [diagnosis](b17c-cholesky-and-bfgs-diagnosis-20260917.md) separates those outer-loop failures from BFGS line-search roundoff and verifies the penalty derivative independently.

Numerics' 11,537 tests across four frameworks, BestFit's 5,021 mandatory fast tests, 13 scoped B17C covariance checks, seven scoped example checks, and the exact one-result Example #1 verification all passed. The full B17C convergence target remains unmet.

## Source locations

- `C:\GIT\Numerics\Numerics\Mathematics\Linear Algebra\CholeskyDecomposition.cs`: factorization and scale-relative pivot checks.
- `C:\GIT\Numerics\Numerics\Mathematics\Linear Algebra\MatrixRegularization.cs`: `CholeskyAccepts` and `MakeSymmetricPositiveDefinite`.
- `C:\GIT\RMC-BestFit\src\RMC.BestFit\Estimation\GeneralizedMethodOfMoments.cs`: `GetS` and `ComputeCovariance`.
- Diagnostic trace: `C:\GIT\RMC-BestFit\src\RMC.BestFit\obj\b17c-regression-diagnostic-20260917\trace-repaired-serial.jsonl`.
