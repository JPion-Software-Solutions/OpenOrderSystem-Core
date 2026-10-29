using OpenOrderSystem.Core.Services.Ordering.Stages.Interfaces;
using Quartz;

namespace OpenOrderSystem.Core.Quartz.AutomatedTasks;

/// <summary>
/// System-internal job (every 10 seconds) that promotes due queued orders and auto-advances orders whose
/// stage time has elapsed. Not registered yet; it moves onto the scheduling service once that exists.
/// </summary>
[DisallowConcurrentExecution]
public class OrderStageJob : IJob
{
    private readonly IServiceScopeFactory _serviceScopeFactory;
    private readonly ILogger<OrderStageJob> _logger;

    public OrderStageJob(IServiceScopeFactory serviceScopeFactory, ILogger<OrderStageJob> logger)
    {
        _serviceScopeFactory = serviceScopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        await using var scope = _serviceScopeFactory.CreateAsyncScope();
        var stages = scope.ServiceProvider.GetRequiredService<IOrderStageService>();

        var promoted = await stages.PromoteDueOrdersAsync();
        var advanced = await stages.AdvanceDueOrdersAsync();

        if (promoted > 0 || advanced > 0)
            _logger.LogDebug("Order stage job: promoted {Promoted}, advanced {Advanced}.", promoted, advanced);
    }
}
