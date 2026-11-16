using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using EMCA.Application.DTOs;
using EMCA.Application.Services;
using Moq;
using Xunit;

namespace ECMA.Domain.Tests;

public sealed class EventServiceTests
{
    [Fact]
    public async Task Organizer_CannotEditAnotherOwnersEvent()
    {
        var now = new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
        var item = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội", now.AddHours(1), now.AddHours(2), 10, now).Value!;
        var unit = new Mock<IUnitOfWork>();
        var transaction = new Mock<IUnitOfWorkTransaction>();
        var actor = new User("actor", "hash", "stamp", "Actor", UserRole.Organizer, UserStatus.Active);
        unit.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var result = await new EventService(unit.Object, clock.Object).EditDraftAsync(
            new EventActor(actor.Id, UserRole.Admin), item.Id,
            new EventData(EventType.Event, "Sự kiện khác", "Mô tả", "Đà Nẵng", now.AddHours(1), now.AddHours(2), 10));

        Assert.False(result.IsSuccess);
        Assert.Equal("NotFound", result.Error!.Code);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishedOtherOwnersEvent_UsesAuthoritativeRoleForManagementCapability()
    {
        var now = new DateTime(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
        var actor = new User("actor", "hash", "stamp", "Actor", UserRole.Organizer, UserStatus.Active);
        var item = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội", now.AddHours(1), now.AddHours(2), 10, now).Value!;
        Assert.True(item.Publish(now).IsSuccess);
        var unit = new Mock<IUnitOfWork>();
        unit.Setup(x => x.Users.FindByIdAsync(actor.Id, It.IsAny<CancellationToken>())).ReturnsAsync(actor);
        unit.Setup(x => x.Events.FindAsync(item.Id, false, It.IsAny<CancellationToken>())).ReturnsAsync(item);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(now);

        var result = await new EventService(unit.Object, clock.Object).GetAsync(new EventActor(actor.Id, UserRole.Admin), item.Id);

        Assert.True(result.IsSuccess);
        Assert.False(result.Value!.CanManage);
    }

    [Fact]
    public async Task InvalidFilter_IsRejectedBeforeRepositoryQuery()
    {
        var unit = new Mock<IUnitOfWork>();
        var clock = new Mock<IClock>();
        var result = await new EventService(unit.Object, clock.Object).ListAsync(
            new EventActor(Guid.NewGuid(), UserRole.Admin), new EventListRequest(Status: "1"));

        Assert.False(result.IsSuccess);
        Assert.Equal("BadRequest", result.Error!.Code);
        unit.Verify(x => x.Events.ListAsync(It.IsAny<EventQuery>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task InvalidPage_HasDistinctMessage()
    {
        var result = await new EventService(new Mock<IUnitOfWork>().Object, new Mock<IClock>().Object).ListAsync(
            new EventActor(Guid.NewGuid(), UserRole.Admin), new EventListRequest(Page: 0));

        Assert.Equal("Tham số trang không hợp lệ", result.Error!.Message);
    }

    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

    private static EventService Service(out Mock<IUnitOfWork> unit, out Mock<IUnitOfWorkTransaction> transaction)
    {
        unit = new Mock<IUnitOfWork>();
        transaction = new Mock<IUnitOfWorkTransaction>();
        var events = new Mock<IEventRepository>();
        var registrations = new Mock<IRegistrationRepository>();
        events.Setup(x => x.FindAsync(It.IsAny<Guid>(), It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync((Event?)null);
        registrations.Setup(x => x.CancelConfirmedForEventAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        unit.SetupGet(x => x.Events).Returns(events.Object);
        unit.SetupGet(x => x.Registrations).Returns(registrations.Object);
        unit.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(transaction.Object);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(Now);
        return new EventService(unit.Object, clock.Object);
    }

    private static User Actor(string name, UserRole role) =>
        new(name, "hash", "stamp", name, role, UserStatus.Active);

    private static Event Published(Guid ownerId)
    {
        var item = Event.Create(ownerId, EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội",
            Now.AddHours(1), Now.AddHours(2), 100, Now).Value!;
        Assert.True(item.Publish(Now).IsSuccess);
        return item;
    }

    [Fact]
    public async Task CreateAsync_RejectsAttendeeBeforePersisting()
    {
        var attendee = Actor("att01", UserRole.Attendee);
        var service = Service(out var unit, out _);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(attendee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(attendee);

        var result = await service.CreateAsync(new EventActor(attendee.Id, attendee.Role),
            new EventData(EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội", Now.AddHours(1), Now.AddHours(2), 100));

        Assert.Equal("Forbidden", result.Error!.Code);
        unit.Verify(x => x.Events.Add(It.IsAny<Event>()), Times.Never);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task CancelAsync_CascadesConfirmedRegistrationsWithEventReason()
    {
        var organizer = Actor("org01", UserRole.Organizer);
        var item = Published(organizer.Id);
        var service = Service(out var unit, out var transaction);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(organizer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(organizer);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await service.CancelAsync(new EventActor(organizer.Id, organizer.Role), item.Id, "Diễn giả bận");

        Assert.True(result.IsSuccess);
        Assert.Equal(EventStatus.Cancelled, item.Status);
        Assert.Equal("Diễn giả bận", item.CancelReason);
        unit.Verify(x => x.Registrations.CancelConfirmedForEventAsync(
            item.Id, organizer.Id, Now, "Diễn giả bận", It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task CancelAsync_InvalidReason_DoesNotTouchRegistrations()
    {
        var organizer = Actor("org01", UserRole.Organizer);
        var item = Published(organizer.Id);
        var service = Service(out var unit, out var transaction);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(organizer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(organizer);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await service.CancelAsync(new EventActor(organizer.Id, organizer.Role), item.Id, "  ");

        Assert.Equal("CancelReason", result.Error!.Code);
        unit.Verify(x => x.Registrations.CancelConfirmedForEventAsync(
            It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PublishAsync_ByOtherOrganizer_IsHiddenAndNotCommitted()
    {
        var intruder = Actor("org02", UserRole.Organizer);
        var item = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội",
            Now.AddHours(1), Now.AddHours(2), 100, Now).Value!;
        var service = Service(out var unit, out var transaction);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(intruder.Id, It.IsAny<CancellationToken>())).ReturnsAsync(intruder);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await service.PublishAsync(new EventActor(intruder.Id, intruder.Role), item.Id);

        Assert.Equal("NotFound", result.Error!.Code);
        Assert.Equal(EventStatus.Draft, item.Status);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OfPublishedEvent_IsConflictAndNotCommitted()
    {
        var organizer = Actor("org01", UserRole.Organizer);
        var item = Published(organizer.Id);
        var service = Service(out var unit, out var transaction);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(organizer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(organizer);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await service.DeleteAsync(new EventActor(organizer.Id, organizer.Role), item.Id);

        Assert.Equal("Conflict", result.Error!.Code);
        Assert.False(item.IsDeleted);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_OfDraft_CommitsSoftDelete()
    {
        var organizer = Actor("org01", UserRole.Organizer);
        var item = Event.Create(organizer.Id, EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội",
            Now.AddHours(1), Now.AddHours(2), 100, Now).Value!;
        var service = Service(out var unit, out var transaction);
        unit.Setup(x => x.Users.FindByIdForUpdateAsync(organizer.Id, It.IsAny<CancellationToken>())).ReturnsAsync(organizer);
        unit.Setup(x => x.Events.FindAsync(item.Id, true, It.IsAny<CancellationToken>())).ReturnsAsync(item);

        var result = await service.DeleteAsync(new EventActor(organizer.Id, organizer.Role), item.Id);

        Assert.True(result.IsSuccess);
        Assert.True(item.IsDeleted);
        unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        transaction.Verify(x => x.CommitAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task ListAsync_KeywordOver100Scalars_IsRejectedBeforeQuery()
    {
        var result = await new EventService(new Mock<IUnitOfWork>().Object, new Mock<IClock>().Object).ListAsync(
            new EventActor(Guid.NewGuid(), UserRole.Admin), new EventListRequest(Keyword: new string('a', 101)));

        Assert.Equal("Bộ lọc không hợp lệ", result.Error!.Message);
    }

    [Fact]
    public async Task ListAsync_TrimsKeywordAndMapsFilterValuesToQuery()
    {
        var admin = Actor("admin", UserRole.Admin);
        var service = Service(out var unit, out _);
        unit.Setup(x => x.Users.FindByIdAsync(admin.Id, It.IsAny<CancellationToken>())).ReturnsAsync(admin);
        EventQuery? captured = null;
        unit.Setup(x => x.Events.ListAsync(It.IsAny<EventQuery>(), It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .Callback<EventQuery, DateTime, CancellationToken>((q, _, _) => captured = q)
            .ReturnsAsync([]);

        var result = await service.ListAsync(new EventActor(admin.Id, admin.Role),
            new EventListRequest("  Hội nghị  ", "Conference", "Draft", 2));

        Assert.True(result.IsSuccess);
        Assert.NotNull(captured);
        Assert.Equal("Hội nghị", captured!.Keyword);
        Assert.Equal(EventType.Conference, captured.Type);
        Assert.Equal(EffectiveEventStatus.Draft, captured.Status);
        Assert.Equal(2, captured.Page);
    }
}
