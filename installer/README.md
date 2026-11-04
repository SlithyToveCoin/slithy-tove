# Slithy Tove Windows installer

This folder contains the Inno Setup installer for the Windows Slithy Tove wallet.

Official publisher: **CS Idea Labs LLC**. The installer and Slithy-owned application binaries should identify this company as their publisher. Upstream binaries and third-party components retain their original copyright and attribution.

Current installer:

```text
installer\output\Slithy-Tove-Setup-<project-version>.exe
```

The installer uses the Slithy logo, a dark green welcome panel, and simple test-network wording. It installs into the current user's local app folder, not Program Files, so normal users do not need administrator rights.

Wallets, chain data, settings, and logs are intentionally preserved when the app is uninstalled.

## Build

Build the native Windows tools first using docs/BUILDING.md. From the project root:

```powershell
.\installer\build-installer.ps1
```

## For release maintainers

To sign a release, configure an Azure Artifact Signing account and certificate profile. The account running the build needs permission to sign with that profile. Prepare the tools and a local configuration file:

```powershell
.\installer\prepare-signing-tools.ps1
Copy-Item .\installer\azure-signing-metadata.sample.json .\installer\azure-signing-metadata.json
```

Replace the placeholders with your signing account and certificate profile. Use the endpoint for your account's region; the sample uses East US.

Authenticate with Azure CLI using an account that has signing permission, then run:

```powershell
.\installer\build-installer.ps1 -Sign
```

The script signs app files first, then signs the Inno installer and signed uninstaller through Azure Artifact Signing.

Do not commit `installer\azure-signing-metadata.json`, authentication tokens or private keys. Sign contributor builds with your own publisher identity.

## Tooling

Required:

- .NET SDK
- Inno Setup 6
- Azure Artifact Signing Client Tools for signed builds

Microsoft's current signing command uses SignTool, `Azure.CodeSigning.Dlib.dll`, and the metadata JSON file. Time stamping is required because Artifact Signing certificates have a short validity window.
