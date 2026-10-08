# Frequency skill tests

Run the deterministic Python suite from the repository root:

```powershell
python -m pytest tests/skills
```

Install the skill's plotting requirements and pytest in the selected Python environment.
These checks cover input adapters, evidence handling, chronology, response contracts,
plotting, and reproducible packaging. They do not establish numerical accuracy,
runtime installation, or scientific approval.

## Behavioral acceptance scenarios

Review the skill and its references against these scenarios whenever their workflow
instructions change. Inspect chronology and source classification before fitting;
one-shot workflows require that review to have already happened.

| Scenario | Required behavior |
|---|---|
| Blakely historical augmentation | Distinguishes 91 systematic years, the explicit 1882 interval and 52 remaining censored years in 1870–1922; a five-year gap needs separate completeness evidence |
| Newspaper flood and NWS crest | Retains unresolved evidence; does not equate stage with discharge or missing reporting with nonexceedance; October 1 maps to the declared ending water year |
| Regional lookup at an arbitrary town | Resolves basin/outlet first, uses applicable USGS sources and uncertainty definitions, maps skew MSE to Normal SD only for Bayesian entry; does not guess unsupported regional values |
| Dependent regional quantiles | Preserves prediction context and dependencies; prefers separate quantile candidates to unjustified independent combination |
| Kamp causal information | Selects GEV and Q500 Normal(480,80); distinguishes the canonical verification's disabled Jeffreys rule from the saved project's enabled setting |
| Missing runtime or failed service | Reports missing execution capabilities, retains failed source receipts and does not claim a successful fit or source retrieval |
| B17C as a data reference | Applies official B17C collection/entry concepts to Bayesian FFA without automatically choosing the B17C estimator |

## Deterministic coverage

`test_ffa_workflows.py` covers annual indexes, threshold counts, evidence requirements,
units, dependence, unsupported option names, and chronology bounds.
`test_research_and_scenarios.py` covers source failure retention, candidate comparisons,
and systematic-cohort flag transfer. `test_skill_package.py` checks reproducible,
identical skill content across the distribution formats.
