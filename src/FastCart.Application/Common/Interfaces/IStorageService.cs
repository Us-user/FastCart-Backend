namespace FastCart.Application.Common.Interfaces;

/// <summary>
/// Object-storage abstraction (§9.3, D12). Phase 2 ships a local-disk implementation
/// for dev; Phase 3 adds the Cloudflare R2 (S3 SDK) implementation behind the same
/// interface. The DB only ever stores the returned URL.
/// </summary>
public interface IStorageService
{
    /// <summary>Stores a file and returns its public URL.</summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, string folder, CancellationToken ct = default);

    /// <summary>Removes a previously stored object by its public URL (best-effort).</summary>
    Task DeleteAsync(string url, CancellationToken ct = default);
}
