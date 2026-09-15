# Network test coverage

Use `-testnet` or `-chain=test` for Slithy's public beta network. Test coins
do not carry into the live network. Local automated tests use `-regtest`.

## Inherited Testnet4

Slithy does not support Bitcoin Testnet4. Its inherited genesis does not meet
Slithy's yespower target, and its bootstrap data belongs to Bitcoin. Selecting
`-testnet4` or `-chain=testnet4` now stops before node startup with an explanation.
The change does not create a replacement network or alter the public beta.

The mining-interface tests now run against Slithy's beta genesis. They verify
that a late block does not lower beta difficulty. A separate regtest case tests
the minimum-difficulty refresh at two target block intervals, including the
exact boundary and the following second.

## Snapshot fixtures

The height-110 regtest fixture contains 220 unspent outputs: a miner output
and a treasury output for each block. Its block and UTXO hashes were reproduced
in separate test runs before replacing the inherited Bitcoin fixture hashes.
Snapshot rejection, activation, restart and background-validation tests remain
enabled. The tests check both reward outputs after loading the snapshot.

These fixtures are for local tests. No public snapshot is being trusted or
published by this change. Other inherited fuzz and functional snapshot fixtures
need their own validation when those test sets are run.

The IBD fixture uses a minimum-work value of one so it can test a tip below the
threshold without subtracting from zero. Production minimum-work rules are
unchanged.
