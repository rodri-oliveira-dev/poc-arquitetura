using LedgerService.Application;
using LedgerService.Infrastructure;
using LedgerService.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWorkerObservability(builder.Configuration);
builder.Services.AddApplication();
builder.Services
    .AddLedgerInfrastructureCommon()
    .AddLedgerPersistence(builder.Configuration)
    .AddLedgerRepositories()
    .AddLedgerKafkaProducer(builder.Configuration, builder.Environment)
    .AddLedgerOutboxWorker(builder.Configuration)
    .AddLedgerEstornoWorker(builder.Configuration)
    .AddLedgerReprocessamentoWorker(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
