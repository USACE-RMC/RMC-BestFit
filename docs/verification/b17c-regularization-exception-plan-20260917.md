# B17C regularization exception removal plan

Status: proposed only. No changes to Cholesky, matrix regularization, GMM, or bootstrap policy are included in the BFGS repair.

## Observed cause

The original seeded Example #1 run threw 429 caught Cholesky exceptions: 420 while constructing the GMM weighting matrix and nine while computing covariance. The repaired BFGS/Jacobian run throws 420: 411 during weighting updates and nine during covariance. The nine covariance exceptions occur while conditioning the **moment covariance S**, before constructing the sandwich bread or final parameter covariance.

`MatrixRegularization.CholeskyAccepts` constructs `CholeskyDecomposition` inside a try/catch to answer a yes/no question. `MakeSymmetricPositiveDefinite` calls this predicate on the symmetric input and on up to eight successively ridged candidates. Every rejected candidate throws, even though rejection is an expected branch of the regularization routine. The outer estimator does not throw for these events.

The reproducible inputs are in [residual-reproductions.json](b17c-repair-evidence-20260917/residual-reproductions.json), with all optimizer passes and the first nine rejected matrices for the affected realizations. Indices are zero based. In the repaired run:

| Realization | Caught exceptions | First raw S minimum eigenvalue | Minimum eigenvalue after terminal fallback |
|---|---:|---:|---:|
| 213 | 36: 27 weighting, 9 covariance | -9.0923310711e-6 | -8.5296228985e-6 |
| 402 | 384 weighting | -1.3360061875e-6 | -9.3860075849e-7 |

These matrices are indefinite, not just borderline positive definite. NumPy's independent symmetric eigensolver was used to diagnose them; it is not a proposed production dependency.

## Part 1: remove expected exceptions while preserving numerical behavior

1. **Freeze the decision and arithmetic contracts.** Add focused Numerics fixtures for the captured matrices, an ordinary positive-definite matrix, an exactly rank-deficient matrix, and positive-definite matrices with very different coordinate scales. Capture every accepted/rejected pivot decision and the returned regularized matrix before changing the implementation. Include values immediately around the existing pivot threshold.
2. **Share the existing Cholesky factorization core.** Extract its current arithmetic and pivot checks into a private/internal routine that returns success or a structured failure reason. Keep the same loop order, default relative tolerance, diagonal scaling, comparison operators, factor entries, and public constructor exceptions. The public constructor continues to throw the same failure for callers that request an invalid decomposition. Add an internal non-throwing factorization path for expected non-positive-definite candidates; add no public API.
3. **Use the internal path in `CholeskyAccepts`.** Replace exception-driven positive-definiteness probing with the returned success flag. Both `GetS` and `ComputeCovariance` already use `MakeSymmetricPositiveDefinite`, so no changes to their equations or calling logic are necessary. Do not introduce an eigenvalue precheck or duplicate the Cholesky arithmetic in the GMM class.
4. **Keep the current regularization decisions unchanged for this part.** Preserve symmetrization, the base ridge, its eight factors of ten, and the terminal fallback. Preserve parameter covariance status, diagnostics, optimizer fallback choices, parameter bounds, penalties, seeds, and sample generation. Invalid arguments and unexpected implementation errors must remain distinguishable from an expected rejected pivot.
5. **Prove parity.** The captured finite-input matrices must have identical accept/reject decisions, factors, selected ridges, and returned matrices. Public throwing constructors must retain their failure behavior. Trace first-chance exceptions while replaying both the original 429-exception fixtures and the repaired 420-exception fixtures: expected positive-definiteness probes should throw **zero** times. Compare all 1,000 fits, convergence flags, outer pass counts, covariances, and bootstrap diagnostics with the pre-change run; exception removal alone must not change them. Measure Debug and Release without tracing or debugger interception. Run Numerics' four-framework gates, BestFit's four fast suites, and scoped B17C covariance/example verification.

Acceptance for Part 1: zero exceptions from expected regularization probes and exact numerical parity. This removes repeated exception construction/unwinding; it does not establish the one-second bootstrap target or repair the GMM cycles.

## Part 2: terminal ridge defect requiring a separate numerical decision

There is a separate correctness gap in `MakeSymmetricPositiveDefinite`: after the eight rejected ridges, it returns a final matrix **without checking it**. With a positive trace, the last attempted ridge is `1e-3 * trace(S)/p`, whereas the unchecked terminal ridge is only `1e-4 * trace(S)/p`. The terminal candidate is therefore smaller than one that already failed.

For realization 213's captured matrix, that terminal ridge is 5.6270817266e-7 and leaves a negative eigenvalue. The fit later obtains a negative GMM quadratic and accepts location and scale at their lower bounds. This is present in the reproducible trace; it must not be described as a sound converged statistical fit merely because the optimizer and existing GMM convergence flags report success. Realization 402 also enters indefinite weighting and reaches 100 outer passes. Realization 8 cycles with positive-definite weights, so fixing this defect alone cannot be assumed to eliminate every stall.

The proposed follow-up is to continue the **existing trace-scaled ridge escalation monotonically**, verify the final candidate with the shared non-throwing Cholesky core, and use an explicit failure result if no permitted candidate is acceptable. Define the maximum permitted ridge and exhausted-regularization behavior before implementation. Do not silently choose an eigenvalue floor, nearest-positive-definite projection, larger default tolerance, or altered moment covariance formula.

This second part changes the matrices supplied to GMM and can change estimates, covariance, and realization acceptance. It needs Haden Smith's explicit numerical approval under the repository's algorithm-change rule. It is deliberately not bundled into the approved BFGS/Jacobian repair or Part 1's behavior-preserving exception removal.

## Source locations

- `C:\GIT\Numerics\Numerics\Mathematics\Linear Algebra\CholeskyDecomposition.cs`: factorization and scale-relative pivot checks.
- `C:\GIT\Numerics\Numerics\Mathematics\Linear Algebra\MatrixRegularization.cs`: `CholeskyAccepts` and `MakeSymmetricPositiveDefinite`.
- `C:\GIT\RMC-BestFit\src\RMC.BestFit\Estimation\GeneralizedMethodOfMoments.cs`: `GetS` and `ComputeCovariance`.
- Diagnostic trace: `C:\GIT\RMC-BestFit\src\RMC.BestFit\obj\b17c-regression-diagnostic-20260917\trace-repaired-serial.jsonl`.
