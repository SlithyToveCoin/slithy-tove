# Source provenance

The native source is a Slithy-modified Bitcoin Core v31.0 tree. Its upstream base
is commit `6574cb40869b96b9ffc79c19dc8f4e467d60f321` from Bitcoin Core.

The source snapshot was recovered from the beta generation's build tree on
September 10, 2026. Both recorded native CMake build caches point to that tree.
The dependency recipes were recovered from the upstream commit, not from a
directory of compiled dependencies.

The recovered source archive has SHA256
`443e593580a876c02d7ee142232d848703533ef6a5434013c84ccc9b386d97f7`.
The upstream dependency archive has SHA256
`5dfa78104a70118d722faae80adbc34590b1b5a24b7eb715a667714159ac47a1`.
Temporary pre-change copies and private treasury setup instructions were excluded.
Upstream license headers remain in the source files.

The beta genesis is
`000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2`.
The network identity is also recorded in `site/install/linux/slithy-network.json`
and checked against the core by the Slithy regression tests.

Windows 0.1.41 and Linux 0.1.48-testnet are the published app versions at this
source preparation date. Their native source provenance is based on build-tree
records, not a claim of byte-for-byte reproducibility. Code signing, toolchain
versions and embedded build metadata affect binary hashes. The first public
source commit must not be described as the historical commit that built those
already-published artifacts.

Future releases should record the public commit, compiler versions, dependency
inputs and package hashes together. Tag a reviewed source commit before building
a release from it. Keep future live-network preparation distinct from beta tags.
