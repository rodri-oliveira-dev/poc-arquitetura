using TransferService.Worker.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddTransferWorkerComposition(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
