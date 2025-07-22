using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Areas.Identity.ViewModels.Account;

namespace OpenOrderSystem.Core.Areas.Identity.Controllers
{
    [Area("Identity")]
    public class AccountController : Controller
    {
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ILogger<AccountController> _logger;

        public AccountController(SignInManager<IdentityUser> signInManager,
            ILogger<AccountController> logger)
        {
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet]
        [Route("Account/Login")]
        [Route("Identity/Account/Login")]
        public IActionResult Login(string? returnUrl)
        {
            return View(new LoginVM { ReturnUrl = returnUrl });
        }

        [HttpPost]
        [Route("Account/Login")]
        [Route("Identity/Account/Login")]
        public async Task<IActionResult> Login(LoginVM model)
        {
            if (ModelState.IsValid)
            {
                var result = await _signInManager.PasswordSignInAsync(model.Username, model.Password, true, true);

                if (result.Succeeded)
                {
                    _logger.LogInformation($"User '{model.Username}' logged in.");
                    return LocalRedirect(model.ReturnUrl ?? "/");
                }
                else if (result.IsLockedOut)
                {
                    _logger.LogWarning($"User '{model.Username} locked out.");
                    ModelState.AddModelError("", "You have exceeded your maximum login attempts. " +
                        "Your account has been temporarily disabled. Please contact the administrator.");
                }
                else if (result.RequiresTwoFactor)
                {
                    return NotFound("Maybe someday...");
                }
                else
                {
                    ModelState.AddModelError("", "Invalid login attempt.");
                }
            }

            return View(model);
        }

        [HttpPost]
        [Route("Account/Logout")]
        [Route("Identity/Account/Logout")]
        public async Task<IActionResult> Logout(string? returnUrl)
        {
            await _signInManager.SignOutAsync();
            return LocalRedirect(returnUrl ?? "/");
        }

        [HttpGet]
        [Route("Account/AccessDenied")]
        [Route("Identity/Account/AccessDenied")]
        public IActionResult AccessDenied(string returnUrl)
        {
            ViewData["returnUrl"] = returnUrl;
            return View();
        }
    }
}
