using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using FastCart.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Storage;

/// <summary>
/// Cloudflare R2 storage via the AWS S3 SDK (§9.3, D12). Selected when Storage:R2 is
/// configured; otherwise <see cref="LocalFileStorageService"/> is used for dev. The DB
/// only ever stores the returned public URL.
/// </summary>
public sealed class R2StorageService : IStorageService
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string _publicBaseUrl;

    public R2StorageService(IConfiguration config)
    {
        var r2 = config.GetSection("Storage:R2");
        _bucket = r2["Bucket"] ?? throw new InvalidOperationException("Storage:R2:Bucket is not configured.");
        _publicBaseUrl = (r2["PublicBaseUrl"] ?? string.Empty).TrimEnd('/');

        var s3Config = new AmazonS3Config
        {
            ServiceURL = r2["Endpoint"],
            ForcePathStyle = true
        };

        _s3 = new AmazonS3Client(
            new BasicAWSCredentials(r2["AccessKeyId"], r2["SecretAccessKey"]),
            s3Config);
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var key = $"{folder}/{Guid.NewGuid():N}{Path.GetExtension(fileName)}";

        await _s3.PutObjectAsync(new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType
        }, ct);

        return $"{_publicBaseUrl}/{key}";
    }

    public async Task DeleteAsync(string url, CancellationToken ct = default)
    {
        var prefix = $"{_publicBaseUrl}/";
        if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var key = url[prefix.Length..];
            await _s3.DeleteObjectAsync(new DeleteObjectRequest { BucketName = _bucket, Key = key }, ct);
        }
    }
}
