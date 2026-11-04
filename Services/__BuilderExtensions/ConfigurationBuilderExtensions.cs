using OpenOrderSystem.Core.Data.DataModels.V2;
using OpenOrderSystem.Core.Services.Config;
using OpenOrderSystem.Core.Services.Config.Interfaces;
using OpenOrderSystem.Core.Services.Keyring;
using OpenOrderSystem.Core.Services.Keyring.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

public static class ConfigurationBuilderExtensions
{
    /// <summary>
    /// Registers the keyring and the database-backed configuration store.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The keyring must already be open. A boot mode that uses this group opens it in its preflight
    /// (<see cref="KeyringBuilder.Build"/>) and falls back to recovery if that fails; there is no
    /// degraded mode without a keyring.
    /// </para>
    /// <para>
    /// The config store reads from <see cref="OosDbContext"/>, so call this after
    /// <see cref="DatabaseBuilderExtensions.AddOosDatabaseServices"/>.
    /// </para>
    /// </remarks>
    /// <param name="bob">The web application builder.</param>
    /// <param name="keyring">The keyring opened during preflight.</param>
    public static WebApplicationBuilder AddOosConfigurationServices(this WebApplicationBuilder bob, KeyringBuildResult keyring)
    {
        ArgumentNullException.ThrowIfNull(keyring);

        // One keyring for the process. The admin path gets IKeyringService; everything else
        // depends on IReadOnlyKeyringService.
        bob.Services.AddSingleton(keyring.Keyring);
        bob.Services.AddSingleton<IKeyringService>(sp => sp.GetRequiredService<FileKeyringService>());
        bob.Services.AddSingleton<IReadOnlyKeyringService>(sp => sp.GetRequiredService<FileKeyringService>());

        // One store per scope, shared by both interfaces it implements.
        bob.Services.AddScoped<DatabaseConfigurationStore<OosDbContext>>();
        bob.Services.AddScoped<IConfigurationStore>(sp => sp.GetRequiredService<DatabaseConfigurationStore<OosDbContext>>());
        bob.Services.AddScoped<ITimeNormalizationService>(sp => sp.GetRequiredService<DatabaseConfigurationStore<OosDbContext>>());

        return bob;
    }
}
