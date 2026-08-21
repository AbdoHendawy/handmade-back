using Handmade.Api.Extensions;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddDebug();

builder.Services.AddHandmadeApi(builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

app.Logger.LogInformation(
    "Starting {Application} in {Environment} environment",
    "Handmade.Api",
    app.Environment.EnvironmentName);

app.UseHandmadePipeline();

app.Run();

/// <summary>
/// Exposes the entry assembly for WebApplicationFactory in integration tests.
/// </summary>
public partial class Program;
