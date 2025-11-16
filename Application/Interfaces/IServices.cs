using OrquestradorCentral.Application.Models;
using OrquestradorCentral.Domain.Entities;

namespace OrquestradorCentral.Application.Interfaces;

public interface IMessageConsumer
{
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
}

public interface IProcessamentoService
{
    Task ProcessarLoteAsync(LoteProcessamentoMessage message);
}

public interface ILambdaInvoker
{
    Task<LambdaProcessamentoResponse> InvokeProcessamentoAsync(LambdaProcessamentoPayload payload);
}

public interface ILambdaRouter
{
    Task<LambdaProcessamentoResponse> RouteToLambdaAsync(LoteProcessamentoMessage message, PerfilProcessamento perfil);
    string GetLambdaArnForProcessamento(string tipoProcessamento);
}

public interface ILoteRepository
{
    Task<LoteProcessamento?> GetByIdAsync(int id);
    Task UpdateAsync(LoteProcessamento lote);
}

public interface IPerfilProcessamentoRepository
{
    Task<PerfilProcessamento?> GetByIdAsync(int id);
}

public interface IProcessamentoLogRepository
{
    Task AddAsync(ProcessamentoLog log);
}

public interface IMessagePublisher
{
    Task PublishAsync<T>(T message, string queueName) where T : class;
}