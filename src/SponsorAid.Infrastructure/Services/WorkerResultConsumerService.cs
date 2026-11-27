using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;
using Microsoft.Extensions.Logging;
using SponsorAid.Integration.Contracts;
using Google.Protobuf;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Long-running background service that consumes OCR and document verification results from RabbitMQ.
/// </summary>
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
            ?? Environment.GetEnvironmentVariable("RabbitMQ__HostName")
            ?? "localhost";

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass
        };

        // Retries broker connection to survive container startup delays where RabbitMQ starts slower than the API.
        const int maxRetries = 10;
        int currentRetry = 0;

        while (!stoppingToken.IsCancellationRequested && _connection == null)
        {
            try
            {
                currentRetry++;
                _logger.LogInformation("RabbitMQ bağlantısı kuruluyor ({Host}). Deneme: {Attempt}/{Max}", host, currentRetry, maxRetries);
                _connection = await factory.CreateConnectionAsync(stoppingToken);
                _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
                _logger.LogInformation("RabbitMQ bağlantısı başarıyla kuruldu.");
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                if (currentRetry >= maxRetries)
                {
                    _logger.LogCritical(ex, "RabbitMQ servisine ulaşılamadı. Maksimum deneme sayısına ulaşıldı.");
                    throw;
                }

                _logger.LogWarning("RabbitMQ henüz hazır değil ({Message}). 3 saniye sonra tekrar denenecek...", ex.Message);
                await Task.Delay(3000, stoppingToken);
            }
        }

        if (stoppingToken.IsCancellationRequested || _channel == null) return;

        // Declares durable queue to ensure message preservation during broker restarts.
        await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);

        consumer.ReceivedAsync += async (sender, ea) =>
        {
            try
            {
                // Parses incoming binary payload into the Protobuf message contract.
                var resultMsg = WorkerResultMessage.Parser.ParseFrom(ea.Body.ToArray());

                // Converts contract document enum to application command string value.
                string docTypeString = resultMsg.DocumentType switch
                {
                    DocumentType.PopulationRegistry => "PopulationRegistry",
                    DocumentType.Residence => "Residence",
                    DocumentType.IdentityFront => "IdentityFront",
                    DocumentType.IdentityBack => "IdentityBack",
                    DocumentType.MergedIdentity => "MergedIdentity",
                    DocumentType.BankAccount => "BankAccount",
                    _ => "Unknown"
                };

                // Constructs application command populated according to the protobuf oneof payload discriminator.
                var command = new SaveProcessedDocumentCommand(
                    RegistrationKey: resultMsg.RegistrationKey,
                    DocumentType: docTypeString,
                    IsSuccess: resultMsg.IsSuccess,
                    ErrorMessage: resultMsg.HasErrorMessage ? resultMsg.ErrorMessage : null,
                    SavedFilePath: resultMsg.HasSavedFilePath ? resultMsg.SavedFilePath : null,

                    PopulationData: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.PopulationData
                                         ? resultMsg.PopulationData : null,

                    ResidenceData: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ResidenceData
                                         ? resultMsg.ResidenceData : null,

                    ExtractedTcNumber: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ExtractedTcNumber
                                         ? resultMsg.ExtractedTcNumber : null,

                    ExtractedBarcode: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ExtractedBarcode
                                         ? resultMsg.ExtractedBarcode : null,

                    ExtractedIban: resultMsg.ExtractedDataCase == WorkerResultMessage.ExtractedDataOneofCase.ExtractedIban
                                         ? resultMsg.ExtractedIban : null
                );

                // Creates an isolated dependency injection scope to resolve and execute the handler.
                using var scope = _serviceProvider.CreateScope();
                var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
                await mediator.Send(command, stoppingToken);

                // Manually acknowledges successful processing.
                await _channel.BasicAckAsync(ea.DeliveryTag, false, cancellationToken: stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Worker sonucu işlenirken hata oluştu.");
                // Rejects failed message without requeueing to prevent poison message loops.
                await _channel.BasicNackAsync(ea.DeliveryTag, false, requeue: false, cancellationToken: stoppingToken);
            }
        };

        // Disables auto-ack so delivery guarantees remain under explicit application control.
        await _channel.BasicConsumeAsync(queue: QueueName, autoAck: false, consumer: consumer, cancellationToken: stoppingToken);

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
