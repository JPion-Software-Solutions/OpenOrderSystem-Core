using System.ComponentModel;
using System.Globalization;
using System.Runtime.ExceptionServices;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Formatters;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives;

namespace OpenOrderSystem.Core.API;

/// <summary>
/// Request binding for the legacy API. The legacy API used to be MVC controllers and its shape is locked, so this
/// reproduces what MVC model binding did: the same accepted content types, the same model state keys and error
/// messages, and the same 415/400 problem responses for <c>[ApiController]</c> endpoints. It uses MVC's own pieces
/// (<see cref="ModelStateDictionary"/>, <see cref="IObjectModelValidator"/>, <see cref="ProblemDetailsFactory"/>) so
/// messages and ordering come out identical.
/// </summary>
/// <remarks>
/// Create one per request, bind every parameter the old action had, then check <see cref="ApiControllerError"/> (for
/// endpoints that were <c>[ApiController]</c>) or <see cref="UnsupportedMediaTypeError"/> (for plain controllers).
/// </remarks>
internal sealed class MvcCompatBinder
{
    private static readonly MediaType[] JsonMediaTypes =
    [
        new("application/json"),
        new("text/json"),
        new("application/*+json")
    ];

    // SystemTextJsonInputFormatter's supported encodings, in order.
    private static readonly Encoding[] SupportedEncodings =
    [
        new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true),
        new UnicodeEncoding(bigEndian: false, byteOrderMark: true, throwOnInvalidBytes: true)
    ];

    private readonly HttpContext _context;
    private readonly IModelMetadataProvider _metadataProvider;
    private bool _unsupportedContentType;

    public MvcCompatBinder(HttpContext context)
    {
        _context = context;
        _metadataProvider = context.RequestServices.GetRequiredService<IModelMetadataProvider>();
    }

    public ModelStateDictionary ModelState { get; } = new();

    /// <summary>
    /// Binds a non-nullable <c>[FromBody]</c> parameter. Returns null when the body was missing, empty, of the
    /// wrong content type or not valid JSON; the matching model state errors are recorded.
    /// </summary>
    public async Task<T?> BodyAsync<T>(string parameterName) where T : class
    {
        var metadata = _metadataProvider.GetMetadataForType(typeof(T));
        var model = await ReadJsonBodyAsync<T>(metadata);

        if (model == null)
            AddRequiredError(parameterName);
        else
            Validate(model, string.Empty);

        return model;
    }

    /// <summary>
    /// Binds a <c>[FromForm]</c> complex parameter whose properties are simple types. Like MVC, the model is always
    /// created, and the parameter name is used as a key prefix only when the form contains it.
    /// </summary>
    public async Task<T> FormAsync<T>(string parameterName) where T : class, new()
    {
        var request = _context.Request;
        var form = request.HasFormContentType ? await request.ReadFormAsync() : FormCollection.Empty;
        var prefix = ContainsPrefix(form.Keys, parameterName) ? parameterName : string.Empty;

        var model = new T();
        var metadata = _metadataProvider.GetMetadataForType(typeof(T));

        foreach (var property in metadata.Properties)
        {
            if (property.PropertySetter == null || property.PropertyName == null)
                continue;

            var key = ModelNames.CreatePropertyModelName(prefix, property.PropertyName);
            if (TryConvert(key, form[key], CultureInfo.CurrentCulture, property, out var value))
                property.PropertySetter(model, value);
        }

        Validate(model, prefix);
        return model;
    }

    /// <summary>Binds a string from the query string (<c>[FromQuery]</c>, or inferred by <c>[ApiController]</c>).</summary>
    public string? QueryString(string name, bool required) =>
        BindString(name, _context.Request.Query[name], required);

    /// <summary>Binds a string from a request header (<c>[FromHeader]</c>).</summary>
    public string? HeaderString(string name, bool required)
    {
        var values = _context.Request.Headers.GetCommaSeparatedValues(name);

        // MVC's HeaderModelBinder reports a header that is present but empty under the empty key.
        if (values.Length == 0 && _context.Request.Headers.ContainsKey(name))
        {
            if (required)
                AddRequiredError(string.Empty, name);
            return null;
        }

        return BindString(name, new StringValues(values), required);
    }

    /// <summary>Binds a string from a route value.</summary>
    public string? RouteString(string name, bool required) =>
        BindString(name, RouteValues(name), required);

    /// <summary>Binds a simple value type from the query string. Missing values leave <paramref name="fallback"/>.</summary>
    public T Query<T>(string name, T fallback = default!) =>
        BindSimple(name, _context.Request.Query[name], fallback);

    /// <summary>Binds a simple value type from a route value. Failed conversions leave <paramref name="fallback"/>.</summary>
    public T Route<T>(string name, T fallback = default!) =>
        BindSimple(name, RouteValues(name), fallback);

    /// <summary>
    /// What MVC's filters did for an <c>[ApiController]</c> before the action ran: 415 problem details when the body
    /// had an unsupported content type, otherwise 400 validation problem details when model state is invalid.
    /// </summary>
    public IResult? ApiControllerError()
    {
        var factory = _context.RequestServices.GetRequiredService<ProblemDetailsFactory>();

        if (_unsupportedContentType)
            return MvcCompatResults.Problem(factory.CreateProblemDetails(_context, StatusCodes.Status415UnsupportedMediaType));

        if (!ModelState.IsValid)
            return MvcCompatResults.Problem(factory.CreateValidationProblemDetails(_context, ModelState));

        return null;
    }

    /// <summary>
    /// What MVC did for a plain (non-<c>[ApiController]</c>) controller: an empty 415 when the body had an
    /// unsupported content type. Any other binding failure was ignored and the action ran with whatever was bound.
    /// </summary>
    public IResult? UnsupportedMediaTypeError() =>
        _unsupportedContentType ? Results.StatusCode(StatusCodes.Status415UnsupportedMediaType) : null;

    // --- body (BodyModelBinder + SystemTextJsonInputFormatter) ---------------------------------------------------

    private async Task<T?> ReadJsonBodyAsync<T>(ModelMetadata metadata) where T : class
    {
        var request = _context.Request;

        if (string.IsNullOrEmpty(request.ContentType) || !IsJsonContentType(request.ContentType))
        {
            AddUnsupportedContentType(metadata);
            return null;
        }

        var canHaveBody = _context.Features.Get<IHttpRequestBodyDetectionFeature>()?.CanHaveBody
                          ?? request.ContentLength != 0;
        if (!canHaveBody)
        {
            AddMissingBody(metadata);
            return null;
        }

        var encoding = SelectEncoding(request.ContentType);
        if (encoding == null)
        {
            AddUnsupportedContentType(metadata);
            return null;
        }

        var options = _context.RequestServices.GetRequiredService<IOptions<JsonOptions>>().Value.JsonSerializerOptions;
        var transcoding = encoding.CodePage == Encoding.UTF8.CodePage
            ? null
            : Encoding.CreateTranscodingStream(request.Body, encoding, Encoding.UTF8, leaveOpen: true);

        T? model;
        try
        {
            model = await JsonSerializer.DeserializeAsync<T>(transcoding ?? request.Body, options);
        }
        catch (JsonException ex)
        {
            ModelState.TryAddModelError(ex.Path ?? string.Empty, new InputFormatterException(ex.Message, ex), metadata);
            return null;
        }
        catch (Exception ex) when (ex is FormatException or OverflowException)
        {
            ModelState.TryAddModelError(string.Empty, ex, metadata);
            return null;
        }
        finally
        {
            if (transcoding != null)
                await transcoding.DisposeAsync();
        }

        // A literal JSON null is treated the same as an empty body.
        if (model == null)
            AddMissingBody(metadata);

        return model;
    }

    private static bool IsJsonContentType(string contentType)
    {
        var parsed = new MediaType(contentType);
        return JsonMediaTypes.Any(supported => parsed.IsSubsetOf(supported));
    }

    private static Encoding? SelectEncoding(string contentType)
    {
        var mediaType = new MediaType(contentType);
        if (!mediaType.Charset.HasValue)
            return SupportedEncodings[0];

        var requested = mediaType.Encoding;
        if (requested == null)
            return null;

        return SupportedEncodings.FirstOrDefault(e =>
            string.Equals(requested.WebName, e.WebName, StringComparison.OrdinalIgnoreCase));
    }

    private void AddUnsupportedContentType(ModelMetadata metadata)
    {
        _unsupportedContentType = true;
        ModelState.AddModelError(string.Empty,
            new UnsupportedContentTypeException($"Unsupported content type '{_context.Request.ContentType}'."),
            metadata);
    }

    private void AddMissingBody(ModelMetadata metadata) =>
        ModelState.AddModelError(string.Empty, metadata.ModelBindingMessageProvider.MissingRequestBodyRequiredValueAccessor());

    // --- simple types (SimpleTypeModelBinder) ---------------------------------------------------------------------

    private string? BindString(string key, StringValues values, bool required)
    {
        string? model = null;

        if (values.Count > 0)
        {
            ModelState.SetModelValue(key, values, values.ToString());
            var value = values[0];
            model = string.IsNullOrWhiteSpace(value) ? null : value;
        }

        if (model == null && required)
            AddRequiredError(key);
        else if (values.Count > 0)
            ModelState.MarkFieldValid(key);

        return model;
    }

    private T BindSimple<T>(string key, StringValues values, T fallback)
    {
        var metadata = _metadataProvider.GetMetadataForType(typeof(T));
        if (!TryConvert(key, values, CultureInfo.InvariantCulture, metadata, out var value) || value is not T typed)
            return fallback;

        // Top-level parameters are marked valid by MVC's validation; nothing else validates them here.
        ModelState.MarkFieldValid(key);
        return typed;
    }

    private bool TryConvert(string key, StringValues values, CultureInfo culture, ModelMetadata metadata, out object? value)
    {
        value = null;
        if (values.Count == 0)
            return false;

        ModelState.SetModelValue(key, values, values.ToString());
        var raw = values[0];

        try
        {
            value = string.IsNullOrWhiteSpace(raw)
                ? null
                : TypeDescriptor.GetConverter(metadata.UnderlyingOrModelType).ConvertFrom(null, culture, raw);
        }
        catch (Exception exception)
        {
            // TypeConverter wraps the FormatException; MVC unwraps it so the "value is not valid" message is used.
            if (exception is not FormatException && exception.InnerException != null)
                exception = ExceptionDispatchInfo.Capture(exception.InnerException).SourceException;

            ModelState.TryAddModelError(key, exception, metadata);
            return false;
        }

        if (value == null && !metadata.IsReferenceOrNullableType)
        {
            ModelState.TryAddModelError(key, metadata.ModelBindingMessageProvider.ValueMustNotBeNullAccessor(values.ToString()));
            return false;
        }

        if (value != null && metadata.UnderlyingOrModelType.IsEnum && !Enum.IsDefined(metadata.UnderlyingOrModelType, value))
        {
            ModelState.TryAddModelError(key,
                metadata.ModelBindingMessageProvider.ValueIsInvalidAccessor(values.ToString()));
            value = null;
            return false;
        }

        return true;
    }

    private StringValues RouteValues(string name) =>
        _context.GetRouteValue(name) is { } value ? new StringValues(Convert.ToString(value, CultureInfo.InvariantCulture)) : StringValues.Empty;

    private static bool ContainsPrefix(IEnumerable<string> keys, string prefix) =>
        keys.Any(key => key.Length >= prefix.Length
                        && key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                        && (key.Length == prefix.Length || key[prefix.Length] is '.' or '['));

    // --- validation -----------------------------------------------------------------------------------------------

    private void AddRequiredError(string key, string? displayName = null) =>
        ModelState.TryAddModelError(key,
            new global::System.ComponentModel.DataAnnotations.RequiredAttribute().FormatErrorMessage(displayName ?? key));

    private void Validate(object model, string prefix)
    {
        var validator = _context.RequestServices.GetRequiredService<IObjectModelValidator>();
        var actionContext = new ActionContext(_context, _context.GetRouteData(), new ActionDescriptor(), ModelState);
        validator.Validate(actionContext, validationState: null, prefix, model);
    }
}
