# Building the Windows installer

Build the native Windows programs first using docs/BUILDING.md. Install
Inno Setup 6, then run from the source root:

```powershell
./installer/build-installer.ps1
```

The script reads the app version, creates a fresh staging directory and writes
`installer/output/Slithy-Tove-Setup-<version>.exe`. Do not compile SlithyTove.iss
directly with an old staging tree.

Contributor installers are unsigned. They install per user without administrator
rights and preserve wallet data on uninstall. Use disposable test profiles.

## Official signing

Signing is an operator step, not a build prerequisite. Operators need an active
Azure Artifact Signing profile and permission to sign. Copy the sample metadata
to the ignored `installer/azure-signing-metadata.json` and configure your own
account, endpoint and profile. Never commit authentication material.

```powershell
./installer/prepare-signing-tools.ps1
./installer/build-installer.ps1 -Sign
```

Authenticate with Azure CLI before the signed build. The script signs Slithy-owned
programs and verifies their signatures. It does not sign Microsoft runtime files.
The installer is signed after packaging. Generate checksums and update manifests
after signing because signing changes the file hashes.

A build does not publish anything. Upload complete versioned packages before
their signed update feeds. Signing credentials must never be available to
pull-request builds from outside contributors.
