using ApiDefaults.Extensions;

using AuditService.Api.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.WebHost.ConfigureApiDefaults();

builder.Services.AddAuditApiComposition(builder.Configuration, builder.Environment);

var app = builder.Build();

app.UseForwardedHeaders();
app.UseApiSwagger(builder.Configuration);

app.UseApiDefaults();

app.MapApiHealthEndpoints(
    static (_, _, _) => Task.FromResult<IReadOnlyDictionary<string, string>>(new Dictionary<string, string>
    {
        ["self"] = "ok"
    }),
    state: string.Empty,
    "Valida o estado interno inicial do AuditService. O servico ainda nao possui dependencias externas configuradas.");

app.MapControllers().RequireRateLimiting("fixed");

app.Run();
