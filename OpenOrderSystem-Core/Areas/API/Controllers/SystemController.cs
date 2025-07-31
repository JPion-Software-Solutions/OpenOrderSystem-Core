using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Extensions;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{

    [Area("API")]
    [ApiController]
    [Route("API/System/{action}")]
    public class SystemController : ControllerBase
    {
        private StaffTerminalMonitoringService _staffTMS;
        private ConfigurationService _configService;

        public static string Version { get; set; } = string.Empty;
        public static DateTime SystemBoot { get; set; }

        public SystemController(StaffTerminalMonitoringService staffTMS, ConfigurationService configService) 
        { 
            _staffTMS = staffTMS;
            _configService = configService;
        }

        public IResult Ping()
        {
            return Results.Ok(new
            {
                Version,
                UptimeRaw = (DateTime.Now - SystemBoot).TotalMilliseconds,
                Uptime = (DateTime.Now - SystemBoot).ToFriendlyString()
            });
        }

        public IResult AcceptingOrders()
        {
            var acceptingOrders = _staffTMS.TerminalActive && _configService.Settings.AcceptingOrders;

            var response = new
            {
                acceptingOrders,
                reason = acceptingOrders ? "N/A" :
                    !_configService.Settings.AcceptingOrders ? "The kitchen is currently closed." :
                    "Staff terminal unavailable to receive orders.",
                terminalOnline = _staffTMS.TerminalActive,
                withinBusinessHours = _configService.Settings.AcceptingOrders
            };

            if (acceptingOrders) 
                return Results.Ok(response);
            else
                return Results.Json(response, statusCode: 503);
        }

    }
}
