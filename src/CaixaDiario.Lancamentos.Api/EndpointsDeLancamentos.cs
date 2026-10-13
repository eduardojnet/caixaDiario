using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CaixaDiario.Lancamentos.Dominio;
using Microsoft.Extensions.Options;

namespace CaixaDiario.Lancamentos.Api;

public sealed record RequisicaoLancamento(
    string? Tipo,
    decimal Valor,
    DateOnly DataDoFato,
    string? Descricao,
    Guid? EstornoDe);

public static class EndpointsDeLancamentos
{
    public const string CabecalhoIdempotencia = "Idempotency-Key";
    public const string CabecalhoRepeticao = "Idempotency-Replayed";
    private const int TamanhoMaximoChave = 100;

    public static IEndpointRouteBuilder MapEndpointsDeLancamentos(this IEndpointRouteBuilder rotas)
    {
        var grupo = rotas.MapGroup("/v1/caixas/{caixaId}/lancamentos");
        grupo.MapPost("/", RegistrarAsync);
        grupo.MapGet("/", ListarAsync);
        return rotas;
    }

    private static async Task<IResult> RegistrarAsync(
        string caixaId,
        RequisicaoLancamento requisicao,
        HttpContext http,
        RepositorioLancamentos repositorio,
        TimeProvider relogio,
        IOptions<OpcoesCaixa> opcoesCaixa,
        Telemetria telemetria,
        CancellationToken cancellationToken)
    {
        var chave = http.Request.Headers[CabecalhoIdempotencia].ToString();
        if (string.IsNullOrWhiteSpace(chave) || chave.Length > TamanhoMaximoChave)
        {
            return Problema(StatusCodes.Status400BadRequest,
                $"Cabecalho {CabecalhoIdempotencia} obrigatorio, com 1 a {TamanhoMaximoChave} caracteres.");
        }

        if (!TipoLancamentoTexto.TentarLer(requisicao.Tipo, out var tipo))
        {
            return Problema(StatusCodes.Status400BadRequest, "tipo deve ser CREDITO ou DEBITO.");
        }

        var hoje = Hoje(relogio, opcoesCaixa.Value);
        var criacao = Lancamento.Criar(
            caixaId, tipo, requisicao.Valor, requisicao.DataDoFato, requisicao.Descricao, requisicao.EstornoDe, hoje);
        if (!criacao.Sucesso)
        {
            return Results.ValidationProblem(new Dictionary<string, string[]> { ["lancamento"] = [.. criacao.Erros] });
        }

        var lancamento = criacao.Lancamento!;
        var resultado = await repositorio.RegistrarAsync(lancamento, chave, Impressao(lancamento), cancellationToken)
            .ConfigureAwait(false);

        switch (resultado.Situacao)
        {
            case SituacaoRegistro.Criado:
                telemetria.Registrados.Add(1, new KeyValuePair<string, object?>("tipo", resultado.Lancamento!.Tipo));
                return Results.Created(
                    $"/v1/caixas/{Uri.EscapeDataString(caixaId)}/lancamentos?data={lancamento.DataDoFato:yyyy-MM-dd}",
                    resultado.Lancamento);
            case SituacaoRegistro.Repetido:
                http.Response.Headers[CabecalhoRepeticao] = "true";
                return Results.Ok(resultado.Lancamento);
            case SituacaoRegistro.ChaveComOutroConteudo:
                return Problema(StatusCodes.Status422UnprocessableEntity,
                    "Chave de idempotencia ja usada com outro conteudo neste caixa.");
            case SituacaoRegistro.EstornoInvalido:
                return Problema(StatusCodes.Status422UnprocessableEntity,
                    "Estorno deve referenciar um lancamento existente do mesmo caixa, com tipo oposto e mesmo valor.");
            case SituacaoRegistro.JaEstornado:
                return Problema(StatusCodes.Status422UnprocessableEntity, "Lancamento original ja foi estornado.");
            default:
                throw new InvalidOperationException($"Situacao de registro nao tratada: {resultado.Situacao}.");
        }
    }

    private static async Task<IResult> ListarAsync(
        string caixaId, DateOnly data, RepositorioLancamentos repositorio, CancellationToken cancellationToken)
    {
        var lancamentos = await repositorio.ListarDoDiaAsync(caixaId, data, cancellationToken).ConfigureAwait(false);
        return Results.Ok(lancamentos);
    }

    /// <summary>
    /// Impressao digital do conteudo: o mesmo conteudo com a mesma chave e
    /// repeticao; conteudo diferente com a mesma chave e conflito.
    /// </summary>
    public static string Impressao(Lancamento lancamento)
    {
        var canonico = string.Join('|',
            lancamento.Tipo.ParaTexto(),
            lancamento.Valor.ToString("F2", CultureInfo.InvariantCulture),
            lancamento.DataDoFato.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            lancamento.Descricao,
            lancamento.EstornoDe?.ToString() ?? string.Empty);
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonico)));
    }

    private static DateOnly Hoje(TimeProvider relogio, OpcoesCaixa opcoes)
    {
        TimeZoneInfo fuso;
        try
        {
            fuso = TimeZoneInfo.FindSystemTimeZoneById(opcoes.FusoHorario);
        }
        catch (TimeZoneNotFoundException)
        {
            fuso = TimeZoneInfo.Utc;
        }

        return DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(relogio.GetUtcNow(), fuso).DateTime);
    }

    private static IResult Problema(int status, string detalhe) =>
        Results.Problem(detail: detalhe, statusCode: status);
}
