using DotNetEnv;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Behaviors;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration;
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Infrastructure.Persistence;
using SponsorAid.Infrastructure.Services;
using SponsorAid.Infrastructure.Services.Repositories;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLocalization();

// Section 1: Environment and configuration loading.
Env.TraversePath().Load();
builder.Configuration.AddEnvironmentVariables();

// Section 2: API and Swagger documentation endpoints.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SponsorAid API", Version = "v1" });
});

// Section 3: Application layer registration and mediator pipeline behaviors.
builder.Services.AddMediatR(cfg =>
{
    // Scans application assembly to register command handlers.
    cfg.RegisterServicesFromAssembly(typeof(StartRegistrationCommand).Assembly);

    // Global exception pipeline converts domain exceptions into localized user responses.
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));

    // Concurrency control behavior serializes concurrent messages per chat session.
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(RequestSerializationBehavior<,>));
});

// Section 4: Infrastructure services and dependency injection.
// Configures PostgreSQL connection string.
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Generates fallback connection string for local development when run outside container orchestration.
if (string.IsNullOrEmpty(connectionString))
{
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

    connectionString = $"Host=localhost;Port=5432;Database={dbName};Username={dbUser};Password={dbPass};IncludeErrorDetail=true;";
} 
else
{
    // In containerized deployments, connection string is supplied via configuration.
}

builder.Services.AddDbContext<SponsorAidDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IDraftStateRepository, EfCoreDraftStateRepository>();
builder.Services.AddScoped<IRegistrationTrackingRepository, EfCoreRegistrationTrackingRepository>();

// Registered as a singleton to share the underlying RabbitMQ TCP connection across requests.
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// Typed HTTP clients managed by HttpClientFactory to prevent socket exhaustion.
builder.Services.AddHttpClient<IFileStorageService, TelegramLocalFileStorageService>();
builder.Services.AddHttpClient<IUserNotifier, TelegramUserNotifier>();
builder.Services.AddHttpClient<ISemanticMatcher, HttpSemanticMatcher>();

builder.Services.AddScoped<IDialogRouter, TelegramDialogRouter>();
builder.Services.AddScoped<IEGovermentDocumentService, EGovermentDocumentService>();

// Configures named HTTP client for e-Government verification with custom headers and isolated cookies.
builder.Services.AddHttpClient("EDevletClient", client =>
{
    client.BaseAddress = new Uri("https://www.turkiye.gov.tr/");
    client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36");
    client.DefaultRequestHeaders.Add("Accept", "text/html,application/xhtml+xml,application/xml;q=0.9,image/avif,image/webp,*/*;q=0.8");
    client.DefaultRequestHeaders.Add("Accept-Language", "tr-TR,tr;q=0.9,en-US;q=0.8,en;q=0.7");

    // Standard browser request headers required to pass verification checks.
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua", "\"Chromium\";v=\"124\", \"Google Chrome\";v=\"124\"");
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Mobile", "?0");
    client.DefaultRequestHeaders.Add("Sec-Ch-Ua-Platform", "\"Windows\"");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Dest", "document");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Mode", "navigate");
    client.DefaultRequestHeaders.Add("Sec-Fetch-Site", "same-origin");
})
.ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler
{
    // Disables automatic cookie handling so that concurrent verification sessions remain isolated.
    UseCookies = false,
    // Limits pooled connection lifetime to allow DNS rotation while reusing connections.
    PooledConnectionLifetime = TimeSpan.FromMinutes(5)
});

builder.Services.AddScoped<ISecureArchiveService, SharpZipLibSecureArchiveService>();
builder.Services.AddScoped<IRegistrationDispatchService, RegistrationDispatchService>();

// Section 5: Long-running background consumers.
// Starts background service listening to worker processing results from RabbitMQ.
builder.Services.AddHostedService<WorkerResultConsumerService>();

// Section 6: HTTP request pipeline and application startup.
var app = builder.Build();

// Sets Turkish as default culture for localization resources.
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Automatically runs pending database migrations upon startup.
using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<SponsorAidDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation(System.Globalization.CultureInfo.CurrentUICulture.Name);
    try
    {
        logger.LogInformation("Veritabanı tabloları kontrol ediliyor/güncelleniyor...");
        dbContext.Database.Migrate();
        logger.LogInformation("Veritabanı tabloları güncel.");
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Veritabanı migrasyonu sırasında bir hata oluştu.");
    }
}

app.Run();

/// <summary>
/// Exposes application root class for integration test harnesses.
/// </summary>
public partial class Program { }
