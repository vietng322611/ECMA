using ECMA.Web.Models;
using ECMA.Web.Services;
using EMCA.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECMA.Web.Controllers;

[ValidateGuidRouteValues]
public sealed class PublicEventsController(PublicEventService publicEvents, RegistrationService registrations) : Controller
{
    // FR-SRCH-02/03, BR-SRCH-04: hidden events return 404 even for Guests, without a login redirect.
    [AllowAnonymous, HttpGet("events/{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var result = await publicEvents.GetAsync(this.CurrentUserId(), id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!);
        return View(new PublicEventDetailsViewModel { Event = result.Value!, IsAuthenticated = User.Identity?.IsAuthenticated == true });
    }

    // FR-REG-01/04, SRS §4.3. The attendee is always the signed-in user.
    [Authorize, HttpPost("events/{id}/register"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Register(Guid id)
    {
        var result = await registrations.RegisterAsync(this.CurrentUserId()!.Value, id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!, Url.Action(nameof(Details), new { id }), "Quay lại chi tiết sự kiện");
        TempData["SuccessMessage"] = result.Value!.AlreadyRegistered ? "Bạn đã đăng ký sự kiện này" : "Đăng ký thành công";
        return RedirectToAction(nameof(Details), new { id });
    }

    // FR-REG-02.
    [Authorize, HttpGet("my-registrations")]
    public async Task<IActionResult> MyRegistrations()
    {
        var result = await registrations.ListMineAsync(this.CurrentUserId()!.Value, this.RawQuery("page"), HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!, Url.Action(nameof(MyRegistrations)), "Về Đăng ký của tôi");
        return View(new MyRegistrationsViewModel { Data = result.Value! });
    }

    // FR-REG-03, SRS §4.5 with source SELF.
    [Authorize, HttpPost("events/{eventId}/registrations/{registrationId}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelRegistration(Guid eventId, Guid registrationId)
    {
        var result = await registrations.CancelOwnAsync(this.CurrentUserId()!.Value, eventId, registrationId, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!, Url.Action(nameof(MyRegistrations)), "Về Đăng ký của tôi");
        TempData["SuccessMessage"] = result.Value ? "Đã hủy" : "Hủy đăng ký thành công";
        return RedirectToAction(nameof(MyRegistrations));
    }
}
