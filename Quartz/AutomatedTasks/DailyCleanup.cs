using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Data;
using Quartz;

namespace OpenOrderSystem.Core.Quartz.AutomatedTasks
{
    public class DailyCleanup : IJob
    {
        private readonly ILogger<DailyCleanup> _logger;
        private readonly TimeSpan _fileAgeLimit = TimeSpan.FromHours(1);

        public DailyCleanup(ILogger<DailyCleanup> logger)
        {
            _logger = logger;
        }

        public async Task Execute(IJobExecutionContext context)
        {
            _logger.LogInformation($"Daily temp file cleanup started at {DateTime.UtcNow}");

            var tempFilesDir = Path.Combine(MediaManagerService.MediaRootPath, "temp");

            try
            {
                var files = new DirectoryInfo(tempFilesDir)
                    .GetFiles()
                    .Where(f => DateTime.Now - f.LastWriteTime > _fileAgeLimit);

                foreach (var file in files)
                {
                    file.Delete();
                }

                _logger.LogInformation("Cleanup completed.");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
            }
        }
    }
}
