using LedgerService.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddLedgerWorkerComposition(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
