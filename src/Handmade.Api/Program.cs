using Handmade.Api.Extensions;
using Handmade.Infrastructure.Persistence;
using Handmade.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Logging.AddHandmadeLogging(builder.Environment);

builder.Services.AddHandmadeApi(builder.Configuration, builder.Environment);

WebApplication app = builder.Build();

app.Logger.LogInformation(
    "Starting {Application} in {Environment} environment",
    "Handmade.Api",
    app.Environment.EnvironmentName);

if (app.Environment.IsDevelopment())
{
    using IServiceScope scope = app.Services.CreateScope();
    HandmadeDbContext db = scope.ServiceProvider.GetRequiredService<HandmadeDbContext>();
    await db.Database.MigrateAsync();
}

await IdentitySeed.SeedRolesAsync(app.Services);
await IdentitySeed.SeedAdminAsync(app.Services);
if (app.Environment.IsDevelopment())
{
    await CatalogSeed.SeedDevelopmentCategoriesAsync(app.Services);
}

app.UseHandmadePipeline();

app.Run();

/// <summary>
/// Exposes the entry assembly for WebApplicationFactory in integration tests.
/// </summary>
public partial class Program;
