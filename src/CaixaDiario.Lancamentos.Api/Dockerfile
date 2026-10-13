# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props global.json ./
COPY src/CaixaDiario.Lancamentos.Dominio/CaixaDiario.Lancamentos.Dominio.csproj src/CaixaDiario.Lancamentos.Dominio/
COPY src/CaixaDiario.Lancamentos.Api/CaixaDiario.Lancamentos.Api.csproj src/CaixaDiario.Lancamentos.Api/
RUN dotnet restore src/CaixaDiario.Lancamentos.Api/CaixaDiario.Lancamentos.Api.csproj

COPY src/CaixaDiario.Lancamentos.Dominio/ src/CaixaDiario.Lancamentos.Dominio/
COPY src/CaixaDiario.Lancamentos.Api/ src/CaixaDiario.Lancamentos.Api/
RUN dotnet publish src/CaixaDiario.Lancamentos.Api/CaixaDiario.Lancamentos.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

# Processo sem privilegio de root.
USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "CaixaDiario.Lancamentos.Api.dll"]
