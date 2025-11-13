using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using OpenOrderSystem.Core.Data.DataModels.DiscountCodes;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Coupon;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Authorize(Roles = "admin,manager")]
    [Route("Staff/Manager/Coupon/{action=Index}")]
    public class CouponController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CouponController(ApplicationDbContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var model = new IndexVM();

            _context.MenuItems.Load();
            _context.MenuItemVarients
                .Include(miv => miv.DiscountCodes)
                .Load();
            _context.Orders.Load();

            model.PercentDiscounts = _context.PercentDiscountCodes
                .Include(d => d.WhiteListItemsVarients)
                .ToList();

            model.FixedDiscounts = _context.FixedAmountDiscountCodes
                .Include(d => d.WhiteListItemsVarients)
                .ToList();

            model.BogoDiscounts = _context.BuyXGetXForYDiscountCodes
                .Include(d => d.WhiteListItemsVarients)
                .ToList();

            return View(model);
        }

        [HttpGet]
        public IActionResult Create(string couponType)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            if (couponType == typeof(FixedAmountDiscountCode).ToString())
            {
                var coupon = new FixedAmountDiscountCode();
                return View("Create_FixedDiscount", coupon);
            }
            if (couponType == typeof(PercentDiscountCode).ToString())
            {
                var coupon = new PercentDiscountCode();
                return View("Create_PercentDiscount", coupon);
            }
            if (couponType == typeof(BuyXGetXForYDiscountCode).ToString())
            {
                var coupon = new BuyXGetXForYDiscountCode();
                return View("Create_BuyQtyGetQtyDiscount", coupon);
            }

            return NotFound("Failed to locate suitable discount type.");
        }

        [HttpPost]
        public async Task<IActionResult> CreateFixedDiscountAsync(FixedAmountDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.DiscountCodes
                    .FirstOrDefaultAsync(c => c.Code == model.Code);

                if (existing != null)
                {
                    ModelState.AddModelError("Code", $"The promo code '{model.Code} is already in use. Please delete " +
                        $"the code and try again or choose a different code.");
                }

                if (ModelState.IsValid)
                {
                    var varients = new List<MenuItemVarient>();
                    foreach (var id in whitelist ?? Array.Empty<int>())
                    {
                        var varient = await _context.MenuItemVarients
                            .FirstOrDefaultAsync(miv => miv.Id == id);

                        if (varient != null) varients.Add(varient);
                    }

                    model.WhiteListItemsVarients = varients.Any() ? varients : null;
                    model.Created = DateTime.UtcNow;

                    await _context.FixedAmountDiscountCodes.AddAsync(model);
                    await _context.SaveChangesAsync();

                    return RedirectToActionPermanent(nameof(Index));
                }
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Create_FixedDiscount", model);
        }

        [HttpPost]
        public async Task<IActionResult> CreatePercentDiscountAsync(PercentDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.DiscountCodes
                    .FirstOrDefaultAsync(c => c.Code == model.Code);

                if (existing != null)
                {
                    ModelState.AddModelError("Code", $"The promo code '{model.Code} is already in use. Please delete " +
                        $"the code and try again or choose a different code.");
                }

                if (ModelState.IsValid)
                {
                    var varients = new List<MenuItemVarient>();
                    foreach (var id in whitelist ?? Array.Empty<int>())
                    {
                        var varient = await _context.MenuItemVarients
                            .FirstOrDefaultAsync(miv => miv.Id == id);

                        if (varient != null) varients.Add(varient);
                    }

                    model.WhiteListItemsVarients = varients.Any() ? varients : null;
                    model.Created = DateTime.UtcNow;

                    await _context.PercentDiscountCodes.AddAsync(model);
                    await _context.SaveChangesAsync();

                    return RedirectToActionPermanent(nameof(Index));
                }
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Create_FixedDiscount", model);
        }

        [HttpPost]
        public async Task<IActionResult> CreateBuyQtyGetQtyDiscountAsync(BuyXGetXForYDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                var existing = await _context.DiscountCodes
                    .FirstOrDefaultAsync(c => c.Code == model.Code);

                if (existing != null)
                {
                    ModelState.AddModelError("Code", $"The promo code '{model.Code} is already in use. Please delete " +
                        $"the code and try again or choose a different code.");
                }

                if (ModelState.IsValid)
                {
                    var varients = new List<MenuItemVarient>();
                    foreach (var id in whitelist ?? Array.Empty<int>())
                    {
                        var varient = await _context.MenuItemVarients
                            .FirstOrDefaultAsync(miv => miv.Id == id);

                        if (varient != null) varients.Add(varient);
                    }

                    model.WhiteListItemsVarients = varients.Any() ? varients : null;
                    model.Created = DateTime.UtcNow;

                    await _context.BuyXGetXForYDiscountCodes.AddAsync(model);
                    await _context.SaveChangesAsync();

                    return RedirectToActionPermanent(nameof(Index));
                }
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Create_FixedDiscount", model);
        }

        [HttpGet("/Staff/Manager/Coupon/Edit/{code}")]
        public IActionResult Edit(string code)
        {
#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            var coupon = _context.DiscountCodes
                .Include(c => c.WhiteListItemsVarients)
                .Include(c => c.Orders)
                .FirstOrDefault(c => c.Code == code);

            if (coupon == null) return NotFound();

            var couponType = coupon.GetType().ToString();

            if (couponType == typeof(FixedAmountDiscountCode).ToString())
            {
                return View("Edit_FixedDiscount", coupon);
            }
            if (couponType == typeof(PercentDiscountCode).ToString())
            {
                return View("Edit_PercentDiscount", coupon);
            }
            if (couponType == typeof(BuyXGetXForYDiscountCode).ToString())
            {
                return View("Edit_BuyQtyGetQtyDiscount", coupon);
            }

            return NotFound("Failed to locate suitable discount type.");
        }

        [HttpPost]
        public async Task<IActionResult> EditFixedDiscountAsync(FixedAmountDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                _context.DiscountCodes.Update(model);

                var originalVarientList = _context.MenuItemVarients
                    .Include(v => v.DiscountCodes)
                    .AsEnumerable()
                    .Where(v => v.DiscountCodes != null &&
                        v.DiscountCodes.FirstOrDefault(d => d.Code == model.Code) != null)
                    .ToList();

                //remove any varients that were previously in the varient list but removed
                foreach (var varient in originalVarientList)
                {
                    if (!whitelist?.Contains(varient.Id) ?? false)
                    {
                        varient.DiscountCodes?.Remove(model);
                    }
                }

                //add any varients that were added to the list
                foreach (var varientId in whitelist ?? Array.Empty<int>())
                {
                    if (originalVarientList.FirstOrDefault(v => v.Id == varientId) == null)
                    {
                        var varient = _context.MenuItemVarients
                            .FirstOrDefault(v => v.Id == varientId);

                        varient?.DiscountCodes?.Add(model);
                    }
                }

                await _context.SaveChangesAsync();

                return RedirectToActionPermanent(nameof(Index));
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Edit_FixedDiscount", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditPercentDiscountAsync(PercentDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                if (ModelState.IsValid)
                {
                    _context.DiscountCodes.Update(model);

                    var originalVarientList = _context.MenuItemVarients
                        .Include(v => v.DiscountCodes)
                        .AsEnumerable()
                        .Where(v => v.DiscountCodes != null &&
                            v.DiscountCodes.FirstOrDefault(d => d.Code == model.Code) != null)
                        .ToList();

                    //remove any varients that were previously in the varient list but removed
                    foreach (var varient in originalVarientList)
                    {
                        if (!whitelist?.Contains(varient.Id) ?? false)
                        {
                            varient.DiscountCodes?.Remove(model);
                        }
                    }

                    //add any varients that were added to the list
                    foreach (var varientId in whitelist ?? Array.Empty<int>())
                    {
                        if (originalVarientList.FirstOrDefault(v => v.Id == varientId) == null)
                        {
                            var varient = _context.MenuItemVarients
                                .FirstOrDefault(v => v.Id == varientId);

                            varient?.DiscountCodes?.Add(model);
                        }
                    }
                    await _context.SaveChangesAsync();
                }
                return RedirectToActionPermanent(nameof(Index));
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Edit_PercentDiscount", model);
        }

        [HttpPost]
        public async Task<IActionResult> EditBuyQtyGetQtyDiscountAsync(BuyXGetXForYDiscountCode model, int[]? whitelist)
        {
            if (ModelState.IsValid)
            {
                _context.DiscountCodes.Update(model);

                var originalVarientList = _context.MenuItemVarients
                    .Include(v => v.DiscountCodes)
                    .AsEnumerable()
                    .Where(v => v.DiscountCodes != null &&
                        v.DiscountCodes.FirstOrDefault(d => d.Code == model.Code) != null)
                    .ToList();

                //remove any varients that were previously in the varient list but removed
                foreach (var varient in originalVarientList)
                {
                    if (!whitelist?.Contains(varient.Id) ?? false)
                    {
                        varient.DiscountCodes?.Remove(model);
                    }
                }

                //add any varients that were added to the list
                foreach (var varientId in whitelist ?? Array.Empty<int>())
                {
                    if (originalVarientList.FirstOrDefault(v => v.Id == varientId) == null)
                    {
                        var varient = _context.MenuItemVarients
                            .FirstOrDefault(v => v.Id == varientId);

                        varient?.DiscountCodes?.Add(model);
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

#pragma warning disable CS8602 // Dereference of a possibly null reference.
            ViewData["menu"] = _context.MenuItemVarients
                .Include(miv => miv.MenuItem)
                .OrderBy(miv => miv.MenuItem.Name)
                .ToList();
#pragma warning restore CS8602 // Dereference of a possibly null reference.

            return View("Edit_BuyQtyGetQtyDiscount", model);
        }

        [HttpPost]
        public async Task<IActionResult> ArchiveCoupon(string code)
        {
            var coupon = await _context.DiscountCodes
                .FirstOrDefaultAsync(p => p.Code == code);

            if (coupon == null)
                return NotFound($"unable to locate promo code {code}, hit the back button and try again");

            coupon.IsArchived = !coupon.IsArchived;
            await _context.SaveChangesAsync();

            return RedirectToActionPermanent(nameof(Index));
        }

        [HttpPost]
        public async Task<IActionResult> DeleteCoupon(string code)
        {
            var coupon = await _context.DiscountCodes
                .FirstOrDefaultAsync(p => p.Code == code);

            if (coupon == null)
                return NotFound($"unable to locate promo code {code}, hit the back button and try again");

            var orders = _context.Orders
                .Where(o => o.DiscountId == code);

            foreach (var order in orders)
            {
                order.DiscountId = null;
            }

            _context.DiscountCodes.Remove(coupon);
            await _context.SaveChangesAsync();

            return RedirectToActionPermanent(nameof(Index));
        }

    }
}
