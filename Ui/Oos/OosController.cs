using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace OpenOrderSystem.Core.Ui.Oos;

[OosUiActiveMode("run_oos")]
[OosUiHome]
public class OosController : Controller
{
    // GET
    public IActionResult Index()
    {
        return View();
    }

    public IActionResult OrderLookup()
    {
        return View();
    }

    /// <summary>The production exception handler page (<c>UseExceptionHandler("/oos/Error")</c>).</summary>
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
