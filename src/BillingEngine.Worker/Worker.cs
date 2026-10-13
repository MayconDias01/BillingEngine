using BillingEngine.Application.Commands.Billing.TriggerBilling;
using BillingEngine.Domain.Interfaces;
using MediatR;

namespace BillingEngine.Worker;

public class Worker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<Worker> _logger;

    // How often to scan for subscriptions due for billing.
    private static readonly TimeSpan ScanInterval = TimeSpan.FromMinutes(1);

    public Worker(IServiceScopeFactory scopeFactory, ILogger<Worker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("BillingEngine Worker started — scan interval {Interval}", ScanInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await ScanAndTriggerAsync(stoppingToken);
            await Task.Delay(ScanInterval, stoppingToken);
        }
    }

    private async Task ScanAndTriggerAsync(CancellationToken ct)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var subscriptions = scope.ServiceProvider.GetRequiredService<ISubscriptionRepository>();
        var sender        = scope.ServiceProvider.GetRequiredService<ISender>();

        IEnumerable<Domain.Entities.Subscription> due;
        try
        {
            due = await subscriptions.GetDueForBillingAsync(DateTime.UtcNow, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to query subscriptions due for billing");
            return;
        }

        var list = due.ToList();
        if (list.Count == 0) return;

        _logger.LogInformation("Found {Count} subscription(s) due for billing", list.Count);

        foreach (var subscription in list)
        {
            try
            {
                // RequestingUserId = null: system-triggered, ownership check skipped.
                await sender.Send(new TriggerBillingCommand(subscription.Id), ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to trigger billing for subscription {SubscriptionId}", subscription.Id);
            }
        }
    }
}
