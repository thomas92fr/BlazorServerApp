namespace BlazorServerApp.ViewBlazor.Endpoints;

public static class UploadEndpoints
{
    private static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".svg", ".bmp"];

    public static IEndpointRouteBuilder MapUploadEndpoints(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/upload/image", async (IFormFile file, IWebHostEnvironment environment) =>
        {
            var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (!AllowedExtensions.Contains(ext))
            {
                return Results.BadRequest(new { Error = "File type not allowed." });
            }

            var uploadsPath = Path.Combine(environment.WebRootPath, "uploads");
            Directory.CreateDirectory(uploadsPath);

            // Clean up old files (older than today)
            DeleteOldFiles(uploadsPath);

            var fileName = $"upload-{DateTime.Today:yyyy-MM-dd}-{Guid.NewGuid()}{ext}";
            var filePath = Path.Combine(uploadsPath, fileName);

            await using var stream = new FileStream(filePath, FileMode.Create);
            await file.CopyToAsync(stream);

            return Results.Ok(new { Url = $"/uploads/{fileName}" });
        })
        .DisableAntiforgery();

        return endpoints;
    }

    private static void DeleteOldFiles(string uploadsPath)
    {
        var todayPrefix = $"upload-{DateTime.Today:yyyy-MM-dd}";

        foreach (var file in Directory.GetFiles(uploadsPath, "upload-*"))
        {
            if (!Path.GetFileName(file).StartsWith(todayPrefix))
            {
                try { File.Delete(file); } catch { /* ignore */ }
            }
        }
    }
}
