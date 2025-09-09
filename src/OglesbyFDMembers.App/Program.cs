using Mediator;
using Microsoft.EntityFrameworkCore;
using OglesbyFDMembers.App.Views;
using OglesbyFDMembers.Data;
using Radzen;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRadzenComponents();

// Register EF Core Sqlite DbContext
builder.Services.AddSqliteDb(builder.Configuration, builder.Environment);

// Setup the Mediator with source generator. Default assembly scanning is sufficient
builder.Services.AddMediator();

// App services
builder.Services.AddScoped<OglesbyFDMembers.App.Services.IntakeService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.FeeScheduleService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.PeopleService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.PaymentsService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.RolloverService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.IPdfVvecExtractor, OglesbyFDMembers.App.Services.PdfVvecExtractor>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.IVvecImportService, OglesbyFDMembers.App.Services.VvecImportService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.VvecNoticeService>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.BackupSettingsStore>();
builder.Services.AddScoped<OglesbyFDMembers.App.Services.BackupService>();

// Background jobs
builder.Services.AddHostedService<OglesbyFDMembers.App.Background.DailyAssessmentJob>();
builder.Services.AddHostedService<OglesbyFDMembers.App.Background.DailyBackupJob>();
builder.Services.AddHostedService<OglesbyFDMembers.App.Background.WeeklyBackupJob>();

var app = builder.Build();

// Attempt to migrate database on startup with friendly fallback
var dbReady = true;
try
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
catch (Exception ex)
{
    dbReady = false;
    var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Startup");
    logger.LogError(ex, "Database migration failed on startup");
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

// If DB failed to initialize, short-circuit most requests with a friendly message
if (!dbReady)
{
    app.Use(async (ctx, next) =>
    {
        // Allow health checks to pass through
        if (ctx.Request.Path.StartsWithSegments("/health"))
        {
            await next();
            return;
        }
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "text/html; charset=utf-8";
        await ctx.Response.WriteAsync("<html><body style='font-family:system-ui;padding:2rem'>" +
                                      "<h2>We’re having trouble starting up</h2>" +
                                      "<p>The database could not be initialized. Please try again later.</p>" +
                                      "</body></html>");
    });
}


app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Simple DB health endpoint
app.MapGet("/health/db", async (IServiceProvider sp) =>
{
    using var scope = sp.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Health");

    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var canConnect = await db.Database.CanConnectAsync();
        var pending = (await db.Database.GetPendingMigrationsAsync()).Count();
        var status = canConnect ? (pending == 0 ? "Healthy" : "Degraded") : "Unhealthy";

        return Results.Json(new
        {
            status,
            canConnect,
            pendingMigrations = pending,
            provider = db.Database.ProviderName
        });
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "DB health check failed");
        return Results.Problem("Database health check failed", statusCode: 503);
    }
});

app.Run();
