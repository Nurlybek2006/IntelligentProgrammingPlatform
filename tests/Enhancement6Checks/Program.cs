using System.Text;
using System.Text.Json;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Profile;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.ViewModels.Account;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Identity;
using SkiaSharp;

await Checks.RunAsync();

static class Checks
{
    // Шарт орындалмаса тексерісті тоқтатып, сәтті нәтижені көрсетеді.
    private static void Assert(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
        Console.WriteLine("PASS: " + message);
    }

    // Нақты декодтау, файл қауіпсіздігі және бөлек базадағы seed саясатын тексереді.
    public static async Task RunAsync()
    {
        var root = Path.GetFullPath(Path.Combine("obj", "enh6-avatar-" + Guid.NewGuid().ToString("N")));
        Directory.CreateDirectory(root);
        try
        {
            using var service = new AvatarService(new TestEnvironment { WebRootPath = root }, NullLogger<AvatarService>.Instance);
            var fixtureRoot = Path.GetFullPath(Path.Combine("obj", "enh6-image-fixtures"));
            Directory.CreateDirectory(fixtureRoot);
            foreach (var format in new[] { SKEncodedImageFormat.Jpeg, SKEncodedImageFormat.Png, SKEncodedImageFormat.Webp })
            {
                var bytes = Image(format, 900, 600);
                File.WriteAllBytes(Path.Combine(fixtureRoot, format.ToString().ToLowerInvariant()), bytes);
                var path = await service.SaveAsync(Upload(bytes, "../../<img onerror=alert(1)>.exe", "text/html"), default);
                var physical = Path.Combine(root, path.TrimStart('/').Replace('/', Path.DirectorySeparatorChar));
                using var decoded = SKBitmap.Decode(physical);
                using var codecData = SKData.CreateCopy(System.IO.File.ReadAllBytes(physical));
                using var codec = SKCodec.Create(codecData);
                Assert(AvatarService.IsManagedPath(path) && decoded.Width == 512 && decoded.Height == 512
                    && codec.EncodedFormat == SKEncodedImageFormat.Webp, format + ": decoded, center cropped and normalized despite forged filename/MIME");
                var replacement = await service.SaveAsync(Upload(bytes), default);
                Assert(replacement != path, "Fresh random name prevents stale cache");
                service.Delete(path); service.Delete(replacement);
                Assert(!System.IO.File.Exists(physical), "Managed image removed");
            }
            foreach (var (bytes, label) in new[]
            {
                (Array.Empty<byte>(), "empty"), (Encoding.UTF8.GetBytes("not an image"), "fake JPEG"),
                (Encoding.UTF8.GetBytes("<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>"), "SVG"),
                (Encoding.UTF8.GetBytes("MZ executable"), "renamed executable"),
                (Convert.FromBase64String("R0lGODlhAQABAIAAAAAAAP///yH5BAEAAAAALAAAAAABAAEAAAIBRAA7"), "GIF"),
                (new byte[AvatarService.MaxBytes + 1], "oversize"),
                (Image(SKEncodedImageFormat.Png, 8193, 1), "dimension limit"),
                (Image(SKEncodedImageFormat.Jpeg, 900, 600)[..300], "truncated JPEG")
            })
            {
                try { await service.SaveAsync(Upload(bytes), default); throw new InvalidOperationException("Accepted " + label); }
                catch (AvatarValidationException) { Assert(true, "Reject " + label); }
            }
            var outside = Path.Combine(root, "sentinel.txt");
            System.IO.File.WriteAllText(outside, "keep");
            foreach (var path in new[] { "../sentinel.txt", outside, "//server/share/a.webp", "https://example.test/a.webp",
                "javascript:alert(1)", "data:text/html,x", "/uploads/avatars/../../sentinel.txt", "/uploads/avatars/" + new string('a',32) + ".webp\n" })
            {
                Assert(!AvatarService.IsManagedPath(path), "Reject unmanaged display/delete path"); service.Delete(path);
            }
            Assert(System.IO.File.ReadAllText(outside) == "keep", "Traversal cannot delete outside storage");
            Assert(UserAvatarViewModel.GetInitials(null) == "?" && UserAvatarViewModel.GetInitials("   ") == "?"
                && UserAvatarViewModel.GetInitials("  Әлия   Өмірбек ") == "ӘӨ"
                && UserAvatarViewModel.GetInitials("Nurlybek") == "N"
                && UserAvatarViewModel.GetInitials("A\u0301 Test") == "A\u0301T", "Unicode grapheme initials and whitespace fallback");
            Assert(!Directory.EnumerateFiles(service.StorageRoot).Any(), "Avatar test storage is clean");
            await SeedAsync(service);
        }
        finally
        {
            // Бұл UUID каталогын тек осы тексеріс жасайды және толтырады.
            Directory.Delete(root, recursive: true);
        }
        Console.WriteLine("ENHANCEMENT 6 SERVICE CHECKS PASSED; isolated database and temporary storage removed.");
    }

    // Нақты сурет байттарын тексеріс үшін жадта жасайды.
    private static byte[] Image(SKEncodedImageFormat format, int width, int height)
    {
        using var bitmap = new SKBitmap(width, height);
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.CornflowerBlue);
        using var image = SKImage.FromBitmap(bitmap);
        using var encoded = image.Encode(format, 90);
        return encoded.ToArray();
    }

    // HTTP файлының атауы мен MIME-ін еркін өзгерте алатын fixture құрады.
    private static IFormFile Upload(byte[] bytes, string name = "avatar.jpg", string type = "image/jpeg") =>
        new FormFile(new MemoryStream(bytes), 0, bytes.Length, "Avatar", name) { Headers = new HeaderDictionary(), ContentType = type };

    // Негізгі базаға тимей, seed қайталануын және әкімші түзетулерін сақтауды тексереді.
    private static async Task SeedAsync(AvatarService avatars)
    {
        var database = "IntelligentProgrammingPlatform_Enh6_" + Guid.NewGuid().ToString("N");
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(
            $"Server=localhost;Database={database};Trusted_Connection=True;TrustServerCertificate=True").Options;
        await using var db = new ApplicationDbContext(options);
        try
        {
            await db.Database.EnsureCreatedAsync();
            db.Topics.AddRange(new Topic { Name = "Basics" }, new Topic { Name = "Arrays" }, new Topic { Name = "Algorithms" });
            await db.SaveChangesAsync();
            var topic = await db.Topics.Where(t => t.Name == "Basics").Select(t => t.Id).SingleAsync();
            db.Lessons.Add(new Lesson { TopicId = topic, Order = 3, Slug = "python-basics", Title = "Python негіздері",
                Summary = PythonLessonContent.Summary, Content = PythonLessonContent.Content,
                CodeExample = PythonLessonContent.CodeExample, CodeLanguage = "python", IsPublished = true });
            await db.SaveChangesAsync();
            var originalId = await db.Lessons.Select(l => l.Id).SingleAsync();
            await LessonSeeder.SeedAsync(db);
            Assert(await db.Lessons.CountAsync() == 13 && await db.Topics.CountAsync() == 3, "Nine Python lessons; existing topics reused");
            var ordered = await db.Lessons.Where(l => l.CodeLanguage == "python").OrderBy(l => l.Order).ToListAsync();
            Assert(ordered.Select(l => l.Order).SequenceEqual(Enumerable.Range(3,9)) && ordered[0].Id == originalId,
                "Deterministic Python order 3..11; original lesson ID retained");
            ordered[0].Title = "Admin title"; ordered[0].Content = "Admin-edited content"; ordered[0].IsPublished = false;
            ordered[2].Summary = "Admin summary";
            await db.SaveChangesAsync();
            var snapshot = JsonSerializer.Serialize(await db.Lessons.AsNoTracking().OrderBy(l => l.Id).ToListAsync());
            await LessonSeeder.SeedAsync(db);
            var after = JsonSerializer.Serialize(await db.Lessons.AsNoTracking().OrderBy(l => l.Id).ToListAsync());
            Assert(snapshot == after, "Repeated startup preserves all IDs, content, dates, order and admin publication edits");
            await PersistenceAsync(options, avatars);
        }
        finally { await db.Database.EnsureDeletedAsync(); }
    }

    // Бәсекелес өзгеріс пен SQL қатесі кезінде бұрынғы аватардың сақталуын тексереді.
    private static async Task PersistenceAsync(DbContextOptions<ApplicationDbContext> options, AvatarService avatars)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddScoped(_ => new ApplicationDbContext(options));
        services.AddIdentityCore<ApplicationUser>().AddEntityFrameworkStores<ApplicationDbContext>();
        services.AddSingleton(avatars);
        services.AddScoped<ProgressService>();
        services.AddScoped<ExperienceService>();
        services.AddScoped<SubmissionActivityService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<ProfileService>();
        await using var provider = services.BuildServiceProvider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var users = first.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = "fixture@example.test", Email = "fixture@example.test", DisplayName = "Original" };
        Assert((await users.CreateAsync(user)).Succeeded, "Isolated Identity account created");
        var model = new ProfileEditViewModel { DisplayName = "Updated", Avatar = Upload(Image(SKEncodedImageFormat.Png, 100, 100)) };
        var profiles = first.ServiceProvider.GetRequiredService<ProfileService>();
        Assert(await profiles.UpdateAsync(user.Id, model, default) == null, "Initial profile avatar saved");
        await second.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(user.Id);
        model.Avatar = Upload(Image(SKEncodedImageFormat.Png, 120, 120));
        Assert(await profiles.UpdateAsync(user.Id, model, default) == null, "First concurrent writer succeeds");
        model.Avatar = Upload(Image(SKEncodedImageFormat.Png, 140, 140));
        Assert(await second.ServiceProvider.GetRequiredService<ProfileService>().UpdateAsync(user.Id, model, default) != null,
            "Stale Identity concurrency stamp rejects second writer");
        await using var db = new ApplicationDbContext(options);
        var current = (await db.Users.AsNoTracking().SingleAsync()).AvatarPath;
        Assert(Directory.GetFiles(avatars.StorageRoot).Select(Path.GetFileName).SequenceEqual(new[] { Path.GetFileName(current) }),
            "Concurrent failure keeps active avatar and removes unused new file");
        await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER RejectProfileUpdate ON AspNetUsers AFTER UPDATE AS THROW 51000, 'Fixture update failure', 1;");
        model.Avatar = Upload(Image(SKEncodedImageFormat.Png, 160, 160));
        Assert(await profiles.UpdateAsync(user.Id, model, default) != null, "SQL update failure returns a safe profile error");
        Assert(!await profiles.RemoveAvatarAsync(user.Id), "SQL failure cannot report successful avatar removal");
        Assert((await db.Users.AsNoTracking().SingleAsync()).AvatarPath == current
            && Directory.GetFiles(avatars.StorageRoot).Select(Path.GetFileName).SequenceEqual(new[] { Path.GetFileName(current) }),
            "SQL failure preserves previous image and DB reference without orphan replacement");
        await db.Database.ExecuteSqlRawAsync("DROP TRIGGER RejectProfileUpdate;");
        Assert(await profiles.RemoveAvatarAsync(user.Id) && !Directory.EnumerateFiles(avatars.StorageRoot).Any(),
            "Successful removal clears DB then managed file");
    }
}

sealed class TestEnvironment : IWebHostEnvironment
{
    public string WebRootPath { get; set; } = "";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string ApplicationName { get; set; } = "Checks";
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    public string ContentRootPath { get; set; } = "";
    public string EnvironmentName { get; set; } = "Development";
}
