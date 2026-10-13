# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

COPY Directory.Build.props global.json ./
COPY src/CaixaDiario.Consolidado.Migracoes/CaixaDiario.Consolidado.Migracoes.csproj src/CaixaDiario.Consolidado.Migracoes/
RUN dotnet restore src/CaixaDiario.Consolidado.Migracoes/CaixaDiario.Consolidado.Migracoes.csproj

COPY db/consolidado/ db/consolidado/
COPY src/CaixaDiario.Consolidado.Migracoes/ src/CaixaDiario.Consolidado.Migracoes/
RUN dotnet publish src/CaixaDiario.Consolidado.Migracoes/CaixaDiario.Consolidado.Migracoes.csproj -c Release -o /app --no-restore

# Console que aplica e termina: imagem de runtime sem ASP.NET.
FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID

ENTRYPOINT ["dotnet", "CaixaDiario.Consolidado.Migracoes.dll"]
