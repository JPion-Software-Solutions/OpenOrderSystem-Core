using System.Text.Encodings.Web;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.Extensions.Options;
using Microsoft.Net.Http.Headers;

namespace OpenOrderSystem.Core.API;

/// <summary>
/// Responses the legacy MVC controllers produced that the stock minimal API results don't reproduce.
/// </summary>
internal static class MvcCompatResults
{
    private static readonly string[] TextMediaTypes = ["text/plain"];
    private static readonly string[] JsonMediaTypes = ["application/json", "text/json", "application/*+json"];

    /// <summary>
    /// A content-negotiated object result, as MVC wrote one for a returned <c>string</c>/<c>bool</c> or a
    /// <c>NotFound("...")</c>: strings are written as <c>text/plain</c> unless the Accept header asks for JSON;
    /// anything else is JSON.
    /// </summary>
    public static IResult Negotiated(object value, int statusCode = StatusCodes.Status200OK) =>
        new NegotiatedResult(value, statusCode);

    /// <summary>Problem details written the way MVC wrote them (MVC JSON options, problem+json with charset).</summary>
    public static IResult Problem(ProblemDetails problem) => new ProblemResult(problem);

    /// <summary>
    /// What an <c>[ApiController]</c> wrote for a bare status code result such as <c>Unauthorized()</c> or
    /// <c>NotFound()</c>: problem details for that status.
    /// </summary>
    public static IResult ClientError(int statusCode) => new ClientErrorResult(statusCode);

    /// <summary>
    /// The headers <c>[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]</c> set.
    /// </summary>
    public static void NoStore(HttpContext context)
    {
        var headers = context.Response.Headers;
        headers.CacheControl = "no-store";
        headers.AppendCommaSeparatedValues(HeaderNames.CacheControl, "no-cache");
        headers.Pragma = "no-cache";
    }

    private static JsonSerializerOptions? _relaxedOptions;
    private static JsonSerializerOptions? _relaxedSource;

    /// <summary>
    /// The options MVC's SystemTextJsonOutputFormatter writes with: the MVC JSON options, using the relaxed encoder
    /// unless one was configured (so <c>'</c> is written as-is rather than <c>\u0027</c>).
    /// </summary>
    private static JsonSerializerOptions MvcJsonOptions(HttpContext context)
    {
        var options = context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        if (options.Encoder != null)
            return options;

        if (!ReferenceEquals(_relaxedSource, options))
        {
            _relaxedOptions = new JsonSerializerOptions(options) { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
            _relaxedSource = options;
        }

        return _relaxedOptions!;
    }

    private sealed class ClientErrorResult(int statusCode) : IResult
    {
        public Task ExecuteAsync(HttpContext httpContext)
        {
            var factory = httpContext.RequestServices.GetRequiredService<ProblemDetailsFactory>();
            return new ProblemResult(factory.CreateProblemDetails(httpContext, statusCode)).ExecuteAsync(httpContext);
        }
    }

    private sealed class ProblemResult(ProblemDetails problem) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            httpContext.Response.StatusCode = problem.Status ?? StatusCodes.Status500InternalServerError;
            httpContext.Response.ContentType = "application/problem+json; charset=utf-8";
            await JsonSerializer.SerializeAsync(httpContext.Response.Body, problem, problem.GetType(),
                MvcJsonOptions(httpContext), httpContext.RequestAborted);
        }
    }

    /// <summary>Mirrors MVC's DefaultOutputFormatterSelector over the default String and JSON output formatters.</summary>
    private sealed class NegotiatedResult(object value, int statusCode) : IResult
    {
        public async Task ExecuteAsync(HttpContext httpContext)
        {
            var canWriteText = value is string;
            var (asJson, contentType) = SelectFormatter(httpContext.Request, canWriteText);

            httpContext.Response.StatusCode = statusCode;
            httpContext.Response.ContentType = $"{contentType}; charset=utf-8";

            if (asJson)
                await JsonSerializer.SerializeAsync(httpContext.Response.Body, value, value.GetType(),
                    MvcJsonOptions(httpContext), httpContext.RequestAborted);
            else
                await httpContext.Response.WriteAsync((string)value, httpContext.RequestAborted);
        }

        private static (bool AsJson, string ContentType) SelectFormatter(HttpRequest request, bool canWriteText)
        {
            foreach (var accepted in AcceptableMediaTypes(request))
            {
                if (canWriteText && Match(TextMediaTypes, accepted) is { } text)
                    return (false, text);

                if (Match(JsonMediaTypes, accepted) is { } json)
                    return (true, json);
            }

            // No usable Accept header: the first formatter that can write the value wins.
            return canWriteText ? (false, TextMediaTypes[0]) : (true, JsonMediaTypes[0]);
        }

        private static string? Match(string[] supported, MediaType accepted)
        {
            foreach (var candidate in supported)
            {
                var mediaType = new MediaType(candidate);
                // Wildcard media types are never reflected back from an Accept header.
                if (!mediaType.HasWildcard && mediaType.IsSubsetOf(accepted))
                    return candidate;
            }

            return null;
        }

        private static IEnumerable<MediaType> AcceptableMediaTypes(HttpRequest request)
        {
            if (!MediaTypeHeaderValue.TryParseList(request.Headers.Accept, out var parsed))
                return [];

            // RespectBrowserAcceptHeader is off: an Accept header containing */* is ignored entirely.
            if (parsed.Any(m => m.MatchesAllTypes))
                return [];

            return parsed
                .OrderByDescending(m => m.Quality ?? 1.0)
                .Select(m => new MediaType(m.ToString()))
                .ToList();
        }
    }
}
