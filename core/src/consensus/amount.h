// Copyright (c) 2009-2010 Satoshi Nakamoto
// Copyright (c) 2009-present The Bitcoin Core developers
// Distributed under the MIT software license, see the accompanying
// file COPYING or http://www.opensource.org/licenses/mit-license.php.

#ifndef BITCOIN_CONSENSUS_AMOUNT_H
#define BITCOIN_CONSENSUS_AMOUNT_H

#include <cstdint>

/** Amount in satoshis (Can be negative) */
typedef int64_t CAmount;

/** Base units in one SLTHY. */
static constexpr CAmount COIN = 100000000;

/** Reject amounts outside this range during consensus validation.
 * The reward schedule fits below 21,024,000 SLTHY after integer rounding.
 * This bound does not create coins or change the block subsidy. Changing it
 * changes a consensus rule, so node releases must coordinate its adoption.
 */
static constexpr CAmount MAX_MONEY = 21024000 * COIN;
inline bool MoneyRange(const CAmount& nValue) { return (nValue >= 0 && nValue <= MAX_MONEY); }

#endif // BITCOIN_CONSENSUS_AMOUNT_H
