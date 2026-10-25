using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ECMA.Web.Models;
using ECMA.Web.Services;
using EMCA.Application.DTOs;
using EMCA.Application.Services;

namespace ECMA.Web.Controllers;

[Route("")]
public class HomeController(PublicEventService publicEvents) : Controller
{
    // FR-SRCH-01: public event search for Guests and every role.
    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var request = new PublicEventSearchRequest(this.RawQuery("keyword"), this.RawQuery("type"), this.RawQuery("from"), this.RawQuery("to"), this.RawQuery("page"));
        var result = await publicEvents.SearchAsync(request, HttpContext.RequestAborted);
        if (!result.IsSuccess) Response.StatusCode = StatusCodes.Status400BadRequest;
        return View(new PublicEventSearchViewModel
        {
            Keyword = Single(request.Keyword), Type = Single(request.Type), From = Single(request.From), To = Single(request.To),
            Page = result.Value?.Page ?? 1, ErrorMessage = result.Error?.Message, Events = result.Value?.Items ?? []
        });
    }

    private static string? Single(string? raw) => raw == QueryParameters.MultipleValues ? null : raw;

    [HttpGet("privacy")]
    public IActionResult Privacy()
    {
        return View();
    }

    [HttpGet("error"), ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}