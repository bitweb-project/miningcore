using System.Data;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Model.Projections;

namespace Miningcore.Persistence.Repositories;

public interface IShareRepository
{
    Task BatchInsertAsync(IDbConnection con, IDbTransaction tx, IEnumerable<Share> shares, CancellationToken ct);
    Task<Share[]> ReadSharesBeforeAsync(IDbConnection con, string poolId, DateTime before, bool inclusive, int pageSize, CancellationToken ct);
    Task<long> CountSharesBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, DateTime before, CancellationToken ct);
    Task DeleteSharesBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, DateTime before, CancellationToken ct);
    // Bounded by 'created <= before' (the just-processed block's Created timestamp).
    // Used by SOLOPaymentScheme so a just-paid block never deletes shares that belong to a
    // later, not-yet-classified block by the same miner (those are still needed for that
    // block's Effort/MinerEffort calculation and for the live hashrate window).
    Task<long> CountSharesByMinerBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, DateTime before, CancellationToken ct);
    Task DeleteSharesByMinerBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, DateTime before, CancellationToken ct);
    Task<double?> GetAccumulatedShareDifficultyBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct);
    Task<double?> GetMinerShareDifficultyBetweenAsync(IDbConnection con, string poolId, string miner, DateTime start, DateTime end, CancellationToken ct);
    Task<double?> GetEffectiveAccumulatedShareDifficultyBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct);
    Task<double?> GetEffortBetweenCreatedAsync(IDbConnection con, string poolId, double shareConst, DateTime start, DateTime end, CancellationToken ct);
    Task<double?> GetMinerEffortBetweenCreatedAsync(IDbConnection con, string poolId, string miner, DateTime start, DateTime end, CancellationToken ct);
    Task<MinerWorkerHashes[]> GetHashAccumulationBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct);
    Task<string[]> GetRecentyUsedIpAddressesAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, CancellationToken ct);
    Task<string[]> GetRecentlyUsedPasswordsAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, CancellationToken ct);

    Task<KeyValuePair<string, double>[]> GetAccumulatedUserAgentShareDifficultyBetweenAsync(IDbConnection con, string poolId,
        DateTime start, DateTime end, bool byVersion, CancellationToken ct);
}
