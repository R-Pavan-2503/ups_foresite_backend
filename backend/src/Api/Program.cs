using CodeFamily.Api.Core.Interfaces;
using CodeFamily.Api.Core.Models;
using CodeFamily.Api.Core.Services;
using CodeFamily.Api.Workers;
using Microsoft.Extensions.Options;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

// Load environment variables from .env file if it exists
var envPath = Path.Combine(Directory.GetCurrentDirectory(), "..", "..", ".env");
if (File.Exists(envPath))
{
    DotNetEnv.Env.Load(envPath);
    Console.WriteLine("✓ Loaded .env file");
}
else
{
    Console.WriteLine("⚠ .env file not found, using environment variables from system");
}

// Load settings from settings.json in current directory (works in Docker)
var settingsPath = Path.Combine(Directory.GetCurrentDirectory(), "settings.json");
if (File.Exists(settingsPath))
{
    var jsonContent = File.ReadAllText(settingsPath);

    // Replace ${VAR_NAME} with environment variable values
    // Using Regex to find all patterns of ${VAR_NAME}
    var regex = new System.Text.RegularExpressions.Regex(@"\$\{(.+?)\}");
    jsonContent = regex.Replace(jsonContent, match =>
    {
        var varName = match.Groups[1].Value;
        var envValue = Environment.GetEnvironmentVariable(varName);
        return envValue ?? match.Value; // Return original if not found
    });

    var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(jsonContent));
    builder.Configuration.AddJsonStream(stream);
}

// Configure AppSettings
builder.Services.Configure<AppSettings>(builder.Configuration);

// Add controllers
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Add MiniProfiler for performance monitoring
builder.Services.AddMiniProfiler(options =>
{
    options.RouteBasePath = "/profiler"; // URL to access profiler
    options.ColorScheme = StackExchange.Profiling.ColorScheme.Auto;
    options.EnableDebugMode = true; // Show more details in dev
});

// Add CORS with configurable origins
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        var allowedOrigins = Environment.GetEnvironmentVariable("ALLOWED_ORIGINS")
            ?.Split(',')
            ?? new[] { "http://localhost:5173" };
        
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

// Register HttpClient for services
builder.Services.AddHttpClient();

// Configure NpgsqlDataSource for better connection pooling
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new Exception("ConnectionStrings:DefaultConnection is required");

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.ConnectionStringBuilder.MinPoolSize = 5;
dataSourceBuilder.ConnectionStringBuilder.MaxPoolSize = 50;
dataSourceBuilder.ConnectionStringBuilder.Pooling = true;

var dataSource = dataSourceBuilder.Build();
builder.Services.AddSingleton(dataSource);

// Register services
builder.Services.AddScoped<IDatabaseService, DatabaseService>();
builder.Services.AddSingleton<IGitHubService, GitHubService>();
builder.Services.AddSingleton<IGeminiService, GeminiService>();
builder.Services.AddSingleton<ITreeSitterService, TreeSitterService>();
builder.Services.AddSingleton<ISlackService, SlackService>();
builder.Services.AddSingleton<IRepositoryService, RepositoryService>();
builder.Services.AddScoped<IAnalysisService, AnalysisService>();
builder.Services.AddSingleton<IGroqService, GroqService>();

// Notes System Services
builder.Services.AddSingleton<ISupabaseStorageService, SupabaseStorageService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<INotesService, NotesService>();
builder.Services.AddScoped<ILineCommentsService, LineCommentsService>();

// Register background workers
// builder.Services.AddHostedService<IncrementalWorker>(); // Disabled: Not using webhooks

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
    
    // Enable MiniProfiler (dev only)
    app.UseMiniProfiler();
}

app.UseCors();

app.MapControllers();

// Health check endpoint
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

var port = Environment.GetEnvironmentVariable("PORT") ?? "5000";
app.Logger.LogInformation($"CodeFamily API starting on port {port}");
app.Logger.LogInformation("Ensure your GitHub App private key is placed at /secrets/codefamily.pem");

app.Run($"http://0.0.0.0:{port}");
