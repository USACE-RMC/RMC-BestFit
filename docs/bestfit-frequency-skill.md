# BestFit frequency curves from a prompt

The portable [bestfit-frequency skill](../skills/bestfit-frequency/SKILL.md) teaches
terminal-capable Codex and Claude sessions to run the headless API, preserve its
numerical results, plot with matplotlib, and display the PNG in chat. It uses the
existing model library and default desktop plot conventions. No hosted service,
user-account system, or formal plugin is required.

For a ChatGPT or Claude web session with execution tools, provide the
[repository link](https://github.com/USACE-RMC/RMC-BestFit) and ask it to clone the
source, read `skills/bestfit-frequency/SKILL.md`, and build/run only the headless
API inside that session. The API and Python client use loopback in the same runtime.
The [web-session prompt and preflight](../skills/bestfit-frequency/references/install.md#web-session-clone-and-run-from-the-repository)
make this workflow explicit. A repository link is sufficient to identify the source;
the session must also support .NET 10, Python, dependency downloads, and a running
local API process. Native skill installation/discovery is a separate client check.

Build the downloadable ZIP from a checkout containing the implementation:

```sh
python scripts/package-bestfit-skill.py
```

Outputs: `artifacts/bestfit-frequency-skill.zip` and `.zip.sha256`. The archive has
one `bestfit-frequency/` root and includes both scripts, Python requirements, setup
and plotting references, an explicitly synthetic example, and the repository
license. It excludes local results, virtual environments, and application binaries.

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

The [three-session handoff notes](plans/bestfit-frequency-skill-handoffs.md) record
the implementation, validation, and remaining Linux/client deployment checks.
The [skill roadmap](plans/bestfit-frequency-skill-roadmap.md) records the intended
repository-based web workflow and acceptance steps around the owner's future PR
to `main`.
