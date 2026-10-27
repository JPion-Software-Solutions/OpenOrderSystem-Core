using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2;

namespace OpenOrderSystem.Core.Data.Providers;

// Provider-specific context types.
//
// EF Core ties a migration set to a context type (via [DbContext(typeof(...))]), so each
// supported database provider gets an empty subclass that owns its own migrations under
// Migrations/<Context>/<Provider>/. The model itself lives entirely in the base context;
// these types exist only to separate migration sets. Application code keeps depending on
// the base types (OosDbContext, DataProtectionKeyContext); see ProviderDbContextRegistration.
//
// To add a provider: add a subclass pair here, a design-time factory pair in
// ProviderDesignTimeFactories.cs, a case in ProviderDbContextRegistration, then generate
// its baseline migrations with scripts/add-migration.sh.

public sealed class SqlServerOosDbContext(DbContextOptions<SqlServerOosDbContext> options)
    : OosDbContext(options);

public sealed class SqliteOosDbContext(DbContextOptions<SqliteOosDbContext> options)
    : OosDbContext(options);

public sealed class NpgsqlOosDbContext(DbContextOptions<NpgsqlOosDbContext> options)
    : OosDbContext(options);

public sealed class SqlServerDataProtectionKeyContext(DbContextOptions<SqlServerDataProtectionKeyContext> options)
    : DataProtectionKeyContext(options);

public sealed class SqliteDataProtectionKeyContext(DbContextOptions<SqliteDataProtectionKeyContext> options)
    : DataProtectionKeyContext(options);

public sealed class NpgsqlDataProtectionKeyContext(DbContextOptions<NpgsqlDataProtectionKeyContext> options)
    : DataProtectionKeyContext(options);
