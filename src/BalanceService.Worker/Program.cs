using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Infrastructure.Messaging.Kafka;
using BalanceService.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

var kafkaEnabled = builder.Configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);

builder.Services.AddWorkerObservability(builder.Configuration);
builder.Services.AddApplication();
builder.Services
    .AddBalanceInfrastructureCommon()
    .AddBalancePersistence(builder.Configuration)
    .AddBalanceRepositories()
    .AddBalanceKafkaConsumer(builder.Configuration, builder.Environment);

if (kafkaEnabled)
{
    builder.Services.AddHostedService<LedgerEventsConsumer>();
}

await builder.Build().RunAsync();
