// Copyright (c) 2009-2010 Satoshi Nakamoto
// Copyright (c) 2009-present The Bitcoin Core developers
// Distributed under the MIT software license, see the accompanying
// file COPYING or http://www.opensource.org/licenses/mit-license.php.

#include <primitives/block.h>

#include <crypto/yespower/yespower.h>
#include <hash.h>
#include <streams.h>
#include <tinyformat.h>

#include <memory>
#include <span>
#include <sstream>

uint256 CBlockHeader::GetHash() const
{
    std::vector<unsigned char> header_data;
    VectorWriter{header_data, 0, *this};

    static constexpr unsigned char SLITHY_YESPOWER_PERSONALIZATION[] = {
        'S', 'l', 'i', 't', 'h', 'y', ' ', 'T', 'o', 'v', 'e'
    };
    static constexpr yespower_params_t SLITHY_YESPOWER_PARAMS{
        YESPOWER_1_0,
        2048,
        8,
        SLITHY_YESPOWER_PERSONALIZATION,
        sizeof(SLITHY_YESPOWER_PERSONALIZATION)
    };

    yespower_binary_t hash{};
    int rc = yespower_tls(header_data.data(), header_data.size(), &SLITHY_YESPOWER_PARAMS, &hash);
    assert(rc == 0);

    return uint256{std::span<const unsigned char>{hash.uc, 32}};
}

std::string CBlock::ToString() const
{
    std::stringstream s;
    s << strprintf("CBlock(hash=%s, ver=0x%08x, hashPrevBlock=%s, hashMerkleRoot=%s, nTime=%u, nBits=%08x, nNonce=%u, vtx=%u)\n",
        GetHash().ToString(),
        nVersion,
        hashPrevBlock.ToString(),
        hashMerkleRoot.ToString(),
        nTime, nBits, nNonce,
        vtx.size());
    for (const auto& tx : vtx) {
        s << "  " << tx->ToString() << "\n";
    }
    return s.str();
}
