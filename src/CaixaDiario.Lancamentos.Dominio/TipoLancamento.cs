namespace CaixaDiario.Lancamentos.Dominio;

public enum TipoLancamento
{
    Credito = 1,
    Debito = 2,
}

public static class TipoLancamentoTexto
{
    public const string Credito = "CREDITO";
    public const string Debito = "DEBITO";

    public static string ParaTexto(this TipoLancamento tipo) => tipo switch
    {
        TipoLancamento.Credito => Credito,
        TipoLancamento.Debito => Debito,
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de lancamento desconhecido."),
    };

    public static bool TentarLer(string? texto, out TipoLancamento tipo)
    {
        switch (texto)
        {
            case Credito:
                tipo = TipoLancamento.Credito;
                return true;
            case Debito:
                tipo = TipoLancamento.Debito;
                return true;
            default:
                tipo = default;
                return false;
        }
    }

    public static TipoLancamento Oposto(this TipoLancamento tipo) =>
        tipo == TipoLancamento.Credito ? TipoLancamento.Debito : TipoLancamento.Credito;
}
