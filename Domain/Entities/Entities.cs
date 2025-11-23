using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrquestradorCentral.Domain.Entities;

public class LoteProcessamento
{
    public int Id { get; set; }
    
    [Required]
    public int ClienteId { get; set; }
    
    [Required]
    public int UsuarioId { get; set; }
    
    [Required]
    public int PerfilProcessamentoId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string NomeArquivo { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(300)]
    public string CaminhoS3 { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(50)]
    public string Status { get; set; } = string.Empty;
    
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    
    public DateTime? DataProcessamento { get; set; }
}

public class ProcessamentoLog
{
    public int Id { get; set; }
    
    [Required]
    public int LoteProcessamentoId { get; set; }
    
    public string? Mensagem { get; set; }
    
    [MaxLength(20)]
    public string? TipoLog { get; set; }
    
    public DateTime DataHora { get; set; } = DateTime.UtcNow;
}

public class PerfilProcessamento
{
    public int Id { get; set; }
    
    [Required]
    public int ClienteId { get; set; }
    
    [Required]
    [MaxLength(100)]
    public string Nome { get; set; } = string.Empty;
    
    public string? Descricao { get; set; }
    
    [MaxLength(20)]
    public string? TipoArquivo { get; set; }
    
    [MaxLength(5)]
    public string? Delimitador { get; set; }
    
    [MaxLength(200)]
    public string? TemplatePcl { get; set; }
    
    /// <summary>
    /// Tipo de processamento que determina qual Lambda será invocada
    /// Ex: "ClienteMalaDireta", "ClienteEtiquetas", "ClienteCartoes"
    /// </summary>
    [MaxLength(50)]
    public string? TipoProcessamento { get; set; }
    
    /// <summary>
    /// Nome da Lambda function a ser invocada para este tipo de processamento
    /// Ex: "ProcessamentoClienteMalaDireta"
    /// </summary>
    [MaxLength(100)]
    public string? LambdaFunction { get; set; }
    
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}

public class Cliente
{
    public int Id { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string Nome { get; set; } = string.Empty;
    
    [MaxLength(100)]
    public string? Email { get; set; }
    
    [MaxLength(20)]
    public string? Telefone { get; set; }
    
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
}

public class ArquivoPcl
{
    public int Id { get; set; }
    
    [Required]
    public int LoteId { get; set; }
    
    [Required]
    [MaxLength(200)]
    public string NomeArquivo { get; set; } = string.Empty;
    
    [Required]
    [MaxLength(300)]
    public string CaminhoS3 { get; set; } = string.Empty;
    
    [MaxLength(300)]
    public string CaminhoArquivo { get; set; } = string.Empty;
    
    public long TamanhoBytes { get; set; }
    
    public int NumeroPaginas { get; set; }
    
    [MaxLength(50)]
    public string? Status { get; set; }
    
    public DateTime DataCriacao { get; set; } = DateTime.UtcNow;
    
    public DateTime DataUpload { get; set; } = DateTime.UtcNow;
}