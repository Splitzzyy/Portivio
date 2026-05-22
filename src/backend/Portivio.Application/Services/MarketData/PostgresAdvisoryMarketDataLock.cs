using System.Data;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Portivio.Infrastructure.Data;

namespace Portivio.Application.Services.MarketData
{
    public interface IMarketDataDistributedLock
    {
        Task<T> RunAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default);
    }

    public sealed class PostgresAdvisoryMarketDataLock : IMarketDataDistributedLock
    {
        private readonly PortivioDbContext _context;
        private readonly IOptions<MarketDataOptions> _options;
        private readonly ILogger<PostgresAdvisoryMarketDataLock> _logger;

        public PostgresAdvisoryMarketDataLock(
            PortivioDbContext context,
            IOptions<MarketDataOptions> options,
            ILogger<PostgresAdvisoryMarketDataLock> logger)
        {
            _context = context;
            _options = options;
            _logger = logger;
        }

        public async Task<T> RunAsync<T>(string key, Func<CancellationToken, Task<T>> action, CancellationToken ct = default)
        {
            if (!UsesNpgsql())
                return await action(ct);

            var lockId = CreateLockId(key);
            var openedHere = _context.Database.GetDbConnection().State != ConnectionState.Open;

            if (openedHere)
                await _context.Database.OpenConnectionAsync(ct);

            try
            {
                await AcquireAsync(lockId, key, ct);
                try
                {
                    return await action(ct);
                }
                finally
                {
                    await ReleaseAsync(lockId);
                }
            }
            finally
            {
                if (openedHere)
                    await _context.Database.CloseConnectionAsync();
            }
        }

        internal static long CreateLockId(string key)
        {
            var normalized = string.IsNullOrWhiteSpace(key) ? "default" : key.Trim().ToUpperInvariant();
            var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalized));
            return BitConverter.ToInt64(bytes, 0);
        }

        private bool UsesNpgsql()
            => string.Equals(
                _context.Database.ProviderName,
                "Npgsql.EntityFrameworkCore.PostgreSQL",
                StringComparison.Ordinal);

        private async Task AcquireAsync(long lockId, string key, CancellationToken ct)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeout.CancelAfter(TimeSpan.FromSeconds(Math.Max(1, _options.Value.Refresh.AdvisoryLockWaitSeconds)));

            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select pg_advisory_lock(@lock_id)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "lock_id";
            parameter.Value = lockId;
            command.Parameters.Add(parameter);

            try
            {
                await command.ExecuteScalarAsync(timeout.Token);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning("Timed out waiting for PostgreSQL advisory market-data lock. Key={Key}", key);
                throw new TimeoutException($"Timed out waiting for market-data advisory lock '{key}'.");
            }
        }

        private async Task ReleaseAsync(long lockId)
        {
            await using var command = _context.Database.GetDbConnection().CreateCommand();
            command.CommandText = "select pg_advisory_unlock(@lock_id)";

            var parameter = command.CreateParameter();
            parameter.ParameterName = "lock_id";
            parameter.Value = lockId;
            command.Parameters.Add(parameter);

            await command.ExecuteScalarAsync();
        }
    }
}
