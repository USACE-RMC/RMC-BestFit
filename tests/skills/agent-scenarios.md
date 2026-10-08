# Agentic FFA skill acceptance scenarios

Read-only evaluations performed on 2026-09-22. An evaluator first reviewed the
previous bundle and identified missing historical, regional, causal, chronology
and advanced-entry workflows. A fresh evaluator then used the enhanced skill and
its references for the cases below. These are behavioral instruction checks,
not evidence of numerical accuracy, runtime installation or scientific approval.

| Scenario | Required behavior observed |
|---|---|
| Blakely historical augmentation | Distinguishes 91 systematic years, the explicit 1882 interval and 52 remaining censored years in 1870–1922; a five-year gap needs separate completeness evidence |
| Newspaper flood and NWS crest | Retains unresolved evidence; does not equate stage with discharge or missing reporting with nonexceedance; October 1 maps to the declared ending water year |
| Regional lookup at an arbitrary town | Resolves basin/outlet first, uses applicable USGS sources and uncertainty definitions, maps skew MSE to Normal SD only for Bayesian entry; does not guess unsupported regional values |
| Dependent regional quantiles | Preserves prediction context and dependencies; prefers separate quantile candidates to unjustified independent combination |
| Kamp causal information | Selects GEV and Q500 Normal(480,80); distinguishes the canonical verification's disabled Jeffreys rule from the saved project's enabled setting |
| Missing runtime or failed service | Reports missing execution capabilities, retains failed source receipts and does not claim a successful fit or source retrieval |
| B17C as a data reference | Applies official B17C collection/entry concepts to Bayesian FFA without automatically choosing the B17C estimator |

The acceptance pass found one instruction gap: simple and one-shot examples could
be read as permission to fit before inspecting chronology. The workflow now shows
preparation and inspection before fitting, and limits one-shot commands to inputs
whose chronology and source classification have already been reviewed.

Deterministic companions: `test_ffa_workflows.py` covers annual indexes, threshold
counts, evidence requirements, units, dependence, unsupported option names and
chronology bounds; `test_research_and_scenarios.py` covers source failure retention,
candidate comparisons and systematic-cohort flag transfer. `test_skill_package.py`
checks reproducible, identical skill content in both distribution formats.
