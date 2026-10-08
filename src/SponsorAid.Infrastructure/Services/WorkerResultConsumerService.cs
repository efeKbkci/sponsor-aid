using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;
using Microsoft.Extensions.Logging;
using SponsorAid.Integration.Contracts; // Protobuf'ın oluşturduğu namespace
using Google.Protobuf; // ParseFrom için gerekli

namespace SponsorAid.Infrastructure.Services;

public class WorkerResultConsumerService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<WorkerResultConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    private const string QueueName = "document-result-queue";

    public WorkerResultConsumerService(IServiceProvider serviceProvider, ILogger<WorkerResultConsumerService> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    // ExecuteAsync başladığında asenkron olarak bağlantıları kuruyoruz
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 1. Şifreleri ve adresi .env üzerinden dinamik okuyoruz
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST") ?? "localhost";

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user, // 🚀 Anahtarlar (şifreler) buraya da eklendi!
            Password = pass
        };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        // v7'de asenkron tüketici sınıfının adı AsyncEventingBasicConsumer oldu
        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                // 1. Python'dan gelen saf byte'ları Protobuf Sözleşmesine çevir
                var resultMsg = WorkerResultMessage.Parser.ParseFrom(ea.Body.ToArray());

                // 2. Protobuf Enum'ını, C# komutunun beklediği String'e çevir (İleride Command'ini de Enum yapabilirsin)
                string docTypeString = resultMsg.DocumentType switch
                {
                    DocumentType.PopulationRegistry => "PopulationRegistry",
                    DocumentType.Residence => "Residence",
                    DocumentType.IdentityFront => "IdentityFront",
                    DocumentType.IdentityBack => "IdentityBack",
                    DocumentType.MergedIdentity => "MergedIdentity",
                    _ => "Unknown"
                };

                // 3. İç Dünyamızın Komutunu (Command) Yarat
                var command = new SaveProcessedDocumentCommand(
                    RegistrationKey: resultMsg.RegistrationKey,
                    DocumentType: docTypeString,
                    IsSuccess: resultMsg.IsSuccess,
                    // Has... özellikleri Protobuf'ın 'optional' alanları için otomatik ürettiği harika kontrolcülerdir
                    ErrorMessage: resultMsg.HasErrorMessage ? resultMsg.ErrorMessage : null,
                    SavedFilePath: resultMsg.HasSavedFilePath ? resultMsg.SavedFilePath : null,

                    // Hangi veri dolu geldiyse komuta onu yerleştir (Diğerleri null kalır)
                    PopulationData: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.PopulationData
                                        ? resultMsg.PopulationData : null,

                    ResidenceData: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ResidenceData
                                        ? resultMsg.ResidenceData : null,

                    ExtractedTcNumber: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ExtractedTcNumber
                                        ? resultMsg.ExtractedTcNumber : null,

                    ExtractedBarcode: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ExtractedBarcode
                                        ? resultMsg.ExtractedBarcode : null
                );

                // 4. MediatR'a fırlat
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(command, stoppingToken);

                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker sonucu işlenirken hata oluştu.");
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        // BasicConsumeAsync oldu
        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

        // Background service'i açık tutmak için sonsuz bir bekleme (TaskCompletionSource vb. ile yapılabilir ama stoppingToken en iyisidir)
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    public override async void Dispose()
    {
        if (_channel != null) await _channel.DisposeAsync();
        if (_connection != null) await _connection.DisposeAsync();
        base.Dispose();
    }
}