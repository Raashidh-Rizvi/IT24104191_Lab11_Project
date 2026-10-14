using System.Text;
using CampusEats.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. Port binding: Cloud providers (Railway, Render) inject PORT
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}
else if (builder.Environment.IsProduction() && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("ASPNETCORE_URLS")))
{
    builder.WebHost.UseUrls("http://0.0.0.0:8080");
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
            // Do not override with empty or whitespace values
            if (!string.IsNullOrWhiteSpace(val))
            {
                builder.Configuration[key] = val;
            }
        }
    }
}

// 3. Resolve and validate database connection string
var connectionString = DatabaseConfig.ResolveConnectionString(
    builder.Configuration,
    builder.Environment);

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

// 4. Validate JWT secret key
var jwtKey = builder.Configuration["Jwt:Key"];
if (builder.Environment.IsProduction())
{
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32 || DatabaseConfig.IsPlaceholder(jwtKey))
    {
        throw new InvalidOperationException(
            "Production configuration error: Jwt__Key is missing, too short (minimum 32 characters required), " +
            "or contains a placeholder. Please configure the Jwt__Key environment variable in Railway.");
    }
}
else
{
    if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
    {
        jwtKey = "CampusEatsSuperSecretKeyForLocalDevOnly2026!";
    }
}

// 5. Configure CORS
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .Where(o => !string.IsNullOrWhiteSpace(o))
    .ToList();

if (builder.Environment.IsDevelopment())
{
    var devDefaults = new[]
    {
        "http://localhost:5173",
        "http://localhost:4173",
        "http://localhost:3000",
        "http://localhost:5108",
        "http://localhost:8080",
        "http://127.0.0.1:5173",
        "http://127.0.0.1:4173"
    };
    foreach (var devOrigin in devDefaults)
    {
        if (!allowedOrigins.Contains(devOrigin, StringComparer.OrdinalIgnoreCase))
        {
            allowedOrigins.Add(devOrigin);
        }
    }
}

builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        if (builder.Environment.IsProduction())
        {
            policy.SetIsOriginAllowed(origin =>
            {
                if (string.IsNullOrWhiteSpace(origin)) return false;
                var trimmedOrigin = origin.TrimEnd('/');

                // Check explicit allowlist
                if (allowedOrigins.Any(o => string.Equals(o, trimmedOrigin, StringComparison.OrdinalIgnoreCase)))
                {
                    return true;
                }

                // Allow verified Vercel deployments (production and preview)
                if (Uri.TryCreate(trimmedOrigin, UriKind.Absolute, out var uri) &&
                    uri.Scheme == Uri.UriSchemeHttps &&
                    uri.Host.EndsWith(".vercel.app", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                return false;
            })
            .AllowAnyHeader()
            .AllowAnyMethod();
        }
        else
        {
            policy.WithOrigins(allowedOrigins.ToArray())
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
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
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

// 8. Apply database migrations and verify seed records on startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await DbInitializer.ApplyMigrationsAndSeedAsync(db, app.Logger);
}

app.Lifetime.ApplicationStarted.Register(() =>
{
    app.Logger.LogInformation("CampusEats API started successfully. Environment: {Environment}", app.Environment.EnvironmentName);
});

await app.RunAsync();
