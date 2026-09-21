# BestFit frequency skill roadmap

Agreed direction, 2026-09-19: the owner will open a PR from the current branch to
`main`. This roadmap prepares the repository-based skill for use after that PR.
It does not authorize publication, client-profile changes, or additional execution.

## Intended user experience

The user gives a capable ChatGPT or Claude session the
[repository URL](https://github.com/USACE-RMC/RMC-BestFit), observations or a USGS
site, and the requested analysis. The session clones a compatible revision, reads
`skills/bestfit-frequency/SKILL.md`, builds the headless API with the approved
RMC.Numerics 2.2.0 package, and runs the API and Python client in the same execution
environment. The API binds to loopback. Matplotlib plots the returned coordinates,
and the session presents PNG, SVG, results JSON, provenance, and relevant warnings.

No separately hosted BestFit service, public API endpoint, or MCP connector is
required. Docker is an available choice for local Linux validation; the web
session itself needs a working .NET/Python runtime, not a Docker installation.

## Remaining milestones

| Milestone | Action | Acceptance evidence |
|---|---|---|
| Owner PR to `main` | Include the skill, scripts, API changes, approved package pin, and their existing tests/docs together. Preserve unrelated dirty work when selecting the PR diff. | A reachable repository revision contains the documented MGBT and plotting fields, with RMC.Numerics pinned to 2.2.0. The skill ZIP alone does not distribute uncommitted API code. |
| Linux runtime check | First identify an existing Linux target. Build only `src/RMC.BestFit.Api` with `UseLocalRmcNumerics=false`; run the bundled synthetic B17C+MGBT and Bayesian workflows with their existing settings. | Build/runtime versions, assets and `.deps.json` resolve 2.2.0; both workflows complete; requests, warnings, results, PNG/SVG and server provenance are retained; plots are inspected. |
| Web-session execution | First name the actual client/account/workspace. Use a fresh session to follow the repository prompt in `references/install.md`. | That session can clone the compatible revision, use a writable terminal workspace, obtain .NET 10/Python dependencies, retain an API child process, call loopback HTTP, and present/download the resulting artifacts. Record unsupported capabilities honestly. |
| Optional native discovery | When desired, identify the target profile and use its supported skill installation mechanism. Test explicit invocation and, separately, automatic discovery if supported. | The installed skill appears in that profile and is actually read when invoked. A repository link or a different client's loader result does not establish native installation. |
| Distribution refresh | Repackage only when bundled skill contents change. Keep the source revision requirement visible. | ZIP contents match source, references resolve, CRC/checksum pass, and repeated packaging is deterministic. Publishing or attaching a release asset remains the owner's separate action. |

The Linux and web-session checks remain future acceptance work under this clarified
roadmap scope. Windows implementation/validation and the earlier temporary Codex
loader check are already recorded in the
[handoff](bestfit-frequency-skill-handoffs.md); they do not need to be repeated just
to prepare this roadmap.

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

The feature remains uncommitted in this checkout, so a fresh upstream clone cannot
be assumed to contain it yet. Linux execution and a specific web-client runtime
have not been validated. A product name, repository link, uploaded ZIP, or native
loader result cannot certify those capabilities. No hosting work is needed for
the agreed workflow; missing execution capabilities are reported as limitations.
