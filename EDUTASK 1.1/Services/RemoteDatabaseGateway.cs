using Microsoft.Data.SqlClient;
using System.Data;
using System.Net.Http.Json;
using System.Text.Json;

namespace EDUTASK_1._1.Services;

internal static class RemoteDatabaseGateway
{
    private static readonly HttpClient Client = new()
    {
        BaseAddress = new Uri("http://10.0.2.2:5187/"),
        Timeout = TimeSpan.FromSeconds(30)
    };

    public static async Task<DataTable> QueryAsync(string sql, IEnumerable<SqlParameter>? parameters, CancellationToken token)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync("api/database/query", CreateRequest(sql, parameters), token);
        await EnsureSuccessAsync(response, token);
        TablePayload payload = await response.Content.ReadFromJsonAsync<TablePayload>(cancellationToken: token)
            ?? throw new InvalidOperationException("The database API returned no table data.");
        var table = new DataTable();
        foreach (TableColumn column in payload.Columns) table.Columns.Add(column.Name, ResolveType(column.Type));
        foreach (JsonElement[] sourceRow in payload.Rows)
        {
            object?[] values = new object?[payload.Columns.Count];
            for (int i = 0; i < values.Length; i++)
                values[i] = ConvertValue(sourceRow[i], table.Columns[i].DataType) ?? DBNull.Value;
            table.Rows.Add(values);
        }
        return table;
    }

    public static async Task<int> NonQueryAsync(string sql, IEnumerable<SqlParameter>? parameters, CancellationToken token)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync("api/database/nonquery", CreateRequest(sql, parameters), token);
        await EnsureSuccessAsync(response, token);
        return await response.Content.ReadFromJsonAsync<int>(cancellationToken: token);
    }

    public static async Task<object?> ScalarAsync(string sql, IEnumerable<SqlParameter>? parameters, CancellationToken token)
    {
        using HttpResponseMessage response = await Client.PostAsJsonAsync("api/database/scalar", CreateRequest(sql, parameters), token);
        await EnsureSuccessAsync(response, token);
        ScalarPayload payload = await response.Content.ReadFromJsonAsync<ScalarPayload>(cancellationToken: token)
            ?? throw new InvalidOperationException("The database API returned no scalar data.");
        return payload.Value is JsonElement value && payload.Type is not null
            ? ConvertValue(value, ResolveType(payload.Type))
            : null;
    }

    private static SqlRequest CreateRequest(string sql, IEnumerable<SqlParameter>? parameters) =>
        new(sql, parameters?.Select(p => new SqlArgument(
            p.ParameterName, (int)p.SqlDbType, p.Size, p.Value is DBNull ? null : p.Value)).ToArray() ?? []);

    private static async Task EnsureSuccessAsync(HttpResponseMessage response, CancellationToken token)
    {
        if (response.IsSuccessStatusCode) return;
        string details = await response.Content.ReadAsStringAsync(token);
        throw new InvalidOperationException($"Local database API error {(int)response.StatusCode}: {details}".Trim());
    }

    private static Type ResolveType(string typeName) => typeName switch
    {
        "System.Int16" => typeof(short), "System.Int32" => typeof(int), "System.Int64" => typeof(long),
        "System.Byte" => typeof(byte), "System.Boolean" => typeof(bool), "System.DateTime" => typeof(DateTime),
        "System.Decimal" => typeof(decimal), "System.Double" => typeof(double), "System.Single" => typeof(float),
        "System.Guid" => typeof(Guid), "System.Byte[]" => typeof(byte[]), _ => typeof(string)
    };

    private static object? ConvertValue(JsonElement value, Type type)
    {
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined) return null;
        if (type == typeof(short)) return value.GetInt16();
        if (type == typeof(int)) return value.GetInt32();
        if (type == typeof(long)) return value.GetInt64();
        if (type == typeof(byte)) return value.GetByte();
        if (type == typeof(bool)) return value.GetBoolean();
        if (type == typeof(DateTime)) return value.GetDateTime();
        if (type == typeof(decimal)) return value.GetDecimal();
        if (type == typeof(double)) return value.GetDouble();
        if (type == typeof(float)) return value.GetSingle();
        if (type == typeof(Guid)) return value.GetGuid();
        if (type == typeof(byte[])) return value.GetBytesFromBase64();
        return value.GetString() ?? string.Empty;
    }

    private sealed record SqlArgument(string Name, int DbType, int Size, object? Value);
    private sealed record SqlRequest(string Sql, IReadOnlyList<SqlArgument> Parameters);
    private sealed record TableColumn(string Name, string Type);
    private sealed record TablePayload(IReadOnlyList<TableColumn> Columns, IReadOnlyList<JsonElement[]> Rows);
    private sealed record ScalarPayload(string? Type, object? Value);
}
