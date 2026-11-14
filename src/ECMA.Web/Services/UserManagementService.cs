using ECMA.Domain.Enums;
using ECMA.Domain.Results;
using ECMA.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECMA.Web.Services;

public sealed record UserListRow(Guid Id, string Username, string FullName, UserRole Role, UserStatus Status, DateTime CreatedAt);

// FR-USER-01..03. Row locks synchronize with EventService.CreateAsync's owner lock.
public sealed class UserManagementService(EcmaDbContext db, ECMA.Domain.Interfaces.IClock clock, ECMA.Domain.Interfaces.ISecurityStampGenerator stamps)
{
    public async Task<Result<IReadOnlyList<UserListRow>>> ListAsync(Guid actorId, string? keyword, string? role, string? status, int page, CancellationToken ct = default)
    {
        if (!await IsAdminAsync(actorId, ct)) return Result<IReadOnlyList<UserListRow>>.Failure(Error.Forbidden("Bạn không có quyền thực hiện thao tác này"));
        keyword = keyword?.Normalize(System.Text.NormalizationForm.FormC).Trim();
        if ((keyword?.Length ?? 0) > 100 || page < 1 || page > 100000 ||
            (!string.IsNullOrEmpty(role) && role != "All" && !Enum.GetNames<UserRole>().Contains(role, StringComparer.Ordinal)) ||
            (!string.IsNullOrEmpty(status) && status != "All" && !Enum.GetNames<UserStatus>().Contains(status, StringComparer.Ordinal)))
            return Result<IReadOnlyList<UserListRow>>.Failure(Error.BadRequest("Bộ lọc không hợp lệ"));
        var query = db.Users.AsNoTracking().AsQueryable();
        if (!string.IsNullOrEmpty(keyword))
        {
            var search = keyword.ToLowerInvariant();
            query = query.Where(x => x.Username.ToLower().Contains(search) || x.FullName.ToLower().Contains(search));
        }
        if (Enum.TryParse<UserRole>(role, false, out var parsedRole) && role != "All") query = query.Where(x => x.Role == parsedRole);
        if (Enum.TryParse<UserStatus>(status, false, out var parsedStatus) && status != "All") query = query.Where(x => x.Status == parsedStatus);
        var rows = await query.OrderByDescending(x => x.CreatedAt).ThenBy(x => x.Id).Skip((page - 1) * 20).Take(20)
            .Select(x => new UserListRow(x.Id, x.Username, x.FullName, x.Role, x.Status, x.CreatedAt)).ToListAsync(ct);
        return Result<IReadOnlyList<UserListRow>>.Success(rows);
    }

    public async Task<Result> ChangeAsync(Guid actorId, Guid targetId, UserRole role, UserStatus status, CancellationToken ct = default)
    {
        if (!Enum.IsDefined(role) || !Enum.IsDefined(status)) return Result.Failure(Error.BadRequest("Dữ liệu không hợp lệ"));
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // Serializes all Admin changes to uphold the last-active-admin invariant.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(91735122)", ct);
        var actor = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == actorId, ct);
        if (actor is not { Role: UserRole.Admin, Status: UserStatus.Active })
            return Result.Failure(Error.Forbidden("Bạn không có quyền thực hiện thao tác này"));
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT id FROM users WHERE id = {targetId} FOR UPDATE", ct);
        var target = await db.Users.SingleOrDefaultAsync(x => x.Id == targetId, ct);
        if (target is null) return Result.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
        if (target.Role == role && target.Status == status) return Result.Success();
        if (target.Role == UserRole.Admin && target.Status == UserStatus.Active && (role != UserRole.Admin || status != UserStatus.Active))
        {
            var admins = await db.Users.CountAsync(x => x.Role == UserRole.Admin && x.Status == UserStatus.Active, ct);
            if (admins <= 1) return Result.Failure(Error.Conflict("Phải còn ít nhất một Admin đang hoạt động"));
        }
        if ((target.Role is UserRole.Admin or UserRole.Organizer) && (role == UserRole.Attendee || status == UserStatus.Inactive))
        {
            var hasPendingEvents = await db.Events.AnyAsync(x => x.OwnerId == targetId && !x.IsDeleted &&
                (x.Status == EventStatus.Draft || x.Status == EventStatus.Published && x.EndAt > clock.UtcNow), ct);
            if (hasPendingEvents) return Result.Failure(Error.Conflict("Chủ sở hữu còn sự kiện chưa kết thúc hoặc bản nháp"));
        }
        // Stamp renewal invalidates sessions on privilege or status changes.
        target.SetRole(role);
        target.SetStatus(status);
        target.RotateSecurityStamp(stamps.GenerateStamp());
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
        return Result.Success();
    }

    private Task<bool> IsAdminAsync(Guid actorId, CancellationToken ct) =>
        db.Users.AsNoTracking().AnyAsync(x => x.Id == actorId && x.Role == UserRole.Admin && x.Status == UserStatus.Active, ct);
}
