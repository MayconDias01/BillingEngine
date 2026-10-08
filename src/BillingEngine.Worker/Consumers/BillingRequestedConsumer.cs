using BillingEngine.Domain.Events;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BillingEngine.Worker.Consumers;

public class BillingRequestedConsumer : IConsumer<BillingRequested>
{
    private readonly ILogger<BillingRequestedConsumer> _logger;

    public BillingRequestedConsumer(ILogger<BillingRequestedConsumer> logger)
    {
        _logger = logger;
    }

    public Task Consume(ConsumeContext<BillingRequested> context)
    {
        _logger.LogInformation(
            "Received BillingRequested for SubscriptionId: {SubscriptionId}, IdempotencyKey: {IdempotencyKey}",
            context.Message.SubscriptionId,
            context.Message.IdempotencyKey);

        throw new NotImplementedException();
    }
}
