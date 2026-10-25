using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Domain.Results;
using ECMA.Infrastructure;
using ECMA.Infrastructure.Data;
using ECMA.Infrastructure.Services;
using EMCA.Application.DTOs;
using EMCA.Application.Services;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace ECMA.Domain.Tests;

// Runs against a real PostgreSQL server (NFR-04 races cannot be proven with mocks).
// Set ECMA_TEST_CONNECTION_STRING to a server the tests may use; each test class run creates and drops
// its own database whose name contains "test". Without the variable the tests are skipped.
public sealed class PostgresIntegrationTests : IAsyncLifetime
{
    private static readonly string? BaseConnection = Environment.GetEnvironmentVariable("ECMA_TEST_CONNECTION_STRING");
    private readonly string _database = "ecma_test_" + Guid.NewGuid().ToString("N")[..12];
    private readonly TestClock _clock = new() { UtcNow = DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(1), DateTimeKind.Utc) };
    private string _connection = "";

    public async ValueTask InitializeAsync()
    {
        if (BaseConnection is null) return;
        _connection = new NpgsqlConnectionStringBuilder(BaseConnection) { Database = _database }.ConnectionString;
        await using var context = Context();
        await context.Database.MigrateAsync(TestContext.Current.CancellationToken);
    }

    public async ValueTask DisposeAsync()
    {
        if (BaseConnection is null) return;
        NpgsqlConnection.ClearAllPools();
        await using var connection = new NpgsqlConnection(new NpgsqlConnectionStringBuilder(BaseConnection) { Database = "postgres" }.ConnectionString);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{_database}\" WITH (FORCE)", connection);
        await command.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task TwoUsersRacingForLastSeat_OnlyOneSucceeds()
    {
        RequireDatabase();
        for (var round = 0; round < 10; round++)
        {
            var owner = await AddUserAsync(UserRole.Organizer);
            var item = await AddPublishedEventAsync(owner, capacity: 1);
            var first = await AddUserAsync(UserRole.Attendee);
            var second = await AddUserAsync(UserRole.Attendee);

            var results = await Task.WhenAll(RegisterAsync(first, item), RegisterAsync(second, item));

            Assert.Single(results, x => x.IsSuccess);
            Assert.Single(results, x => x.Error?.Message == "Sự kiện đã đủ chỗ");
            Assert.Equal(1, await CountAsync(item, RegistrationStatus.Confirmed));
        }
    }

    [Fact]
    public async Task SameUserDoubleSubmit_CreatesOneRegistration()
    {
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        var item = await AddPublishedEventAsync(owner, capacity: 5);
        var attendee = await AddUserAsync(UserRole.Attendee);

        var results = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ => RegisterAsync(attendee, item)));

        Assert.All(results, x => Assert.True(x.IsSuccess));
        Assert.Single(results, x => !x.Value!.AlreadyRegistered);
        Assert.Single(results.Select(x => x.Value!.Code).Distinct());
        Assert.Equal(1, await CountAsync(item, null));
    }

    [Fact]
    public async Task CancelEventRacingWithRegistration_LeavesNoConfirmedOnCancelledEvent()
    {
        RequireDatabase();
        for (var round = 0; round < 10; round++)
        {
            var owner = await AddUserAsync(UserRole.Organizer);
            var item = await AddPublishedEventAsync(owner, capacity: 10);
            var attendee = await AddUserAsync(UserRole.Attendee);

            await using var scope = Scope();
            var cancel = new EventService(scope.Unit, _clock).CancelAsync(new EventActor(owner, UserRole.Organizer), item, "Thời tiết xấu");
            var register = RegisterAsync(attendee, item);
            await Task.WhenAll(cancel, register);

            Assert.True((await cancel).IsSuccess);
            Assert.Equal(0, await CountAsync(item, RegistrationStatus.Confirmed));
            if ((await register).IsSuccess)
            {
                await using var context = Context();
                var stored = await context.Registrations.SingleAsync(x => x.EventId == item, TestContext.Current.CancellationToken);
                Assert.Equal(CancelSource.Event, stored.CancelSource);
                Assert.Equal("Thời tiết xấu", stored.CancelReason);
            }
            // Cancel won the lock: a Cancelled event is hidden from non-registrants (§4.3 step 2, BR-SRCH-04).
            else Assert.Equal("NotFound", (await register).Error!.Code);
        }
    }

    [Fact]
    public async Task CancelEvent_CancelsConfirmedWithEventSource_AndKeepsEarlierCancellation()
    {
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        var item = await AddPublishedEventAsync(owner, capacity: 10);
        var stays = await AddUserAsync(UserRole.Attendee);
        var left = await AddUserAsync(UserRole.Attendee);
        Assert.True((await RegisterAsync(stays, item)).IsSuccess);
        var leftRegistration = (await RegisterAsync(left, item)).Value!;
        await using (var scope = Scope())
            Assert.True((await new RegistrationService(scope.Unit, _clock, new RegistrationCodeGenerator()).CancelOwnAsync(left, item, leftRegistration.RegistrationId)).IsSuccess);

        await using (var scope = Scope())
            Assert.True((await new EventService(scope.Unit, _clock).CancelAsync(new EventActor(owner, UserRole.Organizer), item, "Lý do hủy")).IsSuccess);

        await using var context = Context();
        var rows = await context.Registrations.Where(x => x.EventId == item).ToListAsync(TestContext.Current.CancellationToken);
        Assert.All(rows, x => Assert.Equal(RegistrationStatus.Cancelled, x.Status));
        Assert.Equal(CancelSource.Event, rows.Single(x => x.UserId == stays).CancelSource);
        var earlier = rows.Single(x => x.UserId == left);
        Assert.Equal(CancelSource.Self, earlier.CancelSource);
        Assert.Equal("Người tham dự tự hủy", earlier.CancelReason);
    }

    [Fact]
    public async Task PublicSearch_IsCaseInsensitive_NfcNfdEquivalent_AndTreatsWildcardsLiterally()
    {
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        var match = await AddPublishedEventAsync(owner, capacity: 3, title: "Hội nghị Công nghệ 100%");
        await AddPublishedEventAsync(owner, capacity: 3, title: "Workshop thiết kế");

        Assert.Equal(new[] { match }, await SearchAsync(new PublicEventSearchRequest(Keyword: "HỘI NGHỊ")));
        Assert.Equal(new[] { match }, await SearchAsync(new PublicEventSearchRequest(Keyword: "hội")));
        Assert.Equal(new[] { match }, await SearchAsync(new PublicEventSearchRequest(Keyword: "100%")));
        Assert.Empty(await SearchAsync(new PublicEventSearchRequest(Keyword: "hoi nghi")));
        Assert.Equal(new[] { match }, await SearchAsync(new PublicEventSearchRequest(Keyword: "%")));
        Assert.Empty(await SearchAsync(new PublicEventSearchRequest(Keyword: "_")));
    }

    [Fact]
    public async Task PublicSearch_DateRangeUsesVietnamDay_BothEndsInclusive()
    {
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        // 16:59 UTC = 23:59 in Vietnam on the same calendar day as the clock date.
        var day = DateOnly.FromDateTime(_clock.UtcNow).AddDays(2);
        var lateEvening = DateTime.SpecifyKind(day.ToDateTime(new TimeOnly(16, 59)), DateTimeKind.Utc);
        var item = await AddPublishedEventAsync(owner, capacity: 3, startAt: lateEvening);
        var text = day.ToString("yyyy-MM-dd");
        var next = day.AddDays(1).ToString("yyyy-MM-dd");

        Assert.Equal(new[] { item }, await SearchAsync(new PublicEventSearchRequest(From: text, To: text)));
        Assert.Empty(await SearchAsync(new PublicEventSearchRequest(From: next)));
        Assert.Equal(new[] { item }, await SearchAsync(new PublicEventSearchRequest(To: text)));
    }

    [Fact]
    public async Task Participants_SearchByCodeUsernameAndFullName_StatsCoverWholeEvent()
    {
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        var item = await AddPublishedEventAsync(owner, capacity: 5);
        var first = await AddUserAsync(UserRole.Attendee, "Nguyễn Văn An");
        var second = await AddUserAsync(UserRole.Attendee, "Trần Thị Bình");
        var code = (await RegisterAsync(first, item)).Value!.Code;
        Assert.True((await RegisterAsync(second, item)).IsSuccess);

        await using var scope = Scope();
        var service = new RegistrationService(scope.Unit, _clock, new RegistrationCodeGenerator());
        var byCode = (await service.ListParticipantsAsync(owner, item, new ParticipantListRequest(Keyword: code.ToLowerInvariant()))).Value!;
        var byName = (await service.ListParticipantsAsync(owner, item, new ParticipantListRequest(Keyword: "BÌNH"))).Value!;

        Assert.Equal("Nguyễn Văn An", Assert.Single(byCode.Items).FullName);
        Assert.Equal("Trần Thị Bình", Assert.Single(byName.Items).FullName);
        Assert.Equal(2, byCode.Stats.Confirmed);
        Assert.Equal(3, byCode.Stats.Remaining);
    }

    [Fact]
    public async Task AddAgenda_ToDraft_IsInsertedAndPersisted()
    {
        // Regression: a new AgendaItem with a domain-assigned ID used to be saved as UPDATE (0 rows) and fail with HTTP 500.
        RequireDatabase();
        var owner = await AddUserAsync(UserRole.Organizer);
        var start = _clock.UtcNow.AddDays(1);
        var draft = Event.Create(owner, EventType.Conference, "Hội nghị nháp", "Mô tả", "Hà Nội", start, start.AddHours(3), 10, _clock.UtcNow).Value!;
        await using (var context = Context())
        {
            context.Events.Add(draft);
            await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        }

        await using (var scope = Scope())
        {
            var result = await new EventService(scope.Unit, _clock).AddAgendaAsync(new EventActor(owner, UserRole.Organizer), draft.Id,
                new AgendaData("Khai mạc", "Diễn giả: A", start, start.AddMinutes(30)));
            Assert.True(result.IsSuccess, result.Error?.Message);
        }

        await using var check = Context();
        Assert.Equal("Khai mạc", (await check.AgendaItems.SingleAsync(x => x.EventId == draft.Id, TestContext.Current.CancellationToken)).Title);
    }

    private static void RequireDatabase() => Assert.SkipWhen(BaseConnection is null, "ECMA_TEST_CONNECTION_STRING chưa được đặt");

    private EcmaDbContext Context() => new(new DbContextOptionsBuilder<EcmaDbContext>().UseNpgsql(_connection).Options);

    private Scoped Scope() => new(Context());

    private async Task<Result<RegisterOutcome>> RegisterAsync(Guid user, Guid item)
    {
        await using var scope = Scope();
        return await new RegistrationService(scope.Unit, _clock, new RegistrationCodeGenerator()).RegisterAsync(user, item);
    }

    private async Task<IReadOnlyList<Guid>> SearchAsync(PublicEventSearchRequest request)
    {
        await using var scope = Scope();
        var result = await new PublicEventService(scope.Unit, _clock).SearchAsync(request);
        Assert.True(result.IsSuccess, result.Error?.Message);
        return result.Value!.Items.Select(x => x.Id).ToList();
    }

    private async Task<int> CountAsync(Guid item, RegistrationStatus? status)
    {
        await using var context = Context();
        return await context.Registrations.CountAsync(x => x.EventId == item && (status == null || x.Status == status), TestContext.Current.CancellationToken);
    }

    private async Task<Guid> AddUserAsync(UserRole role, string fullName = "Người dùng kiểm thử")
    {
        await using var context = Context();
        var user = new User("u" + Guid.NewGuid().ToString("N")[..12], "hash", "stamp", fullName, role, UserStatus.Active);
        context.Users.Add(user);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }

    private async Task<Guid> AddPublishedEventAsync(Guid owner, int capacity, string title = "Sự kiện kiểm thử", DateTime? startAt = null)
    {
        var start = startAt ?? _clock.UtcNow.AddDays(1);
        var item = Event.Create(owner, EventType.Event, title, "Mô tả", "Hà Nội", start, start.AddHours(2), capacity, _clock.UtcNow).Value!;
        Assert.True(item.Publish(_clock.UtcNow).IsSuccess);
        await using var context = Context();
        context.Events.Add(item);
        await context.SaveChangesAsync(TestContext.Current.CancellationToken);
        return item.Id;
    }

    private sealed class Scoped(EcmaDbContext context) : IAsyncDisposable
    {
        public IUnitOfWork Unit { get; } = new UnitOfWork(context);
        public ValueTask DisposeAsync() => context.DisposeAsync();
    }

    private sealed class TestClock : IClock
    {
        public DateTime UtcNow { get; set; }
    }
}
