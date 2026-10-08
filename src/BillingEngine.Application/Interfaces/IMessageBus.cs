namespace BillingEngine.Application.Interfaces;

public interface IMessageBus
{
    Task PublishAsync<T>(T message, CancellationToken ct) where T : class;
}
