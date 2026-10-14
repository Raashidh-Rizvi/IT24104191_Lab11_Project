using System.Text;
using System.Web;

namespace CampusEats.Api.Data;

public static class DatabaseConfig
{
    private static readonly string[] PlaceholderTokens =
    [
        "YOUR_POSTGRESQL_CONNECTION_STRING_HERE",
        "YOUR_NEON",
        "REPLACE_WITH",
        "example.com",
        "<username>",
        "<password>",
        "<host>"
    ];

    /// <summary>
    /// Resolves the canonical PostgreSQL connection string using standard configuration sources.
    /// Priority:
    /// 1. DATABASE_URL (environment or config)
    /// 2. POSTGRES_URL (environment or config)
    /// 3. ConnectionStrings:Default
    /// </summary>
    public static string ResolveConnectionString(
        IConfiguration config,
        IHostEnvironment env,
        ILogger? logger = null)
    {
        // 1. Check DATABASE_URL first (canonical for Railway, Neon, Render)
        var candidates = new[]
        {
            ("DATABASE_URL (Environment)", Environment.GetEnvironmentVariable("DATABASE_URL")),
            ("DATABASE_URL (Configuration)", config["DATABASE_URL"]),
            ("POSTGRES_URL (Environment)", Environment.GetEnvironmentVariable("POSTGRES_URL")),
            ("POSTGRES_URL (Configuration)", config["POSTGRES_URL"]),
            ("ConnectionStrings:Default (Configuration)", config.GetConnectionString("Default")),
            ("ConnectionStrings__Default (Environment)", Environment.GetEnvironmentVariable("ConnectionStrings__Default"))
        };

        string? selectedRaw = null;
        string? selectedSource = null;

        foreach (var (source, rawValue) in candidates)
        {
            if (string.IsNullOrWhiteSpace(rawValue)) continue;

            if (IsPlaceholder(rawValue))
            {
                logger?.LogWarning("Ignoring placeholder database configuration from {Source}.", source);
                continue;
            }

            // In Production, ignore localhost strings
            if (env.IsProduction() && IsLocalhost(rawValue))
            {
                logger?.LogWarning("Ignoring localhost database configuration from {Source} in Production environment.", source);
                continue;
            }

            selectedRaw = rawValue.Trim();
            selectedSource = source;
            break;
        }

        if (string.IsNullOrWhiteSpace(selectedRaw))
        {
            if (env.IsProduction())
            {
                logger?.LogError(
                    "Production database connection string is missing or invalid. " +
                    "Please configure the DATABASE_URL environment variable in Railway with your Neon PostgreSQL connection string.");
                return "";
            }

            logger?.LogWarning("No database connection string configured. Using local development fallback.");
            return "Host=localhost;Port=5432;Database=campuseats;Username=postgres;Password=postgres;";
        }

        logger?.LogInformation("Using database connection configuration from {Source}.", selectedSource);
        var formatted = FormatPostgreSqlConnectionString(selectedRaw);

        logger?.LogInformation("Database configured: {SanitizedConnection}", MaskConnectionString(formatted));
        return formatted;
    }

    /// <summary>
    /// Checks if a connection string is a placeholder.
    /// </summary>
    public static bool IsPlaceholder(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return true;

        foreach (var token in PlaceholderTokens)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Checks if a connection string points to localhost.
    /// </summary>
    public static bool IsLocalhost(string value)
    {
        return value.Contains("localhost", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("127.0.0.1", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Converts postgres:// or postgresql:// URI format to Npgsql ADO.NET key-value connection string.
    /// Preserves URL-encoded characters, colons in passwords, and SSL parameters.
    /// </summary>
    public static string FormatPostgreSqlConnectionString(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return "";

        var trimmed = input.Trim().Trim('"').Trim('\'');

        if (trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) ||
            trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                var uri = new Uri(trimmed);

                // UserInfo can contain username and password separated by the first ':'
                string username = "";
                string password = "";

                if (!string.IsNullOrEmpty(uri.UserInfo))
                {
                    var colonIndex = uri.UserInfo.IndexOf(':');
                    if (colonIndex >= 0)
                    {
                        username = Uri.UnescapeDataString(uri.UserInfo[..colonIndex]);
                        password = Uri.UnescapeDataString(uri.UserInfo[(colonIndex + 1)..]);
                    }
                    else
                    {
                        username = Uri.UnescapeDataString(uri.UserInfo);
                    }
                }

                var host = uri.Host;
                var port = uri.Port > 0 ? uri.Port : 5432;
                var database = uri.AbsolutePath.TrimStart('/');

                var sb = new StringBuilder();
                sb.Append($"Host={host};Port={port};Database={database};Username={username};Password={password};");

                // Parse query parameters (e.g., sslmode=require, channel_binding=prefer)
                var queryParams = HttpUtility.ParseQueryString(uri.Query);
                var sslMode = queryParams["sslmode"];

                if (!string.IsNullOrWhiteSpace(sslMode))
                {
                    // Map postgres sslmode values to Npgsql SSL Mode
                    var npgsqlSslMode = sslMode.ToLowerInvariant() switch
                    {
                        "require" => "Require",
                        "disable" => "Disable",
                        "allow" => "Allow",
                        "prefer" => "Prefer",
                        "verify-ca" => "VerifyCA",
                        "verify-full" => "VerifyFull",
                        _ => "Require"
                    };
                    sb.Append($"SSL Mode={npgsqlSslMode};");
                }
                else if (!IsLocalhost(host))
                {
                    sb.Append("SSL Mode=Require;");
                }

                if (!IsLocalhost(host))
                {
                    sb.Append("Trust Server Certificate=true;");
                }

                // Parse channel_binding query parameter
                var channelBinding = queryParams["channel_binding"];
                if (!string.IsNullOrWhiteSpace(channelBinding))
                {
                    var npgsqlChannelBinding = channelBinding.ToLowerInvariant() switch
                    {
                        "require" => "Require",
                        "prefer" => "Prefer",
                        "disable" => "Disable",
                        _ => "Prefer"
                    };
                    sb.Append($"Channel Binding={npgsqlChannelBinding};");
                }

                // If connection pooling parameters or endpoint options exist in query
                var endpoint = queryParams["endpoint"];
                if (!string.IsNullOrWhiteSpace(endpoint))
                {
                    sb.Append($"Options='endpoint={endpoint}';");
                }

                return sb.ToString();
            }
            catch
            {
                return trimmed;
            }
        }

        // If it's already key-value format, ensure SSL Mode for remote hosts
        if (!IsLocalhost(trimmed) &&
            !trimmed.Contains("SSL Mode", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.Contains("SslMode", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed.TrimEnd(';') + ";SSL Mode=Require;Trust Server Certificate=true;";
        }

        return trimmed;
    }

    /// <summary>
    /// Masks password in connection strings to enable safe structured logging.
    /// </summary>
    public static string MaskConnectionString(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString)) return "";

        var parts = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var maskedParts = parts.Select(part =>
        {
            var kv = part.Split('=', 2);
            if (kv.Length == 2 && kv[0].Trim().Equals("Password", StringComparison.OrdinalIgnoreCase))
            {
                return $"{kv[0]}=********";
            }
            return part;
        });

        return string.Join(";", maskedParts);
    }
}
