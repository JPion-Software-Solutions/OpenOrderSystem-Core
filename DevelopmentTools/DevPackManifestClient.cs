using System.Net.Http.Headers;
using System.Text.Json;

namespace OpenOrderSystem.Core.DevelopmentTools;

public sealed class DevPackManifestClient
{
    private readonly HttpClient _http;

    public DevPackManifestClient(HttpClient http)
    {
        _http = http;
    }

    public async Task<DevPackManifest> GetManifestAsync(string manifestUrl, CancellationToken ct = default)
    {
        // Build a request explicitly so we can tweak headers, add auth later, etc.
        using var request = new HttpRequestMessage(HttpMethod.Get, manifestUrl);

        // Make it clear we expect JSON (some CDNs behave better with Accept set).
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        // HeadersRead avoids buffering the entire response in memory up front.
        using var response = await _http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        // Optional: protect yourself from accidentally downloading a huge file.
        const long maxBytes = 1 * 1024 * 1024; // 1 MB (tune as needed)
        if (response.Content.Headers.ContentLength is long len && len > maxBytes)
            throw new InvalidOperationException($"Manifest too large: {len} bytes.");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);

        // JsonSerializerOptions lets you be a bit forgiving about casing.
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        };

        var manifest = await JsonSerializer.DeserializeAsync<DevPackManifest>(stream, options, ct);

        // If JSON is empty or invalid, DeserializeAsync returns null.
        return manifest ?? throw new InvalidOperationException("Manifest JSON was empty or invalid.");
    }
}

