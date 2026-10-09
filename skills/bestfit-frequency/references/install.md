# Link BestFit to OpenAI and Anthropic chat platforms

One maintained skill is distributed in four archive layouts. From the repository
root run:

```sh
python scripts/package-bestfit-skill.py
```

| Archive under `artifacts/` | Installation purpose |
|---|---|
| `rmc-bestfit-skill.zip` | Standalone skill upload/copy |
| `rmc-bestfit-marketplace.zip` | OpenAI local marketplace registration |
| `rmc-bestfit-openai-plugin.zip` | Direct OpenAI plugin ZIP submission |
| `rmc-bestfit-claude-plugin.zip` | Claude plugin upload/local marketplace |

Each has a SHA-256 sidecar. Packaging does not modify profiles, register a connector
or publish a plugin. The direct OpenAI archive places the supported compatibility
manifest `.codex-plugin/plugin.json`, `skills/`, `assets/`, and `PRIVACY.md` at its root. The
marketplace archive nests that plugin beneath its marketplace root; it is not the
direct-upload archive. The Claude plugin uses `.claude-plugin/plugin.json` and
includes its own one-plugin marketplace. All four contain identical skill files
and the repository license, with no BestFit binaries or runtimes. Plugin version
**0.3.3** is separate from BestFit **2.0.1** and RMC.Numerics **2.2.0**.

The plugin's public name is **RMC-BestFit** and package identifier is
`rmc-bestfit`. The maintained skill remains `bestfit-frequency`; its folder and
standalone invocation have not changed. Version 0.3.2 used the plugin identifier
`bestfit-frequency`. The renamed plugin is a new listing, not an update of that
identifier. Select one installed copy to avoid duplicate skills.

## Runtime requirement for every platform

Provide a reachable compatible repository revision or matching source snapshot.
The session needs a terminal, writable workspace, .NET 10 SDK, Python dependencies,
network access for sources/packages, a persistent child process, loopback HTTP and
artifact delivery. Follow [setup.md](setup.md) to clone/build/run the headless API
with the approved RMC.Numerics **2.2.0** package in that execution environment. The
Python helpers call local REST endpoints; MCP registration is optional. A skill
upload or repository URL alone establishes
none of these capabilities. No separate public service is needed when API and
agent run in the same environment. Localhost on your computer is not localhost
inside a web session. Report missing capabilities; do not claim another host's
validation applies. Saved responses can still be plotted in a capable Python session.

## OpenAI desktop / Codex

1. Extract `rmc-bestfit-marketplace.zip` to a chosen local directory, keeping
   its top-level `rmc-bestfit-marketplace` folder and hidden `.agents` folder.
2. Register that extracted marketplace root:
   `codex plugin marketplace add ABSOLUTE_PATH/rmc-bestfit-marketplace`.
3. Restart the desktop client. Open **Plugins Directory**, select **BestFit Local**,
   and install **RMC-BestFit**. Installation is a separate user action;
   creating the ZIP does not install it.
4. Start a fresh task with access to the matching BestFit checkout. Select the skill
   with `@` where available, or invoke `$bestfit-frequency` in Codex CLI.
5. Run the [live preflight and acceptance](#live-preflight-and-acceptance) below.

For standalone installation instead, extract `bestfit-frequency/` from the skill
ZIP into `~/.agents/skills/` or the target repository's `.agents/skills/`. This is
also the skill route for the Codex IDE extension, which does not support plugins.
Restart if it is not discovered. Choose one route to avoid duplicate copies.
Do not overwrite an existing edited skill without reviewing its contents.

Official instructions: [skills](https://developers.openai.com/codex/skills),
[plugins and local marketplaces](https://developers.openai.com/plugins/build/plugins).
Local/repository marketplace availability varies by surface; it does not imply
installation or synchronization into a separate web/mobile account.

## ChatGPT private workspace

A workspace admin can use the existing marketplace ZIP for a private pilot:

1. Follow the desktop marketplace steps above to register and install the plugin.
2. In ChatGPT **Plugins → Personal**, find the plugin, open its three-dot menu,
   choose **Publish**, and select the permitted workspace roles.
3. In the target workspace, install the available plugin and start a fresh chat.
   Explicitly select it with `@`, then run the live preflight below.

This shares the plugin within that workspace; it does not put it in the public
directory. Admin policy can disable workspace publishing. See the official
[workspace publishing instructions](https://developers.openai.com/plugins/build/plugins#publish-a-local-plugin-to-your-workspace).

For ongoing GitHub-managed distribution, an admin can instead use **Admin →
Plugins → Add → Import marketplace**, supplying a repository, marketplace directory
and revision, then reviewing imported plugins and access policies. This needs the
complete extracted marketplace layout committed at a reachable revision. The
`packaging/bestfit-frequency/` templates alone are not an importable marketplace.
This alternative is not required for the local pilot. See
[workspace import and sync](https://learn.chatgpt.com/docs/enterprise/plugin-management).

If the account lacks either route, use the repository prompt below in a session
with execution tools. Report missing account/workspace capabilities rather than
assuming that another client's local installation made the plugin available.

## OpenAI public plugin directory

The owner can submit `rmc-bestfit-openai-plugin.zip` through the OpenAI
Platform Plugins dashboard using an eligible organization/project and verified
developer identity. Resolve package and skill findings, submit for review, and
publish only after approval. Metadata or skill updates require a new complete ZIP.
See the official [submission process](https://developers.openai.com/plugins/deploy/submission).

Before uploading, publish and anonymously verify the exact
[privacy-policy URL](https://github.com/USACE-RMC/RMC-BestFit/blob/main/docs/plugin-privacy.md)
declared in the manifest. A policy bundled inside the ZIP does not make this URL
publicly accessible. The policy must describe the publisher's actual practices.
The listing uses **Data & Analytics**, with analysis and plotting described in
its metadata. A valid category name does not guarantee a category-fit approval.

After publication, users can find the plugin in the shared ChatGPT/Codex directory,
install it, start a fresh chat, and invoke it with `@`. Actual surfaces and account
access remain subject to current [plugin availability](https://learn.chatgpt.com/docs/plugins).
These instructions do not claim that BestFit has been submitted, approved or
published. A skills-only package does not need developer-mode MCP registration.
Do not expose the unauthenticated local API as a public connector to compensate
for a session without execution tools.

## Claude chat

1. Enable **Code execution and file creation** under **Settings → Capabilities**.
   Team/Enterprise administrators may control skills and execution availability.
2. Open **Customize → Skills → + → Create skill → Upload a skill**.
3. Upload `rmc-bestfit-skill.zip`. It has one top-level `bestfit-frequency/`
   folder containing `SKILL.md`; do not upload a plugin or marketplace ZIP instead.
4. Enable the skill, start a fresh chat, and explicitly request `bestfit-frequency`.
5. Supply the compatible repository revision and study location/data. Test the
   synthetic input chronology and runtime prerequisites before an engineering fit.

[Official Claude skill use](https://support.claude.com/en/articles/12512180-use-skills-in-claude)
and [ZIP structure](https://support.claude.com/en/articles/12512198-how-to-create-custom-skills).
Installing a skill does not guarantee .NET installation or persistent processes in
that chat environment. Keep runtime acceptance distinct from successful upload.

## Claude Desktop plugin

1. Open **Customize → Plugins**, choose the upload option, and select
   `rmc-bestfit-claude-plugin.zip`. It has one top-level
   `rmc-bestfit-claude-plugin/` folder holding `.claude-plugin/plugin.json`
   and `skills/bestfit-frequency/`; do not upload the skill or OpenAI ZIP instead.
2. Open the installed plugin and confirm it lists the `bestfit-frequency` skill.
   An installed plugin is saved to the account, so the skill is also available in
   chat and in Claude Code; do not also upload the standalone skill.
3. Start a fresh task with the compatible repository revision and test the
   synthetic preparation/chronology workflow before an engineering fit.

[Official plugin installation](https://claude.com/docs/cowork/guide/plugins).

## Claude Code

Plugin route:

1. Extract `rmc-bestfit-claude-plugin.zip`, keeping its top-level
   `rmc-bestfit-claude-plugin` folder and hidden `.claude-plugin` folder.
2. Validate the marketplace, plugin manifest, and skill directory explicitly:

   ```sh
   claude --version
   claude plugin validate ABSOLUTE_PATH/rmc-bestfit-claude-plugin/.claude-plugin/marketplace.json --strict
   claude plugin validate ABSOLUTE_PATH/rmc-bestfit-claude-plugin/.claude-plugin/plugin.json --strict
   claude plugin validate ABSOLUTE_PATH/rmc-bestfit-claude-plugin/skills --strict
   ```

   Directory-listing URL fields require Claude Code 2.1.281 or newer.
   Skills-directory validation requires Claude Code 2.1.233 or newer. Older
   directory validation prioritizes a marketplace manifest when both manifests
   exist; validation of both from the root requires 2.1.289 or newer. Keep all three
   explicit checks and their outputs so marketplace-only success cannot be reported
   as skill validation. See the official
   [validator reference](https://code.claude.com/docs/en/plugins/cli-reference#plugin-validate).
3. Register that folder as a local marketplace:
   `claude plugin marketplace add ABSOLUTE_PATH/rmc-bestfit-claude-plugin`
   (or `/plugin marketplace add ...` inside a session).
4. Run `claude plugin install rmc-bestfit@bestfit-local`, start a new session
   in the compatible checkout, and invoke `/rmc-bestfit:bestfit-frequency`.

Standalone route instead: extract the skill folder into `~/.claude/skills/` or the
repository's `.claude/skills/` and invoke `/bestfit-frequency`. Choose one route to
avoid duplicate copies. Test preparation and chronology before fitting.
[Skills](https://code.claude.com/docs/en/skills) and
[plugin marketplaces](https://code.claude.com/docs/en/plugins/marketplace-reference).

## Anthropic public directory

Public directory submission is separate from a local Claude plugin ZIP upload.
Use the [developer portal](https://claude.ai/directory/manage), select **Submit new
→ Plugin bundle**, and supply a public GitHub repository, plugin folder and a
permanent branch or tag. The directory requires a paid Claude account and a
connected GitHub account with push access. Select the organization that should
own the listing long term before submitting.

The generated Claude archive includes a plugin-root README, privacy policy,
512-pixel PNG icon, and a manifest license and privacy-policy URL.
Publish its extracted plugin contents at the chosen durable GitHub location;
do not point the listing at a development branch that will be deleted. A full
BestFit source checkout is not a suitable directory snapshot: the directory's
repository limits are 50 MiB archived, 256 MiB unpacked and 10,000 entries.
A small distribution repository or retained distribution branch is needed.

For this renamed 0.3.3 package, extract `rmc-bestfit-claude-plugin.zip` and publish
the contents of its outer folder at the selected permanent source ref. The old
`bestfit-frequency-v0.3.2` tag identifies the previous package and must not be
moved or reused for these bytes. Select the plugin root and the new ref in the
portal. Keep application source pinned to `v2.0.1` as described in `setup.md`.

Run **Validate** in the portal, resolve blocking findings, complete the listing,
data-handling and compliance fields, then submit for review. Native
`claude plugin validate` checks do not replace this directory validation. Confirm
the portal reports **Published** before advertising directory availability.
The setup example passes a limited runtime environment to its local API child;
it does not copy ambient API keys or cloud credentials. Scanners can still flag
instructions for review. Review the actual finding and explain the data flow;
do not assert that a local validator clears a portal hold. If a required
compliance declaration describes MCP-only execution, clarify its application to
these documented skill scripts with Anthropic before acknowledging it.
See the official [publication guide](https://claude.com/docs/directory/publish)
and [directory checklist](https://claude.com/docs/plugins/pre-submission-checklist).

For either public directory, first publish the permanent `v2.0.1` source tag and
pass the release checks. The bundled [setup instructions](setup.md) use this tag
so users do not depend on a temporary release-development branch.

## Live preflight and acceptance

Use a fresh session in the named client/account/workspace. Installation, package
validation, runtime execution, and scientific acceptance are separate results.

Preflight prompt:

> Use bestfit-frequency from [installed location or supplied checkout] at [revision].
> Read SKILL.md and references/setup.md. Record the client/version, OS, workspace
> and source revision. Verify a writable terminal workspace, .NET 10 SDK, Python
> dependencies, package/source network access, a persistent child process and
> loopback HTTP. Build only the headless API against RMC.Numerics 2.2.0, start it on
> loopback, and capture /health, /api/info and the required OpenAPI contracts. Use
> the bundled synthetic input with --prepare-only, inspect and display chronology.png,
> and verify I can download its JSON and SVG. Do not fit yet. Report each unavailable
> capability and retain logs and provenance; do not substitute saved results.

After preflight passes, use this acceptance prompt:

> In this same environment, run the bundled synthetic Bulletin 17C+MGBT and Bayesian
> workflows from references/workflow.md, preserving their documented inputs and
> settings. Save the original requests, input sources, effective configuration,
> validation, results, diagnostics, PNG/SVG and runtime provenance in fresh output
> directories. Inspect and display the returned plots and provide downloadable
> artifacts. Report workflow failures and convergence warnings separately from
> scientific validity. Stop only the API process started for this check.

Retain dated results for the actual client/account. Linux CI, Windows tests, ZIP
validation, or a different client's successful run cannot stand in for this
session's process, loopback and artifact-delivery checks.

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

OpenAI distribution and Claude Code validation instructions checked against official
documentation on 2026-10-06. Named client/account installation and Linux/web runtime
execution still need their own acceptance evidence; documentation review is not a
record of those checks passing.
