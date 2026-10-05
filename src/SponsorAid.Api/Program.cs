using DotNetEnv;
using MediatR;
using SponsorAid.Application.Behaviors;
using SponsorAid.Application.Features.Registrations.Commands.StartRegistration; // Assembly'i bulmak için örnek bir Command
using SponsorAid.Application.Interfaces;
using SponsorAid.Application.Interfaces.Repositories;
using SponsorAid.Infrastructure.MessageBrokers;
using SponsorAid.Infrastructure.Services.Repositories;
using SponsorAid.Infrastructure.Services;

var builder = WebApplication.CreateBuilder(args);

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
// In-Memory Veritabanı (Sunucu kapanana kadar verilerin yaşamasını sağlar)
builder.Services.AddSingleton<IDraftStateRepository, InMemoryDraftStateRepository>();

// RabbitMQ Mesaj Gönderici (Tekil bağlantı performansı için Singleton)
builder.Services.AddSingleton<IEventPublisher, RabbitMqEventPublisher>();

// Telegram Servisleri (Modern, tıkanıklık yapmayan HttpClient Factory yapısı)
builder.Services.AddHttpClient<IFileStorageService, TelegramLocalFileStorageService>();
builder.Services.AddHttpClient<IUserNotifier, TelegramUserNotifier>();

// ==============================================================================
// 5. ARKA PLAN SERVİSLERİ (Background Workers)
// ==============================================================================
// Uygulama çalıştığı an arkada RabbitMQ'yu dinlemeye başlayan tüketici servis
builder.Services.AddHostedService<WorkerResultConsumerService>();

// ==============================================================================
// 6. UYGULAMAYI İNŞA ET VE AYAĞA KALDIR
// ==============================================================================
var app = builder.Build();

// Geliştirme ortamındaysak Swagger (Görsel API Test Ekranı) arayüzünü aç
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

// Ve Motor Çalışır!
app.Run();