"""Run desktop checks in a disposable Xfce/X11 session on a Linux test runner."""
import argparse
import os
from pathlib import Path
import signal
import subprocess
import sys
import tempfile
import time


def stop(process):
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=8)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)


def inside(args):
    output = args.output.resolve()
    output.mkdir(parents=True, exist_ok=True)
    config = Path(os.environ["XDG_CONFIG_HOME"]) / "xfce4/xfconf/xfce-perchannel-xml"
    config.mkdir(parents=True)
    (config / "xfce4-panel.xml").write_text('''<?xml version="1.0" encoding="UTF-8"?>
<channel name="xfce4-panel" version="1.0">
  <property name="configver" type="int" value="2"/>
  <property name="panels" type="array">
    <value type="int" value="1"/>
    <property name="panel-1" type="empty">
      <property name="position" type="string" value="p=6;x=0;y=0"/>
      <property name="position-locked" type="bool" value="true"/>
      <property name="size" type="uint" value="40"/>
      <property name="length" type="double" value="100"/>
      <property name="plugin-ids" type="array"><value type="int" value="1"/><value type="int" value="2"/></property>
    </property>
  </property>
  <property name="plugins" type="empty">
    <property name="plugin-1" type="string" value="tasklist"/>
    <property name="plugin-2" type="string" value="systray"/>
  </property>
</channel>
''')
    children, streams = [], []
    try:
        for command, name in ((["xfwm4", "--replace", "--compositor=off"], "xfwm4"),
                              (["xfsettingsd", "--no-daemon"], "xfsettingsd"),
                              (["xfce4-panel", "--disable-wm-check"], "xfce4-panel")):
            stream = (output / (name + ".log")).open("w")
            streams.append(stream)
            process = subprocess.Popen(command, stdout=stream, stderr=stream)
            children.append(process)
            time.sleep(1)
            if process.poll() is not None:
                raise RuntimeError(name + " exited before the tests started")
        env = dict(os.environ, SLITHY_DESKTOP_TEST_SESSION="xfce-x11")
        return subprocess.run(args.command, env=env, timeout=args.timeout, check=False).returncode
    finally:
        for child in reversed(children):
            stop(child)
        for stream in streams:
            stream.close()


def main(args):
    if args.inside:
        return inside(args)
    with tempfile.TemporaryDirectory(prefix="slithy-xfce-test-") as temporary:
        root = Path(temporary)
        env = dict(os.environ)
        for key, name in (("HOME", "home"), ("XDG_CONFIG_HOME", "config"), ("XDG_DATA_HOME", "data"),
                          ("XDG_CACHE_HOME", "cache"), ("XDG_RUNTIME_DIR", "run")):
            path = root / name
            path.mkdir(mode=0o700)
            env[key] = str(path)
        for key in ("DISPLAY", "WAYLAND_DISPLAY", "DBUS_SESSION_BUS_ADDRESS", "SESSION_MANAGER", "AVALONIA_SCREEN_SCALE_FACTORS", "AVALONIA_GLOBAL_SCALE_FACTOR"):
            env.pop(key, None)
        env.update(XDG_CURRENT_DESKTOP="XFCE", XDG_SESSION_TYPE="x11", LIBGL_ALWAYS_SOFTWARE="1")
        command = ["dbus-run-session", "--", "xvfb-run", "--auto-servernum", "--server-args=-screen 0 2560x1800x24 -nolisten tcp",
            sys.executable, str(Path(__file__).resolve()), "--inside", "--output", str(args.output.resolve()),
            "--timeout", str(args.timeout), "--", *args.command]
        process = subprocess.Popen(command, env=env, start_new_session=True)
        try:
            return process.wait(timeout=args.timeout + 45)
        finally:
            # This process group was created by this wrapper, not by the user's desktop.
            try:
                os.killpg(process.pid, signal.SIGTERM)
            except ProcessLookupError:
                pass
            try:
                process.wait(timeout=10)
            except subprocess.TimeoutExpired:
                os.killpg(process.pid, signal.SIGKILL)
                process.wait(timeout=5)


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--output", type=Path, required=True)
    parser.add_argument("--timeout", type=int, default=900)
    parser.add_argument("--inside", action="store_true", help=argparse.SUPPRESS)
    parser.add_argument("command", nargs=argparse.REMAINDER)
    args = parser.parse_args()
    if args.command[:1] == ["--"]:
        args.command = args.command[1:]
    if not args.command:
        parser.error("provide a test command after --")
    raise SystemExit(main(args))
