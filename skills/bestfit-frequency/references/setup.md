# Repository checkout and session-local API setup

Use an existing RMC-BestFit checkout if supplied. Otherwise clone the official
`https://github.com/USACE-RMC/RMC-BestFit.git` repository into the task workspace.
Use a reachable revision that includes this skill and the API changes it documents,
or a supplied matching source snapshot. Verify its contents rather than assuming
the default branch or an application version identifies the required contracts.
The ZIP contains instructions/scripts, not BestFit binaries or unpublished commits.

For ChatGPT or Claude web use, the checkout, .NET host, Python client, and renderer
all run inside the session's execution environment. `127.0.0.1` refers to that
environment, not the user's computer. A repository connection that only reads files
is insufficient: check for a terminal, writable disk, dependency network access,
and support for a persistent child process and loopback HTTP before starting.

When no checkout is supplied, use a new directory:

```sh
git clone https://github.com/USACE-RMC/RMC-BestFit.git
cd RMC-BestFit
git rev-parse HEAD
```

Use the user's specified compatible revision when supplied. Confirm the checkout
contains `skills/bestfit-frequency/SKILL.md`, the bundled scripts, and the API
features described below. If those files/features are missing from the cloned
revision, report the source-availability boundary and request a compatible source
snapshot/revision. A local developer's uncommitted files are not available through
the repository URL. Keep supplied checkouts and unrelated dirty work intact.

Check `dotnet --list-sdks`, `python --version` (or `python3`/`py`), repository
`AGENTS.md`, and `git status --short`. .NET **10 SDK** is required; build only the
portable API project, not the Windows desktop solution. Install missing runtimes
within the session using the environment's supported setup mechanism. If runtime
installation or package networking is unavailable, report it instead of substituting
a Python fit.

Create a virtual environment, then use that interpreter for all commands:

```sh
python -m venv .venv-bestfit
# POSIX interpreter: .venv-bestfit/bin/python
# Windows interpreter: .venv-bestfit/Scripts/python.exe
python -m pip install -r SKILL_DIR/requirements.txt
```

Here and below, replace `python` with the virtual-environment interpreter and
`SKILL_DIR` with the installed skill directory. The scripts use the noninteractive
matplotlib Agg backend and require no GUI/display server.

From the BestFit repository root, build:

```sh
dotnet build src/RMC.BestFit.Api -c Release -p:UseLocalRmcNumerics=false
```

`UseLocalRmcNumerics=false` is the default: the repository builds against the
published RMC.Numerics package unless a build opts into a sibling Numerics checkout
with `-p:UseLocalRmcNumerics=true`. Passing it explicitly keeps the command correct
on older checkouts that selected a sibling checkout automatically.
Inspect `Directory.Packages.props` and `src/RMC.BestFit/obj/project.assets.json`
to record the requested/resolved RMC.Numerics version. This implementation uses
the owner-approved **2.2.0** package. An older 2.1.4 pin fails against current code
using `OptimizationStatus.LineSearchFailed` and `IStandardError.LogAbsQuantileJacobian`;
use a compatible checkout instead of silently changing its numerical dependency.
An explicitly authorized source build must record the Numerics commit and dirty
state too.

Start the compiled API with a loopback binding, retaining the process handle/PID
and log. Set `ASPNETCORE_ENVIRONMENT=Development` so the local HTTP endpoint is not
redirected to HTTPS. Run from the compiled DLL's directory so its copied
`appsettings.json` files are loaded. From the repository root, a terminal-capable
agent can use Python:

```python
import os, subprocess
from pathlib import Path
api_dll = Path("src/RMC.BestFit.Api/bin/Release/net10.0/RMC.BestFit.Api.dll").resolve(strict=True)
env = dict(os.environ, ASPNETCORE_ENVIRONMENT="Development")
log = open("bestfit-api.log", "w", encoding="utf-8")
flags = subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0
server = subprocess.Popen([
    "dotnet", str(api_dll),
    "--urls", "http://127.0.0.1:5210"
], cwd=api_dll.parent, env=env, stdout=log, stderr=subprocess.STDOUT,
    creationflags=flags)
# Keep this process handle. Poll /health with a bounded wait (e.g., 30 seconds).
# When finished, in a finally block:
# server.terminate(); server.wait(timeout=30); log.close()
```

On PowerShell, set `$env:ASPNETCORE_ENVIRONMENT='Development'` and use
`Start-Process ... -PassThru -WindowStyle Hidden` with the absolute DLL path,
`-WorkingDirectory` set to its parent directory, and redirected logs, or the
host's managed background-command tool. If port 5210 is occupied, choose another
loopback port and pass the matching `--base-url` to the client. Do not kill the
existing listener or all `dotnet` processes. Do not expose this development host
on a public network; this workflow has no account/authentication setup.

Verify `GET /health` and `GET /api/info`. In Development, the OpenAPI schema is
`/openapi/v1.json`; confirm input `/chronology` and `/source` paths,
`useMultipleGrubbsBeckTest`, `useJeffreysRuleForScale`, and analysis `configuration`
exist. The source revision must include these contracts. The API store is in
memory and disappears on restart. Keep it alive until input and results are saved.

The Python helpers call REST in this same environment and normally send no `Origin`
header. All API routes, including REST, `/mcp` and health/schema endpoints, reject
untrusted browser origins with HTTP 403 in Development and Production. Requests
without `Origin` remain allowed. Automatic same-origin trust is limited to a
literal localhost/loopback authority matching the request scheme, host and
effective port; a matching arbitrary DNS hostname is not implicitly trusted.

For a separately authorized browser client, list each exact origin in the
`Cors:AllowedOrigins` array, or set environment entries such as
`Cors__AllowedOrigins__0=https://client.example`. Use only the scheme, host and
optional port: no wildcard, trailing slash/path, query, fragment or credentials.
The configured origin must match, with case/default-port normalization; subdomains
are not included automatically. The shipped `https://localhost` entry is retained.
Origin checks are not authentication: native clients can omit the header. Keep this
development workflow bound to loopback; it does not provision a hosted service.

Retain server-side provenance beside each run: BestFit commit/dirty state,
`dotnet --info`, resolved Numerics version (and source commit if used), build log,
and the matching `.deps.json`. The client saves Python/package versions, API info,
defaults, requests, resource IDs, validation, and results. API version alone does
not identify the exact source or numerical dependency.
