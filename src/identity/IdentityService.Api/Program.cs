using IdentityService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddIdentityInfrastructure(builder.Configuration);

var app = builder.Build();

app.Run();
