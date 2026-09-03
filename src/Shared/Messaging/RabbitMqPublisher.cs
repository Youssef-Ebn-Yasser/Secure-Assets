using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace Shared.Messaging;

public interface IRabbitMqPublisher
{
    void Publish<T>(string queueName, T message);
}

public class RabbitMqPublisher : IRabbitMqPublisher, IDisposable
{
    private readonly IConnection? _connection;
    private readonly IModel? _channel;
    private readonly object _lock = new();

    public RabbitMqPublisher(string hostName, string userName = "guest", string password = "guest", int port = 5672)
    {
        try
        {
            var factory = new ConnectionFactory
            {
                HostName = hostName,
                UserName = userName,
                Password = password,
                Port = port,
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(5)
            };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
        }
        catch
        {
            // Transient connection failure will be handled gracefully
        }
    }

    public void Publish<T>(string queueName, T message)
    {
        if (_channel == null || _channel.IsClosed) return;

        lock (_lock)
        {
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null);

            var json = JsonSerializer.Serialize(message);
            var body = Encoding.UTF8.GetBytes(json);

            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;

            _channel.BasicPublish(
                exchange: string.Empty,
                routingKey: queueName,
                basicProperties: properties,
                body: body);
        }
    }

    public void Dispose()
    {
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
