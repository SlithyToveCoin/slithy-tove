// Copyright (c) 2025-present The Bitcoin Core developers
// Distributed under the MIT software license, see the accompanying
// file COPYING or http://www.opensource.org/licenses/mit-license.php.

#include <common/system.h>
#include <chainparams.h>
#include <chainparamsbase.h>
#include <interfaces/mining.h>
#include <node/miner.h>
#include <util/time.h>
#include <validation.h>

#include <test/util/setup_common.h>

#include <boost/test/unit_test.hpp>

using interfaces::BlockTemplate;
using interfaces::Mining;
using node::BlockAssembler;
using node::BlockWaitOptions;

namespace testnet_miner_tests {

struct TestnetMinerTestingSetup : public TestingSetup {
    TestnetMinerTestingSetup() : TestingSetup{ChainType::TESTNET} {}
    std::unique_ptr<Mining> MakeMining()
    {
        return interfaces::MakeMining(m_node, /*wait_loaded=*/false);
    }
};
} // namespace testnet_miner_tests

BOOST_FIXTURE_TEST_SUITE(testnet_miner_tests, TestnetMinerTestingSetup)

BOOST_AUTO_TEST_CASE(UnsupportedTestnet4)
{
    BOOST_CHECK_EXCEPTION(SelectBaseParams(ChainType::TESTNET4), std::runtime_error,
        [](const std::runtime_error& error) {
            return std::string{error.what()}.find("Use -testnet or -chain=test") != std::string::npos;
        });
}

BOOST_AUTO_TEST_CASE(MiningInterface)
{
    auto mining{MakeMining()};
    BOOST_REQUIRE(mining);

    BlockAssembler::Options options;
    options.include_dummy_extranonce = true;
    std::unique_ptr<BlockTemplate> block_template;

    // Set node time a few minutes past the Slithy beta genesis block.
    const int64_t genesis_time{WITH_LOCK(cs_main, return m_node.chainman->ActiveChain().Tip()->GetBlockTime())};
    SetMockTime(genesis_time + 3 * 60);

    block_template = mining->createNewBlock(options, /*cooldown=*/false);
    BOOST_REQUIRE(block_template);

    // The template should use the mocked system time
    BOOST_REQUIRE_EQUAL(block_template->getBlockHeader().nTime, genesis_time + 3 * 60);

    const BlockWaitOptions wait_options{.timeout = MillisecondsDouble{0}, .fee_threshold = 1};

    // waitNext() should return nullptr because there is no better template
    auto should_be_nullptr = block_template->waitNext(wait_options);
    BOOST_REQUIRE(should_be_nullptr == nullptr);

    // At the minimum-difficulty boundary the current template is still valid.
    const auto difficulty_delay = 2 * Params().GetConsensus().nPowTargetSpacing;
    {
        LOCK(cs_main);
        SetMockTime(m_node.chainman->ActiveChain().Tip()->GetBlockTime() + difficulty_delay);
    }
    should_be_nullptr = block_template->waitNext(wait_options);
    BOOST_REQUIRE(should_be_nullptr == nullptr);

    // Slithy's beta does not lower difficulty because a block is late.
    {
        LOCK(cs_main);
        SetMockTime(m_node.chainman->ActiveChain().Tip()->GetBlockTime() + difficulty_delay + 1);
    }
    block_template = block_template->waitNext(wait_options);
    BOOST_CHECK(!Params().GetConsensus().fPowAllowMinDifficultyBlocks);
    BOOST_CHECK(block_template == nullptr);
}

BOOST_AUTO_TEST_SUITE_END()

BOOST_FIXTURE_TEST_SUITE(regtest_difficulty_refresh_tests, RegTestingSetup)

BOOST_AUTO_TEST_CASE(MinimumDifficultyDelay)
{
    auto mining = interfaces::MakeMining(m_node, false);
    BlockAssembler::Options options;
    options.include_dummy_extranonce = true;
    const auto tip_time = WITH_LOCK(cs_main, return m_node.chainman->ActiveChain().Tip()->GetBlockTime());
    const auto delay = 2 * Params().GetConsensus().nPowTargetSpacing;
    BOOST_REQUIRE(Params().GetConsensus().fPowAllowMinDifficultyBlocks);
    SetMockTime(tip_time + delay);
    auto block_template = mining->createNewBlock(options, false);
    BOOST_REQUIRE(block_template);
    const BlockWaitOptions wait{.timeout = MillisecondsDouble{0}, .fee_threshold = 1};
    BOOST_CHECK(block_template->waitNext(wait) == nullptr);
    SetMockTime(tip_time + delay + 1);
    BOOST_CHECK(block_template->waitNext(wait) != nullptr);
}

BOOST_AUTO_TEST_SUITE_END()
