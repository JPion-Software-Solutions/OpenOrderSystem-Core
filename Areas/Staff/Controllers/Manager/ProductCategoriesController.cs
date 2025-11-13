using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Categories.Product;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.ViewModels.Shared;
using System.Text.Json;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Authorize(Roles = "admin,manager")]
    [Route("Staff/Manager/Categories/Product/{action=Index}")]
    public class ProductCategoriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ProductCategoriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var model = _context.ProductCategories
                .Include(pc => pc.Ingredients)
                .Include(pc => pc.MenuItems)
                .ToList();

            return View(model);
        }
        public ActionResult Create()
        {
            var ingredients = _context.Ingredients
                .Include(i => i.ProductCategories)
                .OrderBy(i => i.CategoryId)
                .ToList();

            var menu = _context.MenuItems.ToList();

            return View("CreateEdit", new CreateEditVM(ingredients, menu));
        }

        // POST: IngredientCategoryController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateEditVM model)
        {
            if (ModelState.IsValid)
            {
                var ingredients = new List<Ingredient>();
                foreach (var ingredientId in model.GetLinkedIngredients())
                {
                    var ingredient = _context.Ingredients.FirstOrDefault(i => i.Id == ingredientId);
                    if (ingredient != null)
                        ingredients.Add(ingredient);
                }
                model.Category.Ingredients = ingredients;

                _context.ProductCategories.Add(model.Category);
                _context.SaveChanges();

                foreach (var menuItemId in model.GetLinkedMenuItems())
                {
                    var menuItem = _context.MenuItems.FirstOrDefault(i => i.Id == menuItemId);
                    if (menuItem != null)
                        menuItem.ProductCategoryId = model.Id;
                }

                _context.SaveChanges();

                return RedirectToActionPermanent(nameof(Index));
            }
            else
            {
                return View("CreateEdit", model);
            }
        }

        // GET: IngredientCategoryController/Edit/5
        public ActionResult Edit(int id)
        {
            var ingredients = _context.Ingredients
                .Include(i => i.ProductCategories)
                .OrderBy(i => i.CategoryId)
                .ToList();

            var menu = _context.MenuItems.ToList();

            var category = _context.ProductCategories.FirstOrDefault(c => c.Id == id);

            return View("CreateEdit", new CreateEditVM(ingredients, menu, category));
        }

        // POST: IngredientCategoryController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CreateEditVM model)
        {
            if (ModelState.IsValid)
            {
                var category = _context.ProductCategories
                    .Include(c => c.Ingredients)
                    .FirstOrDefault(c => c.Id == model.Id);

                if (category == null)
                {
                    return NotFound();
                }


                foreach (var menuItemId in model.GetLinkedMenuItems())
                {
                    var menuItem = _context.MenuItems.FirstOrDefault(i => i.Id == menuItemId);
                    if (menuItem != null)
                        menuItem.ProductCategoryId = model.Id;
                }

                var ingredients = new List<Ingredient>();
                foreach (var ingredientId in model.GetLinkedIngredients())
                {
                    var ingredient = _context.Ingredients.FirstOrDefault(i => i.Id == ingredientId);
                    if (ingredient != null)
                        ingredients.Add(ingredient);
                }

                category.Ingredients = ingredients;
                category.Name = model.Name;
                category.Description = model.Description;
                category.Priority = model.Priority;

                _context.ProductCategories.Update(category);
                _context.SaveChanges();

                return RedirectToActionPermanent(nameof(Index));
            }
            else
            {
                return View("CreateEdit", model);
            }
        }

        // GET: IngredientCategoryController/Delete/5
        public ActionResult Delete(int id)
        {
            var category = _context.ProductCategories.FirstOrDefault(i => i.Id == id);
            if (category == null)
                return NoContent();

            var model = new ConfirmDeleteVM
            {
                Action = "/Staff/Manager/Categories/Product/DeleteConfirmed",
                Id = id,
                Title = $"Delete {category.Name}?",
                Description = $"Are you sure you want to permanently delete menu category #{id} {category.Name}? This action cannot be undone!"
            };

            return PartialView("_ConfirmDeleteModal", model);
        }

        // POST: IngredientCategoryController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteConfirmed(int id)
        {
            var deadBitsComputing = _context.ProductCategories.FirstOrDefault(i => i.Id == id);
            if (deadBitsComputing == null)
                return NotFound();

            _context.ProductCategories.Remove(deadBitsComputing);
            _context.SaveChanges();

            return RedirectToActionPermanent(nameof(Index));
        }
    }
}
