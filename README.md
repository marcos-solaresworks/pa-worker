# 🔄 OrquestradorCentral - Worker de Processamento PCL
**HG10 — Implementação .NET 8 Worker Service | Gráfica Ltda**

[![.NET](https://img.shields.io/badge/.NET-8.0-purple.svg)](https://dotnet.microsoft.com/)
[![RabbitMQ](https://img.shields.io/badge/RabbitMQ-3.12+-orange.svg)](https://rabbitmq.com/)
[![AWS Lambda](https://img.shields.io/badge/AWS-Lambda-orange.svg)](https://aws.amazon.com/lambda/)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-15+-blue.svg)](https://postgresql.org/)

---

## 📋 Visão Geral

O **OrquestradorCentral** é um Worker Service .NET 8 responsável por orquestrar o processamento de lotes PCL (Printer Command Language) através da integração entre RabbitMQ e AWS Lambda. Este serviço atua como o coração do sistema de processamento, consumindo mensagens de filas e **direcionando o processamento para diferentes funções Lambda baseadas no tipo de cliente/produto**.

### 🎯 Roteamento Inteligente de Lambdas

O sistema implementa um **roteamento inteligente** que direciona cada lote para a Lambda específica baseada no perfil de processamento:

- **ClienteMalaDireta**: Processa correspondências de mala direta
- **ClienteEtiquetas**: Processa etiquetas e labels
- **ClienteCartoes**: Processa cartões de visita e similares
- **Default**: Lambda genérica para processamentos não específicos

### 🚧 Modo de Operação MVP

> **⚠️ IMPORTANTE - SIMULAÇÃO ATIVA**
> 
> Para o MVP, o OrquestradorCentral está configurado em **modo de simulação**. A invocação real das AWS Lambdas está **comentada** no código e o sistema retorna automaticamente um processamento bem-sucedido. Isso permite testar toda a arquitetura e fluxo de mensageria sem necessitar das Lambdas deployadas.
> 
> **Características da Simulação:**
> - ✅ Simula tempo de processamento realista (2 segundos)
> - ✅ Gera número aleatório de registros processados (50-500)
> - ✅ Cria caminho de output no S3 simulado
> - ✅ Atualiza status do lote corretamente no banco
> - ✅ Gera logs completos do processamento
> - 📝 Código real da Lambda preservado em comentários
> 
> **Para habilitar processamento real:**
> 1. Descomentar bloco de código em `Infrastructure/Lambda/LambdaInvoker.cs`
> 2. Comentar o bloco de simulação
> 3. Configurar ARNs das Lambdas no `appsettings.json`

### 🏗️ **Responsabilidades Core**

| Funcionalidade | Descrição | Integração |
|----------------|-----------|------------|
| 📨 **Message Consumer** | Consome mensagens da fila `lote.processamento` | RabbitMQ |
| ⚡ **Lambda Orchestrator** | Invoca AWS Lambda via ARN para processamento | AWS SDK |
| 📊 **Status Management** | Atualiza status dos lotes no PostgreSQL | EF Core |
| 🔄 **Retry Logic** | Implementa retry com backoff exponencial | Polly |
| 📝 **Logging & Monitoring** | Logs estruturados e métricas | Serilog + CloudWatch |
| 🚨 **Error Handling** | Dead Letter Queue e alertas | RabbitMQ DLQ |

### 🎨 **Arquitetura do Worker**

```mermaid
graph TD
    A[📨 RabbitMQ] --> B[🔄 OrquestradorCentral]
    B --> C[⚡ AWS Lambda]
    C --> D[📦 S3 Output]
    B --> E[💾 PostgreSQL]
    B --> F[📊 CloudWatch]
    B --> G[🚨 DLQ]
```

---

## ⚙️ Stack Tecnológico

### 🛠️ **Tecnologias Principais**

| Componente | Versão | Descrição | Justificativa |
|------------|--------|-----------|---------------|
| 🔷 **.NET 8 Worker Service** | `8.0+` | Background service principal | Long-running, performance |
| 🐰 **RabbitMQ.Client** | `Latest` | Consumer AMQP | Reliability, acknowledgments |
| ⚡ **AWS Lambda SDK** | `Latest` | Invocação de funções | Processamento serverless |
| 🗄️ **Entity Framework Core** | `8.0+` | ORM para PostgreSQL | Status tracking, logs |
| 🔄 **Polly** | `Latest` | Resilience patterns | Retry, circuit breaker |
| 📊 **Serilog** | `Latest` | Logging estruturado | Observabilidade avançada |
| ⚡ **Microsoft.Extensions.Hosting** | `8.0+` | Host genérico | Lifecycle management |

### 🔧 **Dependências do Projeto**

```xml
<PackageReference Include="Microsoft.Extensions.Hosting" Version="8.0.1" />
<PackageReference Include="RabbitMQ.Client" Version="6.8.1" />
<PackageReference Include="AWSSDK.Lambda" Version="3.7.400" />
<PackageReference Include="Microsoft.EntityFrameworkCore" Version="8.0.0" />
<PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="8.0.0" />
<PackageReference Include="Polly" Version="8.2.0" />
<PackageReference Include="Serilog" Version="3.1.1" />
<PackageReference Include="Serilog.Sinks.Console" Version="5.0.1" />
<PackageReference Include="Serilog.Extensions.Hosting" Version="8.0.0" />
```

---

## 🏗️ Arquitetura da Solução

### 📁 **Estrutura de Pastas**

O Worker segue padrões de **Clean Architecture** adaptados para Background Services:

```
src/
 ├── 🎯 Application/          # Orquestração e casos de uso
 │   ├── Services/            # Serviços de domínio
 │   ├── Models/              # DTOs e mensagens
 │   ├── Interfaces/          # Contratos de serviço
 │   └── Handlers/            # Processadores de mensagem
 │
 ├── 🏛️ Domain/               # Entidades e regras
 │   ├── Entities/            # Entidades compartilhadas
 │   ├── Enums/               # Status e tipos
 │   └── Exceptions/          # Exceções de domínio
 │
 ├── 🔧 Infrastructure/       # Implementações externas
 │   ├── Messaging/           # RabbitMQ consumer
 │   ├── Lambda/              # AWS Lambda client
 │   ├── Data/                # EF Core context
 │   └── Logging/             # Serilog configuration
 │
 └── 🚀 Worker/               # Entry point
     ├── Program.cs           # Host configuration
     ├── Worker.cs            # Background service
     └── appsettings.json     # Configurações
```

### 🔄 **Fluxo de Processamento**

```mermaid
sequenceDiagram
    participant RMQ as 📨 RabbitMQ
    participant Worker as 🔄 OrquestradorCentral
    participant DB as 💾 PostgreSQL
    participant Lambda as ⚡ AWS Lambda
    participant S3 as 📦 S3

    RMQ->>Worker: Mensagem lote.processamento
    Worker->>DB: Atualizar status "Processando"
    Worker->>Lambda: Invocar ARN processamento
    Lambda->>S3: Baixar arquivo origem
    Lambda->>Lambda: Gerar PCL
    Lambda->>S3: Salvar PCL gerado
    Lambda-->>Worker: Resultado processamento
    Worker->>DB: Atualizar status "Concluído"
    Worker->>RMQ: ACK mensagem
```

---

## 📡 Integração com Componentes

### 🔗 **API Central → Worker**

#### **Mensagem RabbitMQ:**
```json
{
  "loteId": 123,
  "clienteId": 1,
  "nomeArquivo": "mala_direta_001.csv",
  "caminhoS3": "lotes/guid-único/mala_direta_001.csv",
  "perfilId": 2,
  "timestamp": "2025-11-15T10:30:00Z"
}
```

#### **Configuração da Fila:**
- **Queue**: `lote.processamento`
- **Exchange**: `graficaltda.exchange`
- **Routing Key**: `lote.processamento`
- **Durability**: `true`
- **Dead Letter Queue**: `lote.processamento.dlq`

### ⚡ **Worker → AWS Lambda**

#### **Payload de Invocação:**
```json
{
  "loteId": 123,
  "clienteId": 1,
  "s3Bucket": "api-central-storage",
  "s3Key": "lotes/guid-único/mala_direta_001.csv",
  "perfilProcessamento": {
    "tipoArquivo": "CSV",
    "delimitador": ",",
    "templatePcl": "template_offset.pcl"
  },
  "outputPath": "pcl-output/guid-único/"
}
```

#### **ARN Configuration:**
```json
{
  "AWS": {
    "Lambda": {
      "ProcessamentoArn": "arn:aws:lambda:us-east-1:123456789012:function:ProcessamentoPCL",
      "TimeoutSeconds": 300,
      "MaxRetries": 3
    }
  }
}
```

### 💾 **Integração com PostgreSQL**

#### **Tabelas de Interação:**
- `lotes_processamento` - Atualização de status
- `processamento_logs` - Logs detalhados
- `lote_registros` - Contagem de registros processados

#### **Status Flow:**
1. **"Recebido"** → **"Processando"** (Worker inicia)
2. **"Processando"** → **"Concluido"** (Lambda sucesso)
3. **"Processando"** → **"Erro"** (Falha no processamento)

---

## 🔄 Padrões de Resilência

### 🔁 **Retry Policy**

```csharp
// Configuração Polly para Lambda
var retryPolicy = Policy
    .Handle<AmazonLambdaException>()
    .WaitAndRetryAsync(
        retryCount: 3,
        sleepDurationProvider: retryAttempt => 
            TimeSpan.FromSeconds(Math.Pow(2, retryAttempt)), // Exponential backoff
        onRetry: (outcome, timespan, retryCount, context) =>
        {
            _logger.LogWarning("Retry {RetryCount} para lote {LoteId} em {Delay}s", 
                retryCount, context["LoteId"], timespan.TotalSeconds);
        });
```

### 🚨 **Dead Letter Queue**

- **Queue DLQ**: `lote.processamento.dlq`
- **Max Delivery Count**: 3
- **TTL**: 24 horas
- **Reprocessing**: Manual via dashboard

### ⚡ **Circuit Breaker**

```csharp
// Proteção para AWS Lambda
var circuitBreakerPolicy = Policy
    .Handle<AmazonLambdaException>()
    .CircuitBreakerAsync(
        handledEventsAllowedBeforeBreaking: 5,
        durationOfBreak: TimeSpan.FromMinutes(1));
```

---

## 📊 Observabilidade e Monitoramento

### 📝 **Structured Logging**

```csharp
// Log com contexto enriquecido
_logger.LogInformation("Iniciando processamento do lote {LoteId} " +
    "para cliente {ClienteId} com perfil {PerfilId}", 
    lote.Id, lote.ClienteId, lote.PerfilId);
```

### 📈 **Métricas Customizadas**

| Métrica | Tipo | Descrição |
|---------|------|-----------|
| `lotes_processados_total` | Counter | Total de lotes processados |
| `tempo_processamento_seconds` | Histogram | Tempo de processamento |
| `erros_lambda_total` | Counter | Falhas na invocação Lambda |
| `mensagens_dlq_total` | Counter | Mensagens enviadas para DLQ |

### 🔍 **Health Checks**

- **RabbitMQ Connection**: Verifica conectividade
- **PostgreSQL**: Testa query simples
- **AWS Lambda**: Valida permissões IAM
- **S3 Access**: Verifica acesso aos buckets

---

## ⚙️ Configuração e Deployment

### 🔧 **appsettings.json**

```json
{
  "RabbitMQ": {
    "HostName": "localhost",
    "UserName": "guest", 
    "Password": "guest",
    "Port": 5672,
    "QueueName": "lote.processamento",
    "ExchangeName": "graficaltda.exchange",
    "DeadLetterQueue": "lote.processamento.dlq"
  },
  "AWS": {
    "AccessKey": "AKIAIOSFODNN7EXAMPLE",
    "SecretKey": "wJalrXUtnFEMI/K7MDENG/bPxRfiCYEXAMPLEKEY",
    "Region": "us-east-1",
    "Lambda": {
      "ProcessamentoArn": "arn:aws:lambda:us-east-1:123456789012:function:ProcessamentoPCL",
      "TimeoutSeconds": 300
    }
  },
  "ConnectionStrings": {
    "DefaultConnection": "Host=localhost;Database=bpdapi_db;Username=postgres;Password=EasdPG;Port=5435"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "OrquestradorCentral": "Debug",
      "RabbitMQ.Client": "Warning"
    }
  }
}
```

### 🐳 **Docker Support**

```dockerfile
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS base
WORKDIR /app

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["OrquestradorCentral.csproj", "./"]
RUN dotnet restore "OrquestradorCentral.csproj"
COPY . .
RUN dotnet build "OrquestradorCentral.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "OrquestradorCentral.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "OrquestradorCentral.dll"]
```

---

## 🚀 Roadmap de Desenvolvimento

### 📋 **Fase 1: Core Implementation**
- ✅ Base project structure
- ⏳ RabbitMQ consumer service
- ⏳ AWS Lambda integration
- ⏳ PostgreSQL status updates
- ⏳ Basic error handling

### 📋 **Fase 2: Resilience & Monitoring**
- ⏳ Polly retry policies
- ⏳ Dead Letter Queue handling
- ⏳ Health checks implementation
- ⏳ Structured logging
- ⏳ Metrics collection

### 📋 **Fase 3: Production Ready**
- ⏳ Docker containerization
- ⏳ Configuration management
- ⏳ Security hardening
- ⏳ Performance optimization
- ⏳ Documentation completion

---

## 🎯 Critérios de Aceite MVP

| Critério | Status | Descrição |
|----------|--------|-----------|
| 📨 **Message Consumer** | ⏳ | Consome mensagens do RabbitMQ |
| ⚡ **Lambda Invocation** | ⏳ | Invoca AWS Lambda via ARN |
| 💾 **Status Updates** | ⏳ | Atualiza PostgreSQL com status |
| 🔄 **Retry Logic** | ⏳ | Implementa retry com backoff |
| 📝 **Logging** | ⏳ | Logs estruturados completos |
| 🚨 **Error Handling** | ⏳ | DLQ e tratamento de erros |
| 📊 **Monitoring** | ⏳ | Health checks funcionais |
| 🐳 **Containerization** | ⏳ | Docker image funcional |

---

## 👥 Personas Atendidas

### 👩‍💼 **Ana Ribeiro (Operadora)**
- Monitoramento em tempo real do processamento
- Notificações de conclusão/erro via logs

### 👨‍🔧 **Carlos Mendes (Impressão)**  
- Disponibilidade imediata dos PCLs processados
- Logs detalhados para troubleshooting

### 👨‍💼 **Marcos Oliveira (Gestor)**
- Métricas de performance do Worker
- Dashboards de SLA e throughput

---

> 📝 **Documentação viva:** Este README será atualizado conforme a evolução do Worker, mantendo sempre a documentação sincronizada com o código implementado.

---

## 🔗 Links Relacionados

- [📡 API Central](../HG09/readme.md)
- [🏗️ Arquitetura C4](../HG06/entrega%20hg06.md)
- [💾 Modelo de Dados](../HG08/entrega%20hg08.md)
- [🔧 Componentes](../HG07/entrega%20hg07.md)