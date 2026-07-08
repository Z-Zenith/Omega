using BackendApi.Data;

namespace BackendApi.Services;

// SDA-13: polls every minute so a session that just crossed the 20-minute mark is caught
// promptly. All actual logic lives in NoLoginAlertScanner (unit-testable); this class only
// owns the scoped-DbContext + polling-loop plumbing a BackgroundService requires.
public class NoLoginAlertHostedService(IServiceScopeFactory scopeFactory) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromMinutes(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
            await NoLoginAlertScanner.ScanAsync(db, notifications, DateTime.UtcNow, stoppingToken);
        }
    }
}
