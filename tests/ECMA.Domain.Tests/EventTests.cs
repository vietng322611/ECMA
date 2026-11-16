using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Results;
using Xunit;

namespace ECMA.Domain.Tests;

public sealed class EventTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Conference_PublishRequiresAgenda_ThenLocksAgenda()
    {
        var item = Valid(EventType.Conference);
        Assert.Equal("Conflict", item.Publish(Now).Error!.Code);
        Assert.True(item.AddAgenda("Khai mạc", null, Now.AddHours(2), Now.AddHours(2).AddMinutes(5)).IsSuccess);
        Assert.True(item.Publish(Now).IsSuccess);
        Assert.False(item.AddAgenda("Phiên sau", null, Now.AddHours(3), Now.AddHours(4)).IsSuccess);
    }

    [Fact]
    public void Agenda_RejectsOverlap_AllowsTouchingBoundary()
    {
        var item = Valid(EventType.Event);
        Assert.True(item.AddAgenda("Phiên một", null, Now.AddHours(2), Now.AddHours(3)).IsSuccess);
        Assert.False(item.AddAgenda("Chồng lấn", null, Now.AddHours(2).AddMinutes(30), Now.AddHours(3).AddMinutes(30)).IsSuccess);
        Assert.True(item.AddAgenda("Chạm biên", null, Now.AddHours(3), Now.AddHours(4)).IsSuccess);
    }

    [Fact]
    public void DraftTimeChange_RejectsAgendaOutsideNewRange()
    {
        var item = Valid(EventType.Event);
        Assert.True(item.AddAgenda("Phiên cuối", null, Now.AddHours(4), Now.AddHours(5)).IsSuccess);
        var result = item.UpdateDraft(EventType.Event, "Sự kiện mới", "Mô tả", "Hà Nội", Now.AddHours(1), Now.AddHours(4), 100, Now);
        Assert.False(result.IsSuccess);
    }

    [Fact]
    public void Published_UsesDerivedCompleted_AndOnlyCapacityIncrease()
    {
        var item = Valid(EventType.Event);
        Assert.True(item.Publish(Now).IsSuccess);
        Assert.False(item.UpdatePublished("Sự kiện mới", "Mô tả", "Hà Nội", 99, Now).IsSuccess);
        Assert.True(item.UpdatePublished("Sự kiện mới", "Mô tả", "Hà Nội", 101, Now).IsSuccess);
        Assert.Equal(EffectiveEventStatus.Completed, item.EffectiveStatus(Now.AddHours(7)));
    }

    [Fact]
    public void Values_AreNfcTrimmed_ButInvalidPrecisionIsRejected()
    {
        var valid = Event.Create(Guid.NewGuid(), EventType.Event, "  Sự kiện  ", " Mô tả ", " Hà Nội ",
            Now.AddHours(1), Now.AddHours(2), 10, Now);
        var seconds = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện", "Mô tả", "Hà Nội",
            Now.AddHours(1).AddSeconds(1), Now.AddHours(2), 10, Now);
        var local = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện", "Mô tả", "Hà Nội",
            DateTime.SpecifyKind(Now.AddHours(1), DateTimeKind.Local), Now.AddHours(2), 10, Now);

        Assert.Equal("Sự kiện", valid.Value!.Title);
        Assert.Equal("Time", seconds.Error!.Code);
        Assert.Equal("Time", local.Error!.Code);
    }

    [Fact]
    public void UnicodeLength_CountsScalars_NotGraphemeClusters()
    {
        var fourScalars = "👨‍👩";
        var result = Event.Create(Guid.NewGuid(), EventType.Event, fourScalars, "Mô tả", "Hà Nội",
            Now.AddHours(1), Now.AddHours(2), 10, Now);

        Assert.Equal("Title", result.Error!.Code);
    }

    [Fact]
    public void Agenda_SeparatesValidationFromOverlapConflict()
    {
        var item = Valid(EventType.Event);
        Assert.Equal("Agenda", item.AddAgenda("Ab", null, Now.AddHours(2), Now.AddHours(3)).Error!.Code);
        Assert.True(item.AddAgenda("Phiên một", null, Now.AddHours(2), Now.AddHours(3)).IsSuccess);
        Assert.Equal("Conflict", item.AddAgenda("Phiên hai", null, Now.AddHours(2), Now.AddHours(3)).Error!.Code);
    }

    private static Event Valid(EventType type) => Event.Create(Guid.NewGuid(), type, "Sự kiện mẫu", "Mô tả", "Hà Nội",
        Now.AddHours(1), Now.AddHours(6), 100, Now).Value!;

    private static Result<Event> Make(string title = "Sự kiện mẫu", string description = "Mô tả", string location = "Hà Nội",
        DateTime? start = null, DateTime? end = null, int capacity = 100, EventType type = EventType.Event) =>
        Event.Create(Guid.NewGuid(), type, title, description, location,
            start ?? Now.AddHours(1), end ?? Now.AddHours(6), capacity, Now);

    private static Event Draft(EventType type = EventType.Event) => Make(type: type).Value!;

    [Theory]
    [InlineData(4, "Title")]      // below minimum
    [InlineData(5, null)]         // minimum boundary
    [InlineData(100, null)]       // maximum boundary
    [InlineData(101, "Title")]    // above maximum
    public void Title_UsesInclusiveBoundaries(int length, string? expectedCode)
    {
        Assert.Equal(expectedCode, Make(title: new string('a', length)).Error?.Code);
    }

    [Theory]
    [InlineData(0, "Capacity")]
    [InlineData(1, null)]
    [InlineData(10000, null)]
    [InlineData(10001, "Capacity")]
    public void Capacity_UsesInclusiveBoundaries(int capacity, string? expectedCode)
    {
        Assert.Equal(expectedCode, Make(capacity: capacity).Error?.Code);
    }

    [Fact]
    public void DescriptionAndLocation_RejectEmptyAndOverlongValues()
    {
        Assert.Equal("Description", Make(description: "").Error!.Code);
        Assert.Equal("Description", Make(description: new string('a', 3001)).Error!.Code);
        Assert.Equal("Location", Make(location: "  ").Error!.Code);
        Assert.Equal("Location", Make(location: new string('a', 201)).Error!.Code);
        Assert.True(Make(description: new string('a', 3000), location: new string('a', 200)).IsSuccess);
    }

    [Fact]
    public void Duration_MinimumFiveMinutesAndMaximumThirtyDays()
    {
        Assert.True(Make(start: Now.AddHours(1), end: Now.AddHours(1).AddMinutes(5)).IsSuccess);
        Assert.Equal("Time", Make(start: Now.AddHours(1), end: Now.AddHours(1).AddMinutes(4)).Error!.Code);
        Assert.True(Make(start: Now.AddHours(1), end: Now.AddHours(1).AddDays(30)).IsSuccess);
        Assert.Equal("Time", Make(start: Now.AddHours(1), end: Now.AddHours(1).AddDays(30).AddMinutes(1)).Error!.Code);
    }

    [Fact]
    public void StartAtOrBeforeNow_IsRejected()
    {
        Assert.Equal("Time", Make(start: Now, end: Now.AddHours(1)).Error!.Code);
        Assert.Equal("Time", Make(start: Now.AddHours(-1), end: Now.AddHours(1)).Error!.Code);
        Assert.Equal("Time", Make(start: Now.AddHours(2), end: Now.AddHours(1)).Error!.Code);
    }

    [Fact]
    public void DraftUpdate_RejectsNonDraftEvents()
    {
        var published = Draft();
        Assert.True(published.Publish(Now).IsSuccess);

        var result = published.UpdateDraft(EventType.Conference, "Sự kiện khác", "Mô tả", "Đà Nẵng",
            Now.AddHours(1), Now.AddHours(6), 100, Now);

        Assert.Equal("Conflict", result.Error!.Code);
    }

    [Fact]
    public void PublishedUpdate_IsLockedFromStartTime()
    {
        var item = Draft();
        Assert.True(item.Publish(Now).IsSuccess);

        Assert.Equal("Conflict", item.UpdatePublished("Sự kiện mới", "Mô tả", "Hà Nội", 100, Now.AddHours(1)).Error!.Code);
        Assert.True(item.UpdatePublished("Sự kiện mới", "Mô tả", "Hà Nội", 100, Now.AddMinutes(59)).IsSuccess);
    }

    [Fact]
    public void Agenda_IsLockedAfterPublish()
    {
        var item = Draft(EventType.Conference);
        var agenda = item.AddAgenda("Khai mạc", null, Now.AddHours(2), Now.AddHours(3)).Value!;
        Assert.True(item.Publish(Now).IsSuccess);

        Assert.Equal("Conflict", item.AddAgenda("Phiên hai", null, Now.AddHours(3), Now.AddHours(4)).Error!.Code);
        Assert.Equal("Conflict", item.UpdateAgenda(agenda.Id, "Khai mạc mới", null, Now.AddHours(2), Now.AddHours(3)).Error!.Code);
        Assert.Equal("Conflict", item.DeleteAgenda(agenda.Id).Error!.Code);
    }

    [Fact]
    public void Agenda_UnknownItem_IsNotFound()
    {
        var item = Draft(EventType.Conference);
        Assert.Equal("NotFound", item.DeleteAgenda(Guid.NewGuid()).Error!.Code);
        Assert.Equal("NotFound", item.UpdateAgenda(Guid.NewGuid(), "Không tồn tại", null, Now.AddHours(2), Now.AddHours(3)).Error!.Code);
    }

    [Fact]
    public void Publish_RejectsStartedOrAlreadyPublishedEvents()
    {
        var started = Make().Value!;
        Assert.Equal("Conflict", started.Publish(Now.AddHours(1)).Error!.Code);
        Assert.Equal(EventStatus.Draft, started.Status);

        var published = Make().Value!;
        Assert.True(published.Publish(Now).IsSuccess);
        Assert.Equal("Conflict", published.Publish(Now.AddHours(1)).Error!.Code);
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(0, false)]
    [InlineData(201, false)]
    public void Cancel_ReasonLengthBoundaries(int length, bool expected)
    {
        var item = Draft();
        Assert.True(item.Publish(Now).IsSuccess);

        var actorId = Guid.NewGuid();
        var result = item.Cancel(new string('a', length), actorId, Now);

        Assert.Equal(expected, result.IsSuccess);
        Assert.Equal(expected, item.Status == EventStatus.Cancelled);
        if (expected)
        {
            Assert.Equal(actorId, item.CancelledBy);
            Assert.Equal(Now, item.CancelledAt);
            Assert.Equal(new string('a', length), item.CancelReason);
        }
    }

    [Fact]
    public void Cancel_RejectsDraftAndStartedEvents()
    {
        Assert.Equal("Conflict", Draft().Cancel("Lý do", Guid.NewGuid(), Now).Error!.Code);

        var published = Draft();
        Assert.True(published.Publish(Now).IsSuccess);
        Assert.Equal("Conflict", published.Cancel("Lý do", Guid.NewGuid(), Now.AddHours(6)).Error!.Code);
    }

    [Fact]
    public void Delete_OnlyAppliesToLiveDrafts()
    {
        var draft = Draft();
        Assert.True(draft.Delete().IsSuccess);
        Assert.True(draft.IsDeleted);
        Assert.Equal("NotFound", draft.Delete().Error!.Code);

        var published = Draft();
        Assert.True(published.Publish(Now).IsSuccess);
        Assert.Equal("Conflict", published.Delete().Error!.Code);
        Assert.False(published.IsDeleted);
    }

    [Fact]
    public void EffectiveStatus_IsDraftOrCompletedOrCancelledByPublishState()
    {
        Assert.Equal(EffectiveEventStatus.Draft, Draft().EffectiveStatus(Now.AddDays(5)));

        var published = Draft();
        Assert.True(published.Publish(Now).IsSuccess);
        Assert.Equal(EffectiveEventStatus.Published, published.EffectiveStatus(Now.AddHours(5).AddMinutes(59)));
        Assert.Equal(EffectiveEventStatus.Completed, published.EffectiveStatus(Now.AddHours(6)));

        var cancelled = Draft();
        Assert.True(cancelled.Publish(Now).IsSuccess);
        Assert.True(cancelled.Cancel("Lý do", Guid.NewGuid(), Now).IsSuccess);
        Assert.Equal(EffectiveEventStatus.Cancelled, cancelled.EffectiveStatus(Now.AddDays(5)));
    }
}

