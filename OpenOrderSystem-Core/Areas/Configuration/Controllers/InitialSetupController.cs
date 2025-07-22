using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenOrderSystem.Core.Areas.Configuration.ViewModels.InitialSetup;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Services;
using OpenOrderSystem.Core.Services.Interfaces;
using OpenOrderSystem.Core.Attributes;
using OpenOrderSystem.Core.Data.DataModels;
using System.Security.Cryptography;
using System.Text;

namespace OpenOrderSystem.Core.Areas.Configuration.Controllers
{
    [Area("Configuration")]
    [InitialConfig(redirectOnFail: "/Configuration/Recovery")]
    public class InitialSetupController : Controller
    {
        private readonly ConfigurationService _configurationService;
        private readonly IEmailService _emailService;
        private readonly ISmsService _smsService;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<InitialSetupController> _logger;

        public InitialSetupController(ConfigurationService configurationService,
            IEmailService emailService, ISmsService smsService,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager,
            ApplicationDbContext context, ILogger<InitialSetupController> logger)
        {
            _configurationService = configurationService;
            _emailService = emailService;
            _smsService = smsService;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
            _context = context;
        }

        [Route("/Welcome")]
        public IActionResult Index()
        {
            //generate the recovery key and store a hash of it in the settings
            using (var random = RandomNumberGenerator.Create())
            {
                ViewBag.RecoveryKey = "";
                byte[] buffer = new byte[14];
                random.GetBytes(buffer);
                var key = Convert.ToHexString(buffer);
                for (int i = 0; i < key.Length; i++)
                {
                    if (i % 7 == 0 && i != 0)
                        ViewBag.RecoveryKey += $"-{key[i]}";
                    else
                        ViewBag.RecoveryKey += key[i];
                }

                using (var sha512 = SHA256.Create())
                {
                    var dataBytes = Encoding.UTF8.GetBytes(key);
                    var hash = sha512.ComputeHash(dataBytes);
                    _configurationService.Settings.RecoveryKeyHash = Convert.ToBase64String(hash);
                }
            }

            return View();
        }

        [HttpGet]
        public IActionResult CreateAdmin()
        {
            return View(new CreateAdminVM());
        }

        [HttpPost]
        public async Task<IActionResult> CreateAdmin(CreateAdminVM model)
        {
            if (ModelState.IsValid)
            {
                model.PhoneNumber = _smsService.ConvertPhone(model.PhoneNumber);

                if (!_smsService.VerifyPhone(model.PhoneNumber))
                {
                    ModelState.AddModelError("PhoneNumber", "Please enter a valid US phone number.");
                    return View(model);
                }

                var newUser = Activator.CreateInstance<IdentityUser>();

                newUser.UserName = model.Username;
                newUser.NormalizedUserName = model.Username.Normalize();
                newUser.Email = model.Email;
                newUser.NormalizedEmail = model.Email.Normalize();
                newUser.PhoneNumber = model.PhoneNumber;

                var result = await _userManager.CreateAsync(newUser, model.Password);

                if (result.Succeeded)
                {
                    var userId = await _userManager.GetUserIdAsync(newUser);
                    var confirmCode = new ConfirmationCode { UserId = userId };
                    confirmCode.GenerateCode();

                    _emailService.Send(newUser.Email, "Confirm Email", confirmCode.Code);

                    _context.ConfirmationCodes.Add(confirmCode);
                    await _context.SaveChangesAsync();

                    await _userManager.AddToRoleAsync(newUser, "default_admin");
                    await _userManager.AddToRoleAsync(newUser, "admin");
                    await _userManager.AddToRoleAsync(newUser, "manager");
                    await _userManager.AddToRoleAsync(newUser, "terminal_user");

                    return View("ConfirmEmail", new ConfirmEmailVM { UserId = newUser.Id });
                }
                else
                {
                    foreach (var err in result.Errors)
                    {
                        ModelState.AddModelError("", err.Description);
                    }
                }
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult ConfirmEmail()
        {
            return View();
        }

        [HttpPost]
        public IActionResult ConfirmEmail(ConfirmEmailVM model)
        {
            if (ModelState.IsValid)
            {
                //find confirmation code
                var cc = _context.ConfirmationCodes
                    .FirstOrDefault(c => c.Code == model.ConfirmationCode);

                if (cc == null)
                {
                    ModelState.AddModelError("ConfirmationCode", "Invalid code.");
                    return View(model);
                }

                //verify correct user
                if (cc.UserId != model.UserId) //prevents use of another user's codes
                {
                    ModelState.AddModelError("ConfirmationCode", "Invalid code.");
                    return View(model);
                }

                //check if code is expired
                if (!cc.IsValid)
                {
                    ModelState.AddModelError("ConfirmationCode", "Code expired.");
                    return View(model);
                }

                //update user
                var user = _context.Users
                    .FirstOrDefault(u => u.Id == model.UserId);

                if (user == null)
                {
                    return NotFound("Should be impossible...");
                }

                user.EmailConfirmed = true;

                //save changes
                _context.Update(user);
                _context.SaveChangesAsync();

                //redirect to next step
                return RedirectToAction(nameof(SetSchedule));
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult SetSchedule()
        {
            return View(new SetScheduleVM());
        }

        [HttpPost]
        public IActionResult SetSchedule(SetScheduleVM model)
        {
            if (ModelState.IsValid)
            {
                var validHours = true;

                for (int i = 0; i < model.OpenTimes.Length; i++)
                {
                    var open = model.OpenTimes[i];
                    var close = model.CloseTimes[i];

                    if (open > close)
                    {
                        validHours = false;
                        ModelState.AddModelError($"DayLabels[{i}]",
                            "Cannot set closeing earlier than opening.");
                        continue;
                    }

                    _configurationService.Settings.Schedule[i].Open = open;
                    _configurationService.Settings.Schedule[i].Close = close;
                }

                if (validHours)
                    return RedirectToAction(nameof(PrinterSetup));
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult PrinterSetup()
        {
            return View(new PrinterSetupVM());
        }

        [HttpPost]
        public IActionResult PrinterSetup(PrinterSetupVM model)
        {
            if (ModelState.IsValid)
            {
                _configurationService.Settings.LocalPrinterAddress =
                    $"{model.PrinterAddress}:{model.PrinterPort}";

                return RedirectToAction(nameof(Congratulations));
            }

            return View(model);
        }

        [HttpGet]
        public IActionResult Congratulations()
        {
            _configurationService.SaveChages();

            return View();
        }
    }
}
