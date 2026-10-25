using EMCA.Application.DTOs;

namespace ECMA.Web.Models;

public sealed class StatusMessageViewModel
{
    public int StatusCode { get; init; }
    public string Message { get; init; } = "";
    public string BackUrl { get; init; } = "/";
    public string BackLabel { get; init; } = "";
}

public sealed class PublicEventSearchViewModel
{
    public string? Keyword { get; init; }
    public string? Type { get; init; }
    public string? From { get; init; }
    public string? To { get; init; }
    public int Page { get; init; } = 1;
    public string? ErrorMessage { get; init; }
    public IReadOnlyList<PublicEventListItemDto> Events { get; init; } = [];
}

public sealed class PublicEventDetailsViewModel
{
    public required PublicEventDetailsDto Event { get; init; }
    public bool IsAuthenticated { get; init; }
}

public sealed class MyRegistrationsViewModel
{
    public required MyRegistrationPageDto Data { get; init; }
}

public sealed class ParticipantsViewModel
{
    public required ParticipantPageDto Data { get; init; }
    public string? Keyword { get; init; }
    public string? Status { get; init; }
    public string? CheckIn { get; init; }
}

public sealed class ManagerCancelViewModel
{
    public Guid EventId { get; set; }
    public Guid RegistrationId { get; set; }
    public string EventTitle { get; set; } = "";
    public string Username { get; set; } = "";
    public string FullName { get; set; } = "";
    public string Code { get; set; } = "";

    // Validated by RegistrationService after the §4.5 eligibility checks, not by DataAnnotations.
    public string? Reason { get; set; }
}
