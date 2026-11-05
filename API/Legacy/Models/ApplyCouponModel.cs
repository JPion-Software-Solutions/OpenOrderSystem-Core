// This type's full name appears in System.Text.Json error messages returned by the legacy API, so it keeps
// its original namespace from the removed API area. See API/Legacy/Models/__OriginalTypeNames.cs.
namespace OpenOrderSystem.Core.Areas.API.Legacy.Models
{
    public class ApplyCouponModel
    {
        public string CartId { get; set; } = string.Empty;
        public string PromoCode { get; set; } = string.Empty;
    }
}
