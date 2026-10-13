namespace CaixaDiario.Lancamentos.Dominio;

/// <summary>
/// Registro de um movimento de dinheiro no caixa. Imutavel depois de criado:
/// correcao so por estorno (RF-05), e o banco recusa UPDATE e DELETE (RNF-06).
/// </summary>
public sealed record Lancamento
{
    public const int TamanhoMaximoCaixaId = 64;
    public const int TamanhoMaximoDescricao = 200;

    private Lancamento(
        Guid id, string caixaId, TipoLancamento tipo, decimal valor,
        DateOnly dataDoFato, string descricao, Guid? estornoDe)
    {
        Id = id;
        CaixaId = caixaId;
        Tipo = tipo;
        Valor = valor;
        DataDoFato = dataDoFato;
        Descricao = descricao;
        EstornoDe = estornoDe;
    }

    public Guid Id { get; }

    public string CaixaId { get; }

    public TipoLancamento Tipo { get; }

    /// <summary>Sempre positivo, em reais, duas casas. O sinal vem do tipo.</summary>
    public decimal Valor { get; }

    /// <summary>Dia em que o dinheiro entrou ou saiu; define o dia do saldo.</summary>
    public DateOnly DataDoFato { get; }

    public string Descricao { get; }

    /// <summary>Lancamento que este estorna, quando for estorno.</summary>
    public Guid? EstornoDe { get; }

    /// <summary>
    /// Valida e cria um lancamento. <paramref name="hoje"/> e a data corrente no
    /// fuso do caixa, recebida de fora para o dominio nao depender do relogio.
    /// </summary>
    public static ResultadoCriacao Criar(
        string? caixaId, TipoLancamento tipo, decimal valor, DateOnly dataDoFato,
        string? descricao, Guid? estornoDe, DateOnly hoje)
    {
        var erros = new List<string>();

        if (string.IsNullOrWhiteSpace(caixaId) || caixaId.Length > TamanhoMaximoCaixaId)
        {
            erros.Add($"caixaId deve ter de 1 a {TamanhoMaximoCaixaId} caracteres.");
        }

        if (!Enum.IsDefined(tipo))
        {
            erros.Add("tipo deve ser CREDITO ou DEBITO.");
        }

        if (valor <= 0)
        {
            erros.Add("valor deve ser positivo.");
        }
        else if (decimal.Round(valor, 2) != valor)
        {
            erros.Add("valor deve ter no maximo duas casas decimais.");
        }
        else if (valor > 999_999_999_999.99m)
        {
            erros.Add("valor excede o maximo aceito.");
        }

        // QA-03: retroativo permitido; data futura recusada.
        if (dataDoFato > hoje)
        {
            erros.Add("dataDoFato nao pode ser futura.");
        }

        var descricaoNormalizada = descricao?.Trim() ?? string.Empty;
        if (descricaoNormalizada.Length > TamanhoMaximoDescricao)
        {
            erros.Add($"descricao deve ter no maximo {TamanhoMaximoDescricao} caracteres.");
        }

        if (erros.Count > 0)
        {
            return ResultadoCriacao.Invalido(erros);
        }

        return ResultadoCriacao.Valido(new Lancamento(
            Guid.CreateVersion7(), caixaId!, tipo, valor, dataDoFato, descricaoNormalizada, estornoDe));
    }

    /// <summary>
    /// Regra do estorno: tipo oposto e mesmo valor do original, no mesmo caixa.
    /// </summary>
    public bool EstornaCorretamente(string caixaIdOriginal, TipoLancamento tipoOriginal, decimal valorOriginal) =>
        EstornoDe is not null
        && string.Equals(CaixaId, caixaIdOriginal, StringComparison.Ordinal)
        && Tipo == tipoOriginal.Oposto()
        && Valor == valorOriginal;
}

public sealed class ResultadoCriacao
{
    private ResultadoCriacao(Lancamento? lancamento, IReadOnlyList<string> erros)
    {
        Lancamento = lancamento;
        Erros = erros;
    }

    public Lancamento? Lancamento { get; }

    public IReadOnlyList<string> Erros { get; }

    public bool Sucesso => Lancamento is not null;

    internal static ResultadoCriacao Valido(Lancamento lancamento) => new(lancamento, []);

    internal static ResultadoCriacao Invalido(IReadOnlyList<string> erros) => new(null, erros);
}
