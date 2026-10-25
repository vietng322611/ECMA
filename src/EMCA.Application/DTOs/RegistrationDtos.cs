using ECMA.Domain.Enums;

namespace EMCA.Application.DTOs;

public enum Availability { Open, Full, Closed }

public sealed record PublicEventSearchRequest(string? Keyword = null, string? Type = null, string? From = null, string? To = null, string? Page = null);
public sealed record PublicEventListItemDto(Guid Id, EventType Type, string Title, string Location, DateTime StartAt, DateTime EndAt,
    int Capacity, int Remaining, Availability Availability);
public sealed record PublicEventSearchDto(string? Keyword, EventType? Type, DateOnly? From, DateOnly? To, int Page, IReadOnlyList<PublicEventListItemDto> Items);

public sealed record MyEventRegistrationDto(Guid Id, RegistrationStatus Status, CancelSource? CancelSource);
public sealed record PublicEventDetailsDto(Guid Id, EventType Type, string Title, string Description, string Location, DateTime StartAt, DateTime EndAt,
    string OwnerName, int Capacity, int Remaining, EffectiveEventStatus Status, Availability Availability, string? CancelReason,
    IReadOnlyList<AgendaDto> Agenda, bool IsOwner, bool CanManage, MyEventRegistrationDto? MyRegistration);

public sealed record RegisterOutcome(Guid RegistrationId, string Code, bool AlreadyRegistered);

public sealed record MyRegistrationDto(Guid Id, Guid EventId, EventType EventType, string EventTitle, string Location, DateTime StartAt, DateTime EndAt,
    EffectiveEventStatus EventStatus, string Code, RegistrationStatus Status, DateTime RegisteredAt, CancelSource? CancelSource,
    DateTime? CancelledAt, string? CancelReason, DateTime? CheckedInAt, bool CanCancel);
public sealed record MyRegistrationPageDto(int Page, IReadOnlyList<MyRegistrationDto> Items);

public sealed record ParticipantListRequest(string? Keyword = null, string? Status = null, string? CheckIn = null, string? Page = null);
public sealed record ParticipantStatsDto(int Capacity, int Confirmed, int Cancelled, int Remaining, int CheckedIn, int NotCheckedIn,
    decimal AttendanceRate, int? Absent);
public sealed record ParticipantDto(Guid Id, string Username, string FullName, string Code, RegistrationStatus Status, DateTime RegisteredAt,
    DateTime? CheckedInAt, string? CheckedInBy, CancelSource? CancelSource, DateTime? CancelledAt, string? CancelReason, string? CancelledBy);
public sealed record ParticipantPageDto(Guid EventId, string EventTitle, EffectiveEventStatus EventStatus, DateTime StartAt, DateTime EndAt,
    ParticipantStatsDto Stats, string? Keyword, RegistrationStatus? Status, bool? CheckedIn, int Page, bool CanCancel, IReadOnlyList<ParticipantDto> Items);
public sealed record ManagerCancelFormDto(Guid EventId, string EventTitle, Guid RegistrationId, string Username, string FullName, string Code);
