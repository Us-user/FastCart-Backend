using System.ComponentModel.DataAnnotations;

namespace FastCart.Application.Content;

// ── Sliders (§6.12) ──────────────────────────────────────────────────────────

public interface ISliderService
{
    /// <summary>Active sliders ordered for the home page (public).</summary>
    Task<IReadOnlyList<SliderDto>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>All sliders incl. inactive, ordered (admin).</summary>
    Task<IReadOnlyList<SliderDto>> ListAllAsync(CancellationToken ct = default);

    Task<SliderDto> GetAsync(int id, CancellationToken ct = default);
    Task<SliderDto> CreateAsync(SliderInput input, CancellationToken ct = default);
    Task<SliderDto> UpdateAsync(int id, SliderInput input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record SliderDto(int Id, string ImageUrl, string? Title, string? Subtitle, int SortOrder, bool IsActive);

/// <summary>Slider create/update — image stream plus copy/ordering (multipart, §6.12).</summary>
public sealed record SliderInput
{
    public string? Title { get; init; }
    public string? Subtitle { get; init; }
    public int SortOrder { get; init; }
    public bool IsActive { get; init; } = true;

    public Stream? ImageContent { get; init; }
    public string? ImageFileName { get; init; }
    public string? ImageContentType { get; init; }
}

// ── Banners (§6.12) ──────────────────────────────────────────────────────────

public interface IBannerService
{
    /// <summary>Active banners (countdown not yet elapsed) for the home page (public).</summary>
    Task<IReadOnlyList<BannerDto>> ListActiveAsync(CancellationToken ct = default);

    /// <summary>All banners incl. inactive/expired (admin).</summary>
    Task<IReadOnlyList<BannerDto>> ListAllAsync(CancellationToken ct = default);

    Task<BannerDto> GetAsync(int id, CancellationToken ct = default);
    Task<BannerDto> CreateAsync(BannerInput input, CancellationToken ct = default);
    Task<BannerDto> UpdateAsync(int id, BannerInput input, CancellationToken ct = default);
    Task DeleteAsync(int id, CancellationToken ct = default);
}

public sealed record BannerDto(int Id, string ImageUrl, string? Title, int? CategoryId, DateTime? EndsAt, bool IsActive);

/// <summary>Banner create/update — image stream, optional category + countdown (multipart, §6.12).</summary>
public sealed record BannerInput
{
    public string? Title { get; init; }
    public int? CategoryId { get; init; }
    public DateTime? EndsAt { get; init; }
    public bool IsActive { get; init; } = true;

    public Stream? ImageContent { get; init; }
    public string? ImageFileName { get; init; }
    public string? ImageContentType { get; init; }
}
