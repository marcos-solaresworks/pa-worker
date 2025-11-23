using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Application.Models;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using System.Text.Json;

namespace OrquestradorCentral.Infrastructure.Messaging;

public class RabbitMQConsumer : IMessageConsumer, IDisposable
{
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<RabbitMQConsumer> _logger;
    private IConnection? _connection;
    private IModel? _channel;
    private string? _consumerTag;
    private readonly object _lock = new object();
    private bool _disposed = false;

    public RabbitMQConsumer(
        IConfiguration configuration,
        IServiceProvider serviceProvider,
        ILogger<RabbitMQConsumer> logger)
    {
        _configuration = configuration;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Connect();
            StartConsuming(cancellationToken);
            _logger.LogInformation("RabbitMQ Consumer iniciado com sucesso");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao iniciar RabbitMQ Consumer");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrEmpty(_consumerTag) && _channel != null)
            {
                _channel.BasicCancel(_consumerTag);
                _consumerTag = null;
            }

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

            _logger.LogInformation("RabbitMQ Consumer parado com sucesso");
            return Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao parar RabbitMQ Consumer");
            return Task.CompletedTask;
        }
    }

    private void Connect()
    {
        var hostName = _configuration["RabbitMQ:HostName"] ?? "localhost";
        var port = _configuration.GetValue<int>("RabbitMQ:Port", 5672);
        var userName = _configuration["RabbitMQ:UserName"] ?? "guest";
        var password = _configuration["RabbitMQ:Password"] ?? "guest";
        var virtualHost = _configuration["RabbitMQ:VirtualHost"] ?? "/";

        _logger.LogInformation("🔍 Tentando conectar ao RabbitMQ com as seguintes credenciais:");
        _logger.LogInformation("   HostName: {HostName}", hostName);
        _logger.LogInformation("   Port: {Port}", port);
        _logger.LogInformation("   UserName: {UserName}", userName);
        _logger.LogInformation("   Password: {Password}", password);
        _logger.LogInformation("   VirtualHost: {VirtualHost}", virtualHost);

        var factory = new ConnectionFactory()
        {
            HostName = hostName,
            Port = port,
            UserName = userName,
            Password = password,
            VirtualHost = virtualHost,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = TimeSpan.FromSeconds(10)
        };

        _connection = factory.CreateConnection();
        _channel = _connection.CreateModel();

        // Configurar QoS
        _channel.BasicQos(prefetchSize: 0, prefetchCount: 1, global: false);

        var exchangeName = _configuration["RabbitMQ:ExchangeName"] ?? "graficaltda.exchange";
        var queueName = _configuration["RabbitMQ:QueueName"] ?? "lote.processamento";
        var queueRetorno = _configuration["RabbitMQ:QueueRetorno"] ?? "lote.processamento.retorno";

        // Exchange é criada pela API Central, não declaramos aqui
        // Apenas declarar nossas queues

        // Declarar queue de consumo
        _channel.QueueDeclare(
            queue: queueName,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Bind queue de consumo ao exchange
        _channel.QueueBind(
            queue: queueName,
            exchange: exchangeName,
            routingKey: queueName);

        // Declarar queue de retorno
        _channel.QueueDeclare(
            queue: queueRetorno,
            durable: true,
            exclusive: false,
            autoDelete: false);

        // Bind queue de retorno ao exchange
        _channel.QueueBind(
            queue: queueRetorno,
            exchange: exchangeName,
            routingKey: queueRetorno);

        _logger.LogInformation("✅ Conectado ao RabbitMQ - Exchange: {Exchange}", exchangeName);
        _logger.LogInformation("   📥 Queue de entrada: {QueueEntrada}", queueName);
        _logger.LogInformation("   📤 Queue de retorno: {QueueRetorno}", queueRetorno);
    }

    private void StartConsuming(CancellationToken cancellationToken)
    {
        if (_channel == null)
            throw new InvalidOperationException("Canal RabbitMQ não está conectado");

        var queueName = _configuration["RabbitMQ:QueueName"] ?? "lote.processamento";
        
        var consumer = new EventingBasicConsumer(_channel);
        
        consumer.Received += async (model, ea) =>
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                _logger.LogInformation("📨 [STEP 1] Mensagem recebida do RabbitMQ - DeliveryTag: {Tag}, Tamanho: {Size} bytes", 
                    ea.DeliveryTag, body.Length);
                _logger.LogDebug("Conteúdo da mensagem: {Message}", message);
                
                // Deserializar mensagem
                var loteMessage = JsonSerializer.Deserialize<LoteProcessamentoMessage>(message, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (loteMessage == null)
                {
                    _logger.LogError("❌ Não foi possível deserializar a mensagem: {Message}", message);
                    _channel.BasicNack(ea.DeliveryTag, false, false);
                    return;
                }

                _logger.LogInformation("✅ Mensagem deserializada - LoteId: {LoteId}, CaminhoS3: {CaminhoS3}", 
                    loteMessage.LoteId, loteMessage.CaminhoS3);
                _logger.LogDebug("   S3Bucket extraído: {Bucket}, S3Key extraído: {Key}", 
                    loteMessage.S3Bucket, loteMessage.S3Key);

                // Processar mensagem
                _logger.LogInformation("⚙️ [STEP 2] Iniciando processamento do lote {LoteId}...", loteMessage.LoteId);
                await ProcessMessageAsync(loteMessage);
                
                // Confirmar processamento
                _channel.BasicAck(ea.DeliveryTag, false);
                
                _logger.LogInformation("✅ [STEP 5] Mensagem processada e confirmada no RabbitMQ - Lote: {LoteId}", loteMessage.LoteId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Erro ao processar mensagem: {Message}", message);
                
                // Rejeitar mensagem (não vai para DLQ com requeue=false)
                _channel.BasicNack(ea.DeliveryTag, false, false);
            }
        };

        _consumerTag = _channel.BasicConsume(
            queue: queueName,
            autoAck: false,
            consumer: consumer);

        _logger.LogInformation("Consumer iniciado na queue: {Queue} com tag: {ConsumerTag}", 
            queueName, _consumerTag);
    }

    private async Task ProcessMessageAsync(LoteProcessamentoMessage message)
    {
        using var scope = _serviceProvider.CreateScope();
        var processamentoService = scope.ServiceProvider.GetRequiredService<IProcessamentoService>();
        
        await processamentoService.ProcessarLoteAsync(message);
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
                StopAsync().Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro durante dispose do RabbitMQ Consumer");
            }

            _disposed = true;
        }

        GC.SuppressFinalize(this);
    }
}