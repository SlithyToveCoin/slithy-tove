#!/usr/bin/env python3
"""Run desktop checks inside a private Weston session with XWayland.

Ubuntu 24.04 packages: weston, xwayland, xvfb, xauth, x11-utils, dbus-daemon.
This tests X11 application compatibility under a Wayland compositor. It does
not turn an X11 application into a native Wayland client.
"""
import argparse
from contextlib import ExitStack
import json
import os
from pathlib import Path
import re
import selectors
import shutil
import signal
import subprocess
import sys
import tempfile
import time


def stop_group(process):
    """Stop a process group created by this script, including its children."""
    if process is None:
        return
    try:
        os.killpg(process.pid, signal.SIGTERM)
    except ProcessLookupError:
        pass
    try:
        process.wait(timeout=8)
    except subprocess.TimeoutExpired:
        try:
            os.killpg(process.pid, signal.SIGKILL)
        except ProcessLookupError:
            pass
        process.wait(timeout=8)


def wait_for_bus(process, timeout=10):
    with selectors.DefaultSelector() as selector:
        selector.register(process.stdout, selectors.EVENT_READ)
        if not selector.select(timeout):
            raise RuntimeError("The private session bus did not start.")
        address = process.stdout.readline().strip()
    if not address.startswith("unix:"):
        raise RuntimeError("The private session bus returned no address.")
    return address


def wait_for_display(process, capture, socket_name, env):
    deadline = time.monotonic() + 40
    while not capture.exists():
        if process.poll() is not None:
            raise RuntimeError("Weston exited before launching its environment probe. See weston.log.")
        if time.monotonic() > deadline:
            raise RuntimeError("Weston did not report its XWayland display. See weston.log.")
        time.sleep(0.1)
    captured = json.loads(capture.read_text())
    display = captured.get("DISPLAY", "")
    if not re.fullmatch(r":\d+(?:\.\d+)?", display) or captured.get("WAYLAND_DISPLAY") != socket_name:
        raise RuntimeError("The probe did not receive the private compositor's display.")
    env.update(captured)
    while True:
        if process.poll() is not None:
            raise RuntimeError("Weston exited while starting XWayland. See weston.log.")
        try:
            probe = subprocess.run(["xdpyinfo", "-display", display], env=env,
                                   stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL, timeout=3)
            if probe.returncode == 0:
                return display
        except subprocess.TimeoutExpired:
            pass
        if time.monotonic() > deadline:
            raise RuntimeError("XWayland did not accept an X11 connection. See weston.log.")
        time.sleep(0.2)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output", type=Path, required=True, help="Compositor logs and session metadata")
    parser.add_argument("--timeout", type=int, default=600, help="Maximum test command runtime in seconds")
    parser.add_argument("--width", type=int, default=2560)
    parser.add_argument("--height", type=int, default=1800)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    command = args.command[1:] if args.command[:1] == ["--"] else args.command
    if not command:
        parser.error("Provide a test command after --.")
    if not 1 <= args.timeout <= 7200 or not 800 <= args.width <= 7680 or not 600 <= args.height <= 4320:
        parser.error("Use a positive timeout up to 7200 seconds and a supported desktop size.")
    if sys.platform != "linux":
        parser.error("Run this script on Linux.")
    for executable in ("weston", "Xwayland", "Xvfb", "xvfb-run", "xauth", "xdpyinfo", "dbus-daemon"):
        if shutil.which(executable) is None:
            parser.error(f"Missing {executable}. Install the packages listed in this script's help.")
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    bus = compositor = test = None
    result = {"coverage": "Weston with XWayland, not native Wayland", "status": "starting"}

    def interrupt(_signum, _frame):
        raise KeyboardInterrupt

    previous = {sig: signal.signal(sig, interrupt) for sig in (signal.SIGTERM, signal.SIGINT)}

    def cleanup():
        # Stop the session before its sockets and private home are removed.
        for sig in previous:
            signal.signal(sig, signal.SIG_IGN)
        for child in (test, compositor, bus):
            stop_group(child)

    try:
        with tempfile.TemporaryDirectory(prefix="slithy-wayland-test-") as temporary, ExitStack() as owned:
            owned.callback(cleanup)
            root = Path(temporary)
            env = dict(os.environ)
            # A test session must not inherit the user's display, bus or saved desktop.
            for key in ("DISPLAY", "WAYLAND_DISPLAY", "WAYLAND_SOCKET", "XAUTHORITY",
                        "DBUS_SESSION_BUS_ADDRESS", "SESSION_MANAGER", "DESKTOP_STARTUP_ID"):
                env.pop(key, None)
            for key, name in (("HOME", "home"), ("XDG_RUNTIME_DIR", "run"), ("XDG_CONFIG_HOME", "config"),
                              ("XDG_DATA_HOME", "data"), ("XDG_CACHE_HOME", "cache")):
                path = root / name
                path.mkdir(mode=0o700)
                env[key] = str(path)
            env.update(XDG_SESSION_TYPE="wayland", XDG_CURRENT_DESKTOP="Weston", LIBGL_ALWAYS_SOFTWARE="1",
                       SLITHY_DESKTOP_TEST_SESSION="weston-xwayland")
            socket_name = "slithy-test-wayland"
            capture = root / "compositor-environment.json"
            helper = root / "capture-environment"
            helper.write_text("#!/usr/bin/python3\nimport json, os\nfrom pathlib import Path\n"
                              f"target = Path({str(capture)!r})\n"
                              "values = {key: os.environ[key] for key in ('DISPLAY', 'WAYLAND_DISPLAY', 'XAUTHORITY') if key in os.environ}\n"
                              "pending = target.with_suffix('.pending')\npending.write_text(json.dumps(values))\n"
                              "pending.chmod(0o600)\npending.rename(target)\n")
            helper.chmod(0o700)
            config = root / "weston.ini"
            # Weston exports DISPLAY to children after choosing its own XWayland socket.
            config.write_text("[core]\nxwayland=true\nidle-time=0\n"
                              "[shell]\nlocking=false\npanel-position=top\n"
                              f"[xwayland]\npath={shutil.which('Xwayland')}\n"
                              f"[autolaunch]\npath={helper}\nwatch=false\n")
            with (output / "bus.log").open("w") as bus_log, (output / "weston-process.log").open("w") as compositor_log:
                bus = subprocess.Popen(["dbus-daemon", "--session", "--nofork", "--print-address=1"], env=env,
                                       stdout=subprocess.PIPE, stderr=bus_log, text=True, start_new_session=True)
                env["DBUS_SESSION_BUS_ADDRESS"] = wait_for_bus(bus)
                version = subprocess.check_output(["weston", "--version"], env=env, text=True, timeout=10).strip()
                # The X11 backend gives Weston an input seat. Headless Weston has no
                # seat, and older XWayland servers can crash on an XTest pointer event.
                compositor = subprocess.Popen(["xvfb-run", "--auto-servernum",
                    f"--server-args=-screen 0 {args.width}x{args.height}x24 -nolisten tcp", "--",
                    "weston", "--backend=x11", "--renderer=pixman", "--xwayland",
                    "--idle-time=0", f"--width={args.width}", f"--height={args.height}", f"--socket={socket_name}",
                    f"--config={config}", f"--log={output / 'weston.log'}"], env=env,
                    stdout=compositor_log, stderr=compositor_log, start_new_session=True)
                display = wait_for_display(compositor, capture, socket_name, env)
                result.update(status="running", compositor=version, display=display, wayland_display=socket_name,
                              width=args.width, height=args.height, backend="nested X11 on a private Xvfb")
                print(f"Running desktop checks on {version}, XWayland DISPLAY={display}.", flush=True)
                test = subprocess.Popen(command, env=env, start_new_session=True)
                deadline = time.monotonic() + args.timeout
                while test.poll() is None:
                    if compositor.poll() is not None:
                        raise RuntimeError("Weston exited during the desktop test. See weston.log.")
                    if time.monotonic() >= deadline:
                        result.update(status="timeout", exit_code=124)
                        return 124
                    time.sleep(0.2)
                result.update(status="passed" if test.returncode == 0 else "failed", exit_code=test.returncode)
                return test.returncode
    except KeyboardInterrupt:
        result.update(status="interrupted", exit_code=130)
        return 130
    except (OSError, RuntimeError, ValueError, subprocess.SubprocessError) as error:
        result.update(status="failed", error=str(error), exit_code=1)
        print(f"Desktop session failed: {error}", file=sys.stderr)
        return 1
    finally:
        result["cleanup"] = "owned process groups stopped"
        (output / "wayland-session.json").write_text(json.dumps(result, indent=2) + "\n")
        for sig, handler in previous.items():
            signal.signal(sig, handler)


if __name__ == "__main__":
    raise SystemExit(main())
