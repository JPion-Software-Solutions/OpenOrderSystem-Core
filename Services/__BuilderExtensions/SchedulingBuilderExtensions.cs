using OpenOrderSystem.Core.Services.QuartzScheduler;
using OpenOrderSystem.Core.Services.QuartzScheduler.Interfaces;

namespace OpenOrderSystem.Core.Services.__BuilderExtensions;

/// <summary>
/// The scheduling service group: the service that owns the <see cref="IOosJob"/> schedule.
/// </summary>
/// <remarks>
/// Needs Quartz itself (<c>AddQuartz</c> and its hosted service), which the boot mode registers along with
/// the post-boot task runner. The jobs are found by scanning, so nothing here lists them.
/// </remarks>
public static class SchedulingBuilderExtensions
{
    /// <summary>Registers <see cref="IJobScheduleService"/>.</summary>
    public static WebApplicationBuilder AddOosSchedulingServices(this WebApplicationBuilder bob)
    {
        // Singleton: the instance holds the lock that keeps rebuilds from overlapping.
        bob.Services.AddSingleton<IJobScheduleService, QuartzJobScheduleService>();

        return bob;
    }
}
