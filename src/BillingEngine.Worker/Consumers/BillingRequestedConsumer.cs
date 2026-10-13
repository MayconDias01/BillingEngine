using BillingEngine.Application.DTOs;
using BillingEngine.Application.Interfaces;
using BillingEngine.Domain.Entities;
using BillingEngine.Domain.Events;
using BillingEngine.Domain.Interfaces;
using MassTransit;
using Microsoft.Extensions.Logging;

namespace BillingEngine.Worker.Consumers;

public class BillingRequestedConsumer : IConsumer<BillingRequested>
{
    private readonly IBillingRepository _billings;
    private readonly ISubscriptionRepository _subscriptions;
    private readonly IPlanRepository _plans;
    private readonly IIdempotencyService _idempotency;
    private readonly IPaymentGateway _gateway;
    private readonly ILogger<BillingRequestedConsumer> _logger;

    public BillingRequestedConsumer(
        IBillingRepository billings,
        ISubscriptionRepository subscriptions,
        IPlanRepository plans,
        IIdempotencyService idempotency,
        IPaymentGateway gateway,
        ILogger<BillingRequestedConsumer> logger)
    {
        _billings      = billings;
        _subscriptions = subscriptions;
        _plans         = plans;
        _idempotency   = idempotency;
        _gateway       = gateway;
        _logger        = logger;
    }

    public async Task Consume(ConsumeContext<BillingRequested> context)
    {
        var msg = context.Message;
        var ct  = context.CancellationToken;

        // Redis idempotency: guarantee at-most-once processing even under retries.
        var redisKey = $"billing-processed:{msg.IdempotencyKey}";
        var isNew = await _idempotency.SetIfNotExistsAsync(redisKey, TimeSpan.FromHours(24), ct);
        if (!isNew)
        {
            _logger.LogInformation(
                "Billing {Key} already processed — skipping", msg.IdempotencyKey);
            return;
        }

        var billing = await _billings.GetByIdempotencyKeyAsync(msg.IdempotencyKey, ct);
        if (billing is null)
        {
            _logger.LogWarning(
                "Billing record not found for idempotency key {Key}", msg.IdempotencyKey);
            return;
        }

        var subscription = await _subscriptions.GetByIdAsync(billing.SubscriptionId, ct);
        if (subscription is null)
        {
            _logger.LogWarning(
                "Subscription {Id} not found for billing {Key}",
                billing.SubscriptionId, msg.IdempotencyKey);
            return;
        }

        var plan = await _plans.GetByIdAsync(subscription.PlanId, ct);
        if (plan is null)
        {
            _logger.LogWarning(
                "Plan {Id} not found for subscription {SubId}",
                subscription.PlanId, subscription.Id);
            return;
        }

        var attempts = await _billings.GetAttemptsByBillingIdAsync(billing.Id, ct);
        var attemptNumber = attempts.Count() + 1;

        var attempt = BillingAttempt.Create(billing.Id, attemptNumber, billing.Gateway);
        await _billings.AddAttemptAsync(attempt, ct);

        var chargeRequest = new ChargeRequest(
            subscription.Id,
            billing.Amount,
            billing.Currency,
            msg.IdempotencyKey,
            subscription.GatewayCustomerId,
            subscription.GatewayPaymentMethodId);

        GatewayResult result;
        try
        {
            result = await _gateway.ChargeAsync(chargeRequest, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Gateway threw for billing {Key}", msg.IdempotencyKey);
            attempt.Fail("GATEWAY_EXCEPTION", ex.Message);
            await _billings.UpdateAttemptAsync(attempt, ct);
            billing.MarkAsFailed();
            await _billings.UpdateAsync(billing, ct);
            return;
        }

        if (result.Success)
        {
            attempt.Complete(result.TransactionId!);
            await _billings.UpdateAttemptAsync(attempt, ct);

            billing.MarkAsPaid();
            await _billings.UpdateAsync(billing, ct);

            // Advance next billing date by the plan's interval (cycle-aligned, no drift).
            subscription.UpdateNextBillingDate(
                subscription.NextBillingDate.AddDays(plan.BillingCycle.IntervalDays));
            await _subscriptions.UpdateAsync(subscription, ct);

            _logger.LogInformation(
                "Billing {Key} succeeded — txn {TxnId}", msg.IdempotencyKey, result.TransactionId);
        }
        else
        {
            attempt.Fail(result.FailureCode ?? "UNKNOWN", result.FailureReason ?? "Unknown failure");
            await _billings.UpdateAttemptAsync(attempt, ct);

            billing.MarkAsFailed();
            await _billings.UpdateAsync(billing, ct);

            _logger.LogWarning(
                "Billing {Key} failed — code {Code}: {Reason}",
                msg.IdempotencyKey, result.FailureCode, result.FailureReason);
        }
    }
}
