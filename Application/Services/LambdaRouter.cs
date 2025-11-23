using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Application.Models;
using OrquestradorCentral.Domain.Entities;
using System.IO;
using ClienteModel = OrquestradorCentral.Application.Models.Cliente;
using ArquivoPclModel = OrquestradorCentral.Application.Models.ArquivoPcl;
using ClienteEntity = OrquestradorCentral.Domain.Entities.Cliente;
using ArquivoPclEntity = OrquestradorCentral.Domain.Entities.ArquivoPcl;

namespace OrquestradorCentral.Application.Services;

public class LambdaRouter : ILambdaRouter
{
    private readonly ILambdaInvoker _lambdaInvoker;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LambdaRouter> _logger;
    private readonly IClienteRepository _clienteRepository;
    private readonly IArquivoPclRepository _arquivoPclRepository;
    private readonly Dictionary<string, string> _lambdaFunctions;

    public LambdaRouter(
        ILambdaInvoker lambdaInvoker,
        IConfiguration configuration,
        ILogger<LambdaRouter> logger,
        IClienteRepository clienteRepository,
        IArquivoPclRepository arquivoPclRepository)
    {
        _lambdaInvoker = lambdaInvoker;
        _configuration = configuration;
        _logger = logger;
        _clienteRepository = clienteRepository;
        _arquivoPclRepository = arquivoPclRepository;
        
        // Carregar mapeamento de Lambdas da configuração
        _lambdaFunctions = new Dictionary<string, string>();
        var functionsSection = _configuration.GetSection("AWS:Lambda:Functions");
        
        foreach (var kvp in functionsSection.GetChildren())
        {
            _lambdaFunctions[kvp.Key] = kvp.Value ?? "";
        }
        
        _logger.LogInformation("Lambda Router inicializado com {Count} funções mapeadas", _lambdaFunctions.Count);
        foreach (var func in _lambdaFunctions)
        {
            _logger.LogDebug("Mapeamento: {TipoProcessamento} -> {LambdaArn}", func.Key, func.Value);
        }
    }

    public async Task<LambdaProcessamentoResponse> RouteToLambdaAsync(LoteProcessamentoMessage message, PerfilProcessamento perfil)
    {
        _logger.LogInformation("🎯 Determinando tipo de processamento para o perfil '{Nome}'...", perfil.Nome);
        var tipoProcessamento = DeterminarTipoProcessamento(perfil);
        
        _logger.LogInformation("📍 Tipo de processamento identificado: {Tipo}", tipoProcessamento);
        _logger.LogInformation("🔎 Buscando ARN da Lambda para tipo '{Tipo}'...", tipoProcessamento);
        var lambdaArn = GetLambdaArnForProcessamento(tipoProcessamento);
        
        _logger.LogInformation("✅ Lambda selecionada: {ARN}", lambdaArn);
        _logger.LogInformation("🎯 Roteando lote {LoteId} para Lambda {TipoProcessamento}", 
            message.LoteId, tipoProcessamento);

        // Preparar payload específico para a Lambda
        _logger.LogDebug("📦 Preparando payload para a Lambda...");
        var payload = await PrepararPayloadCompletoAsync(message, perfil, tipoProcessamento, lambdaArn);
        _logger.LogInformation("✅ Payload preparado com Cliente e ArquivosPcl incluídos");

        // Adicionar metadados específicos baseados no tipo de processamento
        _logger.LogDebug("⚙️ Enriquecendo payload com configurações específicas do tipo {Tipo}...", tipoProcessamento);
        payload = EnriquecerPayloadPorTipo(payload, tipoProcessamento, perfil);
        _logger.LogInformation("✅ Payload preparado com {ConfigCount} configurações", payload.ProcessamentoConfig?.Count ?? 0);

        _logger.LogInformation("🚀 Invocando Lambda Invoker...");
        return await _lambdaInvoker.InvokeProcessamentoAsync(payload);
    }

    public string GetLambdaArnForProcessamento(string tipoProcessamento)
    {
        if (string.IsNullOrEmpty(tipoProcessamento))
        {
            _logger.LogWarning("Tipo de processamento não especificado, usando Lambda padrão");
            return _lambdaFunctions.GetValueOrDefault("Default", "");
        }

        var arn = _lambdaFunctions.GetValueOrDefault(tipoProcessamento);
        
        if (string.IsNullOrEmpty(arn))
        {
            _logger.LogWarning("Lambda não encontrada para tipo {TipoProcessamento}, usando Lambda padrão", tipoProcessamento);
            arn = _lambdaFunctions.GetValueOrDefault("Default", "");
        }

        if (string.IsNullOrEmpty(arn))
        {
            throw new InvalidOperationException($"Nenhuma Lambda configurada para tipo '{tipoProcessamento}' e Lambda padrão não encontrada");
        }

        return arn;
    }

    private string DeterminarTipoProcessamento(PerfilProcessamento perfil)
    {
        // Prioridade: 1. TipoProcessamento explícito, 2. LambdaFunction, 3. Nome do perfil
        if (!string.IsNullOrEmpty(perfil.TipoProcessamento))
        {
            return perfil.TipoProcessamento;
        }

        if (!string.IsNullOrEmpty(perfil.LambdaFunction))
        {
            // Extrair tipo do nome da função se necessário
            // Ex: "ProcessamentoClienteMalaDireta" -> "ClienteMalaDireta"
            var functionName = perfil.LambdaFunction;
            if (functionName.StartsWith("Processamento"))
            {
                return functionName.Substring("Processamento".Length);
            }
            return functionName;
        }

        // Fallback: tentar inferir do nome do perfil
        var nome = perfil.Nome.ToLowerInvariant();
        
        if (nome.Contains("mala") && nome.Contains("direta"))
            return "ClienteMalaDireta";
        
        if (nome.Contains("etiqueta"))
            return "ClienteEtiquetas";
            
        if (nome.Contains("cartao") || nome.Contains("cartão"))
            return "ClienteCartoes";

        // Se não conseguir determinar, usar padrão
        _logger.LogWarning("Não foi possível determinar tipo de processamento para perfil {PerfilId}, usando padrão", perfil.Id);
        return "Default";
    }

    private LambdaProcessamentoPayload EnriquecerPayloadPorTipo(
        LambdaProcessamentoPayload payload, 
        string tipoProcessamento, 
        PerfilProcessamento perfil)
    {
        // Adicionar configurações específicas baseadas no tipo de processamento
        switch (tipoProcessamento)
        {
            case "ClienteMalaDireta":
                payload.ProcessamentoConfig = new Dictionary<string, object>
                {
                    ["formatoSaida"] = "PCL_MALA_DIRETA",
                    ["incluirCodBarras"] = true,
                    ["margemEsquerda"] = "10mm",
                    ["margemSuperior"] = "15mm",
                    ["template"] = perfil.TemplatePcl ?? "template_mala_direta.pcl"
                };
                break;

            case "ClienteEtiquetas":
                payload.ProcessamentoConfig = new Dictionary<string, object>
                {
                    ["formatoSaida"] = "PCL_ETIQUETAS",
                    ["tipoEtiqueta"] = "PIMACO_6180",
                    ["etiquetasPorPagina"] = 30,
                    ["template"] = perfil.TemplatePcl ?? "template_etiquetas.pcl"
                };
                break;

            case "ClienteCartoes":
                payload.ProcessamentoConfig = new Dictionary<string, object>
                {
                    ["formatoSaida"] = "PCL_CARTOES",
                    ["tamanhoCartao"] = "85x54mm",
                    ["cartoesPorPagina"] = 10,
                    ["template"] = perfil.TemplatePcl ?? "template_cartoes.pcl"
                };
                break;

            default:
                payload.ProcessamentoConfig = new Dictionary<string, object>
                {
                    ["formatoSaida"] = "PCL_GENERICO",
                    ["template"] = perfil.TemplatePcl ?? "template_generico.pcl"
                };
                break;
        }

        _logger.LogDebug("Payload enriquecido para tipo {TipoProcessamento} com {ConfigCount} configurações", 
            tipoProcessamento, payload.ProcessamentoConfig.Count);

        return payload;
    }

    private async Task<LambdaProcessamentoPayload> PrepararPayloadCompletoAsync(
        LoteProcessamentoMessage message, 
        PerfilProcessamento perfil, 
        string tipoProcessamento, 
        string lambdaArn)
    {
        _logger.LogInformation("🔍 Buscando dados reais do banco de dados para lote {LoteId}...", message.LoteId);

        // 1. Buscar cliente real do banco de dados
        var clienteEntity = await _clienteRepository.GetByIdAsync(message.ClienteId);
        if (clienteEntity == null)
        {
            throw new InvalidOperationException($"Cliente {message.ClienteId} não encontrado no banco de dados");
        }

        _logger.LogInformation("✅ Cliente encontrado: {Nome} (ID: {Id})", clienteEntity.Nome, clienteEntity.Id);

        // 2. Criar lista de arquivos PCL a partir dos dados da mensagem
        // Nota: A tabela arquivos_pcl não existe no banco, então usamos os dados da mensagem
        _logger.LogInformation("📄 Usando informações do arquivo da mensagem recebida");
        var arquivosEntity = new List<ArquivoPclEntity>
        {
            new ArquivoPclEntity
            {
                LoteId = message.LoteId,
                NomeArquivo = message.NomeArquivo,
                CaminhoArquivo = message.CaminhoS3,
                CaminhoS3 = message.CaminhoS3,
                TamanhoBytes = 0,
                NumeroPaginas = 0,
                DataUpload = DateTime.UtcNow,
                DataCriacao = DateTime.UtcNow
            }
        };

        _logger.LogInformation("✅ {Count} arquivo(s) PCL preparado(s) para processamento", arquivosEntity.Count);

        // 3. Converter entidades do Domain para modelos do Application
        var clienteModel = new ClienteModel
        {
            Id = clienteEntity.Id,
            Nome = clienteEntity.Nome,
            Email = clienteEntity.Email ?? string.Empty,
            Telefone = clienteEntity.Telefone ?? string.Empty,
            DataCadastro = clienteEntity.DataCriacao
        };

        var arquivosModel = arquivosEntity.Select(a => new ArquivoPclModel
        {
            Id = a.Id,
            LoteId = a.LoteId,
            NomeArquivo = a.NomeArquivo,
            CaminhoArquivo = a.CaminhoArquivo,
            TamanhoBytes = a.TamanhoBytes,
            NumeroPaginas = a.NumeroPaginas,
            DataUpload = a.DataUpload
        }).ToList();

        foreach (var arquivo in arquivosModel)
        {
            _logger.LogInformation("   📄 Arquivo: {Nome} - Caminho S3: {Caminho}", arquivo.NomeArquivo, arquivo.CaminhoArquivo);
        }

        var payload = new LambdaProcessamentoPayload
        {
            LoteId = message.LoteId,
            Cliente = clienteModel,
            PerfilProcessamento = perfil,
            ArquivosPcl = arquivosModel,
            DataCriacao = DateTime.UtcNow,
            TipoProcessamento = tipoProcessamento,
            LambdaArn = lambdaArn,
            ProcessamentoConfig = new Dictionary<string, object>()
        };

        // Enriquecer com configurações específicas
        payload = EnriquecerPayloadPorTipo(payload, tipoProcessamento, perfil);
        
        _logger.LogInformation("✅ Payload preparado com dados reais do banco de dados");
        return payload;
    }
}