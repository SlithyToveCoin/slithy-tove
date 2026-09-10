# Slithy Tove

Slithy is cryptocurrency software for CPU mining, with a Windows wallet and
a Linux terminal interface. Ten percent of the block subsidy goes to a
public literacy treasury. Transaction fees go to the miner.

This is beta software. The live network has not launched. Test coins have no
promised value, and the test chain and wallets will be reset before the live
launch. Do not use a test wallet to hold anything you cannot afford to lose.

## Build and test

Start with [the build guide](docs/BUILDING.md). It covers the native tools,
Windows app, Linux package and isolated tests. You do not need access to the
official servers or an Azure signing account to build the software.

The Windows app runs a local node and mines on your computer. Official public
nodes relay and verify blocks; they do not mine on behalf of app users.

## Source layout

- `core/`: complete Bitcoin-derived yespower core and upstream build recipes.
- `Desktop/Slithy Tove/`: Windows Forms wallet, including Designer files.
- `shared/` and `tools/SlithySeedTool/`: shared recovery-word implementation.
- `site/install/linux/`: terminal menu, installer and updater.
- `installer/`: per-user Windows installer source and artwork.
- `site/`: public website source. Live packages and private analytics are not included.
- `tests/`: Slithy regression checks. Upstream native tests are under `core/`.
- `services/`: read-only node snapshots and validated treasury publication.

See [source provenance](docs/SOURCE-PROVENANCE.md), [contribution guidance](CONTRIBUTING.md)
and [security reporting](SECURITY.md). Upstream documentation under `core/doc`
uses Bitcoin names. Follow the Slithy build guide for this project.

CS Idea Labs LLC publishes the official software and operates slithy.io.
The literacy treasury is initially controlled by the project operator.
Community voting is planned; it is not implemented as a binding voting system.

Original code is MIT licensed. Read [NOTICE](NOTICE) for upstream licenses and
the separate treatment of branding. The beta service terms do not replace
the software licenses.

Official downloads and user guide: https://slithy.io
