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
            _logger.LogInformation("🔄 [SIMULAÇÃO] Invocando Lambda {TipoProcessamento} para lote {LoteId} com arquivo {Arquivo} (ARN: {Arn})", 
                payload.TipoProcessamento, payload.LoteId, payload.S3Key, functionArn);

            // ========================================================================
            // 🚧 SIMULAÇÃO DE PROCESSAMENTO - INVOCAÇÃO REAL DA LAMBDA COMENTADA
            // ========================================================================
            // A invocação real será habilitada quando a Lambda estiver disponível
            // Para o MVP, simulamos um processamento bem-sucedido
            // ========================================================================

            /* CÓDIGO REAL DA LAMBDA (COMENTADO TEMPORARIAMENTE)
            
            var jsonPayload = JsonSerializer.Serialize(payload, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            var context = new Context($"lote-{payload.LoteId}");
            context["LoteId"] = payload.LoteId.ToString();

            var response = await _retryPolicy.ExecuteAsync(async (ctx) =>
            {
                var request = new InvokeRequest
                {
                    FunctionName = functionArn,
                    Payload = jsonPayload,
                    InvocationType = InvocationType.RequestResponse
                };

                var invokeResponse = await _lambdaClient.InvokeAsync(request);
                
                if (invokeResponse.StatusCode != 200)
                {
                    throw new AmazonLambdaException($"Lambda invocation failed with status code: {invokeResponse.StatusCode}");
                }

                return invokeResponse;
            }, context);

            // Processar resposta
            var responsePayload = response.Payload;
            using var reader = new StreamReader(responsePayload);
            var responseJson = await reader.ReadToEndAsync();

            _logger.LogDebug("Resposta da Lambda para lote {LoteId}: {Response}", payload.LoteId, responseJson);

            var lambdaResponse = JsonSerializer.Deserialize<LambdaProcessamentoResponse>(responseJson, new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });

            if (lambdaResponse == null)
            {
                return new LambdaProcessamentoResponse
                {
                    Success = false,
                    ErrorMessage = "Resposta inválida da Lambda"
                };
            }

            */

            // ========================================================================
            // ✅ SIMULAÇÃO: RETORNO DE SUCESSO
            // ========================================================================

            // Simular tempo de processamento realista
            var inicioProcessamento = DateTime.UtcNow;
            await Task.Delay(TimeSpan.FromSeconds(2)); // Simula 2 segundos de processamento
            var tempoProcessamento = DateTime.UtcNow - inicioProcessamento;

            // Simular número de registros processados baseado no nome do arquivo
            var registrosSimulados = Random.Shared.Next(50, 500);

            // Construir caminho de saída simulado no S3
            var outputPath = $"s3://{payload.S3Bucket}/processados/{payload.LoteId}/output_{DateTime.UtcNow:yyyyMMddHHmmss}.pcl";

            var lambdaResponse = new LambdaProcessamentoResponse
            {
                Success = true,
                RegistrosProcessados = registrosSimulados,
                OutputPath = outputPath,
                TempoProcessamento = tempoProcessamento,
                ErrorMessage = null
            };

            _logger.LogInformation("✅ [SIMULAÇÃO] Lambda processou lote {LoteId} com sucesso. Registros: {Registros}, Tempo: {Tempo}ms, Output: {Output}", 
                payload.LoteId, lambdaResponse.RegistrosProcessados, lambdaResponse.TempoProcessamento.TotalMilliseconds, outputPath);

            return lambdaResponse;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "❌ Erro ao invocar Lambda para lote {LoteId}", payload.LoteId);
            
            return new LambdaProcessamentoResponse
            {
                Success = false,
                ErrorMessage = ex.Message
            };
        }
    }
}