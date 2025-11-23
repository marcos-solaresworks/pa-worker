using Microsoft.EntityFrameworkCore;
using OrquestradorCentral.Domain.Entities;

namespace OrquestradorCentral.Infrastructure.Data;

public class OrquestradorDbContext : DbContext
{
    public OrquestradorDbContext(DbContextOptions<OrquestradorDbContext> options) : base(options)
    {
    }

    public DbSet<LoteProcessamento> LotesProcessamento { get; set; }
    public DbSet<ProcessamentoLog> ProcessamentoLogs { get; set; }
    public DbSet<PerfilProcessamento> PerfisProcessamento { get; set; }
    public DbSet<Cliente> Clientes { get; set; }
    public DbSet<ArquivoPcl> ArquivosPcl { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Configuração das entidades
        modelBuilder.Entity<LoteProcessamento>(entity =>
        {
            entity.ToTable("lotes_processamento");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.UsuarioId).HasColumnName("usuario_id");
            entity.Property(e => e.PerfilProcessamentoId).HasColumnName("perfil_processamento_id");
            entity.Property(e => e.NomeArquivo).HasColumnName("nome_arquivo");
            entity.Property(e => e.CaminhoS3).HasColumnName("caminho_s3");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataProcessamento).HasColumnName("data_processamento");
        });

        modelBuilder.Entity<ProcessamentoLog>(entity =>
        {
            entity.ToTable("processamento_logs");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.LoteProcessamentoId).HasColumnName("lote_processamento_id");
            entity.Property(e => e.Mensagem).HasColumnName("mensagem");
            entity.Property(e => e.TipoLog).HasColumnName("tipo_log");
            entity.Property(e => e.DataHora).HasColumnName("data_hora");
        });

        modelBuilder.Entity<PerfilProcessamento>(entity =>
        {
            entity.ToTable("perfis_processamento");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.ClienteId).HasColumnName("cliente_id");
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Descricao).HasColumnName("descricao");
            entity.Property(e => e.TipoArquivo).HasColumnName("tipo_arquivo");
            entity.Property(e => e.Delimitador).HasColumnName("delimitador");
            entity.Property(e => e.TemplatePcl).HasColumnName("template_pcl");
            entity.Property(e => e.TipoProcessamento).HasColumnName("tipo_processamento");
            entity.Property(e => e.LambdaFunction).HasColumnName("lambda_function");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        modelBuilder.Entity<Cliente>(entity =>
        {
            entity.ToTable("clientes");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.Nome).HasColumnName("nome");
            entity.Property(e => e.Email).HasColumnName("email");
            entity.Property(e => e.Telefone).HasColumnName("telefone");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
        });

        modelBuilder.Entity<ArquivoPcl>(entity =>
        {
            entity.ToTable("arquivos_pcl");
            entity.HasKey(e => e.Id);
            entity.Property(e => e.Id).HasColumnName("id").ValueGeneratedOnAdd();
            entity.Property(e => e.LoteId).HasColumnName("lote_id");
            entity.Property(e => e.NomeArquivo).HasColumnName("nome_arquivo");
            entity.Property(e => e.CaminhoS3).HasColumnName("caminho_s3");
            entity.Property(e => e.CaminhoArquivo).HasColumnName("caminho_arquivo");
            entity.Property(e => e.TamanhoBytes).HasColumnName("tamanho_bytes");
            entity.Property(e => e.NumeroPaginas).HasColumnName("numero_paginas");
            entity.Property(e => e.Status).HasColumnName("status");
            entity.Property(e => e.DataCriacao).HasColumnName("data_criacao");
            entity.Property(e => e.DataUpload).HasColumnName("data_upload");
        });
    }
}