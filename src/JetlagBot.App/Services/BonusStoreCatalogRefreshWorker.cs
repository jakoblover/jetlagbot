namespace JetlagBot.App.Services;

/// <summary>Keeps the Discord store autocomplete catalog warm in the background.</summary>
public sealed class BonusStoreCatalogRefreshWorker(
    IBonusStoreCatalogCache catalog,
    ILogger<BonusStoreCatalogRefreshWorker> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);
    private static readonly TimeSpan FetchTimeout = TimeSpan.FromSeconds(8);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial load shortly after startup (do not block host startup).
        await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await catalog
                    .RefreshIfNeededAsync(maxAge: TimeSpan.Zero, timeout: FetchTimeout, stoppingToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "Background bonus store catalog refresh failed.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
