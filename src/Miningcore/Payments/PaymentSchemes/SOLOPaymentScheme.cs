using System.Data;
using Miningcore.Mining;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Repositories;
using NLog;
using Contract = Miningcore.Contracts.Contract;

namespace Miningcore.Payments.PaymentSchemes;

/// <summary>
/// SOLO payout scheme implementation
/// </summary>
/// ReSharper disable once InconsistentNaming
public class SOLOPaymentScheme : IPayoutScheme
{
    public SOLOPaymentScheme(
        IShareRepository shareRepo,
        IBalanceRepository balanceRepo)
    {
        Contract.RequiresNonNull(shareRepo);
        Contract.RequiresNonNull(balanceRepo);

        this.shareRepo = shareRepo;
        this.balanceRepo = balanceRepo;
    }

    private readonly IBalanceRepository balanceRepo;
    private readonly IShareRepository shareRepo;
    private static readonly ILogger logger = LogManager.GetLogger("SOLO Payment");

    #region IPayoutScheme

    public async Task UpdateBalancesAsync(IDbConnection con, IDbTransaction tx, IMiningPool pool, IPayoutHandler payoutHandler,
        Block block, decimal blockReward, CancellationToken ct)
    {
        var poolConfig = pool.Config;

        // calculate rewards
        var rewards = new Dictionary<string, decimal>();
        var shareCutOffDate = CalculateRewards(block, blockReward, rewards, ct);

        // update balances
        foreach(var address in rewards.Keys)
        {
            var amount = rewards[address];

            if(amount > 0)
            {
                logger.Info(() => $"Crediting {address} with {payoutHandler.FormatAmount(amount)} for block {block.BlockHeight}");

                await balanceRepo.AddAmountAsync(con, tx, poolConfig.Id, address, amount, $"Reward for block {block.BlockHeight}");
            }
        }

        // delete discarded shares
        // Bounded by shareCutOffDate (= this block's Created timestamp): shares that arrived
        // AFTER this block belong to the miner's *next* round and must survive this cleanup.
        // The old unbounded delete (by miner only, no date) wiped those out whenever this run
        // classified more than one of the same miner's blocks together (e.g. after a batch of
        // NewChainHeightNotification confirmations) — the earlier block's cleanup deleted the
        // shares the later block still needed for its own Effort/MinerEffort calculation,
        // permanently leaving it with a null ("--") effort, and briefly zeroing that miner's
        // live hashrate stats in StatsRecorder until fresh shares accumulated again.
        if(shareCutOffDate.HasValue)
        {
            var cutOffCount = await shareRepo.CountSharesByMinerBeforeAsync(con, tx, poolConfig.Id, block.Miner, shareCutOffDate.Value, ct);

            if(cutOffCount > 0)
            {
                logger.Info(() => $"Deleting {cutOffCount} discarded shares for {block.Miner} up to {shareCutOffDate.Value:O}");

                await shareRepo.DeleteSharesByMinerBeforeAsync(con, tx, poolConfig.Id, block.Miner, shareCutOffDate.Value, ct);
            }
        }
    }

    #endregion // IPayoutScheme

    private DateTime? CalculateRewards(Block block, decimal blockReward, Dictionary<string, decimal> rewards, CancellationToken ct)
    {
        rewards[block.Miner] = blockReward;

        return block.Created;
    }
}
