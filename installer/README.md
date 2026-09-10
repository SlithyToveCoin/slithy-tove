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

## Signed build

Finish the Azure Artifact Signing organization validation and certificate profile for **CS Idea Labs LLC** first.

Then copy:

```powershell
.\installer\prepare-signing-tools.ps1
Copy-Item .\installer\azure-signing-metadata.sample.json .\installer\azure-signing-metadata.json
```

Edit the new file with the real endpoint, signing account name, and certificate profile name.

Then run:

```powershell
.\installer\build-installer.ps1 -Sign
```

The script signs app files first, then signs the Inno installer and signed uninstaller through Azure Artifact Signing.

Do not commit `installer\azure-signing-metadata.json`.

## Tooling

Required:

- .NET SDK
- Inno Setup 6
- Azure Artifact Signing Client Tools for signed builds

Microsoft's current signing command uses SignTool, `Azure.CodeSigning.Dlib.dll`, and the metadata JSON file. Time stamping is required because Artifact Signing certificates have a short validity window.
