# Architecture overview

## Observed baseline

The solution contains one `Microsoft.NET.Sdk.Web` project, `ECMA.csproj`, targeting `net10.0`, with nullable reference types and implicit usings enabled. There are no PackageReferences or SDK pin (`global.json`). Inspection on 2026-10-03 found SDK 10.0.203 and ASP.NET Core runtime 10.0.7. Docker uses SDK/runtime 10.0 image tags; Compose defines only the application, not PostgreSQL.

Current request flow is Browser → ASP.NET Core MVC → `HomeController` → Razor Views. `Index`, `Privacy`, and `Error` are template pages, not product features.

`Program.cs` registers `AddControllersWithViews`, builds the app, configures production exception handling at `/Home/Error` and HSTS, HTTPS redirection, routing, authorization middleware, `MapStaticAssets`, and conventional `{controller=Home}/{action=Index}/{id?}` routing with `.WithStaticAssets()`. Preserve these modern .NET 10 static-asset conventions.

## Required direction, not yet implemented

Browser → MVC Controllers → business services when needed → EF Core DbContext → PostgreSQL. Controllers return server-rendered Razor Views; a separate frontend/API application is not required. [Decision 0001](../decisions/0001-server-rendered-mvc.md) resolves the SRS architecture conflict in favor of the explicit project brief.

Add services only for meaningful business logic. Use the same web project until a concrete requirement justifies another boundary. Persistence, accounts, role management, background jobs, mock payment, and all domain features remain unimplemented.

## Cross-cutting concerns

| Concern | Existing implementation | Required direction |
|---|---|---|
| DI | MVC registered in `Program.cs` | Constructor injection; scoped DbContext/services as appropriate |
| Authentication | None; no authentication registration/middleware | Accepted cookie authentication and database-stored Identity `PasswordHasher<TUser>` hashes; see decision 0002 |
| Authorization | `UseAuthorization`, no protected actions/policies | Single role plus resource ownership/event assignment; immediate role/status changes |
| Validation | Unobtrusive validation partial available; no input forms | ViewModel DataAnnotations, server `ModelState`, service business rules |
| Errors | Non-development exception handler, request ID error ViewModel | Safe errors without stack traces; log unexpected failures |
| Logging | Built-in log-level configuration only | `ILogger<T>`; important operations per NFR-14, no secrets/passwords |
| Time/jobs | None | Testable time source; minute-based idempotent jobs per NFR-11/13 |
| Tests/CI | No test projects, CI, seed/reset scripts | Focused requirement-linked checks; PostgreSQL proof for database concurrency |

The SRS excludes a standalone audit-log feature but requires operational logs and check-in/undo records. Do not confuse these obligations or build a general audit subsystem.

See [product context](../product/agent-context.md) for feature dependencies and unresolved business rules.

Styling direction is Razor with locally compiled Tailwind CSS v4 per [decision 0003](../decisions/0003-razor-tailwind-styling.md). Bun's `package.json`/`bun.lock` and Tailwind CLI packages now exist; the layout still uses Bootstrap and no Tailwind build is configured. These asset dependencies do not change the server-rendered architecture.