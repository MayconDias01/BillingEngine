using BillingEngine.Application.Interfaces;

namespace BillingEngine.Infrastructure.Messaging;

public class RabbitMqMessageBus : IMessageBus
{
    public Task PublishAsync<T>(T message, CancellationToken ct) where T : class
        => throw new NotImplementedException();
}
