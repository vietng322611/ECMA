using System.Text;
using System.Text.RegularExpressions;
using ECMA.Domain.Enums;
using ECMA.Domain.Results;
using ECMA.Infrastructure.Data;
using EMCA.Application.Services;
using Microsoft.EntityFrameworkCore;

namespace ECMA.Web.Services;

// FR-ATT-01..03. Reuses the event row lock shared by registrations and cancellations.
public sealed class CheckInService(EcmaDbContext db, ECMA.Domain.Interfaces.IClock clock, ECMA.Domain.Interfaces.IUnitOfWork unitOfWork)
{
    private static readonly Regex CodePattern = new(@"^REG-[A-Z0-9]{12}$", RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public static string NormalizeCode(string? raw)
    {
        var text = (raw ?? "").Normalize(NormalizationForm.FormC).Trim();
        return new string(text.Select(c => c is >= 'a' and <= 'z' ? (char)(c - 32) : c).ToArray());
    }

    public async Task<Result> CanOpenAsync(Guid actorId, Guid eventId, CancellationToken ct = default)
    {
        var actor = await db.Users.SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && !x.IsDeleted, ct);
        if (item is null || actor is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, actor, item, clock.UtcNow, ct))
            return Result.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
        return EventAccessPolicy.CanManage(actor, item) ? Result.Success()
            : Result.Failure(Error.Forbidden("Bạn không có quyền thực hiện thao tác này"));
    }

    public async Task<Result<string>> CheckAsync(Guid actorId, Guid eventId, string? rawCode, CancellationToken ct = default)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var actor = await db.Users.SingleOrDefaultAsync(x => x.Id == actorId, ct);
        var item = await db.Events.SingleOrDefaultAsync(x => x.Id == eventId && !x.IsDeleted, ct);
        var now = clock.UtcNow;
        if (item is null || actor is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, actor, item, now, ct))
            return Result<string>.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
        if (!EventAccessPolicy.CanManage(actor, item))
            return Result<string>.Failure(Error.Forbidden("Bạn không có quyền thực hiện thao tác này"));
        // Lock the event before checking time/code to serialize same-event writes.
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM events WHERE id = {eventId} FOR UPDATE", ct);
        await db.Entry(item).ReloadAsync(ct);
        now = clock.UtcNow;
        if (item.Status is EventStatus.Draft or EventStatus.Cancelled)
            return Result<string>.Failure(Error.Conflict("Sự kiện không cho phép điểm danh"));
        if (now < item.StartAt) return Result<string>.Failure(Error.Conflict("Chưa đến giờ điểm danh"));
        if (now >= item.EndAt) return Result<string>.Failure(Error.Conflict("Đã hết giờ điểm danh"));
        var code = NormalizeCode(rawCode);
        if (!CodePattern.IsMatch(code)) return Result<string>.Failure(Error.BadRequest("Mã đăng ký không hợp lệ"));
        var registration = await db.Registrations.SingleOrDefaultAsync(x => x.Code == code, ct);
        if (registration is null) return Result<string>.Failure(Error.NotFound("Không tìm thấy mã đăng ký"));
        if (registration.EventId != eventId) return Result<string>.Failure(Error.Conflict("Mã đăng ký không thuộc sự kiện này"));
        if (registration.Status == RegistrationStatus.Cancelled) return Result<string>.Failure(Error.Conflict("Đăng ký đã bị hủy"));
        if (registration.CheckedInAt is not null)
            return Result<string>.Failure(Error.Conflict("Đã điểm danh lúc " + VietnamTime.Format(registration.CheckedInAt.Value, "dd/MM/yyyy HH:mm:ss")));
        // Conditional SQL protects against duplicate check-in even if the service is invoked concurrently.
        var affected = await db.Registrations.Where(x => x.Id == registration.Id && x.Status == RegistrationStatus.Confirmed && x.CheckedInAt == null)
            .ExecuteUpdateAsync(s => s.SetProperty(x => x.CheckedInAt, (DateTime?)now).SetProperty(x => x.CheckedInBy, (Guid?)actorId), ct);
        if (affected != 1) return Result<string>.Failure(Error.Conflict("Đăng ký đã được xử lý"));
        var attendee = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == registration.UserId, ct);
        await tx.CommitAsync(ct);
        return Result<string>.Success("Điểm danh thành công: " + (attendee?.Username ?? "") + " - " + (attendee?.FullName ?? ""));
    }

}
