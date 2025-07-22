using OpenOrderSystem.Core.Data;

namespace OpenOrderSystem.Core.Attributes
{
    public class ValidatePrintBridgeAttribute : Attribute
    {
        public ValidatePrintBridgeAttribute(string? errorMsg = null)
        {
            ErrorMsg = errorMsg ?? ErrorMsg;
        }

        public string ErrorMsg { get; set; } = "Client not associated with printer.";
    }
}
