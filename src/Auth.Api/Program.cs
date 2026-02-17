using Auth.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureAuthApiKestrel();
builder.Services.AddAuthApiServices(builder.Configuration);

var app = builder.Build();

app.UseAuthApiPipeline();
app.UseAuthApiSwagger(builder.Configuration);

app.MapAuthApiEndpoints();

app.Run();

// Necessário para WebApplicationFactory em testes de integração
public partial class Program
{
}
