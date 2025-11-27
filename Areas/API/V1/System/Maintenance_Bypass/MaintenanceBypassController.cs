using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Data;
using OpenOrderSystem.Core.Data.DataModels;
using System;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Areas.API.V1.System.Maintenance_Bypass
{
    [Area("API")]
    [Route("API/V1/System/Maintenance_Bypass/{action}")]
    [ApiController]
    public class MaintenanceBypassController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signInManager;

        public MaintenanceBypassController(
            ApplicationDbContext db,
            UserManager<IdentityUser> userManager,
            SignInManager<IdentityUser> signInManager)
        {
            _db = db;
            _userManager = userManager;
            _signInManager = signInManager;
        }

        public class BypassLoginRequest
        {
            [Required]
            public string Username { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;

            [Required]
            public DateTime Expires { get; set; }
        }
        // POST: /API/v1/System/Maintenance_Bypass/Issue
        [HttpPost]
        public async Task<IActionResult> Issue([FromBody] BypassLoginRequest req)
        {
            var user = await _userManager.FindByNameAsync(req.Username);
            if (user == null)
                return Unauthorized();

            var pwCheck = await _signInManager.CheckPasswordSignInAsync(user, req.Password, false);
            if (!pwCheck.Succeeded)
                return Unauthorized();

            var token = new MaintenanceBypassToken
            {
                Id = Guid.NewGuid().ToString(), // PK and token
                IssuedById = user.Id,
                IssuedBy = user,
                IssuedAt = DateTime.UtcNow,
                ExpiresAt = req.Expires
            };

            _db.MaintenanceBypassTokens.Add(token);
            await _db.SaveChangesAsync();

            return Ok(token.Id); // return the token
        }

        // GET: /API/v1/System/Maintenance_Bypass/Validate/
        [HttpGet]
        public async Task<IActionResult> Validate(string token, string fromIp)
        {
            var entity = await _db.MaintenanceBypassTokens
                .FirstOrDefaultAsync(t => t.Id == token);

            if (entity == null)
                return Unauthorized();

            if (DateTime.UtcNow > entity.ExpiresAt)
            {
                _db.MaintenanceBypassTokens.Remove(entity);
                await _db.SaveChangesAsync();
                return Unauthorized();
            }

            entity.LastUsedAt = DateTime.UtcNow;
            entity.LastUsedApiCallerIp = HttpContext.Connection.RemoteIpAddress?.ToString();
            entity.LastUsedClientReportedIp = fromIp;
            await _db.SaveChangesAsync();

            return Ok();
        }
    }
}

