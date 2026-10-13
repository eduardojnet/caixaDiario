using CaixaDiario.Consolidado.Migracoes;
using Npgsql;

// Passo de migracao do docker compose: aplica o que falta, define a senha do
// papel da aplicacao e termina. O codigo de saida decide se a API sobe
// (service_completed_successfully).
//
//   ConnectionStrings__Migrador  conexao administrativa (obrigatoria)
//   APP_DB_PASSWORD              senha do papel consolidado_app (obrigatoria)

var conexao = Environment.GetEnvironmentVariable("ConnectionStrings__Migrador");
var senha = Environment.GetEnvironmentVariable("APP_DB_PASSWORD");
if (string.IsNullOrWhiteSpace(conexao) || string.IsNullOrWhiteSpace(senha))
{
    Console.Error.WriteLine("ConnectionStrings__Migrador e APP_DB_PASSWORD sao obrigatorias.");
    return 2;
}

var resultado = Migrador.Aplicar(conexao, registrarNoConsole: true);
if (!resultado.Sucesso)
{
    Console.Error.WriteLine($"Migracao falhou: {resultado.Erro?.Message}");
    return 1;
}

Console.WriteLine(resultado.Aplicados.Count == 0
    ? "Nenhum script novo: esquema ja atualizado."
    : $"Scripts aplicados: {string.Join(", ", resultado.Aplicados)}");

try
{
    Migrador.DefinirSenhaDaAplicacao(conexao, senha);
}
catch (PostgresException ex)
{
    Console.Error.WriteLine($"Falha ao definir a senha de {Migrador.PapelDaAplicacao}: {ex.MessageText}");
    return 1;
}

return 0;
