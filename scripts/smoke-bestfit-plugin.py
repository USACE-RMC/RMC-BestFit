"""Exercise an extracted release plugin against an owned, loopback-only BestFit API.

This is release integration evidence, not numerical verification or scientific acceptance.
The bundled synthetic inputs and all analysis defaults remain unchanged.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import importlib
import importlib.metadata
import json
import os
from pathlib import Path, PurePosixPath
import platform
import shutil
import socket
import stat
import subprocess
import sys
import time
import traceback
from urllib.error import HTTPError, URLError
from urllib.request import Request, urlopen
import zipfile


ROOT = Path(__file__).resolve().parents[1]


def write_json(path, value):
    """Persist a receipt without losing JSON precision to a console transcript."""
    Path(path).write_text(json.dumps(value, indent=2, ensure_ascii=False, allow_nan=False) + "\n",
                          encoding="utf-8")


def sha256(path):
    """Identify the exact artifact bytes used by the integration run."""
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def command_receipt(command, cwd=ROOT):
    """Capture a bounded, read-only environment command without invoking a shell."""
    result = subprocess.run(command, cwd=cwd, capture_output=True, text=True, timeout=30,
                            creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
    return {"command": command, "exitCode": result.returncode,
            "stdout": result.stdout, "stderr": result.stderr}


def extract_plugin(path, destination):
    """Validate every ZIP path and the submission root before writing any archive member."""
    with zipfile.ZipFile(path) as archive:
        members = archive.infolist()
        names = [member.filename for member in members]
        if len(names) != len(set(names)):
            raise ValueError("Plugin ZIP contains duplicate member names")
        for member in members:
            name = member.filename
            parts = name.rstrip("/").split("/")
            if (not name or "\\" in name or ":" in name or "\x00" in name
                    or PurePosixPath(name).is_absolute() or any(part in ("", ".", "..") for part in parts)
                    or stat.S_ISLNK(member.external_attr >> 16)):
                raise ValueError(f"Unsafe plugin ZIP member: {name!r}")
            if parts[0] not in (".codex-plugin", "skills", "assets"):
                raise ValueError(f"Unexpected submission ZIP root: {parts[0]}")
        manifest_name = ".codex-plugin/plugin.json"
        if manifest_name not in names or "skills/bestfit-frequency/SKILL.md" not in names:
            raise ValueError("Expected a plugin-root OpenAI submission ZIP, with its maintained skill")
        manifest = json.loads(archive.read(manifest_name))
        if (manifest.get("name") != "bestfit-frequency" or manifest.get("skills") != "./skills/"
                or "mcpServers" in manifest):
            raise ValueError("Expected the skills-only bestfit-frequency plugin manifest")
        for field in ("logo", "composerIcon"):
            relative = manifest.get("interface", {}).get(field, "").removeprefix("./")
            if not relative or relative not in names:
                raise ValueError(f"Plugin {field} asset is missing from the ZIP")
        bad = archive.testzip()
        if bad:
            raise ValueError(f"Plugin CRC failed for {bad}")
        destination.mkdir()
        inventory = {}
        for member in members:
            target = destination.joinpath(*PurePosixPath(member.filename).parts)
            if member.is_dir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                target.write_bytes(archive.read(member))
                inventory[member.filename] = sha256(target)
        return manifest, inventory


def request(base, path, *, method="GET", body=None, headers=None, timeout=30):
    """Retain the HTTP response, including refused browser requests and MCP SSE envelopes."""
    request_headers = {"Content-Type": "application/json", **(headers or {})}
    data = None if body is None else json.dumps(body, allow_nan=False).encode("utf-8")
    try:
        response = urlopen(Request(base + path, data=data, headers=request_headers, method=method), timeout=timeout)
    except HTTPError as error:
        response = error
    with response:
        raw = response.read().decode("utf-8")
        return {"status": response.status, "headers": dict(response.headers.items()), "body": raw}


def mcp_result(response):
    """Decode a single MCP JSON-RPC response from JSON or streamable-HTTP event data."""
    if response["status"] != 200:
        raise RuntimeError(f"MCP returned HTTP {response['status']}")
    raw = response["body"].strip()
    if raw.startswith(("event:", "data:")):
        messages = [json.loads(line[5:].strip()) for line in raw.splitlines() if line.startswith("data:")]
        result = next((message for message in messages if "id" in message), None)
    else:
        result = json.loads(raw)
    if not isinstance(result, dict) or result.get("error") or result.get("result", {}).get("isError"):
        raise RuntimeError(f"MCP operation failed: {result}")
    return result


def exercise(base, skill, output, timeout):
    """Run packaged preparation, native default analyses, plots, MCP, and origin refusals."""
    # Only the extracted release ZIP supplies skill modules, fixtures, and plot adapters.
    sys.path[:0] = [str(skill / "scripts"), str(skill)]
    frequency = importlib.import_module("run_frequency")
    study = importlib.import_module("run_study")
    plots = importlib.import_module("plot_frequency")
    for module in (frequency, study, plots):
        if not Path(module.__file__).resolve().is_relative_to(skill):
            raise RuntimeError(f"Skill import escaped the extracted release: {module.__file__}")
    fixture = study.read(skill / "assets/synthetic-annual-flows.json")
    api = frequency.checked(frequency.request_json(base, "/api/info", timeout=timeout), "api-info.json")
    write_json(output / "api-info.json", api)
    schema = frequency.request_json(base, "/openapi/v1.json", timeout=timeout)
    write_json(output / "openapi.json", schema)
    for path in ("/api/inputdata/{id}/source", "/api/inputdata/{id}/chronology"):
        if path not in schema.get("paths", {}):
            raise RuntimeError(f"Required REST contract is absent: {path}")

    mcp_dir = output / "mcp"
    mcp_dir.mkdir()
    headers = {"Accept": "application/json, text/event-stream"}
    sequence = 0

    def call(method, params, name):
        """Keep both JSON-RPC requests and transport responses before asserting success."""
        nonlocal sequence
        sequence += 1
        body = {"jsonrpc": "2.0", "id": sequence, "method": method, "params": params}
        write_json(mcp_dir / (name + "-request.json"), body)
        response = request(base, "/mcp", method="POST", body=body, headers=headers, timeout=timeout)
        write_json(mcp_dir / (name + "-response.json"), response)
        result = mcp_result(response)
        write_json(mcp_dir / (name + ".json"), result)
        return result

    initialized = call("initialize", {"protocolVersion": "2025-06-18", "capabilities": {},
                       "clientInfo": {"name": "BestFitPluginReleaseSmoke", "version": "1"}}, "initialize")
    headers["MCP-Protocol-Version"] = initialized["result"]["protocolVersion"]
    notification = {"jsonrpc": "2.0", "method": "notifications/initialized"}
    write_json(mcp_dir / "initialized-request.json", notification)
    response = request(base, "/mcp", method="POST", body=notification, headers=headers, timeout=timeout)
    write_json(mcp_dir / "initialized-response.json", response)
    if response["status"] != 202:
        raise RuntimeError("MCP initialized notification was not accepted")
    tools = call("tools/list", {}, "tools-list")
    tool_names = {tool["name"] for tool in tools["result"]["tools"]}
    required = {"get_metadata", "get_inputdata_source", "get_inputdata_chronology"}
    if not required <= tool_names:
        raise RuntimeError(f"Required MCP tools are missing: {required - tool_names}")
    call("tools/call", {"name": "get_metadata", "arguments": {}}, "metadata")

    print("Preparing packaged synthetic input and six study scenarios", flush=True)
    frequency.run_frequency(base, "bulletin17c", fixture, "manual", {}, "auto", output / "preview",
                            timeout, prepare_only=True, ylabel="Flow (illustrative units)")
    input_id = study.read(output / "preview/input-created.json")["inputData"]["id"]
    for tool in ("get_inputdata_source", "get_inputdata_chronology"):
        call("tools/call", {"name": tool, "arguments": {"id": input_id}}, tool)
    statuses = study.execute(study.read(skill / "assets/synthetic-study.json"), output / "study-preview",
                             base, skill / "assets", prepare_only=True, timeout=timeout)
    if len(statuses) != 6 or any(row["status"] != "prepared" for row in statuses):
        raise RuntimeError(f"The packaged six-scenario study did not prepare successfully: {statuses}")

    before = frequency.checked(frequency.request_json(base, "/api/resources", timeout=timeout),
                               "origin-resource-state.json before")
    refusals = {}
    for name, method, path, body, extra in (
        ("get", "GET", "/api/resources", None, {}),
        ("write", "POST", "/api/inputdata/manual", fixture, {}),
        ("preflight", "OPTIONS", "/api/inputdata/manual", None,
         {"Access-Control-Request-Method": "POST", "Access-Control-Request-Headers": "content-type"}),
    ):
        response = request(base, path, method=method, body=body,
                           headers={"Origin": "https://example.invalid", **extra}, timeout=timeout)
        refusals[name] = {"method": method, "path": path, "response": response}
        write_json(output / "foreign-origin.json", refusals)
        if response["status"] != 403:
            raise RuntimeError(f"Foreign-Origin {name} was not rejected with HTTP 403")
    after = frequency.checked(frequency.request_json(base, "/api/resources", timeout=timeout),
                              "origin-resource-state.json after")
    write_json(output / "origin-resource-state.json", {"before": before, "after": after})
    state_fields = ("timeSeriesCount", "inputDataCount", "analysisCount", "resources")
    if {key: before[key] for key in state_fields} != {key: after[key] for key in state_fields}:
        raise RuntimeError("Refused foreign-Origin requests changed the resource inventory")

    import matplotlib.pyplot as plt
    analyses = []
    for kind, mgbt in (("bulletin17c", "auto"), ("univariate", "on")):
        print(f"Running packaged {kind} workflow with unchanged API analysis defaults", flush=True)
        folder = output / kind
        frequency.run_frequency(base, kind, fixture, "manual", {}, mgbt, folder, timeout,
                                ylabel="Flow (illustrative units)")
        fig, _, notes = plots.build_figure(study.read(folder / "results.json"), study.read(folder / "input.json"),
                                          title=f"Synthetic {kind} integration run", ylabel="Flow (illustrative units)")
        try:
            plots.save_figure(fig, folder / "frequency")
        finally:
            plt.close(fig)
        write_json(folder / "frequency-display-notes.json", notes)
        for stem in ("chronology", "frequency"):
            for suffix in (".png", ".svg"):
                path = folder / (stem + suffix)
                if not path.is_file() or path.stat().st_size == 0:
                    raise RuntimeError(f"Expected packaged plot is missing: {path}")
        analysis_request = study.read(folder / "analysis-request.json")
        if set(analysis_request) != {"inputDataId"}:
            raise RuntimeError("The integration harness must not override scientific analysis defaults")
        analyses.append({"kind": kind, "analysisOptions": {}, "mgbt": mgbt, "status": "completed"})

    imports = {name: str(Path(module.__file__).resolve()) for name, module in sys.modules.items()
               if name in ("run_frequency", "run_study", "prepare_study", "plot_frequency", "plot_chronology")
               or name == "bestfit_plots" or name.startswith("bestfit_plots.")}
    if any(not Path(path).is_relative_to(skill) for path in imports.values()):
        raise RuntimeError("A workflow or plotting import escaped the extracted release")
    write_json(output / "skill-imports.json", imports)
    return {"api": api, "mcpToolCount": len(tool_names), "studyPreviews": statuses,
            "foreignOrigin": {name: value["response"]["status"] for name, value in refusals.items()},
            "analyses": analyses}


def run(args):
    """Own one API process for the full run and always preserve its final lifecycle receipt."""
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=False)
    started = datetime.now(timezone.utc).isoformat()
    receipt = {"schemaVersion": 1, "status": "running", "startedUtc": started,
               "scope": "Release integration only; no numerical verification or scientific acceptance claim",
               "python": sys.version, "platform": platform.platform(), "harnessSha256": sha256(__file__)}
    process = None
    log = None
    try:
        plugin = args.plugin_zip.resolve()
        api_dll = args.api_dll.resolve()
        if not api_dll.is_file():
            raise ValueError(f"Build the Release API before running this harness: {api_dll}")
        dotnet = shutil.which("dotnet")
        if not dotnet:
            raise ValueError("dotnet must be available on PATH")
        receipt["dotnet"] = command_receipt([dotnet, "--info"])
        receipt["git"] = {name: command_receipt(["git", *arguments]) for name, arguments in (
            ("commit", ["rev-parse", "HEAD"]), ("branch", ["branch", "--show-current"]),
            ("status", ["status", "--porcelain=v1"]))}
        receipt["packages"] = {name: importlib.metadata.version(name) for name in ("numpy", "matplotlib")}
        os.environ["MPLBACKEND"] = "Agg"
        os.environ["MPLCONFIGDIR"] = str(output / "matplotlib")
        receipt["plugin"] = {"path": str(plugin), "sha256": sha256(plugin)}
        manifest, inventory = extract_plugin(plugin, output / "plugin")
        receipt["plugin"]["version"] = manifest["version"]
        write_json(output / "plugin-inventory.json", inventory)
        build_files = sorted(path for path in api_dll.parent.iterdir()
                             if path.is_file() and (path.suffix == ".dll" or path.name.endswith(
                                 (".deps.json", ".runtimeconfig.json")) or path.name.startswith("appsettings")))
        receipt["build"] = {"apiDll": str(api_dll), "sha256": {path.name: sha256(path) for path in build_files}}
        deps = json.loads(api_dll.with_suffix(".deps.json").read_text(encoding="utf-8-sig"))
        receipt["numericsDependencies"] = {name: value for name, value in deps["libraries"].items()
                                           if "numerics" in name.lower()}
        shutil.copyfile(api_dll.with_suffix(".deps.json"), output / "api.deps.json")
        with socket.socket(socket.AF_INET, socket.SOCK_STREAM) as listener:
            listener.bind(("127.0.0.1", 0))
            port = listener.getsockname()[1]
        base = f"http://127.0.0.1:{port}"
        environment = os.environ.copy()
        environment.update({"ASPNETCORE_ENVIRONMENT": "Development", "DOTNET_ENVIRONMENT": "Development",
                            "ASPNETCORE_URLS": base})
        command = [dotnet, str(api_dll), "--urls", base, "--environment", "Development"]
        log = (output / "api.log").open("w", encoding="utf-8")
        process = subprocess.Popen(command, cwd=api_dll.parent, env=environment, stdout=log,
                                   stderr=subprocess.STDOUT,
                                   creationflags=subprocess.CREATE_NO_WINDOW if os.name == "nt" else 0)
        receipt["process"] = {"pid": process.pid, "command": command, "baseUrl": base,
                              "cwd": str(api_dll.parent), "environment": "Development"}
        write_json(output / "receipt.json", receipt)
        deadline = time.monotonic() + args.startup_timeout
        while True:
            if process.poll() is not None:
                raise RuntimeError(f"Owned API exited before health readiness: {process.returncode}; inspect api.log")
            try:
                health = request(base, "/health", timeout=min(2, args.startup_timeout))
                if health["status"] == 200:
                    write_json(output / "health.json", health)
                    break
            except (OSError, URLError):
                pass
            if time.monotonic() >= deadline:
                raise RuntimeError("Owned API did not become healthy before the startup timeout")
            time.sleep(0.2)
        receipt["checks"] = exercise(base, (output / "plugin/skills/bestfit-frequency").resolve(),
                                      output, args.request_timeout)
        receipt["status"] = "passed"
    except BaseException as error:
        receipt.update(status="failed", error=f"{type(error).__name__}: {error}")
        (output / "failure.txt").write_text(traceback.format_exc(), encoding="utf-8")
        raise
    finally:
        if process is not None:
            cleanup = "already-exited"
            if process.poll() is None:
                process.terminate()
                cleanup = "terminated-owned-child"
                try:
                    process.wait(timeout=10)
                except subprocess.TimeoutExpired:
                    process.kill()
                    process.wait(timeout=10)
                    cleanup = "killed-owned-child-after-timeout"
            receipt["process"].update(cleanup=cleanup, exitCode=process.returncode)
        if log is not None:
            log.close()
        receipt["finishedUtc"] = datetime.now(timezone.utc).isoformat()
        write_json(output / "receipt.json", receipt)
        files = {str(path.relative_to(output)).replace("\\", "/"): sha256(path)
                 for path in sorted(output.rglob("*")) if path.is_file() and path != output / "sha256.json"}
        write_json(output / "sha256.json", files)
    print(f"Release integration passed: {output / 'receipt.json'}", flush=True)


def main():
    """Require fresh evidence output and expose wait limits without estimator overrides."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="Fresh evidence directory; existing paths are refused")
    parser.add_argument("--plugin-zip", type=Path, default=ROOT / "artifacts/bestfit-frequency-openai-plugin.zip")
    parser.add_argument("--api-dll", type=Path, default=ROOT / "src/RMC.BestFit.Api/bin/Release/net10.0/RMC.BestFit.Api.dll")
    parser.add_argument("--startup-timeout", type=float, default=60, help="Health readiness wait in seconds")
    parser.add_argument("--request-timeout", type=float, default=1800,
                        help="Per-request wait in seconds; never changes scientific defaults")
    args = parser.parse_args()
    if args.startup_timeout <= 0 or args.request_timeout <= 0:
        parser.error("Timeouts must be positive")
    try:
        run(args)
    except (RuntimeError, ValueError, KeyError, TypeError, OSError, subprocess.SubprocessError) as error:
        parser.exit(1, f"Plugin release integration failed: {error}\n")


if __name__ == "__main__":
    main()
