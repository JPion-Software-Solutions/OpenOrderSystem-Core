using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc;

// The request model keeps the full type name it had as a nested class of the old AccessController, because
// System.Text.Json puts it into the error messages this endpoint returns. See API/Legacy/Models/__OriginalTypeNames.cs.

namespace OpenOrderSystem.Core.Areas.API.V1.System.Maintenance_Bypass
{
    [NonController]
    public static class AccessController
    {
        public class BypassLoginRequest
        {
            [Required]
            public string Username { get; set; } = string.Empty;

            [Required]
            public string Password { get; set; } = string.Empty;

            [Required]
            public DateTime Expires { get; set; }
        }
    }
}
