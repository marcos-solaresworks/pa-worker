# Use SDK para build
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copiar arquivos de projeto
COPY ["OrquestradorCentral.csproj", "./"]
RUN dotnet restore "OrquestradorCentral.csproj"

# Copiar código fonte e build
COPY . .
RUN dotnet build "OrquestradorCentral.csproj" -c Release -o /app/build

# Publish
RUN dotnet publish "OrquestradorCentral.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Runtime image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app

# Instalar timezone data para PostgreSQL
RUN apt-get update && apt-get install -y tzdata && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

# Configurar variáveis de ambiente
ENV ASPNETCORE_ENVIRONMENT=Production
ENV TZ=America/Sao_Paulo

# Health check
HEALTHCHECK --interval=30s --timeout=10s --start-period=5s --retries=3 \
  CMD ps aux | grep -v grep | grep -q dotnet || exit 1

ENTRYPOINT ["dotnet", "OrquestradorCentral.dll"]