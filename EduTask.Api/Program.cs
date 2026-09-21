using EduTask.Api.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options =>
    options.Limits.MaxRequestBodySize = 48 * 1024 * 1024);
builder.Logging.ClearProviders();
builder.Logging.AddSimpleConsole(options => options.SingleLine = true);
builder.Services.AddControllers();
builder.Services.AddSingleton<LocalDatabaseExecutor>();

var app = builder.Build();
if (!app.Environment.IsDevelopment())
    throw new InvalidOperationException("The LocalDB development bridge cannot run outside Development.");

app.MapGet("/health", () => Results.Ok(new { status = "running" }));
app.MapControllers();
app.Run();
