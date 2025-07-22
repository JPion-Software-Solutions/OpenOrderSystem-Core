using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Areas.API.Models;

namespace OpenOrderSystem.Core.Areas.API.Controllers
{
    [Area("API")]
    [ApiController]
    [Route("API/Identity/{action}")]
    public class IdentityController : ControllerBase
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public IdentityController(UserManager<IdentityUser> userManager, SignInManager<IdentityUser> signInManager)
        {
            _userManager = userManager;
            _signInManager = signInManager;
        }

        [HttpPost]
        public async Task<IResult> Login([FromBody] LoginModel model)
        {
            if (ModelState.IsValid)
            {
                var user = await _userManager.FindByNameAsync(model.Username);
                if (user == null)
                {
                    ModelState.AddModelError("", "Invalid username or password");
                    return Results.BadRequest(ModelState);
                }

                var validPass = await _userManager.CheckPasswordAsync(user, model.Password);
                if (!validPass)
                {
                    ModelState.AddModelError("", "Invalid username or password");
                    return Results.BadRequest(ModelState);
                }

                var result = await _signInManager.PasswordSignInAsync(user, model.Password, false, true);

                return result.Succeeded ? Results.Ok() : Results.BadRequest(ModelState);
            }

            return Results.BadRequest(ModelState);
        }

        [HttpPost]
        [Authorize]
        public async Task<IResult> LogoutAsync()
        {
            await _signInManager.SignOutAsync();
            return Results.Ok();
        }

        [HttpGet]
        [Authorize]
        public string TestAuth() => "hello world";
    }
}
