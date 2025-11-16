using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrquestradorCentral.Application.Interfaces;
using RabbitMQ.Client;
using System.Text;
using System.Text.Json;

namespace OrquestradorCentral.Infrastructure.Messaging;

public class RabbitMQPublisher : IMessagePublisher, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly ILogger<RabbitMQPublisher> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private readonly object _lock = new object();
    private bool _disposed = false;

    public RabbitMQPublisher(
        IConfiguration configuration,
        ILogger<RabbitMQPublisher> logger)
    {
        _configuration = configuration;
        _logger = logger;
    }

    private void EnsureConnection()
    {
        if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
            return;

        lock (_lock)
        {
            if (_connection != null && _connection.IsOpen && _channel != null && _channel.IsOpen)
                return;

            _logger.LogInformation("📡 Estabelecendo conexão com RabbitMQ para publicação...");

            var factory = new ConnectionFactory()
            {
                HostName = _configuration["RabbitMQ:Host"] ?? "localhost",
                Port = _configuration.GetValue<int>("RabbitMQ:Port", 5672),
                UserName = _configuration["RabbitMQ:UserName"] ?? "guest",
                Password = _configuration["RabbitMQ:Password"] ?? "guest",
                VirtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/",
                AutomaticRecoveryEnabled = true,
                NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? "graficaltda.exchange";

            // Não declaramos o exchange aqui - ele é criado pela API Central
            // Apenas usamos o exchange existente

            _logger.LogInformation("✅ Conexão estabelecida com RabbitMQ - Exchange: {Exchange}", exchangeName);
        }
    }

    public Task PublishAsync<T>(T message, string queueName) where T : class
    {
        try
        {
            _logger.LogDebug("🔄 Iniciando publicação na fila '{Queue}'...", queueName);
            
            EnsureConnection();
            _logger.LogDebug("✅ Conexão RabbitMQ verificada");

            if (_channel == null)
            {
                _logger.LogError("❌ Canal RabbitMQ é nulo após EnsureConnection");
                throw new InvalidOperationException("Canal RabbitMQ não está disponível");
            }

            var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? "graficaltda.exchange";
            _logger.LogDebug("📍 Exchange: {Exchange}, Queue: {Queue}", exchangeName, queueName);

            // Declarar queue de retorno
            _logger.LogDebug("📦 Declarando queue '{Queue}'...", queueName);
            _channel.QueueDeclare(
                queue: queueName,
                durable: true,
                exclusive: false,
                autoDelete: false);

            // Bind queue ao exchange
            _logger.LogDebug("🔗 Fazendo bind da queue '{Queue}' ao exchange '{Exchange}'...", queueName, exchangeName);
            _channel.QueueBind(
                queue: queueName,
                exchange: exchangeName,
                routingKey: queueName);

            // Serializar mensagem
            _logger.LogDebug("📝 Serializando mensagem...");
            var jsonMessage = JsonSerializer.Serialize(message, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var body = Encoding.UTF8.GetBytes(jsonMessage);
            _logger.LogDebug("✅ Mensagem serializada: {Size} bytes", body.Length);

            // Propriedades da mensagem
            var properties = _channel.CreateBasicProperties();
            properties.Persistent = true;
            properties.ContentType = "application/json";
            properties.Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

            // Publicar mensagem
            _logger.LogDebug("📤 Publicando mensagem no exchange '{Exchange}' com routing key '{RoutingKey}'...", 
                exchangeName, queueName);
            _channel.BasicPublish(
                exchange: exchangeName,
                routingKey: queueName,
                basicProperties: properties,
                body: body);

            _logger.LogInformation("✅ Mensagem publicada na fila '{Queue}' - Tamanho: {Size} bytes", 
                queueName, body.Length);
            _logger.LogDebug("Conteúdo da mensagem publicada: {Message}", jsonMessage);

            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao publicar mensagem na fila '{Queue}'", queueName);
            throw;
        }
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        lock (_lock)
        {
            if (_disposed)
                return;

            try
            {
                if (_channel != null)
                {
                    _channel.Close();
                    _channel.Dispose();
                    _channel = null;
                }

                if (_connection != null)
                {
                    _connection.Close();
                    _connection.Dispose();
                    _connection = null;
                }

                _logger.LogInformation("RabbitMQ Publisher disposed com sucesso");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante dispose do RabbitMQ Publisher");
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}
