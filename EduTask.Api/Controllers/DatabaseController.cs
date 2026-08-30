using EduTask.Api.Models;
using EduTask.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace EduTask.Api.Controllers;

[ApiController]
[Route("api/database")]
public sealed class DatabaseController(LocalDatabaseExecutor database) : ControllerBase
{
    [HttpPost("query")]
    public Task<TablePayload> Query(SqlRequest request, CancellationToken token) => database.QueryAsync(request, token);

    [HttpPost("nonquery")]
    public Task<int> NonQuery(SqlRequest request, CancellationToken token) => database.NonQueryAsync(request, token);

    [HttpPost("scalar")]
    public Task<ScalarPayload> Scalar(SqlRequest request, CancellationToken token) => database.ScalarAsync(request, token);
}
