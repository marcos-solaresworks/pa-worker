using Amazon.Lambda;
using Amazon.Lambda.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Application.Models;
using Polly;
using System.Text.Json;

namespace OrquestradorCentral.Infrastructure.Lambda;

public class LambdaInvoker : ILambdaInvoker
{
    private readonly IAmazonLambda _lambdaClient;
    private readonly IConfiguration _configuration;
    private readonly ILogger<LambdaInvoker> _logger;
    private readonly IAsyncPolicy _retryPolicy;

    public LambdaInvoker(
        IAmazonLambda lambdaClient,
        IConfiguration configuration,
        ILogger<LambdaInvoker> logger)
    {
        _lambdaClient = lambdaClient;
        _configuration = configuration;
        _logger = logger;

        // Configuração simples para retry (sem Polly por enquanto)
        _retryPolicy = Policy.NoOpAsync();
    }

    public async Task<LambdaProcessamentoResponse> InvokeProcessamentoAsync(LambdaProcessamentoPayload payload)
    {
        var functionArn = payload.LambdaArn;
        if (string.IsNullOrEmpty(functionArn))
        {
            throw new InvalidOperationException($"Lambda ARN não especificado para tipo de processamento {payload.TipoProcessamento}");
        }

        try
        {
            _logger.LogInformation("🔄 Invocando Lambda {TipoProcessamento} para lote {LoteId} com arquivo {Arquivo} (ARN: {Arn})", 
                payload.TipoProcessamento, payload.LoteId, payload.ArquivosPcl?.FirstOrDefault()?.NomeArquivo ?? "N/A", functionArn);

            // ========================================================================
            // ✅ INVOCAÇÃO REAL DA LAMBDA AWS
            // ========================================================================
            
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            _logger.LogDebug("📤 Payload JSON da Lambda: {Payload}", jsonPayload);

            var request = new InvokeRequest
            {
                FunctionName = functionArn,
                Payload = jsonPayload,
                InvocationType = InvocationType.RequestResponse
            };

            _logger.LogInformation("📡 Invocando Lambda AWS: {Arn}", functionArn);
            var invokeResponse = await _lambdaClient.InvokeAsync(request);
            
            if (invokeResponse.StatusCode != 200)
            {
                throw new InvalidOperationException($"Lambda invocation failed with status code: {invokeResponse.StatusCode}");
            }

            _logger.LogInformation("✅ Lambda invocada com sucesso - Status: {Status}", invokeResponse.StatusCode);

            // Processar resposta
            var responsePayload = invokeResponse.Payload;
            using var reader = new StreamReader(responsePayload);
            var responseJson = await reader.ReadToEndAsync();

            _logger.LogDebug("📥 Resposta da Lambda para lote {LoteId}: {Response}", payload.LoteId, responseJson);

            var lambdaResponse = JsonSerializer.Deserialize<LambdaProcessamentoResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (lambdaResponse == null)
            {
                _logger.LogError("❌ Resposta da Lambda inválida ou nula");
                return new LambdaProcessamentoResponse
                {
                    LoteId = payload.LoteId,
                    Sucesso = false,
                    Status = "Erro",
                    MensagemRetorno = "Resposta inválida da Lambda",
                    DataProcessamento = DateTime.UtcNow,
                    TempoProcessamento = TimeSpan.Zero,
                    ArquivosProcessados = new List<string>(),
                    TotalPaginas = 0
                };
            }

            _logger.LogInformation("✅ Resposta da Lambda processada: Sucesso={Sucesso}, Arquivos={Arquivos}, Páginas={Paginas}", 
                lambdaResponse.Sucesso, lambdaResponse.ArquivosProcessados.Count, lambdaResponse.TotalPaginas);

            return lambdaResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao invocar Lambda para lote {LoteId}", payload.LoteId);
            
            return new LambdaProcessamentoResponse
            {
                LoteId = payload.LoteId,
                Sucesso = false,
                Status = "Erro",
                MensagemRetorno = ex.Message,
                DataProcessamento = DateTime.UtcNow,
                TempoProcessamento = TimeSpan.Zero,
                ArquivosProcessados = new List<string>(),
                TotalPaginas = 0
            };
        }
    }
}