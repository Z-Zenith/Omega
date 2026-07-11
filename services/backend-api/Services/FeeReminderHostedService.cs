using BackendApi.Data;

namespace BackendApi.Services;

// AWA-05: polls daily — a fee due date only ever moves in day-sized steps, so there's no need
// for SDA-13's minute-level polling cadence here. All actual logic lives in FeeReminderScanner
// (unit-testable); this class only owns the scoped-DbContext + polling-loop plumbing a
// BackgroundService requires.
public class FeeReminderHostedService(IServiceScopeFactory scopeFactory, IConfiguration configuration) : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromHours(24);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var daysBeforeDue = configuration.GetValue("Notifications:FeeReminderDaysBeforeDue", 3);

        using var timer = new PeriodicTimer(PollInterval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var notifications = scope.ServiceProvider.GetRequiredService<INotificationRouter>();
            await FeeReminderScanner.ScanAsync(db, notifications, DateTime.UtcNow, daysBeforeDue, stoppingToken);
        }
    }
}
