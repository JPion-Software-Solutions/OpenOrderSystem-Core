using System;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;

namespace OpenOrderSystem.Core.Data.Interfaces;

public interface IConfigurationStoreContext
{
    public DbSet<SystemConfig> Configuration { get; set; }

    public Task<int> SaveChangesAsync();

    public int SaveChanges();
}
