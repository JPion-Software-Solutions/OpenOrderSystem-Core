using ESCPOS_NET;
using Microsoft.AspNetCore.Mvc;
using OpenOrderSystem.Core.Areas.API.Legacy.Models;

// System.Text.Json puts a type's full name into its error messages (for example "The JSON value could not be converted
// to OpenOrderSystem.Core.Areas.API.Legacy.Controllers.PrinterController+TestPrintModel"), and the legacy API returns
// those messages to clients. The legacy API's shape is locked, so the request models keep the names they had when
// they lived in the API area: these were nested in the old controllers, so static holder classes with the old
// controller names keep the nesting. Being static, MVC never treats them as controllers. The other request models
// (LoginModel, the Cart models, PrinterRegistrationModel) keep their old namespace in their own files.

namespace OpenOrderSystem.Core.Areas.API.Legacy.Controllers
{
    [NonController]
    public static class OrderController
    {
        public enum LocateMethod
        {
            OrderId,
            Phone
        }

        public class UpdateStatusModel
        {
            public int OrderId { get; set; }
        }
    }

    [NonController]
    public static class PrinterController
    {
        public class RegisterExistingPrinterModel : PrinterRegistrationModel
        {
            public string PrinterId { get; set; } = string.Empty;
        }

        public class PrinterCheckInModel
        {
            public string PrinterId { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;

            public PrinterStatusEventArgs? Status { get; set; }
        }

        public class CompleteJobModel
        {
            public string JobId { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;
            public string PrinterId { get; set; } = string.Empty;
        }

        public class PrintOrderTicketModel
        {
            public int OrderId { get; set; }

            public string? PrinterId { get; set; } = string.Empty;

            public string? TemplateId { get; set; } = string.Empty;
        }

        public class TestPrintModel
        {
            public string PrinterId { get; set; } = string.Empty;
        }
    }
}

namespace OpenOrderSystem.Core.Areas.API.Legacy.Controllers.Staff
{
    [NonController]
    public static class TerminalServiceController
    {
        public class PingDto
        {
            public string State { get; set; } = string.Empty;
            public string? Printer { get; set; }
        }
    }
}
