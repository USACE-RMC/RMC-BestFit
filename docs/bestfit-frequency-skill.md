# Agentic flood frequency analysis with BestFit

The portable [bestfit-frequency skill](../skills/bestfit-frequency/SKILL.md) teaches
terminal-capable Codex and Claude sessions to run the headless API, preserve its
numerical results, plot with matplotlib, and display the PNG in chat. It uses the
existing model library and default desktop plot conventions. No hosted service,
user-account system, or formal plugin is required. The enhanced workflow adds
historical/perception-threshold evidence, location-based USGS regional research,
regional-skew and regional/causal-quantile recipes, and an input chronology before
fitting. Official Bulletin 17C guides data collection/entry in either estimator.

Start with the [study workflow](../skills/bestfit-frequency/references/study-workflow.md),
[source-backed recipes](../skills/bestfit-frequency/references/information-recipes.md)
and [platform installation steps](../skills/bestfit-frequency/references/install.md).
`run_study.py --prepare-only` builds a review bundle without fitting;
`run_study.py` compares documented candidates and retains failed runs and diagnostics.
No scientific algorithms or numerical defaults are changed.

The bundled [worked-example guide](../skills/bestfit-frequency/references/examples.md)
routes frequency-analysis prompts to the repository's junior-engineer tutorials.
It covers data entry, historical evidence, Bayesian fits, GMM penalties, measurement
error, trends and joint models. Both package formats include the guide and the
same updated Python renderer. Examples supply a standard for explanation and
source review, not numerical assumptions to copy into another study. Consult the
[example issue log](example-issues-for-haden.md) for unresolved source/engineering
questions and saved-result limitations.

For a ChatGPT or Claude web session with execution tools, provide the
[repository link](https://github.com/USACE-RMC/RMC-BestFit) and ask it to clone the
source, read `skills/bestfit-frequency/SKILL.md`, and build/run only the headless
API inside that session. The API and Python client use loopback in the same runtime.
The [web-session prompt and preflight](../skills/bestfit-frequency/references/install.md#repository-workflow-and-starter-prompt)
make this workflow explicit. A repository link is sufficient to identify the source;
the session must also support .NET 10, Python, dependency downloads, and a running
local API process. Native skill installation/discovery is a separate client check.

Build the downloadable ZIP from a checkout containing the implementation:

```sh
python scripts/package-bestfit-skill.py
```

Outputs: `artifacts/bestfit-frequency-skill.zip` and
`artifacts/bestfit-frequency-marketplace.zip`, each with a `.zip.sha256` sidecar.
The standalone archive has one `bestfit-frequency/` root; the marketplace archive
wraps the same skill in a thin OpenAI plugin. Both include the preparation,
research-capture, execution and plotting helpers, Python requirements, references,
synthetic examples and repository license. They exclude local results, virtual
environments and application binaries.

See [installation instructions](../skills/bestfit-frequency/references/install.md)
for Codex, Claude Code, and custom ZIP uploads. Once installed, matching prompts
can select the skill, or users can invoke it explicitly. The session still needs
execution tools, .NET 10, Python, and dependency access. Publishing a ZIP does not
publish the corresponding API code; distribute a compatible source revision.

MGBT remains an API opt-in. The skill enables it explicitly for B17C unless the
user supplies another screening choice. Plots use BestFit's returned plotting
positions and uncertainty coordinates. Saved desktop customizations are outside
the [default plot contract](../skills/bestfit-frequency/references/plot-contract.md).

Tests: `python -m unittest discover -s tests/skills` after installing the skill's
requirements. API contracts live in `RMC.BestFit.Api.Tests`. Synthetic workflow
demonstrations exercise integration; they are not scientific verification.

The [FFA implementation and validation record](plans/agentic-ffa-implementation.md)
records the current enhancements, tests and acceptance limits. The earlier
[three-session handoff notes](plans/bestfit-frequency-skill-handoffs.md) record
the initial plotting skill and its deployment checks.
The [skill roadmap](plans/bestfit-frequency-skill-roadmap.md) records the intended
repository-based web workflow and acceptance steps around the owner's future PR
to `main`.
