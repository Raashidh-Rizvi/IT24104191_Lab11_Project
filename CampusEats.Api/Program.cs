using System.Text;
using CampusEats.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Cors.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Port binding: Cloud providers (Railway, Render) inject PORT.
// Listen on both $PORT (if set) and 8080 to ensure Railway internal router always connects.
var port = Environment.GetEnvironmentVariable("PORT");
var listenUrls = new List<string> { "http://0.0.0.0:8080" };

if (!string.IsNullOrWhiteSpace(port) && port != "8080")
{
    listenUrls.Insert(0, $"http://0.0.0.0:{port}");
}

if (builder.Environment.IsProduction())
{
    builder.WebHost.UseUrls(listenUrls.ToArray());
}

// 2. Load optional local .env file (primarily for local development)
var envPath = Path.Combine(builder.Environment.ContentRootPath, ".env");
if (File.Exists(envPath))
{
    foreach (var line in File.ReadAllLines(envPath))
    {
        var trimmed = line.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#')) continue;
        var parts = trimmed.Split('=', 2);
        if (parts.Length == 2)
        {
            var key = parts[0].Trim().Replace("__", ":");
            var val = parts[1].Trim().Trim('"').Trim('\'');
            if (!string.IsNullOrWhiteSpace(val))
            {
                builder.Configuration[key] = val;
            }
        }
    }
}

// 3. Resolve database connection string
var connectionString = DatabaseConfig.ResolveConnectionString(
    builder.Configuration,
    builder.Environment);

if (string.IsNullOrWhiteSpace(connectionString))
{
    // Fallback unconfigured dummy to prevent container crashing on startup if variables were delayed
    connectionString = "Host=127.0.0.1;Port=5432;Database=unconfigured;Username=none;Password=none;";
}

builder.Services.AddDbContext<AppDbContext>(options =>
{
    options.UseNpgsql(connectionString, npgsqlOptions =>
    {
        npgsqlOptions.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(5),
            errorCodesToAdd: null);
    });
});

// 4. Validate JWT secret key (ensuring container never crashes on startup if variable is delayed)
var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 || DatabaseConfig.IsPlaceholder(jwtKey))
{
    if (builder.Environment.IsProduction())
    {
        jwtKey = "CampusEatsSuperSecretProductionKey2026AtLeast32Chars!";
    }
    else
    {
        jwtKey = "CampusEatsSuperSecretKeyForLocalDevOnly2026!";
    }
}

// 5. Configure comprehensive CORS policy
var rawAllowedOrigins = builder.Configuration["Cors:AllowedOrigins"] ?? "";
var allowedOrigins = rawAllowedOrigins
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .ToList();

var knownOrigins = new[]
{
    "https://it-24104191-lab11-project-lilac.vercel.app",
    "https://it-24104191-lab11-project-jfjbfrgjk-raashidh-rizvis-projects.vercel.app",
    "https://campuseats-it24104191.vercel.app",
    "http://localhost:5173",
    "http://localhost:4173",
    "http://localhost:3000",
    "http://localhost:5108",
    "http://localhost:8080",
    "http://127.0.0.1:5173",
    "http://127.0.0.1:4173"
};

foreach (var origin in knownOrigins)
{
    if (!allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase))
    {
        allowedOrigins.Add(origin);
    }
}

void ConfigureCorsPolicy(CorsPolicyBuilder policy)
{
    policy.SetIsOriginAllowed(origin =>
    {
        if (string.IsNullOrWhiteSpace(origin)) return false;
        var trimmedOrigin = origin.TrimEnd('/');

        // Explicit allowlist
        if (allowedOrigins.Any(o => string.Equals(o, trimmedOrigin, StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Allow any Vercel production or preview deployment (*.vercel.app)
        if (Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out var uri) &&
            uri.Scheme == Uri.UriSchemeHttps &&
            uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        // Allow localhost in non-production environments
        if (!builder.Environment.IsProduction() && DatabaseConfig.IsLocalhost(trimmedOrigin))
        {
            return true;
        }

        return false;
    })
    .AllowAnyHeader()
    .AllowAnyMethod();
}

builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(ConfigureCorsPolicy);
    options.AddPolicy("Frontend", ConfigureCorsPolicy);
});

// 6. Authentication & Authorization
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "CampusEats",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "CampusEatsUsers",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// 7. Middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}

app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "CampusEats API v1");
    c.RoutePrefix = "swagger";
});

// Correct order: CORS -> Authentication -> Authorization -> MapControllers
app.UseCors("Frontend");
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers().RequireCors("Frontend");

// 8. Run migrations in background after startup so Kestrel binds immediately
app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("CampusEats API started successfully. Environment: {Environment}. Listening on: {Urls}",
        app.Environment.EnvironmentName, string.Join(", ", listenUrls));

    _ = Task.Run(async () =>
    {
        try
        {
            using var scope = app.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await DbInitializer.ApplyMigrationsAndSeedAsync(db, app.Logger);
        }
        catch (Exception ex)
        {
            app.Logger.LogError(ex, "Background database migration encountered an error: {Message}", ex.Message);
        }
    });
});

await app.RunAsync();
