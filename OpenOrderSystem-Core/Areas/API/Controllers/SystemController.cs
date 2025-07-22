using Microsoft.AspNetCore.Mvc;
using Microsoft.CodeAnalysis.Elfie.Extensions;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{

    [Area("API")]
    [ApiController]
    [Route("API/System/{action}")]
    public class SystemController : ControllerBase
    {
        public static string Version { get; set; } = string.Empty;
        public static DateTime SystemBoot { get; set; }

        public IResult Ping()
        {
            return Results.Ok(new
            {
                Version,
                UptimeRaw = (DateTime.Now - SystemBoot).TotalMilliseconds,
                Uptime = (DateTime.Now - SystemBoot).ToFriendlyString()
            });
        }
    }
}
