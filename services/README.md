# Public node and treasury snapshots

These services collect public node statistics and treasury balances. Their
configuration and SSH keys belong outside the website directory.

The status collector calls a configured local CLI and caches results. HTTP
requests read that cache; they do not execute arbitrary RPC calls. It binds
to loopback by default. Set absolute SLITHY_STATUS_CLI and SLITHY_STATUS_CONFIG
paths and the expected public treasury identity before running it.

The snapshot exporter requires an operator-supplied target configuration and
verified SSH host keys. Both publishers discard fields outside their public
schema. The treasury publisher also checks the network, address, timestamp and
balance totals before replacing its JSON file. A failed check leaves the last
published file untouched. Run either script with --help for its arguments.

Do not expose node RPC or wallet credentials to visitors. Run the disposable
tests in tests/security/test_status_and_archives.py, test_node_publication.py
and test_treasury_publication.py after changing the collector or publishers.
