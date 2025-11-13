using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using System.Security.Permissions;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{
    [Area("API")]
    [Route("api/[controller]/[action]")]
    [ApiController]
    public class MenuController : ControllerBase
    {
        private ApplicationDbContext _context;

        public MenuController(ApplicationDbContext context)
        {
            _context = context; 
        }
        
        public IResult GetMenuItems()
        {
            var menu = _context.MenuItems
                .Include(mi => mi.RawDbVarients)
                .Include(mi => mi.ProductCategory)
                .ToList();

            foreach (var menuItem in menu)
            {
                menuItem.ProductCategory!.MenuItems = null;

                foreach (var variant in menuItem.RawDbVarients ?? new List<Data.DataModels.MenuItemVarient>())
                {
                    variant.MenuItem = null;
                }
            }

            return Results.Ok(menu);
        }

        public IResult GetItemDetail(int id)
        {
            return Results.StatusCode(418);
        }
    }
}
