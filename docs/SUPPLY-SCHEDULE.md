# Slithy Tove supply schedule

The initial subsidy stays at 10 SLTHY. It halves every 1,051,200 blocks.
The source uses an amount validation limit of 21,024,000 SLTHY so the full
reward schedule fits within that limit.

Summing all reward eras in whole base units gives 21,023,999.863344 SLTHY
before excluding height zero. Genesis has no spendable reward, so the maximum
scheduled issuance from height one is 21,023,989.863344 SLTHY. Actual issuance
can be lower if miners claim less than the permitted reward. Transaction fees
transfer existing coins and do not increase this total.

The treasury share comes from each subsidy. It is not extra issuance.
Slithy uses eight decimal places; reward halvings round down to a whole base
unit. The rounded amount limit is a validation bound, not a promise that every
coin below that bound will be issued.

## Release status

This change is in the source repository. It is not part of the September 10 beta
downloads. MAX_MONEY affects consensus validation. Nodes running different limits
can disagree about valid amounts, so adoption needs a coordinated software release.
The block subsidy, halving interval and treasury percentage are unchanged.
