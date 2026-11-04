# Source provenance

The native source is a Slithy-modified Bitcoin Core v31.0 tree. Its upstream base
is commit `6574cb40869b96b9ffc79c19dc8f4e467d60f321` from Bitcoin Core.

## Initial source publication

The initial source snapshot comes from the beta build tree recorded on
September 10, 2026. Dependency recipes come from the upstream commit above.

The recovered source archive has SHA256
`443e593580a876c02d7ee142232d848703533ef6a5434013c84ccc9b386d97f7`.
The upstream dependency archive has SHA256
`5dfa78104a70118d722faae80adbc34590b1b5a24b7eb715a667714159ac47a1`.
Upstream license headers remain in the source files.

SOURCE-MANIFEST.json records checkout file bytes. Git archive can expand the
upstream version placeholder in core/src/clientversion.cpp through export-subst;
compare committed blobs or a normal clone when checking the manifest.

The beta genesis is
`000f888cdb70403cd5310d02d7983951ee146ac799485bca337a7b52c24643f2`.
The network identity is also recorded in `site/install/linux/slithy-network.json`
and checked against the core by the Slithy regression tests.

Windows 0.1.41 and Linux 0.1.48-testnet were published before this repository.
Their source was identified from build records. This does not establish a
byte-for-byte match with the downloads. Signing, compiler versions and embedded
build information can change binary hashes. Changes made here after September 10
are not part of those earlier downloads.

## Matching a download to its source

The version numbers above describe that September snapshot, not the latest
downloads. Each current download has a source link on the downloads page.
Check its release record for the commit and file hashes.

For a new release, record the public commit, compiler versions, dependency
inputs and package hashes together. Build from the reviewed source commit.
Use separate release records for the beta and live networks.
