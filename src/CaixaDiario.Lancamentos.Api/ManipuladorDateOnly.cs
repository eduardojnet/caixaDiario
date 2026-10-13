using System.Data;
using Dapper;

namespace CaixaDiario.Lancamentos.Api;

/// <summary>
/// Faz o Dapper tratar DateOnly como coluna date. O Npgsql le date como
/// DateTime por padrao; a conversao fica aqui, num lugar so.
/// </summary>
internal sealed class ManipuladorDateOnly : SqlMapper.TypeHandler<DateOnly>
{
    public override void SetValue(IDbDataParameter parameter, DateOnly value)
    {
        parameter.DbType = DbType.Date;
        parameter.Value = value;
    }

    public override DateOnly Parse(object value) => value switch
    {
        DateOnly data => data,
        DateTime instante => DateOnly.FromDateTime(instante),
        _ => throw new DataException($"Valor de data inesperado: {value.GetType().Name}."),
    };
}
