using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace OpenOrderSystem.Core.Data.DataModels
{
    public class MaintenanceBypassToken
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();

        [Required]
        public string IssuedById { get; set; } = string.Empty;
        public IdentityUser? IssuedBy { get; set; }

        [Required]
        public DateTime IssuedAt { get; set; }

        [Required]
        public DateTime ExpiresAt { get; set; }

        public DateTime? LastUsedAt { get; set; }
        public string? LastUsedApiCallerIp { get; set; }  // trusted
        public string? LastUsedClientReportedIp { get; set; } // untrusted but useful
    }
}
