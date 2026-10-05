using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using SponsorAid.Application.Interfaces;

namespace SponsorAid.Infrastructure.Services;

// v7 ile birlikte IDisposable yerine IAsyncDisposable kullanıyoruz
public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;

    public RabbitMqEventPublisher()
    {
        // v7'de DispatchConsumersAsync özelliğine gerek kalmadı, sistem varsayılan olarak asenkrondur.
        _factory = new ConnectionFactory
        {
            HostName = "localhost"
        };
    }

    // Bağlantıyı (Connection) asenkron olarak ilk ihtiyaç anında güvenlice açıyoruz
    private async Task<IConnection> GetConnectionAsync()
    {
        if (_connection == null || !_connection.IsOpen)
        {
            _connection = await _factory.CreateConnectionAsync();
        }
        return _connection;
    }

    public async Task PublishAsync<TEvent>(string queueName, TEvent @event) where TEvent : class
    {
        var connection = await GetConnectionAsync();

        // 1. v7'de CreateModel yerine CreateChannelAsync kullanılır. await using ile işi bitince kapanmasını sağlıyoruz.
        await using var channel = await connection.CreateChannelAsync();

        // 2. Kuyruk tanımlama işlemi de artık asenkron (QueueDeclareAsync)
        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null);

        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        // 3. v7'de özellikler (Properties) için metot yerine doğrudan sınıf (BasicProperties) kullanıyoruz
        var properties = new BasicProperties
        {
            Persistent = true // Kalıcılık (Elektrik kesilirse mesaj kaybolmaz)
        };

        // 4. Gönderim işlemi de artık asenkron (BasicPublishAsync)
        await channel.BasicPublishAsync(
            exchange: string.Empty,
            routingKey: queueName,
            mandatory: false,
            basicProperties: properties,
            body: body);
    }

    public async ValueTask DisposeAsync()
    {
        // Uygulama kapandığında bağlantıyı asenkron olarak temizle
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}