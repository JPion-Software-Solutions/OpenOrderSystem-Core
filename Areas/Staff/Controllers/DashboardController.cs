using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers
{
    [Area("Staff")]
    [Authorize(Roles = "admin,manager")]
    [Route("Staff/Manager/Dashboard/{action=Index}")]
    public class DashboardController : Controller
    {
        [Route("/Manager")]
        public IActionResult Manager()
        {
            return RedirectToAction(nameof(Index));
        }

        public IActionResult Index()
        {
            return View();
        }
    }
}
