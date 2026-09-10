# Public node and treasury snapshots

These are the source implementations used by the node status and treasury
publication checks. No deployed configuration or SSH key is included.

The status collector calls a configured local CLI and caches results. HTTP
requests read that cache; they do not execute arbitrary RPC calls. It binds
to loopback by default. Set absolute SLITHY_STATUS_CLI and SLITHY_STATUS_CONFIG
paths and the expected public treasury identity before running it.

The snapshot exporter requires an operator-supplied target configuration and
verified SSH host keys. The treasury publisher validates the snapshot's identity,
freshness and amounts before replacing a public JSON file. Run either script
with --help to see its required arguments.

These services are not needed to build or run the wallet. Do not expose node RPC
or wallet credentials to visitors. Use tests/security/test_status_and_archives.py
and test_treasury_publication.py for disposable checks.
