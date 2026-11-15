using Google.Protobuf;
using RabbitMQ.Client;
using SponsorAid.Application.Interfaces;
using System.Text;
using System.Text.Json;

namespace SponsorAid.Infrastructure.Services;

/// <summary>
/// Publishes application events and processing commands to RabbitMQ message queues.
/// Implements asynchronous disposal compatible with RabbitMQ Client v7.
/// </summary>
public class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly ConnectionFactory _factory;
    private IConnection? _connection;

    public RabbitMqEventPublisher()
    {
        // Reads broker credentials from environment variables, defaulting to guest access for development.
        var user = Environment.GetEnvironmentVariable("RABBITMQ_USER") ?? "guest";
        var pass = Environment.GetEnvironmentVariable("RABBITMQ_PASSWORD") ?? "guest";

        // Resolves host name dynamically to accommodate both container networks and local development.
        var host = Environment.GetEnvironmentVariable("RABBITMQ_HOST")
            ?? Environment.GetEnvironmentVariable("RabbitMQ__HostName")
            ?? "localhost";

        _factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass
        };
    }

    // Opens broker connection on first use with retry logic to tolerate temporary startup delays.
    private async Task<IConnection> GetConnectionAsync()
    {
        if (_connection != null && _connection.IsOpen)
        {
            return _connection;
        }

        int attempts = 0;
        while (attempts < 5)
        {
            try
            {
                attempts++;
                _connection = await _factory.CreateConnectionAsync();
                return _connection;
            }
            catch when (attempts < 5)
            {
                await Task.Delay(2000);
            }
        }

        return await _factory.CreateConnectionAsync();
    }

    public async Task PublishAsync<TEvent>(string queueName, TEvent @event) where TEvent : class
    {
        var connection = await GetConnectionAsync();
        await using var channel = await connection.CreateChannelAsync();

        // Declares durable queue to prevent message loss during broker restarts.
        await channel.QueueDeclareAsync(queue: queueName, durable: true, exclusive: false, autoDelete: false, arguments: null);

        byte[] body;
        if (@event is IMessage protoMessage)
        {
            // Serializes Protocol Buffers contracts directly to binary bytes for low latency.
            body = protoMessage.ToByteArray();
        }
        else
        {
            // Fallback serialization to UTF-8 JSON for standard object payloads.
            var message = JsonSerializer.Serialize(@event);
            body = Encoding.UTF8.GetBytes(message);
        }

        // Marks message as persistent so the broker writes it to disk.
        var properties = new BasicProperties { Persistent = true };

        await channel.BasicPublishAsync(exchange: string.Empty, routingKey: queueName, mandatory: false, basicProperties: properties, body: body);
    }

    public async ValueTask DisposeAsync()
    {
        // Gracefully closes active connection on application shutdown.
        if (_connection != null)
        {
            await _connection.CloseAsync();
            await _connection.DisposeAsync();
        }
    }
}
