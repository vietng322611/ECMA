using EMCA.Application.Interfaces;
using EMCA.Application.Services;
using ECMA.Infrastructure;
using ECMA.Infrastructure.Data;
using ECMA.Web.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

var builder = WebApplication.CreateBuilder(args);

if (Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") != "true")
{
    var envFile = FindFileUpward(builder.Environment.ContentRootPath, ".env");
    if (envFile is not null)
    {
        SetFromEnvFileIfMissing("ConnectionStrings:ECMA", "ECMA_CONNECTION_STRING");
        SetFromEnvFileIfMissing("Seed:AdminUsername", "SEED_ADMIN_USERNAME");
        SetFromEnvFileIfMissing("Seed:AdminFullName", "SEED_ADMIN_FULL_NAME");
        SetFromEnvFileIfMissing("Seed:AdminPassword", "SEED_ADMIN_PASSWORD");
    }

    void SetFromEnvFileIfMissing(string configurationKey, string envKey)
    {
        var value = ReadEnvValue(envFile, envKey);
        if (string.IsNullOrWhiteSpace(builder.Configuration[configurationKey]) && !string.IsNullOrWhiteSpace(value))
            builder.Configuration[configurationKey] = value;
    }
}

builder.Services.AddControllersWithViews();
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<IEventService, EventService>();
builder.Services.AddScoped<PublicEventService>();
builder.Services.AddScoped<RegistrationService>();
builder.Services.AddScoped<AuthCookieService>();
builder.Services.AddScoped<AuthSessionStore>();
builder.Services.AddSingleton(new VietnamLocationCatalog(builder.Environment.ContentRootPath));

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.Cookie.HttpOnly = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.ExpireTimeSpan = TimeSpan.FromDays(1);
        options.SlidingExpiration = false;
        options.LoginPath = "/account/login";
        options.LogoutPath = "/account/logout";
        options.AccessDeniedPath = "/account/access-denied";
        options.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = context =>
                context.HttpContext.RequestServices.GetRequiredService<AuthCookieService>().ValidateAsync(context),
            OnRedirectToLogin = context =>
            {
                var returnUrl = ReturnUrlPolicy.Canonicalize(context.Request.PathBase + context.Request.Path + context.Request.QueryString);
                context.HttpContext.RequestServices.GetRequiredService<ITempDataDictionaryFactory>()
                    .GetTempData(context.HttpContext)["ErrorMessage"] = "Vui lòng đăng nhập bằng tài khoản đang hoạt động";
                context.Response.Redirect(context.Options.LoginPath + (returnUrl is null ? "" : "?returnUrl=" + Uri.EscapeDataString(returnUrl)));
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

if (args.Contains("--seed", StringComparer.Ordinal))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AdminSeeder>().SeedAsync(
        app.Configuration["Seed:AdminUsername"],
        app.Configuration["Seed:AdminFullName"],
        app.Configuration["Seed:AdminPassword"]);
    return;
}

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.Use(async (context, next) =>
{
    if (context.Request.Path.Value?.Any(char.IsUpper) == true)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllers().WithStaticAssets();

app.Run();

static string? FindFileUpward(string startPath, string fileName)
{
    for (var directory = new DirectoryInfo(startPath); directory is not null; directory = directory.Parent)
    {
        var path = Path.Combine(directory.FullName, fileName);
        if (File.Exists(path)) return path;
    }

    return null;
}

static string? ReadEnvValue(string path, string key)
{
    foreach (var line in File.ReadLines(path))
    {
        var trimmed = line.Trim();
        if (trimmed.Length == 0 || trimmed.StartsWith('#')) continue;

        var separator = trimmed.IndexOf('=');
        if (separator < 1 || !trimmed[..separator].Trim().Equals(key, StringComparison.Ordinal)) continue;

        return trimmed[(separator + 1)..].Trim().Trim('"', '\'');
    }

    return null;
}