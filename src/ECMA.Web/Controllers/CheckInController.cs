using ECMA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECMA.Web.Controllers;

[Authorize, ValidateGuidRouteValues]
[Route("events/manage/{eventId}/check-in")]
public sealed class CheckInController(CheckInService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(Guid eventId)
    {
        var result = await service.CanOpenAsync(this.CurrentUserId() ?? Guid.Empty, eventId, HttpContext.RequestAborted);
        return result.IsSuccess ? View(new CheckInForm { EventId = eventId }) : this.StatusView(result.Error!);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Index(Guid eventId, [Bind(nameof(CheckInForm.Code))] CheckInForm form)
    {
        var result = await service.CheckAsync(this.CurrentUserId() ?? Guid.Empty, eventId, form.Code, HttpContext.RequestAborted);
        if (!result.IsSuccess)
        {
            if (result.Error!.Code is "NotFound" or "Forbidden")
                return this.StatusView(result.Error, "/events/manage", "Quản lý sự kiện");
            Response.StatusCode = result.Error.Code == "Conflict" ? 409 : 400;
            ModelState.AddModelError(nameof(form.Code), result.Error.Message);
            return View(new CheckInForm { EventId = eventId, Code = form.Code });
        }
        TempData["SuccessMessage"] = result.Value;
        return RedirectToAction(nameof(Index), new { eventId });
    }
}

public sealed class CheckInForm
{
    public Guid EventId { get; set; }
    public string? Code { get; set; }
}
