using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using OpenOrderSystem.Core.Areas.API.Legacy.Models;

namespace OpenOrderSystem.Core.API.Legacy.Identity;

/// <summary>Legacy <c>api/legacy/Identity</c> endpoints (formerly IdentityController).</summary>
public static class IdentityApi
{
    // POST
    public static async Task<IResult> Login(
        HttpContext httpContext,
        [FromServices] UserManager<IdentityUser> userManager,
        [FromServices] SignInManager<IdentityUser> signInManager)
    {
        var binder = new MvcCompatBinder(httpContext);
        var model = await binder.BodyAsync<LoginModel>("model");
        if (binder.ApiControllerError() is { } error) return error;

        // The old action echoed its (by then valid, and therefore empty) ModelState with an added error.
        var modelState = new ModelStateDictionary();

        var user = await userManager.FindByNameAsync(model!.Username);
        if (user == null)
        {
            modelState.AddModelError("", "Invalid username or password");
            return Results.BadRequest(modelState);
        }

        var validPass = await userManager.CheckPasswordAsync(user, model.Password);
        if (!validPass)
        {
            modelState.AddModelError("", "Invalid username or password");
            return Results.BadRequest(modelState);
        }

        var result = await signInManager.PasswordSignInAsync(user, model.Password, false, true);

        return result.Succeeded ? Results.Ok() : Results.BadRequest(modelState);
    }

    // POST, authorized
    public static async Task<IResult> Logout([FromServices] SignInManager<IdentityUser> signInManager)
    {
        await signInManager.SignOutAsync();
        return Results.Ok();
    }

    // GET, authorized
    public static IResult TestAuth() => MvcCompatResults.Negotiated("hello world");
}
