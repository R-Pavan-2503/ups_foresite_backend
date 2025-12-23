namespace CodeFamily.Api.Core.Interfaces;

/// <summary>
/// Service for Supabase Storage operations.
/// </summary>
public interface ISupabaseStorageService
{
    /// <summary>
    /// Upload a file to the specified bucket.
    /// </summary>
    /// <returns>The public URL of the uploaded file.</returns>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string bucketName);

    /// <summary>
    /// Delete a file from storage.
    /// </summary>
    Task<bool> DeleteFileAsync(string fileUrl, string bucketName);

    /// <summary>
    /// Get a signed URL for a private file (valid for specified duration).
    /// </summary>
    Task<string> GetSignedUrlAsync(string filePath, string bucketName, int expiresInSeconds = 3600);
}
