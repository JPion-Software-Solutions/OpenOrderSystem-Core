using System;
using System.IO.Compression;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using OpenOrderSystem.Core.Bootstrapper;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.V2.Core;

namespace OpenOrderSystem.Core.DevelopmentTools;

public sealed class DevPackUnloader
{
    public static async Task UnpackAssets(string filepath)
    {
        var zip = ZipFile.OpenRead(filepath);
        var assets = zip.Entries
            .Where(e => e.FullName.StartsWith("pack/assets/") && e.FullName != "pack/assets")
            .ToList();
        
        foreach(var asset in assets)
        {
            if (asset.FullName == "pack/assets/") continue;
            var assetFilepath = Path.Combine(OpenOrderSystemApplication.DataRootPath,
                "public",
                "wwwroot", 
                "media", 
                "images");
            Directory.CreateDirectory(assetFilepath);
            if (File.Exists(Path.Combine(assetFilepath, asset.Name))) File.Delete(Path.Combine(assetFilepath, asset.Name));
            asset.ExtractToFile(Path.Combine(assetFilepath, asset.Name));
        }
    }

    public static async Task ImportInfo(string filepath, ApplicationDbContext dbContext)
    {
        /*var zip = ZipFile.OpenRead(filepath);
        var infoEntity = zip.GetEntry("pack/data/info.json");
        if (infoEntity == null) return;
        var tempFile = Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store", "DevPacks", "info.tmp");
        infoEntity.ExtractToFile(tempFile);
        var json = await File.ReadAllTextAsync(tempFile);
        var info = JsonSerializer.Deserialize<DevPackInfo>(json);
        File.Delete(tempFile);
        if (info == null) throw new InvalidOperationException("Failed to read pack info.json");

        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "RestaurantName",
            Value = info.Restaurant.DisplayName
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "RestaurantTagline",
            Value = info.Restaurant.Tagline
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "SystemTimezone",
            Value = info.Restaurant.Timezone
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "ResturantPhone",
            Value = info.Restaurant.Contact.Phone
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "RestaurantWebsite",
            Value = info.Restaurant.Contact.Website
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "RestaurantAddress",
            Value = JsonSerializer.Serialize(info.Restaurant.Address)
        });
        dbContext.Configuration.Add(new SystemConfig
        {
            Key = "NormalBusinessHours",
            Value = JsonSerializer.Serialize(info.Hours.Weekly)
        });

        await ImportPrinterTemplates(filepath, dbContext, info);*/

        await dbContext.SaveChangesAsync();
    }

    public static async Task ImportMenu(string filepath, ApplicationDbContext dbContext)
    {
        var zip = ZipFile.OpenRead(filepath);
        var infoEntity = zip.GetEntry("pack/data/menu.json");
        if (infoEntity == null) return;
        var tempFile = Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store", "DevPacks", "menu.tmp");
        infoEntity.ExtractToFile(tempFile);
        var json = await File.ReadAllTextAsync(tempFile);
        var menu = JsonSerializer.Deserialize<DevPackMenu>(json);
        File.Delete(tempFile);
        if (menu == null) throw new InvalidOperationException("Failed to read pack menu.json");

        foreach (var ingredientCategory in menu.IngredientCategories)
        {
            dbContext.IngredientCategories.Add(new IngredientCategory
            {
                Name = ingredientCategory.Name,
                Priority = ingredientCategory.Priority,
                Type = Enum.Parse<IngredientType>(ingredientCategory.Type)
            });
        }

        await dbContext.SaveChangesAsync();

        var ingredientCategoryMap = new Dictionary<string, int>();
        foreach (var ingredient in menu.Ingredients)
        {
            var categoryKey = -1;
            if (ingredientCategoryMap.ContainsKey(ingredient.CategoryKey))
                categoryKey = ingredientCategoryMap[ingredient.CategoryKey];
            else
            {
                var localCategory = menu.IngredientCategories.FirstOrDefault(c => c.Key == ingredient.CategoryKey)
                    ?? throw new InvalidOperationException($"Unable to translate category of key '{ingredient.CategoryKey}. Please verify category exists and try again.");
                
                var dbCategory = dbContext.IngredientCategories.FirstOrDefault(c => c.Name == localCategory.Name)
                    ?? throw new InvalidOperationException($"Unable to translate category of key '{ingredient.CategoryKey}. Please verify category exists and try again.");
                
                ingredientCategoryMap[ingredient.CategoryKey] = dbCategory.Id; // map key to avoid extra Db roundtrips.
                categoryKey = dbCategory.Id;
            }

            var newIngredient = new Ingredient
            {
                Name = ingredient.Name,
                CategoryId = categoryKey,
                Price = ingredient.Price,
            };

            dbContext.Ingredients.Add(newIngredient);
        }

        await dbContext.SaveChangesAsync();

        var ingredientMap = new Dictionary<string, Ingredient>();
        foreach (var productCategory in menu.ProductCategories)
        {
            var allowedIngredients = new List<Ingredient>();
            foreach (var ingredient in productCategory.AllowedIngredientKeys ?? new List<string>())
            {
                ingredientMap.TryGetValue(ingredient, out var dbIngredient);

                if (dbIngredient == null)
                {
                    //fetch from db
                    var localIngredientName = menu.Ingredients.FirstOrDefault(i => i.Key == ingredient)?.Name ?? "";
                    dbIngredient = dbContext.Ingredients.FirstOrDefault(i => i.Name == localIngredientName);

                    if (dbIngredient == null)
                         throw new InvalidOperationException($"Unable to map ingredient with key '{ingredient}' to database record.");

                    ingredientMap[ingredient] = dbIngredient;
                }

                allowedIngredients.Add(dbIngredient);
            }

            var newProductCategory = new ProductCategory
            {
                Name = productCategory.Name,
                Priority = productCategory.Priority,
                Description = productCategory.Description,
                Ingredients = allowedIngredients
            };

            dbContext.ProductCategories.Add(newProductCategory);
        }

        await dbContext.SaveChangesAsync();

        var productCategoryMap = new Dictionary<string, int>();
        foreach (var product in menu.MenuItems)
        {
            if (!productCategoryMap.ContainsKey(product.ProductCategoryKey))
            {
                var localProductCategory = menu.ProductCategories.FirstOrDefault(c => c.Key == product.ProductCategoryKey)?.Name ?? "";
                var dbProductCategory = dbContext.ProductCategories.FirstOrDefault(c => c.Name == localProductCategory);

                if (dbProductCategory == null)
                    throw new InvalidOperationException($"Unable to map product category with key '{product.ProductCategoryKey}' to database record.");

                productCategoryMap[product.ProductCategoryKey] = dbProductCategory.Id;
            }


            var defaultIngredients = new List<Ingredient>();
            foreach (var ingredient in product.DefaultIngredientKeys ?? new List<string>())
            {
                ingredientMap.TryGetValue(ingredient, out var dbIngredient);

                if (dbIngredient == null)
                {
                    //fetch from db
                    var localIngredientName = menu.Ingredients.FirstOrDefault(i => i.Key == ingredient)?.Name ?? "";
                    dbIngredient = dbContext.Ingredients.FirstOrDefault(i => i.Name == localIngredientName);

                    if (dbIngredient == null)
                         throw new InvalidOperationException($"Unable to map ingredient with key '{ingredient}' to database record.");

                    ingredientMap[ingredient] = dbIngredient;
                }

                defaultIngredients.Add(dbIngredient);
            }

            var varients = new List<MenuItemVarient>();
            foreach (var varient in product.Varients ?? new List<DevPackMenu.Varient>())
            {
                varients.Add(new MenuItemVarient
                {
                    Descriptor = varient.Descriptor,
                    Index = varient.Index,
                    Price = varient.Price,
                    Priority = varient.Priority
                });
            }

            var imgUrl = product.ImageUrl.Replace("assets/", "/media/images/user/");

            var newProduct = new MenuItem
            {
                Name = product.Name,
                Priority = product.Priority,
                ProductCategoryId = productCategoryMap[product.ProductCategoryKey],
                Description = product.Description,
                Ingredients = defaultIngredients,
                ImageUrl = imgUrl,
                RawDbVarients = varients
            };

            dbContext.MenuItems.Add(newProduct);
        }

        await dbContext.SaveChangesAsync();
    }

    private static async Task ImportPrinterTemplates(string filepath, ApplicationDbContext dbContext, DevPackInfo info)
    {
        var zip = ZipFile.OpenRead(filepath);
        var infoEntity = zip.GetEntry("pack/data/print-templates.json");
        if (infoEntity == null) return;
        var tempFile = Path.Combine(OpenOrderSystemApplication.DataRootPath, "Store", "DevPacks", "print-templates.tmp");
        infoEntity.ExtractToFile(tempFile);
        var json = await File.ReadAllTextAsync(tempFile);
        var printTemplates = JsonSerializer.Deserialize<DevPackPrinterTemplates>(json);
        File.Delete(tempFile);
        if (printTemplates == null) throw new InvalidOperationException("Failed to read pack printer-templates.json");

        var infoMap = new Dictionary<string, string>
        {
            {"Restaurant.DisplayName", info.Restaurant.DisplayName},
            {"Restaurant.Tagline", info.Restaurant.Tagline},
            {"Restaurant.Address.Line1", info.Restaurant.Address.Line1},
            {"Restaurant.Address.City", info.Restaurant.Address.City},
            {"Restaurant.Address.State", info.Restaurant.Address.State},
            {"Restaurant.Address.PostalCode", info.Restaurant.Address.PostalCode},
            {"Restaurant.Contact.Phone", info.Restaurant.Contact.Phone}
        };

        var i = 0;
        foreach (var template in printTemplates.Templates)
        {
            if (template == null) 
                throw new InvalidOperationException($"Unable to translate printer template #{i}");

            var buildSteps = new List<BuildStep>();
            foreach (var step in template.Steps ?? new List<DevPackPrinterTemplates.PackBuildStep>())
            {
                if(!Enum.TryParse<PrintInstruction>(step.Instruction, out var instruction))
                    throw new InvalidOperationException($"Failed to translate print step '{step.Instruction}'");
                
                foreach (var infoKey in infoMap.Keys)
                    if (step.Data != null) 
                        step.Data = step.Data.Replace($"{{{{{infoKey}}}}}", infoMap[infoKey]);
                
                buildSteps.Add
                (
                    new BuildStep
                    {
                        Instruction = instruction,
                        Data = step.Data
                    }
                );
            }

            var newTemplate = new PrintTemplate
            {
                Id = Guid.NewGuid().ToString(),
                Name = template.Name,
                BuildInstructions = JsonSerializer.Serialize(buildSteps),
                DefaultOrderTemplate = template.DefaultOrderTemplate,
                DefaultEndOfDayTemplate = template.DefaultEndOfDayTemplate
            };
            
            dbContext.PrintTemplates.Add(newTemplate);

            i++;
        }

        await dbContext.SaveChangesAsync();
    }
}
