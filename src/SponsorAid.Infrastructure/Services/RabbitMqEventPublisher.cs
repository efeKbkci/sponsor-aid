using Google.Protobuf;
using RabbitMQ.Client;
using SponsorAid.Application.Interfaces;
using System.Text;
using System.Text.Json;

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
        await using var channel = await connection.CreateChannelAsync();

        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        // 🚀 YENİ MANTIK BURADA
        byte[] body;
        if (@event is IMessage protoMessage) // Gelen nesne Protobuf nesnesi mi?
        {
            body = protoMessage.ToByteArray(); // Evetse inanılmaz hızlı bir şekilde Binary'e çevir
        }
        else
        {
            var message = JsonSerializer.Serialize(@event); // Hayırsa eski usül JSON'a çevir
            body = Encoding.UTF8.GetBytes(message);
        }

        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, mandatory: false, basicProperties: properties, body: body);
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