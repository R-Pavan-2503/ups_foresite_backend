using CodeFamily.Api.Core.Interfaces;
using Microsoft.Extensions.Configuration;
using Supabase;

namespace CodeFamily.Api.Core.Services;

/// <summary>
/// Service for Supabase Storage operations.
/// </summary>
public class SupabaseStorageService : ISupabaseStorageService
{
    private readonly string _supabaseUrl;
    private readonly string _supabaseKey;
    private readonly ILogger<SupabaseStorageService> _logger;

    public SupabaseStorageService(IConfiguration configuration, ILogger<SupabaseStorageService> logger)
    {
        _supabaseUrl = configuration["Supabase:Url"]
            ?? throw new Exception("Supabase:Url is required in configuration");
        _supabaseKey = configuration["Supabase:ServiceKey"]
            ?? configuration["Supabase:AnonKey"]
            ?? throw new Exception("Supabase:ServiceKey or Supabase:AnonKey is required in configuration");
        _logger = logger;
    }

    public async Task<string> UploadFileAsync(Stream fileStream, string fileName, string bucketName)
    {
        try
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };

            var client = new Supabase.Client(_supabaseUrl, _supabaseKey, options);
            await client.InitializeAsync();

            // Generate unique filename to avoid conflicts
            var uniqueFileName = $"{Guid.NewGuid()}_{fileName}";

            // Read stream into byte array
            using var memoryStream = new MemoryStream();
            await fileStream.CopyToAsync(memoryStream);
            var fileBytes = memoryStream.ToArray();

            // Upload file
            var response = await client.Storage
                .From(bucketName)
                .Upload(fileBytes, uniqueFileName);

            if (response == null)
            {
                throw new Exception("Failed to upload file to Supabase Storage");
            }

            // Return the public URL
            var publicUrl = $"{_supabaseUrl}/storage/v1/object/public/{bucketName}/{uniqueFileName}";
            _logger.LogInformation("Uploaded file to Supabase Storage: {Url}", publicUrl);

            return publicUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error uploading file to Supabase Storage");
            throw;
        }
    }

    public async Task<bool> DeleteFileAsync(string fileUrl, string bucketName)
    {
        try
        {
            // Extract filename from URL
            var fileName = fileUrl.Split('/').Last();

            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };

            var client = new Supabase.Client(_supabaseUrl, _supabaseKey, options);
            await client.InitializeAsync();

            await client.Storage
                .From(bucketName)
                .Remove(new List<string> { fileName });

            _logger.LogInformation("Deleted file from Supabase Storage: {FileName}", fileName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting file from Supabase Storage: {Url}", fileUrl);
            return false;
        }
    }

    public async Task<string> GetSignedUrlAsync(string filePath, string bucketName, int expiresInSeconds = 3600)
    {
        try
        {
            var options = new Supabase.SupabaseOptions
            {
                AutoRefreshToken = false,
                AutoConnectRealtime = false
            };

            var client = new Supabase.Client(_supabaseUrl, _supabaseKey, options);
            await client.InitializeAsync();

            var signedUrl = await client.Storage
                .From(bucketName)
                .CreateSignedUrl(filePath, expiresInSeconds);

            return signedUrl;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating signed URL for file: {FilePath}", filePath);
            throw;
        }
    }
}
