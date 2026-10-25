using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using EMCA.Application.DTOs;
using EMCA.Application.Services;
using Xunit;

namespace ECMA.Domain.Tests;

public sealed class RegistrationTests
{
    private static readonly DateTime Now = new(2026, 10, 19, 8, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Actor = Guid.NewGuid();

    [Fact]
    public void SelfCancel_BeforeStart_StoresSystemReasonAndSource()
    {
        var item = Published();
        var registration = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now);

        var result = registration.Cancel(item, CancelSource.Self, Actor, "ignored", item.StartAt.AddMinutes(-1));

        Assert.True(result.IsSuccess);
        Assert.Equal(RegistrationStatus.Cancelled, registration.Status);
        Assert.Equal(CancelSource.Self, registration.CancelSource);
        Assert.Equal("Người tham dự tự hủy", registration.CancelReason);
        Assert.Equal(Actor, registration.CancelledBy);
    }

    [Fact]
    public void Cancel_AtExactStart_IsRejected()
    {
        var item = Published();
        var result = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now).Cancel(item, CancelSource.Self, Actor, null, item.StartAt);

        Assert.Equal("Đã bắt đầu sự kiện. Không thể hủy đăng ký", result.Error!.Message);
    }

    [Fact]
    public void ManagerCancel_ChecksTimeBeforeReason()
    {
        var item = Published();
        var result = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now).Cancel(item, CancelSource.Manager, Actor, "  ", item.StartAt);

        Assert.Equal("Conflict", result.Error!.Code);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(200, true)]
    [InlineData(201, false)]
    public void ManagerCancel_ReasonLengthBoundaries(int length, bool valid)
    {
        var item = Published();
        var result = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now).Cancel(item, CancelSource.Manager, Actor, new string('a', length), Now);

        Assert.Equal(valid, result.IsSuccess);
        if (!valid) Assert.Equal("Lý do hủy phải có từ 1 đến 200 ký tự", result.Error!.Message);
    }

    [Fact]
    public void Cancel_OnCancelledEvent_IsRejected()
    {
        var item = Published();
        Assert.True(item.Cancel("Lý do", Actor, Now).IsSuccess);
        var result = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now).Cancel(item, CancelSource.Self, Actor, null, Now);

        Assert.Equal("Sự kiện không cho phép hủy đăng ký", result.Error!.Message);
    }

    [Fact]
    public void Reconfirm_KeepsIdAndCode_AndClearsCancellation()
    {
        var item = Published();
        var registration = new Registration(item.Id, Actor, "REG-ABCDEFGHJKLM", Now);
        var id = registration.Id;
        Assert.True(registration.Cancel(item, CancelSource.Self, Actor, null, Now).IsSuccess);

        registration.Reconfirm(Now.AddMinutes(5));

        Assert.Equal(id, registration.Id);
        Assert.Equal("REG-ABCDEFGHJKLM", registration.Code);
        Assert.Equal(RegistrationStatus.Confirmed, registration.Status);
        Assert.Equal(Now.AddMinutes(5), registration.RegisteredAt);
        Assert.Null(registration.CancelSource);
        Assert.Null(registration.CancelReason);
        Assert.Null(registration.CancelledAt);
        Assert.Null(registration.CancelledBy);
    }

    [Theory]
    [InlineData(null, true, 1)]
    [InlineData("1", true, 1)]
    [InlineData("01", true, 1)]
    [InlineData("2147483647", true, 2147483647)]
    [InlineData("", false, 0)]
    [InlineData("0", false, 0)]
    [InlineData("-1", false, 0)]
    [InlineData("+1", false, 0)]
    [InlineData(" 1", false, 0)]
    [InlineData("1.5", false, 0)]
    [InlineData("abc", false, 0)]
    [InlineData("2147483648", false, 0)]
    [InlineData(QueryParameters.MultipleValues, false, 0)]
    public void Page_FollowsSrsSection22(string? raw, bool valid, int expected)
    {
        Assert.Equal(valid, QueryParameters.TryPage(raw, out var page));
        if (valid) Assert.Equal(expected, page);
    }

    [Fact]
    public void Keyword_IsNfcNormalized_AndLimitedTo100CodePoints()
    {
        Assert.True(QueryParameters.TryKeyword("  Hội nghị  ", out var keyword));
        Assert.Equal("Hội nghị", keyword);
        Assert.True(QueryParameters.TryKeyword(new string('ệ', 100), out _));
        Assert.False(QueryParameters.TryKeyword(new string('ệ', 101), out _));
        Assert.True(QueryParameters.TryKeyword("   ", out var empty));
        Assert.Null(empty);
    }

    [Theory]
    [InlineData(0, 0, 0)]
    [InlineData(3, 1, 33.3)]
    [InlineData(3, 2, 66.7)]
    [InlineData(4, 4, 100)]
    public void Stats_AttendanceRateRoundsToOneDecimal(int confirmed, int checkedIn, double expected)
    {
        var stats = RegistrationService.Stats(10, new RegistrationCounts(confirmed, 2, checkedIn), EffectiveEventStatus.Published);

        Assert.Equal((decimal)expected, stats.AttendanceRate);
        Assert.Equal(10 - confirmed, stats.Remaining);
        Assert.Equal(confirmed - checkedIn, stats.NotCheckedIn);
        Assert.Equal(2, stats.Cancelled);
        Assert.Null(stats.Absent);
    }

    [Fact]
    public void Stats_AbsentOnlyWhenCompleted()
    {
        Assert.Equal(2, RegistrationService.Stats(10, new RegistrationCounts(5, 0, 3), EffectiveEventStatus.Completed).Absent);
        Assert.Null(RegistrationService.Stats(10, new RegistrationCounts(5, 0, 3), EffectiveEventStatus.Cancelled).Absent);
    }

    [Fact]
    public void Availability_ClosedFromStart_EvenWithSeats()
    {
        var item = Published();
        Assert.Equal(Availability.Open, PublicEventService.AvailabilityOf(item, 9, Now));
        Assert.Equal(Availability.Full, PublicEventService.AvailabilityOf(item, 10, Now));
        Assert.Equal(Availability.Closed, PublicEventService.AvailabilityOf(item, 0, item.StartAt));
        Assert.Equal(Availability.Closed, PublicEventService.AvailabilityOf(item, 10, item.StartAt));
    }

    private static Event Published()
    {
        var item = Event.Create(Guid.NewGuid(), EventType.Event, "Sự kiện mẫu", "Mô tả", "Hà Nội", Now.AddHours(1), Now.AddHours(3), 10, Now).Value!;
        Assert.True(item.Publish(Now).IsSuccess);
        return item;
    }
}
