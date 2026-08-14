namespace DaftechCrm.Domain.Enums;

/// <summary>
/// Which backend LocalFileStorageService/CloudinaryFileStorageService (or
/// a future implementation) stores files against. LocalFileStorage is not
/// durable on Render's free/hobby tier (containers are ephemeral, no
/// mounted disk) — Cloudinary is the default for anything that must
/// survive a redeploy, e.g. ticket attachments.
/// </summary>
public enum StorageProvider
{
    LocalFileSystem,
    Cloudinary
}
