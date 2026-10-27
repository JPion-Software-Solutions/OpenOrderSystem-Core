using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage;
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
                // Suspended: see DbProvidersExtensions.NotAvailable for details and restore steps.
                // Restore: bob.UseMySql(connectionString, MySqlServerVersion.AutoDetect(connectionString));
                throw provider.NotAvailable();

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
    /// Determines whether the database is available, missing on a reachable server, or unreachable.
    /// </summary>
    /// <remarks>
    /// A missing database is distinguished from connection failures (bad credentials, unreachable
    /// server) so callers can decide whether migrations should be allowed to create it.
    /// </remarks>
    /// <param name="errors">
    /// When this method returns, contains the reason the database is not available. Empty when it is available.
    /// </param>
    /// <returns>The <see cref="DatabaseStatus"/> of the configured database.</returns>
    public DatabaseStatus GetDatabaseStatus(out string[] errors)
    {
        using var ctx = CreateContext();

        // Exists() is called once: on SQL Server it retries login failures for up to a minute,
        // so a separate CanConnect() call would double the wait on bad credentials.
        try
        {
            if (ctx.GetService<IRelationalDatabaseCreator>().Exists())
            {
                errors = [];
                return DatabaseStatus.Available;
            }

            errors = ["The database does not exist."];
            return DatabaseStatus.Missing;
        }
        catch (Exception ex)
        {
            errors = ex.InnerException is null
                ? [ex.Message]
                : [ex.Message, ex.InnerException.Message];

            return DatabaseStatus.Unreachable;
        }
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
                // Suspended: see DbProvidersExtensions.NotAvailable for details and restore steps.
                // Restore: bob.UseMySql(connectionString, MySqlServerVersion.AutoDetect(connectionString));
                throw provider.NotAvailable();

            case DbProviders.PostgreSQL:
                bob.UseNpgsql(connectionString);
                break;

            default:
                throw new NotSupportedException($"Unsupported DB provider: {provider}");
        }
    }
}

/// <summary>
/// Result of <see cref="DatabaseConnectionTool{TContext}.GetDatabaseStatus"/>.
/// </summary>
public enum DatabaseStatus
{
    /// <summary>The database exists and a connection succeeded.</summary>
    Available,

    /// <summary>The server is reachable and accepted the credentials, but the database does not exist.</summary>
    Missing,

    /// <summary>The server could not be reached or rejected the connection.</summary>
    Unreachable
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

    /// <summary>
    /// MySQL or MariaDB. Common on shared hosting and many Linux server environments.
    /// Currently unavailable; see <see cref="DbProvidersExtensions.NotAvailable"/>.
    /// </summary>
    /// <remarks>
    /// Retained so existing boot configs referencing this value still deserialize and can be
    /// routed to recovery by the preflight check instead of failing to load.
    /// </remarks>
    [System.ComponentModel.Description("MySQL / MariaDB (currently unavailable). Common on shared hosting and many Linux server environments.")]
    MySQL,

    /// <summary>PostgreSQL. Strong general-purpose choice with excellent reliability and features.</summary>
    [System.ComponentModel.Description("PostgreSQL. Strong general-purpose choice with excellent reliability and features.")]
    PostgreSQL
}

/// <summary>
/// Availability helpers for <see cref="DbProviders"/>.
/// </summary>
public static class DbProvidersExtensions
{
    /// <summary>
    /// Determines whether the provider can currently be used at runtime.
    /// </summary>
    /// <param name="provider">The provider to check.</param>
    /// <returns><see langword="true"/> if the provider is available; otherwise <see langword="false"/>.</returns>
    public static bool IsAvailable(this DbProviders provider) => provider != DbProviders.MySQL;

    /// <summary>
    /// Creates the exception thrown when a suspended provider is requested.
    /// </summary>
    /// <remarks>
    /// MySQL / MariaDB support was suspended for the .NET 10 migration (2026-10):
    /// <list type="bullet">
    /// <item>Pomelo.EntityFrameworkCore.MySql has no EF Core 10 release (latest 9.0.0 pins EF Core 9.0.x).</item>
    /// <item>Oracle's MySql.EntityFrameworkCore does not reliably support MariaDB and is GPL licensed.</item>
    /// <item>The existing migrations were generated for SQL Server and would not apply on MySQL regardless.</item>
    /// </list>
    /// To restore: add an EF Core 10 compatible MySQL/MariaDB provider package, restore the
    /// <c>UseMySql</c> calls noted in <see cref="DatabaseConnectionTool{TContext}"/> and
    /// <see cref="DbContextOptionsBuilderExtensions.UseDynamicSQLProvider"/>, add a MySQL-specific
    /// migrations assembly, and remove MySQL from <see cref="IsAvailable"/>.
    /// </remarks>
    /// <param name="provider">The unavailable provider.</param>
    /// <returns>A <see cref="NotSupportedException"/> describing why the provider is unavailable.</returns>
    public static NotSupportedException NotAvailable(this DbProviders provider) =>
        new($"The {provider} database provider is not supported in this version of Open Order System. " +
            "No EF Core 10 compatible MySQL/MariaDB provider is currently available. " +
            "Use SQLite, SQL Server, or PostgreSQL.");
}
