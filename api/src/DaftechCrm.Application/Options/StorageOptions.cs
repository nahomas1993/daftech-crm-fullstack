using DaftechCrm.Domain.Enums;

namespace DaftechCrm.Application.Options;

/// <summary>Bound from appsettings.json ("Storage" section).</summary>
public class StorageOptions
{
    public const string SectionName = "Storage";

    public StorageProvider Provider { get; set; } = StorageProvider.LocalFileSystem;

    /// <summary>
    /// Root folder for uploaded files when Provider is LocalFileSystem.
    /// Files are further organized into {RootPath}/{yyyy}/{MM}/{guid}{ext}
    /// subfolders. Must be a path the API process can read and write —
    /// in Docker this should be a mounted volume so uploads survive
    /// container restarts (see docker-compose.yml).
    /// </summary>
    public string RootPath { get; set; } = "storage/uploads";

    /// <summary>
    /// File extensions allowed for upload, lowercase, including the
    /// leading dot. Audio types (.webm/.ogg/.m4a/.mp3/.wav) cover ticket
    /// voice-note recordings — .webm is what the browser MediaRecorder
    /// API produces by default in Chrome/Edge/Firefox, the others catch
    /// Safari/mobile and any file-picker fallback.
    /// </summary>
    public string[] AllowedExtensions { get; set; } =
        [".pdf", ".doc", ".docx", ".png", ".jpg", ".jpeg", ".webm", ".ogg", ".m4a", ".mp3", ".wav"];

    /// <summary>Maximum upload size in bytes. Default 10 MB.</summary>
    public long MaxFileSizeBytes { get; set; } = 10 * 1024 * 1024;
}
