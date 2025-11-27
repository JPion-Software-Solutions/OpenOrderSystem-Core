using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using NuGet.Protocol;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Menu;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Areas.Configuration.Models;
using OpenOrderSystem.Core.Areas.Staff.Models;
using OpenOrderSystem.Core.ViewModels.Shared;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Web.Helpers;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Route("Staff/Manager/Menu/{action=Index}")]
    [Authorize(Roles = "admin,manager")]
    public class MenuController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<MenuController> _logger;
        public MenuController(ApplicationDbContext context, ILogger<MenuController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public static string ImageDirectoryPath { get; set; } = string.Empty;


        // GET: MenuController
        public ActionResult Index()
        {
            List<MenuItem> menuItems = _context.MenuItems
                .Include(mi => mi.RawDbVarients)
                .Include(mi => mi.ProductCategory)
                .Include(mi => mi.Ingredients)
                .ToList() ?? new List<MenuItem>();

            return View(menuItems);
        }

        // GET: MenuController/Details/5
        public ActionResult Details(int id)
        {
            return View();
        }

        // GET: MenuController/Create
        public ActionResult Create()
        {
            var model = GetRequiredModelData(new CreateEditVM());
            return View(model);
        }

        // POST: MenuController/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(CreateEditVM model)
        {
            //extract complex data from form
            var ingredientIds = JsonSerializer.Deserialize<int[]>(model.Ingredients);
            var varients = JsonSerializer.Deserialize<List<Varient>>(model.Varients, new JsonSerializerOptions
            {
                IncludeFields = true
            });

            if (ModelState.IsValid)
            {
                var ingredients = new List<Ingredient>();
                foreach (var id in ingredientIds ?? Array.Empty<int>())
                {
                    var ingredient = _context.Ingredients
                        .FirstOrDefault(i => i.Id == id);

                    if (ingredient == null)
                        continue;

                    ingredients.Add(ingredient);
                }

                var menuItem = new MenuItem
                {
                    Name = model.Name,
                    Description = model.Description,
                    ImageUrl = model.ImageUrl,
                    Ingredients = ingredients,
                    ProductCategoryId = model.CategoryId
                };

                _context.MenuItems.Add(menuItem);
                _context.SaveChanges();

                List<Varient> list = varients ?? new List<Varient>();
                for (int i = 0; i < list.Count; i++)
                {
                    Varient varient = list[i];
                    if (string.IsNullOrEmpty(varient.descriptor))
                        ModelState.AddModelError("Varients", "Missing descriptor(s) for one or more varients.");
                    if (varient.price < 0.01f)
                        ModelState.AddModelError("Varients", "Missing price or negative price entered for one or more varients");
                    if (varient.upc < 1)
                        ModelState.AddModelError("Varients", "Missing UPC or negative number entered for one or more varients");

                    if (!ModelState.IsValid)
                    {
                        //rollback changes
                        _context.MenuItems.Remove(menuItem);
                        _context.SaveChanges();
                        model = GetRequiredModelData(model);
                        return View(model);
                    }

                    _context.MenuItemVarients.Add(new MenuItemVarient
                    {
                        Descriptor = varient.descriptor,
                        Price = varient.price,
                        Index = i,
                        Upc = varient.upc.ToString("00000"),
                        MenuItemId = menuItem.Id
                    });
                }

                _context.SaveChanges();
                _logger.LogInformation($"User '{User.Identity?.Name ?? "ERROR_UNKNOWN_USER"}' added MenuItem #{menuItem.Id} {menuItem.Name} to menu.");
                return RedirectToActionPermanent("Index");
            }

            model = GetRequiredModelData(model);
            return View(model);
        }

        // GET: MenuController/Edit/5
        public ActionResult Edit(int id)
        {
            var model = new CreateEditVM();
            var menuItem = _context.MenuItems
                .Include(x => x.RawDbVarients)
                .Include(x => x.Ingredients)
                .FirstOrDefault(x => x.Id == id);

            if (menuItem == null)
                return NotFound($"Unable to locate MenuItem with id:{id}");

            var ingredientIds = new List<int>();
            foreach (var ing in menuItem.Ingredients ?? new List<Ingredient>())
            {
                ingredientIds.Add(ing.Id);
            }

            model.Ingredients = JsonSerializer.Serialize(ingredientIds);

            var varients = new List<Varient>();
            foreach (var v in menuItem.MenuItemVarients ?? new List<MenuItemVarient>())
            {
                varients.Add(new Varient
                {
                    descriptor = v.Descriptor,
                    price = v.Price,
                    upc = int.Parse(v.Upc)
                });
            }
            model.Varients = JsonSerializer.Serialize(varients, new JsonSerializerOptions
            {
                IncludeFields = true
            });

            model.CategoryId = menuItem.ProductCategoryId;
            model.ImageUrl = menuItem.ImageUrl ?? "";
            model.Description = menuItem.Description ?? "";
            model.Name = menuItem.Name ?? "";
            model.Action = CreateEditVM.Actions.Edit;
            model.Id = menuItem.Id;

            model = GetRequiredModelData(model);
            return View("Create", model);
        }

        // POST: MenuController/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Edit(CreateEditVM model)
        {
            //extract complex data from form
            var ingredientIds = JsonSerializer.Deserialize<int[]>(model.Ingredients);
            var varients = JsonSerializer.Deserialize<List<Varient>>(model.Varients, new JsonSerializerOptions
            {
                IncludeFields = true
            });

            if (ModelState.IsValid)
            {
                var ingredients = new List<Ingredient>();
                foreach (var id in ingredientIds ?? Array.Empty<int>())
                {
                    var ingredient = _context.Ingredients
                        .Include(i => i.MenuItems)
                        .FirstOrDefault(i => i.Id == id);

                    if (ingredient == null)
                        continue;

                    ingredients.Add(ingredient);
                }

                var menuItem = _context.MenuItems
                    .Include(m => m.Ingredients)
                    .FirstOrDefault(m => m.Id == model.Id);

                if (menuItem == null)
                    return NotFound("Unable to locate menu item.");

                var snapshot = new MenuItem
                {
                    Id = menuItem.Id,
                    Name = menuItem.Name,
                    Description = menuItem.Description,
                    ImageUrl = menuItem.ImageUrl,
                    ProductCategoryId = menuItem.ProductCategoryId,
                    Ingredients = menuItem.Ingredients
                };

                menuItem.Name = model.Name;
                menuItem.Description = model.Description;
                menuItem.ImageUrl = model.ImageUrl;
                menuItem.ProductCategoryId = model.CategoryId;
                menuItem.Ingredients = ingredients;
                menuItem.Priority = model.Priority;

                _context.MenuItems.Update(menuItem);
                _context.SaveChanges();


                var varientDescriptors = new string[varients?.Count ?? 0];
                for (int i = 0; i < varientDescriptors.Length; i++)
                {
                    varientDescriptors[i] = varients?[i].descriptor ?? "__ERROR__";
                }

                var removedVarients = _context.MenuItemVarients
                    .Where(v => v.MenuItemId == model.Id && !varientDescriptors.Contains(v.Descriptor));

                foreach (var deleteMe in removedVarients)
                {
                    _context.Remove(deleteMe);
                }

                List<Varient> list = varients ?? new List<Varient>();
                for (int i = 0; i < list.Count; i++)
                {
                    Varient varient = list[i];
                    if (string.IsNullOrEmpty(varient.descriptor))
                        ModelState.AddModelError("Varients", "Missing descriptor(s) for one or more varients.");
                    if (varient.price < 0.01f)
                        ModelState.AddModelError("Varients", "Missing price or negative price entered for one or more varients");
                    if (varient.upc < 1)
                        ModelState.AddModelError("Varients", "Missing UPC or negative number entered for one or more varients");

                    if (!ModelState.IsValid)
                    {
                        //rollback changes
                        model = GetRequiredModelData(model);
                        return View("Create", model);
                    }

                    var varientUpdated = false;

                    var oldVarients = _context.MenuItemVarients
                        .Where(v => v.MenuItemId == model.Id)
                        .ToArray();

                    for (int j = 0; j < oldVarients.Length; j++)
                    {


                        if (oldVarients[j].Descriptor == varient.descriptor)
                        {
                            oldVarients[j].Price = varient.price;
                            oldVarients[j].Upc = varient.upc.ToString("00000");
                            oldVarients[j].Descriptor = varient.descriptor;
                            oldVarients[j].Index = i;
                            varientUpdated = true;
                        }
                    }

                    if (!varientUpdated)
                    {
                        _context.MenuItemVarients.Add(new MenuItemVarient
                        {
                            Descriptor = varient.descriptor,
                            Price = varient.price,
                            Index = i,
                            Upc = varient.upc.ToString("00000"),
                            MenuItemId = menuItem.Id
                        });
                    }

                    //    _context.MenuItemVarients.Remove(dead);

                    //_context.MenuItemVarients.Add(new MenuItemVarient
                    //{
                    //    Descriptor = varient.descriptor,
                    //    Price = varient.price,
                    //    Upc = varient.upc.ToString("00000"),
                    //    MenuItemId = menuItem.Id
                    //});
                }

                _context.SaveChanges();
                _logger.LogInformation($"User '{User.Identity?.Name ?? "ERROR_UNKNOWN_USER"}' updated MenuItem #{menuItem.Id} {menuItem.Name}.");
                return RedirectToActionPermanent("Index");
            }

            model = GetRequiredModelData(model);
            return View(model);
        }

        // GET: MenuController/Delete/5
        public ActionResult Delete(int id)
        {
            var menuItem = _context.MenuItems.FirstOrDefault(m => m.Id == id);
            if (menuItem == null)
                return NotFound();

            return PartialView("_ConfirmDeleteModal", new ConfirmDeleteVM
            {
                Id = id,
                Action = "/Staff/Manager/Menu/ConfirmedDelete",
                Title = $"Delete {menuItem.Name}?",
                Description = $"Are you sure you want to permanently delete item #{id} &mdash; {menuItem.Name} from the menu? This action cannot be undone."
            });
        }

        // POST: MenuController/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ConfirmedDelete(int id)
        {
            var menuItem = _context.MenuItems.FirstOrDefault(m => m.Id == id);
            if (menuItem == null)
                return NotFound();

            _context.MenuItems.Remove(menuItem);
            _context.SaveChanges();
            _logger.LogInformation($"User '{User.Identity?.Name ?? "ERROR_UNKNOWN_USER"}' removed MenuItem #{menuItem.Id} {menuItem.Name} from menu.");

            return RedirectToActionPermanent("Index");
        }

        private CreateEditVM GetRequiredModelData(CreateEditVM model)
        {
            model.Categories = _context.ProductCategories
                .Include(pc => pc.Ingredients)
                .ToList();

            foreach (var category in model.Categories)
            {
                model.AvailableIngredients[category.Id] = category.Ingredients ?? new List<Ingredient>();
            }

            if (Directory.Exists(ImageDirectoryPath))
            {
                var imageFiles = Directory.GetFiles(ImageDirectoryPath);

                foreach (var image in imageFiles)
                {
                    var extension = Path.GetExtension(image);
                    var filename = Path.GetFileName(image);
                    filename = filename.Replace(extension, "");
                    var path = Path.Combine("\\", "media", "images", $"{filename}{extension}");

                    model.Images.Add(new ImageModel
                    {
                        Name = filename,
                        Url = path
                    });
                }
            }
            else throw new InvalidOperationException("system haulted due to missing image directory.");

            return model;
        }

        private struct Varient
        {
            public string descriptor;
            public float price;
            public int upc;
        }
    }
}
