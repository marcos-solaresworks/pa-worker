using Amazon.Lambda;
using Microsoft.EntityFrameworkCore;
using OrquestradorCentral;
using OrquestradorCentral.Application.Interfaces;
using OrquestradorCentral.Application.Services;
using OrquestradorCentral.Infrastructure.Data;
using OrquestradorCentral.Infrastructure.Lambda;
using OrquestradorCentral.Infrastructure.Messaging;
using OrquestradorCentral.Infrastructure.Repositories;
using Serilog;

// Configurar Serilog para Console e Arquivo
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .MinimumLevel.Override("Microsoft", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("System", Serilog.Events.LogEventLevel.Warning)
    .MinimumLevel.Override("Microsoft.EntityFrameworkCore", Serilog.Events.LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .WriteTo.Console(
        outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .WriteTo.File(
        "logs/orquestrador-.log",
        rollingInterval: RollingInterval.Day,
        outputTemplate: "[{Timestamp:yyyy-MM-dd HH:mm:ss.fff} {Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

try
{
    Log.Information("🚀 Iniciando OrquestradorCentral Worker Service");

    var builder = Host.CreateApplicationBuilder(args);

    // Configurar Serilog
    builder.Services.AddSerilog(Log.Logger);

    // Configurar Entity Framework
    builder.Services.AddDbContext<OrquestradorDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    // Configurar AWS Lambda Client
    builder.Services.AddSingleton<IAmazonLambda>(provider =>
    {
        var config = provider.GetRequiredService<IConfiguration>();
        return new AmazonLambdaClient(
            config["AWS:AccessKey"],
            config["AWS:SecretKey"],
            Amazon.RegionEndpoint.GetBySystemName(config["AWS:Region"] ?? "us-east-1")
        );
    });

    // Registrar Services
    builder.Services.AddScoped<ILoteRepository, LoteRepository>();
    builder.Services.AddScoped<IPerfilProcessamentoRepository, PerfilProcessamentoRepository>();
    builder.Services.AddScoped<IProcessamentoLogRepository, ProcessamentoLogRepository>();
    builder.Services.AddScoped<IClienteRepository, ClienteRepository>();
    builder.Services.AddScoped<IArquivoPclRepository, ArquivoPclRepository>();
    
    builder.Services.AddScoped<IProcessamentoService, ProcessamentoService>();
    builder.Services.AddScoped<ILambdaInvoker, LambdaInvoker>();
    builder.Services.AddScoped<ILambdaRouter, LambdaRouter>();
    
    // Registrar Singleton services
    builder.Services.AddSingleton<RabbitMQConsumer>();
    builder.Services.AddSingleton<IMessagePublisher, RabbitMQPublisher>();

    // Registrar Worker
    builder.Services.AddHostedService<Worker>();

    var host = builder.Build();

    // Aplicar migrações automaticamente
    using (var scope = host.Services.CreateScope())
    {
        var context = scope.ServiceProvider.GetRequiredService<OrquestradorDbContext>();
        try
        {
            context.Database.Migrate();
            Log.Information("Migrações do banco de dados aplicadas com sucesso");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Erro ao aplicar migrações do banco de dados");
            throw;
        }
    }

    host.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "OrquestradorCentral Worker Service falhou ao iniciar");
    throw;
}
finally
{
    Log.CloseAndFlush();
}
