using System;
using System.Net.Http.Headers;
using System.Security.Cryptography;

namespace OpenOrderSystem.Core.DevelopmentTools;

public static class ZipDownloader
{
    /// <summary>
    /// Downloads a file from zipUri to disk by streaming the HTTP response to a temp file,
    /// then atomically moves it into place. This avoids partial/corrupt final files.
    /// </summary>
    public static async Task<string> DownloadZipToDiskAsync(
        HttpClient http,
        string zipUri,
        string destinationDirectory,
        string finalFileName,
        string hash,
        CancellationToken ct = default)
    {
        // Ensure destination exists
        Directory.CreateDirectory(destinationDirectory);

        // Final and temp paths
        string finalPath = Path.Combine(destinationDirectory, finalFileName);

        // Put temp file in the same directory so File.Move is atomic on most filesystems
        string tempPath = Path.Combine(destinationDirectory, $"{finalFileName}.{Guid.NewGuid():N}.tmp");

        using var request = new HttpRequestMessage(HttpMethod.Get, zipUri);

        // ResponseHeadersRead is important: it prevents HttpClient from buffering the body in memory.
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        // Optional: basic size guard if Content-Length is available
        // (ZIP bombs are mostly an *extraction* problem, but this still helps)
        const long maxZipBytes = 500L * 1024 * 1024; // 500 MB — tune to your reality
        if (response.Content.Headers.ContentLength is long len && len > maxZipBytes)
            throw new InvalidOperationException($"ZIP is too large ({len} bytes).");

        // Stream the body straight into a file
        await using (var httpStream = await response.Content.ReadAsStreamAsync(ct))
        await using (var fileStream = new FileStream(
            tempPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 1024 * 1024, // 1 MB buffer = fewer syscalls, usually faster
            useAsync: true))
        {
            await httpStream.CopyToAsync(fileStream, ct);
        }

        var valid = false;
        using (var s = File.OpenRead(tempPath))
        {
            var d = await SHA256.HashDataAsync(s, ct);
            var ic = Convert.ToHexString(d);
            valid = hash.ToLowerInvariant() == ic.ToLowerInvariant();
        }

        // Move temp -> final (replace existing if desired)
        // .NET 6+ has File.Move(source, dest, overwrite: true)
        if (valid)
            File.Move(tempPath, finalPath, overwrite: true);
        else
            throw new InvalidOperationException("Download failed integrety check...");

        return finalPath;
    }
}
