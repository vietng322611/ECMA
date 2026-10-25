using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Domain.Results;
using EMCA.Application.DTOs;

namespace EMCA.Application.Services;

// FR-SRCH-01..03: public search and event details, available to Guests and every role.
public sealed class PublicEventService(IUnitOfWork unitOfWork, IClock clock)
{
    public const int PageSize = 20;
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private static readonly IReadOnlyDictionary<string, EventType> Types = new Dictionary<string, EventType>(StringComparer.Ordinal)
    {
        ["Event"] = EventType.Event, ["Conference"] = EventType.Conference
    };

    public async Task<Result<PublicEventSearchDto>> SearchAsync(PublicEventSearchRequest request, CancellationToken cancellationToken = default)
    {
        if (!QueryParameters.TryPage(request.Page, out var page)) return BadRequest<PublicEventSearchDto>("Tham số trang không hợp lệ");
        if (!QueryParameters.TryKeyword(request.Keyword, out var keyword)) return BadRequest<PublicEventSearchDto>("Từ khóa tối đa 100 ký tự");
        if (!QueryParameters.TryChoice(request.Type, Types, out var type)) return BadRequest<PublicEventSearchDto>("Bộ lọc không hợp lệ");
        if (!QueryParameters.TryDate(request.From, out var from) || !QueryParameters.TryDate(request.To, out var to))
            return BadRequest<PublicEventSearchDto>("Ngày không hợp lệ");
        if (from > to) return BadRequest<PublicEventSearchDto>("Từ ngày phải nhỏ hơn hoặc bằng đến ngày");

        // BR-SRCH-02: start date in UTC+7, both ends inclusive.
        var startFrom = from is null ? (DateTime?)null : DateTime.SpecifyKind(from.Value.ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);
        var startBefore = to is null ? (DateTime?)null : DateTime.SpecifyKind(to.Value.AddDays(1).ToDateTime(TimeOnly.MinValue) - VietnamOffset, DateTimeKind.Utc);
        var now = clock.UtcNow;
        var rows = await unitOfWork.Events.SearchPublicAsync(new(keyword, type, startFrom, startBefore, page), now, cancellationToken);
        var items = rows.Select(x => new PublicEventListItemDto(x.Event.Id, x.Event.Type, x.Event.Title, x.Event.Location, x.Event.StartAt, x.Event.EndAt,
            x.Event.Capacity, Math.Max(0, x.Event.Capacity - x.Confirmed), AvailabilityOf(x.Event, x.Confirmed, now))).ToList();
        return Result<PublicEventSearchDto>.Success(new(keyword, type, from, to, page, items));
    }

    public async Task<Result<PublicEventDetailsDto>> GetAsync(Guid? userId, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Events.FindAsync(id, false, cancellationToken);
        var user = userId is null ? null : await unitOfWork.Users.FindByIdAsync(userId.Value, cancellationToken);
        var now = clock.UtcNow;
        if (item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, now, cancellationToken))
            return Result<PublicEventDetailsDto>.Failure(Error.NotFound("Không tìm thấy dữ liệu"));

        var owner = await unitOfWork.Users.FindByIdAsync(item.OwnerId, cancellationToken);
        var confirmed = await unitOfWork.Registrations.CountConfirmedAsync(item.Id, cancellationToken);
        var mine = user is null ? null : await unitOfWork.Registrations.FindByEventAndUserAsync(item.Id, user.Id, cancellationToken);
        return Result<PublicEventDetailsDto>.Success(new(item.Id, item.Type, item.Title, item.Description, item.Location, item.StartAt, item.EndAt,
            owner?.FullName ?? "", item.Capacity, Math.Max(0, item.Capacity - confirmed), item.EffectiveStatus(now), AvailabilityOf(item, confirmed, now),
            item.CancelReason, item.AgendaItems.OrderBy(x => x.StartAt).ThenBy(x => x.Id).Select(x => new AgendaDto(x.Id, x.Title, x.Content, x.StartAt, x.EndAt)).ToList(),
            user is not null && item.OwnerId == user.Id, EventAccessPolicy.CanManage(user, item),
            mine is null ? null : new(mine.Id, mine.Status, mine.CancelSource)));
    }

    // BR-SRCH-03: closed from start_at regardless of remaining seats; otherwise full when no seat remains.
    public static Availability AvailabilityOf(Event item, int confirmed, DateTime now) =>
        item.EffectiveStatus(now) != EffectiveEventStatus.Published || now >= item.StartAt ? Availability.Closed
        : confirmed >= item.Capacity ? Availability.Full : Availability.Open;

    private static Result<T> BadRequest<T>(string message) => Result<T>.Failure(Error.BadRequest(message));
}
