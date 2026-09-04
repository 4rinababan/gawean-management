using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Options;
using TaskManagement.Application.Abstractions;

namespace TaskManagement.Infrastructure.Storage;

/// <summary>
/// Works against any S3-compatible endpoint (AWS S3, Cloudflare R2, Backblaze B2, MinIO...), not just
/// AWS — <see cref="Endpoint"/> takes the full service URL either way.
/// </summary>
public sealed class S3StorageOptions
{
    public const string SectionName = "S3Storage";

    public string Endpoint { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string BucketName { get; set; } = string.Empty;

    public string AccessKey { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(Endpoint)
        && !string.IsNullOrWhiteSpace(BucketName)
        && !string.IsNullOrWhiteSpace(AccessKey)
        && !string.IsNullOrWhiteSpace(SecretKey);
}

/// <summary>
/// Stores attachment bytes in an S3-compatible bucket instead of local disk — swapped in for
/// <see cref="LocalFileStorage"/> automatically once <see cref="S3StorageOptions.IsConfigured"/> is true.
/// Uses the same sharded key shape as <see cref="LocalFileStorage"/>, so nothing else has to change.
/// </summary>
public sealed class S3FileStorage : IFileStorage
{
    private readonly AmazonS3Client _client;
    private readonly string _bucket;

    public S3FileStorage(IOptions<S3StorageOptions> options)
    {
        var o = options.Value;
        _bucket = o.BucketName;

        var config = new AmazonS3Config
        {
            ServiceURL = o.Endpoint,
            AuthenticationRegion = string.IsNullOrWhiteSpace(o.Region) ? null : o.Region,
            ForcePathStyle = true, // required by most non-AWS S3-compatible providers
            // The SDK's v4 default (WHEN_SUPPORTED) adds a trailing checksum to the upload stream that
            // only AWS itself understands — R2 (and MinIO/B2) reject it with "STREAMING-AWS4-HMAC-
            // SHA256-PAYLOAD-TRAILER not implemented". WHEN_REQUIRED keeps checksums SigV4-only.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
        };
        _client = new AmazonS3Client(new BasicAWSCredentials(o.AccessKey, o.SecretKey), config);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        var extension = Path.GetExtension(fileName);
        var key = $"{DateTime.UtcNow:yyyy/MM}/{Guid.CreateVersion7():n}{extension}";

        await _client.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            AutoCloseStream = false, // the caller owns the stream's lifetime, same as LocalFileStorage
        }, ct);

        return key;
    }

    public async Task<Stream> OpenReadAsync(string storageKey, CancellationToken ct = default)
    {
        try
        {
            var response = await _client.GetObjectAsync(_bucket, storageKey, ct);
            return response.ResponseStream;
        }
        catch (AmazonS3Exception ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            throw new FileNotFoundException("Attachment blob not found.", storageKey);
        }
    }

    public Task DeleteAsync(string storageKey, CancellationToken ct = default)
        => _client.DeleteObjectAsync(_bucket, storageKey, ct);
}
