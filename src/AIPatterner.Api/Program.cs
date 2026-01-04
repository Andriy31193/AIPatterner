// Main entry point for the API
using AIPatterner.Api;
using AIPatterner.Application.Mappings;
using AIPatterner.Application.Validators;
using AIPatterner.Infrastructure.Persistence;
using AIPatterner.Infrastructure.Services;
using AIPatterner.Infrastructure.Workers;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Serilog;
using System.Reflection;
using System.Text;
using System.Threading;

var builder = WebApplication.CreateBuilder(args);

// Serilog configuration
Log.Logger = new LoggerConfiguration()
    .ReadFrom.Configuration(builder.Configuration)
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/aipatterner-.log", rollingInterval: RollingInterval.Day)
    .CreateLogger();

builder.Host.UseSerilog();

// Add services
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        // Serialize enums as strings instead of numbers
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
    });
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key not configured");
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "AIPatterner";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "AIPatterner";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
    };
});

builder.Services.AddAuthorization();

// Database
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));

// Health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<ApplicationDbContext>();

// MediatR
builder.Services.AddMediatR(cfg =>
{
    cfg.RegisterServicesFromAssembly(typeof(AIPatterner.Application.Commands.IngestEventCommand).Assembly);
});

// AutoMapper
builder.Services.AddAutoMapper(typeof(MappingProfile));

// FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<ActionEventDtoValidator>();

// Application services
builder.Services.AddScoped<AIPatterner.Application.Handlers.IEventRepository, AIPatterner.Infrastructure.Persistence.Repositories.EventRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.ITransitionRepository, AIPatterner.Infrastructure.Persistence.Repositories.TransitionRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IReminderCandidateRepository, AIPatterner.Infrastructure.Persistence.Repositories.ReminderCandidateRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IExecutionHistoryRepository, AIPatterner.Infrastructure.Persistence.Repositories.ExecutionHistoryRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IApiKeyRepository, AIPatterner.Infrastructure.Persistence.Repositories.ApiKeyRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IConfigurationRepository, AIPatterner.Infrastructure.Persistence.Repositories.ConfigurationRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IUserPreferencesRepository, AIPatterner.Infrastructure.Persistence.Repositories.UserPreferencesRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.ICooldownService, AIPatterner.Infrastructure.Services.CooldownService>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IReminderScheduler, ReminderScheduler>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IReminderEvaluationService, ReminderEvaluationService>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.INotificationService, NotificationService>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IMemoryGateway, MemoryGateway>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IApiKeyService, ApiKeyService>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IExecutionHistoryService, ExecutionHistoryService>();
builder.Services.AddScoped<AIPatterner.Application.Services.IMatchingRemindersService, MatchingRemindersService>();
builder.Services.AddScoped<AIPatterner.Application.Services.IMatchingPolicyService, MatchingPolicyService>();
builder.Services.AddScoped<AIPatterner.Application.Services.IExecutionActionEvaluator, ExecutionActionEvaluator>();

// Routine learning services
builder.Services.AddScoped<AIPatterner.Application.Handlers.IRoutineRepository, AIPatterner.Infrastructure.Persistence.Repositories.RoutineRepository>();
builder.Services.AddScoped<AIPatterner.Application.Handlers.IRoutineReminderRepository, AIPatterner.Infrastructure.Persistence.Repositories.RoutineReminderRepository>();
builder.Services.AddScoped<AIPatterner.Application.Services.IRoutineLearningService, AIPatterner.Infrastructure.Services.RoutineLearningService>();

// Domain services
builder.Services.AddScoped<AIPatterner.Domain.Services.ITransitionLearner, TransitionLearner>();
builder.Services.AddScoped<AIPatterner.Domain.Services.ISignalSelector, AIPatterner.Infrastructure.Services.SignalSelector>();
builder.Services.AddScoped<AIPatterner.Domain.Services.ISignalSimilarityEvaluator, AIPatterner.Infrastructure.Services.SignalSimilarityEvaluator>();
builder.Services.AddScoped<AIPatterner.Application.Services.ISignalPolicyService, AIPatterner.Infrastructure.Services.SignalPolicyService>();
builder.Services.AddScoped<AIPatterner.Domain.Services.IContextBucketKeyBuilder, ContextBucketKeyBuilder>();
builder.Services.AddScoped<AIPatterner.Domain.Services.IReminderPolicyEvaluator, ReminderPolicyEvaluator>();
builder.Services.AddScoped<AIPatterner.Application.Services.IOccurrencePatternParser, AIPatterner.Application.Services.OccurrencePatternParser>();
builder.Services.AddScoped<IContextService, ContextService>();
builder.Services.AddScoped<ILLMClient, LLMClient>();

// Authentication service
builder.Services.AddScoped<IAuthService, AuthService>();

// HTTP clients
builder.Services.AddHttpClient("Notification");
builder.Services.AddHttpClient("LLM");
builder.Services.AddHttpClient("Memory");

// Background workers
builder.Services.AddHostedService<EventCleanupWorker>();
builder.Services.AddHostedService<TransitionDecayWorker>();
builder.Services.AddHostedService<CandidateSchedulerWorker>();

var app = builder.Build();

// Migrate database with retry logic (non-blocking in Development)
var maxRetries = 5;
var retryDelay = TimeSpan.FromSeconds(5);
var migrationSucceeded = false;

for (int attempt = 1; attempt <= maxRetries; attempt++)
{
    try
    {
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
            
            logger.LogInformation("Applying database migrations (attempt {Attempt}/{MaxRetries})...", attempt, maxRetries);
            
            // Ensure database can be connected
            if (!db.Database.CanConnect())
            {
                throw new InvalidOperationException("Cannot connect to database");
            }
            
            // Apply migrations
            db.Database.Migrate();
            
            // Verify critical tables exist
            var pendingMigrations = db.Database.GetPendingMigrations().ToList();
            if (pendingMigrations.Any())
            {
                throw new InvalidOperationException($"Migrations still pending: {string.Join(", ", pendingMigrations)}");
            }
            
            // Verify ReminderCandidates table exists by attempting a simple query
            try
            {
                var count = db.ReminderCandidates.Count();
                logger.LogInformation("ReminderCandidates table verified (contains {Count} records)", count);
            }
            catch (Exception tableEx)
            {
                throw new InvalidOperationException("ReminderCandidates table does not exist or is not accessible", tableEx);
            }
            
            logger.LogInformation("Database migrations applied successfully. All tables verified.");
            migrationSucceeded = true;
            break; // Success, exit retry loop
        }
    }
    catch (Exception ex)
    {
        if (attempt == maxRetries)
        {
            if (app.Environment.IsDevelopment())
            {
                // In development, log error but don't crash - allow app to start for testing auth endpoints
                Log.Warning(ex, "Failed to apply database migrations after {MaxRetries} attempts. Continuing in Development mode - some features may not work.", maxRetries);
                Log.Warning("To fix: Ensure PostgreSQL is running and connection string is correct. For local development, use: Host=localhost;Port=5433;Database=aipatterner;Username=postgres;Password=postgres");
            }
            else
            {
                // In production, fail fast
                Log.Fatal(ex, "Failed to apply database migrations after {MaxRetries} attempts", maxRetries);
                throw;
            }
        }
        else
        {
            Log.Warning(ex, "Database migration attempt {Attempt}/{MaxRetries} failed. Retrying in {Delay} seconds...", 
                attempt, maxRetries, retryDelay.TotalSeconds);
            Thread.Sleep(retryDelay);
        }
    }
}

// Configure pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseSerilogRequestLogging();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<ApiKeyAuthenticationMiddleware>();
app.UseHealthChecks("/health");
app.UseHealthChecks("/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = check => check.Tags.Contains("ready")
});

app.MapControllers();

Log.Information("AIPatterner API starting");

app.Run();

