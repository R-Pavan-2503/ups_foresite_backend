namespace CodeFamily.Core.Services;

public interface IStorageService
{
    /// <summary>
    /// Upload a file to storage
    /// </summary>
    /// <returns>The public URL of the uploaded file</returns>
    Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType);

    /// <summary>
    /// Delete a file from storage
    /// </summary>
    Task<bool> DeleteFileAsync(string fileUrl);

    /// <summary>
    /// Download a file from storage
    /// </summary>
    Task<Stream> DownloadFileAsync(string fileUrl);
}
