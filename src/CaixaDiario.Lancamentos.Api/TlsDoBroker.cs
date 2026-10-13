using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using RabbitMQ.Client;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// TLS na conexao com o broker (ADR-0004). O certificado do broker e validado
/// contra a autoridade certificadora local informada em CaminhoCa, e nao contra
/// o repositorio do sistema: a CA e gerada no primeiro "docker compose up".
/// </summary>
internal static class TlsDoBroker
{
    public static SslOption Criar(OpcoesBroker opcoes)
    {
        if (!opcoes.Tls)
        {
            return new SslOption { Enabled = false };
        }

        var ca = X509CertificateLoader.LoadCertificateFromFile(opcoes.CaminhoCa);
        return new SslOption
        {
            Enabled = true,
            ServerName = opcoes.Host,
            CertificateValidationCallback = (_, certificado, _, erros) => Validar(certificado, erros, ca),
        };
    }

    private static bool Validar(X509Certificate? certificado, SslPolicyErrors erros, X509Certificate2 ca)
    {
        // Nome divergente ou certificado ausente reprovam; so o erro de cadeia
        // (CA desconhecida pelo sistema) e reavaliado contra a CA local.
        if (certificado is null || (erros & ~SslPolicyErrors.RemoteCertificateChainErrors) != SslPolicyErrors.None)
        {
            return false;
        }

        using var cadeia = new X509Chain();
        cadeia.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
        cadeia.ChainPolicy.CustomTrustStore.Add(ca);
        cadeia.ChainPolicy.RevocationMode = X509RevocationMode.NoCheck;
        using var doServidor = new X509Certificate2(certificado);
        return cadeia.Build(doServidor);
    }
}
