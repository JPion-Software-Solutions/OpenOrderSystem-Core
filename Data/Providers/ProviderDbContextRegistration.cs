using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Utilities;

namespace OpenOrderSystem.Core.Data.Providers;

/// <summary>
/// Registers the provider-specific V2 and data protection contexts for the configured
/// <c>DB_PROVIDER</c>, exposed to the rest of the application as the base context types.
/// </summary>
public static class ProviderDbContextRegistration
{
    /// <summary>
    /// Registers <see cref="OosDbContext"/> (scoped and via <see cref="IDbContextFactory{TContext}"/>)
    /// and <see cref="DataProtectionKeyContext"/>, backed by the subclasses that match the configured provider.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="config">The bootstrap configuration supplying <c>DB_PROVIDER</c> and <c>DB_CONNECTION_STRING</c>.</param>
    /// <exception cref="NotSupportedException">Thrown when the configured provider is unavailable or unsupported.</exception>
    public static IServiceCollection AddOosDbContexts(this IServiceCollection services, Configuration config)
    {
        var provider = config.GetConfig<DbProviders>("DB_PROVIDER");

        return provider switch
        {
            DbProviders.SQLServer => services.AddOosDbContexts<SqlServerOosDbContext, SqlServerDataProtectionKeyContext>(config),
            DbProviders.Sqlite => services.AddOosDbContexts<SqliteOosDbContext, SqliteDataProtectionKeyContext>(config),
            DbProviders.PostgreSQL => services.AddOosDbContexts<NpgsqlOosDbContext, NpgsqlDataProtectionKeyContext>(config),
            DbProviders.MySQL => throw provider.NotAvailable(),
            _ => throw new NotSupportedException($"Unsupported DB provider: {provider}")
        };
    }

    private static IServiceCollection AddOosDbContexts<TOosContext, TKeyContext>(this IServiceCollection services, Configuration config)
        where TOosContext : OosDbContext
        where TKeyContext : DataProtectionKeyContext
    {
        services.AddDbContext<DataProtectionKeyContext, TKeyContext>(options => options.UseDynamicSQLProvider(config));

        services.AddDbContextFactory<TOosContext>(options =>
        {
            options.UseDynamicSQLProvider(config);
            options.EnableDetailedErrors();
        });

        services.AddSingleton<IDbContextFactory<OosDbContext>, ForwardingDbContextFactory<TOosContext>>();
        services.AddScoped<OosDbContext>(sp => sp.GetRequiredService<IDbContextFactory<OosDbContext>>().CreateDbContext());

        return services;
    }

    /// <summary>
    /// Exposes a provider-specific context factory as <see cref="IDbContextFactory{OosDbContext}"/>.
    /// </summary>
    private sealed class ForwardingDbContextFactory<TOosContext>(IDbContextFactory<TOosContext> inner)
        : IDbContextFactory<OosDbContext>
        where TOosContext : OosDbContext
    {
        public OosDbContext CreateDbContext() => inner.CreateDbContext();
    }
}
