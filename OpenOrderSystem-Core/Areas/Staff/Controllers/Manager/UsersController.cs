using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Build.Framework;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Areas.Staff.ViewModels.Users;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services.Interfaces;

namespace OpenOrderSystem.Core.Areas.Staff.Controllers.Manager
{
    [Area("Staff")]
    [Route("Staff/Manager/Users/{action=Index}")]
    [Authorize(Roles = "admin")]
    public class UsersController : Controller
    {
        private UserManager<IdentityUser> _userManager;
        private ILogger<UsersController> _logger;
        private ApplicationDbContext _context;
        private ISmsService _smsService;
        private IEmailService _emailService;

        public UsersController(UserManager<IdentityUser> userManager, ILogger<UsersController> logger, ApplicationDbContext context,
            ISmsService smsService, IEmailService emailService)
        {
            _userManager = userManager;
            _logger = logger;
            _context = context;
            _smsService = smsService;
            _emailService = emailService;
        }

        public IActionResult Index()
        {
            var model = _context.Users.ToList();
            return View(model);
        }

        [HttpGet]
        public IActionResult NewUser()
        {
            return View(new NewUserVM());
        }

        [HttpPost]
        public async Task<IActionResult> NewUser(NewUserVM model)
        {
            var contactRequired = false;
            if (model.Roles.Contains("manager"))
            {
                contactRequired = true;
            }
            else
            {
                ModelState.Remove("Email");
                ModelState.Remove("Phone");

                model.Phone = model.Email = "NOT_REQUIRED";
            }

            if (ModelState.IsValid)
            {
                if (contactRequired)
                {
                    model.Phone = _smsService.ConvertPhone(model.Phone);

                    if (!_smsService.VerifyPhone(model.Phone))
                    {
                        ModelState.AddModelError("PhoneNumber", "Please enter a valid US phone number.");
                        return View(model);
                    }
                }

                var user = Activator.CreateInstance<IdentityUser>();

                user.UserName = model.Username;
                user.NormalizedUserName = model.Username.Normalize();
                user.Email = model.Email;
                user.NormalizedEmail = model.Email.Normalize();
                user.PhoneNumber = model.Phone;

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    var roles = model.Roles.Split(',');
                    foreach (var role in roles)
                    {
                        await _userManager.AddToRoleAsync(user, role);
                    }

                    return RedirectToActionPermanent("Index");
                }
                else
                {
                    foreach (var error in result.Errors)
                    {
                        ModelState.AddModelError("", error.Description);
                    }
                }
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null)
                return NotFound("Unable to locate user.");

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                return BadRequest(result.Errors);
            }

            return RedirectToAction("Index");
        }
    }
}
