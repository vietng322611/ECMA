using System.Security.Claims;
using ECMA.Domain.Enums;
using ECMA.Domain.Interfaces;
using ECMA.Domain.Results;
using ECMA.Web.Models;
using ECMA.Web.Services;
using EMCA.Application.DTOs;
using EMCA.Application.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECMA.Web.Controllers;

[Authorize]
[Route("events/manage")]
public sealed class EventsController(IEventService events, IClock clock, VietnamLocationCatalog locations) : Controller
{
    private static readonly TimeZoneInfo VietnamTimeZone = FindVietnamTimeZone();
    public override async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters.Where(x => x.ParameterType == typeof(Guid)))
        {
            var value = context.RouteData.Values[parameter.Name]?.ToString();
            if (value is not null && !Guid.TryParse(value, out _))
            {
                context.Result = BadRequest("Dữ liệu không hợp lệ");
                return;
            }
        }

        await next();
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword = null, string? type = null, string? status = null, int page = 1)
    {
        if (HasRepeatedQueryValue("page")) return BadRequest("Tham số trang không hợp lệ");
        if (HasRepeatedQueryValue("type") || HasRepeatedQueryValue("status")) return BadRequest("Bộ lọc không hợp lệ");
        if (HasRepeatedQueryValue("keyword")) return BadRequest("Từ khóa không hợp lệ");

        var result = await events.ListAsync(Actor(), new(keyword, type, status, page), HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        return View(new EventManageListViewModel
        {
            Keyword = keyword,
            Type = type,
            Status = status,
            Page = page,
            Events = result.Value!.Select(x => new EventListItemViewModel
            {
                Id = x.Id, Type = x.Type, Title = x.Title, Location = x.Location, StartAt = ToVietnamTime(x.StartAt),
                EndAt = ToVietnamTime(x.EndAt), Capacity = x.Capacity, Status = x.Status, CreatedAt = ToVietnamTime(x.CreatedAt)
            }).ToList()
        });
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Details(Guid id)
    {
        var result = await events.GetAsync(Actor(), id, HttpContext.RequestAborted);
        return result.IsSuccess ? View(ToDetails(result.Value!)) : ErrorResult(result.Error!);
    }

    [HttpGet("create")]
    public async Task<IActionResult> Create()
    {
        var result = await events.ListAsync(Actor(), new EventListRequest(), HttpContext.RequestAborted);
        return result.IsSuccess ? View(PopulateLocations(new EventFormViewModel())) : ErrorResult(result.Error!);
    }

    [HttpPost("create"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(EventFormViewModel model)
    {
        if (!TryEventData(model, out var data)) return BadRequestView(nameof(Create), model);
        var result = await events.CreateAsync(Actor(), data!, HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(Create), model, result.Error!);
        TempData["SuccessMessage"] = "Tạo sự kiện thành công";
        return RedirectToAction(nameof(Details), new { id = result.Value!.Id });
    }

    [HttpGet("{id}/edit-draft")]
    public async Task<IActionResult> EditDraft(Guid id)
    {
        var result = await events.GetAsync(Actor(), id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        return result.Value!.Status == EffectiveEventStatus.Draft
            ? View(ToEventForm(result.Value))
            : Conflict("Sự kiện không ở trạng thái bản nháp");
    }

    [HttpPost("{id}/edit-draft"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditDraft(Guid id, EventFormViewModel model)
    {
        model.Id = id;
        if (!TryEventData(model, out var data)) return BadRequestView(nameof(EditDraft), model);
        var result = await events.EditDraftAsync(Actor(), id, data!, HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(EditDraft), model, result.Error!);
        TempData["SuccessMessage"] = "Cập nhật bản nháp thành công";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet("{id}/edit-published")]
    public async Task<IActionResult> EditPublished(Guid id)
    {
        var result = await events.GetAsync(Actor(), id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        if (!result.Value!.CanManage) return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền thực hiện thao tác này");
        return CanChangePublished(result.Value)
            ? View(ToPublishedForm(result.Value))
            : Conflict("Sự kiện không còn cho phép chỉnh sửa");
    }

    [HttpPost("{id}/edit-published"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditPublished(Guid id, PublishedEventFormViewModel model)
    {
        model.Id = id;
        if (!TryLocation(model.StreetAddress, model.ProvinceCode, model.WardCode, out var location))
            return BadRequestView(nameof(EditPublished), PopulateLocations(model));
        var result = await events.EditPublishedAsync(Actor(), id, new(model.Title, model.Description, location, model.Capacity), HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(EditPublished), PopulateLocations(model), result.Error!);
        TempData["SuccessMessage"] = "Cập nhật sự kiện thành công";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/publish"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Publish(Guid id) => await Command(events.PublishAsync(Actor(), id, HttpContext.RequestAborted), id, "Công bố sự kiện thành công");

    [HttpGet("{id}/cancel")]
    public async Task<IActionResult> Cancel(Guid id)
    {
        var result = await events.GetAsync(Actor(), id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        if (!result.Value!.CanManage) return StatusCode(StatusCodes.Status403Forbidden, "Bạn không có quyền thực hiện thao tác này");
        return CanChangePublished(result.Value)
            ? View(new CancelEventViewModel { EventId = id })
            : Conflict("Không thể hủy sự kiện đã bắt đầu");
    }

    [HttpPost("{id}/cancel"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(Guid id, CancelEventViewModel model)
    {
        model.EventId = id;
        if (!ModelState.IsValid) return BadRequestView(nameof(Cancel), model);
        var result = await events.CancelAsync(Actor(), id, model.Reason, HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(Cancel), model, result.Error!);
        TempData["SuccessMessage"] = "Hủy sự kiện thành công";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost("{id}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(Guid id)
    {
        var result = await events.DeleteAsync(Actor(), id, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        TempData["SuccessMessage"] = "Xóa bản nháp thành công";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("{eventId}/agenda/add")]
    public async Task<IActionResult> AddAgenda(Guid eventId)
    {
        var result = await events.GetAsync(Actor(), eventId, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        return result.Value!.Status == EffectiveEventStatus.Draft
            ? View(new AgendaFormViewModel { EventId = eventId })
            : Conflict("Chương trình đã bị khóa");
    }

    [HttpPost("{eventId}/agenda/add"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAgenda(Guid eventId, AgendaFormViewModel model)
    {
        model.EventId = eventId;
        if (!TryAgendaData(model, out var data)) return BadRequestView(nameof(AddAgenda), model);
        var result = await events.AddAgendaAsync(Actor(), eventId, data!, HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(AddAgenda), model, result.Error!);
        TempData["SuccessMessage"] = "Thêm mục chương trình thành công";
        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    [HttpGet("{eventId}/agenda/{itemId}/edit")]
    public async Task<IActionResult> EditAgenda(Guid eventId, Guid itemId)
    {
        var result = await events.GetAsync(Actor(), eventId, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        if (result.Value!.Status != EffectiveEventStatus.Draft) return Conflict("Chương trình đã bị khóa");
        var item = result.Value.Agenda.SingleOrDefault(x => x.Id == itemId);
        return item is null ? NotFound("Không tìm thấy dữ liệu") : View(new AgendaFormViewModel
        {
            EventId = eventId, ItemId = itemId, Title = item.Title, Content = item.Content,
            StartAt = ToVietnamTime(item.StartAt), EndAt = ToVietnamTime(item.EndAt)
        });
    }

    [HttpPost("{eventId}/agenda/{itemId}/edit"), ValidateAntiForgeryToken]
    public async Task<IActionResult> EditAgenda(Guid eventId, Guid itemId, AgendaFormViewModel model)
    {
        model.EventId = eventId;
        model.ItemId = itemId;
        if (!TryAgendaData(model, out var data)) return BadRequestView(nameof(EditAgenda), model);
        var result = await events.EditAgendaAsync(Actor(), eventId, itemId, data!, HttpContext.RequestAborted);
        if (!result.IsSuccess) return FormError(nameof(EditAgenda), model, result.Error!);
        TempData["SuccessMessage"] = "Cập nhật mục chương trình thành công";
        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    [HttpPost("{eventId}/agenda/{itemId}/delete"), ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteAgenda(Guid eventId, Guid itemId)
    {
        var result = await events.DeleteAgendaAsync(Actor(), eventId, itemId, HttpContext.RequestAborted);
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        TempData["SuccessMessage"] = "Xóa mục chương trình thành công";
        return RedirectToAction(nameof(Details), new { id = eventId });
    }

    private async Task<IActionResult> Command(Task<Result> operation, Guid id, string message)
    {
        var result = await operation;
        if (!result.IsSuccess) return ErrorResult(result.Error!);
        TempData["SuccessMessage"] = message;
        return RedirectToAction(nameof(Details), new { id });
    }

    private bool HasRepeatedQueryValue(string key) => Request.Query.ContainsKey(key) && Request.Query[key].Count != 1;

    private EventActor Actor()
    {
        Guid.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId);
        var role = Enum.TryParse<UserRole>(User.FindFirstValue(ClaimTypes.Role), false, out var parsedRole) ? parsedRole : UserRole.Attendee;
        return new(userId, role);
    }

    private bool TryEventData(EventFormViewModel model, out EventData? data)
    {
        data = null;
        if (!TryLocation(model.StreetAddress, model.ProvinceCode, model.WardCode, out var location) ||
            !TryUtc(model.StartAt, nameof(model.StartAt), out var startAt) || !TryUtc(model.EndAt, nameof(model.EndAt), out var endAt)) return false;
        data = new(model.Type!.Value, model.Title, model.Description, location, startAt, endAt, model.Capacity);
        return true;
    }

    private bool TryLocation(string streetAddress, string provinceCode, string wardCode, out string location)
    {
        if (ModelState.IsValid && locations.TryCompose(streetAddress, provinceCode, wardCode, out location)) return true;
        location = "";
        ModelState.AddModelError(nameof(EventFormViewModel.ProvinceCode), "Địa điểm không hợp lệ hoặc vượt quá 200 ký tự");
        return false;
    }

    private EventFormViewModel PopulateLocations(EventFormViewModel model) { model.Provinces = locations.Provinces; return model; }
    private PublishedEventFormViewModel PopulateLocations(PublishedEventFormViewModel model) { model.Provinces = locations.Provinces; return model; }

    private bool TryAgendaData(AgendaFormViewModel model, out AgendaData? data)
    {
        data = null;
        if (!ModelState.IsValid || !TryUtc(model.StartAt, nameof(model.StartAt), out var startAt) || !TryUtc(model.EndAt, nameof(model.EndAt), out var endAt)) return false;
        data = new(model.Title, model.Content, startAt, endAt);
        return true;
    }

    private bool TryUtc(DateTime? value, string field, out DateTime utc)
    {
        utc = default;
        if (value is null) return false;
        var local = DateTime.SpecifyKind(value.Value, DateTimeKind.Unspecified);
        if (local.Ticks % TimeSpan.TicksPerMinute != 0 || VietnamTimeZone.IsInvalidTime(local) || VietnamTimeZone.IsAmbiguousTime(local))
        {
            ModelState.AddModelError(field, "Thời gian phải là giờ Việt Nam hợp lệ và chính xác đến phút");
            return false;
        }
        utc = TimeZoneInfo.ConvertTimeToUtc(local, VietnamTimeZone);
        return true;
    }

    private IActionResult FormError(string view, object model, Error error)
    {
        if (error.Code is "NotFound" or "Forbidden" or "Conflict") return ErrorResult(error);
        ModelState.AddModelError(error.Code is "BadRequest" ? string.Empty : error.Code, error.Message);
        return BadRequestView(view, model);
    }

    private IActionResult ErrorResult(Error error)
    {
        TempData["ErrorMessage"] = error.Message;
        return error.Code switch
        {
            "NotFound" => NotFound(error.Message),
            "Forbidden" => StatusCode(StatusCodes.Status403Forbidden, error.Message),
            "Conflict" => Conflict(error.Message),
            _ => BadRequest(error.Message)
        };
    }

    private ViewResult BadRequestView(string view, object model)
    {
        if (model is EventFormViewModel eventForm) PopulateLocations(eventForm);
        if (model is PublishedEventFormViewModel publishedForm) PopulateLocations(publishedForm);
        Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(view, model);
    }

    private EventFormViewModel ToEventForm(EventDto x)
    {
        var parsed = locations.Parse(x.Location);
        return PopulateLocations(new EventFormViewModel
        {
            Id = x.Id, Type = x.Type, Title = x.Title, Description = x.Description, StreetAddress = parsed.StreetAddress,
            ProvinceCode = parsed.ProvinceCode, WardCode = parsed.WardCode,
            StartAt = ToVietnamTime(x.StartAt), EndAt = ToVietnamTime(x.EndAt), Capacity = x.Capacity
        });
    }

    private PublishedEventFormViewModel ToPublishedForm(EventDto x)
    {
        var parsed = locations.Parse(x.Location);
        return PopulateLocations(new PublishedEventFormViewModel
        {
            Id = x.Id, Title = x.Title, Description = x.Description, StreetAddress = parsed.StreetAddress,
            ProvinceCode = parsed.ProvinceCode, WardCode = parsed.WardCode, OriginalLocation = x.Location, Capacity = x.Capacity
        });
    }

    private EventDetailsViewModel ToDetails(EventDto x)
    {
        var canChangePublished = CanChangePublished(x);
        return new()
        {
            Id = x.Id, OwnerId = x.OwnerId, Type = x.Type, Title = x.Title, Description = x.Description, Location = x.Location,
            StartAt = ToVietnamTime(x.StartAt), EndAt = ToVietnamTime(x.EndAt), Capacity = x.Capacity, Status = x.Status,
            CreatedAt = ToVietnamTime(x.CreatedAt), CancelReason = x.CancelReason,
            CanEditPublished = canChangePublished, CanCancel = canChangePublished, CanManage = x.CanManage,
            Agenda = x.Agenda.Select(a => new AgendaItemViewModel
            {
                Id = a.Id, Title = a.Title, Content = a.Content, StartAt = ToVietnamTime(a.StartAt), EndAt = ToVietnamTime(a.EndAt)
            }).ToList()
        };
    }

    private bool CanChangePublished(EventDto item) => item.CanManage && item.Status == EffectiveEventStatus.Published && clock.UtcNow < item.StartAt;
    private static DateTime ToVietnamTime(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), VietnamTimeZone);
    private static TimeZoneInfo FindVietnamTimeZone()
    {
        foreach (var id in new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        throw new TimeZoneNotFoundException("Không tìm thấy múi giờ Asia/Ho_Chi_Minh");
    }
}
