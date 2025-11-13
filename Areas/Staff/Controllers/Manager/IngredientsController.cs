using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Ingredients;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.ViewModels.Shared;
using OpenOrderSystem.Core.ViewModels.Shared;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Authorize(Roles = "manager,admin")]
    [Route("Staff/Manager/Ingredients/{action=Index}")]
    public class IngredientsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<IngredientsController> _logger;

        public IngredientsController(ApplicationDbContext context, ILogger<IngredientsController> logger)
        {
            _context = context;
            _logger = logger;
        }

        // GET: IngredientsController
        public ActionResult Index()
        {
            var model = _context.Ingredients
                .Include(i => i.MenuItems)
                .Include(i => i.ProductCategories)
                .Include(i => i.Category)
                .ToList();

            return View(model);
        }

        // GET: IngredientsController/Create
        public ActionResult Create()
        {
            var iCategories = _context.IngredientCategories
                .Include(c => c.MemberIngredients)
                .ToList();

            return View("CreateEdit", new CreateEditVM(iCategories));
        }

        // POST: IngredientsController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateEditVM model)
        {
            if (ModelState.IsValid)
            {
                _context.Ingredients.Add(model.Ingredient);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return View("CreateEdit", model);
            }
        }

        // GET: IngredientsController/Edit/5
        public ActionResult Edit(int id)
        {
            var ing = _context.Ingredients
                .FirstOrDefault(i => i.Id == id);

            if (ing == null)
                return NotFound($"Unable to locate ingredient #{id}");

            var iCategories = _context.IngredientCategories
                .Include(c => c.MemberIngredients)
                .ToList();

            var model = new CreateEditVM(iCategories, ing);

            model.Action = CrudAction.Edit;

            return View("CreateEdit", model);
        }

        // POST: IngredientsController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CreateEditVM model)
        {
            if (ModelState.IsValid)
            {
                _context.Ingredients.Update(model.Ingredient);
                _context.SaveChanges();
                return RedirectToAction(nameof(Index));
            }
            else
            {
                return View("CreateEdit", model);
            }
        }

        // GET: IngredientsController/Delete/5
        public ActionResult Delete(int id)
        {
            var ingredient = _context.Ingredients.FirstOrDefault(i => i.Id == id);
            if (ingredient == null)
                return NoContent();

            var model = new ConfirmDeleteVM
            {
                Action = "/Staff/Manager/Ingredients/DeleteConfirmed",
                Id = id,
                Title = $"Delete {ingredient.Name}?",
                Description = $"Are you sure you want to permanently delete ingredient #{id} {ingredient.Name}? This action cannot be undone!"
            };

            return PartialView("_ConfirmDeleteModal", model);
        }

        // POST: IngredientsController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var deadBitsComputing = _context.Ingredients.FirstOrDefault(i => i.Id == id);
            if (deadBitsComputing == null)
                return NotFound();

            _context.Ingredients.Remove(deadBitsComputing);
            _context.SaveChanges();

            return RedirectToActionPermanent(nameof(Index));
        }
    }
}
