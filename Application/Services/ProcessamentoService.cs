using Microsoft.Extensions.Logging;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Application.Models;
using OrquestradorCentral.Domain.Entities;

namespace OrquestradorCentral.Application.Services;

public class ProcessamentoService : IProcessamentoService
{
    private readonly ILoteRepository _loteRepository;
    private readonly IProcessamentoLogRepository _logRepository;
    private readonly IPerfilProcessamentoRepository _perfilRepository;
    private readonly ILambdaRouter _lambdaRouter;
    private readonly IMessagePublisher _messagePublisher;
    private readonly ILogger<ProcessamentoService> _logger;

    public ProcessamentoService(
        ILoteRepository loteRepository,
        IProcessamentoLogRepository logRepository,
        IPerfilProcessamentoRepository perfilRepository,
        ILambdaRouter lambdaRouter,
        IMessagePublisher messagePublisher,
        ILogger<ProcessamentoService> logger)
    {
        _loteRepository = loteRepository;
        _logRepository = logRepository;
        _perfilRepository = perfilRepository;
        _lambdaRouter = lambdaRouter;
        _messagePublisher = messagePublisher;
        _logger = logger;
    }

    public async Task ProcessarLoteAsync(LoteProcessamentoMessage message)
    {
        var loteId = message.LoteId;
        
        try
        {
            _logger.LogInformation("🔍 Buscando dados do lote {LoteId} no banco de dados...", loteId);

            // Buscar lote no banco
            var lote = await _loteRepository.GetByIdAsync(loteId);
            if (lote == null)
            {
                _logger.LogError("❌ Lote {LoteId} não encontrado no banco de dados", loteId);
                return;
            }

            _logger.LogInformation("✅ Lote {LoteId} encontrado - Status: {Status}, Perfil: {PerfilId}", 
                loteId, lote.Status, lote.PerfilProcessamentoId);

            // Buscar perfil de processamento
            _logger.LogInformation("🔍 Buscando perfil de processamento {PerfilId}...", lote.PerfilProcessamentoId);
            var perfil = await _perfilRepository.GetByIdAsync(lote.PerfilProcessamentoId);
            if (perfil == null)
            {
                _logger.LogError("❌ Perfil de processamento não encontrado para lote {LoteId}", loteId);
                await AtualizarStatusLoteAsync(lote, "Erro", "Perfil de processamento não encontrado");
                return;
            }

            _logger.LogInformation("✅ Perfil encontrado - Nome: {Nome}, Tipo: {Tipo}, Lambda: {Lambda}", 
                perfil.Nome, perfil.TipoProcessamento ?? "Default", perfil.LambdaFunction ?? "N/A");

            // Atualizar status para processando
            _logger.LogInformation("📝 Atualizando status do lote para 'Processando'...");
            await AtualizarStatusLoteAsync(lote, "Processando");

            // Criar log de início
            await CriarLogProcessamentoAsync(loteId, $"Iniciando processamento via Lambda - Tipo: {perfil.TipoProcessamento ?? "Default"}", "Info");

            // Rotear para Lambda específica baseada no perfil
            _logger.LogInformation("🚀 [STEP 3] Chamando Lambda Router para lote {LoteId}...", loteId);
            var response = await _lambdaRouter.RouteToLambdaAsync(message, perfil);

            if (response.Sucesso)
            {
                // Processamento bem-sucedido
                _logger.LogInformation("✅ [STEP 4] Processamento concluído com sucesso!");
                _logger.LogInformation("   📊 Arquivos processados: {Arquivos}", response.ArquivosProcessados.Count);
                _logger.LogInformation("   📄 Total de páginas: {Paginas}", response.TotalPaginas);
                _logger.LogInformation("   ⏱️ Tempo de processamento: {Tempo:F2}s", response.TempoProcessamento.TotalSeconds);
                
                var arquivosSaida = response.DetalhesProcessamento?.ArquivosProcessadosS3 ?? new List<string>();
                foreach (var arquivo in arquivosSaida)
                {
                    _logger.LogInformation("   📁 Arquivo de saída: {Output}", arquivo);
                }
                
                // Atualizar status do lote para concluído e salvar caminho do arquivo processado
                var caminhoProcessado = arquivosSaida.FirstOrDefault();
                await AtualizarStatusLoteAsync(lote, "Concluído", caminhoProcessado);
                
                // Criar log de conclusão
                await CriarLogProcessamentoAsync(loteId, $"Processamento concluído com sucesso. Arquivos: {response.ArquivosProcessados.Count}, Páginas: {response.TotalPaginas}, Tempo: {response.TempoProcessamento.TotalSeconds:F2}s", "Info");
                
                // Publicar mensagem de retorno
                _logger.LogInformation("💾 Status atualizado para 'Concluído' no banco de dados");
                _logger.LogInformation("📝 Preparando mensagem de retorno para publicação...");
                var retornoMessage = new LoteProcessamentoRetornoMessage
                {
                    LoteId = loteId,
                    Sucesso = true,
                    Status = "Concluído",
                    RegistrosProcessados = response.TotalPaginas,
                    ArquivoSaida = string.Join(", ", arquivosSaida),
                    TempoProcessamentoSegundos = response.TempoProcessamento.TotalSeconds,
                    DataProcessamento = DateTime.UtcNow
                };

                _logger.LogInformation("📮 [STEP 5] Publicando mensagem de retorno na fila 'lote.processamento.retorno'...");
                _logger.LogDebug("Mensagem: LoteId={LoteId}, Status={Status}, Registros={Registros}, Tempo={Tempo}s, ArquivoSaida={Arquivo}", 
                    retornoMessage.LoteId, retornoMessage.Status, retornoMessage.RegistrosProcessados, retornoMessage.TempoProcessamentoSegundos, retornoMessage.ArquivoSaida);
                
                try
                {
                    await _messagePublisher.PublishAsync(retornoMessage, "lote.processamento.retorno");
                    _logger.LogInformation("✅ Mensagem de retorno publicada com sucesso na fila 'lote.processamento.retorno'!");
                }
                catch (Exception pubEx)
                {
                    _logger.LogError(pubEx, "❌ Erro ao publicar mensagem de retorno");
                    throw;
                }
            }
            else
            {
                // Erro no processamento
                var errorMessage = response.MensagemRetorno ?? "Erro desconhecido no processamento";
                _logger.LogError("❌ Erro no processamento do lote {LoteId}: {Error}", loteId, errorMessage);
                
                await AtualizarStatusLoteAsync(lote, "Erro", errorMessage);
                await CriarLogProcessamentoAsync(loteId, $"Erro no processamento: {errorMessage}", "Error");

                // Publicar mensagem de retorno com erro
                _logger.LogInformation("📝 Preparando mensagem de erro para publicação...");
                var retornoMessage = new LoteProcessamentoRetornoMessage
                {
                    LoteId = loteId,
                    Sucesso = false,
                    Status = "Erro",
                    RegistrosProcessados = 0,
                    MensagemErro = errorMessage,
                    DataProcessamento = DateTime.UtcNow
                };

                _logger.LogInformation("📮 [STEP 5] Publicando mensagem de erro na fila 'lote.processamento.retorno'...");
                try
                {
                    await _messagePublisher.PublishAsync(retornoMessage, "lote.processamento.retorno");
                    _logger.LogInformation("✅ Mensagem de erro publicada com sucesso!");
                }
                catch (Exception pubEx)
                {
                    _logger.LogError(pubEx, "❌ Erro ao publicar mensagem de erro");
                    throw;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "💥 Erro crítico ao processar lote {LoteId}", loteId);
            
            // Tentar atualizar status mesmo com erro
            try
            {
                var lote = await _loteRepository.GetByIdAsync(loteId);
                if (lote != null)
                {
                    await AtualizarStatusLoteAsync(lote, "Erro", ex.Message);
                }
                
                await CriarLogProcessamentoAsync(loteId, $"Erro crítico: {ex.Message}", "Error");

                // Publicar mensagem de retorno com erro crítico
                _logger.LogInformation("📝 Preparando mensagem de erro crítico para publicação...");
                var retornoMessage = new LoteProcessamentoRetornoMessage
                {
                    LoteId = loteId,
                    Sucesso = false,
                    Status = "Erro",
                    RegistrosProcessados = 0,
                    MensagemErro = $"Erro crítico: {ex.Message}",
                    DataProcessamento = DateTime.UtcNow
                };

                _logger.LogInformation("📮 [STEP 5] Publicando mensagem de erro crítico na fila 'lote.processamento.retorno'...");
                try
                {
                    await _messagePublisher.PublishAsync(retornoMessage, "lote.processamento.retorno");
                    _logger.LogInformation("✅ Mensagem de erro crítico publicada com sucesso!");
                }
                catch (Exception pubEx)
                {
                    _logger.LogError(pubEx, "❌ Erro ao publicar mensagem de erro crítico: {Error}", pubEx.Message);
                    // Não lançar exceção aqui para não mascarar o erro original
                }
            }
            catch (Exception logEx)
            {
                _logger.LogError(logEx, "❌ Erro ao salvar log de erro para lote {LoteId}", loteId);
            }
        }
    }

    private async Task AtualizarStatusLoteAsync(LoteProcessamento lote, string status, string? caminhoProcessadoS3 = null)
    {
        lote.Status = status;
        lote.DataProcessamento = DateTime.UtcNow;
        
        if (!string.IsNullOrEmpty(caminhoProcessadoS3))
        {
            lote.CaminhoProcessadoS3 = caminhoProcessadoS3;
            _logger.LogInformation("💾 Salvando caminho do arquivo processado: {Caminho}", caminhoProcessadoS3);
        }

        await _loteRepository.UpdateAsync(lote);
    }

    private async Task CriarLogProcessamentoAsync(int loteId, string mensagem, string nivel)
    {
        var log = new ProcessamentoLog
        {
            LoteProcessamentoId = loteId,
            Mensagem = mensagem,
            TipoLog = nivel,
            DataHora = DateTime.UtcNow
        };

        await _logRepository.AddAsync(log);
    }
}