using System.Security.Cryptography;
using System.Text;
using IntelligentProgrammingPlatform.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IntelligentProgrammingPlatform.Services.Social;

// Екі аккаунттың реттелген жұбын клиент таңдаған топ атауынсыз сақтайды.
public sealed record SocialPair(string UserAId, string UserBId)
{
    // Бір жұптың екі бағытын бірдей ordinal ретке келтіреді.
    public static SocialPair Create(string first, string second)
    {
        if (string.IsNullOrWhiteSpace(first) || string.IsNullOrWhiteSpace(second) || first == second)
            throw new ArgumentException("A social pair needs two different users.");
        return string.CompareOrdinal(first, second) < 0 ? new(first, second) : new(second, first);
    }
}

public sealed class SocialPairLock
{
    private readonly ApplicationDbContext _db;

    // Бір жұптағы достық, блок және хат әрекеттеріне ортақ SQL контекстін алады.
    public SocialPairLock(ApplicationDbContext db) => _db = db;

    // Нақты сақталған аккаунт идентификаторларын тауып, өзімен әрекет жасауды қабылдамайды.
    public async Task<SocialPair?> ResolveAsync(string actorId, string? targetId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetId) || targetId.Length > 450 || targetId == actorId) return null;
        var ids = await _db.Users.AsNoTracking().Where(user => user.Id == actorId || user.Id == targetId)
            .Select(user => user.Id).ToListAsync(cancellationToken);
        return ids.Count == 2 && ids.Contains(actorId, StringComparer.Ordinal) ? SocialPair.Create(ids[0], ids[1]) : null;
    }

    // SQL транзакциясына тиесілі құлыппен жұптың барлық жазу әрекетін реттейді.
    public async Task<IDbContextTransaction> BeginAsync(SocialPair pair, CancellationToken cancellationToken)
    {
        var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await using var command = _db.Database.GetDbConnection().CreateCommand();
            command.Transaction = transaction.GetDbTransaction();
            command.CommandText = "DECLARE @result int; EXEC @result=sys.sp_getapplock @Resource=@resource, @LockMode='Exclusive', @LockOwner='Transaction', @LockTimeout=5000; SELECT @result;";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@resource";
            parameter.Value = "ipp-social-" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(
                pair.UserAId.Length + ":" + pair.UserAId + pair.UserBId.Length + ":" + pair.UserBId)));
            command.Parameters.Add(parameter);
            if (Convert.ToInt32(await command.ExecuteScalarAsync(cancellationToken)) < 0) throw new TimeoutException("Social pair busy.");
            return transaction;
        }
        catch { await transaction.DisposeAsync(); throw; }
    }

    // Жіберу алдында екі бағыттағы блокты және Accepted достықты дерекқордан қайта тексереді.
    public async Task<bool> CanMessageAsync(SocialPair pair, CancellationToken cancellationToken) =>
        await _db.Friendships.AsNoTracking().AnyAsync(item => item.UserAId == pair.UserAId && item.UserBId == pair.UserBId
            && item.Status == Models.Enums.FriendshipStatus.Accepted, cancellationToken)
        && !await _db.UserBlocks.AsNoTracking().AnyAsync(item =>
            (item.BlockerUserId == pair.UserAId && item.BlockedUserId == pair.UserBId)
            || (item.BlockerUserId == pair.UserBId && item.BlockedUserId == pair.UserAId), cancellationToken);
}
