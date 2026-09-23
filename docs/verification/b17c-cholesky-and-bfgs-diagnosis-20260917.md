# B17C Cholesky repair and remaining BFGS/GMM diagnosis

The active Example #1 penalty derivative is correct. The remaining failures have two distinct causes: rounding in objective differences prevents BFGS from completing some near-minimum line searches, and the existing outer weighting iteration is unstable or slowly alternating for the realizations that reach 100 passes. An accurately solved inner problem does not remove the outer instability.

The approved Cholesky and ridge repairs are implemented in Numerics commit `189a5973559cade7725f33cef4ded8fce4e16ccc`. This work does not change BFGS, Nelder-Mead, GMM equations, weighting formulas, penalties, convergence rules, tolerances, bounds, seeds, GMM/optimizer iteration or evaluation limits, or fallback policy. The earlier BFGS/Jacobian repair is Numerics `156204fa2e6257616cca197175b95c363939f100` and BestFit `4732d5cc72a34946ccdbc6851a28f2a18831351b`.

## Reproduction and result

The test uses the preserved XML snapshots of Example #1 - BCB from `examples/4-univariate-distribution-analysis/2-bulletin-17C-analysis/1-bulletin17C-examples/bulletin-17c-examples.bestfit`, 68 systematic observations, 1,000 realizations, and seed `12345`. Realization indices below are zero based. The user's open example database changed during the session; the replay consistently uses the previously captured snapshots in [b17c-repair-evidence-20260917](b17c-repair-evidence-20260917/).

| Measurement | After earlier BFGS/Jacobian repair | After Cholesky/ridge repair |
|---|---:|---:|
| Parent passes / convergence | 6 / true | 6 / true |
| Parent objective evaluations | 55 | 55 |
| Bootstrap objective evaluations | 683,957 | 696,401 |
| BFGS fallbacks | 202 | 203 |
| Bootstrap fits reaching 100 passes | 50 | 51 |
| Bootstrap convergence flags true | 950 | 949 |
| Caught Cholesky exceptions | 420 | 0 |
| Median outer passes per realization | 12 | 12 |

The final parent estimate is `[3.328586975163675, 0.14059047532656307, 0.42233732191184603]`. Only realization 213's fit and covariance changed with the ridge repair. Its old apparent convergence used an indefinite returned matrix and a negative objective. Its corrected run now exposes an existing outer-loop stall. The additional capped fit is not a newly valid fit that the repair spoiled.

Uninstrumented runs against the actual assemblies used one cold run followed by three runs with fresh analysis objects. Release warm timings were 712.8611, 686.9388, and 845.7474 ms, with median **0.713 seconds**; the cold run took 1.736 seconds. Debug's warm median was **1.862 seconds**. These measurements exclude tracing overhead. They meet the approximate warm Release timing target on this machine, but **do not establish the requested 3-5-pass behavior**: only 50 realizations finished within five passes and 51 still reached 100.

## Why BFGS reports failure

All 203 failed BFGS solves were traced at the last accepted point and through their failed line searches, including frozen weighting matrices, gradients, moment vectors, Jacobians, trial coordinates, objectives, slopes, and brackets. All failure points were strictly inside their bounds, with finite gradients and positive local Hessian eigenvalues in an independent calculation.

The residual infinity norms ranged from `1.0020289543e-8` to `1.0431209119e-7`, just above the unchanged `1e-8` gradient convergence threshold. BFGS therefore correctly declines to report gradient convergence. Its strict Armijo comparison then treats rounding in the function evaluation as meaningful improvement or deterioration; the bracket can exclude a useful step before the trial gradient is evaluated.

For realization 8, outer pass 14, the current objective is `0.09859018478072878` and the gradient is approximately `[3.68e-10, -1.78e-8, -7.48e-10]`. Evaluating the same objective with 70-digit Decimal arithmetic on the same binary input data, parameters, and weights gives:

| Full BFGS trial step | Objective change |
|---|---:|
| Independently evaluated change | `-2.5773737272e-19` |
| Production double-precision change | `+2.7755575616e-17` |
| Spacing between adjacent doubles at this objective | `1.3877787808e-17` |

The true decrease is about 0.019 of one representable objective increment, while the computed value increases by two increments. Shrinking this bracket, or restarting once with steepest descent, cannot reliably distinguish such differences using the same rounded objective values.

Across all failures, 202 had a genuinely decreasing trial reported as an increase. The remaining case, realization 985, included a true increase of `1.3116591497e-18` reported as a decrease of `-6.9388939039e-17`, misleading the bracket in the other direction. The largest observed error in an objective difference was `8.3277899264e-17`.

This is a precision limit in the interaction between objective evaluation and line-search comparisons. It is not evidence of a wrong penalty derivative, an active bound, an indefinite local Hessian, or gross BFGS divergence. Curvature scaling and a fresh identity metric on each outer pass also matter: 76 of the 203 failures had a predicted full Newton improvement below one objective increment, but other cases had larger available Newton improvements. Thus not every failure is unavoidable for every possible search direction, and these observations do not establish that the current BFGS implementation is optimal.

The cost is amplified by existing GMM fallback policy: one BFGS failure switches the remainder of that realization's outer passes to Nelder-Mead. The 203 failures are returned statuses, not thrown exceptions. That policy remains unchanged.

The relevant existing code is `BFGS.LineSearch` / `Zoom` and `GeneralizedMethodOfMoments.MinimizeWithFallback`. The reference comparison uses the pinned [SciPy 1.16.2 BFGS implementation](https://github.com/scipy/scipy/blob/v1.16.2/scipy/optimize/_optimize.py) and its [line-search implementation](https://github.com/scipy/scipy/blob/v1.16.2/scipy/optimize/_linesearch.py); the experimental model results are recorded in the local evidence package.

## Independent penalty and moment derivative check

For this fixture there are no parameter links and only the real-scale skew penalty is enabled. Its bootstrap center $c$ is drawn once when the penalty closure is created, not during numerical differentiation. With $n=68$ and $\mathrm{MSE}=0.078$,

$$
P(\gamma)=\frac{(\gamma-c)^2}{2n\,\mathrm{MSE}},\qquad
\nabla P=\left(0,0,\frac{\gamma-c}{5.304}\right),\qquad
P_{\gamma\gamma}=0.1885369532428356.
$$

Write $y_i=\log_{10}(q_i)$, $a_r=n^{-1}\sum_i(y_i-\mu)^r$, $b_2=n/(n-1)$, and $b_3=n^2/((n-1)(n-2))$. The systematic moment vector and its Jacobian are

$$
g=\begin{pmatrix}a_1\\b_2a_2-\sigma^2\\b_3a_3-\gamma\sigma^3\end{pmatrix},\qquad
J=\begin{pmatrix}
-1&0&0\\
-2b_2a_1&-2\sigma&0\\
-3b_3a_2&-3\gamma\sigma^2&-\sigma^3
\end{pmatrix}.
$$

For fixed $W$, the active objective is $Q=\tfrac12g^TWg+P$ and its gradient is $J^TWg+\nabla P$. At all 203 captured failures, independent formulas agreed with the supplied moment Jacobian within `4.17e-17`, the numerical penalty gradient within `5.62e-14`, and the complete gradient within `9.42e-14`. These errors are far smaller than the failed gradient convergence residuals.

There is no missing logarithm factor, Bessel factor, sign, sample-size factor, or factor of two in this active penalized path. A derivative of $W$ does not belong in this inner gradient: GMM deliberately freezes $W$ during each minimization.

## Why the outer GMM passes do not converge

The existing iteration computes a new weighting matrix from the preceding fit, then solves another fixed-weight problem. Write $\widetilde S(a)$ for the covariance after the existing regularization. Locally, the minimizing branch followed by an accurate inner solve satisfies

$$
T(a)\in\mathop{\operatorname{local\,arg\,min}}_x\left\{\tfrac12g(x)^T\widetilde S(a)^{-1}g(x)+P(x)\right\}.
$$

The penalty generally leaves a nonzero moment residual. Updating $S$ therefore changes the compromise between the sample moments and the penalty, even with an exact inner minimizer. In the problematic bootstrap samples, the randomized penalty center and sample skew differ substantially. For example, realization 8 has sample skew `1.0177660203` and penalty center `-0.2967029622`; the parent has sample skew `0.3966261241` and penalty center `0.44`.

An independent calculation found an interior stationary fixed point for every realization and differentiated this local minimizing branch. At all those points the raw covariance and fixed-weight Hessian are positive definite; regularization is inactive in their neighborhoods. Thus $\widetilde S=S$ there. This establishes local behavior, not uniqueness of the fixed point or a globally minimizing branch. If $H$ is the fixed-weight inner Hessian and

$$
B_k=-J^TW\frac{\partial S}{\partial a_k}Wg,
\qquad DT=-H^{-1}B,
$$

then eigenvalues of $DT$ describe how small changes propagate between outer passes. A negative eigenvalue reverses the change each pass; magnitude above one amplifies it.

| Case | Dominant eigenvalue of existing outer iteration | Interpretation |
|---|---:|---|
| Parent | `+0.0367786701` | Rapid contraction |
| Realization 8 | `-2.9714213966` | Alternating amplification |
| Realization 213 | `-3.8188288106` | Alternating amplification |
| Realization 402 | `-3.6990605263` | Alternating amplification |
| Realization 175 | `-0.9862679121` | Very slow alternating contraction |

Of the 51 fits capped at 100 passes, **46 have locally unstable fixed points**. The other five have negative dominant eigenvalues with magnitudes from `0.94825` to `0.98627`: they converge too slowly for the existing limit. All 51 still have parameter changes larger than `1e-4` at the last pass, so these are not just last-bit convergence noise. Twenty-one are already close to a two-pass cycle; the others exhibit wider cycling or slow alternating contraction.

This calculation is not based solely on differentiating code: separate inner stationary solves at perturbations of `+/-1e-6` confirmed the dominant gains for the parent and realizations 8, 175, 213, and 402. The largest directional error was below `7e-10`, with inner gradient residuals below `1.1e-14`. Root solving is used only as an independent diagnostic; no replacement estimator or GMM algorithm is shipped.

Pinned SciPy 1.16.2 BFGS with the analytical penalty gradient also reaches 100 passes for realization 8 under the same outer weighting and convergence rules. Its last two inner solves report success with gradient norms `2.36e-9` and `2.29e-10`, and positive-definite weights, yet alternate between approximately:

```
pass  99: [3.2929194895, 0.1323207316, -0.1606971456]
pass 100: [3.3181824387, 0.1732220512,  0.5576729412]
```

That directly separates the outer failure from Numerics' BFGS line-search failures. Improving inner precision alone cannot make this unchanged outer map contract or guarantee 3-5 passes.

The current systematic covariance formula is $S=C_{\mathrm{model}}-gg^T$. For positive-definite $C_{\mathrm{model}}$, let $t=g^TC_{\mathrm{model}}^{-1}g$. Then $S$ is positive definite exactly when $t<1$, and $g^TS^{-1}g=t/(1-t)$. This explains sensitivity as the moment mismatch grows and why raw indefinite matrices occur. It does not make every outer cycle a regularization defect: realization 8 cycles with positive-definite weights, and realization 402 already received checked positive-definite matrices before this repair. The established Bessel-corrected means and model-moment covariance formula are preserved; changing that statistical contract is outside this repair.

## Cholesky and ridge implementation

`CholeskyDecomposition` now shares its existing factorization arithmetic with an internal `TryFactorize` path. Public constructors preserve their exception behavior and validation priority. Boolean positive-definiteness probes return failure without allocating an exception. Arithmetic order, factor entries, upper-triangle convention, pivot tolerance, and rejection comparisons are preserved.

`MatrixRegularization` preserves an already accepted symmetric matrix and the exact first eight ridge candidates. If those fail, it continues multiplying the last ridge by ten and checks every candidate. Previously it returned an unchecked ridge smaller than one already rejected. Invalid input and exhausted finite representability now fail explicitly. There is no new eigenvalue threshold, public API, or dependency.

The exception-only change was first replayed separately: the original 429 exceptions became zero with exact numerical parity, and the post-BFGS 420 became zero with exact numerical parity. The latter comparison checked 22,180 candidate decisions and successful factors, 21,764 returned matrices, all 1,000 fits, and all covariances.

With the final ridge correction, all 21,857 returned matrices had positive independently computed eigenvalues; the smallest was `7.3661363393e-7`. There were 600 expected rejected probes, all during weighting updates, and **zero thrown exceptions**. The minimum accepted objective was `2.0126278666e-14`; none was negative. All 1,000 bootstrap realizations were retained without retries or substitutions.

## Validation and completion boundary

| Gate | Result |
|---|---:|
| Numerics Release build, all supported targets | 0 warnings / 0 errors |
| Numerics full tests: net481, net8.0, net9.0, net10.0 | 2,873 + 2,888 + 2,888 + 2,888 passed |
| BestFit Debug solution build with isolated output | 0 warnings / 0 errors |
| Mandatory core / UI / App / API fast tests | 3,434 + 645 + 444 + 498 passed |
| Scoped B17C covariance / example verification | 13 + 7 passed |
| Exact Example #1 verification script, one-result TRX | Passed |
| BestFit namespace/private XML scan | 945 source files passed |
| Packaged independent diagnosis rerun | Saved results reproduced exactly |

The open application locked its normal Debug executable output. An isolated artifact directory allowed the full solution build and fast tests to run without closing the user's application. No full BestFit Verification suite or excluded coverage study was run.

The [evidence package](b17c-cholesky-evidence-20260917/README.md) contains all failure inputs, independent calculations, reference comparison, result summaries, timings, and validation metadata. The Cholesky/ridge repair is complete. The full B17C convergence repair is not: 203 near-minimum BFGS failures and 51 outer stalls remain in this replay. Further work must distinguish stable objective/line-search arithmetic from a decision about the existing outer weighting iteration; neither relaxed tolerances nor a substituted solver has been used to conceal the remaining problem.
