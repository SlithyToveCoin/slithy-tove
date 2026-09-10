// Copyright (c) 2020-present The Bitcoin Core developers
// Distributed under the MIT software license, see the accompanying
// file COPYING or http://www.opensource.org/licenses/mit-license.php.

#ifndef BITCOIN_RPC_MINING_H
#define BITCOIN_RPC_MINING_H

#include <uint256.h>
#include <chrono>
#include <cstdint>

/** Refresh work when its parent changes or its transaction selection grows old. */
inline bool SlithyCpuWorkExpired(const uint256& parent, const uint256& tip, std::chrono::milliseconds age)
{
    return parent != tip || age >= std::chrono::seconds{5};
}

/** Default max iterations to try in RPC generatetodescriptor, generatetoaddress, and generateblock. */
static const uint64_t DEFAULT_MAX_TRIES{1000000};

/** Join local CPU workers before destroying the node context. */
void StopSlithyCpuMiner();

#endif // BITCOIN_RPC_MINING_H
