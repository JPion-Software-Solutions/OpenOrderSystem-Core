using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace OpenOrderSystem.Core.Data.Providers;

// Design-time factories used by `dotnet ef` for the provider-specific contexts.
//
// Each factory pins its provider and uses a placeholder connection string, so migrations
// for every provider can be generated on any machine without that database being present
// (`migrations add`, `migrations remove` and `has-pending-model-changes` never connect).
// Don't use `dotnet ef database update` with these contexts; the app applies migrations
// at startup against the configured database.

internal static class DesignTimeConnections
{
    public const string SqlServer = "Server=localhost;Database=OosDesignTime;Trusted_Connection=True;TrustServerCertificate=True";
    public const string Sqlite = "Data Source=oos-design-time.db";
    public const string Npgsql = "Host=localhost;Database=oos_design_time";
}

public class SqlServerOosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerOosDbContext>
{
    public SqlServerOosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerOosDbContext>().UseSqlServer(DesignTimeConnections.SqlServer).Options);
}

public class SqliteOosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<SqliteOosDbContext>
{
    public SqliteOosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteOosDbContext>().UseSqlite(DesignTimeConnections.Sqlite).Options);
}

public class NpgsqlOosDbContextDesignTimeFactory : IDesignTimeDbContextFactory<NpgsqlOosDbContext>
{
    public NpgsqlOosDbContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NpgsqlOosDbContext>().UseNpgsql(DesignTimeConnections.Npgsql).Options);
}

public class SqlServerDataProtectionKeyContextDesignTimeFactory : IDesignTimeDbContextFactory<SqlServerDataProtectionKeyContext>
{
    public SqlServerDataProtectionKeyContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqlServerDataProtectionKeyContext>().UseSqlServer(DesignTimeConnections.SqlServer).Options);
}

public class SqliteDataProtectionKeyContextDesignTimeFactory : IDesignTimeDbContextFactory<SqliteDataProtectionKeyContext>
{
    public SqliteDataProtectionKeyContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<SqliteDataProtectionKeyContext>().UseSqlite(DesignTimeConnections.Sqlite).Options);
}

public class NpgsqlDataProtectionKeyContextDesignTimeFactory : IDesignTimeDbContextFactory<NpgsqlDataProtectionKeyContext>
{
    public NpgsqlDataProtectionKeyContext CreateDbContext(string[] args) =>
        new(new DbContextOptionsBuilder<NpgsqlDataProtectionKeyContext>().UseNpgsql(DesignTimeConnections.Npgsql).Options);
}
