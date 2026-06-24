using FastCart.Application.Common.Exceptions;
using FastCart.Application.Common.Interfaces;
using FastCart.Application.Content;
using FastCart.Domain.Entities;
using FastCart.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace FastCart.Infrastructure.Content;

/// <summary>Slider CRUD with multipart image upload (§6.12).</summary>
public sealed class SliderService : ISliderService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public SliderService(AppDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<SliderDto>> ListActiveAsync(CancellationToken ct = default) =>
        await _db.Sliders.AsNoTracking().Where(s => s.IsActive)
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new SliderDto(s.Id, s.ImageUrl, s.Title, s.Subtitle, s.SortOrder, s.IsActive))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SliderDto>> ListAllAsync(CancellationToken ct = default) =>
        await _db.Sliders.AsNoTracking()
            .OrderBy(s => s.SortOrder).ThenBy(s => s.Id)
            .Select(s => new SliderDto(s.Id, s.ImageUrl, s.Title, s.Subtitle, s.SortOrder, s.IsActive))
            .ToListAsync(ct);

    public async Task<SliderDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Sliders.AsNoTracking().Where(s => s.Id == id)
            .Select(s => new SliderDto(s.Id, s.ImageUrl, s.Title, s.Subtitle, s.SortOrder, s.IsActive))
            .FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Slider not found.");
    }

    public async Task<SliderDto> CreateAsync(SliderInput input, CancellationToken ct = default)
    {
        if (input.ImageContent is null)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["image"] = ["A slider image is required."] });
        }

        var slider = new Slider
        {
            Title = input.Title,
            Subtitle = input.Subtitle,
            SortOrder = input.SortOrder,
            IsActive = input.IsActive,
            ImageUrl = await SaveImageAsync(input, ct)
        };

        _db.Sliders.Add(slider);
        await _db.SaveChangesAsync(ct);
        return new SliderDto(slider.Id, slider.ImageUrl, slider.Title, slider.Subtitle, slider.SortOrder, slider.IsActive);
    }

    public async Task<SliderDto> UpdateAsync(int id, SliderInput input, CancellationToken ct = default)
    {
        var slider = await _db.Sliders.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Slider not found.");

        slider.Title = input.Title;
        slider.Subtitle = input.Subtitle;
        slider.SortOrder = input.SortOrder;
        slider.IsActive = input.IsActive;
        if (input.ImageContent is not null)
        {
            slider.ImageUrl = await SaveImageAsync(input, ct);
        }

        await _db.SaveChangesAsync(ct);
        return new SliderDto(slider.Id, slider.ImageUrl, slider.Title, slider.Subtitle, slider.SortOrder, slider.IsActive);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var slider = await _db.Sliders.FirstOrDefaultAsync(s => s.Id == id, ct)
            ?? throw new NotFoundException("Slider not found.");
        _db.Sliders.Remove(slider);
        await _db.SaveChangesAsync(ct);
    }

    private Task<string> SaveImageAsync(SliderInput input, CancellationToken ct) =>
        _storage.SaveAsync(input.ImageContent!, input.ImageFileName ?? "slider",
            input.ImageContentType ?? "application/octet-stream", "sliders", ct);
}

/// <summary>Banner CRUD with multipart image upload, optional category + countdown (§6.12).</summary>
public sealed class BannerService : IBannerService
{
    private readonly AppDbContext _db;
    private readonly IStorageService _storage;

    public BannerService(AppDbContext db, IStorageService storage)
    {
        _db = db;
        _storage = storage;
    }

    public async Task<IReadOnlyList<BannerDto>> ListActiveAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        return await _db.Banners.AsNoTracking()
            .Where(b => b.IsActive && (b.EndsAt == null || b.EndsAt > now))
            .OrderByDescending(b => b.Id)
            .Select(b => new BannerDto(b.Id, b.ImageUrl, b.Title, b.CategoryId, b.EndsAt, b.IsActive))
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<BannerDto>> ListAllAsync(CancellationToken ct = default) =>
        await _db.Banners.AsNoTracking()
            .OrderByDescending(b => b.Id)
            .Select(b => new BannerDto(b.Id, b.ImageUrl, b.Title, b.CategoryId, b.EndsAt, b.IsActive))
            .ToListAsync(ct);

    public async Task<BannerDto> GetAsync(int id, CancellationToken ct = default)
    {
        var dto = await _db.Banners.AsNoTracking().Where(b => b.Id == id)
            .Select(b => new BannerDto(b.Id, b.ImageUrl, b.Title, b.CategoryId, b.EndsAt, b.IsActive))
            .FirstOrDefaultAsync(ct);
        return dto ?? throw new NotFoundException("Banner not found.");
    }

    public async Task<BannerDto> CreateAsync(BannerInput input, CancellationToken ct = default)
    {
        if (input.ImageContent is null)
        {
            throw new ValidationException(new Dictionary<string, string[]> { ["image"] = ["A banner image is required."] });
        }

        await EnsureCategoryAsync(input.CategoryId, ct);

        var banner = new Banner
        {
            Title = input.Title,
            CategoryId = input.CategoryId,
            EndsAt = input.EndsAt,
            IsActive = input.IsActive,
            ImageUrl = await SaveImageAsync(input, ct)
        };

        _db.Banners.Add(banner);
        await _db.SaveChangesAsync(ct);
        return new BannerDto(banner.Id, banner.ImageUrl, banner.Title, banner.CategoryId, banner.EndsAt, banner.IsActive);
    }

    public async Task<BannerDto> UpdateAsync(int id, BannerInput input, CancellationToken ct = default)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Banner not found.");

        await EnsureCategoryAsync(input.CategoryId, ct);

        banner.Title = input.Title;
        banner.CategoryId = input.CategoryId;
        banner.EndsAt = input.EndsAt;
        banner.IsActive = input.IsActive;
        if (input.ImageContent is not null)
        {
            banner.ImageUrl = await SaveImageAsync(input, ct);
        }

        await _db.SaveChangesAsync(ct);
        return new BannerDto(banner.Id, banner.ImageUrl, banner.Title, banner.CategoryId, banner.EndsAt, banner.IsActive);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var banner = await _db.Banners.FirstOrDefaultAsync(b => b.Id == id, ct)
            ?? throw new NotFoundException("Banner not found.");
        _db.Banners.Remove(banner);
        await _db.SaveChangesAsync(ct);
    }

    private async Task EnsureCategoryAsync(int? categoryId, CancellationToken ct)
    {
        if (categoryId is not null && !await _db.Categories.AnyAsync(c => c.Id == categoryId, ct))
        {
            throw new NotFoundException("Category not found.");
        }
    }

    private Task<string> SaveImageAsync(BannerInput input, CancellationToken ct) =>
        _storage.SaveAsync(input.ImageContent!, input.ImageFileName ?? "banner",
            input.ImageContentType ?? "application/octet-stream", "banners", ct);
}
