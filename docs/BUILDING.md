# Building Slithy

Build the native programs before building the Windows app or packaging Linux.
These instructions do not use private servers, wallet files or signing credentials.
Compiler output goes in `build/`, which is not source-controlled.

## Linux native tools

Ubuntu 24.04 is the reference build environment. Install dependencies:

```sh
sudo apt-get update
sudo apt-get install build-essential cmake pkgconf python3 libevent-dev libboost-dev libsqlite3-dev
bash tools/build-core.sh linux
ctest --test-dir build/native/linux --output-on-failure -j2
```

The four programs are in `build/native/linux/bin`. The menu and Windows app
select the beta network. If you run the raw daemon yourself, pass `-testnet`
for the public beta or `-regtest` with a disposable data directory for tests.
Do not start the unfinished live network as a substitute for beta testing.
`JOBS=2` is the default;
raise it if your machine has enough memory. Compilation needs substantially
more memory and disk space than running a node. A build does not start a node.

## Windows native tools from Linux or WSL

Use a checkout inside the Linux filesystem, such as `~/src/slithy-tove`, not
under `/mnt/c`. The upstream dependency build does not support Windows-mounted
source paths. Install the native dependencies above, then:

```sh
sudo apt-get install g++-mingw-w64-x86-64-posix make automake libtool bsdextrautils curl zip unzip patch
bash tools/build-core.sh windows
```

The first run downloads and builds pinned dependencies from `core/depends`.
Their recipes check source hashes. It can take a while. The executables appear
in `build/native/windows/bin`. No signing account is needed.

To reuse an existing dependency build, set `SLITHY_TOOLCHAIN` to its
`toolchain.cmake`. This is optional and is not required by the public build.
`BUILD_DIR` can select a separate output directory.

## Windows application

Install the .NET 8 SDK and the Visual Studio .NET desktop development workload.
Copy the four native Windows executables from the Linux build to
`build/native/windows/bin` in your Windows checkout. Then run:

```powershell
dotnet build 'Desktop/Slithy Tove/Slithy Tove.sln' -c Release
dotnet run --project tests/terms/TermsTests.csproj
dotnet run --project tests/security/Slithy.SecurityTests.csproj
```

Or open that solution in Visual Studio. Set `SlithyNativeDirectory` as an MSBuild
property if you use another reviewed native output folder. Do not put native
executables into Git.

The normal app still checks the official update feed. Building from source does
not disable updates. Test commands above exit before ordinary app startup and
use disposable fixtures. Do not run an interactive developer build against your
everyday wallet unless you intend to do so.

## Linux menu package

Install the .NET 8 SDK on Linux for the recovery helper. From the source root:

```sh
dotnet publish tools/SlithySeedTool/SlithySeedTool.csproj -c Release -r linux-x64 --self-contained true -o build/seed/linux
SLITHY_SEED_TOOL="$PWD/build/seed/linux/SlithySeedTool" bash tools/build-linux-release-tarball.sh "$PWD/build/native/linux/bin" 0.1.48-testnet "$PWD/build/packages"
python3 tests/terms/linux-terms-test.py
python3 -m unittest discover -s tests/security -p 'test_*.py'
```

Use a fresh package output directory for another run. The archive includes the
terminal menu, native programs, helper, notices and full offline beta terms.
The build never installs it over an existing node.

## Windows installer

Install Inno Setup 6 and run `./installer/build-installer.ps1` in PowerShell.
It creates an unsigned, per-user test installer in `installer/output`. Official
release signing is a separate operator step. See installer/BUILD-INSTALLER.md.
Unsigned contributor builds must not be advertised as signed official releases.

## Testing limits

Use isolated regtest for native integration checks. The harness accepts an
explicit binary directory; inspect its `--help` before running it. Some test
drivers require Linux namespaces or Windows and cannot run on the other OS.
The full inherited native suite is not green yet. The September 10, 2026 run
found Bitcoin-specific expectations, including the fixed Bitcoin block hash in
`TestChain100Setup` and Bitcoin network/address vectors. Other failures still
need triage. Do not change Slithy's consensus rules to make those fixtures pass.

The GitHub workflow runs Slithy's proof-of-work and isolated integration checks,
along with the app, terms and publication checks. That is a defined regression
set, not a claim that every inherited Bitcoin test passes. Run the full `ctest`
command above when working on native test compatibility and report its result.
