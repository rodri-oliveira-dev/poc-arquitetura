using TransferService.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddTransferApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.Run();

public partial class Program;
