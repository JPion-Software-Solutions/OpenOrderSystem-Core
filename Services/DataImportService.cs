using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using System.Security.Permissions;

namespace OpenOrderSystem.Core.Services
{
    public class DataImportService
    {
        private readonly ApplicationDbContext _destContext;
        private ApplicationDbContext? _sourceContext;
        private readonly ILogger<DataImportService>? _logger;

        public DataImportService(ApplicationDbContext context, ILogger<DataImportService>? logger)
        {
            _destContext = context;
            _logger = logger;
        }

        public bool IsReady { get; private set; } = false;
        public bool IsErrored => Errors.Any();

        public List<string> Errors { get; private set; } = new List<string>();

        public ApplicationDbContext Source => _sourceContext ?? throw new ArgumentNullException($"No source data loaded! Call OpenSource first.");

        public DataImportService OpenSource(string connectionString, SourceArchetecture archetecture = SourceArchetecture.SQLite)
        {
            switch (archetecture)
            {
                case SourceArchetecture.SQLite:
                    if (!File.Exists(connectionString))
                    {
                        IsReady = false;
                        Errors.Add("SQLite db file not found.");
                        break;
                    }

                    connectionString = $"DATA SOURCE={connectionString}";
                    var optionsBob = new DbContextOptionsBuilder<ApplicationDbContext>();
                    optionsBob.UseSqlite(connectionString);

                    try
                    {
                        _sourceContext = new ApplicationDbContext(optionsBob.Options);
                        IsReady = true;
                    }
                    catch (Exception ex)
                    {
                        IsReady = false;
                        Errors.Add($"Failed to open database. Exception thrown:{ex.Message}");
                    }

                    break;
            }

            return this;
        }

        public DataImportService MergeData(params string[] tables)
        {
            if (!IsReady)
            {
                Errors.Add("Unable to merge data. Source not ready.");
                return this;
            }

            //merging all tables
            if (tables == null || tables.Length == 0)
            {
                if (_sourceContext == null) throw new ArgumentNullException();

                foreach (var sourcePropInfo in _sourceContext.GetType().GetProperties())
                {
                    var destPropInfo = _destContext.GetType().GetProperty(sourcePropInfo.Name);
                    if (sourcePropInfo.PropertyType.Name == "DbSet`1" && destPropInfo != null)
                    {
                        var dumdum = 0;
                    }
                }
            }

            //merging specific tables
            else
            {
                foreach (var table in tables)
                {

                }
            }

            return this;
        }
    }

    public enum SourceArchetecture
    {
        SQLite,
        SQLServer
    }
}
