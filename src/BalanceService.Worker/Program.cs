using BalanceService.Application;
using BalanceService.Infrastructure;
using BalanceService.Infrastructure.Messaging.Kafka;

var builder = Host.CreateApplicationBuilder(args);

var kafkaEnabled = builder.Configuration.GetValue<bool>("Kafka:Enabled", defaultValue: true);

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
