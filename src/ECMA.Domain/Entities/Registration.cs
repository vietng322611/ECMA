using ECMA.Domain.Enums;
using ECMA.Domain.Results;

namespace ECMA.Domain.Entities;

public sealed class Registration
{
    public const string SelfCancelReason = "Người tham dự tự hủy";

    public Guid Id { get; private set; }
    public Guid EventId { get; private set; }
    public Guid UserId { get; private set; }
    public string Code { get; private set; } = string.Empty;
    public RegistrationStatus Status { get; private set; }
    public DateTime RegisteredAt { get; private set; }
    public CancelSource? CancelSource { get; private set; }
    public DateTime? CancelledAt { get; private set; }
    public string? CancelReason { get; private set; }
    public Guid? CancelledBy { get; private set; }
    public DateTime? CheckedInAt { get; private set; }
    public Guid? CheckedInBy { get; private set; }

    private Registration() { }

    public Registration(Guid eventId, Guid userId, string code, DateTime now)
    {
        Id = Guid.NewGuid();
        EventId = eventId;
        UserId = userId;
        Code = code;
        Status = RegistrationStatus.Confirmed;
        RegisteredAt = now;
    }

    public bool IsCheckedIn => CheckedInAt is not null;

    // BR-REG-02: only SELF cancellations may be confirmed again; ID and code are kept.
    public void Reconfirm(DateTime now)
    {
        Status = RegistrationStatus.Confirmed;
        RegisteredAt = now;
        CancelSource = null;
        CancelledAt = null;
        CancelReason = null;
        CancelledBy = null;
    }

    // BR-REG-05 / BR-PART-02 / §4.5 steps 6-10. Caller handles ownership and the idempotent Cancelled case first.
    public Result Cancel(Event item, CancelSource source, Guid actorId, string? reason, DateTime now)
    {
        var status = item.EffectiveStatus(now);
        if (status is EffectiveEventStatus.Draft or EffectiveEventStatus.Cancelled)
            return Result.Failure(Error.Conflict("Sự kiện không cho phép hủy đăng ký"));
        if (IsCheckedIn) return Result.Failure(Error.Conflict("Không thể hủy đăng ký đã điểm danh"));
        if (now >= item.StartAt) return Result.Failure(Error.Conflict("Đã bắt đầu sự kiện. Không thể hủy đăng ký"));

        reason = source == Enums.CancelSource.Self ? SelfCancelReason : AgendaItem.Normalize(reason ?? "");
        if (AgendaItem.Length(reason) is < 1 or > 200)
            return Result.Failure(Error.Validation("Reason", "Lý do hủy phải có từ 1 đến 200 ký tự"));

        Status = RegistrationStatus.Cancelled;
        CancelSource = source;
        CancelledAt = now;
        CancelReason = reason;
        CancelledBy = actorId;
        return Result.Success();
    }
}
