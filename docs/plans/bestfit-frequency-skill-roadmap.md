# BestFit frequency skill roadmap

Agreed direction, 2026-09-19; release-readiness guidance refreshed 2026-10-06.
The owner controls the PR to `main`, release publication, and client/workspace
installation. This roadmap distinguishes repository/package readiness from a
successful run in a named client. It is not a publication or runtime-validation
receipt.

## Intended user experience

The user gives a capable ChatGPT or Claude session the
[repository URL](https://github.com/USACE-RMC/RMC-BestFit), observations or a USGS
site, and the requested analysis. The session clones a compatible revision, reads
`skills/bestfit-frequency/SKILL.md`, builds the headless API with the approved
RMC.Numerics 2.2.0 package using the .NET 10 SDK, and runs the API and Python client
in the same execution environment. The Python helpers use REST over loopback;
MCP is optional. Matplotlib plots the returned coordinates,
and the session presents PNG, SVG, results JSON, provenance, and relevant warnings.

No separately hosted BestFit service, public API endpoint, or MCP connector is
required. Docker is an available choice for local Linux validation; the web
session itself needs a working .NET/Python runtime, not a Docker installation.

## Remaining milestones

| Milestone | Action | Acceptance evidence |
|---|---|---|
| Compatible source publication | Include the skill, scripts, API changes, approved package pin, and their tests/docs together in the owner's PR. | Verify the selected reachable revision contains the documented MGBT, chronology, source and plotting contracts and the RMC.Numerics 2.2.0 pin. A ZIP does not publish API source or local commits. |
| Package contract gate | Run `python -m pytest tests/skills`; build all four archive formats and SHA-256 sidecars. Validate Claude marketplace, plugin manifest and skills explicitly. | Contents match source, references/assets resolve, CRC and sidecars pass, and repeated packaging is deterministic. Record the validator version and each target checked. |
| Linux runtime check | First identify an existing Linux target. Build only `src/RMC.BestFit.Api` with `UseLocalRmcNumerics=false`; run the bundled synthetic B17C+MGBT and Bayesian workflows with their existing settings. | Build/runtime versions, assets and `.deps.json` resolve 2.2.0; both workflows complete; requests, warnings, results, PNG/SVG and server provenance are retained; plots are inspected. |
| Named client execution | Name the actual client/account/workspace and OS. Use a fresh session for the preflight and acceptance prompts in `references/install.md`. | That session can obtain the compatible revision, use a writable terminal workspace, obtain .NET 10/Python dependencies, retain an API child process, call loopback HTTP, and present/download artifacts. Record unsupported capabilities honestly. |
| Private workspace pilot | Register the extracted local marketplace, install in the desktop client, then have a permitted workspace admin publish it through ChatGPT Plugins → Personal. | The intended workspace roles can install/invoke the skill in a fresh chat and pass its own runtime acceptance. Local discovery alone is insufficient. |
| Release assets | The `PluginPackages.yml` workflow validates Windows/Linux API and Python contracts, exercises packaged synthetic workflows, and builds the four ZIPs and sidecars. Manual dispatch validates; a successful published `vX.Y.Z` release run attaches assets. | Inspect the actual run, artifact contents/checksums, and release attachment results. Workflow configuration does not prove a run succeeded, and it does not create or publish a release. |
| Public directory submission | Owner submits the direct OpenAI plugin ZIP, resolves checks, and completes review before publishing. | Record the actual submission/review/publication state and test discovery in the intended account; a GitHub release is not directory publication. |

Linux and named web/client checks require fresh evidence from their own targets.
Earlier Windows implementation/validation and the temporary Codex loader check are
recorded in the [handoff](bestfit-frequency-skill-handoffs.md). Preserve those dated
receipts; they neither validate a later package nor certify another environment.

## Distribution identity

Plugin **0.3.2** and application/API **2.0.1** have independent versions; the
numerical dependency remains **RMC.Numerics 2.2.0**. The packager emits a standalone
skill, an OpenAI local marketplace, a direct OpenAI plugin ZIP, and a Claude plugin.
The direct OpenAI ZIP has `.codex-plugin/plugin.json`, `skills/`, and `assets/` at
its root. The local marketplace ZIP contains a nested plugin and is used for local
registration. Use the [installation guide](../../skills/bestfit-frequency/references/install.md)
to select the correct archive and current private/public distribution route.

## Skill instructions to retain

- Keep the repository URL and same-session clone/build/run flow near the top of
  `SKILL.md`; detailed commands stay in `references/setup.md`.
- Preserve default Bayesian selection, explicit B17C/MGBT behavior, manual flags,
  numerical settings, priors, seeds, uncertainty settings, and all supplied data.
- Require the matching source features and approved numerical dependency before
  fitting. If the revision or runtime is unavailable, report the specific boundary.
- Keep the server alive until the client saves input, validation, final state, and
  results. Stop only the process started by this workflow.
- Plot only the API-returned coordinates, retain display omissions and diagnostic
  warnings, inspect the PNG, and use the host's actual attachment mechanism.
- Report workflow success, convergence concerns, and scientific validation as
  separate conclusions. The synthetic examples demonstrate integration.

## Current limits

Check source availability at the revision supplied to the user; neither a local
commit nor generated ZIP establishes that it is reachable upstream. This roadmap
does not claim successful Linux CI, client/account installation, web execution,
private workspace sharing, or public directory publication. A product name,
repository link, uploaded ZIP, or native loader result cannot certify those
capabilities. No hosting work is needed for the agreed workflow; missing execution
capabilities are reported as limitations.
