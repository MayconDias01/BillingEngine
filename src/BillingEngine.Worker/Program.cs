using BillingEngine.Application.Commands.Auth.Register;
using BillingEngine.Infrastructure.DependencyInjection;
using BillingEngine.Worker;
using BillingEngine.Worker.Consumers;
using MassTransit;
using MediatR;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

builder.Services.AddMediatR(cfg =>
    cfg.RegisterServicesFromAssembly(typeof(RegisterCommand).Assembly));

builder.Services.AddMassTransit(x =>
{
    x.AddConsumer<BillingRequestedConsumer>();
    x.UsingRabbitMq((context, cfg) =>
    {
        cfg.Host(builder.Configuration["RabbitMQ:Host"], h =>
        {
            h.Username(builder.Configuration["RabbitMQ:Username"] ?? "guest");
            h.Password(builder.Configuration["RabbitMQ:Password"] ?? "guest");
        });
        cfg.ConfigureEndpoints(context);
    });
});

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
