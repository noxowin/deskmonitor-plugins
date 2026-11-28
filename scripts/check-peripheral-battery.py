"""Verify a built peripheral-battery package on Windows."""

import hashlib
import json
from pathlib import Path
import shutil
import struct
import subprocess
from tempfile import TemporaryDirectory


PLUGIN = Path(__file__).resolve().parents[1] / "extensions" / "peripheral-battery"
PACKAGE = PLUGIN / "bin" / "Release" / "net48" / "peripheral-battery.plugin"
TRAILER = b"DeskMonitorPlugin1"


def check_package():
    manifest = (PLUGIN / "manifest.json").read_bytes()
    if not PACKAGE.read_bytes().endswith(manifest + struct.pack("<I", len(manifest)) + TRAILER):
        raise ValueError("Package manifest does not match manifest.json")
    bundle = json.loads((PLUGIN / "vendor" / "upstream.json").read_bytes())

    with TemporaryDirectory(dir=PACKAGE.parent) as directory:
        temporary = Path(directory)
        executable = shutil.copyfile(PACKAGE, temporary / "peripheral-battery.exe")
        exported = temporary / "third-party"
        subprocess.run([executable, "--export-third-party", exported], check=True, timeout=20)
        for key in ("portable", "source", "headsetSource"):
            resource = bundle[key]
            actual = hashlib.sha256((exported / resource["name"]).read_bytes()).hexdigest()
            if actual != resource["sha256"]:
                raise ValueError(f"Embedded resource checksum mismatch: {resource['name']}")

        # Exercise the same invocation as the host, including the engine and its CLI parser.
        result = subprocess.run([executable, "--poll"], check=True, capture_output=True, timeout=20)
        report = json.loads(result.stdout)
        if not isinstance(report.get("readings"), list) or report.get("message") not in (
            None, "暂无可读取电量的外设。"
        ):
            raise ValueError(f"Bundled engine poll failed: {report!r}")
    print(f"Verified {PACKAGE.name} with upstream {bundle['tag']}")


if __name__ == "__main__":
    check_package()
