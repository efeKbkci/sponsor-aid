using System.Text;
using System.Text.Json;
using MediatR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SponsorAid.Application.Features.Registrations.Commands.SaveProcessedDocument;
using Microsoft.Extensions.Logging;

namespace SponsorAid.Infrastructure.MessageBrokers;

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
        var factory = new ConnectionFactory { HostName = "localhost" };

        _connection = await factory.CreateConnectionAsync(stoppingToken);
        _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

        await _channel.QueueDeclareAsync(queue: QueueName, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: stoppingToken);

        // v7'de asenkron tüketici sınıfının adı AsyncEventingBasicConsumer oldu
        var consumer = new AsyncEventingBasicConsumer(_channel);

        // Received yerine ReceivedAsync oldu
        consumer.ReceivedAsync += async (sender, ea) =>
        {
            var content = Encoding.UTF8.GetString(ea.Body.ToArray());

            try
            {
                var command = JsonSerializer.Deserialize<SaveProcessedDocumentCommand>(content);

                if (command != null)
                {
                    using var scope = _serviceProvider.CreateScope();
                    var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();

                    await mediator.Send(command, stoppingToken);
                }

                // BasicAckAsync oldu
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