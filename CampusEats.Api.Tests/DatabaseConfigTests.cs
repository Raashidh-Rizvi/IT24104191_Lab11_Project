using CampusEats.Api.Data;
using Xunit;

namespace CampusEats.Api.Tests;

public class DatabaseConfigTests
{
    [Fact]
    public void FormatPostgreSqlConnectionString_WithNeonUri_ParsesCorrectly()
    {
        var input = "postgresql://neondb_owner:npg_SecretPass123@ep-cool-fog-123456.us-east-2.aws.neon.tech/neondb?sslmode=require";
        var result = DatabaseConfig.FormatPostgreSqlConnectionString(input);

        Assert.Contains("Host=ep-cool-fog-123456.us-east-2.aws.neon.tech;", result);
        Assert.Contains("Port=5432;", result);
        Assert.Contains("Database=neondb;", result);
        Assert.Contains("Username=neondb_owner;", result);
        Assert.Contains("Password=npg_SecretPass123;", result);
        Assert.Contains("SSL Mode=Require;", result);
        Assert.Contains("Trust Server Certificate=true;", result);
    }

    [Fact]
    public void FormatPostgreSqlConnectionString_WithPasswordContainingColon_PreservesPassword()
    {
        var input = "postgresql://myuser:p%3Aass%3Aword@ep-test.neon.tech/neondb?sslmode=require";
        var result = DatabaseConfig.FormatPostgreSqlConnectionString(input);

        Assert.Contains("Username=myuser;", result);
        Assert.Contains("Password=p:ass:word;", result);
    }

    [Fact]
    public void FormatPostgreSqlConnectionString_WithNeonPooledEndpoint_PreservesEndpoint()
    {
        var input = "postgresql://myuser:mypass@ep-cool-fog-123456-pooler.us-east-2.aws.neon.tech/neondb?sslmode=require&endpoint=ep-cool-fog-123456";
        var result = DatabaseConfig.FormatPostgreSqlConnectionString(input);

        Assert.Contains("Host=ep-cool-fog-123456-pooler.us-east-2.aws.neon.tech;", result);
        Assert.Contains("Options='endpoint=ep-cool-fog-123456';", result);
    }

    [Fact]
    public void IsPlaceholder_DetectsPlaceholdersCorrectly()
    {
        Assert.True(DatabaseConfig.IsPlaceholder("YOUR_POSTGRESQL_CONNECTION_STRING_HERE"));
        Assert.True(DatabaseConfig.IsPlaceholder("postgresql://YOUR_NEON_USER@YOUR_NEON_HOST/neondb"));
        Assert.True(DatabaseConfig.IsPlaceholder(""));
        Assert.True(DatabaseConfig.IsPlaceholder("   "));
        Assert.True(DatabaseConfig.IsPlaceholder(null));

        Assert.False(DatabaseConfig.IsPlaceholder("postgresql://actual_user:actual_pass@ep-real.neon.tech/neondb"));
        Assert.False(DatabaseConfig.IsPlaceholder("Host=localhost;Database=campuseats;Username=postgres;Password=postgres"));
    }

    [Fact]
    public void MaskConnectionString_HidesPassword()
    {
        var conn = "Host=myhost;Port=5432;Database=mydb;Username=myuser;Password=super_secret_password;SSL Mode=Require;";
        var masked = DatabaseConfig.MaskConnectionString(conn);

        Assert.DoesNotContain("super_secret_password", masked);
        Assert.Contains("Password=********", masked);
        Assert.Contains("Username=myuser", masked);
        Assert.Contains("Host=myhost", masked);
    }

    [Fact]
    public void FormatPostgreSqlConnectionString_WithRemoteKeyValue_EnsuresSsl()
    {
        var input = "Host=ep-test.neon.tech;Port=5432;Database=neondb;Username=user;Password=pass";
        var result = DatabaseConfig.FormatPostgreSqlConnectionString(input);

        Assert.Contains("SSL Mode=Require;", result);
        Assert.Contains("Trust Server Certificate=true;", result);
    }

    [Fact]
    public void FormatPostgreSqlConnectionString_WithLocalhost_DoesNotForceSsl()
    {
        var input = "Host=localhost;Port=5432;Database=campuseats;Username=postgres;Password=postgres";
        var result = DatabaseConfig.FormatPostgreSqlConnectionString(input);

        Assert.DoesNotContain("SSL Mode=Require", result);
    }
}
