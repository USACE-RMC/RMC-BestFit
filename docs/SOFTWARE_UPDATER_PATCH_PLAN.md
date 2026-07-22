# Software Updater Metadata Repair and Patch Plan

## Status

This document records the July 2026 diagnosis and repair of the RMC-BestFit
desktop updater, and defines the deferred WPF Framework 1.0.5 and RMC-BestFit
2.0.1 patches. The immediate repair is release metadata only. The v2.0.0 tag,
release asset, and application binaries must not be replaced.

## Confirmed diagnosis

The v2.0.0 update failure has two independent causes.

1. **Missing checksum metadata.** RMC-BestFit configures
   `RequireSha256Checksum = true`. WPF Framework 1.0.4 populates
   `UpdateInfo.Sha256Checksum` only by finding a token in the GitHub release
   body with the form `SHA256: <64 hexadecimal characters>`. The v2.0.0
   release body did not contain that token, so `DownloadUpdateAsync` rejected
   the update before starting the network transfer.
2. **Duplicate update prompt.** `App.AutoCheckForUpdatesAsync` checks for an
   update and displays the first confirmation. On Yes, it raises the
   `CheckForUpdatesMenuItem` click event. The FrameworkUI menu handler performs
   another GitHub check and displays its own confirmation, producing the
   redundant second "Update Available" dialog.

The updater-related files did not change between `v2.0-beta.5` and `v2.0.0`.
The duplicate prompt is therefore an existing integration defect, while the
download failure is caused by the published v2.0.0 release metadata.

## Verified v2.0.0 desktop asset

The published asset was independently downloaded and inspected on 2026-07-22.

| Property | Verified value |
|---|---|
| Asset | `RMC-BestFit.v2.0.0.zip` |
| Size | `13,437,994` bytes |
| SHA-256 | `95c61f4b538636c47d5578118bebd56c56d7118462a22093359164c75c5a0bb9` |
| GitHub asset digest | `sha256:95c61f4b538636c47d5578118bebd56c56d7118462a22093359164c75c5a0bb9` |
| Zip entries | `90` |

The required root-level update payload is present:

- `RMC-BestFit.exe`
- `SoftwareUpdate.Updater.exe`
- `SoftwareUpdate.Updater.dll`
- `SoftwareUpdate.Updater.deps.json`
- `SoftwareUpdate.Updater.runtimeconfig.json`

The zip is valid and must not be replaced. Replacing an asset would change its
checksum and introduce an unnecessary binary provenance change.

## Immediate v2.0.0 metadata repair

Append the following section exactly once to the existing GitHub release body:

```markdown
## Desktop Application Download Verification

SHA256: 95c61f4b538636c47d5578118bebd56c56d7118462a22093359164c75c5a0bb9
```

Do not change the release tag, target commit, asset, or release classification.
After editing the release, validate the live metadata and bytes from the
`fix-software-updater` worktree:

```powershell
.\scripts\Test-DesktopRelease.ps1 -Tag v2.0.0
```

The script must report one matching asset, matching release-note and GitHub
digests, a matching downloaded-file hash, and all five required payload files.
It downloads only to an owned temporary directory, never extracts or launches
the update, and deletes only that owned directory when finished.

This repair allows Framework 1.0.4 checksum enforcement to pass. Users running
v2.0-beta.5 will still see the two existing update confirmations until the
formal code patches below are released.

## Deferred WPF Framework 1.0.5 patch

### Branch and release sequence

1. Fetch `origin` in `C:\GIT\wpf-framework`.
2. Update the active `bug-fixes-and-enhancements` branch with the latest
   `origin/main` before editing.
3. Implement and validate the updater changes on that release branch.
4. Prepare and publish all coordinated `RMC.Wpf.Framework.*` 1.0.5 packages
   before changing BestFit to consume them.

### Checksum metadata behavior

- Add the GitHub release-asset `digest` JSON property to
  `GitHubReleaseAsset`.
- Resolve SHA-256 metadata from both the selected asset and release body:
  - Accept an asset digest only when it has the exact form
    `sha256:<64 hexadecimal characters>`.
  - Retain the existing release-body parser as a compatibility fallback when
    no supported asset digest is supplied.
  - Normalize accepted hashes to lowercase.
  - When both sources provide valid SHA-256 values, require them to match and
    fail the update check on disagreement.
  - When neither source provides a valid SHA-256 value, preserve the current
    `RequireSha256Checksum` failure before download.
- Continue validating downloaded bytes against the resolved checksum.

GitHub's asset digest is the preferred source because it is associated with
the exact uploaded asset. Release-body support remains necessary for older
GitHub environments and existing release processes.

### Update-flow API

- Change the existing
  `FrameworkUI.MainWindow.DownloadAndInstallUpdateAsync(UpdateInfo update)`
  continuation from private to public.
- Keep the method responsible for download progress, checksum verification,
  dirty-state/save handling, updater launch, and restart.
- Keep `CheckForUpdates_Click` responsible for checking and displaying the
  manual Tools-menu confirmation, then delegate its accepted update to the
  public continuation.
- Document that applications which already checked and prompted must call the
  continuation directly instead of raising the Tools-menu click event.

### Framework test requirements

- Add an internal `HttpClient` or `HttpMessageHandler` injection seam so tests
  exercise the real GitHub JSON-to-`UpdateInfo` mapping and download path
  without internet access.
- Cover digest-only metadata, release-body fallback, matching dual sources,
  conflicting dual sources, malformed/unsupported digests, missing required
  metadata, matching bytes, and byte-hash mismatch.
- Verify the known-update continuation is publicly callable and does not
  perform another update check.
- Update `docs/software-update.md` and compiled documentation snippets for the
  digest precedence and continuation API.

Run the repository gates required by WPF Framework:

```powershell
dotnet restore WPF-Framework.sln
dotnet build WPF-Framework.sln -c Release /p:Version=1.0.5
$env:VSTEST_CONNECTION_TIMEOUT = '600'
dotnet test WPF-Framework.sln -c Release --no-build
.\scripts\validate-code-xml-docs.ps1 -Configuration Debug
.\scripts\pack-wpf-framework.ps1 -Configuration Release -Version 1.0.5 -OutputDirectory artifacts/packages
```

Inspect all four packages for version 1.0.5, intended release notes, updater
payload, and the approved `RMC.Numerics` dependency range before publishing.

## Deferred RMC-BestFit 2.0.1 patch

### Prerequisite

Do not commit a public-package dependency on WPF Framework 1.0.5 until all four
1.0.5 packages are available from the configured NuGet source.

### Startup update coordinator

- Update all `RMC.Wpf.Framework.*` dependencies to 1.0.5.
- Extract the startup decision logic from `App.xaml.cs` into an internal,
  deterministic coordinator with injected prompt and continuation delegates
  or an equivalent narrow view interface.
- Perform exactly one `CheckForUpdateAsync` call.
- Return silently for failed checks, no update, or an already-skipped version.
- Display exactly one startup "Update Available" prompt.
- On Yes, pass the returned `UpdateInfo` directly to
  `MainWindow.DownloadAndInstallUpdateAsync`.
- On No, call `SkipVersion` for the offered version.
- Remove the `FindName("CheckForUpdatesMenuItem")` lookup and routed click-event
  raise. Do not duplicate the FrameworkUI download/install implementation in
  BestFit.

### BestFit tests

Use a fake update service and fake prompt/continuation to prove:

- An available, unskipped update performs one check, one prompt, and one
  continuation call with the same `UpdateInfo` instance.
- Declining performs one check and one prompt, records the skipped version,
  and does not start the continuation.
- Unavailable, skipped, and failed results do not prompt or continue.
- Exceptions remain non-fatal to application startup.
- Release metadata resolves the running application as 2.0.1 and continues to
  enforce SHA-256 validation.

### Versioning and release automation

- Synchronize application, assembly, package, UI/project, citation, CodeMeta,
  and release-metadata test values to 2.0.1.
- Generalize GitHub workflows that currently hard-code 2.0.0 so a validated
  `v2.0.1` release can publish the model package.
- Produce `RMC-BestFit.v2.0.1.zip` only from the validated release build.
- Compute its SHA-256 after the final zip is created.
- Put exactly one `SHA256: <hash>` token in the v2.0.1 release body and run
  `Test-DesktopRelease.ps1 -Tag v2.0.1` after publishing.

The release-body token remains mandatory even after Framework 1.0.5 supports
GitHub's asset digest: v2.0-beta.5 and v2.0.0 clients use Framework 1.0.4 and
can update to v2.0.1 only through the release-body checksum parser.

Run the BestFit public validation gates, including all fast test projects and
the full XML documentation validator. Never run `RMC.BestFit.Verification`
from an agent.

## Completion criteria

The immediate repair is complete when the live v2.0.0 release passes
`Test-DesktopRelease.ps1` without changing the asset or tag. The formal patch
is complete only after WPF Framework 1.0.5 is published, BestFit 2.0.1 consumes
it, deterministic tests prove a single startup prompt/check, and the v2.0.1
release passes the same live release verification.
