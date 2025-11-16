namespace OrquestradorCentral.Application.Models;

public class LoteProcessamentoMessage
{
    public int LoteId { get; set; }
    public int ClienteId { get; set; }
    public string NomeArquivo { get; set; } = string.Empty;
    public string CaminhoS3 { get; set; } = string.Empty;
    public int PerfilId { get; set; }
    
    // Propriedades derivadas do CaminhoS3 para compatibilidade
    public string S3Bucket => ExtractS3Bucket(CaminhoS3);
    public string S3Key => ExtractS3Key(CaminhoS3);
    public string? CallbackUrl { get; set; }
    
    private string ExtractS3Bucket(string s3Path)
    {
        if (string.IsNullOrEmpty(s3Path)) return "";
        
        try
        {
            // Se já começar com s3://, é um caminho S3 completo
            if (s3Path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(s3Path);
                return uri.Host;
            }
            
            // Se não tem protocolo, assumir que é bucket/key e extrair bucket
            var parts = s3Path.Split('/', 2);
            return parts.Length > 0 ? parts[0] : "";
        }
        catch
        {
            // Em caso de erro, retornar vazio
            return "";
        }
    }
    
    private string ExtractS3Key(string s3Path)
    {
        if (string.IsNullOrEmpty(s3Path)) return "";
        
        try
        {
            // Se já começar com s3://, é um caminho S3 completo
            if (s3Path.StartsWith("s3://", StringComparison.OrdinalIgnoreCase))
            {
                var uri = new Uri(s3Path);
                return uri.AbsolutePath.TrimStart('/');
            }
            
            // Se não tem protocolo, assumir que é bucket/key e extrair key
            var parts = s3Path.Split('/', 2);
            return parts.Length > 1 ? parts[1] : s3Path;
        }
        catch
        {
            // Em caso de erro, retornar o caminho original
            return s3Path;
        }
    }
}

public class LambdaProcessamentoPayload
{
    public int LoteId { get; set; }
    public string S3Bucket { get; set; } = string.Empty;
    public string S3Key { get; set; } = string.Empty;
    public PerfilProcessamentoDto PerfilProcessamento { get; set; } = new();
    public string? CallbackUrl { get; set; }
    public string TipoProcessamento { get; set; } = string.Empty;
    public string LambdaArn { get; set; } = string.Empty;
    public Dictionary<string, object> ProcessamentoConfig { get; set; } = new();
}

public class PerfilProcessamentoDto
{
    public int Id { get; set; }
    public string Nome { get; set; } = string.Empty;
    public string? TemplatePcl { get; set; }
    public string? TipoProcessamento { get; set; }
    public string? LambdaFunction { get; set; }
}

public class LambdaProcessamentoResponse
{
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public int RegistrosProcessados { get; set; }
    public string? OutputPath { get; set; }
    public TimeSpan TempoProcessamento { get; set; }
}

public class LoteProcessamentoRetornoMessage
{
    public int LoteId { get; set; }
    public bool Sucesso { get; set; }
    public string Status { get; set; } = string.Empty;
    public int RegistrosProcessados { get; set; }
    public string? ArquivoSaida { get; set; }
    public double TempoProcessamentoSegundos { get; set; }
    public string? MensagemErro { get; set; }
    public DateTime DataProcessamento { get; set; }
}