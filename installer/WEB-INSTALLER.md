# Windows web installer

This document is for release maintainers.

The web installer downloads a full signed installer from a separate signed feed:
`https://slithy.io/updates/windows/installer/stable.json`. It does not use the
desktop ZIP update feed and never installs an unsigned fallback.

The small installer has its own version, starting at 1.0.0. Ordinary wallet
releases do not require rebuilding it. Fix and replace it when its own code,
trust key or installation requirements change. A stable file is not a promise
that Windows will stop showing reputation warnings.

## Build and sign

Run `installer/build-web-installer.ps1` for a local unsigned build.
For a public release, use `-Sign` and pass `-SignToolPath`,
`-SigningLibraryPath` and `-SigningMetadataPath`. These refer to the installed
Azure signing tools and your private account configuration. No account
credentials belong in this repository.

The project is self-contained and uses the existing installer artwork.
Controls live in SetupForm.Designer.cs for editing in Visual Studio.

## Publish a wallet release

1. Build and sign a full offline installer from the reviewed release commit.
   Sign the bundled Slithy executables as part of that build.
2. Run `new-installer-manifest.ps1` in PowerShell 7 with the installer path,
   release version, full source commit, private release key path and output path.
   The private release key is separate from the Azure publisher certificate.
3. Upload the full installer to the versioned URL in that manifest. Verify its
   server-side hash before atomically replacing the installer feed.
4. Test the signed web installer from a clean Windows account. Check cancel,
   retry, offline failure, invalid signatures, installation, repair and uninstall.
5. Link the signed web installer on the downloads page and retain a clearly
   labeled current offline installer. Back up the website design before editing it.

The initial release requires wallet version 0.1.42 or newer. Do not point it at
the older beta to bypass the minimum version check. Coordinate native consensus
changes with the official nodes before publishing a new full installer.

The download stays in a unique temporary directory. Closing during download
cancels it. Once the full setup opens, finish or cancel in that setup window.
The wrapper waits for setup to exit and removes its temporary download.
It never starts a node or opens a wallet by itself.

## Verification

The wrapper verifies the ECDSA release envelope, checks the SHA256 package hash,
and asks Windows to verify Authenticode and the expected publisher. Redirects
are disabled. No TLS or signature checks are bypassed. If Windows cannot verify
the signature, installation stops.

Run `dotnet run --project tests/installer/InstallerTests.csproj -c Release`
for manifest, publisher-rejection and chain-progress regression tests.

Current-source packaging and Azure signing remain release gates. Building the
wrapper does not publish a manifest, change public downloads, or sign anything
unless the signing switch and configuration are supplied.
