# Link BestFit to OpenAI and Anthropic chat platforms

One maintained skill is distributed two ways. From the repository root run:

```sh
python scripts/package-bestfit-skill.py
```

This creates `artifacts/bestfit-frequency-skill.zip` (Claude/standalone skill) and
`artifacts/bestfit-frequency-marketplace.zip` (OpenAI skills-only plugin), each
with a SHA-256 sidecar. No profile is modified, connector registered or plugin
published. The plugin uses the supported `.codex-plugin/plugin.json` compatibility
layout. Both archives contain identical skill files, with no binaries or runtimes.

## Runtime requirement for every platform

Provide a reachable compatible repository revision or matching source snapshot.
The session needs a terminal, writable workspace, .NET 10 SDK, Python dependencies,
network access for sources/packages, a persistent child process, loopback HTTP and
artifact delivery. Follow [setup.md](setup.md) to clone/build/run the headless API
in that execution environment. A skill upload or repository URL alone establishes
none of these capabilities. No separate public service is needed when API and
agent run in the same environment. Localhost on your computer is not localhost
inside a web session. Report missing capabilities; do not claim another host's
validation applies. Saved responses can still be plotted in a capable Python session.

## OpenAI desktop / Codex

1. Extract `bestfit-frequency-marketplace.zip` to a chosen local directory, keeping
   its top-level `bestfit-frequency-marketplace` folder and hidden `.agents` folder.
2. Register that extracted marketplace root:
   `codex plugin marketplace add ABSOLUTE_PATH/bestfit-frequency-marketplace`.
3. Restart the desktop client. Open **Plugins Directory**, select **BestFit Local**,
   and install **BestFit Flood Frequency**. Installation is a separate user action;
   creating the ZIP does not install it.
4. Start a fresh task with access to the matching BestFit checkout. Select the skill
   with `@` where available, or invoke `$bestfit-frequency` in Codex CLI/IDE.
5. Run the synthetic preparation/chronology workflow first. Confirm the agent reads
   the skill, uses the matching API, retains sources/settings and displays the plot.

For standalone installation instead, extract `bestfit-frequency/` from the skill
ZIP into `~/.agents/skills/` or the target repository's `.agents/skills/`. Restart
if it is not discovered. Choose one installation route to avoid duplicate copies.
Do not overwrite an existing edited skill without reviewing its contents.

Official instructions: [skills](https://developers.openai.com/codex/skills),
[plugins and local marketplaces](https://developers.openai.com/plugins/build/plugins).
Local/repository marketplace availability varies by surface; it does not imply
installation or synchronization into a separate web/mobile account.

## ChatGPT web

If your workspace exposes an agent builder with **Add skill**, upload the standard
skill package there, provide the compatible source revision and execution
capabilities, and test with **Preview / Try in ChatGPT** before creating/sharing
that agent. Admin permissions and product availability apply.
[Official workspace-agent example](https://developers.openai.com/cookbook/articles/chatgpt-agents-sales-meeting-prep).

Otherwise use the repository workflow below in a terminal-capable ChatGPT session.
For broader native distribution across web/mobile, publication to OpenAI's universal
plugin directory is a later owner action under its current submission process;
this repository package is not a published plugin. A skills-only package does not
need developer-mode MCP registration. Do not expose the unauthenticated local API
as a public connector to work around missing execution capabilities.

## Claude chat

1. Enable **Code execution and file creation** under **Settings → Capabilities**.
   Team/Enterprise administrators may control skills and execution availability.
2. Open **Customize → Skills → + → Create skill → Upload a skill**.
3. Upload `bestfit-frequency-skill.zip`. It has one top-level `bestfit-frequency/`
   folder containing `SKILL.md`; do not upload the marketplace ZIP instead.
4. Enable the skill, start a fresh chat, and explicitly request `bestfit-frequency`.
5. Supply the compatible repository revision and study location/data. Test the
   synthetic input chronology and runtime prerequisites before an engineering fit.

[Official Claude skill use](https://support.claude.com/en/articles/12512180-use-skills-in-claude)
and [ZIP structure](https://support.claude.com/en/articles/12512198-how-to-create-custom-skills).
Installing a skill does not guarantee .NET installation or persistent processes in
that chat environment. Keep runtime acceptance distinct from successful upload.

## Claude Code

Extract the skill folder into `~/.claude/skills/` or the repository's `.claude/skills/`.
Open the compatible checkout, invoke `/bestfit-frequency`, and test preparation and
chronology before fitting. [Official instructions](https://code.claude.com/docs/en/skills).

## Repository workflow and starter prompt

Supply the revision that actually contains these API and skill changes. An upstream
URL cannot clone uncommitted work or unpublished local commits; use a matching
snapshot until the owner publishes a reachable revision.

> Use bestfit-frequency from https://github.com/USACE-RMC/RMC-BestFit at [compatible
> revision]. Clone it into this session, read skills/bestfit-frequency/SKILL.md and
> its references, and verify the .NET/Python/loopback capabilities. Investigate flood
> frequency at [location/site]. Use official Bulletin 17C to guide data collection
> and entry, establish flow definition and year convention, research and justify
> historical and regional inputs, show input chronology, and compare supported
> candidate analyses. Retain sources, requests, applied settings, diagnostics and
> plots. Identify unresolved judgments before final engineering adoption.

Installation instructions checked against official documentation on 2026-09-22.
Named client/account installation and Linux/web runtime execution must be validated
separately; package checks and Windows tests do not prove those environments work.
