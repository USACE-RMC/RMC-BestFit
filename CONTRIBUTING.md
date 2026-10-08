# Contributing to RMC-BestFit

Thank you for your interest in contributing to RMC-BestFit. We welcome bug reports, feature requests, documentation improvements, validation results, and other feedback from the community.

## Review Capacity

RMC-BestFit is maintained by a small team within the U.S. Army Corps of Engineers Risk Management Center (USACE-RMC). Our capacity to review external pull requests is limited. We prioritize issues and bug reports, which are always welcome and will be reviewed as resources permit.

If you plan to submit a pull request, please open an issue first to discuss your proposed change. This helps avoid duplicated effort and ensures the contribution aligns with the project's direction.

## How to Contribute

### Report a Bug

If you find a bug, please open a bug report and include:

- Steps to reproduce the problem
- Input data and configuration, if applicable
- Expected behavior and actual behavior
- Software version, operating system, and .NET SDK version
- Relevant error messages, logs, screenshots, or small sample files

### Request a Feature

Feature requests are welcome. Please open a feature request describing:

- The use case or problem you are trying to solve
- How you envision the feature working
- How important the request is to your work
- Relevant statistical methods, guidance documents, or published literature

### Report Validation Results

Given the life-safety applications of this software, independent validation is especially valuable. If you have compared RMC-BestFit results against R packages, published tables, analytical solutions, or other flood-frequency software, please share the result through an issue.

### Submit a Pull Request

Pull requests may take several weeks or longer to review. Before submitting code:

1. Open an issue first to discuss the proposed change.
2. Keep changes focused and avoid unrelated formatting or refactoring.
3. Preserve the public API unless the issue explicitly discusses a breaking change.
4. Include XML documentation for public types and members.
5. Include unit tests for new model-library behavior.
6. Use MSTest for tests in this repository.
7. Ensure a clean build with zero errors and zero warnings.
8. For model-library changes, run the public model test project locally.

## Binary Assets

Binary files stay in the repository history permanently, so they are committed sparingly:

- The technical reference and verification report PDFs in `output/pdf/` are regenerated and committed once per release, at the release's source checkpoint. `output/pdf/SHA256SUMS` records their final bytes separately from the metadata embedded in each PDF. Retained PDFs remain explicitly dated snapshots until a reviewed rebuild.
- Example projects (`examples/**/*.bestfit`) are recommitted only when their content changes, for example a corrected fixture or a project-format migration, not after an incidental open and save.
- Release packages and desktop zips are not committed. They are attached to the GitHub release, and the release description carries the desktop zip's SHA-256 checksum.

## Public Repository Content

Commit maintained application code, tests, user documentation, reproducible scientific evidence and distribution sources. Keep plans, assistant handoffs, session logs, temporary investigations and build/release staging under ignored `artifacts/` on the developer's machine. `plans/` and `superpowers/` directories are ignored at every level. Ignoring a path does not remove an already tracked file: remove it from tracking and repair its public references before committing.

New public documentation should have a defined audience and an entry in the relevant index or README. Retained scripts should have a documented repeatable purpose. Preserve frozen numerical evidence and its original source identities even when the experiment is historical. Do not replace historical hashes or test outcomes with current ones without performing and recording a new measurement.

## Maintainer Tooling

Run commands from the repository root. Normal development uses .NET 10, Python and, for report builds, Node.js, LaTeX/dvisvgm and Chrome or Edge. Report wrappers accept explicit executable paths and a Node module directory; they also support normal `PATH` discovery. Install the Node `markdown-it` package in the repository's ignored `node_modules`, or select an existing module directory with `-NodeModulesPath` or `BESTFIT_NODE_MODULES`. Python figure dependencies are listed in `skills/bestfit-frequency/requirements.txt`; example page previews additionally need `markdown-it-py`.

| Task | Maintained command or guidance |
|---|---|
| Required fast tests | `dotnet test src/<project>/<project>.csproj -c Release` for `RMC.BestFit.Tests`, `RMC.BestFit.UI.Tests`, `RMC.BestFit.App.Tests` and `RMC.BestFit.Api.Tests` |
| XML documentation | `scripts/validate-code-xml-docs.ps1` |
| Verification catalog | `scripts/validate-verification-catalog.ps1 -Catalog docs/verification/verification-catalog.json -SourceRoot src/RMC.BestFit.Verification -RequireComplete`; validator regressions: `scripts/test-verification-catalog-validator.ps1` |
| Scoped numerical evidence | [Verification execution and evidence policy](docs/verification/README.md); use `scripts/run-verification-test.ps1` for a single named run of record |
| Technical reference | [Report build and source metadata](docs/technical-reference/index.md#peer-review-release-artifacts); `scripts/build-technical-reference-book.ps1` |
| Verification report | [Build and review](docs/verification/README.md#build-and-review); `scripts/build-verification-report.ps1` |
| Examples and figures | [Example maintenance](tools/ExampleDocumentation/README.md); ordinary validation must not use `--refresh` on preserved figures |
| Skill tests and packages | `python -m pytest tests/skills -q -p no:cacheprovider`; `python scripts/package-bestfit-skill.py`; `python scripts/smoke-bestfit-plugin.py` |

Use `python scripts/generate-technical-reference-bibliography.py --check` and `python scripts/build-verification-coverage.py --check` to check generated publication inputs without rewriting them. Report finalizers, equation validators and PDF audit helpers are maintained parts of the report build pipeline. Do not run numerical estimators merely to refresh documentation or repository housekeeping.

### Desktop Release Preparation

1. Validate the intended source commit and the version in `Directory.Build.props`. Keep explicit published dependency pins in `Directory.Packages.props`; release builds use `-p:UseLocalRmcNumerics=false`. Confirm every required package version is available from NuGet.org. The desktop additionally requires the managed HEC-DSS assembly and native libraries referenced by `Directory.Build.props`.
2. Run the four fast suites and the applicable documentation, package and release checks. Build the desktop in Release configuration from that source commit. Preserve its root application/updater files and its `libraries/` and `runtimes/` directory layout. The NuGet and plugin workflows do not build the desktop ZIP.
3. Stage release files outside Git. Create the final `RMC-BestFit.vX.Y.Z.zip`, then hash those exact bytes. Attach exactly one asset matching `RMC-BestFit.*.zip` to the release draft.
4. Put exactly one `SHA256: <64 hexadecimal characters>` line in the release body for that desktop ZIP. Current and older supported updater clients read this body token. Keep other asset hashes in `SHA256SUMS` or individual sidecars, not additional body tokens. Compare the body checksum with the local archive, GitHub's asset digest and an independently downloaded copy; inspect the packaged versions and root updater payload.
5. Point the draft to the intended release commit and versioned notes under `docs/release-notes/`. Ensure that commit has been pushed before creating the tag or publishing. Preserve accurate known limitations; pinning a dependency is not proof that a deferred repair shipped.
6. Publication is a separate maintainer action. The publication event starts the NuGet and plugin-package workflows. Leave the four plugin ZIP names and their sidecars for the plugin workflow, which intentionally refuses to overwrite existing assets. Inspect both Windows and Linux package validation results and NuGet trusted-publishing configuration before publication.
7. After authorized publication, run `scripts/Test-DesktopRelease.ps1 -Tag vX.Y.Z` and perform installed-client upgrade acceptance. The script rejects drafts; before publication use the equivalent metadata, downloaded-byte and payload checks described above. A prepublication check cannot establish that the installed client's live upgrade succeeded.

## Coding Standards

RMC-BestFit is used for flood risk and life-safety engineering decisions. Code should be explicit, well-tested, and numerically defensive.

- Prefer clear validation at method entry.
- Use `double.NegativeInfinity` for impossible log-likelihood values.
- Guard logarithms, divisions, transformations, and integration bounds.
- Keep statistical notation clear in names and comments.
- Add comments for non-obvious numerical or domain decisions, not for routine assignments.
- Do not suppress XML documentation warnings to pass a build.

## Developer Certificate of Origin

By submitting a pull request, you certify under the [Developer Certificate of Origin (DCO) Version 1.1](https://developercertificate.org/) that you have the right to submit the work under the license associated with this project and that you agree to the DCO.

All contributions will be released under the same license as the project. See [LICENSE](LICENSE).

## Federal Government Contributors

U.S. Federal law prevents the government from accepting gratuitous services unless certain conditions are met. By submitting a pull request, you acknowledge that your services are offered without expectation of payment and that you expressly waive any future pay claims against the U.S. Federal government related to your contribution.

If you are a U.S. Federal government employee and use a `*.mil` or `*.gov` email address, your contribution is understood to have been created in whole or in part as part of your official duties and is not subject to domestic copyright protection under 17 USC 105.

## Security

If you discover a security vulnerability, please do not open a public issue. See [SECURITY.md](SECURITY.md) for reporting guidance.

## License

RMC-BestFit is released under the [Zero-Clause BSD (0BSD)](LICENSE) license.