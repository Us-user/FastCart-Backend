using FastCart.Application.Common.Exceptions;

namespace FastCart.Api.Common;

/// <summary>
/// Validates uploaded images by content type, extension, and size (§8). Allowed:
/// JPG / PNG / GIF / SVG, up to <see cref="MaxBytes"/>. Throws a 400 ValidationException.
/// </summary>
public static class ImageValidation
{
    public const long MaxBytes = 5 * 1024 * 1024; // 5 MB

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        "image/jpeg", "image/pjpeg", "image/png", "image/gif", "image/svg+xml"
    };

    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".jpg", ".jpeg", ".png", ".gif", ".svg"
    };

    public static void Validate(IFormFile file, string field = "image")
    {
        var errors = new List<string>();

        if (file.Length == 0)
        {
            errors.Add("File is empty.");
        }
        else if (file.Length > MaxBytes)
        {
            errors.Add($"File exceeds the {MaxBytes / (1024 * 1024)} MB limit.");
        }

        var ext = Path.GetExtension(file.FileName);
        if (!AllowedContentTypes.Contains(file.ContentType) && !AllowedExtensions.Contains(ext))
        {
            errors.Add("Only JPG, PNG, GIF, or SVG images are allowed.");
        }

        if (errors.Count > 0)
        {
            throw new ValidationException(new Dictionary<string, string[]> { [field] = errors.ToArray() });
        }
    }

    public static void ValidateAll(IEnumerable<IFormFile>? files, string field = "images")
    {
        if (files is null)
        {
            return;
        }

        foreach (var file in files)
        {
            Validate(file, field);
        }
    }
}
