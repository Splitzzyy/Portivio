namespace Portivio.Application.Services
{
    public interface IRefreshThrottle
    {
        Task DelayAsync(TimeSpan delay, CancellationToken ct);
    }

    public class RealRefreshThrottle : IRefreshThrottle
    {
        public Task DelayAsync(TimeSpan delay, CancellationToken ct) =>
            delay > TimeSpan.Zero ? Task.Delay(delay, ct) : Task.CompletedTask;
    }
}
