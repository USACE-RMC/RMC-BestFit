<!-- technical-reference-status: complete -->

# Bayesian Estimation and MCMC

[Estimation index](index.md) | [MAP](maximum-a-posteriori.md) | [Model comparison](model-comparison.md) | [Diagnostics](diagnostics.md) | [Technical Reference](../index.md)

`BayesianAnalysis` is BestFit's primary uncertainty engine. It samples the full `IModel.LogLikelihood` target, summarizes retained posterior draws, and propagates those draws into analysis-specific frequency, prediction, and uncertainty products. The public sampler inventory in BestFit 2.0 is `DEMCz`, `DEMCzs`, `ARWMH`, and `NUTS`; plain `HMC` exists in Numerics but is not selectable through `BayesianAnalysis` ([TR-022](../review-findings.md#tr-022)).

## Posterior Target

For observation-information units $y_1,\ldots,y_n$, parameter vector $\boldsymbol\theta$, likelihood contributions $L_i$, and complete implemented prior $\pi$, the target is

$$
p(\boldsymbol\theta\mid\mathbf y)
\propto
\left\{\prod_{i=1}^{n}L_i(\boldsymbol\theta)\right\}
\pi(\boldsymbol\theta). \tag{MCMC.1}
$$

The sampler receives the log kernel

$$
\log\widetilde p(\boldsymbol\theta\mid\mathbf y)
=\texttt{Model.DataLogLikelihood}(\boldsymbol\theta)
+\texttt{Model.PriorLogLikelihood}(\boldsymbol\theta). \tag{MCMC.2}
$$

Equation (MCMC.2) includes parameter-prior normalizing constants, quantile-prior terms and Jacobians, Jeffreys scale terms, and model-specific prior components. `SetUpSampler()` clones each `ModelParameter.PriorDistribution` for initialization and bound information, but the Metropolis/Hamiltonian target is the model callback itself.

## Simulation Schedule and Retained Draws

BestFit configuration counts *recorded thinned iterations*. One recorded transition invokes the Numerics chain transition `ThinningInterval` times. Therefore the first main run performs

$$
N_{\mathrm{transition}}=
\texttt{Iterations}\times\texttt{ThinningInterval} \tag{MCMC.3}
$$

raw transitions per chain. `MarkovChains[c]` stores `Iterations` recorded states. The first `WarmupIterations` of those states are excluded when Numerics calculates R-hat, but they remain serialized in the chain collection. The base sampler then runs additional transitions and stores exactly `OutputLength` states pooled across chains in `MCMCResults.Output`. Those extra post-run draws—not `MarkovChains`—feed parameter summaries, ESS, DIC, WAIC, PSIS-LOO, covariance, and downstream posterior propagation.

This schedule differs from interfaces in which “iterations” means warmup plus retained sampling. The API enforces `WarmupIterations <= Iterations / 2`; hence at least half of the main recorded chain remains post-warmup for R-hat. The separate output run follows all main iterations.

Before sampling, Numerics generates `InitialIterations` Latin-hypercube combinations from the cloned marginal priors, evaluates the full target, and starts chains from the best finite combinations. `BayesianAnalysis` leaves the Numerics initialization mode at its default `Randomize`; it does not expose the base sampler's optional MAP or user-defined initialization properties.

## Differential-Evolution MCMC

### `DEMCz`

Let $\boldsymbol\theta_c$ be the current state of chain $c$ and let $\mathbf z_{r_1},\mathbf z_{r_2}$ be two distinct states selected from the accumulated population/archive. The proposal is

$$
\boldsymbol\theta_c^*
=\boldsymbol\theta_c
+\gamma(\mathbf z_{r_1}-\mathbf z_{r_2})
+\boldsymbol\varepsilon,
\qquad
\varepsilon_j\sim N(0,b^2). \tag{MCMC.4}
$$

BestFit's default advanced settings are

$$
\gamma=\frac{2.38}{\sqrt{2d}},
\qquad
P(\gamma=1)=0.10,
\qquad
b=10^{-12}, \tag{MCMC.5}
$$

where $d$ is the parameter count. A proposal outside any cloned prior support is rejected without evaluating the target. Otherwise, because (MCMC.4) is treated as symmetric, acceptance is

$$
\alpha=\min\left\{1,
\exp[\log\widetilde p(\boldsymbol\theta^*)-
\log\widetilde p(\boldsymbol\theta)]\right\}. \tag{MCMC.6}
$$

### `DEMCzs`

`DEMCzs` uses (MCMC.4) for ordinary moves and, with default probability 0.10 after the first five recorded intervals, attempts a snooker move. It projects a difference between two other-chain states onto the line between the current state and another chain, uses a uniform multiplier on $[1.2,2.2]$, and includes the $d-1$ dimensional distance Jacobian in the Metropolis ratio. This is the BestFit default sampler and is the workhorse for correlated, non-Gaussian hydrologic posteriors.

Both methods are population samplers and require at least three chains in Numerics; BestFit validation is stricter and requires 4–20 so multi-chain diagnostics are available. Differential evolution can still fail when the population occupies one mode, bounds truncate proposals, components are nonidentified, or posterior geometry has narrow ridges.

## Adaptive Random-Walk Metropolis-Hastings

`ARWMH` uses a Gaussian random-walk mixture centered on the current state. With probability $\beta$—and for the first $100d$ raw transitions—it proposes from a small identity covariance; otherwise it uses the running covariance $\widehat{\boldsymbol\Sigma}_c$:

$$
\boldsymbol\theta_c^*\sim
\begin{cases}
N(\boldsymbol\theta_c,,0.1^2\mathbf I/d), & \text{small-kernel move},\\
N(\boldsymbol\theta_c,,s\widehat{\boldsymbol\Sigma}_c), & \text{adaptive move},
\end{cases}
\quad
s=2.38^2/d,
\quad \beta=0.05. \tag{MCMC.7}
$$

The symmetric Metropolis ratio is (MCMC.6). The Adaptive Metropolis construction estimates covariance from the complete realized-chain history, so a rejection or infeasible proposal contributes the repeated retained state. Numerics performs exactly one covariance update after every transition and retains the original continual-adaptation schedule. Proposal covariance begins using the accumulated history after the existing `100 * d` transition threshold. This corrected contract is verified under [TR-025](../review-findings.md#tr-025).

## No-U-Turn Sampler

For differentiable log target $\mathcal L(\boldsymbol\theta)=\log\widetilde p(\boldsymbol\theta\mid\mathbf y)$ and auxiliary momentum $\mathbf r\sim N(\mathbf0,\mathbf M)$, NUTS uses Hamiltonian

$$
H(\boldsymbol\theta,\mathbf r)
=-\mathcal L(\boldsymbol\theta)
+\frac12\mathbf r^{\mathsf T}\mathbf M^{-1}\mathbf r. \tag{MCMC.8}
$$

Leapfrog integration builds a binary tree in randomly selected forward/backward directions. Tree doubling stops at a U-turn, invalid/divergent subtree, or `MaxTreeDepth`; the maximum trajectory contains $2^{\texttt{MaxTreeDepth}}$ leapfrog steps. A candidate is selected by multinomial Hamiltonian weights. During `WarmupIterations * ThinningInterval` raw transitions, dual averaging targets an average Metropolis statistic of 0.80. Numerics can adapt a diagonal mass matrix in windows, but its `AdaptMassMatrix` default is false and `BayesianAnalysis.SetUpSampler()` does not enable or expose that property; the BestFit path therefore retains an identity diagonal mass matrix.

BestFit supplies no analytic gradient, so Numerics applies bound-aware finite differences to the complete `Model.LogLikelihood` posterior target. A coupled-prior verification confirms both data and prior derivatives enter this path. When a Numerics caller supplies an analytic `GradientFunction`, both ordinary leapfrog integration and the reasonable-step-size initialization heuristic use it; the latter route has a permanent regression because an earlier implementation bypassed the configured function. Sampling still occurs in the bounded API parameterization rather than an unconstrained transformed space. Nondifferentiable likelihood branches, hard support boundaries, interval-probability underflow, and strongly different parameter scales can impair Hamiltonian trajectories.

At the sampler level, `MCMCSampler.AcceptanceRates` always means accepted transitions divided by samples. NUTS accepts each completed transition, so that generic counter is normally 1.0 and is not its tuning statistic. `NUTS.HamiltonianAcceptanceRates` separately exposes the mean post-warmup Hamiltonian acceptance probability. When results are constructed from NUTS, the existing `MCMCResults.AcceptanceRates` field stores this Hamiltonian statistic so BestFit can persist and report it with concise NUTS-specific wording. Diagnostic transition counts, divergences, maximum-tree-depth hits, mean tree depth, mean leapfrog steps, final step size, and energy Bayesian fraction of missing information (E-BFMI) remain available only on the live `NUTS` sampler. They are accumulated online with constant memory and no additional target or gradient evaluations, but are not serialized or displayed by BestFit. This correction is verified under [TR-030](../review-findings.md#tr-030).

## Posterior Summaries

Let $S=\texttt{Results.Output.Count}$ and let $\boldsymbol\theta^{(s)}$ be the pooled retained draws. For each parameter, Numerics reports the arithmetic mean, sample standard deviation, median, and equal-tailed interval

$$
\left[
Q_{\theta_j}\left(\frac{1-w}{2}\right),
Q_{\theta_j}\left(1-\frac{1-w}{2}\right)
\right], \tag{MCMC.9}
$$

where $w=\texttt{CredibleIntervalWidth}$. `Results.PosteriorMean` is the vector of marginal means evaluated under the full target. `Results.MAP` is the highest-target state encountered in the separate output run; it is a sampled mode candidate, not a continuous optimization result. `PointEstimator` determines which of these an analysis uses for point summaries.

Posterior quantile/return-level uncertainty must be obtained by transforming every joint draw. Transforming marginal parameter means does not in general equal a posterior mean quantile, and separately transforming marginal credible endpoints destroys dependence.

## R-hat and Effective Sample Size

Numerics discards the first $w_0$ recorded main-chain states, splits every retained chain into equal half-chains, and applies pooled midranks and Blom's inverse-normal transform. With within-half-chain variance $W$ and between-half-chain variance $B$, the rank-normalized statistic is

$$
\widehat R_{\mathrm{rank}}
=\sqrt{\frac{(n-1)W/n+B/n}{W}}. \tag{MCMC.10}
$$

The same calculation is repeated after folding draws about their pooled median and rank normalizing again. The stored `Rhat` is $\max(\widehat R_{\mathrm{rank}},\widehat R_{\mathrm{folded\ rank}})$. The concise report threshold is $\widehat R<1.01$. Single-chain, insufficient, constant, and nonfinite diagnostic input yields `NaN`.

Rank normalization improves behavior for heavy-tailed marginals, splitting detects within-chain drift, and folding detects scale disagreement. A value near one remains necessary but not sufficient: chains can agree on these marginal diagnostics while sharing the same incomplete region or missing multimodal structure.

ESS is calculated from the separate output draws. Numerics rank-normalizes and splits the chains, estimates autocovariances with zero-padded FFTs, and applies Geyer's multi-chain initial-positive and initial-monotone paired sequence. If $S$ is the total number of split draws and $\widehat\tau$ is the estimated integrated autocorrelation time,

$$
\widehat N_{\mathrm{eff}}=\frac{S}{\widehat\tau}. \tag{MCMC.11}
$$

Bulk ESS uses rank-normalized draws. Lower- and upper-tail ESS use pooled 0.05 and 0.95 quantile indicators. The existing scalar `ESS` stores the minimum of these three estimates without adding result or serialization fields. Unequal chains are trimmed to a common retained length for ESS; invalid or constant diagnostic input yields `NaN`. The 51-lag original-scale averaged ACF remains unchanged for plots.

ESS expresses correlated-chain precision as an approximate number of independent draws. The stored value is conservative across central and 5%/95% tail behavior, but it is not a guarantee for more extreme flood quantiles or exceedance probabilities. Negative autocorrelation can legitimately produce ESS above the retained draw count.

## Compile-Checked API Workflow

<!-- snippet: bayesian-mcmc-workflow -->
```csharp
private static BayesianAnalysis ConfigureBayesianMcmc(IModel model)
{
    int parameterCount = Math.Max(1, model.Parameters.Count);
    var analysis = new BayesianAnalysis(
        model,
        BayesianAnalysis.SamplerType.DEMCzs)
    {
        UseSimulationDefaults = false,
        UseAdvancedSimulationDefaults = false,
        NumberOfChains = 4,
        InitialIterations = Math.Max(4, 100 * parameterCount),
        WarmupIterations = 1_500,
        Iterations = 3_000,
        ThinningInterval = 20,
        OutputLength = 10_000,
        CredibleIntervalWidth = 0.90,
        PRNGSeed = 12345,
        Jump = 2.38 / Math.Sqrt(2.0 * parameterCount),
        JumpThreshold = 0.10,
        SnookerThreshold = 0.10,
        Noise = 1E-12
    };

    analysis.SetUpSampler();
    return analysis;
}
```

Before `RunAsync`, call `Validate()` and treat warnings separately from errors. The validation result intentionally includes a “not estimated” warning before a first run but can still be valid. `RunAsync` rethrows cancellation, stores other failures in `LastError`, clears partial results, and otherwise computes DIC, WAIC, and verified PSIS-LOO. Report the seed, sampler, every simulation setting, dependency version/commit, R-hat/ESS, trace plots, and posterior predictive checks.

## Assumptions, Failure Modes, and Verification

- The full posterior in (MCMC.2) must be proper and scientifically coherent.
- Marginal prior supports also serve as hard proposal bounds; bounds should not truncate plausible posterior mass.
- Label switching and redundant weights invalidate ordinary component-wise summaries in mixtures.
- Thinning reduces stored autocorrelation but usually discards information; it does not repair a poorly mixing chain.
- Parallel chains are deterministic for a fixed seed and configuration only to the extent guaranteed by the pinned implementation and runtime.
- Rank-normalized R-hat and bulk/tail ESS are screening diagnostics, not proof of convergence or model adequacy.
- Compile checking verifies configuration syntax. Computational recovery, coverage, and cross-package parity remain Verification work and were not run during this pass.

Implementation source: `RMC.BestFit.Estimation.BayesianAnalysis`; pinned Numerics commit `828664650c9327b309ee8332e707ccca73588e93`, files `MCMCSampler.cs`, `DEMCz.cs`, `DEMCzs.cs`, `ARWMH.cs`, `NUTS.cs`, `MCMCResults.cs`, and `MCMCDiagnostics.cs`.

## References

<a id="ref-1"></a>[1] N. Metropolis et al., “Equation of state calculations by fast computing machines,” *The Journal of Chemical Physics*, vol. 21, no. 6, pp. 1087–1092, 1953.

<a id="ref-2"></a>[2] W. K. Hastings, “Monte Carlo sampling methods using Markov chains and their applications,” *Biometrika*, vol. 57, no. 1, pp. 97–109, 1970.

<a id="ref-3"></a>[3] C. J. F. ter Braak, “A Markov Chain Monte Carlo version of the genetic algorithm Differential Evolution: easy Bayesian computing for real parameter spaces,” *Statistics and Computing*, vol. 16, pp. 239–249, 2006.

<a id="ref-4"></a>[4] C. J. F. ter Braak and J. A. Vrugt, “Differential Evolution Markov Chain with snooker updater and fewer chains,” *Statistics and Computing*, vol. 18, pp. 435–446, 2008.

<a id="ref-5"></a>[5] H. Haario, E. Saksman, and J. Tamminen, “An adaptive Metropolis algorithm,” *Bernoulli*, vol. 7, no. 2, pp. 223–242, 2001.

<a id="ref-6"></a>[6] M. D. Hoffman and A. Gelman, “The No-U-Turn Sampler: adaptively setting path lengths in Hamiltonian Monte Carlo,” *Journal of Machine Learning Research*, vol. 15, pp. 1593–1623, 2014.

<a id="ref-7"></a>[7] A. Vehtari et al., “Rank-normalization, folding, and localization: an improved $\widehat R$ for assessing convergence of MCMC,” *Bayesian Analysis*, vol. 16, no. 2, pp. 667–718, 2021.

<a id="ref-8"></a>[8] PyMC Developers, “`pymc.NUTS`,” PyMC API documentation, sampler-statistics interface. <https://www.pymc.io/projects/docs/en/stable/api/generated/pymc.NUTS.html>

<a id="ref-9"></a>[9] BlackJAX Developers, “No-U-Turn Sampling,” BlackJAX API documentation, `NUTSInfo` interface. <https://blackjax-devs.github.io/blackjax/autoapi/blackjax/mcmc/nuts/index.html>

---

[Previous: GMM](generalized-method-of-moments.md) | [Next: model comparison](model-comparison.md)
