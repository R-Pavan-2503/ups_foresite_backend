using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using CodeFamily.Api.Core.Services;
using CodeFamily.Api.Workers;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Load settings from settings.json in root directory
var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "..", "settings.json");
if (File.Exists(settingsPath))
{
    builder.Configuration.AddJsonFile(settingsPath, optional: false, reloadOnChange: true);
}

// Configure AppSettings
builder.Services.Configure<AppSettings>(builder.Configuration);

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins("http://localhost:5173")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register HttpClient for services
builder.Services.AddHttpClient();

// Register services
builder.Services.AddSingleton<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddSingleton<ITreeSitterService, TreeSitterService>();
builder.Services.AddSingleton<ISlackService, SlackService>();
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddSingleton<IAnalysisService, AnalysisService>();

// Register background workers
builder.Services.AddHostedService<IncrementalWorker>();

// Add logging
builder.Services.AddLogging(config =>
{
    config.AddConsole();
    config.AddDebug();
});

var app = builder.Build();

// Configure middleware
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Logger.LogInformation("CodeFamily API starting on port 5000");
app.Logger.LogInformation("Ensure your GitHub App private key is placed at /secrets/codefamily.pem");

app.Run("http://localhost:5000");
