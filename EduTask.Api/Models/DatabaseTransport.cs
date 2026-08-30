using System.Text.Json;

namespace EduTask.Api.Models;

public sealed record SqlArgument(string Name, int DbType, int Size, JsonElement? Value);
public sealed record SqlRequest(string Sql, IReadOnlyList<SqlArgument> Parameters);
public sealed record TableColumn(string Name, string Type);
public sealed record TablePayload(IReadOnlyList<TableColumn> Columns, IReadOnlyList<object?[]> Rows);
public sealed record ScalarPayload(string? Type, object? Value);
