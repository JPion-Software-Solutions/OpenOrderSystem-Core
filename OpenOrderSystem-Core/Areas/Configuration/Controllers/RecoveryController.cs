using Microsoft.AspNetCore.Mvc;

namespace OpenOrderSystem.Core.Areas.Configuration.Controllers
{
    [Area("Configuration")]
    public class RecoveryController : Controller
    {
        public IActionResult Index()
        {
            return View();
        }
    }
}
