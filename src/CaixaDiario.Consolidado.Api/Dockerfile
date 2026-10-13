# syntax=docker/dockerfile:1

FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /source

# So o proprio projeto: a imagem do Consolidado nao contem codigo de Lancamentos.
COPY Directory.Build.props global.json ./
COPY src/CaixaDiario.Consolidado.Api/CaixaDiario.Consolidado.Api.csproj src/CaixaDiario.Consolidado.Api/
RUN dotnet restore src/CaixaDiario.Consolidado.Api/CaixaDiario.Consolidado.Api.csproj

COPY src/CaixaDiario.Consolidado.Api/ src/CaixaDiario.Consolidado.Api/
RUN dotnet publish src/CaixaDiario.Consolidado.Api/CaixaDiario.Consolidado.Api.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app
COPY --from=build /app ./

USER $APP_UID

EXPOSE 8080
ENTRYPOINT ["dotnet", "CaixaDiario.Consolidado.Api.dll"]
