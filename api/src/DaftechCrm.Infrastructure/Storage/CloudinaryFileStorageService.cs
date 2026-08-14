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
/// Cloudinary's "public_id" — NOT a full URL — so a Cloudinary account
/// migration or CloudName change doesn't invalidate every stored
/// reference. Files are uploaded as resource_type "auto" so images and
/// non-image files (PDF, docx) both work through one code path.
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
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
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

        _logger.LogInformation("Uploaded file to Cloudinary {PublicId} ({SizeBytes} bytes)", result.PublicId, content.Length);

        return new StoredFileResult(result.PublicId, result.SecureUrl, originalFileName, content.Length, contentType);
    }

    public async Task<RetrievedFile?> GetAsync(string storageKey, CancellationToken ct = default)
    {
        // Files are uploaded with default (public-read) access, so the
        // secure_url returned at upload time — reconstructed here from the
        // stored public_id — can be fetched directly without a signed
        // download URL. Fine for attachments that aren't sensitive
        // documents (contrast with agreement scans, which stay on
        // LocalFileStorageService/a private bucket).
        var deliveryUrl = $"https://res.cloudinary.com/{_options.CloudName}/raw/upload/{storageKey}";

        var response = await _http.GetAsync(deliveryUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
        {
            // Cloudinary splits delivery by detected resource_type at
            // upload time (image vs raw); retry the "image" path before
            // giving up, since ticket attachments are usually screenshots.
            deliveryUrl = $"https://res.cloudinary.com/{_options.CloudName}/image/upload/{storageKey}";
            response = await _http.GetAsync(deliveryUrl, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!response.IsSuccessStatusCode)
                return null;
        }

        var stream = await response.Content.ReadAsStreamAsync(ct);
        var contentType = response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream";
        var originalFileName = Path.GetFileName(storageKey);

        return new RetrievedFile(stream, contentType, originalFileName);
    }

    public async Task DeleteAsync(string storageKey, CancellationToken ct = default)
    {
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var signature = Sign(new SortedDictionary<string, string>
        {
            ["public_id"] = storageKey,
            ["timestamp"] = timestamp,
        });

        using var form = new MultipartFormDataContent
        {
            { new StringContent(storageKey), "public_id" },
            { new StringContent(timestamp), "timestamp" },
            { new StringContent(_options.ApiKey), "api_key" },
            { new StringContent(signature), "signature" },
        };

        var response = await _http.PostAsync(
            $"https://api.cloudinary.com/v1_1/{_options.CloudName}/auto/destroy", form, ct);

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
        [property: JsonPropertyName("secure_url")] string SecureUrl);
}
