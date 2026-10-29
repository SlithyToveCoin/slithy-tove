"""Black-box desktop checks against the published Linux GUI archive.

Run inside xfce-session.py or wayland-session.py. Wallets live in a temporary
profile, the node uses regtest with no peers, and no mining command is sent.
"""
import argparse
import base64
import ctypes as c
import hashlib
import json
import os
from pathlib import Path, PurePosixPath
import secrets
import shlex
import shutil
import signal
import socket
import struct
import subprocess
import tarfile
import tempfile
import time
import urllib.request
import zlib

ARCHIVE_SHA256 = "aeb08782d19fd039623f84be4a9834e5b3f82e30ff37e293995f24a0a66489f3"
TITLE = "Slithy Tove | Linux GUI beta"


def wait_for(test, description, timeout=30):
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        result = test()
        if result:
            return result
        time.sleep(.15)
    raise AssertionError("Timed out: " + description)


def run(command, env, **kwargs):
    return subprocess.run(command, env=env, check=True, text=True, timeout=kwargs.pop("timeout", 30),
                          stdout=subprocess.PIPE, stderr=subprocess.PIPE, **kwargs)


def extract_archive(archive, destination, expected):
    with archive.open("rb") as stream:
        digest = hashlib.file_digest(stream, "sha256").hexdigest()
    if digest != expected:
        raise ValueError("Archive hash does not match the reviewed release")
    total, names = 0, set()
    with tarfile.open(archive, "r:gz") as source:
        members = source.getmembers()
        if len(members) > 10000:
            raise ValueError("Archive contains too many entries")
        for member in members:
            path = PurePosixPath(member.name)
            if path.is_absolute() or ".." in path.parts or "\\" in member.name or member.name in names or member.size < 0:
                raise ValueError("Unsafe or duplicate archive path")
            if not (member.isdir() or member.isfile()):
                raise ValueError("Links and special files are not accepted")
            total += member.size
            if total > 2 * 1024 ** 3:
                raise ValueError("Expanded archive is too large")
            names.add(member.name)
        for member in members:
            target = destination.joinpath(*PurePosixPath(member.name).parts)
            if member.isdir():
                target.mkdir(parents=True, exist_ok=True)
            else:
                target.parent.mkdir(parents=True, exist_ok=True)
                with source.extractfile(member) as incoming, target.open("wb") as outgoing:
                    shutil.copyfileobj(incoming, outgoing)
                target.chmod(0o755 if member.mode & 0o111 else 0o644)
    apps = list(destination.rglob("slithy-desktop"))
    if len(apps) != 1:
        raise ValueError("Expected one desktop executable")
    return apps[0]


class X11:
    class XImage(c.Structure):
        _fields_ = [(name, c.c_int) for name in ("width", "height", "xoffset", "format")] + [("data", c.c_void_p)] + [
            (name, c.c_int) for name in ("byte_order", "bitmap_unit", "bitmap_bit_order", "bitmap_pad", "depth", "bytes_per_line", "bits_per_pixel")] + [
            (name, c.c_ulong) for name in ("red_mask", "green_mask", "blue_mask")] + [("obdata", c.c_void_p)]

    def __init__(self, env):
        self.env = env
        self.lib = c.CDLL("libX11.so.6")
        signatures = {
            "XOpenDisplay": ([c.c_char_p], c.c_void_p),
            "XDefaultRootWindow": ([c.c_void_p], c.c_ulong),
            "XInternAtom": ([c.c_void_p, c.c_char_p, c.c_int], c.c_ulong),
            "XGetWindowProperty": ([c.c_void_p, c.c_ulong, c.c_ulong, c.c_long, c.c_long, c.c_int, c.c_ulong,
                c.POINTER(c.c_ulong), c.POINTER(c.c_int), c.POINTER(c.c_ulong), c.POINTER(c.c_ulong), c.POINTER(c.POINTER(c.c_ubyte))], c.c_int),
            "XGetImage": ([c.c_void_p, c.c_ulong, c.c_int, c.c_int, c.c_uint, c.c_uint, c.c_ulong, c.c_int], c.POINTER(self.XImage)),
            "XDestroyImage": ([c.POINTER(self.XImage)], c.c_int),
            "XFree": ([c.c_void_p], c.c_int),
            "XCloseDisplay": ([c.c_void_p], c.c_int),
        }
        for name, (arguments, result) in signatures.items():
            function = getattr(self.lib, name)
            function.argtypes, function.restype = arguments, result
        self.display = self.lib.XOpenDisplay(env["DISPLAY"].encode())
        if not self.display:
            raise RuntimeError("Cannot open the isolated X11 display")
        self.root = self.lib.XDefaultRootWindow(self.display)

    def command(self, *arguments, check=True, input=None):
        return subprocess.run(["xdotool", *map(str, arguments)], env=self.env, text=True, input=input,
                              stdout=subprocess.PIPE, stderr=subprocess.PIPE, timeout=15, check=check).stdout.strip()

    def atom(self, name):
        return self.lib.XInternAtom(self.display, name.encode(), 0)

    def prop(self, window, name):
        kind, fmt, count, left = c.c_ulong(), c.c_int(), c.c_ulong(), c.c_ulong()
        data = c.POINTER(c.c_ubyte)()
        result = self.lib.XGetWindowProperty(self.display, window, self.atom(name), 0, 1024 * 1024, 0, 0,
            c.byref(kind), c.byref(fmt), c.byref(count), c.byref(left), c.byref(data))
        if result != 0:
            raise RuntimeError("Cannot read window property " + name)
        try:
            return list(c.cast(data, c.POINTER(c.c_ulong))[:count.value]) if fmt.value == 32 else bytes(data[:count.value])
        finally:
            if data:
                self.lib.XFree(data)

    def windows(self, title=None, visible=True):
        arguments = ["search"] + (["--onlyvisible"] if visible else []) + ["--name", "^" + title.replace("|", "\\|") + "$" if title else "."]
        return [int(value) for value in self.command(*arguments, check=False).splitlines()]

    def window(self, title, timeout=30):
        return wait_for(lambda: next(iter(self.windows(title)), None), title, timeout)

    def geometry(self, window):
        return {key: int(value) for key, value in (line.split("=", 1) for line in self.command("getwindowgeometry", "--shell", window).splitlines())}

    def click(self, window, x, y):
        self.command("mousemove", "--window", window, round(x), round(y), "click", 1)
        time.sleep(.4)

    def key(self, window, keys):
        self.command("windowfocus", "--sync", window, "key", "--clearmodifiers", keys)
        time.sleep(.3)

    def type_private(self, text):
        self.command("type", "--clearmodifiers", "--file", "-", input=text)

    def drag(self, window, start, end):
        before = self.geometry(window)
        self.command("mousemove", "--window", window, *start, "mousedown", 1, "sleep", ".2", "mousemove", "--sync",
                     before["X"] + end[0], before["Y"] + end[1], "sleep", ".4", "mouseup", 1)
        time.sleep(.5)
        return before, self.geometry(window)

    def screenshot(self, path, window=None):
        window = self.root if window is None else window
        size = self.geometry(window)
        width, height = size["WIDTH"], size["HEIGHT"]
        pointer = self.lib.XGetImage(self.display, window, 0, 0, width, height, c.c_ulong(-1).value, 2)
        if not pointer:
            raise RuntimeError("Cannot capture the test display")
        try:
            item = pointer.contents
            if item.bits_per_pixel != 32 or item.byte_order != 0 or item.red_mask != 0xff0000:
                raise RuntimeError("Unexpected X11 image format")
            data = c.string_at(item.data, item.bytes_per_line * height)
            rows = []
            for row in range(height):
                pixels = data[row * item.bytes_per_line:row * item.bytes_per_line + width * 4]
                rgb = bytearray(width * 3)
                rgb[0::3], rgb[1::3], rgb[2::3] = pixels[2::4], pixels[1::4], pixels[0::4]
                rows.append(b"\0" + rgb)
            def chunk(kind, value):
                return struct.pack(">I", len(value)) + kind + value + struct.pack(">I", zlib.crc32(kind + value))
            png = b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 2, 0, 0, 0))
            path.write_bytes(png + chunk(b"IDAT", zlib.compress(b"".join(rows))) + chunk(b"IEND", b""))
        finally:
            self.lib.XDestroyImage(pointer)

    def close(self):
        self.lib.XCloseDisplay(self.display)


class Suite:
    def __init__(self, args):
        self.args = args
        self.output = args.output.resolve()
        self.output.mkdir(parents=True, exist_ok=True)
        self.results = {"session": args.session, "archive_sha256": args.sha256, "checks": [], "limitations": [
            "Virtual display and software rendering; no hardware GPU, multi-monitor or suspend coverage.",
            "Update notice/button checks use the unmodified package's bundled UI fixtures, not a new public release."]}
        if args.session == "weston-xwayland":
            self.results["limitations"].append("Avalonia 11 uses XWayland here. This does not test native Wayland support.")

    def passed(self, name):
        self.results["checks"].append({"name": name, "status": "passed"})
        self.save()
        print("PASS:", name, flush=True)

    def skipped(self, name, reason):
        self.results["checks"].append({"name": name, "status": "not-supported", "reason": reason})
        self.save()
        print("NOT SUPPORTED:", name, reason, flush=True)

    def save(self):
        (self.output / "summary.json").write_text(json.dumps(self.results, indent=2))

    def profile(self, directory, scale="1"):
        env = dict(os.environ)
        for key, name in (("HOME", "home"), ("XDG_CONFIG_HOME", "config"), ("XDG_DATA_HOME", "data"), ("XDG_CACHE_HOME", "cache")):
            path = directory / name
            path.mkdir(mode=0o700)
            env[key] = str(path)
        env["AVALONIA_GLOBAL_SCALE_FACTOR"] = scale
        env.pop("AVALONIA_SCREEN_SCALE_FACTORS", None)
        env.pop("SLITHY_NODE_BIN", None)
        return env

    def process(self, app, arguments, env):
        # Output stays in memory unless it contains a test result. Do not upload node logs or profiles.
        return subprocess.Popen([str(app), *arguments], env=env, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)

    def stop_process(self, process):
        if process.poll() is None:
            process.terminate()
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                process.kill()
                process.wait(timeout=5)

    @staticmethod
    def stop_owned_node(data):
        # Match the complete temporary data argument. Never stop a node by its name alone.
        expected = ("-datadir=" + str(data)).encode()
        for process in Path("/proc").iterdir():
            if not process.name.isdigit():
                continue
            try:
                arguments = (process / "cmdline").read_bytes().split(b"\0")
                if expected not in arguments or Path(arguments[0].decode()).name != "slithyd":
                    continue
                os.kill(int(process.name), signal.SIGTERM)
                try:
                    wait_for(lambda: not process.exists() or (process / "stat").read_text().split()[2] == "Z", "owned test node cleanup", timeout=15)
                except AssertionError:
                    if expected in (process / "cmdline").read_bytes().split(b"\0"):
                        os.kill(int(process.name), signal.SIGKILL)
                        wait_for(lambda: not process.exists() or (process / "stat").read_text().split()[2] == "Z", "owned test node forced cleanup", timeout=5)
            except (FileNotFoundError, ProcessLookupError):
                pass

    def fixtures(self, app, env):
        for flag in ("--self-test", "--update-test", "--ui-test"):
            command = [str(app), flag]
            if flag == "--ui-test":
                command += ["--screenshots", str(self.output / "ui-fixtures")]
            result = run(command, env, timeout=180)
            (self.output / (flag[2:] + ".log")).write_text(result.stdout + result.stderr)
            self.passed("published package " + flag)

    def controls(self, app, directory, scale):
        profile = directory / ("preview-" + scale)
        profile.mkdir()
        env = self.profile(profile, scale)
        x = X11(env)
        process = self.process(app, ["--preview"], env)
        factor = float(scale)
        try:
            window = x.window(TITLE)
            wait_for(lambda: x.geometry(window)["WIDTH"] == round(980 * factor), "mapped preview size")
            time.sleep(.8)
            identity = x.prop(window, "WM_CLASS")
            assert b"io.slithy.Tove" in identity, "Window class does not match the desktop entry"
            icon = x.prop(window, "_NET_WM_ICON")
            assert len(icon) > 1000 and len(set(icon[2:])) > 20, "Window icon is blank or missing"
            x.screenshot(self.output / ("preview-" + scale + ".png"), window)
            size = x.geometry(window)
            x.click(window, size["WIDTH"] - 60 * factor, 16 * factor)
            wait_for(lambda: x.atom("_NET_WM_STATE_MAXIMIZED_VERT") in x.prop(window, "_NET_WM_STATE"), "maximize")
            size = x.geometry(window)
            x.click(window, size["WIDTH"] - 60 * factor, 16 * factor)
            wait_for(lambda: x.atom("_NET_WM_STATE_MAXIMIZED_VERT") not in x.prop(window, "_NET_WM_STATE"), "restore")
            if self.args.session == "xfce-x11":
                size = x.geometry(window)
                x.click(window, size["WIDTH"] - 100 * factor, 16 * factor)
                wait_for(lambda: x.prop(window, "WM_STATE")[:1] == [3], "minimize")
                x.command("windowmap", window, "windowactivate", "--sync", window)
                wait_for(lambda: x.prop(window, "WM_STATE")[:1] == [1], "show minimized window")
            else:
                self.skipped("minimize at scale " + scale, "Weston's desktop shell has no taskbar restore route; checked on Xfce instead.")
            if scale == "1":
                before, after = x.drag(window, (300, 16), (340, 46))
                assert (after["X"], after["Y"]) != (before["X"], before["Y"]), "Titlebar drag did not move"
                size = x.geometry(window)
                before, after = x.drag(window, (size["WIDTH"] - 2, size["HEIGHT"] - 2), (size["WIDTH"] + 78, size["HEIGHT"] + 38))
                assert after["WIDTH"] > before["WIDTH"] and after["HEIGHT"] > before["HEIGHT"], "Resize did not grow window"
                size = x.geometry(window)
                x.drag(window, (size["WIDTH"] - 2, size["HEIGHT"] - 2), (400, 400))
                assert x.geometry(window)["WIDTH"] == 760 and x.geometry(window)["HEIGHT"] == 620, "Minimum window size changed"
                x.screenshot(self.output / "minimum-size.png", window)
            size = x.geometry(window)
            x.click(window, size["WIDTH"] - 20 * factor, 16 * factor)
            assert process.wait(timeout=10) == 0, "Titlebar close failed"
            self.passed("native logo, window controls and scaling " + scale)
        finally:
            self.stop_process(process)
            x.close()

    def wallet(self, app, directory):
        with socket.socket() as probe:
            assert probe.connect_ex(("127.0.0.1", 54436)) != 0, "Regtest port is already in use; no existing node was touched"
        profile = directory / "wallet-profile"
        profile.mkdir()
        env = self.profile(profile)
        config = Path(env["XDG_CONFIG_HOME"]) / "slithy-tove-gui/beta-20260909"
        config.mkdir(parents=True, mode=0o700)
        terms = (app.parent / "licenses/BETA-TERMS.txt").read_bytes().decode("utf-8-sig")
        settings = {"AutoCheckUpdates": False, "MinimizeToTrayOnClose": False,
                    "TermsAcceptedVersion": "1.0:" + hashlib.sha256(terms.encode()).hexdigest().upper()}
        (config / "settings.json").write_text(json.dumps(settings))
        (config / "settings.json").chmod(0o600)
        run(["bash", str(app.parent / "install-desktop.sh")], env)
        entry = Path(env["XDG_DATA_HOME"]) / "applications/io.slithy.Tove.desktop"
        icon = Path(env["XDG_DATA_HOME"]) / "icons/hicolor/256x256/apps/io.slithy.Tove.png"
        run(["desktop-file-validate", str(entry)], env)
        text = entry.read_text()
        assert 'Exec="' + str(app) + '"\n' in text and "StartupWMClass=io.slithy.Tove\n" in text
        assert icon.read_bytes() == (app.parent / "Assets/slithy-icon-256.png").read_bytes()
        self.passed("per-user desktop entry and installed icon")
        # Add regtest to this disposable entry. Launching a test must never start a public-chain node.
        entry.write_text(text.replace('Exec="' + str(app) + '"\n', 'Exec="' + str(app) + '" --regtest\n'))
        x = X11(env)
        app_pid, rpc = None, None
        data = Path(env["XDG_DATA_HOME"]) / "slithy-tove-gui/beta-20260909/isolated-regtest"
        try:
            run(["gtk-launch", "io.slithy.Tove"], env)
            window = x.window(TITLE, timeout=60)
            app_pid = x.prop(window, "_NET_WM_PID")[0]
            assert Path("/proc", str(app_pid), "exe").resolve() == app
            picker = x.window("Open wallet", timeout=100)
            x.screenshot(self.output / "wallet-empty.png", picker)
            x.key(picker, "Escape")
            wait_for(lambda: not x.windows("Open wallet"), "cancel empty picker")
            cookie = wait_for(lambda: next(iter(data.glob("gui-rpc-*.cookie")), None), "regtest cookie")
            authorization = "Basic " + base64.b64encode(cookie.read_bytes().strip()).decode()

            def rpc_call(method, parameters=None, wallet=""):
                payload = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": parameters or []}).encode()
                request = urllib.request.Request("http://127.0.0.1:54436/" + ("wallet/" + wallet if wallet else ""), payload,
                    headers={"Authorization": authorization, "Content-Type": "application/json"})
                with urllib.request.urlopen(request, timeout=30) as response:
                    reply = json.load(response)
                if reply.get("error"):
                    raise AssertionError("Regtest RPC failed: " + method)
                return reply["result"]

            rpc = rpc_call
            assert rpc("getblockchaininfo")["chain"] == "regtest" and rpc("getconnectioncount") == 0
            self.passed("desktop launcher started a disconnected regtest node")
            if self.args.session == "xfce-x11":
                watcher = run(["gdbus", "call", "--session", "--dest", "org.kde.StatusNotifierWatcher", "--object-path", "/StatusNotifierWatcher",
                    "--method", "org.freedesktop.DBus.Properties.Get", "org.kde.StatusNotifierWatcher", "RegisteredStatusNotifierItems"], env)
                assert "StatusNotifierItem" in watcher.stdout, "The Xfce tray did not register Slithy's icon"
                self.passed("Slithy registered with the native Xfce notification area")
            else:
                self.skipped("tray host", "Weston's desktop shell has no StatusNotifier panel; Xfce covers registration.")
            password = secrets.token_hex(12)
            for name in ("DesktopAlpha", "DesktopBeta"):
                rpc("createwallet", [name, False, False, password])
            x.click(window, 410, 288)
            picker = x.window("Open wallet")
            x.click(picker, 240, 132)
            x.screenshot(self.output / "wallet-dropdown.png")
            x.key(picker, "End")
            x.key(picker, "Return")
            x.click(picker, 200, 207)
            x.type_private(password)
            x.key(picker, "Return")
            wait_for(lambda: not x.windows("Open wallet"), "open selected wallet", timeout=30)
            wait_for(lambda: json.loads((config / "settings.json").read_text()).get("LastWalletName") == "DesktopBeta", "persist dropdown selection")
            assert rpc("getwalletinfo", wallet="DesktopBeta")["walletname"] == "DesktopBeta"
            x.screenshot(self.output / "wallet-open.png", window)
            self.passed("real wallet dropdown selects, opens and remembers the chosen wallet")
            x.click(window, x.geometry(window)["WIDTH"] - 20, 16)
            wait_for(lambda: not Path("/proc", str(app_pid)).exists(), "quit installed app", timeout=30)
            with socket.socket() as probe:
                assert probe.connect_ex(("127.0.0.1", 54436)) != 0, "Owned regtest node did not stop"
            self.passed("installed launcher app quits and stops its node")
        finally:
            if rpc is not None:
                try:
                    rpc("stop")
                except Exception:
                    pass
            if app_pid and Path("/proc", str(app_pid)).exists():
                if Path("/proc", str(app_pid), "exe").resolve() == app:
                    os.kill(app_pid, signal.SIGTERM)
                    try:
                        wait_for(lambda: not Path("/proc", str(app_pid)).exists(), "owned app cleanup", timeout=10)
                    except AssertionError:
                        if Path("/proc", str(app_pid), "exe").resolve() == app:
                            os.kill(app_pid, signal.SIGKILL)
            self.stop_owned_node(data)
            x.close()

    def main(self):
        try:
            assert os.environ.get("SLITHY_DESKTOP_TEST_SESSION") == self.args.session, "Use the isolated desktop wrapper"
            with tempfile.TemporaryDirectory(prefix="slithy-desktop-suite-") as temporary:
                directory = Path(temporary)
                app = extract_archive(self.args.archive.resolve(), directory / "package", self.args.sha256)
                with (app.parent / "slithy-desktop.dll").open("rb") as stream:
                    self.results["assembly_sha256"] = hashlib.file_digest(stream, "sha256").hexdigest()
                self.passed("exact published archive hash and safe extraction")
                fixture = directory / "fixtures"
                fixture.mkdir()
                self.fixtures(app, self.profile(fixture))
                for scale in ("1", "1.5", "2"):
                    self.controls(app, directory, scale)
                self.wallet(app, directory)
            self.results["status"] = "passed"
            return 0
        except Exception as error:
            self.results["status"] = "failed"
            self.results["error"] = type(error).__name__ + ": " + str(error)
            print("FAIL:", self.results["error"], flush=True)
            return 1
        finally:
            self.save()


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--archive", type=Path, required=True)
    parser.add_argument("--sha256", default=ARCHIVE_SHA256)
    parser.add_argument("--session", choices=("xfce-x11", "weston-xwayland"), required=True)
    parser.add_argument("--output", type=Path, required=True)
    raise SystemExit(Suite(parser.parse_args()).main())
