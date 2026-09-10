"""Build unsigned, disposable Inno fixtures and test the production updater code."""
import argparse
import os
from pathlib import Path
import shutil
import subprocess
import uuid

parser = argparse.ArgumentParser()
parser.add_argument("--compiler", required=True)
args = parser.parse_args()
source = Path(__file__).resolve().parent
project = source.parents[2]
root = project / "review/security-fixes" / ("roundtrip-" + uuid.uuid4().hex)
driver = root / "driver"
subprocess.run(["dotnet", "build", str(source / "UpdateRoundTrip.csproj"), "-c", "Release", "-o", str(driver)], check=True)
for mode in ("success", "rollback"):
    case = root / mode
    payload = case / "payload"
    shutil.copytree(driver, payload)
    (payload / "introduced-marker").write_text("candidate file")
    if mode == "rollback":
        (payload / "fail-startup").write_text("exercise rollback")
    env = os.environ | {"SLITHY_FIXTURE_PAYLOAD": str(payload), "SLITHY_FIXTURE_OUTPUT": str(case / "package")}
    subprocess.run([args.compiler, str(source / "fixture.iss")], env=env, check=True, stdout=subprocess.DEVNULL)
    subprocess.run([str(driver / "Slithy Tove.exe"), str(case), str(case / "package/fixture-setup.exe"), mode], check=True, timeout=100)
    print("PASS: Windows EXE update", mode, flush=True)
print("Fixture evidence:", root, flush=True)
