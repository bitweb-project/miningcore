using System.Data;
using System.Linq;
using MapsterMapper;
using Dapper;
using Miningcore.Persistence.Model;
using Miningcore.Persistence.Model.Projections;
using Miningcore.Persistence.Repositories;
using Npgsql;
using NpgsqlTypes;

namespace Miningcore.Persistence.Postgres.Repositories;

public class ShareRepository : IShareRepository
{
    public ShareRepository(IMapper mapper)
    {
        this.mapper = mapper;
    }

    private readonly IMapper mapper;

    public async Task BatchInsertAsync(IDbConnection con, IDbTransaction tx, IEnumerable<Share> shares, CancellationToken ct)
    {
        // NOTE: Even though the tx parameter is completely ignored here,
        // the COPY command still honors a current ambient transaction

        var pgCon = (NpgsqlConnection) con;

        const string query = @"COPY shares (poolid, blockheight, difficulty,
            networkdifficulty, miner, worker, useragent, ipaddress, source, created, mpassword) FROM STDIN (FORMAT BINARY)";

        // Drop shares with invalid networkdifficulty (<=0, NaN, Infinity) before they
        // reach the DB. Mirrors the guard already applied at read-time (aggregate
        // queries) and in the payout schemes, closing the write-time gap.
        var validShares = shares.Where(s => s.NetworkDifficulty > 0 &&
            !double.IsNaN(s.NetworkDifficulty) && !double.IsInfinity(s.NetworkDifficulty)).ToArray();

        await using(var writer = await pgCon.BeginBinaryImportAsync(query, ct))
        {
            foreach(var share in validShares)
            {
                await writer.StartRowAsync(ct);

                await writer.WriteAsync(share.PoolId, ct);
                await writer.WriteAsync((long) share.BlockHeight, NpgsqlDbType.Bigint, ct);
                await writer.WriteAsync(share.Difficulty, NpgsqlDbType.Double, ct);
                await writer.WriteAsync(share.NetworkDifficulty, NpgsqlDbType.Double, ct);
                await writer.WriteAsync(share.Miner, ct);
                await writer.WriteAsync(share.Worker, ct);
                await writer.WriteAsync(share.UserAgent, ct);
                await writer.WriteAsync(share.IpAddress, ct);
                await writer.WriteAsync(share.Source, ct);
                await writer.WriteAsync(share.Created, NpgsqlDbType.TimestampTz, ct);

                if(share.MinerPass != null)
                    await writer.WriteAsync(share.MinerPass, ct);
                else
                    await writer.WriteNullAsync(ct);
            }

            await writer.CompleteAsync(ct);
        }
    }

    public async Task<Share[]> ReadSharesBeforeAsync(IDbConnection con, string poolId, DateTime before,
        bool inclusive, int pageSize, CancellationToken ct)
    {
        var query = @$"SELECT * FROM shares WHERE poolid = @poolId AND created {(inclusive ? " <= " : " < ")} @before
            ORDER BY created DESC FETCH NEXT @pageSize ROWS ONLY";

        return (await con.QueryAsync<Entities.Share>(new CommandDefinition(query, new { poolId, before, pageSize }, cancellationToken: ct)))
            .Select(mapper.Map<Share>)
            .ToArray();
    }

    public Task<long> CountSharesBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, DateTime before, CancellationToken ct)
    {
        const string query = "SELECT count(*) FROM shares WHERE poolid = @poolId AND created < @before";

        return con.QuerySingleAsync<long>(new CommandDefinition(query, new { poolId, before }, tx, cancellationToken: ct));
    }

    public Task<double?> GetEffortBetweenCreatedAsync(IDbConnection con, string poolId, double shareConst, DateTime start, DateTime end, CancellationToken ct)
    {
        // NOTE: shareConst is intentionally NOT multiplied into the query.
        // share.Difficulty is stored as (stratumDiff / shareMultiplier) and
        // share.NetworkDifficulty is stored as (diff1 / networkTarget), so their
        // ratio already equals the fractional block-work contributed by the share.
        // Multiplying by shareConst (= shareMultiplier) would inflate the result
        // by exactly shareMultiplier (e.g. 65536×) — that was the original bug.
        const string query = "SELECT SUM(difficulty / NULLIF(networkdifficulty, 0)) FROM shares WHERE poolid = @poolId AND created > @start AND created < @end AND networkdifficulty > 0";

        return con.QuerySingleAsync<double?>(new CommandDefinition(query, new { poolId, start, end }, cancellationToken: ct));
    }

    public Task<double?> GetMinerEffortBetweenCreatedAsync(IDbConnection con, string poolId, string miner, DateTime start, DateTime end, CancellationToken ct)
    {
        const string query = "SELECT SUM(difficulty / NULLIF(networkdifficulty, 0)) FROM shares WHERE poolid = @poolId AND miner = @miner AND created > @start AND created < @end AND networkdifficulty > 0";

        return con.QuerySingleAsync<double?>(new CommandDefinition(query, new { poolId, miner, start, end }, cancellationToken: ct));
    }

    public Task<long> CountSharesByMinerBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, DateTime before, CancellationToken ct)
    {
        const string query = "SELECT count(*) FROM shares WHERE poolid = @poolId AND miner = @miner AND created <= @before";

        return con.QuerySingleAsync<long>(new CommandDefinition(query, new { poolId, miner, before }, tx, cancellationToken: ct));
    }

    public async Task DeleteSharesByMinerBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, DateTime before, CancellationToken ct)
    {
        // Bounded by 'created <= before' (the just-processed block's Created timestamp) so this
        // never touches shares that arrived after this block — those belong to a later,
        // not-yet-classified block by the same miner and are still needed for its own
        // Effort/MinerEffort calculation and for the live hashrate window in StatsRecorder.
        const string query = "DELETE FROM shares WHERE poolid = @poolId AND miner = @miner AND created <= @before";

        await con.ExecuteAsync(new CommandDefinition(query, new { poolId, miner, before }, tx, cancellationToken: ct));
    }

    public async Task DeleteSharesBeforeAsync(IDbConnection con, IDbTransaction tx, string poolId, DateTime before, CancellationToken ct)
    {
        const string query = "DELETE FROM shares WHERE poolid = @poolId AND created < @before";

        await con.ExecuteAsync(new CommandDefinition(query, new { poolId, before }, tx, cancellationToken: ct));
    }

    public Task<double?> GetAccumulatedShareDifficultyBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct)
    {
        const string query = "SELECT SUM(difficulty) FROM shares WHERE poolid = @poolId AND created > @start AND created < @end";

        return con.QuerySingleAsync<double?>(new CommandDefinition(query, new { poolId, start, end }, cancellationToken: ct));
    }

    public Task<double?> GetMinerShareDifficultyBetweenAsync(IDbConnection con, string poolId, string miner, DateTime start, DateTime end, CancellationToken ct)
    {
        const string query = "SELECT SUM(difficulty / NULLIF(networkdifficulty, 0)) FROM shares WHERE poolid = @poolId AND miner = @miner AND created > @start AND created < @end AND networkdifficulty > 0";

        return con.QuerySingleAsync<double?>(new CommandDefinition(query, new { poolId, miner, start, end }, cancellationToken: ct));
    }

    public Task<double?> GetEffectiveAccumulatedShareDifficultyBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct)
    {
        const string query = "SELECT SUM(difficulty / NULLIF(networkdifficulty, 0)) FROM shares WHERE poolid = @poolId AND created > @start AND created <= @end AND networkdifficulty > 0";

        return con.QuerySingleAsync<double?>(new CommandDefinition(query, new { poolId, start, end }, cancellationToken: ct));
    }

    public async Task<MinerWorkerHashes[]> GetHashAccumulationBetweenAsync(IDbConnection con, string poolId, DateTime start, DateTime end, CancellationToken ct)
    {
        const string query = @"SELECT SUM(difficulty), COUNT(difficulty), MIN(created) AS firstshare, MAX(created) AS lastshare, miner, worker FROM shares
            WHERE poolid = @poolId AND created >= @start AND created <= @end
            GROUP BY miner, worker";

        return (await con.QueryAsync<MinerWorkerHashes>(new CommandDefinition(query, new { poolId, start, end }, cancellationToken: ct)))
            .ToArray();
    }

    public async Task<KeyValuePair<string, double>[]> GetAccumulatedUserAgentShareDifficultyBetweenAsync(
        IDbConnection con, string poolId, DateTime start, DateTime end, bool byVersion, CancellationToken ct)
    {
        const string query = @"SELECT SUM(difficulty) AS value, REGEXP_REPLACE(useragent, '/.+', '') AS key FROM shares
                WHERE poolid = @poolId AND created > @start AND created < @end
                GROUP BY key ORDER BY value DESC";

        const string queryByVersion = @"SELECT SUM(difficulty) AS value, useragent AS key FROM shares
            WHERE poolid = @poolId AND created > @start AND created < @end
            GROUP BY key ORDER BY value DESC";

        return (await con.QueryAsync<KeyValuePair<string, double>>(new CommandDefinition(!byVersion ? query : queryByVersion, new { poolId, start, end }, cancellationToken: ct)))
            .ToArray();
    }

    public async Task<string[]> GetRecentyUsedIpAddressesAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, CancellationToken ct)
    {
        const string query = @"SELECT DISTINCT s.ipaddress FROM (SELECT * FROM shares
            WHERE poolid = @poolId and miner = @miner ORDER BY CREATED DESC LIMIT 100) s";

        return (await con.QueryAsync<string>(new CommandDefinition(query, new { poolId, miner }, tx, cancellationToken: ct)))
            .ToArray();
    }

    public async Task<string[]> GetRecentlyUsedPasswordsAsync(IDbConnection con, IDbTransaction tx, string poolId, string miner, CancellationToken ct)
    {
        const string query = @"SELECT DISTINCT s.mpassword FROM (SELECT * FROM shares
            WHERE poolid = @poolId AND miner = @miner AND mpassword IS NOT NULL ORDER BY created DESC LIMIT 100) s";

        return (await con.QueryAsync<string>(new CommandDefinition(query, new { poolId, miner }, tx, cancellationToken: ct)))
            .ToArray();
    }
}
