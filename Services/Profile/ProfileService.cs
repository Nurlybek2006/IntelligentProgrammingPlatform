using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.ViewModels.Account;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IntelligentProgrammingPlatform.Services.Profile;

public sealed class ProfileService
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ApplicationDbContext _db;
    private readonly AvatarService _avatars;
    private readonly ProgressService _progress;
    private readonly ILogger<ProfileService> _logger;

    // Профильге Identity, аватар және дайын статистика қызметтерін береді.
    public ProfileService(UserManager<ApplicationUser> users, ApplicationDbContext db, AvatarService avatars,
        ProgressService progress, ILogger<ProfileService> logger)
    {
        _users = users; _db = db; _avatars = avatars; _progress = progress; _logger = logger;
    }

    // Қауіпсіз профиль өрістерін және соңғы бес әрекетті бірге қайтарады.
    public async Task<ProfileViewModel?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var model = await _db.Users.AsNoTracking().Where(user => user.Id == userId).Select(user => new ProfileViewModel
        {
            DisplayName = user.DisplayName, Email = user.Email ?? "", CreatedAt = user.CreatedAt, AvatarPath = user.AvatarPath
        }).SingleOrDefaultAsync(cancellationToken);
        if (model == null) return null;
        model.Roles = await (from membership in _db.UserRoles.AsNoTracking()
            join role in _db.Roles.AsNoTracking() on membership.RoleId equals role.Id
            where membership.UserId == userId orderby role.Name select role.Name ?? "").ToListAsync(cancellationToken);
        model.Progress = await _progress.GetAsync(userId, cancellationToken);
        return model;
    }

    // Алдымен жаңа суретті сақтап, Identity жаңартуы сәтті болса ғана ескісін жояды.
    public async Task<string?> UpdateAsync(string userId, ProfileEditViewModel model, CancellationToken cancellationToken)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return "Profile_SaveFailed";
        var name = model.DisplayName.Trim();
        if (name.Length is < 1 or > 100) return "Profile_InvalidName";
        string? saved = null;
        try
        {
            if (model.Avatar != null) saved = await _avatars.SaveAsync(model.Avatar, cancellationToken);
        }
        catch (AvatarValidationException exception) { return exception.ResourceKey; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(exception, "Avatar storage failed");
            return "Profile_SaveFailed";
        }
        var old = user.AvatarPath;
        user.DisplayName = name;
        if (saved != null) user.AvatarPath = saved;
        if (!await PersistAsync(user, saved)) return "Profile_SaveFailed";
        if (saved != null) _avatars.Delete(old);
        return null;
    }

    // Иесінің немесе әкімші таңдаған аккаунттың сақталған аватарын қауіпсіз алып тастайды.
    public async Task<bool> RemoveAvatarAsync(string userId)
    {
        var user = await _users.FindByIdAsync(userId);
        if (user == null) return false;
        var old = user.AvatarPath;
        if (old == null) return true;
        user.AvatarPath = null;
        if (!await PersistAsync(user, null)) return false;
        _avatars.Delete(old);
        return true;
    }

    // Бәсекелес жаңартуды Identity арқылы тексеріп, базаға жазылмаған жаңа файлды тазалайды.
    private async Task<bool> PersistAsync(ApplicationUser user, string? saved)
    {
        try
        {
            var result = await _users.UpdateAsync(user);
            if (result.Succeeded) return true;
            _avatars.Delete(saved);
        }
        catch (DbUpdateException exception)
        {
            _logger.LogWarning(exception, "Profile update failed");
            // Commit нәтижесі белгісіз болса, әлі сілтенген суретті жоймайды.
            if (saved != null)
            {
                try
                {
                    if (!await _db.Users.AsNoTracking().AnyAsync(item => item.AvatarPath == saved)) _avatars.Delete(saved);
                }
                catch (Exception cleanupException) when (cleanupException is DbUpdateException or System.Data.Common.DbException)
                {
                    _logger.LogWarning(cleanupException, "Avatar reference check failed; retained new file");
                }
            }
        }
        _db.Entry(user).State = EntityState.Detached;
        return false;
    }
}
