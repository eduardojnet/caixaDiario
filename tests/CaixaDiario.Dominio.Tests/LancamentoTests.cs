using CaixaDiario.Lancamentos.Dominio;

namespace CaixaDiario.Dominio.Tests;

public class LancamentoTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 12);

    private static ResultadoCriacao Criar(
        decimal valor = 10m, DateOnly? data = null, TipoLancamento tipo = TipoLancamento.Credito,
        string? caixaId = "loja-centro", string? descricao = "venda", Guid? estornoDe = null) =>
        Lancamento.Criar(caixaId, tipo, valor, data ?? Hoje, descricao, estornoDe, Hoje);

    [Fact]
    public void Lancamento_valido_e_criado_com_os_dados_informados()
    {
        var resultado = Criar(valor: 150.75m, descricao: "  venda balcao  ");

        Assert.True(resultado.Sucesso);
        var lancamento = resultado.Lancamento!;
        Assert.NotEqual(Guid.Empty, lancamento.Id);
        Assert.Equal("loja-centro", lancamento.CaixaId);
        Assert.Equal(TipoLancamento.Credito, lancamento.Tipo);
        Assert.Equal(150.75m, lancamento.Valor);
        Assert.Equal(Hoje, lancamento.DataDoFato);
        Assert.Equal("venda balcao", lancamento.Descricao);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-0.01)]
    [InlineData(-100)]
    public void Valor_zero_ou_negativo_e_recusado(double valor)
    {
        var resultado = Criar(valor: (decimal)valor);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("positivo", StringComparison.Ordinal));
    }

    [Fact]
    public void Valor_com_mais_de_duas_casas_e_recusado()
    {
        var resultado = Criar(valor: 10.005m);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("duas casas", StringComparison.Ordinal));
    }

    [Fact]
    public void Data_futura_e_recusada()
    {
        var resultado = Criar(data: Hoje.AddDays(1));

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("futura", StringComparison.Ordinal));
    }

    [Fact]
    public void Data_retroativa_e_aceita()
    {
        Assert.True(Criar(data: Hoje.AddDays(-30)).Sucesso);
    }

    [Fact]
    public void Tipo_fora_do_enum_e_recusado()
    {
        var resultado = Criar(tipo: (TipoLancamento)99);

        Assert.False(resultado.Sucesso);
        Assert.Contains(resultado.Erros, e => e.Contains("CREDITO ou DEBITO", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Caixa_vazio_e_recusado(string? caixaId)
    {
        Assert.False(Criar(caixaId: caixaId).Sucesso);
    }

    [Fact]
    public void Descricao_acima_do_limite_e_recusada()
    {
        Assert.False(Criar(descricao: new string('x', Lancamento.TamanhoMaximoDescricao + 1)).Sucesso);
    }

    [Fact]
    public void Caixa_no_limite_e_aceito_e_um_acima_e_recusado()
    {
        Assert.True(Criar(caixaId: new string('c', Lancamento.TamanhoMaximoCaixaId)).Sucesso);

        var acima = Criar(caixaId: new string('c', Lancamento.TamanhoMaximoCaixaId + 1));
        Assert.False(acima.Sucesso);
        Assert.Contains(acima.Erros, e => e.Contains("caixaId deve ter de 1 a 64", StringComparison.Ordinal));
    }

    [Fact]
    public void Descricao_no_limite_e_aceita_e_um_acima_e_recusada_com_mensagem()
    {
        Assert.True(Criar(descricao: new string('x', Lancamento.TamanhoMaximoDescricao)).Sucesso);

        var acima = Criar(descricao: new string('x', Lancamento.TamanhoMaximoDescricao + 1));
        Assert.Contains(acima.Erros, e => e.Contains("descricao deve ter no maximo 200", StringComparison.Ordinal));
    }

    [Fact]
    public void Descricao_ausente_vira_texto_vazio()
    {
        Assert.Equal(string.Empty, Criar(descricao: null).Lancamento!.Descricao);
    }

    [Fact]
    public void Valor_maximo_da_coluna_e_aceito_e_um_centavo_acima_e_recusado()
    {
        Assert.True(Criar(valor: 999_999_999_999.99m).Sucesso);

        var acima = Criar(valor: 1_000_000_000_000.00m);
        Assert.False(acima.Sucesso);
        Assert.Contains(acima.Erros, e => e.Contains("excede o maximo", StringComparison.Ordinal));
    }

    [Fact]
    public void Tipo_desconhecido_nao_tem_texto_de_contrato()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ((TipoLancamento)99).ParaTexto());
    }

    [Fact]
    public void Todos_os_erros_sao_reportados_de_uma_vez()
    {
        var resultado = Criar(valor: -1m, data: Hoje.AddDays(1), caixaId: "");

        Assert.Equal(3, resultado.Erros.Count);
    }

    [Theory]
    [InlineData("CREDITO", TipoLancamento.Credito)]
    [InlineData("DEBITO", TipoLancamento.Debito)]
    public void Tipo_e_lido_do_texto_do_contrato(string texto, TipoLancamento esperado)
    {
        Assert.True(TipoLancamentoTexto.TentarLer(texto, out var tipo));
        Assert.Equal(esperado, tipo);
        Assert.Equal(texto, tipo.ParaTexto());
    }

    [Theory]
    [InlineData("credito")]
    [InlineData("PIX")]
    [InlineData(null)]
    public void Texto_de_tipo_desconhecido_nao_e_lido(string? texto)
    {
        Assert.False(TipoLancamentoTexto.TentarLer(texto, out _));
    }

    [Fact]
    public void Estorno_correto_tem_tipo_oposto_mesmo_valor_e_mesmo_caixa()
    {
        var estorno = Criar(valor: 50m, tipo: TipoLancamento.Debito, estornoDe: Guid.NewGuid()).Lancamento!;

        Assert.True(estorno.EstornaCorretamente("loja-centro", TipoLancamento.Credito, 50m));
        Assert.False(estorno.EstornaCorretamente("loja-centro", TipoLancamento.Debito, 50m));
        Assert.False(estorno.EstornaCorretamente("loja-centro", TipoLancamento.Credito, 49.99m));
        Assert.False(estorno.EstornaCorretamente("outra-loja", TipoLancamento.Credito, 50m));
    }

    [Fact]
    public void Lancamento_sem_referencia_nao_e_estorno()
    {
        var lancamento = Criar(tipo: TipoLancamento.Debito).Lancamento!;

        Assert.False(lancamento.EstornaCorretamente("loja-centro", TipoLancamento.Credito, 10m));
    }
}
