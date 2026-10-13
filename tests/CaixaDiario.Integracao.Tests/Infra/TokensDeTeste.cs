using System.Security.Cryptography;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CaixaDiario.Integracao.Tests.Infra;

/// <summary>
/// Emite tokens JWT assinados por uma chave de teste, no lugar do servidor de
/// autorizacao. As APIs validam emissor, audiencia, validade, assinatura e
/// escopo exatamente como no compose (ADR-0004).
/// </summary>
public static class TokensDeTeste
{
    public const string Emissor = "http://emissor-de-teste/realms/caixadiario";
    public const string Audiencia = "caixadiario-api";
    public const string TodosOsEscopos = "lancamentos.escrita lancamentos.leitura consolidado.leitura";

    public static readonly RsaSecurityKey Chave = new(RSA.Create(2048)) { KeyId = "chave-de-teste" };

    public static string Emitir(
        string escopos, string emissor = Emissor, string audiencia = Audiencia, SecurityKey? chave = null) =>
        new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = emissor,
            Audience = audiencia,
            Expires = DateTime.UtcNow.AddMinutes(10),
            Claims = new Dictionary<string, object> { ["scope"] = escopos, ["azp"] = "cliente-de-teste" },
            SigningCredentials = new SigningCredentials(chave ?? Chave, SecurityAlgorithms.RsaSha256),
        });
}
