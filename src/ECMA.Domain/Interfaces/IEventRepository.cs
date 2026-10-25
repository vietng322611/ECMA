using ECMA.Domain.Entities;
using ECMA.Domain.Enums;

namespace ECMA.Domain.Interfaces;

public sealed record EventQuery(Guid ActorId, UserRole Role, string? Keyword, EventType? Type, EffectiveEventStatus? Status, int Page = 1);
public sealed record PublicEventQuery(string? Keyword, EventType? Type, DateTime? StartFrom, DateTime? StartBefore, int Page);
public sealed record PublicEventRow(Event Event, int Confirmed);

public interface IEventRepository
{
    Task<Event?> FindAsync(Guid id, bool forUpdate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Event>> ListAsync(EventQuery query, DateTime now, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PublicEventRow>> SearchPublicAsync(PublicEventQuery query, DateTime now, CancellationToken cancellationToken = default);
    void Add(Event item);
}
