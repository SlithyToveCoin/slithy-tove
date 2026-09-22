# Slithy Tove

Slithy is cryptocurrency software for CPU mining, with a Windows wallet and
a Linux terminal interface. Ten percent of the block subsidy goes to a
public literacy treasury. Transaction fees go to the miner.

Slithy is in public beta. Test coins have no market value. The test chain and
wallets will reset before the live launch, currently planned for November 1, 2026.

## Try Slithy

Download the [Windows installer or Linux package](https://slithy.io/downloads.html).
The [user guide](https://slithy.io/guide.html) explains wallet recovery, mining
and balances. Join the [community](https://slithy.io/community.html) to ask
questions or share how testing went.

## Build and test

To build from source, follow the [build guide](docs/BUILDING.md). It includes
commands for the Windows app, Linux tools and tests.

The September 15 native test run passed 153 tests. One optional test needs an
external dataset and was skipped. See the build guide's testing limits before
treating a successful build as a completed security review.

The Windows app runs a local node and mines on your computer. Official public
nodes relay and verify blocks; they do not mine on behalf of app users.

## Source layout

- `core/`: complete Bitcoin-derived yespower core and upstream build recipes.
- `Desktop/Slithy Tove/`: Windows Forms wallet, including Designer files.
- `shared/` and `tools/SlithySeedTool/`: shared recovery-word implementation.
- `site/install/linux/`: terminal menu, installer and updater.
- `installer/`: per-user Windows installer source and artwork.
- `site/`: website pages and download scripts.
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
