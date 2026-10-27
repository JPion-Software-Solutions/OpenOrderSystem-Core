using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.Interfaces;

namespace OpenOrderSystem.Core.Areas.API.Legacy.Controllers
{

    [Area("API")]
    [ApiController]
    [Route("api/legacy/System/{action}")]
    public class SystemController : ControllerBase
    {
        private StaffTerminalMonitoringService _staffTMS;
        private ConfigurationService _configService;
        private ITimeNormalizationService _timeService;

        public static string Version { get; set; } = string.Empty;
        public static DateTime SystemBoot { get; set; }

        public SystemController(StaffTerminalMonitoringService staffTMS, ConfigurationService configService,
            ITimeNormalizationService timeService)
        {
            _staffTMS = staffTMS;
            _configService = configService;
            _timeService = timeService;
        }

        [Route("/api/system/ping")]
        public IResult PingLegacyRoute() => Ping();
        
        [Route("/ping")]
        public IResult PingNewRoute() => Ping();
        
        public IResult Ping()
        {
            return Results.Ok(new
            {
                Version,
                UptimeRaw = (DateTime.Now - SystemBoot).TotalMilliseconds,
                Uptime = (DateTime.Now - SystemBoot).ToFriendlyString()
            });
        }

        public async Task<IResult> AcceptingOrders()
        {
            var currentLocalTime = await _timeService.GetLocalTime();
            var withinBusinessHours = _configService.Settings.IsAcceptingOrders(currentLocalTime);
            var acceptingOrders = _staffTMS.TerminalActive && withinBusinessHours;

            var response = new
            {
                acceptingOrders,
                reason = acceptingOrders ? "N/A" :
                    !withinBusinessHours ? "The kitchen is currently closed." :
                    "Staff terminal unavailable to receive orders.",
                terminalOnline = _staffTMS.TerminalActive,
                withinBusinessHours
            };

            if (acceptingOrders) 
                return Results.Ok(response);
            else
                return Results.Json(response, statusCode: 503);
        }

    }
}
