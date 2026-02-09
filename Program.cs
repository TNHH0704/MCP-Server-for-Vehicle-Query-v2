using McpVersionVer2.Models;
using McpVersionVer2.Services;
using McpVersionVer2.Services.Mappers;
using McpVersionVer2.Security;
using McpVersionVer2.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.AspNetCore.RateLimiting;
using System.Threading.RateLimiting;
using DotNetEnv;

Env.Load();

var builder = WebApplication.CreateBuilder(args);

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Services.AddMemoryCache();

builder.Services.AddHttpClient("AuthService", client =>
{
    client.Timeout = TimeSpan.FromSeconds(30);
    client.DefaultRequestHeaders.Add("User-Agent", "McpVersionVer2/1.0");
});
builder.Services.AddHttpClient(); 

builder.Services.Configure<ConversationConfig>(builder.Configuration.GetSection("ConversationContext"));

// Add DbContext factory for database access (singleton lifetime for use in singleton services)
// Unified factory serves both design-time (EF migrations) and runtime needs
builder.Services.AddSingleton<IDbContextFactory<ConversationDbContext>>(sp =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection") 
        ?? throw new InvalidOperationException("DefaultConnection is not configured");
    return new McpVersionVer2.Data.ConversationDbContextFactory(connectionString);
});

// Add scoped DbContext for controllers (using factory to create instances)
builder.Services.AddScoped<ConversationDbContext>(sp =>
{
    var factory = sp.GetRequiredService<IDbContextFactory<ConversationDbContext>>();
    return factory.CreateDbContext();
});

builder.Services.AddSingleton<IConversationContextService, InMemoryConversationContextService>();
// Use database-backed session storage for persistence across restarts
builder.Services.AddSingleton<ISessionStorageService, DatabaseSessionStorageService>();
builder.Services.AddScoped<RequestContextService>();
builder.Services.AddSingleton<AuditLogService>();
builder.Services.AddSingleton<IGitHubOpenAIService, GitHubOpenAIService>();
builder.Services.AddSingleton<SecurityValidationService>();
builder.Services.AddScoped<IConversationSummarizationService, ConversationSummarizationService>();

builder.Services.AddTransient<VehicleMapperService>();

builder.Services.AddTransient<VehicleStatusService>(sp =>
    new VehicleStatusService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        sp.GetRequiredService<IConfiguration>()
    ));

builder.Services.AddTransient<VehicleStatusMapperService>(sp =>
    new VehicleStatusMapperService(sp.GetRequiredService<SecurityValidationService>())
);

builder.Services.AddTransient<WaypointService>(sp =>
    new WaypointService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        sp.GetRequiredService<ILogger<WaypointService>>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<IMemoryCache>()
    ));

builder.Services.AddTransient<VehicleHistoryService>();

builder.Services.AddTransient<VehicleResolverService>();

builder.Services.AddTransient<AuthService>(sp =>
    new AuthService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient("AuthService"),
        sp.GetRequiredService<ILogger<AuthService>>(),
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ISessionStorageService>()
    ));

builder.Services.AddTransient<VehicleService>(sp =>
    new VehicleService(
        sp.GetRequiredService<IHttpClientFactory>().CreateClient(),
        sp.GetRequiredService<ILogger<VehicleService>>(),
        sp.GetRequiredService<IConfiguration>()
    ));

builder.Services.AddControllers();

builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    
    options.AddPolicy("toolApi", context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var token = authHeader?.Replace("Bearer ", "") ?? string.Empty;
        var partitionKey = TokenHashHelper.GetTokenHash(token);
        
        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 60,
            SegmentsPerWindow = 6,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
    
    // Rate limiting for conversation API endpoints (10 requests per minute)
    options.AddPolicy("conversationApi", context =>
    {
        var authHeader = context.Request.Headers["Authorization"].FirstOrDefault();
        var token = authHeader?.Replace("Bearer ", "") ?? string.Empty;
        var partitionKey = TokenHashHelper.GetTokenHash(token);
        
        return RateLimitPartition.GetSlidingWindowLimiter(partitionKey, _ => new SlidingWindowRateLimiterOptions
        {
            Window = TimeSpan.FromMinutes(1),
            PermitLimit = 10,
            SegmentsPerWindow = 2,
            QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
            QueueLimit = 0
        });
    });
});

builder.Services.AddMcpServer()
    .WithPromptsFromAssembly()
    .WithHttpTransport() 
    .WithToolsFromAssembly(typeof(McpVersionVer2.Tools.AuthTools).Assembly);

builder.Services.AddCors(options =>
{
    options.AddPolicy("DefaultPolicy", policy =>
    {
        policy.SetIsOriginAllowed(_ => true) 
              .AllowAnyMethod()
              .AllowAnyHeader() 
              .AllowCredentials();
    });
});

var app = builder.Build();

app.UseCors("DefaultPolicy");

app.UseDefaultFiles(); 
app.UseStaticFiles();

app.UseMiddleware<SessionHeaderMiddleware>();

app.UseRouting();

app.UseRateLimiter();

app.UseMiddleware<ConversationContextMiddleware>();

app.MapMcp("/sse").RequireRateLimiting("toolApi");

app.MapControllers();

app.MapGet("/api/session", (ISessionStorageService sessionStorage) => {
    var sessionId = sessionStorage.CreateAnonymousSession();
    return Results.Json(new { sessionId });
});

// Fallback must be LAST - after all API routes
app.MapFallbackToFile("index.html");

app.Run("http://0.0.0.0:8080");

// Make Program accessible for integration tests
public partial class Program { }
