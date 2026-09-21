# Install the portable BestFit skill

The ZIP contains one top-level `bestfit-frequency/` folder with `SKILL.md`, scripts,
references, requirements, and a synthetic example. Keep the folder intact. It
teaches a workflow; it does not include .NET, Python packages, or BestFit binaries.

| Client | Installation |
|---|---|
| Codex local/terminal | Extract `bestfit-frequency/` into `~/.agents/skills/`, or a repository's `.agents/skills/`. Invoke `$bestfit-frequency` or choose it in the skill selector. |
| Claude Code | Extract the folder into `~/.claude/skills/`, or a repository's `.claude/skills/`. Invoke `/bestfit-frequency`. |
| Claude with custom skill upload | Upload the ZIP in the account's Skills interface and enable it, where available. Runtime/network capabilities still determine whether that session can build and run BestFit. |
| ChatGPT or Claude web with an execution workspace | Provide the repository link and explicitly request the repository workflow below. The session clones the source and runs the API internally; this is explicit use of repository instructions, not proof of native skill installation/discovery. |

## Web session: clone and run from the repository

Use this prompt in the chosen web session:

> Use https://github.com/USACE-RMC/RMC-BestFit. Clone it into this session's execution
> workspace and read skills/bestfit-frequency/SKILL.md and its setup/workflow
> references. Build only the headless API using RMC.Numerics 2.2.0 in package mode,
> run it on loopback in this same environment, and use the skill for my requested
> frequency analysis. Preserve numerical settings and show the PNG with downloadable
> SVG and results JSON. Report missing runtime capabilities or unavailable source
> features before attempting the analysis.

Supply the observations or USGS site and analysis choice with that prompt. The
session acquires code from the repository and runs it on its own runtime; a
separately hosted service, public API URL, or MCP connector is unnecessary.
Follow [setup.md](setup.md) for the actual commands and source-compatibility check.
This route requires a terminal, .NET 10 SDK, Python, dependency downloads, and an
API process that survives long enough for local HTTP calls. Reading a GitHub link
alone does not establish those capabilities. Report an unavailable environment
honestly rather than treating another client's successful run as evidence.

Before this implementation is available in a repository revision, supply the
matching development source snapshot to the session. The skill ZIP alone cannot
make the current uncommitted API changes cloneable. Publishing is a separate owner
action.

## Native skill discovery

Codex supports discovery from local skill folders and matching prompts to the
description; restart if the installed skill does not appear. Folder installation
is sufficient for this package. A marketplace plugin is a separate distribution
option. [Official Codex skills documentation](https://developers.openai.com/codex/skills).

Claude Code supports personal and repository skill folders and explicit or
automatic invocation. [Official Claude Code skills documentation](https://code.claude.com/docs/en/skills).
Claude's custom upload format uses a ZIP with the skill folder at its root.
[Official custom-skill packaging instructions](https://support.claude.com/en/articles/12512198-how-to-create-custom-skills).

These instructions were checked on 2026-09-19. Client settings and organization
policies can affect availability. Local installation does not guarantee discovery
in a separate cloud environment; install or include the skill there too.

Once installed, ask for example:

> Use bestfit-frequency to run Bulletin 17C on my annual-flow observations with
> MGBT enabled, then display the BestFit-style matplotlib plot and link the results.

The agent should read the skill and its setup/workflow references. Automatic
selection is supported, but explicit invocation is the reliable way to select it.
The skill does not change the agent's tool permissions or guarantee that every
chat product can install .NET. When a session lacks execution support, use a
terminal-capable session or provide saved API results for plotting.

To install from this source checkout, copy only `skills/bestfit-frequency/` into
the chosen skill directory. Existing unrelated skills need not be changed. To
create the ZIP, run `python scripts/package-bestfit-skill.py` from the repository
root; it writes `artifacts/bestfit-frequency-skill.zip` and a SHA-256 sidecar.

Before these API changes are published, use the matching development checkout.
Downloading this ZIP alone does not make unpublished API changes available in
the upstream repository. Follow `setup.md` for the numerical dependency check.
