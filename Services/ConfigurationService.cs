using Microsoft.CodeAnalysis.CSharp.Syntax;
using NuGet.Protocol;
using OpenOrderSystem.Core.Areas.Configuration.Models;
using System.Text.Json;
using System.Web.Helpers;

namespace OpenOrderSystem.Core.Services
{
    /// <summary>
    /// Service used to access and/or modify the site configuration.
    /// </summary>
    public class ConfigurationService
    {
        private SiteConfig _siteConfig;
        private readonly string _configurationFilename;

        /// <summary>
        /// Loads current site configuration or creates a default configuration
        /// </summary>
        /// <param name="configurationFilePath"></param>
        public ConfigurationService(string configurationFilePath = "siteSettings.json")
        {
            _configurationFilename = configurationFilePath;
            _siteConfig = new SiteConfig();

            //attempt to load the configuration file or create a new default configuration.
            LoadFromFile();
        }

        /// <summary>
        /// In-memory representation of the current configuration.
        /// </summary>
        public SiteConfig Settings
        {
            get => _siteConfig;
        }

        /// <summary>
        /// Undo all changes made to the configuration file and start from most recent save.
        /// </summary>
        public void AbandonChanges()
        {
            LoadFromFile();
        }

        /// <summary>
        /// Save any changes made to the configuration file.
        /// </summary>
        public void SaveChages()
        {
            if (!Directory.Exists("config")) ;
            Directory.CreateDirectory("config");

            var jsonPayload = JsonSerializer.Serialize(Settings);
            File.WriteAllText(Path.Combine("config", _configurationFilename), jsonPayload);
        }

        /// <summary>
        /// Make a backup of the current configuration.
        /// </summary>
        /// <param name="filename">name of the backup</param>
        /// <exception cref="InvalidOperationException">Thrown when the backup filename 
        /// already exists to prevent overrwiting an existing backup</exception>
        public void MakeBackup(string filename)
        {
            if (!File.Exists(filename))
            {
                if (!Directory.Exists(Path.Combine("config", "backup")))
                    Directory.CreateDirectory(Path.Combine("config", "backup"));

                File.WriteAllText(Path.Combine("backup", filename), Json.Encode(_siteConfig));
            }
            else
            {
                //thrown to prevent overriting backups by mistake
                throw new InvalidOperationException($"The backup file {filename} already exists!");
            }
        }

        /// <summary>
        /// Load a backup configuration file
        /// </summary>
        /// <param name="filename">backup filename to load</param>
        /// <exception cref="InvalidOperationException">Thrown when the backup filename 
        /// was not found</exception>
        public void LoadBackup(string filename)
        {
            if (!File.Exists(Path.Combine("config", "backup", filename)))
                throw new InvalidOperationException($"Backup file '{filename}' not found!");

            LoadFromFile(Path.Combine("backup", filename));
        }

        /// <summary>
        /// Load a configuration from file or create a default config.
        /// </summary>
        /// <param name="filepath"></param>
        private void LoadFromFile(string? filepath = null)
        {
            //attempt to load the configuration file or create a new default configuration.
            if (File.Exists(Path.Combine("config", _configurationFilename)))
            {
                var fileLocation = Path.Combine("config", filepath ?? _configurationFilename);
                var configFile = File.ReadAllText(fileLocation);
                _siteConfig = JsonSerializer.Deserialize<SiteConfig>(configFile) ??
                    new SiteConfig();
            }
            else
            {
                _siteConfig = new SiteConfig();
            }
        }
    }
}
