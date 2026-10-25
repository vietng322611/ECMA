using ECMA.Web.Models;
using ECMA.Web.Services;
using EMCA.Application.DTOs;
using EMCA.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECMA.Web.Controllers;

[Authorize, ValidateGuidRouteValues]
[Route("events/manage/{eventId}/participants")]
public sealed class ParticipantsController(RegistrationService registrations) : Controller
{
    // FR-PART-01/02/04.
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid eventId)
    {
        var request = new ParticipantListRequest(this.RawQuery("keyword"), this.RawQuery("status"), this.RawQuery("checkIn"), this.RawQuery("page"));
        var result = await registrations.ListParticipantsAsync(this.CurrentUserId()!.Value, eventId, request, HttpContext.RequestAborted);
        if (!result.IsSuccess)
            return result.Error!.Code == "BadRequest"
                ? this.StatusView(result.Error, Url.Action(nameof(Index), new { eventId }), "Về danh sách người tham dự")
                : this.StatusView(result.Error, "/events/manage", "Về Quản lý sự kiện");
        return View(new ParticipantsViewModel
        {
            Data = result.Value!, Keyword = result.Value!.Keyword, Status = request.Status, CheckIn = request.CheckIn
        });
    }

    // FR-PART-03: confirmation form with reason and the MANAGER re-registration warning.
    [HttpGet("{registrationId}/cancel")]
    public async Task<IActionResult> Cancel(Guid eventId, Guid registrationId)
    {
        var result = await registrations.GetManagerCancelFormAsync(this.CurrentUserId()!.Value, eventId, registrationId, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!, Url.Action(nameof(Index), new { eventId }), "Về danh sách người tham dự");
        var x = result.Value!;
        return View(new ManagerCancelViewModel
        {
            EventId = x.EventId, RegistrationId = x.RegistrationId, EventTitle = x.EventTitle, Username = x.Username, FullName = x.FullName, Code = x.Code
        });
    }

    // SRS §4.5 with source MANAGER; the source is fixed by this action, never posted by the browser.
    [HttpPost("{registrationId}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid eventId, Guid registrationId, [Bind(nameof(ManagerCancelViewModel.Reason))] ManagerCancelViewModel model)
    {
        var result = await registrations.CancelAsManagerAsync(this.CurrentUserId()!.Value, eventId, registrationId, model.Reason, HttpContext.RequestAborted);
        if (result.IsSuccess)
        {
            TempData["SuccessMessage"] = result.Value ? "Đã hủy" : "Hủy đăng ký thành công";
            return RedirectToAction(nameof(Index), new { eventId });
        }

        if (result.Error!.Code != "Reason")
            return this.StatusView(result.Error, Url.Action(nameof(Index), new { eventId }), "Về danh sách người tham dự");

        var form = await registrations.GetManagerCancelFormAsync(this.CurrentUserId()!.Value, eventId, registrationId, HttpContext.RequestAborted);
        if (!form.IsSuccess) return this.StatusView(form.Error!, Url.Action(nameof(Index), new { eventId }), "Về danh sách người tham dự");
        ModelState.AddModelError(nameof(ManagerCancelViewModel.Reason), result.Error.Message);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        var x = form.Value!;
        return View(new ManagerCancelViewModel
        {
            EventId = x.EventId, RegistrationId = x.RegistrationId, EventTitle = x.EventTitle, Username = x.Username, FullName = x.FullName,
            Code = x.Code, Reason = model.Reason
        });
    }
}
