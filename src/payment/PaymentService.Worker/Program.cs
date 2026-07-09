using PaymentService.Worker.Extensions;

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddPaymentWorkerComposition(builder.Configuration, builder.Environment);

await builder.Build().RunAsync();
