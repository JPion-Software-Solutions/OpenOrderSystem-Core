using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing.Template;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Models;
using OpenOrderSystem.Core.Services;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Authorize(Roles = "admin,manager")]
    [Route("Staff/Manager/Printers/{action=Index}")]
    public class PrintersController : Controller
    {
        private ApplicationDbContext _context;
        private PrinterService _printerService;

        public PrintersController(ApplicationDbContext context, PrinterService printerService)
        {
            _context = context;
            _printerService = printerService;
        }

        public IActionResult Index()
        {
            var printers = _context.Printers.ToList();
            var model = new Dictionary<string, PrinterStatus>();

            foreach (var printer in printers)
            {
                var status = _printerService.GetStatus(printer.Id);
                model[printer.Id] = status;
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult TemplateBuilder(PrintTemplate? template)
        {
            if (template == null)
            {
                return View(new PrintTemplate());
            }

            return View(template);
        }

        [HttpGet]
        public async Task<IActionResult> EditTemplateAsync(string id)
        {
            var template = await _context.PrintTemplates.FindAsync(id);

            if (template == null)
            {
                return NotFound($"Cannot locate PrintTemplate id:{id}");
            }
            var dumdum = template.Instructions.Count;
            return RedirectToAction(nameof(TemplateBuilder), new PrintTemplate
            {
                Id = id,
                BuildInstructions = template.BuildInstructions,
                Name = template.Name,
                DefaultEndOfDayTemplate = template.DefaultEndOfDayTemplate,
                DefaultOrderTemplate = template.DefaultOrderTemplate
            });
        }


        [HttpPost]
        public IActionResult SaveTemplate(PrintTemplate template)
        {
            template.Id = template.Id != null ? template.Id : Guid.NewGuid().ToString();
            ModelState.Remove("Id");
            if (ModelState.IsValid)
            {
                _context.PrintTemplates.Add(template);
                _context.SaveChanges();

                return RedirectToAction("Index");
            }

            return View(nameof(TemplateBuilder), template);
        }

        [HttpPost]
        public IActionResult AddBuildStep(string name, string id, string buildInstructions, PrintInstruction instruction, string? data)
        {
            var template = new PrintTemplate
            {
                Id = id,
                Name = name,
                BuildInstructions = buildInstructions
            };

            template.AddBuildStep(instruction, data);

            return RedirectToAction(nameof(TemplateBuilder), new
            {
                template.Name,
                template.Id,
                template.BuildInstructions
            });
        }
    }
}
