using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Infrastructure.DependencyInjection;
using BillingEngine.Worker;
using BillingEngine.Worker.Consumers;
using MediatR;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = Host.CreateApplicationBuilder(args);

// Infrastructure: persistence, security, MassTransit+RabbitMQ (with consumer), Redis.
builder.Services.AddInfrastructure(
    builder.Configuration,
    x => x.AddConsumer<BillingRequestedConsumer>());

// MediatR for sending commands from the Worker scanner loop.
// ValidationBehavior is intentionally omitted — Worker commands are system-generated,
// not from user input, so runtime validation is not needed.
builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

// Observabilidade (OpenTelemetry)
builder.Services.AddOpenTelemetry()
    .ConfigureResource(r => r.AddService(
        serviceName: builder.Configuration["Otel:ServiceName"] ?? "billing-engine-worker",
        serviceVersion: "1.0.0"))
    .WithTracing(tracing => tracing
        // .NET 9 built-in activity source for outbound HTTP calls.
        .AddSource("System.Net.Http")
        .AddOtlpExporter(o => o.Endpoint =
            new Uri(builder.Configuration["Otel:Endpoint"] ?? "http://localhost:4317")));

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
