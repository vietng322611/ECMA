using System.Text;
using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Domain.Results;
using EMCA.Application.DTOs;
using EMCA.Application.Interfaces;

namespace EMCA.Application.Services;

public sealed class EventService(IUnitOfWork unitOfWork, IClock clock) : IEventService
{
    public async Task<Result<IReadOnlyList<EventDto>>> ListAsync(EventActor actor, EventListRequest request, CancellationToken cancellationToken = default)
    {
        if (request.Page < 1) return Result<IReadOnlyList<EventDto>>.Failure(Error.BadRequest("Tham số trang không hợp lệ"));
        var keyword = NormalizeKeyword(request.Keyword);
        if (keyword is not null && keyword.EnumerateRunes().Count() > 100 || !TryType(request.Type, out var type) || !TryStatus(request.Status, out var status))
            return Result<IReadOnlyList<EventDto>>.Failure(Error.BadRequest("Bộ lọc không hợp lệ"));
        var user = await unitOfWork.Users.FindByIdAsync(actor.UserId, cancellationToken);
        if (!CanCreate(user)) return Forbidden<IReadOnlyList<EventDto>>();
        var items = await unitOfWork.Events.ListAsync(new(actor.UserId, user!.Role, keyword, type, status, request.Page), clock.UtcNow, cancellationToken);
        return Result<IReadOnlyList<EventDto>>.Success(items.Select(x => ToDto(x, CanManage(user, x))).ToList());
    }

    public async Task<Result<EventDto>> GetAsync(EventActor actor, Guid id, CancellationToken cancellationToken = default)
    {
        var item = await unitOfWork.Events.FindAsync(id, false, cancellationToken);
        var user = await unitOfWork.Users.FindByIdAsync(actor.UserId, cancellationToken);
        return item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, clock.UtcNow, cancellationToken)
            ? Result<EventDto>.Failure(Error.NotFound("Không tìm thấy dữ liệu")) : Result<EventDto>.Success(ToDto(item, CanManage(user!, item)));
    }

    public async Task<Result<EventDto>> CreateAsync(EventActor actor, EventData data, CancellationToken cancellationToken = default)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await unitOfWork.Users.FindByIdForUpdateAsync(actor.UserId, cancellationToken);
        if (!CanCreate(user)) return Forbidden<EventDto>();
        var created = Event.Create(actor.UserId, data.Type, data.Title, data.Description, data.Location, data.StartAt, data.EndAt, data.Capacity, clock.UtcNow);
        if (created.IsFailure) return Result<EventDto>.Failure(created.Error!);
        unitOfWork.Events.Add(created.Value!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<EventDto>.Success(ToDto(created.Value!));
    }

    public Task<Result<EventDto>> EditDraftAsync(EventActor actor, Guid id, EventData data, CancellationToken cancellationToken = default) =>
        WriteAsync(actor, id, e => e.UpdateDraft(data.Type, data.Title, data.Description, data.Location, data.StartAt, data.EndAt, data.Capacity, clock.UtcNow), e => ToDto(e), cancellationToken);

    public Task<Result<EventDto>> EditPublishedAsync(EventActor actor, Guid id, PublishedEventData data, CancellationToken cancellationToken = default) =>
        WriteAsync(actor, id, e => e.UpdatePublished(data.Title, data.Description, data.Location, data.Capacity, clock.UtcNow), e => ToDto(e), cancellationToken);

    public Task<Result> PublishAsync(EventActor actor, Guid id, CancellationToken cancellationToken = default) =>
        WriteResultAsync(actor, id, e => e.Publish(clock.UtcNow), cancellationToken);

    // BR-EVT-04: confirmed registrations are cancelled with source EVENT in the same transaction as the event.
    public async Task<Result> CancelAsync(EventActor actor, Guid id, string reason, CancellationToken cancellationToken = default)
    {
        var result = await WriteAsync(actor, id, e => e.Cancel(reason, actor.UserId, clock.UtcNow), _ => true, cancellationToken,
            e => unitOfWork.Registrations.CancelConfirmedForEventAsync(e.Id, e.CancelledBy!.Value, e.CancelledAt!.Value, e.CancelReason!, cancellationToken));
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }

    public Task<Result> DeleteAsync(EventActor actor, Guid id, CancellationToken cancellationToken = default) =>
        WriteResultAsync(actor, id, e => e.Delete(), cancellationToken);

    public Task<Result<AgendaDto>> AddAgendaAsync(EventActor actor, Guid eventId, AgendaData data, CancellationToken cancellationToken = default) =>
        WriteValueAsync(actor, eventId, e => e.AddAgenda(data.Title, data.Content, data.StartAt, data.EndAt), x => ToDto(x), cancellationToken);

    public async Task<Result> EditAgendaAsync(EventActor actor, Guid eventId, Guid itemId, AgendaData data, CancellationToken cancellationToken = default)
    {
        var result = await WriteAsync(actor, eventId, e => e.UpdateAgenda(itemId, data.Title, data.Content, data.StartAt, data.EndAt), _ => true, cancellationToken);
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }

    public async Task<Result> DeleteAgendaAsync(EventActor actor, Guid eventId, Guid itemId, CancellationToken cancellationToken = default)
    {
        var result = await WriteAsync(actor, eventId, e => e.DeleteAgenda(itemId), _ => true, cancellationToken);
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }

    private async Task<Result> WriteResultAsync(EventActor actor, Guid id, Func<Event, Result> change, CancellationToken cancellationToken)
    {
        var result = await WriteAsync(actor, id, change, _ => true, cancellationToken);
        return result.IsFailure ? Result.Failure(result.Error!) : Result.Success();
    }

    private async Task<Result<T>> WriteAsync<T>(EventActor actor, Guid id, Func<Event, Result> change, Func<Event, T> value, CancellationToken cancellationToken,
        Func<Event, Task>? afterChange = null)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await unitOfWork.Users.FindByIdForUpdateAsync(actor.UserId, cancellationToken);
        var item = await unitOfWork.Events.FindAsync(id, true, cancellationToken);
        if (item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, clock.UtcNow, cancellationToken)) return Result<T>.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
        if (!CanManage(user!, item)) return Forbidden<T>();
        var result = change(item);
        if (result.IsFailure) return Result<T>.Failure(result.Error!);
        if (afterChange is not null) await afterChange(item);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<T>.Success(value(item));
    }

    private async Task<Result<TOutput>> WriteValueAsync<TValue, TOutput>(EventActor actor, Guid id, Func<Event, Result<TValue>> change, Func<TValue, TOutput> value, CancellationToken cancellationToken)
    {
        await using var transaction = await unitOfWork.BeginTransactionAsync(cancellationToken);
        var user = await unitOfWork.Users.FindByIdForUpdateAsync(actor.UserId, cancellationToken);
        var item = await unitOfWork.Events.FindAsync(id, true, cancellationToken);
        if (item is null || !await EventAccessPolicy.CanViewAsync(unitOfWork, user, item, clock.UtcNow, cancellationToken)) return Result<TOutput>.Failure(Error.NotFound("Không tìm thấy dữ liệu"));
        if (!CanManage(user!, item)) return Forbidden<TOutput>();
        var result = change(item);
        if (result.IsFailure) return Result<TOutput>.Failure(result.Error!);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Result<TOutput>.Success(value(result.Value!));
    }

    private static bool CanCreate(User? user) => user is { Status: UserStatus.Active, Role: UserRole.Organizer or UserRole.Admin };
    private static bool CanManage(User user, Event item) => EventAccessPolicy.CanManage(user, item);
    private static Result<T> Forbidden<T>() => Result<T>.Failure(Error.Forbidden("Bạn không có quyền thực hiện thao tác này"));
    private static string? NormalizeKeyword(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Normalize(NormalizationForm.FormC).Trim();
    private static bool TryType(string? value, out EventType? result) { result = value switch { "Event" => EventType.Event, "Conference" => EventType.Conference, _ => null }; return value is null or "All" or "Event" or "Conference"; }
    private static bool TryStatus(string? value, out EffectiveEventStatus? result) { result = value switch { "Draft" => EffectiveEventStatus.Draft, "Published" => EffectiveEventStatus.Published, "Cancelled" => EffectiveEventStatus.Cancelled, "Completed" => EffectiveEventStatus.Completed, _ => null }; return value is null or "All" or "Draft" or "Published" or "Cancelled" or "Completed"; }
    private EventDto ToDto(Event e, bool canManage = true) => new(e.Id, e.OwnerId, e.Type, e.Title, e.Description, e.Location, e.StartAt, e.EndAt, e.Capacity,
        e.EffectiveStatus(clock.UtcNow), e.CreatedAt, e.CancelReason, e.AgendaItems.OrderBy(x => x.StartAt).Select(ToDto).ToList(), canManage);
    private static AgendaDto ToDto(AgendaItem x) => new(x.Id, x.Title, x.Content, x.StartAt, x.EndAt);
}
