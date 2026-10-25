using ECMA.Domain.Entities;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;

namespace EMCA.Application.Services;

// BR-SRCH-04 / UC-EVT-08 visibility and the management column of the SRS §3.9 permission matrix.
public static class EventAccessPolicy
{
    public static async Task<bool> CanViewAsync(IUnitOfWork unitOfWork, User? user, Event item, DateTime now, CancellationToken cancellationToken)
    {
        if (item.IsDeleted) return false;
        var status = item.EffectiveStatus(now);
        if (status == EffectiveEventStatus.Published) return true;
        if (user is not { Status: UserStatus.Active }) return false;
        if (user.Role == UserRole.Admin) return true;
        if (user.Role == UserRole.Organizer && item.OwnerId == user.Id) return true;
        return status != EffectiveEventStatus.Draft &&
               await unitOfWork.Registrations.ExistsForEventAndUserAsync(item.Id, user.Id, cancellationToken);
    }

    public static bool CanManage(User? user, Event item) => user is { Status: UserStatus.Active } &&
        (user.Role == UserRole.Admin || user.Role == UserRole.Organizer && item.OwnerId == user.Id);
}
