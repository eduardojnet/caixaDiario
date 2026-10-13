using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace CaixaDiario.Consolidado.Api;

public sealed class OpcoesAutenticacao
{
    public const string Secao = "Autenticacao";

    /// <summary>Documento OpenID do servidor de autorizacao (chaves de assinatura).</summary>
    public string EnderecoDeMetadados { get; set; } = "http://localhost:8180/realms/caixadiario/.well-known/openid-configuration";

    public string Emissor { get; set; } = "http://localhost:8180/realms/caixadiario";

    public string Audiencia { get; set; } = "caixadiario-api";

    /// <summary>Metadados so por HTTPS. Desligado apenas no ambiente local, declarado.</summary>
    public bool ExigirHttps { get; set; } = true;
}

/// <summary>
/// OAuth2 client credentials (ADR-0004): a API so valida o token JWT e o
/// escopo; quem emite e o servidor de autorizacao.
/// </summary>
public static class Autenticacao
{
    public static IServiceCollection AdicionarAutenticacao(
        this IServiceCollection servicos, IConfiguration configuracao, params string[] escopos)
    {
        var opcoes = configuracao.GetSection(OpcoesAutenticacao.Secao).Get<OpcoesAutenticacao>() ?? new OpcoesAutenticacao();

        servicos.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(o =>
            {
                o.MetadataAddress = opcoes.EnderecoDeMetadados;
                o.RequireHttpsMetadata = opcoes.ExigirHttps;
                // Mantem "scope" com o nome do token, sem traducao para URI.
                o.MapInboundClaims = false;
                o.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = opcoes.Emissor,
                    ValidAudience = opcoes.Audiencia,
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                };
            });

        servicos.AddAuthorization(o =>
        {
            foreach (var escopo in escopos)
            {
                o.AddPolicy(escopo, p => p.RequireAuthenticatedUser().RequireAssertion(contexto =>
                    contexto.User.FindAll("scope")
                        .SelectMany(c => c.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                        .Contains(escopo, StringComparer.Ordinal)));
            }
        });

        return servicos;
    }
}
