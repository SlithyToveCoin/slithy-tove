# Linux native build for the November 2026 security update

The Linux GUI 0.1.49-beta.1 and terminal package 0.1.51-testnet use native source commit `fdbd90d108053269f343e2febd6871ec8ece3e0b`. Their native tools link libevent 2.1.13-stable statically. The usual `tools/build-core.sh` Linux build uses your installed libevent, so use the commands below to match this release's dependency choice.

The release was built on Ubuntu 24.04.3 LTS x86_64 with GCC 13.3.0 and CMake 3.28.3. SQLite, the C/C++ runtime and system libraries remain dynamically linked. These instructions record the source and build configuration. They do not promise byte-identical binaries across build directories or different system packages.

Start with a fresh checkout and the Linux build dependencies listed in `docs/BUILDING.md`:

```sh
git clone https://github.com/SlithyToveCoin/slithy-tove.git
cd slithy-tove
git checkout fdbd90d108053269f343e2febd6871ec8ece3e0b
source_root="$PWD"
mkdir -p build/release-deps
cd build/release-deps
curl --proto '=https' --tlsv1.2 -fL \
  https://github.com/libevent/libevent/releases/download/release-2.1.13-stable/libevent-2.1.13-stable.tar.gz \
  -o libevent-2.1.13-stable.tar.gz
printf '%s\n' 'f7e9383b8c0baa81b687e5b5eecc01beefaf1b19b64151d95ed61647fe7a315c  libevent-2.1.13-stable.tar.gz' | sha256sum -c -
tar -xzf libevent-2.1.13-stable.tar.gz
cd libevent-2.1.13-stable
for patch_name in cmake_fixups.patch netbsd_fixup.patch winver_fixup.patch; do
  patch -p1 < "$source_root/core/depends/patches/libevent/$patch_name"
done
```

Those three patches come from the pinned Slithy source. No additional edits were made to the libevent source for this release. Build both libevent library types and select the static libraries when building the node:

```sh
cmake -S . -B ../libevent-build \
  -DCMAKE_BUILD_TYPE=Release \
  -DEVENT__LIBRARY_TYPE=BOTH \
  -DEVENT__DISABLE_OPENSSL=ON \
  -DEVENT__DISABLE_BENCHMARK=ON \
  -DEVENT__DISABLE_SAMPLES=ON \
  -DEVENT__DISABLE_TESTS=OFF \
  -DEVENT__DISABLE_REGRESS=OFF
cmake --build ../libevent-build -j4
event_build="$source_root/build/release-deps/libevent-build"
cd "$source_root"
cmake -S core -B build/release-native \
  -DCMAKE_BUILD_TYPE=Release \
  -DBUILD_DAEMON=ON -DBUILD_CLI=ON -DBUILD_TX=ON \
  -DBUILD_WALLET_TOOL=ON -DBUILD_TESTS=ON -DBUILD_BENCH=OFF \
  -DBUILD_GUI=OFF -DBUILD_BITCOIN_BIN=OFF -DBUILD_UTIL=OFF \
  -DENABLE_IPC=OFF -DWITH_ZMQ=OFF -DWITH_USDT=OFF \
  -DLibevent_DIR="$event_build" -DLIBEVENT_STATIC_LINK=TRUE
cmake --build build/release-native -j4
```

Thread support in libevent remains enabled. The node links against its build-tree configuration; libevent does not need to be installed into `/usr` or copied over system libraries.

Run the native tests before packaging. Stage copies of the four executables and strip those copies:

```sh
ctest --test-dir build/release-native --output-on-failure
mkdir -p build/release-dist/bin
for name in slithyd slithy-cli slithy-wallet slithy-tx; do
  cp "build/release-native/bin/$name" "build/release-dist/bin/$name"
  strip --strip-unneeded "build/release-dist/bin/$name"
  sha256sum "build/release-dist/bin/$name"
done
ldd build/release-dist/bin/slithyd
strings build/release-dist/bin/slithyd | grep -F '2.1.13-stable'
```

The daemon's `ldd` output should not list a shared libevent library. The version-string check is a quick sanity check, not proof that a dependency has no vulnerabilities. Test the staged files with the isolated wallet/mining checks before you package them. The GUI archive bundles `slithyd`; the terminal archive bundles all four native tools.
