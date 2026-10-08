# Agentic flood frequency analysis implementation

Approved 2026-09-22; baseline `d46907d5e6053a7c925cf1f3d03177ad271d30b2`.

Implement the reviewed plan in the existing `bestfit-frequency` skill. Stationary
univariate Bayesian analysis remains the default; B17C estimation is selected
explicitly. Official Bulletin 17C is a primary reference for **data collection and
entry in either workflow**. Preserve all numerical algorithms, seeds, defaults,
likelihoods and verification reference contracts.

1. Add REST/MCP input chronology and preserved source responses; expose the existing
   Jeffreys switch without changing its default; echo effective univariate/B17C
   configuration and reject unsupported univariate quantile-prior counts.
2. Add chronology rendering and evidence-based study preparation, regional-service
   research capture, candidate execution and comparison helpers. Retain sources,
   decisions, requests, effective settings, diagnostics and plots.
3. Teach historical completeness, perception thresholds, year conventions, regional
   skew, regional and causal quantile information using source-backed recipes.
   Distinguish the guide's three-day Blakely example from saved one-day data and
   the GEV Kamp example from the default LP3 workflow.
4. Package one maintained skill as a Claude ZIP and a thin OpenAI skills-only plugin
   with local marketplace and current installation instructions.
5. Validate deterministic API/Python contracts, four fast .NET suites, package-mode
   build, XML documentation, reproducible packages and agent scenarios. Numerical
   Verification methods run only when explicitly authorized by name. Report native
   client and Linux acceptance separately from local contract evidence.

Interfaces: API chronology uses the existing observation DTOs and model-derived
bounds/counts; Python renders without statistical recomputation. Source responses
retain original requests and raw USGS text. Candidate preparation requires explicit
annual indexes, units, evidence and dependency decisions; unknown years remain gaps.

Execution record: `artifacts/agentic-ffa/progress.md` (local). The implementation
and validation below were completed on 2026-09-22 for a scoped local commit. No
push, publication, user-profile installation or core numerical changes were made.

## Delivered contracts

- REST `/api/inputdata/{id}/chronology` and `/source`, with matching MCP
  `get_inputdata_chronology` and `get_inputdata_source` tools. Chronology uses
  model-derived values, bounds, low-outlier flags and processed threshold counts.
  Source responses preserve detached requests and available original USGS text.
- Optional univariate `useJeffreysRuleForScale`, effective univariate/B17C
  configuration snapshots, and rejection of unsupported quantile-prior counts.
- Evidence-bearing study preparation with explicit annual indexes, completeness
  rationales, source dependencies, systematic screening cohorts and candidate
  comparisons. Original evidence and failed requests/results remain in the bundle.
- Full and systematic-period chronology PNG/SVG, including ancient indexes,
  zeros, interval/uncertain bounds and aggregate threshold counts without invented
  event dates. Python does not recompute statistical results.
- B17C data-collection/entry guidance, regional-source research and capture,
  skew/MSE and quantile recipes, and the six-candidate synthetic study. Regional
  service requests use current discovered metadata; geography and applicability
  require source interpretation by the agent.
- Reproducible Claude skill ZIP and OpenAI local-marketplace ZIP from identical
  maintained skill files; [installation steps](../../skills/bestfit-frequency/references/install.md).

## Validation evidence

All .NET runs below use `UseLocalRmcNumerics=false` (RMC.Numerics 2.2.0), .NET SDK
10.0.400 and Windows. Python checks use 3.12.14 with the skill requirements installed
in an isolated, ignored artifact virtual environment.

| Check | Result |
|---|---|
| `dotnet test src/RMC.BestFit.Tests -c Debug -p:UseLocalRmcNumerics=false` | 3,434 passed |
| Same command for `RMC.BestFit.UI.Tests` | 645 passed |
| Same command for `RMC.BestFit.App.Tests` | 444 passed |
| Same command for `RMC.BestFit.Api.Tests` | 521 passed |
| `python -m unittest discover -s tests/skills` | 30 passed |
| `dotnet build src/RMC.BestFit.Api -c Release -p:UseLocalRmcNumerics=false` | 0 warnings, 0 errors |
| `scripts/validate-code-xml-docs.ps1 -Configuration Debug`, package dependency selected | Passed; 954 source files, strict project builds |
| Package source parity, stable archive hashes and ZIP integrity | Passed |
| Official plugin-creator manifest validator on extracted marketplace plugin | Passed |
| Live Release API OpenAPI, REST and MCP contracts | Passed |
| All six synthetic candidates with `run_study.py --prepare-only` | Passed; chronology rendered before fitting |

Local evidence is under `artifacts/agentic-ffa/`; the archives and SHA-256 sidecars
are under `artifacts/`. The live harness started and stopped its own loopback
process. It exercised MGBT/input construction and unrun analysis configuration;
no estimator or `/run` endpoint was invoked. The XML gate built Verification but
did not execute its tests. No numerical Verification methods were run.

The full chronology and systematic zoom were visually inspected. Endpoint marker
clipping in the zoom was corrected with half-year display padding. An initial
REST/MCP parity assertion included transport-only timing fields; the corrected
test compares the complete scientific payload after removing those fields.

Independent read-only review identified historical exact records bypassing the
screening-cohort guard and misspelled nested analysis options passing preparation.
Both were reproduced by failing tests and fixed. Final behavioral acceptance
scenarios are recorded in [the scenario matrix](../../tests/skills/agent-scenarios.md).

## Execution decisions and acceptance limits

- Used the existing clean feature checkout and retained unrelated worktrees.
  The PowerShell-native local ledger replaces shell-specific skill scaffolding;
  reviewable logs and artifacts are retained for handoff.
- Used published guide/verification references plus saved examples and current
  code because the original Word files were unavailable. The information recipes
  distinguish differing flow durations and saved versus verification settings.
- Kept regional-service geography, IDs, schemas and uncertainty definitions
  discoverable from current primary sources instead of embedding guesses. The
  capture helper preserves evidence; it does not certify source applicability.
- Native ChatGPT/Codex/Claude installation, Linux/web execution, live regional
  predictions for a real site and new numerical FFA runs were not acceptance-tested.
  These results establish Windows API/helper/package behavior, not statistical
  adequacy or final engineering adoption of a study.

Distribute both a skill package and a compatible API source revision. A local
commit is not reachable by a remote chat session until the owner shares a matching
snapshot or publishes the revision. No unauthenticated API hosting is required
for a session that can run the headless process over its own loopback.
