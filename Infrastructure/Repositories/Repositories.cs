using Microsoft.EntityFrameworkCore;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Domain.Entities;
using OrquestradorCentral.Infrastructure.Data;

namespace OrquestradorCentral.Infrastructure.Repositories;

public class LoteRepository : ILoteRepository
{
    private readonly OrquestradorDbContext _context;

    public LoteRepository(OrquestradorDbContext context)
    {
        _context = context;
    }

    public async Task<LoteProcessamento?> GetByIdAsync(int id)
    {
        return await _context.LotesProcessamento.FirstOrDefaultAsync(l => l.Id == id);
    }

    public async Task UpdateAsync(LoteProcessamento lote)
    {
        _context.LotesProcessamento.Update(lote);
        await _context.SaveChangesAsync();
    }
}

public class PerfilProcessamentoRepository : IPerfilProcessamentoRepository
{
    private readonly OrquestradorDbContext _context;

    public PerfilProcessamentoRepository(OrquestradorDbContext context)
    {
        _context = context;
    }

        public async Task<PerfilProcessamento?> GetByIdAsync(int id)
        {
            // Projetar apenas as colunas "seguras" que sabemos existir na tabela
            // Isso evita que o EF gere SQL referenciando colunas que possam não existir
            // (ex: lambda_function) e provoque uma exceção durante a leitura.
            var perfil = await _context.PerfisProcessamento
                .Where(p => p.Id == id)
                .Select(p => new PerfilProcessamento
                {
                    Id = p.Id,
                    ClienteId = p.ClienteId,
                    Nome = p.Nome,
                    Descricao = p.Descricao,
                    TipoArquivo = p.TipoArquivo,
                    Delimitador = p.Delimitador,
                    TemplatePcl = p.TemplatePcl,
                    // Não referenciar aqui TipoProcessamento ou LambdaFunction para evitar
                    // gerar SELECT dessas colunas caso não existam no banco.
                    DataCriacao = p.DataCriacao
                })
                .FirstOrDefaultAsync();

            return perfil;
        }
}

public class ProcessamentoLogRepository : IProcessamentoLogRepository
{
    private readonly OrquestradorDbContext _context;

    public ProcessamentoLogRepository(OrquestradorDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(ProcessamentoLog log)
    {    
        await _context.ProcessamentoLogs.AddAsync(log);
        await _context.SaveChangesAsync();
    }
}