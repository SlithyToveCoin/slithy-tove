//======================================================================
//                        Slithy Tove
//======================================================================


//===============================================
//               Treasury Status Calculator
//===============================================
namespace Slithy_Tove;

// Plain treasury status value used when the app calculates from chain height.
internal sealed record TreasuryStatus(
    long ChainHeight,
    decimal Accrued,
    string Address,
    bool IsDevelopment);

// Calculates what the treasury should have earned from block height alone.
internal static class TreasuryStatusCalculator
{
    public const string DevelopmentTreasuryAddress =
        "tslithy1quwm9s9qq9k03vdlus547p2x69uqvh50v3wffcu";

    private const long HalvingInterval = 1_051_200;
    private const long InitialSubsidyAtomic = 1_000_000_000;

    public static TreasuryStatus FromChainHeight(long chainHeight)
    {
        long remaining = Math.Max(0, chainHeight);
        decimal accruedAtomic = 0m;
        // Height is the last block, not a count including genesis. Core rounds
        // each reward in atomic units and starts its first halving at the interval.
        for (int era = 0; era < 64 && remaining > 0; era++)
        {
            long subsidy = InitialSubsidyAtomic >> era;
            if (subsidy == 0) break;
            long eraBlocks = Math.Min(remaining, era == 0 ? HalvingInterval - 1 : HalvingInterval);
            accruedAtomic += (decimal)eraBlocks * (subsidy / 10);
            remaining -= eraBlocks;
        }

        return new TreasuryStatus(
            chainHeight,
            accruedAtomic / 100_000_000m,
            DevelopmentTreasuryAddress,
            true);
    }
}

