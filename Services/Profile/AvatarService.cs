using System.Text.RegularExpressions;
using SkiaSharp;

namespace IntelligentProgrammingPlatform.Services.Profile;

public sealed class AvatarService : IDisposable
{
    public const int MaxBytes = 4 * 1024 * 1024;
    public const int MaxSide = 512;
    private static readonly Regex ManagedPath = new(@"\A/uploads/avatars/[a-f0-9]{32}\.webp\z", RegexOptions.CultureInvariant);
    private readonly SemaphoreSlim _decoders = new(2, 2);
    private readonly ILogger<AvatarService> _logger;
    private readonly string _webRoot;
    public string StorageRoot { get; }

    // Сақтау бумасын сервер ортасынан алып, сілтеме емес екенін тексереді.
    public AvatarService(IWebHostEnvironment environment, ILogger<AvatarService> logger)
    {
        _logger = logger;
        _webRoot = Path.GetFullPath(environment.WebRootPath);
        StorageRoot = Path.Combine(_webRoot, "uploads", "avatars");
        CheckDirectories();
        Directory.CreateDirectory(StorageRoot);
        CheckDirectories();
    }

    // Тек сервер жасаған жергілікті аватар жолын қабылдайды.
    public static bool IsManagedPath(string? path) => path != null && ManagedPath.IsMatch(path);

    // Нақты файлды шектеп оқып, толық декодтап, жаңа WebP суретін сақтайды.
    public async Task<string> SaveAsync(IFormFile file, CancellationToken cancellationToken)
    {
        if (file.Length <= 0) throw new AvatarValidationException("Avatar_Invalid");
        if (file.Length > MaxBytes) throw new AvatarValidationException("Avatar_TooLarge");
        await _decoders.WaitAsync(cancellationToken);
        try
        {
            using var input = file.OpenReadStream();
            using var bytes = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) != 0)
            {
                if (bytes.Length + read > MaxBytes) throw new AvatarValidationException("Avatar_TooLarge");
                bytes.Write(buffer, 0, read);
            }
            using var data = SKData.CreateCopy(bytes.ToArray());
            using var codec = SKCodec.Create(data);
            if (codec == null || codec.EncodedFormat is not (SKEncodedImageFormat.Jpeg or SKEncodedImageFormat.Png or SKEncodedImageFormat.Webp)
                || codec.FrameCount > 1) throw new AvatarValidationException("Avatar_Invalid");
            var info = codec.Info;
            if (info.Width <= 0 || info.Height <= 0 || info.Width > 8192 || info.Height > 8192
                || (long)info.Width * info.Height > 16_777_216)
                throw new AvatarValidationException("Avatar_Dimensions");
            using var source = new SKBitmap(new SKImageInfo(info.Width, info.Height, SKColorType.Rgba8888, SKAlphaType.Premul));
            if (codec.GetPixels(source.Info, source.GetPixels()) != SKCodecResult.Success)
                throw new AvatarValidationException("Avatar_Invalid");
            cancellationToken.ThrowIfCancellationRequested();
            using var normalized = Normalize(source, codec.EncodedOrigin);
            using var encoded = normalized.Encode(SKEncodedImageFormat.Webp, 85);
            if (encoded == null) throw new IOException("Avatar encoding failed.");
            var relative = "/uploads/avatars/" + Guid.NewGuid().ToString("N") + ".webp";
            var destination = Resolve(relative)!;
            var created = false;
            try
            {
                await using var output = new FileStream(destination, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                created = true;
                await output.WriteAsync(encoded.ToArray(), cancellationToken);
            }
            catch
            {
                if (created) Delete(relative);
                throw;
            }
            return relative;
        }
        finally { _decoders.Release(); }
    }

    // Бағытын түзетіп, ортасынан шаршы қиып, метадерексіз шағын сурет жасайды.
    private static SKImage Normalize(SKBitmap source, SKEncodedOrigin origin)
    {
        var w = source.Width;
        var h = source.Height;
        var rotated = (int)origin >= 5;
        var width = rotated ? h : w;
        var height = rotated ? w : h;
        var crop = Math.Min(width, height);
        var size = Math.Min(MaxSide, crop);
        using var target = new SKBitmap(size, size, SKColorType.Rgba8888, SKAlphaType.Premul);
        using var canvas = new SKCanvas(target);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale((float)size / crop);
        canvas.Translate(-(width - crop) / 2f, -(height - crop) / 2f);
        var matrix = origin switch
        {
            SKEncodedOrigin.TopRight => new SKMatrix(-1, 0, w, 0, 1, 0, 0, 0, 1),
            SKEncodedOrigin.BottomRight => new SKMatrix(-1, 0, w, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.BottomLeft => new SKMatrix(1, 0, 0, 0, -1, h, 0, 0, 1),
            SKEncodedOrigin.LeftTop => new SKMatrix(0, 1, 0, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightTop => new SKMatrix(0, -1, h, 1, 0, 0, 0, 0, 1),
            SKEncodedOrigin.RightBottom => new SKMatrix(0, -1, h, -1, 0, w, 0, 0, 1),
            SKEncodedOrigin.LeftBottom => new SKMatrix(0, 1, 0, -1, 0, w, 0, 0, 1),
            _ => SKMatrix.Identity
        };
        canvas.Concat(in matrix);
        using var image = SKImage.FromBitmap(source);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None));
        canvas.Flush();
        return SKImage.FromBitmap(target);
    }

    // Тек басқарылатын файлды жояды; тазалау қатесі аккаунт жұмысын тоқтатпайды.
    public void Delete(string? path)
    {
        try
        {
            var fullPath = Resolve(path);
            if (fullPath == null || !File.Exists(fullPath)) return;
            if ((File.GetAttributes(fullPath) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Avatar file cannot be a link.");
            File.Delete(fullPath);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Managed avatar cleanup failed");
        }
    }

    // Жолдың бақыланатын буманың ішінде қалуын тексереді.
    private string? Resolve(string? path)
    {
        if (!IsManagedPath(path)) return null;
        CheckDirectories();
        var fullPath = Path.GetFullPath(Path.Combine(StorageRoot, Path.GetFileName(path!)));
        if (!fullPath.StartsWith(StorageRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException("Avatar path is outside storage.");
        return fullPath;
    }

    // Сақтау жолындағы каталог сілтемелеріне жазуды және жоюды тоқтатады.
    private void CheckDirectories()
    {
        foreach (var directory in new[] { _webRoot, Path.Combine(_webRoot, "uploads"), StorageRoot })
            if (Directory.Exists(directory) && (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0)
                throw new IOException("Avatar storage cannot contain directory links.");
    }

    // Қосымша тоқтағанда ортақ декодтау семафорын босатады.
    public void Dispose() => _decoders.Dispose();
}
