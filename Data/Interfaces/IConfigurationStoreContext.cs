using System;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data.DataModels;

namespace OpenOrderSystem.Core.Data.Interfaces;

public interface IConfigurationStoreContext
{
    public DbSet<SystemConfig> Confguration { get; set; }

    public Task<int> SaveChangesAsync();
}
