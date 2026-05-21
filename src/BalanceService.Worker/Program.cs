using BalanceService.Worker.Extensions;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddBalanceWorkerComposition(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
