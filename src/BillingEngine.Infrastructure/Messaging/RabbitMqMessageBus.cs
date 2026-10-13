using BillingEngine.Application.Interfaces;
using MassTransit;

namespace BillingEngine.Infrastructure.Messaging;

public class RabbitMqMessageBus : IMessageBus
{
    private readonly IPublishEndpoint _publisher;

    public RabbitMqMessageBus(IPublishEndpoint publisher) => _publisher = publisher;

    public Task PublishAsync<T>(T message, CancellationToken ct) where T : class =>
        _publisher.Publish(message, ct);
}
