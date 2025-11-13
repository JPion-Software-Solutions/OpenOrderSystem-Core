
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using OpenOrderSystem.Core.Attributes;
using OpenOrderSystem.Core.Data;
using System.Text.Json;

namespace OpenOrderSystem.Core.Middleware
{
    public class PrinterBridgeAuth : IMiddleware
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<PrinterBridgeAuth> _logger;

        public PrinterBridgeAuth(ApplicationDbContext context, ILogger<PrinterBridgeAuth> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext httpContext, RequestDelegate next)
        {
            var endpoint = httpContext.Features.Get<IEndpointFeature>()?.Endpoint;
            var printBridgeAttribute = endpoint?.Metadata.GetMetadata<ValidatePrintBridgeAttribute>();
            if (printBridgeAttribute == null)
            {
                await next(httpContext);
                return;
            }

            httpContext.Request.EnableBuffering();

            using (var reader = new StreamReader(httpContext.Request.Body, encoding: System.Text.Encoding.UTF8, leaveOpen: true))
            {
                var requestBody = await reader.ReadToEndAsync();

                httpContext.Request.Body.Position = 0;

                if (!string.IsNullOrWhiteSpace(requestBody))
                {
                    try
                    {
                        using (var jsonDoc = JsonDocument.Parse(requestBody))
                        {
                            var root = jsonDoc.RootElement;

                            var printerId = root.TryGetProperty("printerId", out var printerIdElement)
                                ? printerIdElement.GetString() : string.Empty;

                            var clientId = root.TryGetProperty("clientId", out var clientIdElement)
                                ? clientIdElement.GetString() : string.Empty;

                            var printer = await _context.Printers.FirstOrDefaultAsync(p => p.Id == printerId);

                            if (printer == null || !printer.ValidateClient(clientId ?? "INVALID"))
                            {
                                httpContext.Response.StatusCode = 400;
                                await httpContext.Response.WriteAsync("Invalid client/printer relationship.");
                                return;
                            }
                        }
                    }
                    catch (JsonException ex)
                    {
                        httpContext.Response.StatusCode = 400;
                        await httpContext.Response.WriteAsync("Invalid JSON format.");
                        _logger.LogWarning(ex, ex.Message);
                        return;
                    }
                }

            }

            //allow request to proceed
            await next(httpContext);
            return;
        }
    }
}
