// Copyright (c) 2026 The Slithy Tove Core developers
// Distributed under the MIT software license, see the accompanying
// file COPYING or https://opensource.org/license/mit/.

#include <arith_uint256.h>
#include <consensus/amount.h>
#include <consensus/merkle.h>
#include <crypto/hex_base.h>
#include <primitives/block.h>
#include <primitives/transaction.h>
#include <script/script.h>
#include <uint256.h>

#include <algorithm>
#include <atomic>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iomanip>
#include <iostream>
#include <limits>
#include <string>
#include <thread>
#include <vector>

using namespace util::hex_literals;

namespace {

constexpr const char* GENESIS_TIMESTAMP =
    "Slithy Tove beta reset: help test the reading fund, 9 September 2026";
constexpr const char* INITIAL_TARGET =
    "003fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff";
constexpr uint32_t GENESIS_TIME = 1788912000;

CBlock CreateSlithyGenesis(uint32_t nonce, uint32_t bits)
{
    CMutableTransaction tx;
    tx.version = 1;
    tx.vin.resize(1);
    tx.vout.resize(1);
    tx.vin[0].scriptSig = CScript()
        << 486604799
        << CScriptNum(4)
        << std::vector<unsigned char>(
               reinterpret_cast<const unsigned char*>(GENESIS_TIMESTAMP),
               reinterpret_cast<const unsigned char*>(GENESIS_TIMESTAMP) + std::strlen(GENESIS_TIMESTAMP));
    tx.vout[0].nValue = 0;
    tx.vout[0].scriptPubKey = CScript() << OP_RETURN << "536c6974687920546f7665"_hex;

    CBlock genesis;
    genesis.nTime = GENESIS_TIME;
    genesis.nBits = bits;
    genesis.nNonce = nonce;
    genesis.nVersion = 1;
    genesis.vtx.push_back(MakeTransactionRef(std::move(tx)));
    genesis.hashPrevBlock.SetNull();
    genesis.hashMerkleRoot = BlockMerkleRoot(genesis);
    return genesis;
}

} // namespace

int main(int argc, char* argv[])
{
    unsigned int thread_count = std::max(1u, std::thread::hardware_concurrency());
    if (argc == 2) {
        thread_count = std::max(1, std::atoi(argv[1]));
    } else if (argc > 2) {
        std::cerr << "Usage: slithy-genesis [threads]\n";
        return EXIT_FAILURE;
    }

    const uint256 target_hex{INITIAL_TARGET};
    const arith_uint256 target{UintToArith256(target_hex)};
    const uint32_t bits{target.GetCompact()};
    const CBlock base{CreateSlithyGenesis(0, bits)};

    std::atomic<bool> found{false};
    std::atomic<uint64_t> attempts{0};
    uint32_t found_nonce{0};
    uint256 found_hash;

    std::cout << "timestamp=" << GENESIS_TIMESTAMP << "\n"
              << "time=" << GENESIS_TIME << "\n"
              << "target=" << INITIAL_TARGET << "\n"
              << "bits=" << std::hex << std::setw(8) << std::setfill('0') << bits << std::dec << "\n"
              << "merkle=" << base.hashMerkleRoot.ToString() << "\n"
              << "threads=" << thread_count << "\n";

    std::vector<std::thread> workers;
    workers.reserve(thread_count);
    for (unsigned int thread_id = 0; thread_id < thread_count; ++thread_id) {
        workers.emplace_back([&, thread_id] {
            CBlock candidate{base};
            for (uint64_t nonce = thread_id;
                 nonce <= std::numeric_limits<uint32_t>::max() && !found.load(std::memory_order_relaxed);
                 nonce += thread_count) {
                candidate.nNonce = static_cast<uint32_t>(nonce);
                const uint256 hash{candidate.GetHash()};
                attempts.fetch_add(1, std::memory_order_relaxed);
                if (UintToArith256(hash) <= target) {
                    bool expected{false};
                    if (found.compare_exchange_strong(expected, true)) {
                        found_nonce = candidate.nNonce;
                        found_hash = hash;
                    }
                    break;
                }
            }
        });
    }

    for (auto& worker : workers) worker.join();

    if (!found.load()) {
        std::cerr << "No valid nonce found in the 32-bit range.\n";
        return EXIT_FAILURE;
    }

    std::cout << "nonce=" << found_nonce << "\n"
              << "hash=" << found_hash.ToString() << "\n"
              << "attempts=" << attempts.load() << "\n";
    return EXIT_SUCCESS;
}
