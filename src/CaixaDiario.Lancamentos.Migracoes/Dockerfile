# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props global.json ./
COPY src/CaixaDiario.Lancamentos.Migracoes/CaixaDiario.Lancamentos.Migracoes.csproj src/CaixaDiario.Lancamentos.Migracoes/
RUN dotnet restore src/CaixaDiario.Lancamentos.Migracoes/CaixaDiario.Lancamentos.Migracoes.csproj

COPY db/lancamentos/ db/lancamentos/
COPY src/CaixaDiario.Lancamentos.Migracoes/ src/CaixaDiario.Lancamentos.Migracoes/
RUN dotnet publish src/CaixaDiario.Lancamentos.Migracoes/CaixaDiario.Lancamentos.Migracoes.csproj -c Release -o /app --no-restore

# Console que aplica e termina: imagem de runtime sem ASP.NET.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID

ENTRYPOINT ["dotnet", "CaixaDiario.Lancamentos.Migracoes.dll"]
