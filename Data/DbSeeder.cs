using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.CodeExecution;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Data;

public static class DbSeeder
{
    // Рөлдерді және даму ортасының бастапқы деректерін қайталамай дайындайды.
    public static async Task SeedAsync(IServiceProvider services, IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        await using var scope = services.CreateAsyncScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbSeeder");

        foreach (var role in new[] { RoleNames.Student, RoleNames.Admin })
        {
            if (!await roles.RoleExistsAsync(role))
            {
                var result = await roles.CreateAsync(new IdentityRole(role));
                if (!result.Succeeded && !await roles.RoleExistsAsync(role))
                    throw new InvalidOperationException($"Could not create the {role} role.");
            }
        }

        if (!environment.IsDevelopment())
            return;

        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await SeedAdminAsync(users, configuration, logger);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await SeedRuntimeAsync(db);
        await SeedTasksAsync(db);
    }

    // C++ тілінің бір ғана даму runtime жазбасын қайталамай қосады.
    private static async Task SeedRuntimeAsync(ApplicationDbContext db)
    {
        if (await db.Runtimes.AnyAsync(runtime => runtime.LanguageKey == CodeRunnerOptions.LanguageKey))
            return;
        db.Runtimes.Add(new Runtime
        {
            Name = "C++ 20", LanguageKey = CodeRunnerOptions.LanguageKey, Version = "GCC 14.3.0",
            FileExtension = ".cpp", DockerImage = CodeRunnerOptions.Image, IsEnabled = true,
            CompileCommand = "Managed by the trusted C++ runner", RunCommand = "Managed by the trusted C++ runner"
        });
        await db.SaveChangesAsync();
    }

    // User Secrets баптауынан жергілікті әкімші аккаунтын қауіпсіз жасайды.
    private static async Task SeedAdminAsync(UserManager<ApplicationUser> users,
        IConfiguration configuration, ILogger logger)
    {
        var email = configuration["SeedAdmin:Email"]?.Trim();
        var password = configuration["SeedAdmin:Password"];
        var displayName = configuration["SeedAdmin:DisplayName"]?.Trim();

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            logger.LogInformation("Development admin seed credentials are not configured. Roles were created; admin creation was skipped.");
            return;
        }

        var existing = await users.FindByEmailAsync(email);
        if (existing != null)
        {
            // Never promote an independently registered account merely by matching its email.
            if (!await users.IsInRoleAsync(existing, RoleNames.Admin))
                logger.LogWarning("Admin seeding skipped: the configured email belongs to a non-admin account.");
            return;
        }

        var admin = new ApplicationUser
        {
            UserName = email,
            Email = email,
            DisplayName = string.IsNullOrWhiteSpace(displayName) ? "Development Admin" : displayName,
            CreatedAt = DateTime.UtcNow
        };
        if (admin.DisplayName.Length > 100)
            throw new InvalidOperationException("SeedAdmin:DisplayName must not exceed 100 characters.");

        var created = await users.CreateAsync(admin, password);
        if (!created.Succeeded)
        {
            // Error codes contain no credential values.
            logger.LogWarning("Development admin could not be created. Identity error codes: {Codes}",
                string.Join(", ", created.Errors.Select(error => error.Code)));
            return;
        }

        var assigned = await users.AddToRoleAsync(admin, RoleNames.Admin);
        if (!assigned.Succeeded)
        {
            await users.DeleteAsync(admin);
            throw new InvalidOperationException("Could not assign the development admin role.");
        }
        logger.LogInformation("Development admin account created from configured User Secrets.");
    }

    // Төрт оқу есебін және тесттерін тек жоқ болғанда қосады.
    private static async Task SeedTasksAsync(ApplicationDbContext db)
    {
        await using var transaction = await db.Database.BeginTransactionAsync();
        foreach (var name in new[] { "Basics", "Arrays", "Algorithms" })
        {
            if (!await db.Topics.AnyAsync(topic => topic.Name == name))
                db.Topics.Add(new Topic { Name = name, Description = $"Practice tasks about {name.ToLowerInvariant()}." });
        }
        await db.SaveChangesAsync();

        var topics = await db.Topics.ToDictionaryAsync(topic => topic.Name, topic => topic.Id,
            StringComparer.OrdinalIgnoreCase);
        var tasks = new[]
        {
            new ProgrammingTask
            {
                Title = "Longest Increasing Subsequence", Slug = "longest-increasing-subsequence",
                TopicId = topics["Algorithms"], Difficulty = Difficulty.Hard, IsPublished = true,
                Description = "Read n (1 <= n <= 100000), then n integers between -1000000000 and 1000000000. Print the length of the longest strictly increasing subsequence. A subsequence keeps the original order but may skip elements. Equal values do not form an increase.",
                TestCases = new List<TestCase>
                {
                    new() { Input = "6\n3 1 4 2 5 6", ExpectedOutput = "4", Order = 1 },
                    new() { Input = "4\n7 7 7 7", ExpectedOutput = "1", Order = 2 },
                    new() { Input = "8\n10 9 2 5 3 7 101 18", ExpectedOutput = "4", Order = 3, IsHidden = true },
                    new() { Input = "5\n-5 -4 -3 -2 -1", ExpectedOutput = "5", Order = 4, IsHidden = true }
                }
            },
            new ProgrammingTask
            {
                Title = "Sum of Two Numbers", Slug = "sum-of-two-numbers",
                TopicId = topics["Basics"], Difficulty = Difficulty.Easy, IsPublished = true,
                Description = "Read two integers from one line, separated by a space. Print their sum.",
                TestCases = new List<TestCase>
                {
                    new() { Input = "2 3", ExpectedOutput = "5", Order = 1 },
                    new() { Input = "-4 4", ExpectedOutput = "0", Order = 2 },
                    new() { Input = "123456 654321", ExpectedOutput = "777777", Order = 3, IsHidden = true }
                }
            },
            new ProgrammingTask
            {
                Title = "Maximum in Array", Slug = "maximum-in-array",
                TopicId = topics["Arrays"], Difficulty = Difficulty.Easy, IsPublished = true,
                Description = "Read a positive integer n on the first line and n integers on the second line. Print the largest integer.",
                TestCases = new List<TestCase>
                {
                    new() { Input = "5\n1 9 3 2 4", ExpectedOutput = "9", Order = 1 },
                    new() { Input = "3\n-17 -29 -41", ExpectedOutput = "-17", Order = 2, IsHidden = true }
                }
            },
            new ProgrammingTask
            {
                Title = "Fibonacci Number", Slug = "fibonacci-number",
                TopicId = topics["Algorithms"], Difficulty = Difficulty.Medium, IsPublished = true,
                Description = "Read an integer n (0 <= n <= 40). Print F(n), where F(0) = 0, F(1) = 1, and F(n) = F(n-1) + F(n-2).",
                TestCases = new List<TestCase>
                {
                    new() { Input = "7", ExpectedOutput = "13", Order = 1 },
                    new() { Input = "0", ExpectedOutput = "0", Order = 2 },
                    new() { Input = "37", ExpectedOutput = "24157817", Order = 3, IsHidden = true }
                }
            }
        };

        foreach (var task in tasks)
        {
            if (!await db.ProgrammingTasks.AnyAsync(existing => existing.Slug == task.Slug))
                db.ProgrammingTasks.Add(task);
        }
        await db.SaveChangesAsync();
        await transaction.CommitAsync();
    }
}
