using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OrquestradorCentral.Infrastructure.Messaging;

namespace OrquestradorCentral;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;
    private readonly RabbitMQConsumer _rabbitMQConsumer;

    public Worker(
        ILogger<Worker> logger,
        RabbitMQConsumer rabbitMQConsumer)
    {
        _logger = logger;
        _rabbitMQConsumer = rabbitMQConsumer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrquestradorCentral Worker iniciado em: {time}", DateTimeOffset.Now);

        try
        {
            // Iniciar o consumer RabbitMQ
            await _rabbitMQConsumer.StartAsync(stoppingToken);

            // Manter o serviço rodando
            while (!stoppingToken.IsCancellationRequested)
            {
                await Task.Delay(5000, stoppingToken); // Check a cada 5 segundos
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrquestradorCentral Worker foi cancelado");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro crítico no OrquestradorCentral Worker");
            throw;
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrquestradorCentral Worker parando...");
        
        await _rabbitMQConsumer.StopAsync(cancellationToken);
        
        await base.StopAsync(cancellationToken);
        
        _logger.LogInformation("OrquestradorCentral Worker parado");
    }
}
