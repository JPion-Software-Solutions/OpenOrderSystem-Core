using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OpenOrderSystem.Core.Data
{
    public class DataProtectionKeyContext : DbContext, IDataProtectionKeyContext
    {
        public DataProtectionKeyContext(DbContextOptions<DataProtectionKeyContext> options)
            : base(options)
        {
        }

        /// <summary>
        /// Used by the provider-specific subclasses in <see cref="OpenOrderSystem.Core.Data.Providers"/>,
        /// which each own a separate migration set.
        /// </summary>
        protected DataProtectionKeyContext(DbContextOptions options)
            : base(options)
        {
        }
        public DbSet<DataProtectionKey> DataProtectionKeys { get; set; }
    }
}
