using System.Collections.Concurrent;
using System.Data.Common;
using IntelligentProgrammingPlatform.Data;
using IntelligentProgrammingPlatform.Hubs;
using IntelligentProgrammingPlatform.Models;
using IntelligentProgrammingPlatform.Models.Enums;
using IntelligentProgrammingPlatform.Services.Progress;
using IntelligentProgrammingPlatform.Services.Social;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;

await Checks.RunAsync();

static class Checks
{
    // Нақты SQL ережелерін тек UUID атаулы уақытша базада тексеріп, соңында сол базаны жояды.
    public static async Task RunAsync()
    {
        var database = "IntelligentProgrammingPlatform_Enh8_" + Guid.NewGuid().ToString("N");
        var queries = new QueryRecorder();
        var options = new DbContextOptionsBuilder<ApplicationDbContext>().UseSqlServer(
            $"Server=localhost;Database={database};Trusted_Connection=True;TrustServerCertificate=True;").AddInterceptors(queries).Options;
        await using var db = new ApplicationDbContext(options);
        var hub = new RecordingHub();
        try
        {
            await db.Database.EnsureCreatedAsync();
            foreach (var userId in new[] { "a", "b", "c", "d" }) db.Users.Add(new ApplicationUser { Id = userId, UserName = userId,
                NormalizedUserName = userId.ToUpperInvariant(), DisplayName = "Social " + userId, Email = userId + "@example.test", CreatedAt = DateTime.UtcNow });
            await db.SaveChangesAsync();
            Assert(SocialPair.Create("a", "b") == SocialPair.Create("b", "a"), "Pair normalization is symmetric");
            Assert((await Change("a", "a", FriendshipAction.Request)).Outcome == SocialOutcome.NotAllowed
                && (await Change("a", "a", FriendshipAction.Block)).Outcome == SocialOutcome.NotAllowed, "Self requests and blocks rejected");
            var racers = await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Change(i % 2 == 0 ? "a" : "b", i % 2 == 0 ? "b" : "a", FriendshipAction.Request)));
            Assert(racers.All(item => item.Succeeded) && await db.Friendships.CountAsync() == 1, "Eight reverse request races create one pair");
            var pending = await db.Friendships.AsNoTracking().SingleAsync();
            var sender = pending.RequestedByUserId;
            var receiver = sender == "a" ? "b" : "a";
            Assert(pending.Status == FriendshipStatus.Pending && pending.AcceptedAt == null, "Reverse request never auto-accepts");
            Assert((await Change(sender, receiver, FriendshipAction.Accept)).Outcome == SocialOutcome.NotAllowed,
                "Requester cannot accept own outgoing request");
            Assert((await Change("c", sender, FriendshipAction.Accept)).Outcome == SocialOutcome.NotAllowed, "Third party cannot accept another pair");
            Assert((await Change(sender, receiver, FriendshipAction.Decline)).Outcome == SocialOutcome.NotAllowed, "Requester cannot decline incoming-only action");
            Assert((await Open("a", "b")).Outcome == SocialOutcome.Unavailable, "Pending friends cannot open new conversation");
            Assert((await Change(receiver, sender, FriendshipAction.Accept)).Succeeded, "Recipient explicitly accepts");
            var opens = await Task.WhenAll(Enumerable.Range(0, 6).Select(i => Open(i % 2 == 0 ? "a" : "b", i % 2 == 0 ? "b" : "a")));
            Assert(opens.All(item => item.Succeeded) && opens.Select(item => item.Id).Distinct().Count() == 1, "Concurrent lazy open reuses one conversation");
            var id = opens[0].Id!.Value;
            const string hostile = "<script>alert(1)</script>\n<img src=x onerror=attack()>";
            var clientId = Guid.NewGuid();
            var sends = await Task.WhenAll(Enumerable.Range(0, 8).Select(_ => Send("a", id, hostile, clientId)));
            Assert(sends.All(item => item.Succeeded) && await db.DirectMessages.CountAsync() == 1, "Eight retries persist one message");
            Assert(hub.Events.Count(item => item.Event == "NewMessage") == 1, "Only the committed new message generates an event");
            Assert(hub.Events.Where(item => item.Event == "NewMessage").All(item => item.Groups.Order().SequenceEqual(new[] { "social-user:a", "social-user:b" })),
                "Dispatch targets only both authenticated user groups");
            Assert((await Send("a", id, "changed", clientId)).Outcome == SocialOutcome.InvalidMessage, "Idempotency key cannot overwrite earlier body");
            Assert((await Send("c", id, "intrusion")).Outcome == SocialOutcome.NotFound, "Outsider cannot send");
            Assert((await Send("a", id, " \n\t")).Outcome == SocialOutcome.InvalidMessage && (await Send("a", id, new string('x', 2001))).Outcome == SocialOutcome.InvalidMessage,
                "Whitespace and oversized messages rejected");
            Assert((await Send("a", id, new string('x', 2000))).Succeeded, "Exactly 2000 UTF-16 code units accepted");
            await using (var scope = new ApplicationDbContext(options))
            {
                var messages = Messages(scope, hub);
                Assert(await messages.GetThreadAsync("c", id, null, default) == null && (await messages.GetListAsync("c", 1, default)).Rows.Count == 0,
                    "Outsider history/list queries disclose no conversation");
                Assert((await messages.MarkReadAsync("c", id, sends[0].Id!.Value, default)).Outcome == SocialOutcome.NotFound, "Outsider cannot mark read");
                Assert((await messages.GetThreadAsync("b", id, null, default))!.Messages.First().Body == hostile, "Plain text is stored verbatim");
                Assert((await new SocialNotificationService(scope).GetCountsAsync("b", default)).UnreadMessages == 2, "GET does not mark messages read");
                await messages.MarkReadAsync("a", id, sends[0].Id!.Value, default);
                Assert(await scope.DirectMessages.CountAsync(item => item.ReadAt != null) == 0, "Sender cannot mark own outgoing message read");
                await messages.MarkReadAsync("b", id, sends[0].Id!.Value, default);
                Assert((await new SocialNotificationService(scope).GetCountsAsync("b", default)).UnreadMessages == 1, "Read cursor leaves newer unseen messages unread");
            }
            for (var index = 0; index < 53; index++) Assert((await Send("b", id, "History " + index)).Succeeded, "History fixture " + index);
            await using (var scope = new ApplicationDbContext(options))
            {
                var messages = Messages(scope, hub);
                var latest = (await messages.GetThreadAsync("a", id, null, default))!;
                var older = (await messages.GetThreadAsync("a", id, latest.Messages[0].Id, default))!;
                Assert(latest.Messages.Count == 50 && latest.HasOlder && older.Messages.Count == 5 && !older.HasOlder
                    && latest.Messages.Select(item => item.Id).Intersect(older.Messages.Select(item => item.Id)).Count() == 0, "History uses bounded stable cursor pages");
                var profiles = new SocialProfileService(scope, new ExperienceService(scope), Friends(scope, hub));
                queries.Commands.Clear();
                var profile = (await profiles.GetProfileAsync("a", "b", default))!;
                Assert(profile.Profile.DisplayName == "Social a" && queries.Commands.All(sql => !sql.Contains("SourceCode") && !sql.Contains("PasswordHash")
                    && !sql.Contains("TaskFavorites") && !sql.Contains("DirectMessages") && !sql.Contains("SecurityStamp") && !sql.Contains("Email")),
                    "Public profile SQL projects no private fields, favorites or message bodies");
                Assert((await profiles.GetFriendsAsync("c", "search", "SOCIAL", 1, default)).Users.Count == 3, "DisplayName search is case insensitive and excludes self");
            }
            var before = await db.DirectMessages.CountAsync();
            Assert((await Change("a", "b", FriendshipAction.Remove)).Succeeded, "Either friend may remove friendship");
            Assert((await Send("b", id, "disabled")).Outcome == SocialOutcome.Unavailable && await db.DirectMessages.CountAsync() == before,
                "Unfriend preserves history and prevents writes");
            await Change("b", "a", FriendshipAction.Request);
            await Change("a", "b", FriendshipAction.Accept);
            Assert((await Open("a", "b")).Id == id, "Reaccepted friendship reuses old conversation");
            await Task.WhenAll(Change("b", "a", FriendshipAction.Block), Send("a", id, "concurrent-before-block"));
            Assert((await Send("a", id, "after-block")).Outcome == SocialOutcome.Unavailable
                && (await Send("b", id, "after-block")).Outcome == SocialOutcome.Unavailable && await db.Friendships.CountAsync() == 0,
                "Block/send race serializes; no new messages after committed block");
            Assert((await Change("a", "b", FriendshipAction.Request)).Outcome == SocialOutcome.Unavailable
                && (await Change("b", "a", FriendshipAction.Request)).Outcome == SocialOutcome.Unavailable, "Either block direction denies requests");
            await using (var scope = new ApplicationDbContext(options))
            {
                Assert((await Friends(scope, hub).GetStatesAsync("a", ["b"], default))["b"] == SocialRelationship.Unavailable
                    && (await Friends(scope, hub).GetStatesAsync("b", ["a"], default))["a"] == SocialRelationship.Blocked, "Only blocker sees explicit block state");
                Assert((await Messages(scope, hub).GetThreadAsync("a", id, null, default)) is { CanSend: false }, "Blocked participant can still read old history");
                var profiles = new SocialProfileService(scope, new ExperienceService(scope), Friends(scope, hub));
                Assert((await profiles.GetFriendsAsync("a", "search", "Social b", 1, default)).Users.Count == 0, "Search omits blocked relationships");
            }
            await Change("a", "b", FriendshipAction.Unblock);
            Assert(await db.UserBlocks.CountAsync() == 1, "Unblock cannot remove other user's block");
            await Change("b", "a", FriendshipAction.Unblock);
            Assert(await db.UserBlocks.CountAsync() == 0 && await db.Friendships.CountAsync() == 0 && (await Send("a", id, "still-disabled")).Outcome == SocialOutcome.Unavailable,
                "Unblock never restores friendship");
            await Change("a", "b", FriendshipAction.Request);
            await Change("b", "a", FriendshipAction.Decline);
            Assert(await db.Friendships.CountAsync() == 0, "Recipient decline removes pending pair");
            await Change("a", "b", FriendshipAction.Request);
            await Change("a", "b", FriendshipAction.Cancel);
            Assert(await db.Friendships.CountAsync() == 0, "Requester cancellation removes pending pair");
            await ConstraintChecksAsync(options);
            await PaginationChecksAsync(options, hub);
            Console.WriteLine("ENHANCEMENT 8 SERVICE/SQL/CONCURRENCY CHECKS PASSED. Paid AI calls: 0.");
        }
        finally { await db.Database.EnsureDeletedAsync(); Console.WriteLine("Removed only isolated Enhancement 8 database."); }

        // Әр әрекетке бөлек request тәрізді контекст ашып, параллель SQL құлпын тексереді.
        async Task<SocialResult> Change(string actor, string target, FriendshipAction action)
        { await using var scope = new ApplicationDbContext(options); return await Friends(scope, hub).ChangeAsync(actor, target, action, default); }
        // Диалог ашуды бөлек DbContext-пен шақырады.
        async Task<SocialResult> Open(string actor, string target)
        { await using var scope = new ApplicationDbContext(options); return await Messages(scope, hub).OpenAsync(actor, target, default); }
        // Хат жіберу мен қайталанған сұрауды бөлек контексте орындайды.
        async Task<SocialResult> Send(string actor, long conversation, string text, Guid? key = null)
        { await using var scope = new ApplicationDbContext(options); return await Messages(scope, hub).SendAsync(actor, conversation, text, key ?? Guid.NewGuid(), default); }
    }

    // SQL бірегейлігін, өзімен жұп құруды және Identity FK жою қорғанысын тікелей тексереді.
    private static async Task ConstraintChecksAsync(DbContextOptions<ApplicationDbContext> options)
    {
        await using var db = new ApplicationDbContext(options);
        db.Friendships.Add(new Friendship { UserAId = "c", UserBId = "d", RequestedByUserId = "c", CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        await RejectAsync("INSERT Friendships(UserAId,UserBId,RequestedByUserId,Status,CreatedAt) VALUES(N'c',N'd',N'd',0,SYSUTCDATETIME())", "SQL rejects duplicate friendship");
        await RejectAsync("INSERT DirectConversations(UserAId,UserBId,CreatedAt) VALUES(N'a',N'a',SYSUTCDATETIME())", "SQL rejects self conversation");
        await RejectAsync("INSERT DirectConversations(UserAId,UserBId,CreatedAt) VALUES(N'a',N'b',SYSUTCDATETIME())", "SQL rejects duplicate conversation");
        await RejectAsync("DELETE AspNetUsers WHERE Id=N'a'", "Identity deletion cannot cascade private history");
        // Тек бекітілген тест SQL-інің constraint қатесімен аяқталғанын растайды.
        async Task RejectAsync(string sql, string label)
        {
            var rejected = false;
            try { await db.Database.ExecuteSqlRawAsync(sql); } catch (DbException) { rejected = true; }
            Assert(rejected, label);
        }
    }

    // Жиырмадан көп нақты жолмен тізім, іздеу және достар беттерінің шектелуін тексереді.
    private static async Task PaginationChecksAsync(DbContextOptions<ApplicationDbContext> options, RecordingHub hub)
    {
        await using var db = new ApplicationDbContext(options);
        var now = DateTime.UtcNow;
        for (var index = 0; index < 24; index++)
        {
            var userId = "page-" + index.ToString("D2");
            db.Users.Add(new ApplicationUser { Id = userId, UserName = userId, DisplayName = "Batch Learner " + index, CreatedAt = now });
            db.Friendships.Add(new Friendship { UserAId = "a", UserBId = userId, RequestedByUserId = "a",
                Status = FriendshipStatus.Accepted, CreatedAt = now, AcceptedAt = now });
            db.DirectConversations.Add(new DirectConversation { UserAId = "a", UserBId = userId, CreatedAt = now });
        }
        await db.SaveChangesAsync();
        var messaging = Messages(db, hub);
        var first = await messaging.GetListAsync("a", 1, default);
        var second = await messaging.GetListAsync("a", 2, default);
        Assert(first.Rows.Count == 20 && first.HasNextPage && second.Rows.Count == 5 && !second.HasNextPage
            && !first.Rows.Select(item => item.Id).Intersect(second.Rows.Select(item => item.Id)).Any(), "Conversation list is bounded to 20 and pages without overlap");
        var profiles = new SocialProfileService(db, new ExperienceService(db), Friends(db, hub));
        foreach (var section in new[] { "friends", "search" })
        {
            var page = await profiles.GetFriendsAsync("a", section, "BATCH", 1, default);
            var next = await profiles.GetFriendsAsync("a", section, "BATCH", 2, default);
            Assert(page.Users.Count == 20 && page.HasNextPage && next.Users.Count == 4 && !next.HasNextPage,
                "Bounded 20-user pagination: " + section);
        }
    }

    // Нақты қызметті жалған тасымалдаушыға қосып, сақталу тәртібін тексереді.
    private static FriendshipService Friends(ApplicationDbContext db, RecordingHub hub) => new(db, new SocialPairLock(db),
        new SocialNotifier(hub, NullLogger<SocialNotifier>.Instance), TimeProvider.System, NullLogger<FriendshipService>.Instance);
    // Нақты хабарлама қызметін тест контекстімен құрады.
    private static MessagingService Messages(ApplicationDbContext db, RecordingHub hub) => new(db, new SocialPairLock(db),
        new SocialNotifier(hub, NullLogger<SocialNotifier>.Instance), TimeProvider.System, NullLogger<MessagingService>.Instance);
    // Тексеру сәтсіз болса, толық жинақты тоқтатады.
    private static void Assert(bool condition, string label)
    { if (!condition) throw new InvalidOperationException(label); Console.WriteLine("PASS: " + label); }
}

sealed class QueryRecorder : DbCommandInterceptor
{
    public ConcurrentQueue<string> Commands { get; } = new();
    // Ашық профиль SQL проекцияларында жеке өрістердің жоқтығын жазып алады.
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Commands.Enqueue(command.CommandText); return new(result); }
}

sealed class RecordingHub : IHubContext<ChatHub>
{
    public ConcurrentQueue<(IReadOnlyList<string> Groups, string Event)> Events { get; } = new();
    public IHubClients Clients { get; }
    public IGroupManager Groups => throw new NotSupportedException();
    // Хабар жіберу мақсаттарын ағынға қауіпсіз кезекке жазады.
    public RecordingHub() => Clients = new RecordingClients(Events);
}

// Тек қызмет таңдаған нақты user топтарына жіберуді қабылдайды.
sealed class RecordingClients(ConcurrentQueue<(IReadOnlyList<string> Groups, string Event)> events) : IHubClients
{
    public IClientProxy All => throw new InvalidOperationException("Broadcast forbidden");
    // Барлық клиентке broadcast жасау тестте бірден қате береді.
    public IClientProxy AllExcept(IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
    // Клиент таңдаған connection арнасын қолдануды қабылдамайды.
    public IClientProxy Client(string connectionId) => throw new NotSupportedException();
    // Connection тізіміне жіберуді қабылдамайды.
    public IClientProxy Clients(IReadOnlyList<string> connectionIds) => throw new NotSupportedException();
    // Тек бірнеше trusted user тобы қолданылуын талап етеді.
    public IClientProxy Group(string groupName) => throw new NotSupportedException();
    // Топтан шығару арқылы хабарлауды қабылдамайды.
    public IClientProxy GroupExcept(string groupName, IReadOnlyList<string> excludedConnectionIds) => throw new NotSupportedException();
    // Қызмет белгілеген топтар үшін оқиға жазушыны береді.
    public IClientProxy Groups(IReadOnlyList<string> groupNames) => new RecordingProxy(groupNames, events);
    // Пайдаланушы арнасын айналып өтуді қабылдамайды.
    public IClientProxy User(string userId) => throw new NotSupportedException();
    // Тікелей users тізімін қолдануды қабылдамайды.
    public IClientProxy Users(IReadOnlyList<string> userIds) => throw new NotSupportedException();
}

// Оқиға мәтінін сақтамай, тек атауы мен жеткізу тобын тіркейді.
sealed class RecordingProxy(IReadOnlyList<string> groups, ConcurrentQueue<(IReadOnlyList<string> Groups, string Event)> events) : IClientProxy
{
    // Жіберілген оқиғаны бір рет тіркейді.
    public Task SendCoreAsync(string method, object?[] args, CancellationToken cancellationToken = default)
    { events.Enqueue((groups, method)); return Task.CompletedTask; }
}
