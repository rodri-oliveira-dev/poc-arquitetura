using AuditService.Worker;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddAuditWorkerComposition(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
