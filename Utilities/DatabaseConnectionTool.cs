using System;
using System.ComponentModel;
using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data;

namespace OpenOrderSystem.Core.Utilities;

public class DatabaseConnectionTool<TContext> where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;

    public DatabaseConnectionTool(DbProviders provider, string connectionString)
    {
        var bob = new DbContextOptionsBuilder<TContext>();        
        
        switch (provider)
        {
            case DbProviders.Sqlite:
                bob.UseSqlite(connectionString);
                break;

            case DbProviders.SQLServer:
                bob.UseSqlServer(connectionString);
                break;

            case DbProviders.MySQL:
                bob.UseMySql(connectionString, MySqlServerVersion.AutoDetect(connectionString));
                break;

            case DbProviders.PostgreSQL:
                bob.UseNpgsql(connectionString);
                break;

            default:
            throw new NotSupportedException($"Unsupported DB provider: {provider}");
        }

        _options = bob.Options;
    }

    public record ConnectionProps(string host, string? username = null, string? password = null, params string[] additional);
    
    private static void ApplyAdditional(DbConnectionStringBuilder b, string[] additional)
    {
        if (additional is null || additional.Length == 0)
            return;

        foreach (var item in additional)
        {
            if (string.IsNullOrWhiteSpace(item))
                continue;

            var idx = item.IndexOf('=');
            if (idx <= 0 || idx == item.Length - 1)
                throw new ArgumentException($"Invalid additional entry '{item}'. Expected 'Key=Value'.");

            var key = item[..idx].Trim();
            var value = item[(idx + 1)..].Trim();

            // Setting via indexer ensures proper escaping when emitted.
            b[key] = value;
        }
    }

    public static string GetConnectionString(DbProviders provider, ConnectionProps connection)
    {
        if (string.IsNullOrWhiteSpace(connection.host))
            throw new ArgumentException("Host/path is required.", nameof(connection));

        // Provider-agnostic connection string builder.
        // It stores key/value pairs and produces a properly escaped connection string.
        var b = new DbConnectionStringBuilder();

        // Add the minimum required keys for each provider.
        // Note: Key names here are the connection-string key names understood by each provider.
        switch (provider)
        {
            case DbProviders.Sqlite:
                // SQLite typically uses "Data Source"
                b["Data Source"] = connection.host;
                break;

            case DbProviders.SQLServer:
                // SQL Server commonly uses these keys
                b["Data Source"] = connection.host;

                // Only set SQL authentication if a username is provided;
                // otherwise caller can use Integrated Security via "additional".
                if (!string.IsNullOrWhiteSpace(connection.username))
                {
                    b["User ID"] = connection.username!;
                    b["Password"] = connection.password ?? string.Empty;
                }

                break;

            case DbProviders.MySQL:
                // MySQL (Pomelo/MySqlConnector) typically uses:
                // Server, User ID, Password, Database, Port, SslMode, etc.
                b["Server"] = connection.host;

                if (!string.IsNullOrWhiteSpace(connection.username))
                {
                    b["User ID"] = connection.username!;
                    b["Password"] = connection.password ?? string.Empty;
                }

                break;

            case DbProviders.PostgreSQL:
                // Npgsql typically uses:
                // Host, Username, Password, Database, Port, etc.
                b["Host"] = connection.host;

                if (!string.IsNullOrWhiteSpace(connection.username))
                {
                    b["Username"] = connection.username!;
                    b["Password"] = connection.password ?? string.Empty;
                }

                break;

            default:
                throw new NotSupportedException($"Unsupported DB provider: {provider}");
        }

        // Apply additional settings last so callers can override defaults / add Database/Port/etc.
        // Expected format: "Key=Value"
        ApplyAdditional(b, connection.additional);

        // DbConnectionStringBuilder outputs a fully formatted connection string.
        return b.ConnectionString;
    }

    private TContext CreateContext()
    {
        // Requires: TContext has a ctor that accepts DbContextOptions<TContext> (or DbContextOptions).
        return Activator.CreateInstance(typeof(TContext), _options) as TContext
            ?? throw new InvalidOperationException(
                $"Unable to create DbContext instance of type {typeof(TContext).FullName}. " +
                $"Ensure it has a constructor accepting DbContextOptions<{typeof(TContext).Name}>.");
    }

    public bool CanConnect(out string[] errors)
    {
        var e = new List<string>();
        using var ctx = CreateContext();

        try
        {
            if (!ctx.Database.CanConnect())
            {
                e.Add("Failed to connect to the database.");
            }
        }
        catch (Exception ex)
        {
            e.Add(ex.Message);

            if (ex.InnerException != null)
                e.Add(ex.InnerException.Message);
        }

        errors = e.ToArray();

        if (e.Any()) return false;
        else return true;
    }
}

public static class DbContextOptionsBuilderExtensions
{    public static void UseDynamicSQLProvider(this DbContextOptionsBuilder bob, Configuration config, string connectionPrefix = "")
    {
        var providerKey = string.IsNullOrWhiteSpace(connectionPrefix)
            ? "DB_PROVIDER" 
            :  $"{connectionPrefix}:DB_PROVIDER";
        
        var connectionStringKey = string.IsNullOrWhiteSpace(connectionPrefix)
            ? "DB_CONNECTION_STRING" 
            : $"{connectionPrefix}:DB_CONNECTION_STRING";
        
        var provider = config.GetConfig<DbProviders>(providerKey);
        var connectionString = config.GetConfig<string>(connectionStringKey);

        switch (provider)
        {
            case DbProviders.Sqlite:
                bob.UseSqlite(connectionString);
                break;

            case DbProviders.SQLServer:
                bob.UseSqlServer(connectionString);
                break;

            case DbProviders.MySQL:
                bob.UseMySql(connectionString, MySqlServerVersion.AutoDetect(connectionString));
                break;

            case DbProviders.PostgreSQL:
                bob.UseNpgsql(connectionString);
                break;

            default:
            throw new NotSupportedException($"Unsupported DB provider: {provider}");
        }
    }
}

/// <summary>
/// Identifies which database provider EF Core should use at runtime.
///
/// This enum is intended for *deployment-time configuration* (setup wizard / config file),
/// enabling the application to support multiple DB engines without changing code.
/// </summary>
public enum DbProviders
{
    [Description("SQLite (recommended for small, low-traffic installs). Stores data in one local file; simplest setup.")]
    Sqlite,

    [Description("SQL Server. Great choice for Windows environments and existing Microsoft infrastructure.")]
    SQLServer,

    [Description("MySQL / MariaDB. Common on shared hosting and many Linux server environments.")]
    MySQL,

    [Description("PostgreSQL. Strong general-purpose choice with excellent reliability and features.")]
    PostgreSQL
}