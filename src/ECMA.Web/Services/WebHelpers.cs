using System.Security.Claims;
using ECMA.Domain.Results;
using ECMA.Web.Models;
using EMCA.Application.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace ECMA.Web.Services;

public static class VietnamTime
{
    private static readonly TimeZoneInfo Zone = Find();

    public static DateTime ToLocal(DateTime utc) => TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(utc, DateTimeKind.Utc), Zone);
    public static string Format(DateTime utc, string format = "HH:mm, dd/MM/yyyy") => ToLocal(utc).ToString(format);

    private static TimeZoneInfo Find()
    {
        foreach (var id in new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" })
            try { return TimeZoneInfo.FindSystemTimeZoneById(id); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        throw new TimeZoneNotFoundException("Không tìm thấy múi giờ Asia/Ho_Chi_Minh");
    }
}

// SRS §4.2: a malformed target ID returns 400 "Dữ liệu không hợp lệ"; [Authorize] still runs first.
public sealed class ValidateGuidRouteValuesAttribute : ActionFilterAttribute
{
    public override void OnActionExecuting(ActionExecutingContext context)
    {
        foreach (var parameter in context.ActionDescriptor.Parameters.Where(x => x.ParameterType == typeof(Guid)))
        {
            var value = context.RouteData.Values[parameter.Name]?.ToString();
            if (value is not null && !Guid.TryParse(value, out _))
            {
                context.Result = context.Controller is Controller controller
                    ? controller.StatusView(StatusCodes.Status400BadRequest, "Dữ liệu không hợp lệ")
                    : new BadRequestObjectResult("Dữ liệu không hợp lệ");
                return;
            }
        }
    }
}

public static class ControllerExtensions
{
    public static Guid? CurrentUserId(this Controller controller) =>
        Guid.TryParse(controller.User.FindFirstValue(ClaimTypes.NameIdentifier), out var id) ? id : null;

    // Raw query value for QueryParameters: null when absent, MultipleValues when repeated.
    public static string? RawQuery(this Controller controller, string key) =>
        !controller.Request.Query.TryGetValue(key, out var values) ? null : values.Count == 1 ? values[0] : QueryParameters.MultipleValues;

    public static ViewResult StatusView(this Controller controller, Error error, string? backUrl = null, string? backLabel = null) =>
        controller.StatusView(error.Code switch
        {
            "NotFound" => StatusCodes.Status404NotFound,
            "Forbidden" => StatusCodes.Status403Forbidden,
            "Conflict" => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status400BadRequest
        }, error.Message, backUrl, backLabel);

    public static ViewResult StatusView(this Controller controller, int statusCode, string message, string? backUrl = null, string? backLabel = null)
    {
        controller.Response.StatusCode = statusCode;
        return controller.View("StatusMessage", new StatusMessageViewModel
        {
            StatusCode = statusCode, Message = message, BackUrl = backUrl ?? "/", BackLabel = backLabel ?? "Về trang tra cứu sự kiện"
        });
    }
}

public static class Labels
{
    public static string Type(ECMA.Domain.Enums.EventType type) => type == ECMA.Domain.Enums.EventType.Conference ? "Hội nghị" : "Sự kiện";

    public static string Status(ECMA.Domain.Enums.EffectiveEventStatus status) => status switch
    {
        ECMA.Domain.Enums.EffectiveEventStatus.Draft => "Bản nháp",
        ECMA.Domain.Enums.EffectiveEventStatus.Published => "Đã công bố",
        ECMA.Domain.Enums.EffectiveEventStatus.Cancelled => "Đã hủy",
        _ => "Đã kết thúc"
    };

    public static string Availability(EMCA.Application.DTOs.Availability availability) => availability switch
    {
        EMCA.Application.DTOs.Availability.Open => "Còn chỗ",
        EMCA.Application.DTOs.Availability.Full => "Đủ chỗ",
        _ => "Đã đóng đăng ký"
    };

    public static string Registration(ECMA.Domain.Enums.RegistrationStatus status) =>
        status == ECMA.Domain.Enums.RegistrationStatus.Confirmed ? "Đang đăng ký" : "Đã hủy";

    public static string Source(ECMA.Domain.Enums.CancelSource? source) => source switch
    {
        ECMA.Domain.Enums.CancelSource.Self => "Người tham dự tự hủy",
        ECMA.Domain.Enums.CancelSource.Manager => "Ban tổ chức hủy",
        ECMA.Domain.Enums.CancelSource.Event => "Sự kiện bị hủy",
        _ => ""
    };
}
