using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using DaftechCrm.Application.Interfaces;
using DaftechCrm.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DaftechCrm.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files in Cloudinary via its REST API directly (signed
/// upload/destroy — no SDK dependency, same approach as
/// BrevoApiEmailSender for the email provider). Used instead of
/// LocalFileStorageService in any environment without a persistent disk
/// (e.g. Render's free/hobby tier, where the container filesystem is
/// wiped on every redeploy).
///
/// StorageKey (what callers persist, e.g. Ticket.AttachmentStorageKey) is
/// "{resource_type}:{public_id}" — e.g. "video:a1b2c3" for a voice-note
/// recording — NOT a full URL, so a Cloudinary account migration or
/// CloudName change doesn't invalidate every stored reference. Files are
/// uploaded as resource_type "auto" (Cloudinary decides per file: "image"
/// for images, "video" for both video AND audio since Cloudinary has no
/// separate audio type, "raw" for everything else) so images and
/// non-image files (PDF, docx, audio) all work through one upload code
/// path — but that same auto-detection means a caller can't assume one
/// fixed resource_type for a given extension. The prefix records exactly
/// what Cloudinary chose for THIS file, so GetAsync/DeleteAsync never have
/// to guess it — see GetAsync's doc comment for what used to happen
/// without it (guessed-wrong resource_type reads as a permanently missing
/// file on any Cloudinary account with delivery restrictions on some
/// types), and its fallback path for keys saved before this prefix existed.
/// </summary>
public class CloudinaryFileStorageService : IFileStorageService
{
    private readonly HttpClient _http;
    private readonly CloudinaryOptions _options;
    private readonly StorageOptions _storageOptions;
    private readonly ILogger<CloudinaryFileStorageService> _logger;

    public CloudinaryFileStorageService(HttpClient http, IOptions<CloudinaryOptions> options, IOptions<StorageOptions> storageOptions, ILogger<CloudinaryFileStorageService> logger)
    {
        _http = http;
        _options = options.Value;
        _storageOptions = storageOptions.Value;
        _logger = logger;
        _http.Timeout = TimeSpan.FromSeconds(_options.TimeoutSeconds);
    }

    public async Task<StoredFileResult> SaveAsync(Stream content, string originalFileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();

        if (string.IsNullOrEmpty(extension) || !_storageOptions.AllowedExtensions.Contains(extension))
        {
            throw new FileValidationException(
                $"File type '{extension}' is not allowed. Allowed types: {string.Join(", ", _storageOptions.AllowedExtensions)}");
        }

        if (content.Length > _storageOptions.MaxFileSizeBytes)
        {
            var maxMb = _storageOptions.MaxFileSizeBytes / (1024.0 * 1024.0);
            throw new FileValidationException($"File exceeds the maximum allowed size of {maxMb:0.#} MB.");
        }

        // An empty stream would otherwise upload successfully as a
        // 0-byte asset and only surface as broken later, on download —
        // fail here instead, at the point where the cause is known.
        if (content.Length == 0)
        {
            throw new FileValidationException(
                "The uploaded file was empty or could not be read. Please try uploading it again.");
        }

        var effectiveContentType = string.IsNullOrWhiteSpace(contentType)
            ? "application/octet-stream"
            : contentType;

        var now = DateTimeOffset.UtcNow;
        var folder = $"{_options.Folder}/{now:yyyy/MM}";
        var publicIdOnly = Guid.NewGuid().ToString("N");
        var timestamp = now.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);

        // Cloudinary's signed-upload scheme: sign every request parameter
        // EXCEPT file, cloud_name, api_key, and resource_type — sorted
        // alphabetically, then SHA-1'd with the API secret appended. See
        // https://cloudinary.com/documentation/upload_images#generating_authentication_signatures
        var signature = Sign(new SortedDictionary<string, string>
        {
            ["folder"] = folder,
            ["public_id"] = publicIdOnly,
            ["timestamp"] = timestamp,
        });

        using var form = new MultipartFormDataContent();
        content.Position = 0;
        var fileContent = new StreamContent(content);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(effectiveContentType);
        form.Add(fileContent, "file", originalFileName);
        form.Add(new StringContent(folder), "folder");
        form.Add(new StringContent(publicIdOnly), "public_id");
        form.Add(new StringContent(timestamp), "timestamp");
        form.Add(new StringContent(_options.ApiKey), "api_key");
        form.Add(new StringContent(signature), "signature");

        var response = await _http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{_options.CloudName}/auto/upload", form, ct);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            _logger.LogError("Cloudinary upload failed ({Status}): {Body}", response.StatusCode, body);
            throw new FileValidationException("The file could not be uploaded. Please try again.");
        }

        var result = await response.Content.ReadFromJsonAsync<CloudinaryUploadResponse>(cancellationToken: ct)
            ?? throw new FileValidationException("Cloudinary returned an unexpected response.");

        _logger.LogInformation(
            "Uploaded file to Cloudinary {PublicId} as resource_type {ResourceType} ({SizeBytes} bytes)",
            result.PublicId, result.ResourceType, content.Length);

        // The StorageKey persisted on the owning row (Ticket, Agreement,
        // TrainingRecord...) is prefixed with the resource_type Cloudinary
        // actually assigned this upload ("image", "video" — which also
        // covers audio, since Cloudinary has no separate audio type — or
        // "raw"). Auto-detection means the SAME file extension can land
        // under a different resource_type per upload depending on what
        // Cloudinary's content sniffing decides, so GetAsync cannot safely
        // assume one type from the extension alone. Previously GetAsync
        // guessed by probing raw/image/video in turn; on any Cloudinary
        // account with delivery restrictions on some resource types (a
        // default on newer accounts), every probe can 401/403 and the file
        // reads back as permanently "missing from storage" even though the
        // upload succeeded — see GetAsync for the prefixed-key fast path
        // this enables, and its probing fallback for keys saved before
        // this fix shipped.
        var storageKey = $"{result.ResourceType}:{result.PublicId}";

        return new StoredFileResult(storageKey, result.SecureUrl, originalFileName, content.Length, effectiveContentType);
    }

    public async Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        // New-format key ("image:abc123", "video:abc123", "raw:abc123" —
        // see SaveAsync): the resource_type Cloudinary actually assigned
        // this file at upload time is right there, so fetch it directly
        // with no guessing.
        var prefixIndex = storageKey.IndexOf(':');
        if (prefixIndex > 0)
        {
            var knownResourceType = storageKey[..prefixIndex];
            var publicId = storageKey[(prefixIndex + 1)..];
            if (knownResourceType is "image" or "video" or "raw")
            {
                var direct = await TryDeliver(knownResourceType, publicId, ct);
                if (direct is not null) return direct;

                // The recorded resource_type didn't deliver (asset since
                // removed from Cloudinary directly, account delivery
                // settings changed after upload, etc.) — fall through to
                // the full probe below rather than giving up on just one
                // attempt, in case it actually landed under a different
                // type than what was recorded.
            }
        }

        // Old-format key (bare public_id, no prefix — saved before this
        // fix shipped) or the direct attempt above came back empty: fall
        // back to probing every resource_type Cloudinary could have
        // chosen. Files uploaded to an account with delivery restrictions
        // on raw/video will still 404 here exactly as before — that's a
        // Cloudinary account setting, not something this app controls;
        // see the class doc comment.
        var bareStorageKey = prefixIndex > 0 ? storageKey[(prefixIndex + 1)..] : storageKey;
        foreach (var resourceType in new[] { "raw", "image", "video" })
        {
            var found = await TryDeliver(resourceType, bareStorageKey, ct);
            if (found is not null) return found;
        }

        return null;
    }

    private async Task<RetrievedFile?> TryDeliver(string resourceType, string publicId, CancellationToken ct)
    {
        var deliveryUrl = $"https://res.cloudinary.com/{_options.CloudName}/{resourceType}/upload/{publicId}";
        var response = await _http.GetAsync(deliveryUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Logged at Warning (not Debug) specifically for this
            // resource_type/publicId pair, even though GetAsync tries
            // several — a 401/403 here (vs. 404) is the signature of a
            // Cloudinary account with delivery restrictions on that
            // resource type, which reads as "file lost" to the rest of
            // the app but is actually an account setting, not a missing
            // file — see the class doc comment. Check Render's logs for
            // this line's Status the next time a file "goes missing".
            _logger.LogWarning(
                "Cloudinary delivery attempt failed for {ResourceType}/{PublicId}: {Status}",
                resourceType, publicId, response.StatusCode);
            return null;
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var originalFileName = Path.GetFileName(publicId);
        return new RetrievedFile(stream, contentType, originalFileName);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        // Cloudinary's destroy endpoint (unlike upload) requires the
        // asset's real resource_type in the URL — "auto" is only valid on
        // /upload. A new-format key ("video:abc123", see SaveAsync) has it
        // right there; an old-format bare key predates that fix and is
        // assumed "raw" (the most common ticket-attachment case) since
        // there's nothing else to go on — a stale image/video asset from
        // before this fix may need clearing out by hand in the Cloudinary
        // console if this guess is wrong for it.
        var prefixIndex = storageKey.IndexOf(':');
        var resourceType = prefixIndex > 0 && storageKey[..prefixIndex] is "image" or "video" or "raw"
            ? storageKey[..prefixIndex]
            : "raw";
        var publicId = prefixIndex > 0 ? storageKey[(prefixIndex + 1)..] : storageKey;

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign(new SortedDictionary<string, string>
        {
            ["public_id"] = publicId,
            ["timestamp"] = timestamp,
        });

        using var form = new MultipartFormDataContent
        {
            { new StringContent(publicId), "public_id" },
            { new StringContent(timestamp), "timestamp" },
            { new StringContent(_options.ApiKey), "api_key" },
            { new StringContent(signature), "signature" },
        };

        var response = await _http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{_options.CloudName}/{resourceType}/destroy", form, ct);

        if (response.IsSuccessStatusCode)
            _logger.LogInformation("Deleted Cloudinary file {StorageKey}", storageKey);
        else
            _logger.LogWarning("Cloudinary delete failed for {StorageKey}: {Status}", storageKey, response.StatusCode);
    }

    public async Task<bool> ProbeAsync(CancellationToken ct = default)
    {
        try
        {
            using var probeContent = new MemoryStream(Encoding.UTF8.GetBytes("daftech-crm storage probe"));
            var result = await SaveAsync(probeContent, "probe.txt", "text/plain", ct);
            await DeleteAsync(result.StorageKey, ct);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Cloudinary storage health probe failed.");
            return false;
        }
    }

    private string Sign(SortedDictionary<string, string> paramsToSign)
    {
        var toSign = string.Join('&', paramsToSign.Select(kv => $"{kv.Key}={kv.Value}")) + _options.ApiSecret;
        var hash = SHA1.HashData(Encoding.UTF8.GetBytes(toSign));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private record CloudinaryUploadResponse(
        [property: JsonPropertyName("public_id")] string PublicId,
        [property: JsonPropertyName("secure_url")] string SecureUrl,
        [property: JsonPropertyName("resource_type")] string ResourceType);
}
