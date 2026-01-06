using Google.Cloud.Storage.V1;
using Microsoft.Extensions.Configuration;
using CodeFamily.Api.Core.Interfaces;

namespace CodeFamily.Api.Core.Services;

public class GcsStorageService : IStorageService
{
    private readonly StorageClient _storageClient;
    private readonly string _bucketName;

    public GcsStorageService(IConfiguration configuration)
    {
        _storageClient = StorageClient.Create();
        _bucketName = configuration["GCS:BucketName"] ?? "codefamily-files";
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string contentType)
    {
        var objectName = $"{Guid.NewGuid()}_{fileName}";

        await _storageClient.UploadObjectAsync(
            bucket: _bucketName,
            objectName: objectName,
            contentType: contentType,
            source: fileStream
        );

        // Return public URL
        return $"https://storage.googleapis.com/{_bucketName}/{objectName}";
    }

    public async Task<bool> DeleteFileAsync(string fileUrl)
    {
        try
        {
            // Extract object name from URL
            // URL format: https://storage.googleapis.com/{bucket}/{objectName}
            var uri = new Uri(fileUrl);
            var objectName = uri.AbsolutePath.Substring(1 + _bucketName.Length + 1);

            await _storageClient.DeleteObjectAsync(_bucketName, objectName);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public async Task<Stream> DownloadFileAsync(string fileUrl)
    {
        try
        {
            // Extract object name from URL
            var uri = new Uri(fileUrl);
            var objectName = uri.AbsolutePath.Substring(1 + _bucketName.Length + 1);

            var memoryStream = new MemoryStream();
            await _storageClient.DownloadObjectAsync(_bucketName, objectName, memoryStream);
            memoryStream.Position = 0;
            return memoryStream;
        }
        catch (Exception ex)
        {
            throw new Exception($"Failed to download file from GCS: {ex.Message}", ex);
        }
    }
}
