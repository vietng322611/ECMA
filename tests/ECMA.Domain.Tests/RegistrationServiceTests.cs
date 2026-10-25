using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using EMCA.Application.DTOs;
using EMCA.Application.Services;
using Moq;
using Xunit;

namespace ECMA.Domain.Tests;

public sealed class RegistrationServiceTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
    private readonly Mock<IUnitOfWork> _unit = new();
    private readonly Mock<IClock> _clock = new();
    private readonly Mock<IRegistrationCodeGenerator> _codes = new();
    private readonly User _owner = NewUser("owner", UserRole.Organizer);
    private readonly User _attendee = NewUser("attendee", UserRole.Attendee);
    private readonly Event _event;

    public RegistrationServiceTests()
    {
        _event = Event.Create(_owner.Id, EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội", Now.AddHours(1), Now.AddHours(3), 2, Now).Value!;
        Assert.True(_event.Publish(Now).IsSuccess);
        _clock.SetupGet(x => x.UtcNow).Returns(Now);
        _unit.Setup(x => x.BeginTransactionAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new Mock<IUnitOfWorkTransaction>().Object);
        _unit.Setup(x => x.Events.FindAsync(_event.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(_event);
        _unit.Setup(x => x.Users.FindByIdAsync(_owner.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_owner);
        _unit.Setup(x => x.Users.FindByIdAsync(_attendee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(_attendee);
    }

    private RegistrationService Service => new(_unit.Object, _clock.Object, _codes.Object);

    [Fact]
    public async Task Owner_CannotRegisterOwnEvent()
    {
        var result = await Service.RegisterAsync(_owner.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Forbidden", result.Error!.Code);
        Assert.Equal("Bạn không thể đăng ký sự kiện do mình tổ chức", result.Error.Message);
    }

    [Fact]
    public async Task ExistingConfirmed_ReturnsExistingRecord_EvenAfterStart()
    {
        var existing = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        Existing(existing);
        _clock.SetupGet(x => x.UtcNow).Returns(_event.StartAt.AddMinutes(1));

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Value!.AlreadyRegistered);
        Assert.Equal(existing.Id, result.Value.RegistrationId);
        _unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task ManagerCancelled_IsBlockedBeforeFullOrClosedChecks()
    {
        var existing = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        Assert.True(existing.Cancel(_event, CancelSource.Manager, _owner.Id, "Vi phạm", Now).IsSuccess);
        Existing(existing);
        Confirmed(2);
        _clock.SetupGet(x => x.UtcNow).Returns(_event.StartAt);

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Đăng ký đã bị ban tổ chức hủy. Bạn không thể đăng ký lại sự kiện này", result.Error!.Message);
    }

    [Fact]
    public async Task FullAndStarted_ReportsClosedFirst()
    {
        Confirmed(2);
        _clock.SetupGet(x => x.UtcNow).Returns(_event.StartAt);

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Đã đóng đăng ký", result.Error!.Message);
    }

    [Fact]
    public async Task Full_IsConflict()
    {
        Confirmed(2);

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Conflict", result.Error!.Code);
        Assert.Equal("Sự kiện đã đủ chỗ", result.Error.Message);
        _unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task NewRegistration_RetriesDuplicateCode()
    {
        Confirmed(0);
        _codes.SetupSequence(x => x.Generate()).Returns("REG-DUPLICATE000").Returns("REG-UNIQUE000000");
        _unit.Setup(x => x.Registrations.CodeExistsAsync("REG-DUPLICATE000", It.IsAny<CancellationToken>())).ReturnsAsync(true);
        Registration? added = null;
        _unit.Setup(x => x.Registrations.Add(It.IsAny<Registration>())).Callback<Registration>(x => added = x);

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.False(result.Value!.AlreadyRegistered);
        Assert.Equal("REG-UNIQUE000000", added!.Code);
        Assert.Equal(_attendee.Id, added.UserId);
        _unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SelfCancelled_ReRegistersWithSameCode()
    {
        var existing = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now.AddHours(-1));
        Assert.True(existing.Cancel(_event, CancelSource.Self, _attendee.Id, null, Now).IsSuccess);
        Existing(existing);
        Confirmed(1);

        var result = await Service.RegisterAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("REG-AAAAAAAAAAAA", result.Value!.Code);
        Assert.Equal(RegistrationStatus.Confirmed, existing.Status);
        Assert.Equal(Now, existing.RegisteredAt);
        _codes.Verify(x => x.Generate(), Times.Never);
    }

    [Fact]
    public async Task SelfCancel_OfAnotherUsersRegistration_IsForbidden()
    {
        var registration = new Registration(_event.Id, _owner.Id, "REG-AAAAAAAAAAAA", Now);
        _unit.Setup(x => x.Registrations.FindAsync(registration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

        var result = await Service.CancelOwnAsync(_attendee.Id, _event.Id, registration.Id, TestContext.Current.CancellationToken);

        Assert.Equal("Forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task SelfCancel_AlreadyCancelled_IsIdempotentAfterStart()
    {
        var registration = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        Assert.True(registration.Cancel(_event, CancelSource.Self, _attendee.Id, null, Now).IsSuccess);
        _unit.Setup(x => x.Registrations.FindAsync(registration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(registration);
        _clock.SetupGet(x => x.UtcNow).Returns(_event.EndAt.AddDays(1));
        _unit.Setup(x => x.Registrations.ExistsForEventAndUserAsync(_event.Id, _attendee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var result = await Service.CancelOwnAsync(_attendee.Id, _event.Id, registration.Id, TestContext.Current.CancellationToken);

        Assert.True(result.Value);
        Assert.Equal(Now, registration.CancelledAt);
        _unit.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Cancel_RegistrationOfAnotherEvent_IsNotFound()
    {
        var registration = new Registration(Guid.NewGuid(), _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        _unit.Setup(x => x.Registrations.FindAsync(registration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

        var result = await Service.CancelOwnAsync(_attendee.Id, _event.Id, registration.Id, TestContext.Current.CancellationToken);

        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task ManagerCancel_ByAttendee_IsForbidden_AndDoesNotRevealIdempotentResult()
    {
        var registration = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        Assert.True(registration.Cancel(_event, CancelSource.Self, _attendee.Id, null, Now).IsSuccess);
        _unit.Setup(x => x.Registrations.FindAsync(registration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

        var result = await Service.CancelAsManagerAsync(_attendee.Id, _event.Id, registration.Id, "Lý do", TestContext.Current.CancellationToken);

        Assert.Equal("Forbidden", result.Error!.Code);
        Assert.Equal(CancelSource.Self, registration.CancelSource);
    }

    [Fact]
    public async Task ManagerCancel_ByOwner_StoresManagerSource()
    {
        var registration = new Registration(_event.Id, _attendee.Id, "REG-AAAAAAAAAAAA", Now);
        _unit.Setup(x => x.Registrations.FindAsync(registration.Id, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

        var result = await Service.CancelAsManagerAsync(_owner.Id, _event.Id, registration.Id, "  Không đủ điều kiện  ", TestContext.Current.CancellationToken);

        Assert.False(result.Value);
        Assert.Equal(CancelSource.Manager, registration.CancelSource);
        Assert.Equal("Không đủ điều kiện", registration.CancelReason);
        Assert.Equal(_owner.Id, registration.CancelledBy);
    }

    [Fact]
    public async Task Participants_NonManager_GetsForbiddenBeforeInvalidPage()
    {
        var result = await Service.ListParticipantsAsync(_attendee.Id, _event.Id, new ParticipantListRequest(Page: "0"), TestContext.Current.CancellationToken);

        Assert.Equal("Forbidden", result.Error!.Code);
    }

    [Fact]
    public async Task Participants_HiddenDraftOfAnotherOrganizer_IsNotFound()
    {
        var other = NewUser("other", UserRole.Organizer);
        var draft = Event.Create(_owner.Id, EventType.Event, "Bản nháp ẩn", "Mô tả", "Hà Nội", Now.AddHours(1), Now.AddHours(2), 5, Now).Value!;
        _unit.Setup(x => x.Users.FindByIdAsync(other.Id, It.IsAny<CancellationToken>())).ReturnsAsync(other);
        _unit.Setup(x => x.Events.FindAsync(draft.Id, It.IsAny<bool>(), It.IsAny<CancellationToken>())).ReturnsAsync(draft);

        var result = await Service.ListParticipantsAsync(other.Id, draft.Id, new ParticipantListRequest(), TestContext.Current.CancellationToken);

        Assert.Equal("NotFound", result.Error!.Code);
    }

    [Fact]
    public async Task Participants_InvalidFilter_IsBadRequestForManager()
    {
        var result = await Service.ListParticipantsAsync(_owner.Id, _event.Id, new ParticipantListRequest(Status: "confirmed"), TestContext.Current.CancellationToken);

        Assert.Equal("Bộ lọc không hợp lệ", result.Error!.Message);
    }

    [Fact]
    public async Task CompletedEvent_IsHiddenFromGuestAndNonRegistrant()
    {
        _clock.SetupGet(x => x.UtcNow).Returns(_event.EndAt);
        _unit.Setup(x => x.Registrations.ExistsForEventAndUserAsync(_event.Id, _attendee.Id, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        var service = new PublicEventService(_unit.Object, _clock.Object);

        var guest = await service.GetAsync(null, _event.Id, TestContext.Current.CancellationToken);
        var attendee = await service.GetAsync(_attendee.Id, _event.Id, TestContext.Current.CancellationToken);

        Assert.Equal("NotFound", guest.Error!.Code);
        Assert.Equal("NotFound", attendee.Error!.Code);
    }

    // User IDs are normally generated by EF on insert; unit tests need distinct identities.
    private static User NewUser(string username, UserRole role)
    {
        var user = new User(username, "hash", "stamp", username, role, UserStatus.Active);
        typeof(User).GetProperty(nameof(User.Id))!.SetValue(user, Guid.NewGuid());
        return user;
    }

    private void Existing(Registration registration) =>
        _unit.Setup(x => x.Registrations.FindByEventAndUserAsync(_event.Id, registration.UserId, It.IsAny<CancellationToken>())).ReturnsAsync(registration);

    private void Confirmed(int count) =>
        _unit.Setup(x => x.Registrations.CountConfirmedAsync(_event.Id, It.IsAny<CancellationToken>())).ReturnsAsync(count);
}
