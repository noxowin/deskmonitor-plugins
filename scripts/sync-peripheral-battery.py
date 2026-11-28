"""Update the vendored Windows engine from KrisGFX's stable releases."""

import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import tarfile
from tempfile import TemporaryDirectory
from urllib.error import HTTPError
from urllib.request import Request, urlopen
from zipfile import ZipFile


UPSTREAM_HOST = "https://git.krisgfx.com"
UPSTREAM_REPO = "krisgfx/Wireless-Device-Battery-Monitor"
UPSTREAM = f"{UPSTREAM_HOST}/{UPSTREAM_REPO}"
UPSTREAM_API = f"{UPSTREAM_HOST}/api/v1/repos/{UPSTREAM_REPO}"
PLUGIN = Path(__file__).resolve().parents[1] / "extensions" / "peripheral-battery"
VENDOR = PLUGIN / "vendor"
MAX_ARCHIVE = 30 * 1024 * 1024
ARCHIVES = ("portable", "source", "headsetSource")


def request(url, github=False):
    headers = {"Accept": "application/json", "User-Agent": "DeskMonitor-Plugins/1.0"}
    if github and (token := os.environ.get("GH_TOKEN")):
        headers["Authorization"] = f"Bearer {token}"
    return Request(url, headers=headers)


def github_release(tag):
    url = f"https://api.github.com/repos/{os.environ['GITHUB_REPOSITORY']}/releases/tags/{tag}"
    try:
        with urlopen(request(url, github=True), timeout=30) as response:
            return json.load(response)
    except HTTPError as error:
        if error.code == 404:
            return None
        raise


def download(url, path, expected_digest=None):
    digest = hashlib.sha256()
    size = 0
    with urlopen(request(url), timeout=45) as input_file, path.open("xb") as output_file:
        while chunk := input_file.read(256 * 1024):
            size += len(chunk)
            if size > MAX_ARCHIVE:
                raise ValueError(f"Upstream archive exceeds {MAX_ARCHIVE} bytes: {path.name}")
            digest.update(chunk)
            output_file.write(chunk)
    sha256 = digest.hexdigest()
    if expected_digest is not None and sha256 != expected_digest:
        raise ValueError(f"Upstream checksum mismatch: {path.name}")
    return {"name": path.name, "sha256": sha256}


def release_asset(release, name):
    url = f"{UPSTREAM}/releases/download/{release['tag_name']}/{name}"
    if not any(asset["name"] == name and asset["browser_download_url"] == url for asset in release["assets"]):
        raise ValueError(f"Missing upstream release asset: {name}")
    return url


def tag_commit(tag):
    with urlopen(request(f"{UPSTREAM_API}/tags/{tag}"), timeout=30) as response:
        sha = json.load(response)["commit"]["sha"]
    if not re.fullmatch(r"[0-9a-f]{40}", sha):
        raise ValueError(f"Invalid source commit: {sha}")
    return sha


def headset_source(current, source, headset, stage):
    (installer,) = [entry for entry in source.namelist() if entry.endswith("/installer/build-installer.ps1")]
    script = source.read(installer).decode("utf-8-sig")
    (version,) = re.findall(r'^\$hscVersion\s*=\s*"([0-9]+\.[0-9]+\.[0-9]+)"', script, re.M)
    (digest,) = re.findall(r'^\$hscSha256\s*=\s*"([0-9a-f]{64})"', script, re.M)
    if headset["sha256"] != digest:
        raise ValueError("HeadsetControl executable does not match the pin in BatteryHub source")

    name = f"headsetcontrol-{version}.tar.gz"
    previous = current["headsetSource"]
    if name == previous["name"] and digest == current["headset"]["sha256"]:
        shutil.copyfile(VENDOR / name, stage / name)
        with (stage / name).open("rb") as archive:
            if hashlib.file_digest(archive, "sha256").hexdigest() != previous["sha256"]:
                raise ValueError("Existing HeadsetControl source does not match its recorded SHA-256")
        return previous

    return download(f"https://github.com/Sapd/HeadsetControl/archive/refs/tags/{version}.tar.gz", stage / name)


def executable_info(archive, name):
    if archive.namelist().count(name) != 1 or archive.getinfo(name).file_size > MAX_ARCHIVE:
        raise ValueError(f"Unexpected portable ZIP entry: {name}")
    with archive.open(name) as executable:
        if executable.read(2) != b"MZ":
            raise ValueError(f"Invalid Windows executable: {name}")
        executable.seek(0)
        return {"name": name, "sha256": hashlib.file_digest(executable, "sha256").hexdigest()}


def version_tuple(tag):
    if not re.fullmatch(r"v[0-9]+\.[0-9]+\.[0-9]+", tag):
        raise ValueError(f"Unexpected stable release tag: {tag}")
    return tuple(map(int, tag[1:].split(".")))


def update_vendor(current, release, stage):
    tag = release["tag_name"]
    portable_name = f"BatteryHub-{tag[1:]}-portable.zip"
    source_name = f"BatteryHub-{tag[1:]}-source.zip"
    with urlopen(request(release_asset(release, "SHA256SUMS.txt")), timeout=30) as response:
        checksums = response.read(16385).decode("utf-8-sig")
    if len(checksums) > 16384:
        raise ValueError("Unexpectedly large upstream SHA256SUMS.txt")
    (digest,) = re.findall(rf"^([0-9a-f]{{64}})[ \t]+\*?{re.escape(portable_name)}\r?$", checksums, re.M)
    commit = tag_commit(tag)
    bundle = {
        "tag": tag,
        "commit": commit,
        "portable": download(release_asset(release, portable_name), stage / portable_name, digest),
        "source": download(f"{UPSTREAM}/archive/{commit}.zip", stage / source_name),
    }
    with ZipFile(stage / portable_name) as archive:
        if not {"LICENSE.txt", "THIRD_PARTY_NOTICES.txt"}.issubset(archive.namelist()):
            raise ValueError("Portable ZIP is missing license files")
        bundle["engine"] = executable_info(archive, "BatteryHub.exe")
        bundle["headset"] = executable_info(archive, "headsetcontrol.exe")
    with ZipFile(stage / source_name) as source:
        if not any(name.endswith("/LICENSE") for name in source.namelist()):
            raise ValueError("Upstream source ZIP is missing LICENSE")
        bundle["headsetSource"] = headset_source(current, source, bundle["headset"], stage)
    with tarfile.open(stage / bundle["headsetSource"]["name"], "r:gz") as source:
        if not any(member.isfile() and Path(member.name).name.lower() in ("license", "license.md", "license.txt")
                   for member in source):
            raise ValueError("HeadsetControl source archive is missing LICENSE")
    manifest_path = PLUGIN / "manifest.json"
    manifest = json.loads(manifest_path.read_bytes())
    major, minor, patch = version_tuple("v" + manifest["version"])
    manifest["version"] = f"{major}.{minor}.{patch + 1}"
    new_names = {bundle[key]["name"] for key in ARCHIVES}
    for key in ARCHIVES:
        name = bundle[key]["name"]
        shutil.copyfile(stage / name, VENDOR / name)
        old_name = current[key]["name"]
        if old_name not in new_names:
            (VENDOR / old_name).unlink()
    (VENDOR / "upstream.json").write_text(json.dumps(bundle, indent=2) + "\n", encoding="utf-8", newline="\n")
    manifest_path.write_text(json.dumps(manifest, indent=2, ensure_ascii=False) + "\n", encoding="utf-8", newline="\r\n")


def sync():
    current = json.loads((VENDOR / "upstream.json").read_bytes())
    with urlopen(request(f"{UPSTREAM_API}/releases/latest"), timeout=30) as response:
        release = json.load(response)
    updated = (
        not release["draft"] and not release["prerelease"]
        and version_tuple(release["tag_name"]) > version_tuple(current["tag"])
    )
    if updated:
        with TemporaryDirectory() as directory:
            update_vendor(current, release, Path(directory))
        current = json.loads((VENDOR / "upstream.json").read_bytes())
        print(f"Updated the bundled engine to {current['tag']}.")
    else:
        print(f"Keeping bundled engine {current['tag']}.")

    manifest = json.loads((PLUGIN / "manifest.json").read_bytes())
    version_tuple("v" + manifest["version"])
    release_tag = f"peripheral-battery-v{manifest['version']}"
    if output := os.environ.get("GITHUB_OUTPUT"):
        existing = github_release(release_tag)
        published = existing is not None and not existing["draft"]
        if published and not any(asset["name"] == "peripheral-battery.plugin" and asset["state"] == "uploaded"
                                 for asset in existing["assets"]):
            raise ValueError(f"Published release {release_tag} is missing its plugin asset")
        with Path(output).open("a", encoding="utf-8") as stream:
            stream.write(f"vendor_updated={str(updated).lower()}\npublish={str(not published).lower()}\n"
                         f"release_tag={release_tag}\nupstream_tag={current['tag']}\n")


if __name__ == "__main__":
    sync()
