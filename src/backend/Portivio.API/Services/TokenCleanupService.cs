using Portivio.Application.Services;

namespace Portivio.API.Services;

public sealed class TokenCleanupService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromHours(24);
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TokenCleanupService> _logger;

    public TokenCleanupService(IServiceScopeFactory scopeFactory, ILogger<TokenCleanupService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await RunCleanupAsync(stoppingToken);
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunCleanupAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var scope = _scopeFactory.CreateAsyncScope();
            var authService = scope.ServiceProvider.GetRequiredService<IAuthService>();
            var result = await authService.CleanupExpiredTokensAsync();
            if (result.IsSuccess)
                _logger.LogInformation("Token cleanup completed: {Message}", result.Message);
            else
                _logger.LogWarning("Token cleanup failed: {Message}", result.Message);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Token cleanup error");
        }
    }
}
