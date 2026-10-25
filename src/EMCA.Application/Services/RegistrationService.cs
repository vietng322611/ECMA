using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Domain.Results;
using EMCA.Application.DTOs;

namespace EMCA.Application.Services;

// FR-REG-01..04 and FR-PART-01..04. Every write locks the event row first, so registrations,
// cancellations, capacity increases and event cancellation of one event are serialized (NFR-04).
public sealed class RegistrationService(IUnitOfWork unitOfWork, IClock clock, IRegistrationCodeGenerator codes)
{
    public const int PageSize = 20;
    private const int MaxCodeAttempts = 5;
    private static readonly IReadOnlyDictionary<string, RegistrationStatus> Statuses = new Dictionary<string, RegistrationStatus>(StringComparer.Ordinal)
    {
        ["Confirmed"] = RegistrationStatus.Confirmed, ["Cancelled"] = RegistrationStatus.Cancelled
    };
    private static readonly IReadOnlyDictionary<string, bool> CheckIns = new Dictionary<string, bool>(StringComparer.Ordinal)
    {
        ["CheckedIn"] = true, ["NotCheckedIn"] = false
    };

    // SRS §4.3, steps 2-9.
    public async Task<Result<RegisterOutcome>> RegisterAsync(Guid userId, Guid eventId, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var item = await unitOfWork.Events.FindAsync(eventId, true, cancellationToken);
        var user = await unitOfWork.Users.FindByIdAsync(userId, cancellationToken);
        var now = clock.UtcNow;
        if (item is null || user is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, now, cancellationToken))
            return NotFound<RegisterOutcome>();
        if (item.OwnerId == user.Id) return Forbidden<RegisterOutcome>("Bạn không thể đăng ký sự kiện do mình tổ chức");

        var existing = await unitOfWork.Registrations.FindByEventAndUserAsync(eventId, user.Id, cancellationToken);
        if (existing?.Status == RegistrationStatus.Confirmed)
            return Result<RegisterOutcome>.Success(new(existing.Id, existing.Code, true));
        if (existing?.CancelSource == CancelSource.Manager)
            return Forbidden<RegisterOutcome>("Đăng ký đã bị ban tổ chức hủy. Bạn không thể đăng ký lại sự kiện này");

        var status = item.EffectiveStatus(now);
        if (status is EffectiveEventStatus.Draft or EffectiveEventStatus.Cancelled) return Conflict<RegisterOutcome>("Sự kiện không mở đăng ký");
        if (now >= item.StartAt) return Conflict<RegisterOutcome>("Đã đóng đăng ký");
        if (await unitOfWork.Registrations.CountConfirmedAsync(eventId, cancellationToken) >= item.Capacity)
            return Conflict<RegisterOutcome>("Sự kiện đã đủ chỗ");

        var registration = existing;
        if (registration is null)
        {
            registration = new Registration(eventId, user.Id, await NewCodeAsync(cancellationToken), now);
            unitOfWork.Registrations.Add(registration);
        }
        else registration.Reconfirm(now);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<RegisterOutcome>.Success(new(registration.Id, registration.Code, false));
    }

    // FR-REG-02 / BR-REG-07.
    public async Task<Result<MyRegistrationPageDto>> ListMineAsync(Guid userId, string? rawPage, CancellationToken cancellationToken = default)
    {
        if (!QueryParameters.TryPage(rawPage, out var page)) return BadRequest<MyRegistrationPageDto>("Tham số trang không hợp lệ");
        var now = clock.UtcNow;
        var rows = await unitOfWork.Registrations.ListForUserAsync(userId, page, cancellationToken);
        return Result<MyRegistrationPageDto>.Success(new(page, rows.Select(x => new MyRegistrationDto(
            x.Registration.Id, x.Event.Id, x.Event.Type, x.Event.Title, x.Event.Location, x.Event.StartAt, x.Event.EndAt, x.Event.EffectiveStatus(now),
            x.Registration.Code, x.Registration.Status, x.Registration.RegisteredAt, x.Registration.CancelSource, x.Registration.CancelledAt,
            x.Registration.CancelReason, x.Registration.CheckedInAt, IsCancellable(x.Event, x.Registration, now))).ToList()));
    }

    // FR-REG-03, SRS §4.5 with source SELF. Success value is true when the registration had already been cancelled.
    public Task<Result<bool>> CancelOwnAsync(Guid userId, Guid eventId, Guid registrationId, CancellationToken cancellationToken = default) =>
        CancelAsync(userId, eventId, registrationId, CancelSource.Self, null, cancellationToken);

    // FR-PART-03, SRS §4.5 with source MANAGER.
    public Task<Result<bool>> CancelAsManagerAsync(Guid userId, Guid eventId, Guid registrationId, string? reason, CancellationToken cancellationToken = default) =>
        CancelAsync(userId, eventId, registrationId, CancelSource.Manager, reason, cancellationToken);

    public async Task<Result<ManagerCancelFormDto>> GetManagerCancelFormAsync(Guid userId, Guid eventId, Guid registrationId, CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Events.FindAsync(eventId, false, cancellationToken);
        var user = await unitOfWork.Users.FindByIdAsync(userId, cancellationToken);
        if (item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, clock.UtcNow, cancellationToken)) return NotFound<ManagerCancelFormDto>();
        var registration = await unitOfWork.Registrations.FindAsync(registrationId, cancellationToken);
        if (registration is null || registration.EventId != eventId) return NotFound<ManagerCancelFormDto>();
        if (!EventAccessPolicy.CanManage(user, item)) return Forbidden<ManagerCancelFormDto>();
        var attendee = await unitOfWork.Users.FindByIdAsync(registration.UserId, cancellationToken);
        return Result<ManagerCancelFormDto>.Success(new(item.Id, item.Title, registration.Id, attendee?.Username ?? "", attendee?.FullName ?? "", registration.Code));
    }

    // FR-PART-01/02/04 and BR-PART-01/03: statistics always cover the whole event, independent of filters.
    public async Task<Result<ParticipantPageDto>> ListParticipantsAsync(Guid userId, Guid eventId, ParticipantListRequest request, CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Events.FindAsync(eventId, false, cancellationToken);
        var user = await unitOfWork.Users.FindByIdAsync(userId, cancellationToken);
        var now = clock.UtcNow;
        if (item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, now, cancellationToken)) return NotFound<ParticipantPageDto>();
        if (!EventAccessPolicy.CanManage(user, item)) return Forbidden<ParticipantPageDto>();
        if (!QueryParameters.TryPage(request.Page, out var page)) return BadRequest<ParticipantPageDto>("Tham số trang không hợp lệ");
        if (!QueryParameters.TryKeyword(request.Keyword, out var keyword)) return BadRequest<ParticipantPageDto>("Từ khóa tối đa 100 ký tự");
        if (!QueryParameters.TryChoice(request.Status, Statuses, out var status) || !QueryParameters.TryChoice(request.CheckIn, CheckIns, out var checkedIn))
            return BadRequest<ParticipantPageDto>("Bộ lọc không hợp lệ");

        var counts = await unitOfWork.Registrations.CountAsync(eventId, cancellationToken);
        var rows = await unitOfWork.Registrations.ListParticipantsAsync(new(eventId, keyword, status, checkedIn, page), cancellationToken);
        var effective = item.EffectiveStatus(now);
        return Result<ParticipantPageDto>.Success(new(item.Id, item.Title, effective, item.StartAt, item.EndAt, Stats(item.Capacity, counts, effective),
            keyword, status, checkedIn, page, effective == EffectiveEventStatus.Published && now < item.StartAt,
            rows.Select(x => new ParticipantDto(x.Registration.Id, x.Username, x.FullName, x.Registration.Code, x.Registration.Status,
                x.Registration.RegisteredAt, x.Registration.CheckedInAt, x.CheckedInByUsername, x.Registration.CancelSource,
                x.Registration.CancelledAt, x.Registration.CancelReason, x.CancelledByUsername)).ToList()));
    }

    // SRS §3.6 formula table.
    public static ParticipantStatsDto Stats(int capacity, RegistrationCounts counts, EffectiveEventStatus status)
    {
        var notCheckedIn = counts.Confirmed - counts.CheckedIn;
        var rate = counts.Confirmed == 0 ? 0m : Math.Round(counts.CheckedIn * 100m / counts.Confirmed, 1, MidpointRounding.AwayFromZero);
        return new(capacity, counts.Confirmed, counts.Cancelled, capacity - counts.Confirmed, counts.CheckedIn, notCheckedIn, rate,
            status == EffectiveEventStatus.Completed ? notCheckedIn : null);
    }

    public static bool IsCancellable(Event item, Registration registration, DateTime now) =>
        registration.Status == RegistrationStatus.Confirmed && !registration.IsCheckedIn &&
        item.EffectiveStatus(now) == EffectiveEventStatus.Published && now < item.StartAt;

    private async Task<Result<bool>> CancelAsync(Guid userId, Guid eventId, Guid registrationId, CancelSource source, string? reason, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var item = await unitOfWork.Events.FindAsync(eventId, true, cancellationToken);
        var user = await unitOfWork.Users.FindByIdAsync(userId, cancellationToken);
        var now = clock.UtcNow;
        if (item is null || user is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, now, cancellationToken)) return NotFound<bool>();
        var registration = await unitOfWork.Registrations.FindAsync(registrationId, cancellationToken);
        if (registration is null || registration.EventId != eventId) return NotFound<bool>();
        var allowed = source == CancelSource.Self ? registration.UserId == user.Id : EventAccessPolicy.CanManage(user, item);
        if (!allowed) return Forbidden<bool>();
        if (registration.Status == RegistrationStatus.Cancelled) return Result<bool>.Success(true);

        var result = registration.Cancel(item, source, user.Id, reason, now);
        if (result.IsFailure) return Result<bool>.Failure(result.Error!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<bool>.Success(false);
    }

    private async Task<string> NewCodeAsync(CancellationToken cancellationToken)
    {
        for (var attempt = 0; attempt < MaxCodeAttempts; attempt++)
        {
            var code = codes.Generate();
            if (!await unitOfWork.Registrations.CodeExistsAsync(code, cancellationToken)) return code;
        }

        throw new InvalidOperationException("Không thể sinh mã đăng ký duy nhất");
    }

    private static Result<T> NotFound<T>() => Result<T>.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
    private static Result<T> Forbidden<T>(string message = "Bạn không có quyền thực hiện thao tác này") => Result<T>.Failure(Error.Forbidden(message));
    private static Result<T> Conflict<T>(string message) => Result<T>.Failure(Error.Conflict(message));
    private static Result<T> BadRequest<T>(string message) => Result<T>.Failure(Error.BadRequest(message));
}
