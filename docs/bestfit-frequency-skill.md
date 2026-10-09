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
error, trends and joint models. All package formats include the guide and the
same updated Python renderer. Examples supply a standard for explanation and
source review, not numerical assumptions to copy into another study. Consult the
[example limitations](example-limitations.md) for unresolved source/engineering
questions and saved-result limitations.

For a ChatGPT or Claude web session with execution tools, provide the
[repository link](https://github.com/USACE-RMC/RMC-BestFit) and ask it to clone the
source, read `skills/bestfit-frequency/SKILL.md`, and build/run only the headless
API inside that session. The API and Python client use loopback in the same runtime.
The [web-session prompt and preflight](../skills/bestfit-frequency/references/install.md#live-preflight-and-acceptance)
make this workflow explicit. A repository link is sufficient to identify the source;
the session must also support .NET 10, Python, dependency downloads, and a running
local API process. Native skill installation/discovery is a separate client check.

Build the downloadable ZIP from a checkout containing the implementation:

```sh
python scripts/package-bestfit-skill.py
```

The packager produces four archives, each with a `.zip.sha256` sidecar:

| Archive under `artifacts/` | Purpose |
|---|---|
| `rmc-bestfit-skill.zip` | Standalone skill with one `bestfit-frequency/` root |
| `rmc-bestfit-marketplace.zip` | OpenAI local marketplace with a nested plugin |
| `rmc-bestfit-openai-plugin.zip` | Direct OpenAI plugin upload: `.codex-plugin/plugin.json`, `skills/`, `assets/`, and `PRIVACY.md` at the archive root |
| `rmc-bestfit-claude-plugin.zip` | Claude plugin with its own one-plugin marketplace |

All four contain identical maintained skill files, including preparation,
research-capture, execution and plotting helpers, Python requirements, references,
synthetic examples and the repository license. They exclude local results, virtual
environments and application binaries. Plugin version **0.3.3** is independent of
the BestFit application/API version **2.0.1** and RMC.Numerics **2.2.0**.

The plugin is listed as **RMC-BestFit** with the identifier `rmc-bestfit`; the
maintained skill keeps its `bestfit-frequency` name. Both plugin formats link to
the [privacy policy](plugin-privacy.md), which is included in every archive.
Publish that document at the manifest's exact public URL before resubmission.
The Claude plugin also includes a 512-pixel copy of the existing official icon.

See [installation instructions](../skills/bestfit-frequency/references/install.md)
for Codex, Claude Desktop, Claude Code, and custom ZIP uploads. Once installed, matching prompts
can select the skill, or users can invoke it explicitly. The session still needs
execution tools, .NET 10, Python, and dependency access. Publishing a ZIP does not
publish the corresponding API code; distribute a compatible source revision.

MGBT remains an API opt-in. The skill enables it explicitly for B17C unless the
user supplies another screening choice. Plots use BestFit's returned plotting
positions and uncertainty coordinates. Saved desktop customizations are outside
the [default plot contract](../skills/bestfit-frequency/references/plot-contract.md).

Run the complete Python contract suite from the repository root:

```sh
python -m pip install -r skills/bestfit-frequency/requirements.txt pytest
python -m pytest tests/skills
```

Use pytest so both pytest functions and unittest classes are collected. API
contracts live in `RMC.BestFit.Api.Tests`. Validate Claude's marketplace manifest,
plugin manifest, and skill directory separately using the
[installation checks](../skills/bestfit-frequency/references/install.md#claude-code).
Synthetic workflow demonstrations exercise integration; they are not scientific
verification. Package checks do not establish successful installation or execution
in a named client/account.

## Automated package and release validation

The [Plugin packages workflow](../.github/workflows/PluginPackages.yml) runs on
Windows and Linux. It builds/tests the API, runs all Python skill contracts, packages
the four archives, and uses the extracted skill for live synthetic default B17C
and Bayesian runs plus an MCP transport check. It retains execution evidence and
validated packages. The workflow's Linux checks still need an actual CI run after
publication of this change; defining the workflow does not establish a passing run.

To exercise the packaged runtime locally after installing the dependencies above,
build the API and archives first, then run the
[smoke harness](../scripts/smoke-bestfit-plugin.py):

```sh
dotnet build src/RMC.BestFit.Api -c Release -p:UseLocalRmcNumerics=false
python scripts/package-bestfit-skill.py
python scripts/smoke-bestfit-plugin.py --output artifacts/plugin-smoke
```

Use a fresh output directory for each run. These synthetic checks establish
integration behavior; inspect their artifacts and warnings before reporting
acceptance.

Manual workflow dispatch performs validation only. After the owner publishes a
BestFit application release, a successful release-triggered run attaches all four
ZIPs and checksum sidecars only after both OS jobs pass. The tag must be `vX.Y.Z`
and match `Directory.Build.props`; the plugin retains its independent version.
The workflow does not create a release, publish to a plugin directory, or install
anything into a client profile.

For source preparation, platform prerequisites and client acceptance checks, use the maintained [installation guide](../skills/bestfit-frequency/references/install.md). Package validation does not establish public directory publication or successful installation in a particular account.
