using ECMA.Domain.Enums;
using ECMA.Web.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECMA.Web.Controllers;

[Authorize(Roles = "Admin"), ValidateGuidRouteValues]
[Route("admin/users")]
public sealed class UsersController(UserManagementService service) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(string? keyword, string? role, string? status, int page = 1)
    {
        if (new[] { "keyword", "role", "status", "page" }.Any(k => Request.Query.TryGetValue(k, out var values) && values.Count > 1))
            return BadRequest("Bộ lọc không hợp lệ");
        var result = await service.ListAsync(this.CurrentUserId() ?? Guid.Empty, keyword, role, status, page, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!);
        return View(new UsersPage { Items = result.Value!, Keyword = keyword, Role = role, Status = status, Page = page });
    }

    [HttpPost("{id}/update"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Update(Guid id, string? role, string? status)
    {
        if (!Enum.TryParse<UserRole>(role, false, out var newRole) || !Enum.GetNames<UserRole>().Contains(role, StringComparer.Ordinal) ||
            !Enum.TryParse<UserStatus>(status, false, out var newStatus) || !Enum.GetNames<UserStatus>().Contains(status, StringComparer.Ordinal))
            return BadRequest("Dữ liệu không hợp lệ");
        var result = await service.ChangeAsync(this.CurrentUserId() ?? Guid.Empty, id, newRole, newStatus, HttpContext.RequestAborted);
        if (!result.IsSuccess) return this.StatusView(result.Error!, "/admin/users", "Quản lý người dùng");
        TempData["SuccessMessage"] = "Đã cập nhật tài khoản";
        return RedirectToAction(nameof(Index));
    }
}

public sealed class UsersPage
{
    public IReadOnlyList<UserListRow> Items { get; init; } = [];
    public string? Keyword { get; init; }
    public string? Role { get; init; }
    public string? Status { get; init; }
    public int Page { get; init; }
}
