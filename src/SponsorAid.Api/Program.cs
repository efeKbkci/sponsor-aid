using DotNetEnv;
using MediatR;
using Microsoft.EntityFrameworkCore;
using SponsorAid.Application.Behaviors;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration; // Assembly'i bulmak için örnek bir Command
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Infrastructure.Persistence;
using SponsorAid.Infrastructure.Services;
using SponsorAid.Infrastructure.Services.Repositories;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddLocalization();

// ==============================================================================
// 1. GÜVENLİK VE AYARLAR (.env)
// ==============================================================================
Env.TraversePath().Load();
builder.Configuration.AddEnvironmentVariables();

// ==============================================================================
// 2. API VE DOKÜMANTASYON KAPILARI
// ==============================================================================
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "SponsorAid API", Version = "v1" });
});

// ==============================================================================
// 3. APPLICATION KATMANI (MediatR & Behaviors)
// ==============================================================================
builder.Services.AddMediatR(cfg =>
{
    // Application katmanındaki tüm Command ve Handler'ları otomatik bulup kaydeder.
    cfg.RegisterServicesFromAssembly(typeof(StartRegistrationCommand).Assembly);

    // Merkezi Hata Yakalayıcı (Pipeline) mekanizmamızı sisteme tanıtıyoruz.
    cfg.AddBehavior(typeof(IPipelineBehavior<,>), typeof(ExceptionHandlingBehavior<,>));
});

// ==============================================================================
// 4. INFRASTRUCTURE KATMANI (Bağımlılık Enjeksiyonları - DI)
// ==============================================================================
// 🚀 Veritabanı Bağlantısı (EF Core - PostgreSQL)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");

// Eğer "dotnet run" ile çalıştırıyorsak connectionString boş gelir. 
// Bu durumda .env dosyasındaki ortam değişkenlerini okuyarak "localhost" için bağlantı dizesini dinamik oluştururuz.
if (string.IsNullOrEmpty(connectionString))
{
    var dbName = Environment.GetEnvironmentVariable("DB_NAME");
    var dbUser = Environment.GetEnvironmentVariable("DB_USER");
    var dbPass = Environment.GetEnvironmentVariable("DB_PASSWORD");

    connectionString = $"Host=localhost;Port=5432;Database={dbName};Username={dbUser};Password={dbPass};IncludeErrorDetail=true;";
} 
else
{ // proje docker içerisinde çalışıyorsa, 
  // connectionString zaten docker-compose.yml içindeki ortam değişkenlerinden gelmiş olur.
}

builder.Services.AddDbContext<SponsorAidDbContext>(options =>
    options.UseNpgsql(connectionString));

builder.Services.AddScoped<IDraftStateRepository, EfCoreDraftStateRepository>();
builder.Services.AddScoped<IRegistrationTrackingRepository, EfCoreRegistrationTrackingRepository>();

// RabbitMQ Mesaj Gönderici (Tekil bağlantı performansı için Singleton)
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// Telegram Servisleri (Modern, tıkanıklık yapmayan HttpClient Factory yapısı)
builder.Services.AddHttpClient<IFileStorageService, TelegramLocalFileStorageService>();
builder.Services.AddHttpClient<IUserNotifier, TelegramUserNotifier>();
builder.Services.AddHttpClient<ISemanticMatcher, HttpSemanticMatcher>();

// 🚀 YÖNLENDİRİCİ (ROUTER) SERVİSİ KAYDI (BUNU EKLE)
builder.Services.AddScoped<IDialogRouter, TelegramDialogRouter>();
// Program.cs'deki DI (Dependency Injection) kısmına eklenecek:
builder.Services.AddScoped<IEGovermentDocumentService, EGovermentDocumentService>();

// 🚀 ŞİFRELİ ARŞİV SERVİSİ KAYDI
builder.Services.AddScoped<ISecureArchiveService, SharpZipLibSecureArchiveService>();
builder.Services.AddScoped<IRegistrationDispatchService, RegistrationDispatchService>();

// ==============================================================================
// 5. ARKA PLAN SERVİSLERİ (Background Workers)
// ==============================================================================
// Uygulama çalıştığı an arkada RabbitMQ'yu dinlemeye başlayan tüketici servis
builder.Services.AddHostedService<WorkerResultConsumerService>();

// ==============================================================================
// 6. UYGULAMAYI İNŞA ET VE AYAĞA KALDIR
// ==============================================================================
var app = builder.Build();

// Türkçe kültür ayarını zorunlu kılın (Middleware sırasının başlarında olmalı)
var supportedCultures = new[] { "tr-TR" };
var localizationOptions = new RequestLocalizationOptions()
    .SetDefaultCulture("tr-TR")
    .AddSupportedCultures(supportedCultures)
    .AddSupportedUICultures(supportedCultures);

app.UseRequestLocalization(localizationOptions);

// Geliştirme ortamındaysak Swagger (Görsel API Test Ekranı) arayüzünü aç
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// 🚀 Docker ilk açıldığında tabloları veritabanında otomatik oluşturur
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

// Ve Motor Çalışır!
app.Run();

// Test için Program sınıfını public yapıyoruz ki WebApplicationFactory<Program> erişebilsin.
public partial class Program { }