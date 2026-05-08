using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Bootstrapper;

namespace OpenOrderSystem.Core.Utilities;

/// <summary>
/// Provides design-time and preflight database connectivity utilities for a specific
/// <see cref="DbContext"/> type, independent of the application's DI container.
/// </summary>
/// <typeparam name="TContext">The <see cref="DbContext"/> type to test connectivity for.</typeparam>
public class DatabaseConnectionTool<TContext> where TContext : DbContext
{
    private readonly DbContextOptions<TContext> _options;

    /// <summary>
    /// Initializes a new instance of <see cref="DatabaseConnectionTool{TContext}"/> using
    /// the specified provider and connection string.
    /// </summary>
    /// <param name="provider">The database provider to configure.</param>
    /// <param name="connectionString">The connection string to use.</param>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="provider"/> is not a supported provider.</exception>
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

    /// <summary>
    /// Represents the minimum set of properties needed to build a provider-specific connection string.
    /// </summary>
    /// <param name="host">The server host, IP address, or file path (for SQLite).</param>
    /// <param name="username">Optional username for SQL authentication. Omit to use integrated/trust authentication.</param>
    /// <param name="password">Optional password for SQL authentication.</param>
    /// <param name="additional">
    /// Additional key-value pairs in <c>Key=Value</c> format appended to the connection string
    /// (e.g., <c>"Database=mydb"</c>, <c>"Port=5432"</c>). Applied after base properties,
    /// so these can override defaults.
    /// </param>
    public record ConnectionProps(string host, string? username = null, string? password = null, params string[] additional);

    /// <summary>
    /// Builds a provider-appropriate connection string from the supplied <see cref="ConnectionProps"/>.
    /// </summary>
    /// <param name="provider">The target database provider.</param>
    /// <param name="connection">The connection properties to encode.</param>
    /// <returns>A fully formatted connection string for the specified provider.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="connection"/> has a null or empty host.</exception>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="provider"/> is not a supported provider.</exception>
    public static string GetConnectionString(DbProviders provider, ConnectionProps connection)
    {
        if (string.IsNullOrWhiteSpace(connection.host))
            throw new ArgumentException("Host/path is required.", nameof(connection));

        var b = new DbConnectionStringBuilder();

        switch (provider)
        {
            case DbProviders.Sqlite:
                b["Data Source"] = connection.host;
                break;

            case DbProviders.SQLServer:
                b["Data Source"] = connection.host;

                if (!string.IsNullOrWhiteSpace(connection.username))
                {
                    b["User ID"] = connection.username!;
                    b["Password"] = connection.password ?? string.Empty;
                }

                break;

            case DbProviders.MySQL:
                b["Server"] = connection.host;

                if (!string.IsNullOrWhiteSpace(connection.username))
                {
                    b["User ID"] = connection.username!;
                    b["Password"] = connection.password ?? string.Empty;
                }

                break;

            case DbProviders.PostgreSQL:
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

        ApplyAdditional(b, connection.additional);

        return b.ConnectionString;
    }

    /// <summary>
    /// Attempts to open a connection to the database using the configured options.
    /// </summary>
    /// <param name="errors">
    /// When this method returns, contains any error messages encountered during the connection attempt.
    /// Empty when the connection succeeds.
    /// </param>
    /// <returns><see langword="true"/> if the connection succeeded; otherwise <see langword="false"/>.</returns>
    public bool CanConnect(out string[] errors)
    {
        var e = new List<string>();
        using var ctx = CreateContext();

        try
        {
            if (!ctx.Database.CanConnect())
                e.Add("Failed to connect to the database.");
        }
        catch (Exception ex)
        {
            e.Add(ex.Message);

            if (ex.InnerException != null)
                e.Add(ex.InnerException.Message);
        }

        errors = e.ToArray();
        return !e.Any();
    }

    /// <summary>
    /// Applies additional <c>Key=Value</c> entries to an existing <see cref="DbConnectionStringBuilder"/>.
    /// </summary>
    /// <param name="b">The builder to apply entries to.</param>
    /// <param name="additional">Entries in <c>Key=Value</c> format.</param>
    /// <exception cref="ArgumentException">Thrown when an entry does not conform to <c>Key=Value</c> format.</exception>
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

            b[item[..idx].Trim()] = item[(idx + 1)..].Trim();
        }
    }

    /// <summary>
    /// Creates a <typeparamref name="TContext"/> instance using the configured options via reflection.
    /// </summary>
    /// <returns>A new <typeparamref name="TContext"/> instance.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when <typeparamref name="TContext"/> does not have a constructor accepting
    /// <see cref="DbContextOptions{TContext}"/>.
    /// </exception>
    private TContext CreateContext()
    {
        return Activator.CreateInstance(typeof(TContext), _options) as TContext
            ?? throw new InvalidOperationException(
                $"Unable to create DbContext instance of type {typeof(TContext).FullName}. " +
                $"Ensure it has a constructor accepting DbContextOptions<{typeof(TContext).Name}>.");
    }
}

/// <summary>
/// Extension methods for <see cref="DbContextOptionsBuilder"/> providing dynamic provider
/// selection driven by the OOS bootstrap configuration.
/// </summary>
public static class DbContextOptionsBuilderExtensions
{
    /// <summary>
    /// Configures the <see cref="DbContextOptionsBuilder"/> using the provider and connection string
    /// read from the OOS bootstrap configuration.
    /// </summary>
    /// <param name="bob">The options builder to configure.</param>
    /// <param name="config">The bootstrap configuration to read from.</param>
    /// <param name="connectionPrefix">
    /// Optional prefix used to namespace the configuration keys. When provided, keys are read as
    /// <c>{prefix}:DB_PROVIDER</c> and <c>{prefix}:DB_CONNECTION_STRING</c>. When omitted, the
    /// unversioned keys <c>DB_PROVIDER</c> and <c>DB_CONNECTION_STRING</c> are used (V2 / permanent contexts).
    /// </param>
    /// <exception cref="NotSupportedException">Thrown when the configured provider is not supported.</exception>
    public static void UseDynamicSQLProvider(this DbContextOptionsBuilder bob, Configuration config, string connectionPrefix = "")
    {
        var providerKey = string.IsNullOrWhiteSpace(connectionPrefix)
            ? "DB_PROVIDER"
            : $"{connectionPrefix}:DB_PROVIDER";

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
/// </summary>
/// <remarks>
/// Intended for deployment-time configuration via the setup wizard or bootstrap config file,
/// allowing the application to support multiple database engines without code changes.
/// </remarks>
public enum DbProviders
{
    /// <summary>SQLite. Stores data in a single local file. Recommended for small, low-traffic installs.</summary>
    [System.ComponentModel.Description("SQLite (recommended for small, low-traffic installs). Stores data in one local file; simplest setup.")]
    Sqlite,

    /// <summary>Microsoft SQL Server. Recommended for Windows environments and existing Microsoft infrastructure.</summary>
    [System.ComponentModel.Description("SQL Server. Great choice for Windows environments and existing Microsoft infrastructure.")]
    SQLServer,

    /// <summary>MySQL or MariaDB. Common on shared hosting and many Linux server environments.</summary>
    [System.ComponentModel.Description("MySQL / MariaDB. Common on shared hosting and many Linux server environments.")]
    MySQL,

    /// <summary>PostgreSQL. Strong general-purpose choice with excellent reliability and features.</summary>
    [System.ComponentModel.Description("PostgreSQL. Strong general-purpose choice with excellent reliability and features.")]
    PostgreSQL
}
