using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace ECMA.Infrastructure.Repositories;

public sealed class RegistrationRepository(EcmaDbContext context) : IRegistrationRepository
{
    private const int PageSize = 20;

    public Task<Registration?> FindAsync(Guid id, CancellationToken cancellationToken = default) =>
        context.Registrations.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);

    public Task<Registration?> FindByEventAndUserAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default) =>
        context.Registrations.SingleOrDefaultAsync(x => x.EventId == eventId && x.UserId == userId, cancellationToken);

    public Task<bool> ExistsForEventAndUserAsync(Guid eventId, Guid userId, CancellationToken cancellationToken = default) =>
        context.Registrations.AnyAsync(x => x.EventId == eventId && x.UserId == userId, cancellationToken);

    public Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default) =>
        context.Registrations.AnyAsync(x => x.Code == code, cancellationToken);

    public Task<int> CountConfirmedAsync(Guid eventId, CancellationToken cancellationToken = default) =>
        context.Registrations.CountAsync(x => x.EventId == eventId && x.Status == RegistrationStatus.Confirmed, cancellationToken);

    public async Task<RegistrationCounts> CountAsync(Guid eventId, CancellationToken cancellationToken = default)
    {
        var counts = await context.Registrations.Where(x => x.EventId == eventId).GroupBy(_ => 1).Select(g => new
        {
            Confirmed = g.Count(x => x.Status == RegistrationStatus.Confirmed),
            Cancelled = g.Count(x => x.Status == RegistrationStatus.Cancelled),
            CheckedIn = g.Count(x => x.Status == RegistrationStatus.Confirmed && x.CheckedInAt != null)
        }).SingleOrDefaultAsync(cancellationToken);
        return counts is null ? new(0, 0, 0) : new(counts.Confirmed, counts.Cancelled, counts.CheckedIn);
    }

    public async Task<IReadOnlyList<MyRegistrationRow>> ListForUserAsync(Guid userId, int page, CancellationToken cancellationToken = default)
    {
        var rows = await context.Registrations.AsNoTracking().Where(x => x.UserId == userId)
            .Join(context.Events.AsNoTracking(), r => r.EventId, e => e.Id, (r, e) => new { r, e })
            .OrderByDescending(x => x.r.RegisteredAt).ThenBy(x => x.r.Id)
            .Skip((page - 1) * PageSize).Take(PageSize).ToListAsync(cancellationToken);
        return rows.Select(x => new MyRegistrationRow(x.r, x.e)).ToList();
    }

    public async Task<IReadOnlyList<ParticipantRow>> ListParticipantsAsync(ParticipantQuery query, CancellationToken cancellationToken = default)
    {
        var rows = context.Registrations.AsNoTracking().Where(x => x.EventId == query.EventId)
            .Join(context.Users.AsNoTracking(), r => r.UserId, u => u.Id, (r, u) => new { r, u.Username, u.FullName });
        if (query.Status is not null) rows = rows.Where(x => x.r.Status == query.Status);
        if (query.CheckedIn is true) rows = rows.Where(x => x.r.CheckedInAt != null);
        if (query.CheckedIn is false) rows = rows.Where(x => x.r.CheckedInAt == null);
        if (query.Keyword is not null)
        {
            // Substring match without user wildcards: Contains is translated to strpos, not LIKE.
            var keyword = query.Keyword.ToLowerInvariant();
            rows = rows.Where(x => x.Username.ToLower().Contains(keyword) || x.FullName.ToLower().Contains(keyword) || x.r.Code.ToLower().Contains(keyword));
        }

        var page = await rows.OrderBy(x => x.r.RegisteredAt).ThenBy(x => x.r.Id)
            .Skip((query.Page - 1) * PageSize).Take(PageSize)
            .Select(x => new
            {
                x.r, x.Username, x.FullName,
                CheckedInBy = context.Users.Where(u => u.Id == x.r.CheckedInBy).Select(u => u.Username).FirstOrDefault(),
                CancelledBy = context.Users.Where(u => u.Id == x.r.CancelledBy).Select(u => u.Username).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return page.Select(x => new ParticipantRow(x.r, x.Username, x.FullName, x.CheckedInBy, x.CancelledBy)).ToList();
    }

    public Task CancelConfirmedForEventAsync(Guid eventId, Guid actorId, DateTime now, string reason, CancellationToken cancellationToken = default) =>
        context.Registrations.Where(x => x.EventId == eventId && x.Status == RegistrationStatus.Confirmed)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.Status, RegistrationStatus.Cancelled)
                .SetProperty(x => x.CancelSource, (CancelSource?)CancelSource.Event)
                .SetProperty(x => x.CancelledAt, (DateTime?)now)
                .SetProperty(x => x.CancelledBy, (Guid?)actorId)
                .SetProperty(x => x.CancelReason, reason), cancellationToken);

    public void Add(Registration registration) => context.Registrations.Add(registration);
}
