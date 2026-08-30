using EduTask.Api.Services;

var builder = WebApplication.CreateBuilder(args);
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
