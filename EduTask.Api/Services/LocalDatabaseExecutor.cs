using EduTask.Api.Models;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Text.Json;

namespace EduTask.Api.Services;

public sealed class LocalDatabaseExecutor(IConfiguration configuration)
{
    private readonly string _connectionString = configuration.GetConnectionString("EduTask")
        ?? throw new InvalidOperationException("ConnectionStrings:EduTask is not configured.");

    public async Task<TablePayload> QueryAsync(SqlRequest request, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = CreateCommand(connection, request);
        await using SqlDataReader reader = await command.ExecuteReaderAsync(token);
        var columns = Enumerable.Range(0, reader.FieldCount)
            .Select(i => new TableColumn(reader.GetName(i), reader.GetFieldType(i).FullName ?? "System.String"))
            .ToArray();
        var rows = new List<object?[]>();
        while (await reader.ReadAsync(token))
        {
            var values = new object?[reader.FieldCount];
            reader.GetValues(values);
            for (int i = 0; i < values.Length; i++)
                if (values[i] is DBNull) values[i] = null;
            rows.Add(values);
        }
        return new TablePayload(columns, rows);
    }

    public async Task<int> NonQueryAsync(SqlRequest request, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = CreateCommand(connection, request);
        return await command.ExecuteNonQueryAsync(token);
    }

    public async Task<ScalarPayload> ScalarAsync(SqlRequest request, CancellationToken token)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(token);
        await using var command = CreateCommand(connection, request);
        object? value = await command.ExecuteScalarAsync(token);
        return value is null or DBNull
            ? new ScalarPayload(null, null)
            : new ScalarPayload(value.GetType().FullName, value);
    }

    private static SqlCommand CreateCommand(SqlConnection connection, SqlRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Sql)) throw new ArgumentException("SQL text is required.");
        var command = new SqlCommand(request.Sql, connection);
        foreach (SqlArgument argument in request.Parameters)
        {
            var parameter = new SqlParameter(argument.Name, (SqlDbType)argument.DbType);
            if (argument.Size != 0) parameter.Size = argument.Size;
            parameter.Value = ConvertValue(argument.Value, parameter.SqlDbType) ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
        return command;
    }

    private static object? ConvertValue(JsonElement? value, SqlDbType type)
    {
        if (value is null || value.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        JsonElement element = value.Value;
        return type switch
        {
            SqlDbType.Int => element.GetInt32(),
            SqlDbType.BigInt => element.GetInt64(),
            SqlDbType.SmallInt => element.GetInt16(),
            SqlDbType.TinyInt => element.GetByte(),
            SqlDbType.Bit => element.GetBoolean(),
            SqlDbType.Date or SqlDbType.DateTime or SqlDbType.DateTime2 or SqlDbType.SmallDateTime => element.GetDateTime(),
            SqlDbType.Decimal or SqlDbType.Money or SqlDbType.SmallMoney => element.GetDecimal(),
            SqlDbType.Float => element.GetDouble(),
            SqlDbType.Real => element.GetSingle(),
            SqlDbType.UniqueIdentifier => element.GetGuid(),
            SqlDbType.Binary or SqlDbType.VarBinary or SqlDbType.Image => element.GetBytesFromBase64(),
            _ => element.GetString() ?? string.Empty
        };
    }
}
