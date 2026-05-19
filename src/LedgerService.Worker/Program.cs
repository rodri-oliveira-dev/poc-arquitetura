using LedgerService.Application;
using LedgerService.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddApplication();
builder.Services
    .AddLedgerInfrastructureCommon()
    .AddLedgerPersistence(builder.Configuration)
    .AddLedgerRepositories()
    .AddLedgerKafkaProducer(builder.Configuration, builder.Environment)
    .AddLedgerOutboxWorker(builder.Configuration)
    .AddLedgerEstornoWorker(builder.Configuration)
    .AddLedgerReprocessamentoWorker(builder.Configuration, builder.Environment);

// TODO: adicionar OpenTelemetry de Generic Host quando houver extensao compartilhada
// sem dependencias de ASP.NET Core. A configuracao ja reserva ServiceName do worker.

await builder.Build().RunAsync();
