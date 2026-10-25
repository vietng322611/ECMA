using ECMA.Domain.Entities;
using ECMA.Domain.Enums;

namespace ECMA.Domain.Interfaces;

public sealed record ParticipantQuery(Guid EventId, string? Keyword, RegistrationStatus? Status, bool? CheckedIn, int Page);

public sealed record ParticipantRow(Registration Registration, string Username, string FullName, string? CheckedInByUsername, string? CancelledByUsername);

public sealed record MyRegistrationRow(Registration Registration, Event Event);

public sealed record RegistrationCounts(int Confirmed, int Cancelled, int CheckedIn);

public interface IRegistrationRepository
{
    Task<Registration?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task<Registration?> FindByEventAndUserAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> ExistsForEventAndUserAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<int> CountConfirmedAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<RegistrationCounts> CountAsync(Guid eventId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MyRegistrationRow>> ListForUserAsync(Guid userId, int page, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ParticipantRow>> ListParticipantsAsync(ParticipantQuery query, CancellationToken cancellationToken = default);
    Task CancelConfirmedForEventAsync(Guid eventId, Guid actorId, DateTime now, string reason, CancellationToken cancellationToken = default);
    void Add(Registration registration);
}
