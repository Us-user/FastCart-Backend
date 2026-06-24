using FastCart.Application.Common.Interfaces;
using Microsoft.Extensions.Configuration;

namespace FastCart.Infrastructure.Storage;

/// <summary>
/// Local-disk storage for development (D12: "local disk only for local dev"). Writes
/// under <c>wwwroot/uploads</c> (served as static files) and returns a public URL built
/// from <c>Storage:R2:PublicBaseUrl</c>. Phase 3 adds the R2 implementation behind the
/// same <see cref="IStorageService"/>.
/// </summary>
public sealed class LocalFileStorageService : IStorageService
{
    private readonly string _root;
    private readonly string _publicBaseUrl;

    public LocalFileStorageService(IConfiguration config)
    {
        // wwwroot/uploads under the running content root.
        _root = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _publicBaseUrl = (config["Storage:R2:PublicBaseUrl"] ?? "/uploads").TrimEnd('/');
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default)
    {
        var ext = Path.GetExtension(fileName);
        var safeName = $"{Guid.NewGuid():N}{ext}";
        var targetDir = Path.Combine(_root, folder);
        Directory.CreateDirectory(targetDir);

        var fullPath = Path.Combine(targetDir, safeName);
        await using (var file = File.Create(fullPath))
        {
            await content.CopyToAsync(file, ct);
        }

        return $"{_publicBaseUrl}/{folder}/{safeName}";
    }

    public Task DeleteAsync(string url, CancellationToken ct = default)
    {
        var prefix = $"{_publicBaseUrl}/";
        if (url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            var relative = url[prefix.Length..].Replace('/', Path.DirectorySeparatorChar);
            var fullPath = Path.Combine(_root, relative);
            if (File.Exists(fullPath))
            {
                File.Delete(fullPath);
            }
        }

        return Task.CompletedTask;
    }
}
