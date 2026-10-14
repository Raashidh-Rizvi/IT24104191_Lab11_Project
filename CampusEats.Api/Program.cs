using System.Text;
using CampusEats.Api.Data;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// 1. PORT: cloud hosts (Render, Railway) tell the app which port to use.
var port = Environment.GetEnvironmentVariable("PORT");
if (!string.IsNullOrWhiteSpace(port))
{
    builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
}

// 2. Settings. Secrets come from environment variables (cloud), user-secrets (local), or local .env file.
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
            builder.Configuration[key] = val;
        }
    }
}

// Support both ConnectionStrings:Default and Railway's DATABASE_URL / POSTGRES_URL
var rawConn = builder.Configuration.GetConnectionString("Default")
    ?? builder.Configuration["DATABASE_URL"]
    ?? builder.Configuration["POSTGRES_URL"]
    ?? Environment.GetEnvironmentVariable("DATABASE_URL")
    ?? Environment.GetEnvironmentVariable("POSTGRES_URL");

var connectionString = FormatPostgreSqlConnectionString(rawConn);
if (string.IsNullOrWhiteSpace(connectionString))
{
    // Safe local fallback so the app starts even before database configuration
    connectionString = "Host=localhost;Database=campuseats;Username=postgres;Password=postgres";
}

var jwtKey = builder.Configuration["Jwt:Key"];
if (string.IsNullOrWhiteSpace(jwtKey) || jwtKey.Length < 32)
{
    jwtKey = "CampusEatsSuperSecretKeyForAuthentication2026!";
}

var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"] ?? "http://localhost:5173")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
    .Select(o => o.TrimEnd('/'))
    .ToArray();

// 3. Services
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

// Allow CORS for any frontend origin (local Vite, preview, or Vercel deployments)
builder.Services.AddCors(o => o.AddPolicy("Frontend", p =>
    p.SetIsOriginAllowed(_ => true)
     .AllowAnyHeader()
     .AllowAnyMethod()));

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
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

app.UseDeveloperExceptionPage();

// Automatically apply EF Core migrations if the database is accessible
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        db.Database.Migrate();
        app.Logger.LogInformation("Database migrations applied successfully.");
    }
    catch (Exception ex)
    {
        app.Logger.LogWarning("Database migration skipped/failed on startup: {Message}", ex.Message);
    }
}

// Swagger stays ON in production so the lab endpoints can be tested.
app.UseSwagger();
app.UseSwaggerUI();

app.UseCors("Frontend");        // must be before MapControllers
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/", () => Results.Redirect("/swagger"));
app.MapControllers();

app.Lifetime.ApplicationStarted.Register(() =>
    app.Logger.LogInformation("CampusEats API started. Environment: {Environment}", app.Environment.EnvironmentName));

app.Run();

static string FormatPostgreSqlConnectionString(string? input)
{
    if (string.IsNullOrWhiteSpace(input)) return "";

    if (input.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
        input.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
    {
        try
        {
            var uri = new Uri(input);
            var userInfo = uri.UserInfo.Split(':');
            var user = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : "";
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : "";
            var host = uri.Host;
            var port = uri.Port > 0 ? uri.Port : 5432;
            var db = uri.AbsolutePath.TrimStart('/');

            var sb = new StringBuilder();
            sb.Append($"Host={host};Port={port};Database={db};Username={user};Password={password};");
            if (!host.Equals("localhost", StringComparison.OrdinalIgnoreCase) && host != "127.0.0.1")
            {
                sb.Append("SSL Mode=Require;Trust Server Certificate=true;");
            }
            return sb.ToString();
        }
        catch
        {
            return input;
        }
    }

    if (!input.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase) &&
        !input.Contains("localhost", StringComparison.OrdinalIgnoreCase) &&
        !input.Contains("127.0.0.1"))
    {
        input = input.TrimEnd(';') + ";SSL Mode=Require;Trust Server Certificate=true;";
    }

    return input;
}
