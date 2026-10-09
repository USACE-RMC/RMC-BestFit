"""Credential isolation in the executable API setup example shipped to users."""
import os
from pathlib import Path
import re
import tempfile
import unittest
from unittest.mock import patch


ROOT = Path(__file__).resolve().parents[2]


class SetupExampleTests(unittest.TestCase):
    def test_api_child_receives_runtime_settings_without_ambient_credentials(self):
        document = (ROOT / "skills/bestfit-frequency/references/setup.md").read_text(encoding="utf-8")
        example = re.search(r"```python\n(.*?)\n```", document, re.DOTALL).group(1)
        with tempfile.TemporaryDirectory() as temporary:
            workspace = Path(temporary)
            dll = workspace / "src/RMC.BestFit.Api/bin/Release/net10.0/RMC.BestFit.Api.dll"
            dll.parent.mkdir(parents=True)
            dll.touch()
            environment = {
                "PATH": "runtime-path",
                "SystemRoot": "runtime-system-root",
                "DOTNET_ROOT": "runtime-dotnet-root",
                "GH_TOKEN": "sentinel-not-a-real-secret",
                "OPENAI_API_KEY": "sentinel-not-a-real-secret",
                "AWS_SECRET_ACCESS_KEY": "sentinel-not-a-real-secret",
                "BestFit__RemoteEndpoint": "https://unrequested.example.invalid",
            }
            original_directory = Path.cwd()
            scope = {}
            try:
                os.chdir(workspace)
                with patch.dict(os.environ, environment, clear=True), \
                        patch("shutil.which", return_value="resolved-dotnet"), \
                        patch("subprocess.Popen") as launch:
                    exec(compile(example, "setup.md API example", "exec"), scope)
                forwarded = {key.upper(): value for key, value in launch.call_args.kwargs["env"].items()}
                self.assertEqual(forwarded["ASPNETCORE_ENVIRONMENT"], "Development")
                self.assertEqual(forwarded["DOTNET_ROOT"], environment["DOTNET_ROOT"])
                self.assertEqual(forwarded["SYSTEMROOT"], environment["SystemRoot"])
                for secret in ("GH_TOKEN", "OPENAI_API_KEY", "AWS_SECRET_ACCESS_KEY", "BestFit__RemoteEndpoint"):
                    self.assertNotIn(secret.upper(), forwarded)
                self.assertIn("http://127.0.0.1:5210", launch.call_args.args[0])
                self.assertEqual(launch.call_args.kwargs["cwd"], dll.parent)
            finally:
                if "log" in scope:
                    scope["log"].close()
                os.chdir(original_directory)


if __name__ == "__main__":
    unittest.main()
